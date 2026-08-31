# Spotnet 1.8.1 vs Spotnet 2.0 Comparison Matrix & Difference Map

This document establishes the detailed structural and behavioral difference mapping between historical **Spotnet 1.8.1** (VB.NET) and **Spotnet 2.0** (C# 2.0.0.284).

---

## 1. High-Level Architectural Evolution

```
┌────────────────────────────────────────────────────────────┐
│                       Spotnet 1.8.1                        │
│  - Monolithic VB.NET code-behind (sModule, MainWindow.vb)  │
│  - External SABnzbd dependency for downloading             │
│  - Legacy IE COM WebBrowser for HTML view                  │
│  - External Phuse.dll & SpotClient.dll assemblies          │
│  - ClickOnce deployment                                    │
└────────────────────────────────────────────────────────────┘
                              │
                              ▼ (Major Architectural Refactor)
┌────────────────────────────────────────────────────────────┐
│                       Spotnet 2.0                          │
│  - Modern C# MVVM Architecture (MvvmLight + IoC)           │
│  - Integrated Native NNTP Downloader + PAR2 + UnRAR        │
│  - Embedded Awesomium (Chromium) HTML5 rendering engine    │
│  - MahApps.Metro & AvalonDock multi-tab docking UI         │
│  - Native yEnc SIMD decoding acceleration (Spotnet.Enc)    │
│  - Integrated VLC Media Player for instant streaming       │
│  - Squirrel.Windows background auto-updating               │
└────────────────────────────────────────────────────────────┘
```

---

## 2. Component & Subsystem Comparison Matrix

| Subsystem / Area | Spotnet 1.8.1 Baseline | Spotnet 2.0 (2.0.0.284) | Structural Assessment & Changes |
| :--- | :--- | :--- | :--- |
| **Language & Runtime** | VB.NET (.NET 4.0 Client Profile) | C# (.NET 4.5 / x86) | **Complete Rewrite / Port**: Fully ported to idiomatic C# with async/await patterns. |
| **Architecture Pattern** | Monolithic Procedural / Code-Behind | MVVM (Model-View-ViewModel) | **Rewritten**: Uses MVVM Light (`ViewModelBase`, `RelayCommand`, Messenger). |
| **Window & UI Frame** | Standard WPF `Window` with custom chrome | `MahApps.Metro.Controls.MetroWindow` | **Replaced**: Modern Windows 8/10 Metro styling, dark/light themes, accent colors. |
| **Tab / Document View** | Custom WPF TabControl (`cTabs.vb`) | `Xceed.Wpf.AvalonDock` Docking Manager | **Replaced**: Allows docking, floating tabs, pinned left/bottom panes. |
| **HTML Rendering** | Embedded IE COM `WebBrowser` (Trident) | `Awesomium.Windows.Controls.WebControl` | **Replaced**: Chromium engine with HTML5, modern CSS3, and jQuery BBCode editor. |
| **NNTP Protocol Client** | External binary `Phuse.dll` | `Spotnet.Phuse.NNTP` (Internalized C#) | **Internalized & Enhanced**: Multi-connection pooling, keep-alives, SOCKS proxy, SSL. |
| **Spot XML Parser** | `SpotParser.vb` | `Spotnet.Model.Headers.SpotParser` | **Maintained & Enhanced**: Exact protocol compatibility preserved; ported to C#. |
| **RSA Signature Check** | `sModule.vb` (RSACryptoServiceProvider) | `Spotnet.Helpers.CryptoHelper` | **Maintained**: Verifies RSA signatures using `null_modulus.txt`. |
| **Database Persistence** | `SpotSaver.vb`, `SqlDb.vb` | `Spotnet.DAL.SpotSaver`, `SqlDb` | **Refactored**: Added batching, transaction handling, and auto-migration (`DbUpdater`). |
| **Database Retrieval** | `SpotProvider.vb` | `Spotnet.DAL.SpotProvider` | **Refactored**: Connected to `DataVirtualization` for smooth scrolling of 1M+ spots. |
| **Downloader** | External SABnzbd daemon (REST API) | `Spotnet.Downloader` (Integrated Engine) | **Completely New**: In-process multi-segment NNTP downloader with queue & speed limits. |
| **yEnc Article Decoding** | C# Software loop in Phuse | `Spotnet.Enc.dll` (SIMD C++/CLI native) | **New Optimization**: Ultra-fast yEnc decoder with managed C# fallback. |
| **PAR2 Verification** | External SABnzbd handles PAR2 | `Spotnet.Downloader.PostProcessing` (`phpar2`) | **Completely New**: Automatic parity block verification and repair worker. |
| **Archive Unpacking** | External SABnzbd handles UnRAR | `Spotnet.Downloader.PostProcessing` (`UnRAR`) | **Completely New**: Automatic extraction with password list matching (`badwords/passwords`). |
| **Media Player Preview** | None | `Spotnet.Downloader.Controls.Player` (`Meta.Vlc`)| **Completely New**: Real-time media streaming of downloading video files via VLC. |
| **Spam / Blacklist** | Basic local spam filters | `Spotnet.Controls.SpamReportsGrid`, `badwords` | **Enhanced**: Dedicated spam reports grid, blacklisting posters and hashes. |
| **Installer & Update** | ClickOnce | `Squirrel.Windows` (`Squirrel.dll`, `NuGet`) | **Replaced**: Delta packages, background installs, ClickOnce migration helper. |
| **External Indexers** | None | `Spotnet.Model.Newznab` | **Completely New**: Supports Newznab REST API provider querying. |

---

## 3. Class-by-Class Lineage Map

| Spotnet 1.8.1 (VB.NET) | Spotnet 2.0 (C#) | Status | Key Differences |
| :--- | :--- | :--- | :--- |
| `SpotParser.vb` | `Spotnet.Model.SpotParser` | **Direct Lineage** | Exact XML schema parsing; C# implementation. |
| `SpotSaver.vb` | `Spotnet.DAL.SpotSaver` | **Direct Lineage** | Enhanced error recovery and batch commit sizing. |
| `SpotProvider.vb` | `Spotnet.DAL.SpotProvider` | **Direct Lineage** | Integrated with virtualized asynchronous collections. |
| `SqlDb.vb` | `Spotnet.DAL.SqlDb` | **Direct Lineage** | Parameterized SQLite commands, connection pooling. |
| `cFilter.vb` | `Spotnet.Model.Filter` | **Direct Lineage** | Supports XML filter tree serialization and custom icons. |
| `cServers.vb` | `Spotnet.Model.Server` | **Direct Lineage** | Supports SSL, port override, connection quotas, proxy. |
| `cEncPass.vb` | `Spotnet.Helpers.EncryptionHelper` | **Direct Lineage** | DPAPI Windows data protection. |
| `SabHelper.vb` | `Spotnet.Helpers.SabHelper` | **Maintained** | Optional external SABnzbd mode kept alongside internal downloader. |
| `MainWindow.xaml.vb` | `Spotnet.ViewModel.MainViewModel` + `Spotnet.Views.MainWindow` | **Architectural Split**| View/logic split into MVVM structure. |
| `HTMLView.xaml.vb` | `Spotnet.Browser.AwesomiumPage` | **Replaced** | IE ActiveX replaced with Chromium Awesomium page. |
| `Toevoegen.xaml.vb` | `Spotnet.Views.Toevoegen` + `ToevoegenViewModel` | **Refactored** | Spot creation window refactored into MVVM. |
| `ProviderSelectie.xaml.vb`| `Spotnet.Views.SelectProviderWindow` | **Refactored** | Modernized provider selection wizard. |
| *(None in 1.8.1)* | `Spotnet.Downloader.DownloaderEngine` | **NEW in 2.0** | Integrated binary download manager. |
| *(None in 1.8.1)* | `Spotnet.Downloader.PostProcessing.Par2Processor` | **NEW in 2.0** | Integrated PAR2 parity repair. |
| *(None in 1.8.1)* | `Spotnet.Downloader.PostProcessing.UnrarProcessor` | **NEW in 2.0** | Integrated RAR unpacking. |
| *(None in 1.8.1)* | `Spotnet.Helpers.DbUpdater` | **NEW in 2.0** | Schema migration runner. |
| *(None in 1.8.1)* | `Spotnet.Model.Newznab` | **NEW in 2.0** | Newznab Usenet indexer support. |
