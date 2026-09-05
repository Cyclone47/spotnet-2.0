using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Spotnet.Downloader.PostProcessing;

public static class DownloadCleanup
{
    public const string Suggestions = "nfo, sfv, srr, url, lnk, txt, doc, docx, htm, html";

    public static string[] Parse(string value)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string token in Regex.Split(value ?? "", @"[,;\s]+"))
        {
            if (token.Length == 0) continue;
            string extension = (token.StartsWith(".", StringComparison.Ordinal) ? token.Substring(1) : token).ToLowerInvariant();
            if (!Regex.IsMatch(extension, @"^[a-z0-9]+$"))
                throw new FormatException("Gebruik extensies zoals txt of .jpg, gescheiden door komma's of spaties.");
            result.Add(extension);
        }
        return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    // Called only after successful post-processing, within this job's working folder.
    public static void Run(string directory, string extensions, CancellationToken cancellationToken, Action<string> log)
    {
        var selected = new HashSet<string>(Parse(extensions), StringComparer.OrdinalIgnoreCase);
        if (selected.Count == 0) return;
        Visit(directory);

        void Visit(string folder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) return;
            foreach (string file in Directory.EnumerateFiles(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                if (!selected.Contains(Path.GetExtension(file).TrimStart('.'))) continue;
                try
                {
                    File.Delete(file);
                    log("Opgeruimd: " + Path.GetFileName(file));
                }
                catch (IOException ex) { log("Opruimen mislukt: " + ex.Message); }
                catch (UnauthorizedAccessException ex) { log("Opruimen mislukt: " + ex.Message); }
            }
            foreach (string child in Directory.EnumerateDirectories(folder)) Visit(child);
        }
    }
}
