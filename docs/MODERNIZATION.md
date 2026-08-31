# Spotnet 3.0 Modernization & Technology Assessment

This document assesses modern architectural alternatives for future rewrites and modernizations once behavioral and protocol parity is preserved.

---

## 1. Modernization Option Matrix

| Evaluation Criteria | Option A: Incremental .NET 4.8 + WPF | Option B: C# + .NET 9 + WPF | Option C: C# + .NET 9 + Avalonia UI (Recommended Cross-Platform) | Option D: C# + WinUI 3 / Windows App SDK | Option E: Web / Electron / Tauri Backend |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Migration Effort** | Low | Medium | Medium-High | High | Very High |
| **UI Compatibility** | 100% (Identical XAML) | 98% (Identical XAML) | 85% (Avalonia XAML dialect) | 75% (WinUI XAML dialect) | 20% (HTML/CSS rewrite) |
| **Windows Support** | Win 7 / 8 / 10 / 11 | Win 10 / 11 (Modern) | Win 10 / 11 | Win 10 (1809+) / 11 | Win 10 / 11 |
| **Cross-Platform** | Windows Only | Windows Only | **Windows, Linux, macOS** | Windows Only | Windows, Linux, macOS |
| **Performance** | Standard | High (JIT/AOT improvements) | High (Skia GPU Rendering) | High (DirectX 12 / Composition) | Medium (Web DOM overhead) |
| **64-bit / ARM64** | Limited by 32-bit DLLs | Full 64-bit & ARM64 support | Full 64-bit & ARM64 support | Full 64-bit & ARM64 support | Full 64-bit & ARM64 support |
| **Maintenance Risk** | High (Aging framework) | Low (Long-term .NET support)| Low (Active open-source community)| Medium (Microsoft roadmap churn) | High (Complex multi-process bridge)|

---

## 2. Proposed Target Architecture

To facilitate clean modern rewrites without losing any domain or protocol logic, the reconstructed codebase is architected into modular decoupled layers:

1. **`Spotnet.Core`**:
   - Spot XML Parser, Category Taxonomies, Filter Expressions, RSA Cryptographic Verification, Entity Models.
   - 100% pure portable .NET Standard 2.0 / .NET 9 library with zero UI dependencies.
2. **`Spotnet.Network`**:
   - Phuse NNTP client, connection pool, SSL/TLS stream pipeline, SOCKS proxy, yEnc SIMD decoding, article stream reader.
3. **`Spotnet.Data`**:
   - SQLite repositories, FTS4/5 full-text search indexing, schema migrations, batch transaction savers.
4. **`Spotnet.Downloader`**:
   - NZB queue manager, chunk assembly, PAR2 repair runner, UnRAR extraction pipeline.
5. **`Spotnet.UI`**:
   - Pluggable desktop presentation layer (WPF for 2.0 fidelity; Avalonia UI for cross-platform modern client).
