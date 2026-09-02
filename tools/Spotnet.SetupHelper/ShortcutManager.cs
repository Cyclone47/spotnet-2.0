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
    private const int ShellCreate = 0x00000002;
    private const int ShellDelete = 0x00000004;
    private const int ShellUpdateItem = 0x00002000;
    private const uint ShellPathUnicode = 0x0005;
    private const uint ShellFlush = 0x1000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern void SHChangeNotify(int eventId, uint flags,
        [MarshalAs(UnmanagedType.LPWStr)] string item1, IntPtr item2);

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

    private static bool TargetsExecutable(ShortcutInfo link, string executable)
    {
        try
        {
            string target = Environment.ExpandEnvironmentVariables((link?.Target ?? "").Trim('"'));
            return Path.GetFullPath(target).Equals(executable, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        { return false; }
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

    private static void NotifyShell(int eventId, string path)
    {
        // The helper exits immediately after writing links. Explicit, flushed shell
        // notifications keep Explorer from showing a stale Desktop/Start Menu until
        // the user refreshes it or signs in again.
        SHChangeNotify(eventId, ShellPathUnicode | ShellFlush, path, IntPtr.Zero);
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
        record.RemoveAttribute("removedForNaming");
        ProfileSettingsFile.SaveAtomic(manifest, _manifest);
        bool replacing = File.Exists(path);
        if (replacing) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
        NotifyShell(replacing ? ShellUpdateItem : ShellCreate, path);
    }

    /// <summary>
    /// Re-points every Spotnet launcher the user already has, and adds a missing one only
    /// where Setup was asked to. Declining a shortcut never touches an existing link: an
    /// upgrade must not leave a Desktop icon pointing at the old installation.
    /// </summary>
    public string Install(string executable, bool addDesktop = true, bool addPrograms = true, bool? replaceClassic = true)
    {
        executable = Path.GetFullPath(executable);
        if (!File.Exists(executable) || !Path.GetFileName(executable).Equals("Spotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("The installed Spotnet executable was not found.");
        Directory.CreateDirectory(_state);
        using (var guard = new FileStream(Path.Combine(_state, "shortcuts.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            var manifest = LoadManifest();
            string storedMode = manifest.DocumentElement.GetAttribute("classicMode");
            bool replace = replaceClassic ?? !storedMode.Equals("alongside", StringComparison.OrdinalIgnoreCase);
            manifest.DocumentElement.SetAttribute("classicMode", replace ? "replace" : "alongside");
            ProfileSettingsFile.SaveAtomic(manifest, _manifest);
            int replaced = 0, created = 0, declined = 0, preserved = 0;
            var warnings = new List<string>();
            foreach (var location in new[] { Tuple.Create(_desktop, addDesktop), Tuple.Create(_programs, addPrograms) })
            {
                string root = location.Item1;
                bool found = false;
                foreach (string path in FindLinks(root, 6))
                {
                    try
                    {
                        ValidateLinkPath(path);
                        ShortcutInfo link = Read(path);
                        if (!IsSpotnetLauncher(link)) continue;
                        if (!replace && !TargetsExecutable(link, executable)) { preserved++; continue; }
                        InstallLink(manifest, path, executable);
                        if (replace && Path.GetFileName(path).Equals("Spotnet 3.0.lnk", StringComparison.OrdinalIgnoreCase))
                        {
                            string canonical = Path.Combine(Path.GetDirectoryName(path), "Spotnet.lnk");
                            ValidateLinkPath(canonical);
                            if (File.Exists(canonical) && !IsSpotnetLauncher(Read(canonical)))
                                canonical = Path.Combine(Path.GetDirectoryName(path), "Spotnet (64-bit).lnk");
                            ValidateLinkPath(canonical);
                            if (File.Exists(canonical) && !IsSpotnetLauncher(Read(canonical)))
                                throw new IOException("Unversioned shortcut names are occupied by unrelated links.");
                            InstallLink(manifest, canonical, executable);
                            var old = manifest.DocumentElement.ChildNodes.OfType<XmlElement>()
                                .Single(e => e.GetAttribute("path").Equals(path, StringComparison.OrdinalIgnoreCase));
                            // Preserve the original for uninstall, but remove the obsolete
                            // versioned name only after its replacement has been written.
                            old.SetAttribute("removedForNaming", bool.TrueString);
                            ProfileSettingsFile.SaveAtomic(manifest, _manifest);
                            File.Delete(path);
                            NotifyShell(ShellDelete, path);
                        }
                        replaced++;
                        // A launcher inside a Desktop subfolder is not a Desktop
                        // icon. Honour the checked task by adding one at its root.
                        if (root != _desktop || Path.GetDirectoryName(path).Equals(root, StringComparison.OrdinalIgnoreCase))
                            found = true;
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is COMException)
                    { warnings.Add("Could not inspect/update shortcut: " + path); }
                }
                if (found) continue; // Keep names/locations and do not add a duplicate launcher.
                if (!location.Item2) { declined++; continue; }
                string target = Path.Combine(root, replace ? "Spotnet.lnk" : "Spotnet 3.0.lnk");
                if (File.Exists(target)) target = Path.Combine(root, replace ? "Spotnet (64-bit).lnk" : "Spotnet 3.0 (64-bit).lnk");
                if (File.Exists(target)) throw new IOException("Shortcut names are occupied by unrelated or unreadable links; they will not be overwritten.");
                InstallLink(manifest, target, executable);
                created++;
            }
            string summary = "Spotnet shortcuts updated: " + replaced + "; created: " + created + ".";
            if (preserved != 0) summary += " Classic shortcuts preserved: " + preserved + ".";
            if (declined != 0) summary += " Shortcuts you did not ask for were not added: " + declined + ".";
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
            bool exists = File.Exists(path);
            if (exists && Hash(path) != record.GetAttribute("installedHash")) continue;
            if (!exists && record.GetAttribute("removedForNaming") != bool.TrueString) continue;
            if (record.GetAttribute("created") == bool.TrueString)
            {
                if (!exists) continue;
                File.Delete(path); // Only the exact .lnk created and still owned by this installer.
                NotifyShell(ShellDelete, path);
            }
            else
            {
                string backupName = record.GetAttribute("backup");
                if (Path.GetFileName(backupName) != backupName || !backupName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Invalid shortcut backup path.");
                string backup = Path.Combine(_state, backupName);
                if (Hash(backup) != record.GetAttribute("originalHash")) throw new IOException("Shortcut backup verification failed.");
                string temporary = Path.Combine(Path.GetDirectoryName(path), ".spotnet-restore-" + Guid.NewGuid().ToString("N") + ".lnk");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Copy(backup, temporary, false);
                if (exists) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                NotifyShell(exists ? ShellUpdateItem : ShellCreate, path);
            }
            restored++;
        }
        return "Restored/removed " + restored + " managed shortcuts. User-edited links were left unchanged.";
    }
}
