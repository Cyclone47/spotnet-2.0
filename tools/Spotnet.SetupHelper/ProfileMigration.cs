using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Spotnet.Deployment;

namespace Spotnet.Setup;

public sealed class ProfileMigration
{
    public const string ProfileMarker = "profile.ready";
    private static readonly HashSet<string> DataFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Filters.v2", "TabThemes", "Images" };

    public static bool LooksLikeData(string path) => Directory.Exists(path) &&
        (File.Exists(Path.Combine(path, "servers.xml")) || Directory.GetFiles(path, "*.dbs").Length > 0);

    public static void EnsureSpotnetClosed()
    {
        foreach (var process in Process.GetProcessesByName("Spotnet"))
        {
            using (process)
                if (!process.HasExited) throw new IOException("Close all Spotnet windows and background processes, then retry Setup. Nothing has been migrated.");
        }
    }

    public static string SafeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new IOException("An absolute directory path is required.");
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.Length < 4 || full.StartsWith("\\\\", StringComparison.Ordinal)) throw new IOException("Use a local directory, not a drive root or network share.");
        for (var current = new DirectoryInfo(full); current != null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Migration does not follow junctions or symbolic links: " + current.FullName);
        return full;
    }

    private static bool Overlaps(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase) ||
        left.StartsWith(right + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        right.StartsWith(left + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> Walk(string root, bool wholeProfile)
    {
        foreach (string file in Directory.GetFiles(root))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked files cannot be migrated: " + file);
            string name = Path.GetFileName(file);
            string extension = Path.GetExtension(name).ToLowerInvariant();
            if (wholeProfile || new[] { ".xml", ".csv", ".dat", ".txt", ".dbs", ".dbc", ".ols", ".config" }.Contains(extension) ||
                name.EndsWith(".dbs-wal", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dbc-wal", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".dbs-shm", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dbc-shm", StringComparison.OrdinalIgnoreCase)) yield return file;
        }
        foreach (string directory in Directory.GetDirectories(root))
        {
            string name = Path.GetFileName(directory);
            if (name.Equals("Logs", StringComparison.OrdinalIgnoreCase) || name.Equals("cache", StringComparison.OrdinalIgnoreCase)) continue;
            if (!wholeProfile && !DataFolders.Contains(name)) continue;
            SafeDirectory(directory);
            foreach (string file in Walk(directory, true)) yield return file;
        }
    }

    /// <summary>Read handles stay exclusively held for the whole snapshot, including WAL/SHM.</summary>
    private static void Snapshot(string source, string destination, string settingsFile, Action<string> progress, bool wholeProfile)
    {
        var files = new List<Tuple<string, FileStream>>();
        FileStream settings = null;
        try
        {
            if (source != null)
            {
                foreach (string file in Walk(source, wholeProfile).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    files.Add(Tuple.Create(file, new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None)));
            }
            if (!string.IsNullOrEmpty(settingsFile))
            {
                settingsFile = Path.GetFullPath(settingsFile);
                SafeDirectory(Path.GetDirectoryName(settingsFile));
                if ((File.GetAttributes(settingsFile) & FileAttributes.ReparsePoint) != 0) throw new IOException("Settings links are not supported.");
                settings = files.FirstOrDefault(f => f.Item1.Equals(settingsFile, StringComparison.OrdinalIgnoreCase))?.Item2;
                if (settings == null) settings = new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.None);
            }

            long required = files.Sum(f => f.Item2.Length) + (settings?.Length ?? 0) + 256L * 1024 * 1024;
            if (new DriveInfo(Path.GetPathRoot(destination)).AvailableFreeSpace < required)
                throw new IOException("Not enough free disk space for the profile copy and safety margin. The source is unchanged.");

            Directory.CreateDirectory(destination);
            int count = 0;
            foreach (var file in files)
            {
                string relative = file.Item1.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    file.Item2.CopyTo(output);
                    output.Flush(true);
                }
                file.Item2.Position = 0;
                using (var sha = SHA256.Create())
                using (var copy = File.OpenRead(target))
                {
                    if (!sha.ComputeHash(file.Item2).SequenceEqual(sha.ComputeHash(copy)))
                        throw new IOException("Copy verification failed; the source has not been changed.");
                }
                progress?.Invoke("Verified file " + (++count) + " of " + files.Count);
            }
            if (!wholeProfile)
            {
                XmlDocument config;
                if (settings != null)
                {
                    settings.Position = 0;
                    config = ProfileSettingsFile.Normalize(ProfileSettingsFile.Load(settings));
                }
                else config = ProfileSettingsFile.Empty();
                // Retain the modern TLS default even if an old profile opted out.
                ProfileSettingsFile.Set(config, "AllowInvalidServerCertificate", "False");
                ProfileSettingsFile.Set(config, "IsNewVersion", "False");
                ProfileSettingsFile.Set(config, "FiltersAreInitialized", "False");
                ProfileSettingsFile.SaveAtomic(config, Path.Combine(destination, "user.config"));
            }
        }
        finally
        {
            if (settings != null && !files.Any(f => ReferenceEquals(f.Item2, settings))) settings.Dispose();
            foreach (var file in files) file.Item2.Dispose();
        }
    }

    public string Prepare(string profileRoot, string sourceData, string sourceSettings, Action<string> progress = null)
    {
        profileRoot = SafeDirectory(profileRoot);
        string data = Path.Combine(profileRoot, "Data");
        SafeDirectory(data);
        if (!string.IsNullOrWhiteSpace(sourceData))
        {
            sourceData = SafeDirectory(sourceData);
            if (!LooksLikeData(sourceData)) throw new IOException("The selected folder is not a recognizable Spotnet data folder.");
            if (Overlaps(profileRoot, sourceData)) throw new IOException("Source and destination profiles must be separate directories.");
            ValidateServers(sourceData);
        }
        else sourceData = null;

        bool existing = Directory.Exists(data) && Directory.EnumerateFileSystemEntries(data).Any();
        if (existing && !File.Exists(Path.Combine(data, ProfileMarker)))
            throw new IOException("The destination contains an unrecognized profile. It will not be overwritten: " + data);
        Directory.CreateDirectory(profileRoot);
        using (var migrationLock = new FileStream(Path.Combine(profileRoot, "setup.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
            string stage = Path.Combine(profileRoot, "staging-" + id);
            // A failed copy stays in staging for diagnosis; never publish an incomplete profile.
            if (existing)
            {
                if (sourceData != null || !string.IsNullOrEmpty(sourceSettings))
                    throw new IOException("A Spotnet 3.0 profile already exists. Upgrade preserves it; importing over it is not allowed.");
                Snapshot(data, stage, null, progress, true);
                string backups = Path.Combine(profileRoot, "Backups");
                SafeDirectory(backups);
                Directory.CreateDirectory(backups);
                string backup = Path.Combine(backups, id);
                Directory.Move(stage, backup);
                return "Existing profile preserved. Verified pre-upgrade backup: " + backup;
            }
            Snapshot(sourceData, stage, sourceSettings, progress, false);
            File.WriteAllText(Path.Combine(stage, ProfileMarker), "Spotnet3 profile format 1\r\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(stage, "migration.txt"),
                "Created UTC: " + DateTime.UtcNow.ToString("O") + "\r\nSource data: " + (sourceData ?? "Fresh install") +
                "\r\nSource settings: " + (sourceSettings ?? "Defaults") +
                "\r\nThe source was not modified. Download queues/caches are not imported.\r\n", Encoding.UTF8);
            // Only remove an empty destination, never user data.
            if (Directory.Exists(data)) Directory.Delete(data, false);
            Directory.Move(stage, data);
            return sourceData == null && string.IsNullOrEmpty(sourceSettings) ? "Fresh Spotnet 3.0 profile created." : "Verified legacy profile copy completed; original files are unchanged.";
        }
    }

    private static void ValidateServers(string sourceData)
    {
        string path = Path.Combine(sourceData, "servers.xml");
        if (!File.Exists(path)) return;
        var document = ProfileSettingsFile.Load(path);
        var root = document.DocumentElement ?? throw new InvalidDataException("Invalid servers.xml.");
        // The C# client expects named Download/Header/Upload entries. Do not pretend VB profiles are compatible.
        var types = root.ChildNodes.OfType<XmlElement>().Select(e => e.GetAttribute("Type").ToUpperInvariant()).ToList();
        if (types.Count > 0 && (!types.Any(t => t == "DOWNLOAD" || t == "DOWNLOADS") ||
                              !types.Any(t => t == "HEADER" || t == "HEADERS") ||
                              !types.Any(t => t == "UPLOAD" || t == "UPLOADS")))
            throw new InvalidDataException("This server profile is not compatible with Spotnet 2.x/3.x. Use a fresh install and configure the provider manually.");
    }
}
