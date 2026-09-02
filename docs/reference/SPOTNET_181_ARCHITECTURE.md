# Spotnet 1.8.1 Architecture & Baseline Analysis

**Artifact:** Spotnet 1.8.1 Baseline  
**Language:** VB.NET  
**Target Framework:** .NET Framework 4.0 Client Profile  
**UI Engine:** WPF (Windows Presentation Foundation) with standard controls & MSHTML `WebBrowser`  

---

## 1. Solution & Codebase Structure

Spotnet 1.8.1 was architected as a single monolithic project (`SpotNet.vbproj`) with flat file organization:

```
spotnet-1.8.1/
├── SpotNet.vbproj             # Visual Studio VB.NET Project
├── Application.xaml / .vb      # Application entry point & lifetime events
├── MainWindow.xaml / .vb      # Primary UI window (Monolithic code-behind)
├── HTMLView.xaml / .vb        # IE WebBrowser host for spot detail view
├── Toevoegen.xaml / .vb       # Spot creation & posting window
├── ProviderSelectie.xaml / .vb # Usenet provider setup wizard
├── Status.xaml / .vb          # Synchronization progress popup
├── AboutControl.xaml / .vb    # About dialog
├── sModule.vb                 # Global static state, settings, crypto keys & helpers
├── SpotParser.vb              # Usenet article XML parser & RSA validator
├── SpotSaver.vb               # SQLite database bulk persistence engine
├── SpotProvider.vb            # Database query generator & spot retrieval layer
├── SqlDb.vb                   # Low-level SQLite ADO.NET connection wrapper
├── cServers.vb                # Usenet server configuration model
├── cEncPass.vb                # Password encryption via DPAPI / ProtectedData
├── cFilter.vb                 # XML Filter definition model & parser
├── cHistory.vb                # Navigation history stack
├── cTabs.vb                   # Spot view tab management
├── SabHelper.vb / SabItem.vb  # SABnzbd REST API client & download coordinator
└── PortableSettingsProvider.vb# Custom XML settings provider
```

---

## 2. Subsystem Breakdown

### 2.1 Protocol & Usenet Integration
- **News Server Roles:**
  - *Headers Server:* Connects to `free.pt` / `text.spotnet` / `alt.binaries.ftd` to retrieve new spot headers via `XOVER` / `XHDR`.
  - *Download Server:* Connects to binary server for fetching NZB segments and images.
  - *Upload Server:* Connects for posting spots and comments.
- **External Dependencies:** Historically referenced `Phuse.dll` (an NNTP socket client) and `SpotClient.dll`.
- **Spot Storage on Usenet:** Spots are stored as standard RFC 3977 Usenet articles in `free.pt` / `alt.binaries.ftd`. The article header contains summary metadata; the article body contains XML description, image segments, and embedded NZB segment references.

### 2.2 Spotnet Protocol & Parsing (`SpotParser.vb`)
- Parses XML payload within spot articles:
  ```xml
  <Spot>
    <Posting>
      <Key>...</Key>
      <Created>1300000000</Created>
      <Poster>Nickname</Poster>
      <Title>Example Release</Title>
      <Tag>TAG</Tag>
      <Category>01</Category>
      <SubCat>a01|b02|c01|d03</SubCat>
      <Description>Detailed description...</Description>
      <Image Width="300" Height="200"><Segment>...</Segment></Image>
      <NZB><Segment>...</Segment></NZB>
    </Posting>
  </Spot>
  ```
- **Cryptographic Verification:** Spots are digitally signed by posters using RSA public-private key pairs. `SpotParser` validates RSA-MD5/SHA1 signatures against the hardcoded modulus list (`null_modulus.txt`).

### 2.3 Database Layer (`SqlDb.vb`, `SpotSaver.vb`, `SpotProvider.vb`)
- **Database Engine:** Embedded SQLite 3 (`spots.db`).
- **Primary Tables:**
  - `spots`: `id`, `messageid`, `title`, `tag`, `cat`, `subcat`, `poster`, `spotdate`, `filesize`, `rowid`.
  - `comments`: `id`, `messageid`, `spotid`, `poster`, `rating`, `body`, `commentdate`.
  - `spotstate`: User watch list, read state, download state (`seen`, `downloaded`, `favorite`, `trash`).
- **Indexing & Queries:** Custom dynamic SQL generation based on composite category masks (`cats.cat = 0 AND (cats.subcat LIKE '%a01%' OR cats.subcat LIKE '%a02%')`).

### 2.4 Download Management (`SabHelper.vb`)
- Spotnet 1.8.1 did not contain an integrated internal binary downloader.
- Instead, it relied on launching or communicating with an embedded or external **SABnzbd** instance listening on `localhost:8080` via HTTP API calls (`/api?mode=addurl`, `/api?mode=queue`).

### 2.5 User Interface Architecture
- Single WPF window (`MainWindow.xaml`) with WinForms/COM Internet Explorer ActiveX control (`System.Windows.Forms.WebBrowser` / `mshtml`) embedded inside a `WindowsFormsHost` to render the spot HTML template.
- Direct tight coupling between UI code-behind (`MainWindow.xaml.vb`), database calls, and network background worker threads.
