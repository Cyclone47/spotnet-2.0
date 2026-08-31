using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ICSharpCode.Decompiler.Metadata;

namespace BamlExtractor
{
    public class ClassComparer
    {
        public static void Compare()
        {
            string exePath = @"d:\sourcecode\spotnet-2.0.0.284-binary\Spotnet.exe";
            var peFile = new PEFile(exePath);
            var meta = peFile.Metadata;

            var types20 = new List<string>();
            foreach (var handle in meta.TypeDefinitions)
            {
                var td = meta.GetTypeDefinition(handle);
                string ns = meta.GetString(td.Namespace);
                string name = meta.GetString(td.Name);
                if (string.IsNullOrEmpty(ns) && (name.StartsWith("<") || name.StartsWith("__"))) continue;
                types20.Add(string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}");
            }

            Console.WriteLine($"Spotnet 2.0 Total Named Types: {types20.Count}");

            // Group by namespace
            var byNs = types20.GroupBy(t => t.Contains('.') ? t.Substring(0, t.LastIndexOf('.')) : "<global>").OrderBy(g => g.Key);
            foreach (var g in byNs)
            {
                Console.WriteLine($"  Namespace [{g.Key}]: {g.Count()} types");
            }

            // Read 1.8.1 files
            var files181 = Directory.GetFiles(@"d:\sourcecode\spotnet-git", "*.vb", SearchOption.AllDirectories);
            Console.WriteLine($"Spotnet 1.8.1 VB Files: {files181.Length}");
            foreach (var f in files181)
            {
                Console.WriteLine($"    1.8.1 File: {Path.GetFileName(f)}");
            }
        }
    }
}
