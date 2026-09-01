<#
.SYNOPSIS
    Validates providers.json and checks that every listed server still answers as an NNTP server.

.DESCRIPTION
    The provider list goes stale silently: a provider shuts down, or moves off a port, and the only
    symptom is a user staring at a connect dialog that hangs. KPN did exactly this, and port 80 for
    5 Euro Usenet and SnelNL accepts the TCP connection while never sending a greeting - so a plain
    port check is not enough. This reads the NNTP greeting banner, which is the only evidence that
    something on the far end is really a news server.

    Structural rules here mirror Spotnet.Model.ProviderCatalogue, which is what the client enforces
    before it will use a published catalogue. Keep the two in step.

.PARAMETER Path
    providers.json. Defaults to the copy in the repository root.

.PARAMETER Retries
    Attempts per endpoint before it is called a failure. Guards against a flaky runner.

.EXAMPLE
    pwsh tools/Test-Providers.ps1
#>
[CmdletBinding()]
param(
    [string] $Path = (Join-Path $PSScriptRoot '..\providers.json'),
    [int] $Retries = 3,
    [int] $TimeoutSeconds = 12
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls11

$AllowedPorts = @(563, 443, 119, 80)
$AllowedGroups = @('NL', 'INT')
$HostPattern = '^(?=.{1,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$'

function Read-Greeting {
    param([string] $Server, [int] $Port)

    # 563 and 443 are TLS; 119 and 80 are plaintext.
    $useTls = $Port -eq 563 -or $Port -eq 443
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $connect = $client.BeginConnect($Server, $Port, $null, $null)
        if (-not $connect.AsyncWaitHandle.WaitOne($TimeoutSeconds * 1000)) { return @{ Ok = $false; Detail = 'connect timed out' } }
        $client.EndConnect($connect)
        $stream = $client.GetStream()
        if ($useTls) {
            $callback = { $true } -as [Net.Security.RemoteCertificateValidationCallback]
            $tls = New-Object System.Net.Security.SslStream($stream, $false, $callback)
            $tls.AuthenticateAsClient($Server)
            $stream = $tls
        }
        $stream.ReadTimeout = $TimeoutSeconds * 1000
        $buffer = New-Object byte[] 512
        $read = $stream.Read($buffer, 0, $buffer.Length)
        if ($read -le 0) { return @{ Ok = $false; Detail = 'connected but sent no greeting' } }
        $banner = ([Text.Encoding]::ASCII.GetString($buffer, 0, $read)).Trim()
        # 200 = posting allowed, 201 = read-only. Anything else is a refusal, including the 500 that
        # a retired server sends to explain it has stopped.
        if ($banner -notmatch '^(200|201)\b') { return @{ Ok = $false; Detail = "refused: $banner" } }
        return @{ Ok = $true; Detail = $banner }
    }
    catch { return @{ Ok = $false; Detail = $_.Exception.Message.Split("`n")[0] } }
    finally { $client.Close() }
}

function Test-Endpoint {
    param([string] $Server, [int] $Port)

    for ($attempt = 1; $attempt -le $Retries; $attempt++) {
        $result = Read-Greeting -Server $Server -Port $Port
        if ($result.Ok) { return $result }
        # A refusal is a definite answer; only retry when the network itself misbehaved.
        if ($result.Detail -like 'refused:*' -or $result.Detail -like '*sent no greeting*') { return $result }
        if ($attempt -lt $Retries) { Start-Sleep -Seconds 2 }
    }
    return $result
}

if (-not (Test-Path -LiteralPath $Path)) { throw "Not found: $Path" }
$raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
if ([Text.Encoding]::UTF8.GetByteCount($raw) -gt 128KB) { throw 'providers.json exceeds the 128 KB cap the client enforces.' }

$document = $raw | ConvertFrom-Json
if ($document.schema -ne 1) { throw "Unsupported schema: $($document.schema)" }
if (-not $document.providers) { throw 'providers.json lists no providers.' }

$structural = New-Object Collections.Generic.List[string]
$names = New-Object Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
$headerHosts = New-Object Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
$endpoints = New-Object Collections.Generic.List[object]

foreach ($provider in $document.providers) {
    $name = $provider.name
    if ([string]::IsNullOrWhiteSpace($name)) { $structural.Add('An entry has no name.'); continue }
    if (-not $names.Add($name)) { $structural.Add("Duplicate name: $name") }
    if ($AllowedGroups -notcontains $provider.group) { $structural.Add("$name has group '$($provider.group)'; expected NL or INT.") }

    $port = [int] $provider.port
    if ($AllowedPorts -notcontains $port) { $structural.Add("$name uses port $port; allowed: $($AllowedPorts -join ', ')") }

    $roles = [ordered]@{ download = @($provider.host, $port) }
    $uploadHost = if ($provider.PSObject.Properties['upload']) { $provider.upload } else { $provider.host }
    $headerHost = if ($provider.PSObject.Properties['headers']) { $provider.headers } else { $provider.host }
    $uploadPort = if ($provider.PSObject.Properties['uploadPort']) { [int] $provider.uploadPort } else { $port }
    $headerPort = if ($provider.PSObject.Properties['headersPort']) { [int] $provider.headersPort } else { $port }
    $roles['upload'] = @($uploadHost, $uploadPort)
    $roles['headers'] = @($headerHost, $headerPort)

    if (-not $headerHosts.Add($headerHost)) { $structural.Add("Duplicate headers server: $headerHost") }

    foreach ($role in $roles.Keys) {
        $server = $roles[$role][0]
        $rolePort = [int] $roles[$role][1]
        if ([string]::IsNullOrWhiteSpace($server) -or $server.ToLowerInvariant() -notmatch $HostPattern) {
            $structural.Add("$name has an invalid $role server: $server")
            continue
        }
        if ($AllowedPorts -notcontains $rolePort) { $structural.Add("$name uses $role port $rolePort.") ; continue }
        # The same host:port can serve several roles; probe each distinct pair once.
        if (-not ($endpoints | Where-Object { $_.Server -eq $server -and $_.Port -eq $rolePort })) {
            $endpoints.Add([pscustomobject]@{ Name = $name; Role = $role; Server = $server; Port = $rolePort })
        }
    }
}

if ($structural.Count -gt 0) {
    Write-Host 'Structural problems in providers.json:' -ForegroundColor Red
    $structural | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
Write-Host "providers.json is structurally valid: $($document.providers.Count) providers, $($endpoints.Count) distinct endpoints." -ForegroundColor Green

$failures = New-Object Collections.Generic.List[string]
foreach ($endpoint in $endpoints) {
    $result = Test-Endpoint -Server $endpoint.Server -Port $endpoint.Port
    $label = '{0,-22} {1,-30} {2,-5}' -f $endpoint.Name, $endpoint.Server, $endpoint.Port
    if ($result.Ok) {
        Write-Host "OK   $label $($result.Detail)" -ForegroundColor Green
    }
    else {
        Write-Host "FAIL $label $($result.Detail)" -ForegroundColor Red
        $failures.Add("$($endpoint.Name) ($($endpoint.Server):$($endpoint.Port)) - $($result.Detail)")
    }
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host "$($failures.Count) endpoint(s) did not answer as an NNTP server:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host 'Correct or remove these entries in providers.json.' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host "All $($endpoints.Count) endpoints answered." -ForegroundColor Green
