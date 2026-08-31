using System;
using System.Collections;
using System.IO;
using System.Resources.Extensions;
using ICSharpCode.Decompiler.Metadata;

namespace BamlExtractor
{
    public class ResxExtractor
    {
        public static void ExtractAllResources(string exePath, string outputDir)
        {
            var peFile = new PEFile(exePath);

            foreach (var res in peFile.Resources)
            {
                Console.WriteLine("Inspecting resource: " + res.Name);
                if (res.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) && !res.Name.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase))
                {
                    var stream = res.TryOpenStream();
                    if (stream != null)
                    {
                        try
                        {
                            using var resReader = new DeserializingResourceReader(stream);
                            string resxName = res.Name.Replace(".resources", ".txt");
                            string outFile = Path.Combine(outputDir, resxName);
                            string? dir = Path.GetDirectoryName(outFile);
                            if (dir != null) Directory.CreateDirectory(dir);
                            using var writer = new StreamWriter(outFile);
                            var dict = resReader.GetEnumerator();
                            while (dict.MoveNext())
                            {
                                writer.WriteLine($"KEY: {dict.Key}");
                                writer.WriteLine($"TYPE: {dict.Value?.GetType().FullName}");
                                writer.WriteLine($"VAL: {dict.Value}");
                                writer.WriteLine(new string('-', 40));
                            }
                            Console.WriteLine("    [OK] Extracted resource dictionary to " + outFile);
                        }
                        catch (Exception exRes)
                        {
                            Console.WriteLine("    [WARN] Could not parse " + res.Name + ": " + exRes.Message);
                        }
                    }
                }
            }
        }
    }
}
