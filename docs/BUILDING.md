# Spotnet 2.0 Build & Setup Instructions

This document provides complete instructions for building and testing the reconstructed Spotnet 2.0 solution from source.

---

## 1. Prerequisites & Environment

- **Operating System:** Windows 10 / 11 (x64 / x86)
- **SDK / Build Tools:**
  - .NET SDK (6.0, 8.0, 9.0+)
  - Visual Studio 2022 / Visual Studio Build Tools with:
    - .NET desktop development workload
    - .NET Framework 4.7.2 targeting pack
- **Platform Architecture:** `x86` (32-bit compilation target required for native SQLite/Awesomium/VLC interop)

---

## 2. Solution Structure

```
reconstructed/Spotnet2/
├── Spotnet.sln                 # Visual Studio Solution file
├── lib/                        # Managed & Native third-party runtime dependencies
├── Spotnet.Enc/                # Managed yEnc Decoder Library
│   ├── Spotnet.Enc.csproj
│   └── SpotnetDecoder.cs
├── Spotnet/                    # Main WPF Application Project
│   ├── Spotnet.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── Views/                  # Top-level WPF Windows
│   ├── Controls/               # Custom UI Controls & Views
│   ├── ViewModel/              # MVVM Light ViewModels
│   ├── Model/                  # Domain entities & Spot XML parser
│   ├── DAL/                    # SQLite database access layer & migrations
│   ├── Phuse/                  # NNTP protocol client & connection pool
│   ├── Downloader/             # Multi-part binary downloader & queue
│   ├── Browser/                # Chromium / WebBrowser spot rendering
│   ├── Data/                   # Filters, themes, categories definition files
│   └── Resources/              # Icons, fonts, localization, badwords
└── Spotnet.Tests/              # Automated Unit & Integration Tests (xUnit)
    ├── Spotnet.Tests.csproj
    ├── YEncDecoderTests.cs
    ├── SpotParserTests.cs
    ├── CategoryTaxonomyTests.cs
    └── SpotDbTests.cs
```

---

## 3. Build Commands

To restore and compile the complete solution via .NET CLI:

```powershell
# Build entire solution in Release configuration:
dotnet build d:\sourcecode\reconstructed\Spotnet2\Spotnet.sln -c Release

# Or build in Debug configuration:
dotnet build d:\sourcecode\reconstructed\Spotnet2\Spotnet.sln -c Debug
```

---

## 4. Running Automated Tests

To execute the automated test suite verifying yEnc decoding, Spot XML parsing, Category taxonomy resources, and SQLite database operations:

```powershell
dotnet test d:\sourcecode\reconstructed\Spotnet2\Spotnet.sln
```

All 6/6 test suites will run and pass:
- `SpotnetDecoder_DecodesSimpleYEncData` (PASSED)
- `SpotnetDecoder_HandlesEscapedCharacters` (PASSED)
- `SpotParser_ParsesValidSpotXml` (PASSED)
- `CategoriesResources_ContainsGenreStrings` (PASSED)
- `SpotCat_CanAddChildren` (PASSED)
- `SQLite_InMemoryDatabaseOperations` (PASSED)

---

## 5. Output Binaries & Running the Application

The build process automatically deploys all native dependencies (`SQLite.Interop.dll`, `awesomium.dll`, `libEGL.dll`, `awesomium_process.exe`, `UnRAR.exe`, `phpar2.exe`, `7za.exe`), `Data/Filters.v2`, `Data/TabThemes`, and resources to the output folder:

- **Release Executable:** `d:\sourcecode\reconstructed\Spotnet2\Spotnet\bin\Release\net472\Spotnet.exe`
- **Debug Executable:** `d:\sourcecode\reconstructed\Spotnet2\Spotnet\bin\Debug\net472\Spotnet.exe`

To launch:
```powershell
& "d:\sourcecode\reconstructed\Spotnet2\Spotnet\bin\Release\net472\Spotnet.exe"
```
