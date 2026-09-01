using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
                LegacyDiscovery.Detect(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)).SaveIni(options["--output"]);
            }
            else if (args[0] == "shortcuts" || args[0] == "restore-shortcuts")
            {
                var manager = new ShortcutManager(options["--desktop"], options["--programs"], options["--profile"]);
                string result = args[0] == "shortcuts" ? manager.Install(options["--executable"]) : manager.Restore();
                if (report != null) File.WriteAllText(report, result, Encoding.Unicode);
                Console.WriteLine(result);
            }
            else if (args[0] == "close")
            {
                GracefulShutdown.CloseSpotnet();
                if (report != null) File.WriteAllText(report, "Spotnet is closed.", Encoding.Unicode);
            }
            else if (args[0] == "prepare")
            {
                ProfileMigration.EnsureSpotnetClosed();
                options.TryGetValue("--source-data", out string source);
                options.TryGetValue("--source-settings", out string settings);
                options.TryGetValue("--language", out string language);
                if (language != null && language != "nl" && language != "en") throw new ArgumentException("Unsupported language.");
                string result = new ProfileMigration().Prepare(options["--profile"], source, settings, Console.WriteLine, language);
                if (report != null) File.WriteAllText(report, result, Encoding.Unicode);
                Console.WriteLine(result);
            }
            else throw new ArgumentException("Unknown command.");
            return 0;
        }
        catch (Exception ex)
        {
            // Only operational messages; no configuration values or credentials are logged.
            string error = "Setup operation failed: " + ex.Message + "\r\nProfile and shortcut backups are retained. Review the error and retry; original legacy databases are unchanged.";
            Console.Error.WriteLine(error);
            if (report != null) File.WriteAllText(report, error, Encoding.Unicode);
            return 1;
        }
    }
}
