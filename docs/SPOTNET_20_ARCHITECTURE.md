# Spotnet 2.0 Architectural Specification

**Product:** Spotnet 2.0 (Build 2.0.0.284)  
**Platform:** .NET Framework 4.5 / x86  
**Pattern:** MVVM (Model-View-ViewModel) with Dependency Injection / IoC  
**UI Engine:** WPF + MahApps.Metro + AvalonDock + Awesomium Chromium  

---

## 1. System Architecture Diagram

```mermaid
graph TD
    subgraph UI ["User Interface Layer (WPF + MVVM)"]
        MW["MainWindow (MahApps.Metro)"]
        AD["AvalonDock Tab/Document Host"]
        AP["Awesomium WebControl (HTML Spot View)"]
        DG["Xceed Virtualized Spots DataGrid"]
        DL["Downloads View & Status Controls"]
        PL["VLC Player Video Preview Control"]
    end

    subgraph VM ["ViewModel Layer (MvvmLight)"]
        MVM["MainViewModel"]
        SVM["SpotDetailsViewModel"]
        DLVM["DownloadsViewModel"]
        TVM["ToevoegenViewModel"]
        STVM["SettingsViewModel"]
    end

    subgraph Core ["Core Domain & Business Services"]
        SP["SpotParser (XML & RSA Verification)"]
        CAT["Category & Filter Manager"]
        CFG["Settings & Configuration Engine"]
        MIG["DbUpdater (Schema Migrator)"]
    end

    subgraph Data ["Data Access Layer (DAL)"]
        DALP["SpotProvider (Query Generator & Paging)"]
        DALS["SpotSaver (Batch Insertion & Transactions)"]
        SQL["SqlDb (System.Data.SQLite + FTS4)"]
        DB[(Spotnet SQLite DB)]
    end

    subgraph Network ["Networking & Protocol Layer"]
        NNTP["Phuse NNTP Connection Pool"]
        AUTH["NNTP Authenticator & Keep-Alive"]
        TLS["SSL / TLS Stream Handler"]
        PRX["SOCKS 4/5 Proxy (starksoft.aspen)"]
        NZB["NZB Segment Fetcher"]
    end

    subgraph DL_Engine ["Downloader & Post-Processing"]
        Q["Download Queue Manager"]
        ART["Article Segment Downloader"]
        YENC["SpotnetDecoder (SIMD yEnc Decoder)"]
        PAR["Par2Processor (phpar2.exe)"]
        RAR["UnrarProcessor (UnRAR.exe)"]
        DISK["Local File System Storage"]
    end

    %% UI to ViewModel bindings
    MW --> MVM
    AD --> SVM
    DG --> MVM
    AP --> SVM
    DL --> DLVM
    PL --> DLVM

    %% ViewModel to Services
    MVM --> DALP
    MVM --> NNTP
    MVM --> Q
    SVM --> DALP
    SVM --> NNTP
    DLVM --> Q
    TVM --> NNTP
    TVM --> DALS

    %% Services to Data
    DALP --> SQL
    DALS --> SQL
    SQL --> DB
    MIG --> SQL

    %% Services to Network
    NNTP --> TLS
    TLS --> PRX
    NNTP --> AUTH
    NNTP --> SP

    %% Downloader Flow
    Q --> ART
    ART --> NNTP
    ART --> YENC
    YENC --> PAR
    PAR --> RAR
    RAR --> DISK
```

---

## 2. Layer & Subsystem Descriptions

### 2.1 Presentation Layer (`Spotnet.Views` & `Spotnet.Controls`)
- **`Spotnet.Views.MainWindow`**: Root window hosting the application menu, search toolbar, status footer, left-panel filter sidebar, and central docking workspace.
- **`Spotnet.Controls.LeftPanelUserControl`**: Tree view displaying hierarchical filters (Movies, Series, Music, Games, Applications, Books, Custom filters) with unread counters and badge icons.
- **`Spotnet.Controls.SpotsListWithDetailsGrid`**: Virtualized table displaying spot headers with thumbnail previews, poster ratings, file size, genre tags, and age.
- **`Spotnet.Browser.AwesomiumPage`**: Dedicated Chromium rendering surface for loading the HTML/CSS spot detail template, executing jQuery plugins, handling image zoom, and posting comments.
- **`Spotnet.Downloader.Controls.Player.PlayerControl`**: Embedded VLC player wrapper for streaming in-progress video downloads.

### 2.2 ViewModel Layer (`Spotnet.ViewModel`)
- Built on **MVVM Light** (`GalaSoft.MvvmLight`):
  - `MainViewModel`: Orchestrates spot retrieval, search keyword filtering, category selection, synchronization triggers, and status updates.
  - `SpotDetailsViewModel`: Manages single spot detail fetching, comments list loading, BBCode comment creation, NZB dispatching, and blacklist/spam reporting.
  - `DownloadsViewModel`: Controls download queue state (pause, resume, cancel, priority reordering, speed throttling).
  - `SettingsViewModel`: Persists Usenet server configurations, download folders, unpack options, and theme preferences.

### 2.3 Domain Model & Parsers (`Spotnet.Model`)
- **`Header`**: Lightweight representation of spot header retrieved via `XOVER`/`XHDR`.
- **`Spot`**: Comprehensive spot entity containing full XML description, poster details, timestamp, file size, image segment IDs, and embedded NZB segment list.
- **`Comment`**: User comment attached to a spot, including rating, avatar, timestamp, and poster signature.
- **`Filter`**: Hierarchical category filter definition with XML criteria rules.
- **`SpotParser`**: Parses spot XML payload, decrypts encoded keys, and validates cryptographic RSA signatures against `null_modulus.txt`.

### 2.4 Data Access Layer (`Spotnet.DAL`)
- **`SqlDb`**: SQLite database provider abstraction using `System.Data.SQLite`.
- **`SpotSaver`**: High-performance batch insertion pipeline that wraps incoming header/comment streams in SQLite transactions (batches of 1,000–5,000 records).
- **`SpotProvider`**: Dynamic SQL generator that constructs queries matching selected categories, subcategories, tags, posters, and text search strings with pagination support.
- **`DbUpdater`**: Automated schema migrator that applies incremental database updates on application launch.

### 2.5 Downloader & Post-Processing (`Spotnet.Downloader`)
- **`DownloaderEngine`**: In-process Usenet binary downloading engine.
- **Segment Worker Pool**: Spawns multiple parallel workers across configured download connections.
- **`SpotnetEnc.SpotnetDecoder`**: High-performance yEnc stream decoder that decodes Usenet binary articles directly to target file offsets.
- **`Par2Processor`**: Automatically launches `phpar2.exe` to verify parity blocks and repair damaged files.
- **`UnrarProcessor`**: Automatically extracts RAR/7z archives using `UnRAR.exe` or `7za.exe` with support for password list matching.

### 2.6 Deployment & Updates (`Spotnet.Deployment`)
- Managed by **Squirrel.Windows**:
  - Checks for background updates on application launch.
  - Downloads NuGet delta packages.
  - Migrates legacy ClickOnce registry and data files seamlessly.
