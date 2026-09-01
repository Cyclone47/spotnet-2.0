[CmdletBinding()]
param(
    [string]$CompilerPath,
    [switch]$BootstrapCompiler,
    [switch]$SkipBuild,

    # Authenticode signing, opt-in. Give a certificate thumbprint from the current user's
    # store, or a complete command for anything else (HSM, cloud signing service). No
    # password ever passes through this script: a PFX belongs in the certificate store
    # first, and its thumbprint is what comes here.
    [string]$SignThumbprint,
    [string]$SignCommand,
    [string]$SignTimestampUrl = 'http://timestamp.digicert.com',
    [string]$SignToolPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts\installer'
$toolRoot = Join-Path $repoRoot 'artifacts\installer-tools'
New-Item -ItemType Directory -Force -Path $artifactRoot, $toolRoot | Out-Null

function Resolve-SignTool {
    if ($SignToolPath) {
        if (-not (Test-Path -LiteralPath $SignToolPath)) { throw "signtool.exe not found at $SignToolPath." }
        return $SignToolPath
    }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $found = Get-ChildItem -LiteralPath $kits -Filter 'signtool.exe' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -like '*\x64' } |
        Sort-Object -Property FullName -Descending |
        Select-Object -First 1
    if (-not $found) { throw 'signtool.exe was not found. Install the Windows SDK signing tools or pass -SignToolPath.' }
    return $found.FullName
}

# The command Inno Setup runs per file, with $f standing in for the file name. The same
# template drives the payload signing below, so both use exactly one code path.
function Get-SignTemplate {
    if ($SignThumbprint -and $SignCommand) {
        throw 'Give either -SignThumbprint or -SignCommand, not both.'
    }
    if ($SignCommand) {
        if ($SignCommand -notmatch '\$f') { throw '-SignCommand must contain $f, which is replaced by the file to sign.' }
        return $SignCommand
    }
    if (-not $SignThumbprint) { return $null }
    if ($SignThumbprint -notmatch '^[0-9A-Fa-f]{40}$') { throw '-SignThumbprint must be a 40-character SHA1 certificate thumbprint.' }
    $tool = Resolve-SignTool
    # RFC 3161 timestamping, so signatures outlive the certificate.
    return ('"{0}" sign /fd sha256 /td sha256 /tr "{1}" /sha1 {2} $f' -f $tool, $SignTimestampUrl, $SignThumbprint)
}

function Invoke-SignFile([string]$Template, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Cannot sign a file that does not exist: $Path" }
    $command = $Template.Replace('$f', '"' + (Resolve-Path -LiteralPath $Path).Path + '"')
    & cmd.exe /c $command
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $Path (exit $LASTEXITCODE)." }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -eq 'NotSigned') { throw "Signing reported success but $Path carries no signature." }
}

function Get-SignedDownload([string]$Uri, [string]$Path, [string]$Publisher) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Host "Downloading $(Split-Path $Path -Leaf)..."
        Invoke-WebRequest -Uri $Uri -OutFile $Path -UseBasicParsing
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch $Publisher) {
        throw "Publisher signature validation failed for $Path. File will not be executed or packaged."
    }
}

if (-not $CompilerPath) {
    $candidates = @(
        (Join-Path $toolRoot 'InnoSetup7\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { $CompilerPath = $candidate; break }
    }
}
if (-not $CompilerPath -and $BootstrapCompiler) {
    $download = Join-Path $toolRoot 'innosetup-7.1.0-x64.exe'
    Get-SignedDownload 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' $download 'CN=Pyrsys B\.V\.'
    $compilerDirectory = Join-Path $toolRoot 'InnoSetup7'
    $process = Start-Process -FilePath $download -ArgumentList @('/PORTABLE=1', '/CURRENTUSER', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/DIR="' + $compilerDirectory + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Inno Setup bootstrap failed: $($process.ExitCode)" }
    $CompilerPath = Join-Path $compilerDirectory 'ISCC.exe'
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath)) {
    throw 'Install Inno Setup 7.1+, supply -CompilerPath, or use -BootstrapCompiler for a verified portable compiler under artifacts/.'
}

