# Spotnet 3.0 Database Architecture & Compatibility Guide

**Database Engine:** SQLite 3 (Managed via `System.Data.SQLite.dll` + `SQLite.Interop.dll` x64)  
**Full-Text Search Engine:** SQLite FTS5 virtual tables. The shipped interop is not built with
`SQLITE_ENABLE_FTS5`; it carries FTS5 as a loadable extension, which `Fts5Module` registers on
every connection before any query touches `search` or `comments`.  
**Multi-Database Architecture:** Spotnet 3.0 separates domain data into distinct physical SQLite database files to optimize transaction isolation and performance.

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

### 2.2 `search` Virtual Table (FTS5 Full-Text Search)
External-content virtual search index synced with `spots` by trigger. It stores no source
data of its own, so it can always be regenerated from `spots` with no data loss.

```sql
CREATE VIRTUAL TABLE search USING fts5(
    cats,                             -- Column names must match those in `spots`
    sender,
    tag,
    subject,
    content='spots',
    content_rowid='rowid'
);
```

Rows are addressed by `rowid`. FTS4 called the same thing `docid`, and filters written
before the migration still use that name; they are rewritten while `filters.xml` is read.

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
CREATE VIRTUAL TABLE comments USING fts5(
    spot                              -- Target spot Message-ID
);
```

---

## 3. Database Migrations (`DbUpdater.cs` & `DatabaseUpgrade`)

Spotnet 3.0 tracks schema versions using `PRAGMA user_version`:

- **Version 0 -> Version 1**:
  - Creates `spamreports` and `spamgroup` tables if not existing.
- **Version 1 -> Version 2**:
  - Adds `reportmsgid TEXT` column to `spamreports`.
  - Adds `sender TEXT` column to `spamreports`.
- **Version 2 -> Version 3**:
  - Drops the FTS4 `search` triggers and the FTS4 `search` table, recreates `search` as
    FTS5 and rebuilds it from `spots`, in one exclusive transaction. `spots` itself is
    never touched, so a failure leaves the spots intact and the migration simply runs
    again on the next start. `Connect()` recreates the triggers afterwards.
  - The `comments` database carries no `user_version`; `SpotSaver.EnsureCommentsFts5`
    detects an FTS4 `comments` table and copies its rows into an FTS5 one instead.
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
