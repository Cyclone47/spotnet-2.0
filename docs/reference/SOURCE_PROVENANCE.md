# Spotnet 2.0 Source Provenance Record

This document records the exact historical provenance of each reconstructed subsystem and file.

| Component / Subsystem | Primary Provenance Source | Reference Assets Used | Rationale / Notes |
| :--- | :--- | :--- | :--- |
| **`Spotnet.Enc`** (yEnc SIMD Decoder) | Spotnet 2.0 Binary / Decompilation + Managed Reference | `Spotnet.Enc.dll` IL / C++/CLI metadata | Mixed-mode assembly replaced with clean managed C# implementation + native interop option. |
| **`Spotnet.DAL`** (`SpotSaver`, `SpotProvider`, `SqlDb`) | Spotnet 2.0 Binary Decompilation + 1.8.1 Baseline | `Spotnet.exe` metadata, 1.8.1 `SpotSaver.vb`, `SpotProvider.vb` | Direct port and modernization from 1.8.1 VB.NET with 2.0 transaction and migration logic. |
| **`Spotnet.Model`** (`Header`, `Spot`, `Comment`, `Filter`, `Server`) | Spotnet 2.0 Binary Decompilation + 1.8.1 Models | `Spotnet.exe`, `SpotParser.vb`, `cFilter.vb`, `cServers.vb` | Models ported to C# with data binding and serialization support. |
| **`Spotnet.Phuse.NNTP`** (Usenet Engine) | Spotnet 2.0 Binary Decompilation + Phuse Baseline | `Spotnet.exe` `Spotnet.Phuse` namespace | Absorbed from historical Phuse client into standalone managed C# NNTP connection pool. |
| **`Spotnet.Downloader`** (Queue & Workers) | Spotnet 2.0 Binary Decompilation | `Spotnet.exe` `Spotnet.Downloader` namespace | Completely new subsystem in 2.0. |
| **`Spotnet.Downloader.PostProcessing`** (PAR2 & UnRAR) | Spotnet 2.0 Binary Decompilation | `Spotnet.exe`, `phpar2.exe`, `UnRAR.exe` CLI wrappers | Process execution wrappers for parity repair and archive extraction. |
| **`Spotnet.Views` & `Spotnet.Controls`** (WPF UI) | Spotnet 2.0 BAML Extraction + Decompilation | 61 decompiled XAML files + code-behind files | Pristine XAML decompiled from `Spotnet.g.resources`. |
| **`Spotnet.ViewModel`** (MVVM Light Controllers) | Spotnet 2.0 Binary Decompilation | `Spotnet.exe` `Spotnet.ViewModel` namespace | ViewModels utilizing `GalaSoft.MvvmLight`. |
| **`Spotnet.Deployment`** (Squirrel Updates) | Spotnet 2.0 Binary Decompilation | `Squirrel.dll`, `Squirrel.exe` | Standard Squirrel.Windows update integration. |
| **Resources & Dictionaries** | Spotnet 2.0 Binary Resource Extraction | `Categories.resources`, `Words.resources`, `badwords.txt`, `null_modulus.txt` | Extracted directly from PE manifest resources. |
