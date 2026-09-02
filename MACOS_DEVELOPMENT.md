# Spotnet 3.0 macOS Client Development Guide

> **For AI Agents (Antigravity, Claude Code, Codex) & Developers on macOS**  
> This guide outlines how to pick up development of the macOS client on Apple Silicon / macOS Sonoma/Sequoia.

---

## 1. Getting Started on macOS

### 1.1. Clone and Checkout
Ensure you are on the `macos-client` branch:
```bash
git clone https://github.com/Cyclone47/spotnet-3.0.git -b macos-client
cd spotnet-3.0
```

### 1.2. Verify Tooling
Verify that .NET SDK (8.0 or 10.0) and Xcode CLI tools are installed:
```bash
dotnet --version
xcode-select -p
```
If Avalonia templates are needed:
```bash
dotnet new install Avalonia.Templates
```

### 1.3. Verify Core Libraries Build Cleanly on Mac
The platform-neutral core libraries can be built immediately on macOS:
```bash
dotnet build reconstructed/Spotnet2/Spotnet.Enc/Spotnet.Enc.csproj
dotnet build reconstructed/Spotnet2/Spotnet.Core/Spotnet.Core.csproj
```
Both projects target `net8.0` (AnyCPU) and have zero Windows/WPF dependencies.

---

## 2. Solution Architecture

```
reconstructed/Spotnet2/
├── Spotnet.Core/                 <-- Platform-neutral core shared by Windows & macOS
│   ├── Abstractions/             <-- Core interfaces: IAppPaths, ISecretStore, IUiDispatcher, IUserSettings
│   ├── Model/                    <-- ServerInfo, NntpSettings, SpeedCalculator, Spot rows & counters
│   ├── Network/                  <-- Socks5Client (proxy, IPv4/IPv6, DNS)
│   ├── Phuse/                    <-- yEnc codecs (YEncCrc32, YEncDecoder, YEncEncoder) & NNTP transport
│   ├── Platform/                 <-- StandardAppPaths (macOS ~/Library/Application Support conventions)
│   └── Text/                     <-- StringExtension methods
│
├── Spotnet.Enc/                  <-- Platform-neutral spot header crypto and key verification
│
├── Spotnet/                      <-- Existing Windows WPF application (net8.0-windows)
│
└── Spotnet.Mac/ (or Avalonia)    <-- [TO BE CREATED ON MAC] The native macOS desktop client
```

---

## 3. macOS Implementation Directives

### 3.1. Project Structure
Create the Avalonia client under `reconstructed/Spotnet2/Spotnet.Mac/`:
```bash
dotnet new avalonia.app -n Spotnet.Mac -o reconstructed/Spotnet2/Spotnet.Mac
dotnet sln reconstructed/Spotnet2/Spotnet.sln add reconstructed/Spotnet2/Spotnet.Mac/Spotnet.Mac.csproj
dotnet add reconstructed/Spotnet2/Spotnet.Mac/Spotnet.Mac.csproj reference reconstructed/Spotnet2/Spotnet.Core/Spotnet.Core.csproj
dotnet add reconstructed/Spotnet2/Spotnet.Mac/Spotnet.Mac.csproj reference reconstructed/Spotnet2/Spotnet.Enc/Spotnet.Enc.csproj
```

### 3.2. Directory Paths on macOS
Use `Spotnet.Platform.StandardAppPaths` (or implement `IAppPaths`):
- **App Data & Database**: `~/Library/Application Support/Spotnet/`
- **Caches**: `~/Library/Caches/Spotnet/`
- **Logs**: `~/Library/Logs/Spotnet/`
- **Filters & Themes**: `~/Library/Application Support/Spotnet/Filters.v2/`
- **Downloads**: `~/Downloads`

