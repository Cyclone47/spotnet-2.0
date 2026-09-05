# Spotnet 3.0 Build & Setup Instructions

This document provides complete instructions for building and testing Spotnet 3.0 from source.

All paths below are relative to the repository root. Substitute your own clone location.

---

## 1. Prerequisites & Environment

- **Operating System:** Windows 10 / 11 x64
- **SDK / Build Tools:**
  - .NET 10 SDK (10.0.400 or later). A `global.json` at the repository root pins this;
    an older SDK fails with `NETSDK1045: The current .NET SDK does not support targeting .NET 10.0`.
  - Visual Studio / Build Tools compatible with .NET 10, with the .NET desktop development workload
- **Platform Architecture:** `x64` (the application, tests, SQLite interop, WebView2 loader, and LibVLC runtime are all 64-bit)
- **Runtime:** Microsoft Edge WebView2 Evergreen Runtime

The project targets `net10.0-windows`. No .NET Framework targeting pack is required.

---

## 2. Solution Structure

```
src/Spotnet/
├── Spotnet.sln                 # Visual Studio Solution file
├── lib/                        # Vendored Squirrel assemblies & native child-process utilities
├── Spotnet.Enc/                # Managed yEnc Decoder Library
│   ├── Spotnet.Enc.csproj
│   └── SpotnetDecoder.cs
├── Spotnet/                    # Main WPF Application Project
│   ├── Spotnet.csproj
│   ├── app.xaml / Spotnet/App.cs
│   ├── app.config              # Application-scoped defaults for Properties/Settings
│   ├── Spotnet/
│   │   ├── Views/              # Top-level WPF Windows
│   │   ├── Controls/           # Custom UI Controls & Views
│   │   ├── ViewModel/          # ViewModels (Mvvm/ holds the base types)
│   │   ├── Model/              # Domain entities & Spot XML parser
│   │   ├── Community/          # CommunityConfig: moderation feeds & integrations
│   │   ├── DAL/                # SQLite database access layer & migrations
│   │   ├── Phuse/              # NNTP protocol client & connection pool
│   │   ├── Downloader/         # Multi-part binary downloader, queue & post-processing
│   │   ├── Browser/            # WebView2 spot rendering
│   │   ├── Remote/             # Spotnet Remote HTTP server & Android pairing
│   │   ├── Deployment/         # Squirrel updater & installed-profile detection
│   │   └── Notifications/      # Notification centre
│   ├── Data/                   # Filters, themes, categories definition files
│   └── Resources/              # Icons, fonts, localization, badwords
└── Spotnet.Tests/              # Automated Unit & Integration Tests (xUnit)
```

UI markup lives in the lowercase folders next to the project file (`controls/`, `views/`,
`style/`, `downloader/controls/`); the matching code-behind lives under `Spotnet/`.

---

## 3. Build Commands

To restore and compile the complete solution via .NET CLI:

```powershell
# Build entire solution in Release configuration:
dotnet build src\Spotnet\Spotnet.sln -c Release

# Or build in Debug configuration:
dotnet build src\Spotnet\Spotnet.sln -c Debug
```

`build.bat` at the repository root runs the same build, then the tests, then verifies the
output binary.

---

## 4. Running Automated Tests

To execute the automated test suite:

```powershell
dotnet test src\Spotnet\Spotnet.Tests\Spotnet.Tests.csproj
```

All 519 tests should pass. They cover yEnc decoding, Spot XML parsing, category resources,
SQLite durability and rebuild behavior, query generation and parameterization, RSA verifier
caching, WebView2 runtime probing, x64 assembly targeting, traversal-safe ZIP extraction,
NZB download references, Spotnet Remote, the provider catalogue, and setup/profile migration.

---

## 5. Output Binaries & Running the Application

The build process automatically deploys the x64 native dependencies (`SQLite.Interop.dll`,
`libvlc.dll`, `WebView2Loader.dll`) plus the child-process utilities (`UnRAR.exe`,
`phpar2.exe`, `7za.exe`), `Data/Filters.v2`, `Data/TabThemes`, and resources to the output folder:

- **Release Executable:** `src\Spotnet\Spotnet\bin\Release\net10.0-windows\Spotnet.exe`
- **Debug Executable:** `src\Spotnet\Spotnet\bin\Debug\net10.0-windows\Spotnet.exe`

To launch:
```powershell
& "src\Spotnet\Spotnet\bin\Release\net10.0-windows\Spotnet.exe"
```

---

## 6. Building the Installer

`tools\Build-Net10Setup.ps1` compiles the Inno Setup installer. It expects the Inno Setup
compiler at `artifacts\installer-tools\InnoSetup7\ISCC.exe`.