Push-Location $repoRoot
try {
    if (-not $SkipBuild) {
        & dotnet build reconstructed/Spotnet2/Spotnet.sln -c Release -v minimal
        if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
        & dotnet test reconstructed/Spotnet2/Spotnet.Tests/Spotnet.Tests.csproj -c Release --no-build -v minimal
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed; refusing to package.' }
    }
    & dotnet build tools/Spotnet.SetupHelper/Spotnet.SetupHelper.csproj -c Release -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Migration helper build failed.' }
    $appOutput = Join-Path $repoRoot 'reconstructed\Spotnet2\Spotnet\bin\Release\net472'
    $helperOutput = Join-Path $repoRoot 'tools\Spotnet.SetupHelper\bin\Release\net472'
    foreach ($binary in @('Spotnet.exe', 'Spotnet.Enc.dll', 'WebView2Loader.dll', 'x64\SQLite.Interop.dll', 'libvlc\win-x64\libvlc.dll')) {
        $binaryPath = Join-Path $appOutput $binary
        $bytes = [IO.File]::ReadAllBytes($binaryPath)
        $peOffset = [BitConverter]::ToInt32($bytes, 60)
        if ([BitConverter]::ToUInt16($bytes, $peOffset + 4) -ne 0x8664) { throw "Not an AMD64 binary: $binary" }
    }
    # A fresh staging directory prevents stale DLLs or personal runtime data entering the package.
    $payload = Join-Path $artifactRoot ('payload-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $payload | Out-Null
    $trackedAssets = @(git ls-files -- reconstructed/Spotnet2/Spotnet/Data reconstructed/Spotnet2/Spotnet/Resources/ReleaseNotes)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate tracked application assets.' }
    foreach ($file in Get-ChildItem -LiteralPath $appOutput -File) {
        if ($file.Extension -in @('.dll', '.exe') -or $file.Name -eq 'Spotnet.exe.config') {
            if ($file.Name -match '^(Awesomium|awesomium|Meta\.Vlc|Ionic\.Zip|Pri\.LongPath)' -or $file.Name -eq 'Squirrel.exe') { continue }
            Copy-Item -LiteralPath $file.FullName -Destination $payload
        }
    }
    foreach ($directory in @('x64', 'libvlc\win-x64')) {
        $target = Join-Path $payload (Split-Path $directory -Parent)
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        Copy-Item -LiteralPath (Join-Path $appOutput $directory) -Destination $target -Recurse
    }
    foreach ($asset in $trackedAssets) {
        $relative = $asset.Substring('reconstructed/Spotnet2/Spotnet/'.Length)
        $destination = Join-Path $payload $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot $asset) -Destination $destination
    }
    # Preserve satellite resource assemblies, if generated by the application build.
    foreach ($culture in @('nl', 'en', 'en-US')) {
        $cultureDir = Join-Path $appOutput $culture
        if (Test-Path -LiteralPath $cultureDir) { Copy-Item -LiteralPath $cultureDir -Destination $payload -Recurse }
    }
    $webview = Join-Path $toolRoot 'MicrosoftEdgeWebview2Setup.exe'
    Get-SignedDownload 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' $webview 'O=Microsoft Corporation'

    # Sign what this repository produces. The third-party assemblies arrive signed by
    # their own publishers and are left alone.
    $signTemplate = Get-SignTemplate
    $compilerArguments = @('/Q', ('/DPayloadDir=' + $payload), ('/DHelperDir=' + $helperOutput), ('/DWebViewBootstrapper=' + $webview), ('/DOutputDir=' + $artifactRoot))
    if ($signTemplate) {
        $ourBinaries = @('Spotnet.exe', 'Spotnet.Enc.dll') |
            ForEach-Object { Join-Path $payload $_ }
        $ourBinaries += Get-ChildItem -LiteralPath $payload -Filter 'Spotnet.resources.dll' -Recurse |
            Select-Object -ExpandProperty FullName
        $ourBinaries += (Join-Path $helperOutput 'Spotnet.SetupHelper.exe')
        foreach ($binary in $ourBinaries) {
            Write-Host "Signing $(Split-Path $binary -Leaf)..."
            Invoke-SignFile $signTemplate $binary
        }
        # Inno signs the installer and the uninstaller with the same named tool.
        $compilerArguments += '/DSignSetup'
        $compilerArguments += ('/Sspotnet=' + $signTemplate)
    }

    & $CompilerPath @compilerArguments installer/Spotnet3.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $setupFile = Join-Path $artifactRoot 'Spotnet-3.0-x64-Setup.exe'
    $hash = (Get-FileHash -LiteralPath $setupFile -Algorithm SHA256).Hash
    "$hash  Spotnet-3.0-x64-Setup.exe" | Set-Content -LiteralPath ($setupFile + '.sha256') -Encoding ASCII
    Write-Host "Built: $setupFile"
    Write-Host "SHA256: $hash"
    if ($signTemplate) {
        $signature = Get-AuthenticodeSignature -LiteralPath $setupFile
        if ($signature.Status -eq 'NotSigned') { throw 'The installer was built but carries no signature.' }
        Write-Host ("Signed by: " + $signature.SignerCertificate.Subject)
        Write-Host ("Signature status: " + $signature.Status)
        if ($signature.Status -ne 'Valid') {
            Write-Warning 'The signature is present but does not chain to a trusted root on this machine. That is expected for a test certificate; a real publisher certificate will validate.'
        }
    }
    else {
        Write-Warning 'The Spotnet installer is unsigned. Pass -SignThumbprint or -SignCommand to sign it. No installer was run against your profile.'
    }
}
finally { Pop-Location }
