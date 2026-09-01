# Spotnet 3.0

## Download for Windows x64

**[Download Spotnet 3.0.5 Setup](https://github.com/Cyclone47/spotnet-3.0/releases/download/v3.0.5/Spotnet-3.0-x64-Setup.exe)** Â· [Release notes and SHA-256 checksum](https://github.com/Cyclone47/spotnet-3.0/releases/tag/v3.0.5)

**Latest fix:** a fresh profile now creates the complete spots database before the first list is loaded. Version 3.0.5 also repairs the specific incomplete two-table database left by 3.0.4, without deleting its existing rows.

**Previous fix:** the first-use provider dialog now fits low-resolution/scaled desktops and remains resizable, with its action buttons always available. Typing searches such as `news` no longer replaces the text with changing providers or raises an index error.

**Previous update:** a rebuilt connect dialog with a searchable provider list, a provider list re-verified against the live servers, and a Dutch interface that finally works. The list is now published as [providers.json](providers.json) and fetched on launch, so a provider that shuts down can be corrected without a new release.

**Previous fix:** readable dark-mode menus, nested submenus, and right-click menus, including hover, checked, and disabled items. Light-mode contrast is corrected too.

**Installer update:** choosing Nederlands now translates Spotnet's custom Setup pages and messages too. Lengthy pre-installation work shows its current stage and progress instead of an apparently frozen white page.

For fresh installs and upgrades from compatible Spotnet 2.x profiles. Setup closes Spotnet safely, copies your selected profile, and updates your existing Desktop and Start Menu launch shortcuts to 3.0. Requires Windows 10/11 x64 and .NET Framework 4.7.2+. The installer is **unsigned**; Windows may show an unknown-publisher warning. Read the [installation and migration guide](docs/INSTALLER.md) before upgrading.

A reconstructed and modernized Windows Usenet client, built around the familiar Spotnet experience: browse spots, search a local index, read comments, manage NZB downloads, and preview media in one desktop application.

**Current target:** Windows x64 Â· C# / WPF Â· .NET Framework 4.7.2

**Application version:** 3.0.6.0

**Validation checkpoint:** 180 automated tests passing on the x64 Release test host.

## The project idea

The goal is to keep Spotnet usable and maintainable by recovering its application source, replacing obsolete components, and improving reliability without discarding the existing workflow or breaking compatibility with the Spotnet network.

This is an incremental modernization, not a from-scratch rewrite. The existing interface, NNTP protocol, spot metadata, local databases, and integrated downloader provide the foundation. Work on top of that foundation focuses on:

- Moving the application and its in-process native dependencies to 64-bit.
- Replacing the old browser and media integrations.
- Improving database durability, startup behavior, and recovery.
- Hardening network, SQL, XML, and archive handling.
- Adding regression tests and documenting both completed work and remaining limitations.

Spotnet is a client, not a Usenet service: you supply access to a news server. It maintains a local index of spot metadata and retrieves articles through that server.

## What it is based on

The main reconstruction target is **Spotnet 2.0, build 2.0.0.284**, originally a 32-bit C# / WPF application targeting .NET Framework 4.5. The older **Spotnet 1.8.1 VB.NET codebase** was used as a historical reference for the protocol, models, and database behavior.

The reconstruction recovered C# code from the application assemblies, extracted embedded resources and localization, and converted 61 compiled WPF BAML layouts back into XAML. The original mixed-mode `Spotnet.Enc` decoder was replaced with a managed C# implementation.

Spotnet 3.0 is the name of the modernized application in this repository; it is not a claim of an official upstream release. The source folder `reconstructed/Spotnet2/` retains its historical name. References to 1.8.1 or 2.0 in provenance documents describe those original versions, not the current product version.

See the [source provenance record](docs/SOURCE_PROVENANCE.md), [original binary inventory](docs/INVENTORY.md), and [1.8.1 versus 2.0 comparison](docs/181_VS_20_DIFF.md).

## What changed for 3.0

### Platform and component replacements

These are the dependencies used by the current application project, not a list of every historical binary still present in `lib/`.

| Area | Original / reconstructed baseline | Current implementation |
| --- | --- | --- |
| Application platform | x86, constrained by native components | `Spotnet`, `Spotnet.Enc`, and `Spotnet.Tests` target x64 with `Prefer32Bit=false`; database tools also target x64 |
| Embedded web tabs | Awesomium / old Chromium integration | **Microsoft Edge WebView2 1.0.3351.48** for web tabs, release notes, feedback, and Advanced Downloads |
| Media preview | `Meta.Vlc` and `Meta.Vlc.Wpf` | **LibVLCSharp.WPF 3.10.1** with **VideoLAN.LibVLC.Windows 3.0.23.1**, using the x64 native runtime |
| SQLite | Loose legacy provider and native interop DLLs | **System.Data.SQLite.Core 1.0.119** via NuGet, with x64 interop |
| yEnc decoder | Mixed-mode x86 `Spotnet.Enc.dll` | Managed C# `Spotnet.Enc`, compiled for x64; currently a scalar decoder, not a SIMD implementation |
| ZIP archives | Ionic.Zip / DotNetZip application integration | Framework `System.IO.Compression` behind the path-validated `SafeZip` helper |
| NNTP zlib responses | Legacy SharpZipLib | **SharpZipLib 1.4.2**, retained for compressed NNTP responses |
| JSON | Legacy Newtonsoft.Json DLL | **Newtonsoft.Json 13.0.3** via NuGet |
| Logging | Legacy NLog DLL | **NLog 5.5.1**, with integration updates |
| HTML parsing | Legacy HtmlAgilityPack DLL | **HtmlAgilityPack 1.12.4** via NuGet |
| Long paths | Pri.LongPath calls | Framework long-path settings and a `longPathAware` application manifest |

Awesomium's source integration, managed assemblies, native engine, helper process, and supporting assets were removed. The Meta.Vlc assemblies were removed as well. Web page events now use an engine-independent `IPage` contract, and the feedback page's JavaScript bridge was adapted to WebView2 messages.

**Browser scope:** every page, the spot detail view and its comments included, renders in WebView2, and the Windows WebBrowser/MSHTML control is gone from the source tree. The spot page is `SpotWebView2Page`, which talks to its document through an injected script bridge rather than the direct DOM access MSHTML allowed.

### What â€œx64â€ means here

The built application, decoder, and test assemblies have AMD64 PE headers. SQLite, the WebView2 loader, and LibVLC use x64 in-process components. The solution build removes unused x86 and ARM64 runtime payload directories from its output.

The bundled `phpar2.exe`, `UnRAR.exe`, and `7za.exe` are still **32-bit external executables**. They run as separate child processes and do not force Spotnet itself to run as a 32-bit process. Replacing those utilities is separate remaining work.

This is a Windows x64 build, not a native ARM64 or cross-platform port. Moving to modern .NET or another UI framework is also separate from the completed application x64 migration.

### Database reliability and the startup fix

- The writable database/import path uses **write-ahead logging (WAL)** and `synchronous=NORMAL` instead of the old `synchronous=OFF` import setting.
- Database page sizes are set before WAL when creating new stores; existing databases are not automatically subjected to a large startup `VACUUM`.
- Connection handling includes a busy timeout and respects read-only intent, with a writable fallback when recovery requires it.
- Corruption detection checks SQLite result codes, and database schema definitions are shared between creation and rebuilding.
- **Rebuild Database** copies readable records into a fresh database, regenerates search indexes, and preserves the original as a backup. It is a recovery attempt, not a guarantee that every damaged record can be recovered.
- Quick Repair preserves WAL and reports database integrity results.
- The reported **`PRAGMA synchronous` startup error** was fixed: successful PRAGMA assignments can return `-1` from the SQLite provider. The import setup now verifies the actual setting instead of treating that return value alone as failure, with a regression test covering initialization.

WAL with `synchronous=NORMAL` improves the previous durability trade-off, but it is not a substitute for backups or a guarantee that the latest transactions survive power loss.

### Security and performance work

- **TLS:** NNTP uses operating-system protocol negotiation and requires encryption for TLS connections. Server certificates are validated by default; HTTP paths use the system TLS policy.
- **SQL:** network-supplied identifiers and values are parameterized. Advanced filter expressions are checked against allowed identifiers/operators, and their literals are bound as parameters.
- **ZIP extraction:** entry destinations are resolved and checked to reject paths that escape the extraction directory.
- **XML:** external resource resolution is disabled at identified parsing boundaries, including the update-manifest path.
- **RSA verification:** a bounded verifier cache reduces repeated provider allocation while retaining the existing wire-protocol signatures.
- **Import bookkeeping:** row-count refreshes are throttled instead of scanning on every batch. The displayed total may lag by up to 30 seconds during import.
- **Network streams:** sends reuse a buffer, and article/decompression reads handle partial reads.
- **UI:** theme fixes improve selected-tab, menu, and spot-description contrast.
- **Build visibility:** .NET analyzers, NuGet auditing, and deterministic compilation settings expose maintenance issues.

These are targeted improvements, not a claim that the entire reconstructed codebase has passed a security audit. Older libraries and analyzer findings remain.

## What stays familiar

The application retains its WPF desktop shell with MVVM Light, MahApps.Metro, and Xceed controls, along with:

- Spot browsing, categories, filters, favorites, and comments.
- Local SQLite databases and **FTS4** full-text search.
- The Phuse NNTP engine and Spotnet metadata/signature protocol.
- The integrated multi-connection NZB downloader and post-processing workflow.
- Media preview, docking/tab behavior, and light/dark themes.

The UI toolkit has not been replaced, the database has not migrated to FTS5, and the application still runs on **.NET Framework 4.7.2**. C# 11 source syntax does not mean it targets modern .NET.

## Build and run

### First-time install or migrate from Spotnet 2.x

The new per-user **Spotnet 3.0 x64 Setup** detects legacy installations, asks Spotnet to close safely, and offers a verified copy of the selected profile into a separate 3.0 data folder. Existing 3.0 profiles are backed up before an upgrade; uninstall keeps personal data. Old application files and source profiles remain untouched. Your existing Spotnet Desktop and Start Menu launch shortcuts are updated in place; missing launchers are created. Active download queues are not imported.

Build it with `./build-installer.ps1 -BootstrapCompiler`. Output: `artifacts/installer/Spotnet-3.0-x64-Setup.exe`. Released packages are unsigned because no publisher certificate exists yet; the build supports signing whenever one does, with `-SignThumbprint <cert>` for a certificate in the current user's store or `-SignCommand` for an HSM or cloud signing service. It signs the application binaries, the installer and the uninstaller, and refuses to package if any of them ends up without a signature. See the [installer and migration guide](docs/INSTALLER.md) for compatibility, prerequisites, backups, and testing.

### Requirements

- Windows x64; the documented development target is Windows 10/11.
- A .NET SDK capable of compiling C# 11, plus the .NET Framework 4.7.2 targeting pack. Visual Studio 2022 / Build Tools with the .NET desktop development workload is the documented build environment.
- The .NET Framework runtime compatible with the `net472` application.
- **Microsoft Edge WebView2 Evergreen Runtime** for WebView2-backed pages.
- NuGet access for package restore, and news-server access for live Usenet use.

### From the repository root

```powershell
dotnet build reconstructed/Spotnet2/Spotnet.sln -c Release
dotnet test reconstructed/Spotnet2/Spotnet.Tests/Spotnet.Tests.csproj -c Release --no-build
& "./reconstructed/Spotnet2/Spotnet/bin/Release/net472/Spotnet.exe"
```

The project files set `PlatformTarget=x64`; the commands above build the 64-bit application.

Alternatively, run `build.bat` from the repository root. It builds, runs tests, checks for the output executable, and offers to launch it. Its progress label still says â€œx86â€; that is stale display text, not the compiled architecture. The script reports test failures as a warning, so inspect the test result.

Keep the **entire output directory** together when running or copying a build. `Spotnet.exe` alone is not a standalone distribution: native runtimes, managed dependencies, configuration, and data/resources are also required.

Before trying a new build against an existing installation, close Spotnet and back up its configuration and databases. Configure your provider in the application. Leave certificate validation enabled; investigate provider certificate problems instead of routinely bypassing validation.

More detail: [build and setup guide](docs/BUILDING.md).

## Validation and remaining work

At this documentation update, the Release build passed **111/111 automated tests under VSTest x64**. This includes WPF menu rendering and installer localization/progress regressions. The build has zero errors; analyzer warnings remain a maintenance backlog. This is a local validation checkpoint, not a live CI badge.

Coverage includes yEnc decoding, spot XML parsing, categories, SQLite operations and initialization, database rebuilds, SQL/filter parameterization, query generation, RSA verifier caching, header-parser behavior, WebView2 runtime probing, AMD64 targeting, and ZIP path validation.

The development record also includes a healthy real-database check on approximately 2.29 million spots, with WAL active and SQLite integrity checks passing. That does not establish complete end-to-end compatibility.

Remaining validation and modernization work includes:

- Live news-server TLS connections and a full header import.
- Desktop checks for WebView2 navigation, feedback, downloads, and media playback controls.
- Recovery testing with genuinely corrupt databases, beyond the automated fixtures.
- Desktop verification of the WebView2 spot page against real spots, comments and images.
- Replacing the 32-bit child-process utilities (`phpar2.exe`, `UnRAR.exe`, `7za.exe`).
- Proving MahApps.Metro, MVVM Light, the Xceed toolkit and Starksoft.Aspen at runtime on
  modern .NET, which is what the framework migration is actually waiting on.
- Broader desktop/account acceptance testing of the new x64 installer and migration flow.
- UI-toolkit updates, then the modern .NET migration.
- Profiling a real import before attempting parallel verification or SIMD decoding.

Do not interpret a passing build or unit suite as a production-readiness guarantee.

## Repository layout

```text
README.md
build.bat
docs/                         Provenance, architecture, build notes, and work history
reconstructed/Spotnet2/
    Spotnet.sln               Main application solution
    Spotnet/                  WPF application, XAML, resources, and data
    Spotnet.Enc/              Managed yEnc decoder
    Spotnet.Tests/            xUnit regression tests
    lib/                      Retained legacy dependencies and reference binaries
    Directory.Build.props     Analyzer, audit, and runtime-output settings
tools/
    DbDiagnostic/             Database inspection and benchmark utility
    DbRepair/                 Standalone database repair utility
    BamlExtractor/            UI reconstruction tooling
    WpfCleaner/               Code-behind cleanup tooling
```

For development, edit `reconstructed/Spotnet2/` and launch its build output. Do not confuse the retained historical `lib/Spotnet.exe` with the newly built Spotnet 3.0 executable.

### Database diagnostics

```powershell
# Inspect an explicitly selected database.
dotnet run --project tools/DbDiagnostic -c Release -- inspect "C:/path/to/spots.dbs"

# Run a synthetic benchmark; this is not a live-server throughput test.
dotnet run --project tools/DbDiagnostic -c Release -- bench 50000
```

Use backups and understand the selected recovery operation before running repair tools against your own data.

## Documentation

- [Build and setup](docs/BUILDING.md)
- [Installer, migration, and rollback](docs/INSTALLER.md)
- [Updating the Usenet provider list](docs/PROVIDERS.md)
- [Database schema and recovery background](docs/DATABASE.md)
- [NNTP, spot XML, and signatures](docs/PROTOCOL.md)
- [Development handoff and open work](docs/HANDOFF.md)
- [Optimization history and measurements](docs/OPTIMIZATION_PROGRESS.md)
- [Modernization options](docs/MODERNIZATION.md)
- [Source provenance](docs/SOURCE_PROVENANCE.md)
- [Historical assembly inventory](docs/INVENTORY.md)
- [Spotnet 1.8.1 architecture](docs/SPOTNET_181_ARCHITECTURE.md)
- [Spotnet 2.0 architecture baseline](docs/SPOTNET_20_ARCHITECTURE.md)
- [Reconstruction uncertainties](docs/RECONSTRUCTION_UNCERTAINTIES.md)

The archaeological documents describe earlier versions, and the chronological work logs contain intermediate states. Some still mention x86 restrictions, Awesomium fallback, or smaller test suites that have since been superseded. Use this README for the current overview and the project files/code for implementation details.

## Contributing and attribution

Useful contributions include reproducible bug reports, provider/runtime compatibility testing, regression tests, and focused modernization changes. Include the build/commit, Windows version, reproduction steps, and redacted logs in a report. Do not publish credentials, access tokens, or personal database content.

Preserve Spotnet protocol compatibility and add tests for behavior changes. Database changes should include migration/recovery considerations; native dependency changes should be checked in the actual x64 output and on a desktop.

Credit belongs to the original Spotnet and Phuse authors, the authors of the bundled libraries and tools, and contributors to this reconstruction and modernization. Component origins are recorded in the provenance documentation. There is currently no repository-level `LICENSE` file; this README does not assign a blanket license to the recovered application or its third-party components.
