# Spotnet 3.0 x64 installer

The installer is built with Inno Setup 7 and installs **for the current Windows user**. Both Setup and Spotnet are x64 executables. Windows 10/11 on x64 hardware and .NET Framework 4.7.2 or newer are required; this package does not enable native ARM64 support.

## Run Setup

[Download Setup and its checksum from the GitHub release](https://github.com/Cyclone47/spotnet-3.0/releases/tag/v3.0.3). Local build output: `artifacts/installer/Spotnet-3.0-x64-Setup.exe`.

1. Run Setup under the Windows account that owns the old Spotnet profile; do not switch to a different administrator account.
2. Confirm the installation folder, normally `%LOCALAPPDATA%\Programs\Spotnet3`.
3. For migration, select the detected legacy **data folder** and the matching **preferences file**. If there are several candidates, inspect the paths and select one. Setup does not merge profiles or arbitrarily choose the newest settings file.
4. Review the summary. Setup requests a graceful Spotnet exit and waits up to 30 seconds. It uses the existing tray-safe exit command, with a normal close-window fallback. If Spotnet does not exit, or belongs to another Windows session, Setup stops; it never force-kills Spotnet.
   During shutdown, prerequisite handling, and profile copying/verification, Setup shows a dedicated progress page and the current operation. A large profile can still take several minutes, but the wizard no longer leaves the generic Preparing page blank.
5. Missing WebView2 is installed using Microsoft's signed Evergreen bootstrapper. Internet access is needed for that step. Missing .NET Framework is reported before installation; install it from Microsoft and rerun Setup.
6. Setup copies/verifies the profile, installs the application, updates your existing Spotnet launch shortcuts, and offers an optional launch. A fresh install creates both Desktop and Start Menu shortcuts automatically.

A fresh installation gets defaults and the application's provider-selection flow on first launch. Old application files remain available until you have verified the new client. Setup does not change NZB associations or the `spotnet://` handler.

The startup language applies to Inno Setup's standard controls and Spotnet's custom welcome text, migration/profile pages, Ready summary, progress/status text, errors, completion, shortcut messages, and uninstall prompts. English and Dutch are included. Low-level helper reports are replaced with localized safe summaries in the Dutch UI.

### Existing shortcuts

Setup scans this Windows user's Desktop and Start Menu Programs folders (including subfolders, up to six levels) for `.lnk` launchers that target `Spotnet.exe` or the legacy Spotnet Squirrel `Update.exe --processStart Spotnet.exe` command. Both old 2.x and existing 3.0 launchers are updated **in place**, retaining their names and locations. Their target, working directory, and icon point to the installed x64 application; old launch arguments are removed. Renamed Spotnet launchers are recognized by their target, not their displayed name.

If a folder has no matching launcher, Setup creates `Spotnet.lnk` there. It uses a different Spotnet name if that name belongs to an unrelated application, never overwriting that unrelated link. It does not add another launcher where an existing one was updated.

Original links are backed up under `%LOCALAPPDATA%\Spotnet3\ShortcutBackups`, with a hash-checked journal. Repeat upgrades retain the original backup. Uninstall restores replaced links and removes newly created links **only if they still match Setup's last version**; user-edited or deleted links are respected. Backup files are retained for recovery. A shortcut failure is reported visibly and causes Setup exit code `10`; the application remains installed and Setup can be rerun after correcting access/path problems.

Scope is deliberately per-user: Public Desktop/all-users Start Menu entries, other users' shortcuts, taskbar/Start pins, and ClickOnce `.appref-ms` launchers are not changed. Network/junction/symlink folders are not followed. Those launchers need to be updated manually; do not point a shared all-users shortcut to one user's private installation.

## Detection and migration scope

Detection reads the current-user and machine uninstall registry entries in both architecture views, plus known data locations:

- `%PROGRAMDATA%\Spotnet`
- `%LOCALAPPDATA%\Spotnet\Data`
- Registered Spotnet installation locations and their `Data` subfolders
- Bounded Spotnet settings folders under Local/Roaming AppData and the ClickOnce data cache

Custom/portable locations can be selected manually. A registered old application is not required if the data folder still exists. Read/access errors stop a selected migration instead of reporting success after a partial copy.

The automatic profile format is **Spotnet 2.x / this reconstructed C# client**. It imports standard `Spotnet.Properties.Settings` user.config files and compatible portable `<Settings>` XML. Arbitrary Spotnet 1.x/VB settings or server schemas are not automatically converted; use a fresh profile and configure that provider manually. The historical executable is never run to extract settings.

Copied data includes server configuration/credentials, signing keys, spot/comment database files and their WAL/SHM companions, root XML/CSV/DAT/TXT/OLS profile files, custom filters, themes, and images. The preferences importer copies data values only; it does not load types or executable configuration sections from the old file. DTDs/external XML entities are rejected. Certificate-validation bypass is reset to `False` during import.

**Not copied:** legacy executable/DLL payloads, cached content, logs, active downloader queues, and completed or partial download files outside the profile. Those originals remain where they were. Download-folder preferences can still point to their old location; check them before starting downloads. Finish or export old queued jobs before moving to 3.0.

## Data safety and recovery

The installed application uses a new, stable profile location:

```text
%LOCALAPPDATA%\Spotnet3\
    Data\                   Current profile and user.config
    Backups\<timestamp-id>\ Verified pre-upgrade snapshots
    ShortcutBackups\        Original launch links and replacement journal
    staging-<timestamp-id>\  Incomplete copy, if a migration failed
```

- Legacy source files are opened read-only and never edited or deleted. They remain the pre-migration recovery copy.
- All selected source file handles are held exclusively for the snapshot, including SQLite WAL/SHM files. Any sharing conflict aborts preparation. Do not reopen the old client during Setup.
- Copies are checked using SHA-256. Setup estimates required free space for the copy plus a 256 MiB margin; the application payload needs additional space.
- Files are written to a separate staging directory. Only a completed profile is renamed into `Data`. Failures retain staging for diagnosis; they never activate an incomplete profile.
- Existing marked 3.0 profiles are preserved, not overwritten by another import. Every repeat install/upgrade makes a verified backup before replacing application files. Backups exclude cache/log folders and require extra disk space.
- The installer refuses an unrecognized non-empty destination profile, overlapping source/destination paths, network sources, and junction/symlink paths.
- Setup refuses to overwrite an unmarked legacy application directory or downgrade a newer executable in the selected installation folder.
- Uninstall requests a safe application exit, restores eligible original shortcuts, and removes installed application files and unchanged newly created shortcuts. It **retains profiles, backups, downloads and the old application files**.

After migration, check provider access, spot/comment counts, filters, preferences, and download paths before retiring the old version. The old and new databases are separate copies; changes are not synchronized between them.

For rollback to the old client, exit 3.0 and launch the untouched old installation. For a 3.0 data restore, exit Spotnet, preserve the current `Data` folder, then restore a selected complete backup as `Data`. Never restore only the main database file from a snapshot containing WAL/SHM companions. Profile backups may contain credentials and signing keys: keep them private.

Installed builds are marked by `Spotnet.install`. They use stable per-user preferences and do not initialize or use the old Squirrel update feed. Updates to this installation are delivered by running a newer Setup package. Unmarked developer builds retain their previous data-location behavior.

## Build the installer

From the repository root:

```powershell
# Build application, run tests, build helper, verify payload, and compile Setup.
.\build-installer.ps1 -BootstrapCompiler

# Or use an existing Inno Setup 7.1+ compiler.
.\build-installer.ps1 -CompilerPath 'C:\Program Files\Inno Setup 7\ISCC.exe'
```

The bootstrap option downloads the pinned Inno Setup 7.1.0 x64 compiler installer from the publisher's GitHub release, verifies its Authenticode publisher, and uses its portable mode under `artifacts/installer-tools`. Review Inno Setup's own licensing terms for your use. The Microsoft WebView2 bootstrapper is also signature-checked before packaging.

Output includes the setup EXE and a `.sha256` checksum. `artifacts/` is Git-ignored. Compiler downloads and payload staging are intentionally retained for repeat builds and inspection. `-SkipBuild` skips application build/tests for packaging iteration; do not use it as a release-validation substitute.

Packaging takes binaries from the application Release output and bundled data/resources from Git-tracked source paths. It excludes the old Squirrel executable and obsolete browser/player/ZIP/long-path DLLs, and checks AMD64 headers for the app, decoder, WebView2 loader, SQLite interop, and LibVLC. It must not be run against an output directory containing unrelated executable files.

The Spotnet package is **unsigned** until a publisher code-signing certificate is supplied. Windows may show an unknown-publisher/SmartScreen warning. The signature checks on downloaded prerequisites do not sign Spotnet itself. No GitHub release/upload or production installation is performed by the build script.

## Verification

The 111-test x64 regression suite includes installer tests for fresh profiles, data/sidecar preservation, readable SQLite copies, preferences conversion, safe defaults, upgrade backups, unknown destinations, locked files, malformed XML, overlapping paths, excluded queues/caches, discovery, stable settings, graceful-shutdown timeouts, translation completeness, and preparation progress. Eighteen shortcut cases cover legacy/current/Squirrel matching, in-place replacement, fresh launchers, repeat upgrades, unrelated/uninstall links, locked files, backup-path bounds, and uninstall recovery/user edits. The 3.0.1 patch adds WPF menu rendering and live theme-switch coverage.

First-launch testing also caught and fixed a second SQLite PRAGMA return-value issue in fresh spots-database creation. That path now accepts successful no-row results, verifies the resulting settings/schema version, and applies the page size only to a verified-empty database. A regression test refuses initialization when user tables already exist.

An isolated Inno smoke-test build can be compiled by supplying `/DSmokeTestRoot=<repo>\artifacts\installer-smoke` in addition to the normal compiler defines. It writes only to that workspace test root, uses synthetic Desktop/Programs folders for shortcuts, creates no uninstall registry entry, never closes real Spotnet, and never installs prerequisites. WebView2 must already be present. Run:

```powershell
.\installer\Test-InstallerSmoke.ps1
```

The compiler define must match the script's `-TestRoot` argument (default: `<repo>\artifacts\installer-smoke`); choose a new directory for repeat runs. This checks actual extraction, fresh installation and both launchers, replacement of old/current/Squirrel links without duplicates, unrelated-link preservation, repeat upgrade, backup integrity, and uninstall restoring shortcuts while preserving a synthetic profile. It retains logs and test data. Never distribute the `*-smoke.exe` artifact: it is configured for that test directory, not real use.

Real-provider operation, graphics/video behavior, non-admin account variations, and migrations from arbitrary historical profiles still require desktop acceptance testing. Passing tests do not certify every legacy profile as compatible.

## Implementation references

- [Inno Setup non-administrative installation](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm)
- [Inno Setup x64 setup executable](https://jrsoftware.org/ishelp/topic_setup_setuparchitecture.htm)
- [Setup preparation and failure handling](https://jrsoftware.org/ishelp/topic_scriptevents.htm)
- [Verifying Inno Setup downloads](https://jrsoftware.org/isdl-verify.php)
- [Microsoft WebView2 runtime distribution](https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution)
