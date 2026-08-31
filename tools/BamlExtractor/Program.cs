using System;
using System.IO;
using System.Resources;
using ICSharpCode.BamlDecompiler;
using ICSharpCode.Decompiler.Metadata;

namespace BamlExtractor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string exePath = @"d:\sourcecode\spotnet-2.0.0.284-binary\Spotnet.exe";
            string outputDir = @"d:\sourcecode\decompiled_200\Spotnet_xaml";
            Directory.CreateDirectory(outputDir);

            var resolver = new UniversalAssemblyResolver(exePath, false, ".NETFramework,Version=v4.5");
            resolver.AddSearchDirectory(@"d:\sourcecode\spotnet-2.0.0.284-binary");

            var peFile = new PEFile(exePath);
            var typeSystem = new BamlDecompilerTypeSystem(peFile, resolver);
            var decompiler = new XamlDecompiler(typeSystem, new BamlDecompilerSettings());

            Console.WriteLine("PE File loaded: " + peFile.FullName);
            ClassComparer.Compare();

            int count = 0;
            int failed = 0;

            foreach (var res in peFile.Resources)
            {
                Console.WriteLine("Resource: " + res.Name);

                if (res.Name.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase) || res.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                {
                    var stream = res.TryOpenStream();
                    if (stream != null)
                    {
                        using (var resReader = new ResourceReader(stream))
                        {
                            var dict = resReader.GetEnumerator();
                            while (dict.MoveNext())
                            {
                                string key = (string)dict.Key;
                                if (key.EndsWith(".baml", StringComparison.OrdinalIgnoreCase))
                                {
                                    Stream? s = dict.Value as Stream;
                                    if (s == null && dict.Value is byte[] bytes)
                                    {
                                        s = new MemoryStream(bytes);
                                    }

                                    if (s != null)
                                    {
                                        try
                                        {
                                            var result = decompiler.Decompile(s);
                                            string relPath = key.Replace(".baml", ".xaml");
                                            string targetFile = Path.Combine(outputDir, relPath);
                                            string? dir = Path.GetDirectoryName(targetFile);
                                            if (dir != null) Directory.CreateDirectory(dir);
                                            File.WriteAllText(targetFile, result.Xaml.ToString());
                                            Console.WriteLine("    [OK] " + relPath);
                                            count++;
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine("    [FAIL] " + key + ": " + ex.Message);
                                            failed++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine($"Extracted {count} XAML files ({failed} failed).");

            ResxExtractor.ExtractAllResources(exePath, outputDir);

            string satPath = @"d:\sourcecode\spotnet-2.0.0.284-binary\nl\Spotnet.resources.dll";
            if (File.Exists(satPath))
            {
                ResxExtractor.ExtractAllResources(satPath, Path.Combine(outputDir, "nl"));
            }
        }
    }
}
