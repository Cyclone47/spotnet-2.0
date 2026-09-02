using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Spotnet.Deployment;

namespace Spotnet.Setup;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string report = null;
        try
        {
            if (args.Length == 0) throw new ArgumentException("Expected detect or prepare.");
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length || !args[index].StartsWith("--")) throw new ArgumentException("Invalid arguments.");
                options.Add(args[index], args[index + 1]);
            }
            options.TryGetValue("--report", out report);
            if (args[0] == "detect")
            {
                options.TryGetValue("--test-root", out string testRoot);
                var discovery = testRoot != null
                    ? LegacyDiscovery.Detect(Path.Combine(testRoot, "Local"), Path.Combine(testRoot, "Roaming"), Path.Combine(testRoot, "Common"), false)
                    : LegacyDiscovery.Detect(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
                ReadCurrentPreferences(discovery, options);
                discovery.SaveIni(options["--output"]);
            }
            else if (args[0] == "complete-move")
            {
                ProfileMigration.EnsureSpotnetClosed();
                options.TryGetValue("--source-settings", out string settings);
                ProfileMigration.CompleteMove(options["--profile"], options["--source-data"], settings);
                if (report != null) File.WriteAllText(report, "Verified source profile files permanently deleted.", Encoding.UTF8);
            }
            else if (args[0] == "shortcuts" || args[0] == "restore-shortcuts")
            {
                var manager = new ShortcutManager(options["--desktop"], options["--programs"], options["--profile"]);
                // Which missing shortcuts Setup was asked to add. Absent means both, so an
                // older command line keeps the behaviour it had.
                options.TryGetValue("--create", out string create);
                var wanted = (create ?? "desktop,programs").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(entry => entry.Trim()).ToList();
                foreach (string entry in wanted)
                    if (entry != "desktop" && entry != "programs" && entry != "none") throw new ArgumentException("Unsupported shortcut location.");
                options.TryGetValue("--classic-mode", out string classicMode);
                bool? replaceClassic = true;
                if (classicMode == "alongside") replaceClassic = false;
                else if (classicMode == "auto") replaceClassic = null;
                else if (classicMode != null && classicMode != "replace") throw new ArgumentException("Unsupported Classic shortcut mode.");
                string result = args[0] == "shortcuts"
                    ? manager.Install(options["--executable"], wanted.Contains("desktop"), wanted.Contains("programs"), replaceClassic)
                    : manager.Restore();
                if (report != null) File.WriteAllText(report, result, Encoding.UTF8);
                Console.WriteLine(result);
            }
            else if (args[0] == "close")
            {
                GracefulShutdown.CloseSpotnet();
                if (report != null) File.WriteAllText(report, "Spotnet is closed.", Encoding.UTF8);
            }
            else if (args[0] == "measure")
            {
                // Advisory only: Setup shows the numbers and refuses an impossible copy up
                // front, but a measurement that fails must never block an install the real
                // migration would have completed. It keeps its own space check regardless.
                var estimate = new SpaceEstimate();
                try
                {
                    options.TryGetValue("--source-data", out string measureSource);
                    options.TryGetValue("--source-settings", out string measureSettings);
                    estimate = ProfileMigration.Measure(options["--profile"], measureSource, measureSettings);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
                {
                    estimate = new SpaceEstimate();
                }
                estimate.SaveIni(options["--output"]);
            }
            else if (args[0] == "prepare")
            {
                ProfileMigration.EnsureSpotnetClosed();
                options.TryGetValue("--source-data", out string source);
                options.TryGetValue("--source-settings", out string settings);
                options.TryGetValue("--language", out string language);
                if (language != null && language != "nl" && language != "en") throw new ArgumentException("Unsupported language.");
                options.TryGetValue("--app-theme", out string theme);
                if (theme != null && theme != "ClassicLight" && theme != "ModernLight" && theme != "ModernDark")
                    throw new ArgumentException("Unsupported style.");
                options.TryGetValue("--move-source", out string move);
                if (move != null && move != "0" && move != "1") throw new ArgumentException("Unsupported move option.");
                string result = new ProfileMigration().Prepare(options["--profile"], source, settings, Console.WriteLine, language, theme, move == "1");
                if (report != null) File.WriteAllText(report, result, Encoding.UTF8);
                Console.WriteLine(result);
            }
            else throw new ArgumentException("Unknown command.");
            return 0;
        }
        catch (Exception ex)
        {
            // Only operational messages; no configuration values or credentials are logged.
            string recovery = args.Length > 0 && args[0] == "complete-move"
                ? "The verified destination profile is retained. Source removal may be incomplete; review both locations before retrying."
                : "Profile and shortcut backups are retained. Review the error and retry; original legacy databases are unchanged.";
            string error = "Setup operation failed: " + ex.Message + "\r\n" + recovery;
            Console.Error.WriteLine(error);
            if (report != null) File.WriteAllText(report, error, Encoding.UTF8);
            return 1;
        }
    }

    /// <summary>
    /// Reads the style and language an installed profile already uses. Setup preselects
    /// them, so an upgrade that clicks straight past the wizard keeps the look it had.
    /// </summary>
    private static void ReadCurrentPreferences(LegacyDiscovery discovery, Dictionary<string, string> options)
    {
        try
        {
            if (!options.TryGetValue("--profile", out string profile) || string.IsNullOrWhiteSpace(profile))
                profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spotnet3");
            string config = Path.Combine(profile, "Data", "user.config");
            if (!File.Exists(config)) return;
            var document = ProfileSettingsFile.Normalize(ProfileSettingsFile.Load(config));
            discovery.CurrentTheme = ProfileSettingsFile.Get(document, "AppTheme") ?? "";
            discovery.CurrentLanguage = ProfileSettingsFile.Get(document, "UserLanguage") ?? "";
        }
        catch
        {
            // An unreadable profile just leaves Setup on its defaults; detection must
            // never fail because of it.
        }
    }
}
