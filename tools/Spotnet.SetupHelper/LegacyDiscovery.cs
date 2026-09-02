using System;
using System.Collections.Generic;
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

    public static LegacyDiscovery Detect(string local, string roaming, string common, bool registry = true)
    {
        var result = new LegacyDiscovery();
        var data = new List<string> { Path.Combine(common, "Spotnet"), Path.Combine(local, "Spotnet", "Data") };
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
                            if (display == null || !(display.Equals("Spotnet", StringComparison.OrdinalIgnoreCase) || display.StartsWith("Spotnet ", StringComparison.OrdinalIgnoreCase))) continue;
                            string version = key.GetValue("DisplayVersion") as string ?? "unknown";
                            string location = key.GetValue("InstallLocation") as string;
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
