using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using Microsoft.Win32;
using Spotnet.Deployment;

namespace Spotnet.Setup;

public sealed class LegacyDiscovery
{
    public List<string> DataPaths { get; } = new List<string>();
    public List<string> SettingsPaths { get; } = new List<string>();
    public List<string> Installations { get; } = new List<string>();
    public List<string> Warnings { get; } = new List<string>();

    public bool ClassicAvailable => Installations.Count != 0;

    public string PreferredDataPath => DataPaths.Count == 1 ? DataPaths[0] : "";

    public string PreferredSettingsPath => SettingsFor(PreferredDataPath);

    public string SettingsFor(string data)
    {
        if (string.IsNullOrEmpty(data)) return "";
        var colocated = SettingsPaths.Where(path => Path.GetDirectoryName(path).Equals(data, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (colocated.Length == 1) return colocated[0];
        // Never pair an unrelated .NET settings file with another provider's data.
        return DataPaths.Count == 1 && SettingsPaths.Count == 1 ? SettingsPaths[0] : "";
    }

    public static bool IsClassicInstallation(string displayName, string version)
    {
        if (string.IsNullOrWhiteSpace(displayName) ||
            !(displayName.Equals("Spotnet", StringComparison.OrdinalIgnoreCase) ||
              displayName.StartsWith("Spotnet ", StringComparison.OrdinalIgnoreCase))) return false;
        // This installer registers as "Spotnet 3.0 (64-bit)". Never mistake an
        // existing 3.x installation for the 1.8/2.x product the migration page means.
        if (displayName.StartsWith("Spotnet 3", StringComparison.OrdinalIgnoreCase)) return false;
        if (Version.TryParse(version, out Version parsed)) return (parsed.Major == 1 && parsed.Minor == 8) || parsed.Major == 2;
        return displayName.Equals("Spotnet", StringComparison.OrdinalIgnoreCase) ||
               displayName.StartsWith("Spotnet 1.8", StringComparison.OrdinalIgnoreCase) ||
               displayName.StartsWith("Spotnet 2", StringComparison.OrdinalIgnoreCase);
    }

    public static LegacyDiscovery Detect(string local, string roaming, string common, bool registry = true)
    {
        var result = new LegacyDiscovery();
        var data = new List<string> { Path.Combine(common, "Spotnet"), Path.Combine(local, "Spotnet", "Data") };
        // Squirrel installations may have no usable uninstall entry. Require the
        // actual versioned executable, never just an abandoned data directory.
        string localClassic = Path.Combine(local, "Spotnet");
        if (HasClassicExecutable(localClassic))
        {
            result.Installations.Add("Spotnet Classic");
            data.Add(localClassic);
        }
        if (registry)
        {
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using (var root = RegistryKey.OpenBaseKey(hive, view))
                    using (var uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (uninstall == null) continue;
                        foreach (string name in uninstall.GetSubKeyNames())
                        using (var key = uninstall.OpenSubKey(name))
                        {
                            string display = key?.GetValue("DisplayName") as string;
                            if (!IsClassicInstallation(display, key?.GetValue("DisplayVersion") as string)) continue;
                            string version = key.GetValue("DisplayVersion") as string ?? "unknown";
                            string location = key.GetValue("InstallLocation") as string;
                            // Ignore stale uninstall registry entries whose application is gone.
                            var locations = new[] { location, Path.Combine(local, "Spotnet"),
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Spotnet"),
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Spotnet") };
                            if (!locations.Any(HasClassicExecutable)) continue;
                            result.Installations.Add(display + " " + version);
                            if (!string.IsNullOrWhiteSpace(location) && Directory.Exists(location))
                            {
                                data.Add(Path.Combine(location, "Data"));
                                data.Add(location);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException)
                { result.Warnings.Add("Some installation registry entries could not be read."); }
            }
        }
        foreach (string candidate in data.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (ProfileMigration.LooksLikeData(candidate)) result.DataPaths.Add(ProfileMigration.SafeDirectory(candidate));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { result.Warnings.Add("A possible legacy data folder could not be inspected."); }
        }
        foreach (string root in new[] { local, roaming }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root)) continue;
            foreach (string directory in Directory.GetDirectories(root, "Spotnet*"))
                result.FindSettings(directory, 4);
        }
        foreach (string candidate in result.DataPaths) result.FindSettings(candidate, 0);
        // ClickOnce data roots are bounded; no general profile-wide recursive scan.
        result.FindSettings(Path.Combine(local, "Apps", "2.0", "Data"), 5);
        return result;
    }

    public static bool HasClassicExecutable(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return false;
        try
        {
            ProfileMigration.SafeDirectory(directory);
            var candidates = new[] { Path.Combine(directory, "Spotnet.exe") }
                .Concat(Directory.GetDirectories(directory, "app-*").Select(path => Path.Combine(path, "Spotnet.exe")));
            foreach (string candidate in candidates)
            {
                if (!File.Exists(candidate)) continue;
                ProfileMigration.SafeDirectory(Path.GetDirectoryName(candidate));
                var version = FileVersionInfo.GetVersionInfo(candidate);
                if (version.FileMajorPart == 2 || (version.FileMajorPart == 1 && version.FileMinorPart == 8)) return true;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        { }
        return false;
    }

    private int _visited;
    private void FindSettings(string directory, int depth)
    {
        if (!Directory.Exists(directory) || ++_visited > 5000) return;
        try
        {
            ProfileMigration.SafeDirectory(directory);
            foreach (string name in new[] { "user.config", "settings.xml" })
            {
                string path = Path.Combine(directory, name);
                if (!File.Exists(path) || new FileInfo(path).Length > 16 * 1024 * 1024) continue;
                // Never offer the new installed profile as a legacy settings source.
                if (path.StartsWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spotnet3") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var document = ProfileSettingsFile.Load(path);
                    if (ProfileSettingsFile.Section(document) != null || document.DocumentElement?.Name == "Settings")
                        if (!SettingsPaths.Contains(path)) SettingsPaths.Add(path);
                }
                catch (Exception ex) when (ex is System.Xml.XmlException || ex is IOException || ex is UnauthorizedAccessException)
                { Warnings.Add("A settings candidate is unreadable or malformed; select a valid file manually if needed."); }
            }
            if (depth > 0)
                foreach (string child in Directory.GetDirectories(directory)) FindSettings(child, depth - 1);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        { Warnings.Add("A settings directory could not be inspected."); }
    }

    /// <summary>
    /// The style and language the installed profile already uses, so Setup can preselect
    /// them. Leaving the wizard alone then means "keep what I have" rather than silently
    /// repainting an upgraded install.
    /// </summary>
    public string CurrentTheme { get; set; } = "";
    public string CurrentLanguage { get; set; } = "";

    public void SaveIni(string path)
    {
        var text = new StringBuilder("[Detection]\r\n");
        WriteList(text, "Data", DataPaths);
        WriteList(text, "Settings", SettingsPaths);
        WriteList(text, "Install", Installations.Distinct().ToList());
        WriteList(text, "Warning", Warnings.Distinct().ToList());
        text.AppendLine("ClassicAvailable=" + (ClassicAvailable ? "1" : "0"));
        text.AppendLine("ClassicName=" + (Installations.FirstOrDefault() ?? "").Replace("\r", " ").Replace("\n", " "));
        text.AppendLine("ClassicData=" + PreferredDataPath.Replace("\r", " ").Replace("\n", " "));
        text.AppendLine("ClassicSettings=" + PreferredSettingsPath.Replace("\r", " ").Replace("\n", " "));
        for (int index = 0; index < DataPaths.Count; index++)
            text.AppendLine("DataSettings" + index + "=" + SettingsFor(DataPaths[index]).Replace("\r", " ").Replace("\n", " "));
        text.AppendLine("CurrentTheme=" + CurrentTheme);
        text.AppendLine("CurrentLanguage=" + CurrentLanguage);
        File.WriteAllText(path, text.ToString(), Encoding.Unicode);
    }

    private static void WriteList(StringBuilder text, string prefix, IList<string> values)
    {
        text.AppendLine(prefix + "Count=" + values.Count);
        for (int index = 0; index < values.Count; index++)
            text.AppendLine(prefix + index + "=" + values[index].Replace("\r", " ").Replace("\n", " "));
    }
}
