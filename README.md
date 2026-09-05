# Spotnet 3.0

A reconstructed and modernized Windows Usenet client, built around the familiar Spotnet
experience: browse spots, search a local index, read comments, manage NZB downloads, and
preview media in one desktop application.

Spotnet is a client, not a Usenet service — you supply access to a news server.

| | |
| --- | --- |
| **Version** | 3.0.6.0 |
| **Platform** | Windows 10/11 x64 · C# / WPF · .NET 10 |
| **Tests** | 243 passing on the x64 Release host |
| **Based on** | Spotnet 2.0 (build 2.0.0.284), with Spotnet 1.8.1 as reference |

---

## Download

**[Download Spotnet 3.0.6 Setup](https://github.com/Cyclone47/spotnet-3.0/releases/download/v3.0.6/Spotnet-3.0-x64-Setup.exe)**
· [Release notes and SHA-256 checksum](https://github.com/Cyclone47/spotnet-3.0/releases/tag/v3.0.6)

Setup handles both fresh installs and upgrades from compatible Spotnet 2.x profiles. It
closes Spotnet safely, copies your selected profile into a separate 3.0 data folder, and
updates your existing Desktop and Start Menu shortcuts. Old application files and source
profiles are left untouched; active download queues are not imported.

Setup includes .NET 10 inside the Spotnet installation folder. No separate .NET installation is needed. Setup installs Microsoft Edge WebView2 if it is missing.

> **The installer is unsigned.** Windows may show an unknown-publisher warning. No
> publisher certificate exists for this project yet.

Read the [installation and migration guide](docs/INSTALLER.md) before upgrading.

---

## What this project is

The goal is to keep Spotnet usable and maintainable by recovering its application source,
replacing obsolete components, and improving reliability — without discarding the existing
workflow or breaking compatibility with the Spotnet network.

This is an incremental modernization, not a from-scratch rewrite. The existing interface,
NNTP protocol, spot metadata, local databases, and integrated downloader are the
foundation. Work on top of it focuses on:

- Moving the application and its in-process native dependencies to 64-bit.
- Replacing the old browser and media integrations.
- Improving database durability, startup behavior, and recovery.
- Hardening network, SQL, XML, and archive handling.
- Adding regression tests, and documenting both completed work and remaining limits.

**Spotnet 3.0** is the name of the modernized application in this repository. It is not a
claim of an official upstream release. The source folder `reconstructed/Spotnet2/` keeps
its historical name, and references to 1.8.1 or 2.0 in the reference documents describe
those original versions.

### How it was reconstructed

C# code was recovered from the application assemblies, embedded resources and localization
were extracted, and 61 compiled WPF BAML layouts were converted back into XAML. The
original mixed-mode `Spotnet.Enc` decoder was replaced with a managed C# implementation.

See the [source provenance record](docs/reference/SOURCE_PROVENANCE.md) and the
[original binary inventory](docs/reference/INVENTORY.md).

---

## What changed for 3.0

Full history lives in the [release notes](docs/releases/). The headline changes:

- **Runs on .NET 10** with the runtime included in Setup.
- **Edge WebView2** replaces the Windows browser control everywhere, spot pages and
  comments included. The MSHTML control is gone from the source tree.
- **SQLite FTS5** search, with the index rebuilt once on first start.
- **Three styles** — Classic, Modern Light and Modern Dark — chosen during Setup or from
  Edit ▸ Style. Both Modern styles draw the filter icons as FontAwesome glyphs.
- **Built-in VPN support**, implemented with VPN Nederland.
- **A new x64 installer** that detects older installations and copies your profile across
  without touching the original.

### Platform and component replacements

The dependencies used by the current application project — not every historical binary
still present in `lib/`.

| Area | Original baseline | Current implementation |
| --- | --- | --- |
| Application platform | x86, constrained by native components | `Spotnet`, `Spotnet.Enc` and `Spotnet.Tests` target x64 with `Prefer32Bit=false` |
| Embedded web tabs | Awesomium / old Chromium integration | **Microsoft Edge WebView2 1.0.3351.48** |
| Media preview | `Meta.Vlc` and `Meta.Vlc.Wpf` | **LibVLCSharp.WPF 3.10.1** with **VideoLAN.LibVLC.Windows 3.0.23.1**, x64 native runtime |
| SQLite | Loose legacy provider and interop DLLs | **System.Data.SQLite.Core 1.0.119** via NuGet, x64 interop |
| yEnc decoder | Mixed-mode x86 `Spotnet.Enc.dll` | Managed C# `Spotnet.Enc`, x64; a scalar decoder, not SIMD |
| ZIP archives | Ionic.Zip / DotNetZip | Framework `System.IO.Compression` behind the path-validated `SafeZip` helper |
| NNTP zlib responses | Legacy SharpZipLib | **SharpZipLib 1.4.2** |
| JSON | Legacy Newtonsoft.Json DLL | **Newtonsoft.Json 13.0.3** via NuGet |
| Logging | Legacy NLog DLL | **NLog 5.5.1** |
| HTML parsing | Legacy HtmlAgilityPack DLL | **HtmlAgilityPack 1.12.4** via NuGet |
| Long paths | Pri.LongPath calls | Framework long-path settings and a `longPathAware` manifest |

### What "x64" means here

The application, decoder, and test assemblies have AMD64 PE headers. SQLite, the WebView2
loader, and LibVLC use x64 in-process components.

The bundled `phpar2.exe`, `UnRAR.exe` and `7za.exe` are still **32-bit external
executables**. They run as separate child processes and do not force Spotnet itself to run
32-bit. Replacing them is separate remaining work.

This is a Windows x64 build, not a native ARM64 or cross-platform port — WPF ties it to
Windows regardless of the framework it targets.

### Database reliability

- The writable database/import path uses **write-ahead logging (WAL)** with
  `synchronous=NORMAL`, instead of the old `synchronous=OFF` import setting.
- Page sizes are set before WAL when creating new stores; existing databases are not
  automatically subjected to a large startup `VACUUM`.
- Connection handling includes a busy timeout and respects read-only intent, with a
  writable fallback when recovery requires it.
- **Rebuild Database** copies readable records into a fresh database, regenerates search
  indexes, and preserves the original as a backup. It is a recovery attempt, not a
  guarantee that every damaged record survives.
- The **`PRAGMA synchronous` startup error** was fixed: successful PRAGMA assignments can
  return `-1` from the SQLite provider, which was being read as failure.

WAL with `synchronous=NORMAL` improves the previous durability trade-off, but it is not a
substitute for backups.

### Security and performance

- **TLS:** NNTP uses OS protocol negotiation and requires encryption for TLS connections.
  Server certificates are validated by default.
- **SQL:** network-supplied identifiers and values are parameterized. Filter expressions
  are checked against allowed identifiers/operators, with literals bound as parameters.
- **ZIP extraction:** entry destinations are checked to reject paths escaping the target.
- **XML:** external resource resolution is disabled at identified parsing boundaries.
- **RSA verification:** a bounded verifier cache reduces repeated provider allocation.
- **Network streams:** sends reuse a buffer; article/decompression reads handle partial reads.

These are targeted improvements, not a claim that the codebase has passed a security audit.

---

## What stays familiar

The WPF desktop shell with MahApps.Metro and Xceed controls, along with spot browsing,
categories, filters, favorites and comments; local SQLite databases; the Phuse NNTP engine
and Spotnet metadata/signature protocol; the integrated multi-connection NZB downloader;
and media preview with docking/tab behavior.

---

## Build from source

### Requirements

- Windows x64 (development target: Windows 10/11).
- The **.NET 10 SDK** with the Windows desktop workload.
- **Microsoft Edge WebView2 Evergreen Runtime** for WebView2-backed pages.
- NuGet access for package restore; news-server access for live Usenet use.

The setup helper targets `net472`, which every supported Windows already has, so it can run
without requiring the bundled .NET 10 runtime to be installed separately.

### Application

```powershell
dotnet build reconstructed/Spotnet2/Spotnet.sln -c Release
dotnet test reconstructed/Spotnet2/Spotnet.Tests/Spotnet.Tests.csproj -c Release --no-build
& "./reconstructed/Spotnet2/Spotnet/bin/Release/net10.0-windows/Spotnet.exe"
```

Keep the **entire output directory** together when running or copying a build.
`Spotnet.exe` alone is not a standalone distribution — native runtimes, managed
dependencies, configuration and data/resources are all required.

### Installer

```powershell
./build-installer.ps1 -BootstrapCompiler
```

Output: `artifacts/installer/Spotnet-3.0-x64-Setup.exe`. Releases are unsigned because no
publisher certificate exists yet; the build supports signing when one does, via
`-SignThumbprint <cert>` for a certificate in the current user's store, or `-SignCommand`
for an HSM or cloud signing service. It signs the application binaries, the installer and
the uninstaller, and refuses to package if any of them ends up unsigned.

Before testing a new build against an existing installation, close Spotnet and back up its
configuration and databases. Leave certificate validation enabled — investigate provider
certificate problems rather than routinely bypassing validation.

More detail: [build and setup guide](docs/BUILDING.md).

### Database diagnostics

```powershell
dotnet run --project tools/DbDiagnostic -c Release -- inspect "C:/path/to/spots.dbs"
```

```powershell
dotnet run --project tools/DbDiagnostic -c Release -- bench 50000
```

The benchmark is synthetic, not a live-server throughput test. Use backups and understand
the selected recovery operation before running repair tools against your own data.

---

## Repository layout

```text
build-installer.ps1           Builds the signed/unsigned x64 Setup
build.bat                     Build, test, and optionally launch
providers.json                Usenet provider list, fetched by clients on launch

reconstructed/Spotnet2/
    Spotnet.sln               Main application solution
    Spotnet/                  WPF application, XAML, resources, and data
    Spotnet.Enc/              Managed yEnc decoder
    Spotnet.Tests/            xUnit regression tests
    lib/                      Retained legacy dependencies
    Directory.Build.props     Analyzer, audit, and runtime-output settings

installer/                    Inno Setup script and smoke test
tools/
    Spotnet.SetupHelper/      Profile detection, migration, and shortcuts
    Spotnet.ThemePreview/     Renders the Setup style previews from the real themes
    DbDiagnostic/             Database inspection and benchmark utility
    DbRepair/                 Standalone database repair utility
    BamlExtractor/            UI reconstruction tooling
    WpfCleaner/               Code-behind cleanup tooling
    branding/                 Icon and splash artwork, with the scripts that build them

docs/                         Current documentation (see below)
    releases/                 Per-version release notes
    reference/                The original 1.8.1 / 2.0 versions this was recovered from
    internal/                 Working notes from the reconstruction effort
```

For development, edit `reconstructed/Spotnet2/` and launch its build output.

Reference material that is **not** committed — the original 2.0 release package, the
extracted 1.8/2.0 sources, and `lib/Spotnet.exe` — is listed in `.gitignore`. None of it
is a build input; fetch it from the original Spotnet 2.0 release if you want to reproduce
the reconstruction.

---

## Documentation

**Using and building**

- [Build and setup](docs/BUILDING.md)
- [Installer, migration, and rollback](docs/INSTALLER.md)
- [Updating the Usenet provider list](docs/PROVIDERS.md)
- [Database schema and recovery](docs/DATABASE.md)
- [NNTP, spot XML, and signatures](docs/PROTOCOL.md)
- [Modernization options](docs/MODERNIZATION.md)
- [Release notes](docs/releases/)

**Reference — the versions this was recovered from**

- [Source provenance](docs/reference/SOURCE_PROVENANCE.md)
- [Historical assembly inventory](docs/reference/INVENTORY.md)
- [Spotnet 1.8.1 architecture](docs/reference/SPOTNET_181_ARCHITECTURE.md)
- [Spotnet 2.0 architecture baseline](docs/reference/SPOTNET_20_ARCHITECTURE.md)
- [1.8.1 versus 2.0 comparison](docs/reference/181_VS_20_DIFF.md)

**Internal working notes** — kept for history in [`docs/internal/`](docs/internal/). These
are chronological logs containing intermediate states; some still mention x86 restrictions,
Awesomium fallback, or smaller test suites that have since been superseded. Use this README
for the current overview.

---

## Validation and remaining work

The Release build passes **243 automated tests** on the x64 host, with zero build errors;
analyzer warnings remain a maintenance backlog. This is a local validation checkpoint, not
a CI badge.

Coverage includes yEnc decoding, spot XML parsing, categories, SQLite operations and
initialization, database rebuilds, SQL/filter parameterization, query generation, RSA
verifier caching, header-parser behavior, WebView2 runtime probing, AMD64 targeting, and
ZIP path validation.

The development record includes a healthy real-database check on approximately 2.29 million
spots, with WAL active and integrity checks passing. That does not establish complete
end-to-end compatibility.

Still outstanding:

- Live news-server TLS connections and a full header import.
- Desktop checks for WebView2 navigation, feedback, downloads, and media playback.
- Recovery testing with genuinely corrupt databases, beyond the automated fixtures.
- Replacing the 32-bit child-process utilities (`phpar2.exe`, `UnRAR.exe`, `7za.exe`).
- Broader desktop/account acceptance testing of the x64 installer and migration flow.
- Profiling a real import before attempting parallel verification or SIMD decoding.

Do not read a passing build or unit suite as a production-readiness guarantee.

---

## Contributing and attribution

Useful contributions include reproducible bug reports, provider/runtime compatibility
testing, regression tests, and focused modernization changes. Include the build/commit,
Windows version, reproduction steps, and redacted logs. Do not publish credentials, access
tokens, or personal database content.

Preserve Spotnet protocol compatibility and add tests for behavior changes. Database
changes should include migration/recovery considerations; native dependency changes should
be checked in the actual x64 output, on a desktop.

Credit belongs to the original Spotnet and Phuse authors, the authors of the bundled
libraries and tools, and contributors to this reconstruction. Component origins are
recorded in the [provenance documentation](docs/reference/SOURCE_PROVENANCE.md).

There is currently no repository-level `LICENSE` file; this README does not assign a
blanket license to the recovered application or its third-party components.
