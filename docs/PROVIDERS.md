# Updating the Usenet provider list

The providers offered in Spotnet's connect dialog come from [`providers.json`](../providers.json) in
this repository. Clients fetch it when the dialog opens, so **you can add, correct or remove a
provider by editing that one file on GitHub — no rebuild, no release.**

## Editing it

Open `providers.json` on GitHub, press the pencil icon, and commit. Each entry looks like this:

```json
{ "name": "Hitnews", "group": "NL", "host": "news.hitnews.com", "port": 563 }
```

| Field | Required | Notes |
| --- | --- | --- |
| `name` | yes | Shown in the dropdown. Must be unique. |
| `group` | yes | `NL` (Nederlandse providers) or `INT` (Internationale providers). |
| `host` | yes | The download server, and the default for upload and headers. |
| `port` | yes | One of `563`, `443`, `119`, `80`. |
| `upload` | no | Only when the upload server differs from `host`. |
| `headers` | no | Only when the headers server differs from `host`. |
| `uploadPort`, `headersPort` | no | Only when those differ from `port`. |

A provider whose three roles share one server needs only the four required fields. Eweka is the
example that needs all of them:

```json
{ "name": "Eweka", "group": "NL", "host": "newsreader1.eweka.nl", "port": 443,
  "upload": "upload.eweka.nl", "headers": "textnews.eweka.nl" }
```

Do not add an "Other…" entry. The client supplies its own manual row and refuses a published one.

## Check your change before merging

```bash
pwsh tools/Test-Providers.ps1
```

This validates the file against the same rules the client enforces, then opens a socket to every
server and reads its NNTP greeting. A plain port check is not enough: port 80 on 5 Euro Usenet and
SnelNL used to *accept the connection and never answer*, which hangs the connect dialog rather than
failing it. Only a real `200`/`201` greeting counts.

The same script runs in CI on every change to `providers.json` and weekly on a schedule, so a
provider that shuts down gets caught within the week. KPN sat in the list long after it stopped
serving Usenet; that is what the schedule is for.

## What the client does with it

`ProviderCatalogue` validates the fetched document before any of it is used, and
`ProviderCatalogueSource` handles fetching and caching. The behaviour worth knowing:

- **The built-in list wins until a fetched copy fully validates.** The dialog never waits on the
  network and never comes up empty because a download was truncated or an edit was malformed.
- **One bad entry rejects the whole file.** A partially applied list is how a single bad row hides
  among two dozen good ones. On rejection the client logs why and keeps the previous list.
- **The cache is revalidated on every load.** Being on disk earns it no trust.
- **Nothing is auto-applied to a configured account.** The catalogue only changes which options the
  picker offers; it never repoints a server someone has already set up.

### Why the validation is strict

This file decides which servers the dialog offers, and users type their Usenet username and
password into whichever one they pick. A hostile entry means credentials sent to a hostile server.
So the parser allow-lists rather than sanitises:

- HTTPS only, no redirects followed, 128 KB cap, 8-second timeout.
- Hosts must be plain DNS names — no scheme, path, port, credentials or wildcards — and are
  lower-cased before use.
- Ports must be one of the four Usenet ports. Nothing else is reachable through this file.
- Names are length-capped and rejected if they carry control characters or bidi overrides, which
  can otherwise disguise which provider a row really is.
- `group` must be `NL` or `INT`; `MANUAL` is refused.

`PublishedProviderCatalogueTests` pins each of these rejections, and also asserts that the
`providers.json` in this repository matches the client's built-in list — so a fresh install and an
updated install agree about what exists.

## Changing the schema

`schema` is `1`. Clients refuse any value they do not recognise, so bumping it takes a matching
change to `ProviderCatalogue.SupportedSchema` and a release before the published file can move.
Adding an optional field does not need a bump; removing or repurposing one does.
