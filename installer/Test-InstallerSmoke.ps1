[CmdletBinding()]
param([string]$TestRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $TestRoot) { $TestRoot = Join-Path $repoRoot 'artifacts\installer-smoke' }
$testRoot = [IO.Path]::GetFullPath($TestRoot)
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + '\'
if (-not $testRoot.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Smoke-test root must be under repository artifacts.' }
$testInstaller = Join-Path $repoRoot 'artifacts\installer\Spotnet-3.0-x64-Setup-smoke.exe'
if (Get-Process Spotnet -ErrorAction SilentlyContinue) { throw 'Close Spotnet before the smoke test. This test never closes the real application.' }
if (-not (Test-Path -LiteralPath $testInstaller)) { throw 'Compile Spotnet3.iss with /DSmokeTestRoot matching -TestRoot first.' }
if (Test-Path -LiteralPath $testRoot) { throw 'Smoke-test directory already exists. Retain or relocate the previous test artifacts before a fresh run.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$appRoot = Join-Path $testRoot 'App'
$profileRoot = Join-Path $testRoot 'Profile\Data'
$desktopRoot = Join-Path $testRoot 'Desktop'
$programsRoot = Join-Path $testRoot 'Programs'
function Write-TestLink([string]$Path, [string]$Target, [string]$Arguments = '') {
    New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent) | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($Path)
    $link.TargetPath = $Target
    $link.Arguments = $Arguments
    $link.Save()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
}
function Assert-TestLink([string]$Path, [string]$Target) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing shortcut $Path" }
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($Path)
    $actual = $link.TargetPath
    $arguments = $link.Arguments
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    if ($actual -ne $Target -or $arguments -ne '') { throw "Incorrect shortcut target/arguments: $Path" }
}
function Invoke-SmokeSetup([string]$LogName) {
    $process = Start-Process -FilePath $testInstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/FRESH=1', ('/DIR="' + $appRoot + '"'), ('/LOG="' + (Join-Path $testRoot $LogName) + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Smoke setup failed ($($process.ExitCode)); inspect $LogName." }
}
Invoke-SmokeSetup 'fresh.log'
foreach ($required in @('Spotnet.exe', 'Spotnet.install', 'WebView2Loader.dll', 'x64\SQLite.Interop.dll', 'libvlc\win-x64\libvlc.dll', 'Data\TabThemes')) {
    if (-not (Test-Path -LiteralPath (Join-Path $appRoot $required))) { throw "Missing payload: $required" }
}
if (-not (Test-Path -LiteralPath (Join-Path $profileRoot 'profile.ready'))) { throw 'Fresh profile was not initialized.' }
$freshLinks = @((Join-Path $desktopRoot 'Spotnet.lnk'), (Join-Path $programsRoot 'Spotnet.lnk'))
foreach ($link in $freshLinks) { Assert-TestLink $link (Join-Path $appRoot 'Spotnet.exe') }
# Seed legacy and earlier 3.0 links only inside the synthetic shell folders.
$oldLink = Join-Path $desktopRoot 'My old Spotnet.lnk'
$currentLink = Join-Path $desktopRoot 'Spotnet 3.0.lnk'
$squirrelLink = Join-Path $programsRoot 'Spotnet\Spotnet 2.0.lnk'
$unrelatedLink = Join-Path $desktopRoot 'Unrelated.lnk'
Write-TestLink $oldLink (Join-Path $testRoot 'Legacy\Spotnet.exe')
Write-TestLink $currentLink (Join-Path $testRoot 'Previous3\Spotnet.exe')
Write-TestLink $squirrelLink (Join-Path $testRoot 'Spotnet\Update.exe') '--processStart Spotnet.exe'
Write-TestLink $unrelatedLink (Join-Path $testRoot 'Other.exe')
$originalHashes = @{}
foreach ($link in @($oldLink, $currentLink, $squirrelLink, $unrelatedLink)) { $originalHashes[$link] = (Get-FileHash -LiteralPath $link).Hash }
$fixture = Join-Path $profileRoot 'smoke-personal-data.txt'
'preserve-this-test-fixture' | Set-Content -LiteralPath $fixture
$fixtureHash = (Get-FileHash -LiteralPath $fixture).Hash
Invoke-SmokeSetup 'upgrade.log'
foreach ($link in @($oldLink, $currentLink, $squirrelLink) + $freshLinks) { Assert-TestLink $link (Join-Path $appRoot 'Spotnet.exe') }
if ((Get-ChildItem -LiteralPath $desktopRoot -Filter '*.lnk').Count -ne 4) { throw 'Duplicate desktop launcher created.' }
if ((Get-FileHash -LiteralPath $unrelatedLink).Hash -ne $originalHashes[$unrelatedLink]) { throw 'Unrelated shortcut changed.' }
if ((Get-FileHash -LiteralPath $fixture).Hash -ne $fixtureHash) { throw 'Upgrade changed existing profile data.' }
$backups = @(Get-ChildItem (Join-Path $testRoot 'Profile\Backups') -Directory)
if ($backups.Count -ne 1) { throw 'Expected one pre-upgrade backup.' }
if ((Get-FileHash -LiteralPath (Join-Path $backups[0].FullName 'smoke-personal-data.txt')).Hash -ne $fixtureHash) { throw 'Backup does not match.' }
$uninstaller = Join-Path $appRoot 'unins000.exe'
$process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $testRoot 'uninstall.log') + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Uninstall failed: $($process.ExitCode)" }
if (Test-Path -LiteralPath (Join-Path $appRoot 'Spotnet.exe')) { throw 'Application was not uninstalled.' }
if ((Get-FileHash -LiteralPath $fixture).Hash -ne $fixtureHash) { throw 'Uninstall altered profile data.' }
if (-not (Test-Path -LiteralPath $backups[0].FullName)) { throw 'Uninstall removed the backup.' }
foreach ($link in $freshLinks) { if (Test-Path -LiteralPath $link) { throw 'Installer-created shortcut survived uninstall.' } }
foreach ($link in $originalHashes.Keys) { if ((Get-FileHash -LiteralPath $link).Hash -ne $originalHashes[$link]) { throw "Original shortcut was not restored: $link" } }
Write-Host 'PASS: fresh install, payloads, old/current/Squirrel shortcut replacement, no duplicates, unrelated links preserved, upgrade backup, uninstall restoring shortcuts and retaining data.'
Write-Host "Logs and synthetic profile retained in $testRoot. Real installations and profiles were untouched."
