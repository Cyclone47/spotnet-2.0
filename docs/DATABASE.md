# Spotnet 2.0 Database Architecture & Compatibility Guide

**Database Engine:** SQLite 3 (Managed via `System.Data.SQLite.dll` + `SQLite.Interop.dll` x86)  
**Full-Text Search Engine:** SQLite FTS4 Virtual Tables with FTS3 matchinfo  
**Multi-Database Architecture:** Spotnet 2.0 separates domain data into distinct physical SQLite database files to optimize transaction isolation and performance.

---

## 1. Physical Database Files

1. **`{ProviderHash}.dbs` (Spots Database)**:
   - Contains all spot headers, full-text search indexes (`search`), spam reports, and poster trust ratings.
   - Located in `%LOCALAPPDATA%\Spotnet\Data\` or application directory.
2. **`{ProviderHash}.dbc` (Comments Database)**:
   - Stores spot-comment relations and comment full-text indexing (`comments`).
3. **`newznab.dbs` (Newznab Database)**:
   - Stores spots fetched from external Newznab/Torznab API providers.
4. **`userinfo.db` / `userkey.db` (User Identity & Settings Database)**:
   - Stores user RSA keypair for signing new spots and custom poster ratings.

---

## 2. Table Schemas & Definitions

### 2.1 `spots` Table
The primary table indexing Usenet spot articles.

```sql
CREATE TABLE spots (
    rowid INTEGER PRIMARY KEY,        -- NNTP article number / internal row ID
    key INT,                          -- Key type / disposition flag (e.g. 1=Normal, 2=Disposed/Removed)
    cat INT,                          -- Main Category ID (0=Image, 1=Sound, 2=Games, 3=Apps)
    subcat INT,                       -- Primary Subcategory encoded as (Category * 100 + SubCat)
    extcat INT,                       -- Secondary Subcategory / genre filter
    date INT,                         -- Unix Epoch timestamp of spot creation
    filesize INTEGER,                 -- Size of binary payload in bytes
    cats TEXT,                        -- Formatted category tag list (e.g. "0a01 0b02 0c01")
    sender TEXT,                      -- Nickname / handle of the poster
    tag TEXT,                         -- Release group tag or genre tag
    subject TEXT,                     -- Title / Subject line of the spot
    msgid TEXT,                       -- Usenet Message-ID of the spot XML article
    modulus TEXT                      -- RSA Public Key Modulus of the poster
);
```

### 2.2 `search` Virtual Table (FTS4 Full-Text Search)
High-performance virtual search index synced with `spots`.

```sql
CREATE VIRTUAL TABLE search USING fts4(
    content="spots",
    cats TEXT,
    sender TEXT,
    tag TEXT,
    subject TEXT,
    order=desc,
    matchinfo=fts3
);
```

### 2.3 `spamreports` Table
Stores spam and DMCA/disposition reports sent by the community.

```sql
CREATE TABLE spamreports (
    rowid INTEGER PRIMARY KEY,        -- Report article number
    msgid TEXT,                       -- Target spot Message-ID being reported
    modulus TEXT,                     -- RSA Modulus of the reporter
    date INT,                         -- Unix timestamp of the report
    reportmsgid TEXT,                 -- Usenet Message-ID of the report article
    sender TEXT                       -- Username of the reporter
);
```

### 2.4 `spamgroup` Table
Aggregates spam report counts per spot Message-ID for rapid lookup during grid rendering.

```sql
CREATE TABLE spamgroup (
    msgid TEXT PRIMARY KEY NOT NULL,  -- Spot Message-ID
    cnt INT DEFAULT 0                 -- Number of unique spam reports received
);
```

### 2.5 `comments` Virtual Table (in `dbc` database)
```sql
CREATE VIRTUAL TABLE comments USING fts4(
    spot TEXT,                        -- Target spot Message-ID
    matchinfo=fts3
);
```

---

## 3. Database Migrations (`DbUpdater.cs` & `DatabaseUpgrade`)

Spotnet 2.0 tracks schema versions using `PRAGMA user_version`:

- **Version 0 -> Version 1**:
  - Creates `spamreports` and `spamgroup` tables if not existing.
- **Version 1 -> Version 2**:
  - Adds `reportmsgid TEXT` column to `spamreports`.
  - Adds `sender TEXT` column to `spamreports`.
- **Database Pragmas Applied on Open**:
  ```sql
  PRAGMA page_size = 4096;
  PRAGMA journal_mode = DELETE;
  PRAGMA locking_mode = NORMAL;
  PRAGMA synchronous = OFF; -- Used during bulk header insertion
  ```

---

## 4. Query Patterns

- **Main Spots Retrieval Query:**
  ```sql
  SELECT s.rowid, s.subcat, s.extcat, s.date, s.filesize, s.subject, s.sender, s.tag, s.modulus, s.msgid, IFNULL(sg.cnt, 0) as spamcnt, s.cat, s.cats
  FROM spots s
  LEFT JOIN spamgroup sg ON s.msgid = sg.msgid
  WHERE (s.cat = @cat AND (s.cats LIKE '%a01%' OR s.cats LIKE '%a02%'))
  ORDER BY s.rowid DESC
  LIMIT @limit OFFSET @offset;
  ```

- **Retention Cleanup Query:**
  ```sql
  DELETE FROM spots WHERE rowid IN (SELECT rowid FROM spots WHERE date < @retentionLimit LIMIT 2000);
  ```