### 3.3. SQLite & Database Portability (Apple Silicon ARM64)
- Replace Windows `System.Data.SQLite.Core` (which lacks `osx-arm64` interop) with `Microsoft.Data.Sqlite` in the Mac client.
- Include the SQLite bundle with FTS5:
  ```xml
  <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.8" />
  <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.8" />
  ```
- **Crucial**: The SQLite database schema, WAL journaling, and FTS5 indexes are 100% binary-compatible across Windows and macOS. An existing Windows Spotnet database file can be copied directly into `~/Library/Application Support/Spotnet/` and read.

### 3.4. Browser & Spot Rendering (WKWebView)
- Do not use WebView2 on macOS.
- Use Avalonia's native web view integration (`NativeWebView` / `WKWebView`):
  ```xml
  <PackageReference Include="Avalonia.Controls.WebView" Version="11.1.0" />
  ```
- Spotnet's existing spot detail templates (`whatsnew.html`, spot page HTML/CSS/JS) work directly inside `WKWebView`.

### 3.5. Credential Storage (Apple Keychain)
- Implement `ISecretStore` (`Spotnet.Platform.ISecretStore`) using macOS Keychain Services (`SecItemAdd`, `SecItemCopyMatching`, `SecItemDelete` from `Security.framework`).
- Provider passwords, API keys, and private spot signing identities must never be stored in plaintext configuration files.

### 3.6. macOS User Experience & HIG
- **Menu Bar**: Native menu items (App Menu, File, Edit, View, Help).
- **Keyboard Shortcuts**:
  - `Cmd+,` -> Preferences / Provider Settings
  - `Cmd+F` -> Search spots
  - `Cmd+W` -> Close tab / spot detail
  - `Cmd+Q` -> Quit
- **Appearance**: Support macOS system Light and Dark themes dynamically.
- **Dock**: Optional unread count badge or notification on sync completion.

---

## 4. Immediate Milestone: Phase 0 Spike (Go / No-Go Test)

The first task to execute on macOS is a self-contained spike verifying four critical capabilities on Apple Silicon:

1. **Window**: Launch a clean Avalonia window on macOS.
2. **Network/TLS**: Connect to a real Usenet provider over TLS (port 563) via `Spotnet.Core.Model.ServerInfo` / `Phuse` and authenticate.
3. **Database & FTS5**: Open a Spotnet SQLite database on macOS, execute an FTS5 search query, and return results.
4. **Spot Details**: Display a spot page inside `WKWebView`.

---

## 5. Build & Packaging on macOS

### Standalone Self-Contained App:
```bash
dotnet publish reconstructed/Spotnet2/Spotnet.Mac/Spotnet.Mac.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishTrimmed=false
```

### Apple Developer ID Signing & Notarization:
```bash
# Code signing
codesign --deep --force --options runtime --sign "Developer ID Application: YourName (TeamID)" "Spotnet.app"

# Notarization
xcrun notarytool submit "Spotnet.dmg" --keychain-profile "AC_PASSWORD" --wait
xcrun stapler staple "Spotnet.dmg"
```

---

## 6. Key Source References in Windows Repo

| Feature | Windows Location | Mac Action |
|---|---|---|
| **Spot XML & Decryption** | `Spotnet/Helpers/SpotHelper.cs` | Use `Spotnet.Enc.SpotnetDecoder` + `Spotnet.Core` |
| **Socks5 Proxy** | `Spotnet.Core/Network/Socks5Client.cs` | Already portable, ready to use |
| **yEnc Decoder** | `Spotnet.Core/Phuse/YEncDecoder.cs` | Already portable, ready to use |
| **SQLite DAL** | `Spotnet/DAL/SQliteDb.cs`, `Fts5Module.cs` | Adapt to `Microsoft.Data.Sqlite` |
| **Provider Catalogue** | `Spotnet/Model/ProviderCatalogue.cs` | Portable Usenet provider presets |
| **SABnzbd / NZBGet** | `Spotnet/Downloader/NzbGetDownloader.cs` | Call external REST APIs over HTTP |
