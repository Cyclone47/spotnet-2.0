using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Spotnet.Deployment;

namespace Spotnet.Setup;

public sealed class ShortcutInfo
{
    public string Target { get; set; }
    public string Arguments { get; set; }
    public string WorkingDirectory { get; set; }
    public string Icon { get; set; }
}

/// <summary>Only current-user Desktop/Start Menu launch links; never uninstall links or global pins.</summary>
public sealed class ShortcutManager
{
    private readonly string _desktop;
    private readonly string _programs;
    private readonly string _state;
    private readonly string _manifest;

    public ShortcutManager(string desktop, string programs, string profile)
    {
        _desktop = ProfileMigration.SafeDirectory(desktop);
        _programs = ProfileMigration.SafeDirectory(programs);
        _state = Path.Combine(ProfileMigration.SafeDirectory(profile), "ShortcutBackups");
        ProfileMigration.SafeDirectory(_state);
        _manifest = Path.Combine(_state, "shortcuts.xml");
    }

    public static ShortcutInfo Read(string path)
    {
        // WScript can return an empty link for an unreadable existing file. Probe first
        // so a locked legacy launcher is reported instead of mistaken for an unrelated link.
        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { }
        object shell = null, link = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            link = ((dynamic)shell).CreateShortcut(path);
            dynamic shortcut = link;
            return new ShortcutInfo { Target = shortcut.TargetPath, Arguments = shortcut.Arguments,
                WorkingDirectory = shortcut.WorkingDirectory, Icon = shortcut.IconLocation };
        }
        finally
        {
            if (link != null) Marshal.FinalReleaseComObject(link);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }

    public static void Write(string path, string executable)
    {
        object shell = null, link = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            link = ((dynamic)shell).CreateShortcut(path);
            dynamic shortcut = link;
            shortcut.TargetPath = executable;
            shortcut.Arguments = "";
            shortcut.WorkingDirectory = Path.GetDirectoryName(executable);
            shortcut.IconLocation = executable + ",0";
            shortcut.Description = "Spotnet 3.0 (64-bit)";
            shortcut.Save();
        }
        finally
        {
            if (link != null) Marshal.FinalReleaseComObject(link);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }

