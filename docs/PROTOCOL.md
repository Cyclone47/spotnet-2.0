# Spotnet Protocol & NNTP Compatibility Guide

The **Spotnet Protocol** is a decentralized, peer-to-peer indexing and metadata sharing architecture layered directly on top of the standard **NNTP (Network News Transfer Protocol - RFC 3977)** Usenet infrastructure.

---

## 1. Usenet Newsgroup Topology

Spotnet organizes its distributed data across three primary standard Usenet newsgroups:

| Newsgroup | Purpose | Typical Message Format |
| :--- | :--- | :--- |
| `free.pt` (or `alt.binaries.ftd`) | **Spots (Headers & Full Descriptions)** | Articles containing Spot XML metadata, poster signatures, image segment references, and NZB pointers. |
| `free.usenet` | **Comments & Ratings** | User text comments, ratings (1–10), and replies linked via `References: <spot-msgid>`. |
| `free.willey` | **Spam Reports & Dispositions** | Moderation/spam reports with target spot Message-ID and reason. |

---

## 2. Spot Article Format

Each spot is published as a multipart Usenet article.

### 2.1 NNTP Article Headers
```http
From: JohnDoe <johndoe@spot.net>
Newsgroups: free.pt
Subject: [Tag] Awesome Movie (2024) 1080p <01a01b02c01>
Message-ID: <a1b2c3d4e5f6.1710000000@spot.net>
Date: 01 Jan 2024 12:00:00 GMT
X-User-Signature: [Base64-encoded RSA signature]
X-User-Key: [Base64-encoded RSA public modulus & exponent]
```

### 2.2 XML Body Payload
The article body contains the XML definition of the release:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Spotnet>
  <Posting>
    <Key>2</Key>
    <Created>1710000000</Created>
    <Poster>JohnDoe</Poster>
    <Title>Awesome Movie (2024) 1080p</Title>
    <Tag>HDTV</Tag>
    <Category>01</Category>
    <SubCat>a01|b02|c01|d03</SubCat>
    <Description>
      <![CDATA[
        [b]Title:[/b] Awesome Movie
        [b]Year:[/b] 2024
        [b]Format:[/b] MKV 1080p
        [img]https://example.com/poster.jpg[/img]
      ]]>
    </Description>
    <Image Width="300" Height="400">
      <Segment>image12345@spot.net</Segment>
    </Image>
    <NZB>
      <Segment>nzbsegment01@spot.net</Segment>
      <Segment>nzbsegment02@spot.net</Segment>
    </NZB>
  </Posting>
</Spotnet>
```

---

## 3. Cryptographic Verification & Signatures

1. **RSA Key Pair Generation:**
   - Every posting user generates an RSA 2048-bit key pair stored in `userkey.db`.
2. **Signature Calculation:**
   - Hash: `SHA1(Poster + Title + Tag + Category + SubCat + Description + ImageSegment + NZBSegments)`.
   - Signed using RSA Private Key.
3. **Validation Against `null_modulus.txt`:**
   - Spotnet verifies that the `X-User-Key` modulus matches trusted moderator keys or conforms to cryptographic standards to reject forged and malformed posts.

---

## 4. NZB & Media Retrieval Pipeline

1. **Segment Resolution:**
   - The XML `<NZB>` element contains one or more Usenet article Message-IDs (`<Segment>`).
2. **Fetching from Binary Server:**
   - Spotnet connects to the configured NNTP download server and executes:
     `BODY <nzbsegment01@spot.net>`
3. **yEnc Decoding (`Spotnet.Enc`):**
   - The body is decoded from yEnc format back into raw XML NZB or compressed zip bytes.
4. **NZB Parsing & Binary Segment Downloading:**
   - The NZB file specifies the actual Usenet binaries (`.rar`, `.par2`) across groups such as `alt.binaries.movies`.
   - Multi-connection worker pools download the file chunks in parallel.
