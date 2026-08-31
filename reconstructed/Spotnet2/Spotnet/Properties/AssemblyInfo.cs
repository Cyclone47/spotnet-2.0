using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Resources;
using Meta.Vlc;

[assembly: InternalsVisibleTo("Spotnet.AutoTests")]
[assembly: InternalsVisibleTo("Spotnet.Tests")]
[assembly: AssemblyCompany("Spotnet")]
[assembly: AssemblyCopyright("Copyright (C) 2014-2017")]
[assembly: AssemblyDescription("Spotnet")]
[assembly: AssemblyTitle("Spotnet")]
[assembly: RootNamespace("Spotnet")]
[assembly: AssemblyProduct("Spotnet")]
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
[assembly: ComVisible(false)]
[assembly: Guid("A421CD2D-1558-4771-BB5B-EB35B66F668A")]
[assembly: AssemblyTrademark("")]
[assembly: VlcSettings("vlc", new string[] { "-I", "--dummy-quiet", "--ignore-config", "--no-video-title", "--no-sub-autodetect-file" })]
[assembly: AssemblyAssociatedContentFile("resources/releasenotes/whatsnew.html")]
[assembly: AssemblyVersion("2.0.0.284")]