    public static bool IsSpotnetLauncher(ShortcutInfo link)
    {
        if (link == null || string.IsNullOrWhiteSpace(link.Target)) return false;
        string target = Environment.ExpandEnvironmentVariables(link.Target.Trim('"'));
        string arguments = (link.Arguments ?? "").Trim();
        if (Regex.IsMatch(arguments, @"(?:^|\s)(?:--?uninstall|/uninstall|--squirrel-\S+|--exitOnUninstall)(?:\s|$)", RegexOptions.IgnoreCase)) return false;
        if (Path.GetFileName(target).Equals("Spotnet.exe", StringComparison.OrdinalIgnoreCase)) return true;
        // Squirrel links point to Update.exe rather than the versioned application.
        if (!Path.GetFileName(target).Equals("Update.exe", StringComparison.OrdinalIgnoreCase)) return false;
        string parent = Path.GetFileName(Path.GetDirectoryName(target));
        return Regex.IsMatch(parent ?? "", @"^Spotnet(?:$|[ ._-])", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(arguments, "^--processStart(?:AndWait)?\\s+(?:\"Spotnet\\.exe\"|Spotnet\\.exe)(?:\\s+--process-start-args(?:\\s|=).*)?$", RegexOptions.IgnoreCase);
    }

    private bool WithinRoots(string path)
    {
        string full = Path.GetFullPath(path);
        return full.StartsWith(_desktop + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               full.StartsWith(_programs + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateLinkPath(string path)
    {
        if (!WithinRoots(path) || !Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Shortcut path is outside the current-user Desktop/Start Menu.");
        ProfileMigration.SafeDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Linked shortcut files are not followed.");
    }

    private static IEnumerable<string> FindLinks(string root, int depth)
    {
        if (!Directory.Exists(root)) yield break;
        ProfileMigration.SafeDirectory(root);
        foreach (string file in Directory.GetFiles(root, "*.lnk")) yield return file;
        if (depth <= 0) yield break;
        foreach (string child in Directory.GetDirectories(root))
        {
            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
            foreach (string file in FindLinks(child, depth - 1)) yield return file;
        }
    }

    private XmlDocument LoadManifest()
    {
        if (File.Exists(_manifest))
        {
            var existing = ProfileSettingsFile.Load(_manifest);
            if (existing.DocumentElement?.Name != "shortcuts") throw new IOException("Invalid shortcut backup manifest.");
            return existing;
        }
        var document = new XmlDocument { XmlResolver = null };
        document.AppendChild(document.CreateElement("shortcuts"));
        return document;
    }

    private static string Hash(string file)
    {
        using (var stream = File.OpenRead(file))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private void InstallLink(XmlDocument manifest, string path, string executable)
    {
        ValidateLinkPath(path);
        var record = manifest.DocumentElement.ChildNodes.OfType<XmlElement>()
            .FirstOrDefault(e => e.GetAttribute("path").Equals(path, StringComparison.OrdinalIgnoreCase));
        if (record == null)
        {
            record = manifest.CreateElement("link");
            record.SetAttribute("path", path);
            record.SetAttribute("created", (!File.Exists(path)).ToString());
            if (File.Exists(path))
            {
                string backup = Guid.NewGuid().ToString("N") + ".lnk";
                File.Copy(path, Path.Combine(_state, backup), false);
                record.SetAttribute("backup", backup);
                record.SetAttribute("originalHash", Hash(path));
            }
            manifest.DocumentElement.AppendChild(record);
            // Journal the backup before replacing anything.
            ProfileSettingsFile.SaveAtomic(manifest, _manifest);
        }
        string temporary = Path.Combine(Path.GetDirectoryName(path), ".spotnet-" + Guid.NewGuid().ToString("N") + ".lnk");
        Write(temporary, executable);
        record.SetAttribute("installedHash", Hash(temporary));
        record.SetAttribute("executable", executable);
        ProfileSettingsFile.SaveAtomic(manifest, _manifest);
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }

    public string Install(string executable)
    {
        executable = Path.GetFullPath(executable);
        if (!File.Exists(executable) || !Path.GetFileName(executable).Equals("Spotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("The installed Spotnet executable was not found.");
        Directory.CreateDirectory(_state);
        using (var guard = new FileStream(Path.Combine(_state, "shortcuts.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            var manifest = LoadManifest();
            int replaced = 0, created = 0;
            var warnings = new List<string>();
            foreach (string root in new[] { _desktop, _programs })
            {
                bool found = false;
                foreach (string path in FindLinks(root, 6))
                {
                    try
                    {
                        ValidateLinkPath(path);
                        if (!IsSpotnetLauncher(Read(path))) continue;
                        InstallLink(manifest, path, executable);
                        replaced++;
                        found = true;
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is COMException)
                    { warnings.Add("Could not inspect/update shortcut: " + path); }
                }
                if (found) continue; // Keep names/locations and do not add a duplicate launcher.
                string target = Path.Combine(root, "Spotnet.lnk");
                if (File.Exists(target)) target = Path.Combine(root, "Spotnet 3.0.lnk");
                if (File.Exists(target)) target = Path.Combine(root, "Spotnet 3.0 (64-bit).lnk");
                if (File.Exists(target)) throw new IOException("Shortcut names are occupied by unrelated or unreadable links; they will not be overwritten.");
                InstallLink(manifest, target, executable);
                created++;
            }
            string summary = "Spotnet shortcuts updated: " + replaced + "; created: " + created + ".";
            if (warnings.Count != 0) throw new IOException(summary + "\r\n" + string.Join("\r\n", warnings));
            return summary;
        }
    }

    public string Restore()
    {
        if (!File.Exists(_manifest)) return "No managed shortcuts to restore.";
        var document = LoadManifest();
        int restored = 0;
        foreach (var record in document.DocumentElement.ChildNodes.OfType<XmlElement>())
        {
            string path = record.GetAttribute("path");
            ValidateLinkPath(path);
            // Respect shortcuts that the user changed after Setup.
            if (!File.Exists(path) || Hash(path) != record.GetAttribute("installedHash")) continue;
            if (record.GetAttribute("created") == bool.TrueString)
                File.Delete(path); // Only the exact .lnk created and still owned by this installer.
            else
            {
                string backupName = record.GetAttribute("backup");
                if (Path.GetFileName(backupName) != backupName || !backupName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Invalid shortcut backup path.");
                string backup = Path.Combine(_state, backupName);
                if (Hash(backup) != record.GetAttribute("originalHash")) throw new IOException("Shortcut backup verification failed.");
                string temporary = Path.Combine(Path.GetDirectoryName(path), ".spotnet-restore-" + Guid.NewGuid().ToString("N") + ".lnk");
                File.Copy(backup, temporary, false);
                File.Replace(temporary, path, null);
            }
            restored++;
        }
        return "Restored/removed " + restored + " managed shortcuts. User-edited links were left unchanged.";
    }
}
