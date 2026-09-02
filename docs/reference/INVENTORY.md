# Spotnet 2.0 Archaeological Inventory

**Analysis Target:** Spotnet 2.0 (Build 2.0.0.284)  
**Historical Baseline:** Spotnet 1.8.1 (GitHub: `spotnet/spotnet`)  
**Target Runtime:** .NET Framework 4.5 / x86  
**UI Framework:** WPF (Windows Presentation Foundation) with MahApps.Metro & Awesomium  

---

## 1. Primary Application Assemblies

### 1.1 `Spotnet.exe`
- **Assembly Name:** `Spotnet`
- **Assembly Version:** `2.0.0.284`
- **File Version:** `2.0.0.284`
- **Target Runtime:** `.NETFramework,Version=v4.5`
- **Architecture / Platform Target:** `x86` (32-bit required for native interop components)
- **Managed/Native:** Managed (.NET C#)
- **Obfuscation:** None. Metadata, namespaces, class names, method signatures, parameter names, and properties are completely intact.
- **Symbols (PDB):** No separate PDB files distributed; Portable PDBs generated during reverse engineering.
- **Embedded Resources:**
  - `Spotnet.g.resources`: 61 compiled WPF BAML UI layouts/templates (100% recovered to XAML).
  - `Spotnet.Properties.Categories.resources`: Spotnet category taxonomy and metadata.
  - `Spotnet.Properties.Resources.resources`: System icons, toolbar bitmaps, splash screens, glyphs.
  - `Spotnet.Properties.Words.resources`: Keyword filters and localized wordings.
  - `Spotnet.Resources.badwords.txt`: Content filtering dictionary for spam and offensive terms.
  - `Spotnet.Resources.null_modulus.txt`: Known cryptographic RSA modulus key data for signature validation.
- **Primary Namespaces:**
  - `Spotnet.Views` (Application top-level windows: `MainWindow`, `Toevoegen`, `AboutControl`, `SelectProviderWindow`)
  - `Spotnet.ViewModel` (MVVM presentation controllers: `MainViewModel`, `SpotDetailsViewModel`, `SettingsViewModel`, etc.)
  - `Spotnet.Controls` (UI widgets: `LeftPanelUserControl`, `SpotsListWithDetailsGrid`, `SpotsThumbnailsView`, `SpotToolbar`, etc.)
  - `Spotnet.Model` (`Header`, `Spot`, `Comment`, `Filter`, `Server`, `Newznab`, `StatsReporter`)
  - `Spotnet.DAL` (Data Access Layer: `SpotSaver`, `SpotProvider`, `DatabaseMigration`, `SqlDb`)
  - `Spotnet.Phuse.NNTP` (Usenet NNTP client engine, connection pooling, header fetchers, article decoders)
  - `Spotnet.Downloader` (Integrated NZB downloader engine, queue manager, bandwidth limiter)
  - `Spotnet.Downloader.PostProcessing` (PAR2 verification via `phpar2.exe`, UnRAR extraction via `UnRAR.exe`)
  - `Spotnet.Browser` (Awesomium Chromium engine integration for spot HTML descriptions)
  - `Spotnet.Deployment` (Squirrel update and ClickOnce migration)
  - `Spotnet.Helpers` (Configuration management, database updates, cryptographic verification, logging)

### 1.2 `Spotnet.Enc.dll`
- **Assembly Name:** `Spotnet.Enc`
- **Assembly Version:** `0.0.0.0`
- **Architecture:** `x86`
- **Managed/Native:** Mixed-mode C++/CLI assembly wrapping native SIMD/C-optimized yEnc decoding (`_do_decode_raw`, `decoder_init`).
- **Exported Managed Type:** `SpotnetEnc.SpotnetDecoder` (`Init()`, `Decode(byte[] args, byte[] result, int start, uint arg_len)`).
- **Reconstruction Strategy:** Managed C# implementation of high-speed yEnc decoder fallback + native C++/CLI interop option.

### 1.3 `nl\Spotnet.resources.dll`
- **Assembly Name:** `Spotnet.resources`
- **Target Locale:** `nl` (Dutch)
- **Assembly Version:** `2.0.0.284`
- **Managed/Native:** Managed satellite resource assembly.
- **Embedded Resources:** `Spotnet.Properties.Categories.nl.resources`, `Spotnet.Properties.Words.nl.resources`.

---

## 2. Third-Party & Managed Dependencies

| Assembly | Version | Purpose / Role |
| :--- | :--- | :--- |
| `Awesomium.Core.dll` | 1.7.5.1 | Chromium-based web browser core engine for rendering rich HTML spot summaries |
| `Awesomium.Windows.Controls.dll` | 1.7.5.1 | WPF controls wrapper for Awesomium browser |
| `ClickOnceUninstaller.dll` | 1.0.0.0 | Helper to detect and migrate older Spotnet 1.8.x ClickOnce installations |
| `DeltaCompressionDotNet.dll` | 1.0.0.0 | MS Delta patch decompression for Squirrel updates |
| `FileCache.Signed.dll` | 1.4.0.0 | High-performance file system disk caching for spot thumbnails & images |
| `GalaSoft.MvvmLight.dll` | 5.0.2.32240 | MVVM Light toolkit (RelayCommand, ViewModelBase, Messenger) |
| `GalaSoft.MvvmLight.Extras.dll` | 5.0.2.32240 | Inversion of control container & event aggregation |
| `HtmlAgilityPack.dll` | 1.4.9.0 | HTML parser for HTML-formatted spot descriptions and comments |
| `ICSharpCode.SharpZipLib.dll` | 0.86.0.518 | Zip archive handling |
| `Ionic.Zip.dll` | 1.9.1.8 | DotNetZip library for NZB and archive decompression |
| `MahApps.Metro.dll` | 1.1.2.0 | Modern WPF UI styling framework (MetroWindow, accent colors, controls) |
| `Meta.Vlc.dll` | 1.1.5.0 | VLC media player wrapper for integrated media preview |
| `Meta.Vlc.Wpf.dll` | 1.1.5.0 | WPF control hosting for VLC video output |
| `Microsoft.Deployment.WindowsInstaller.*` | 3.0.0.0 | WiX DTF libraries for MSI/Windows installer operations |
| `Microsoft.Practices.ServiceLocation.dll` | 1.3.0.0 | Common Service Locator for IoC / DI |
| `Microsoft.Web.XmlTransform.dll` | 2.1.0.0 | XML transformation engine for config updates |
| `Mono.Cecil.*` | 0.9.5.0 | Metadata and IL inspection library |
| `Newtonsoft.Json.dll` | 6.0.0.0 | JSON serialization for stats reporting, update manifests, and provider lists |
| `NLog.dll` | 3.2.0.0 | Application logging framework |
| `NuGet.Core.dll` | 2.8.2.512 | Package management engine utilized by Squirrel update infrastructure |
| `Pri.LongPath.dll` | 2.0.4.0 | Windows 260-character path limit workaround for deep download directories |
| `Splat.dll` | 1.6.2.0 | Cross-platform image and logging utility for Squirrel |
| `Squirrel.dll` | 0.99.1.1 | Squirrel.Windows desktop application installer and background updater |
| `starksoft.aspen.dll` | 1.0.1.0 | SOCKS 4/4a/5 proxy client implementation for Usenet connections |
| `System.Data.SQLite.dll` | 1.0.96.0 | ADO.NET provider for SQLite embedded database |
| `System.Windows.Interactivity.dll` | 4.5.0.0 | WPF Blend triggers and behaviors |
| `Xceed.Wpf.AvalonDock.*` | 2.4.0.0 | Docking window manager for multi-tab spot views and workspaces |
| `Xceed.Wpf.DataGrid.dll` | 2.4.0.0 | High-performance WPF virtualized data grid for spot list rendering |
| `Xceed.Wpf.Toolkit.dll` | 2.4.0.0 | WPF extended controls (ColorPicker, NumericUpDown, WatermarkTextBox) |

---

## 3. Native Binaries & Runtime Tools

| Executable / DLL | Architecture | Purpose |
| :--- | :--- | :--- |
| `awesomium_process.exe` | x86 | Multi-process Chromium rendering worker |
| `awesomium.dll` | x86 | Awesomium native rendering engine |
| `icudt.dll` | x86 | International Components for Unicode for Awesomium |
| `libEGL.dll`, `libGLESv2.dll` | x86 | ANGLE OpenGL ES to DirectX 9/11 translation layer |
| `SQLite.Interop.dll` | x86 | Native C SQLite3 database engine with FTS3/4 full-text search |
| `avcodec-53.dll`, `avformat-53.dll`, `avutil-51.dll` | x86 | FFmpeg multimedia codecs for preview playback |
| `7za.exe` | x86 | 7-Zip standalone command-line archiver |
| `UnRAR.exe` | x86 | Official RARLAB unrar tool for automated archive extraction |
| `phpar2.exe` | x86 | High-performance PAR2 parity verification and repair tool |
| `Squirrel.exe` | x86 | Update and bootstrapper stub |

---

## 4. Application Configuration & Asset Files

- `Spotnet.exe.config`: .NET runtime configuration, SQLite DbProviderFactories registration, NLog configuration, network connection limits, legacy CAS policies.
- `Data/Filters.v2/`: Default filter XML presets for Movies, TV Series, Music, Games, Applications, E-books, and custom query filters with icon mappings.
- `Data/TabThemes/`: HTML/CSS/JS themes for rendering spots (`Straight`, `Tabbed`, `Classic`) including jQuery, Bootstrap, and BBCode editor toolbars.
- `Resources/ReleaseNotes/whatsnew.html`: Spotnet 2.0 interactive changelog rendered on first run.
