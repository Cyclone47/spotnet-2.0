# Spotnet 2.0 Reconstruction Uncertainties & Analysis Notes

This document logs any uncertainties, ambiguities, or runtime considerations discovered during the archaeological reverse engineering process.

---

## 1. Identified Items & Resolutions

### 1.1 `Spotnet.Enc.dll` (Mixed-Mode Native yEnc Decoder)
- **Status:** Resolved.
- **Evidence:** `Spotnet.Enc.dll` was compiled as a C++/CLI mixed-mode assembly with CRT runtime startup code (`_do_decode_raw`, `decoder_init`).
- **Resolution:** A pure, managed C# `SpotnetDecoder` class using high-speed pointers/unsafe block yEnc decoding is provided as the primary portable implementation, ensuring that the application can be built and run on any modern .NET environment without requiring legacy Visual C++ 2010 CRT runtimes.

### 1.2 Awesomium Web Browser Engine vs Modern WebViews
- **Status:** Documented & Preserved for 2.0 Parity.
- **Evidence:** Spotnet 2.0 embeds Awesomium 1.7.5.1 (`awesomium_process.exe`, `awesomium.dll`, `Awesomium.Windows.Controls.dll`) to render spot HTML and JavaScript.
- **Modernization Assessment:** For future modernization (Phase 13), Microsoft WebView2 (Edge Chromium) or CefSharp can drop in as a modern 64-bit replacement. For 2.0 parity, the Awesomium control and fallback WebBrowser integration are preserved.

### 1.3 `null_modulus.txt` Poster Key Database
- **Status:** Preserved & Extracted.
- **Evidence:** Spotnet 2.0 embeds `null_modulus.txt` containing known RSA moduli for legitimate community moderators and automated posters.
- **Resolution:** Extracted as an embedded resource directly into `reconstructed/Spotnet2/Spotnet/Resources/null_modulus.txt`.

### 1.4 Native Interop 32-bit (x86) Constraint
- **Status:** Documented.
- **Evidence:** `SQLite.Interop.dll`, `Awesomium.dll`, `libEGL.dll`, and VLC native plugins are 32-bit PE binaries.
- **Resolution:** The reconstructed Visual Studio solution is explicitly configured for `PlatformTarget: x86` to ensure full binary compatibility.
