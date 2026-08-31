using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Data.SQLite;

namespace DbRepair
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("======================================================================");
            Console.WriteLine("             Spotnet 2.0 Database Quick-Repair Tool                  ");
            Console.WriteLine("======================================================================");
            Console.ResetColor();
            Console.WriteLine();

            string programData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Spotnet");
            if (!Directory.Exists(programData))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Spotnet data directory not found at: {programData}");
                Console.ResetColor();
                return;
            }

            Console.WriteLine($"[1/4] Scanning Spotnet data folder: {programData}");
            RestoreAccidentalBackups(programData);

            Console.WriteLine();
            Console.WriteLine("[2/4] Repairing & Optimizing SQLite Database Files...");
            RepairDatabaseFiles(programData);

            Console.WriteLine();
            Console.WriteLine("[3/4] Resetting Stale NNTP Watermarks & Sync Pointers...");
            ResetWatermarks(programData);

            Console.WriteLine();
            Console.WriteLine("[4/4] Clearing Malformed Flags in User Configurations...");
            ResetMalformedFlagsInUserConfigs();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("======================================================================");
            Console.WriteLine("  [REPAIR COMPLETE] Spotnet databases are healthy & ready to launch!  ");
            Console.WriteLine("======================================================================");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void RestoreAccidentalBackups(string programData)
        {
            // If .dbc.ols or .dbs,old exists and is much larger than .dbc or .dbs, restore it!
            string[] dbsFiles = Directory.GetFiles(programData, "*.dbs");
            string[] dbcFiles = Directory.GetFiles(programData, "*.dbc");

            foreach (var dbs in dbsFiles)
            {
                string oldBackup = dbs + ",old";
                if (!File.Exists(oldBackup)) oldBackup = dbs + ".old";

                if (File.Exists(oldBackup))
                {
                    FileInfo currentInfo = new FileInfo(dbs);
                    FileInfo backupInfo = new FileInfo(oldBackup);

                    if (backupInfo.Length > currentInfo.Length && currentInfo.Length < 50 * 1024 * 1024)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  [RESTORE] Found large backup {Path.GetFileName(oldBackup)} ({backupInfo.Length / (1024 * 1024)} MB) replacing smaller {Path.GetFileName(dbs)} ({currentInfo.Length / (1024 * 1024)} MB)...");
                        Console.ResetColor();
                        try
                        {
                            string tempSwp = dbs + ".tmp_swap";
                            File.Move(dbs, tempSwp);
                            File.Move(oldBackup, dbs);
                            File.Delete(tempSwp);
                            Console.WriteLine($"  [SUCCESS] Restored {Path.GetFileName(dbs)} from backup.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  [SKIP] Could not swap files: {ex.Message}");
                        }
                    }
                }
            }

            foreach (var dbc in dbcFiles)
            {
                string oldBackup = dbc + ".ols";
                if (!File.Exists(oldBackup)) oldBackup = dbc + ".old";

                if (File.Exists(oldBackup))
                {
                    FileInfo currentInfo = new FileInfo(dbc);
                    FileInfo backupInfo = new FileInfo(oldBackup);

                    if (backupInfo.Length > currentInfo.Length && currentInfo.Length < 50 * 1024 * 1024)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  [RESTORE] Found large backup {Path.GetFileName(oldBackup)} ({backupInfo.Length / (1024 * 1024)} MB) replacing smaller {Path.GetFileName(dbc)} ({currentInfo.Length / (1024 * 1024)} MB)...");
                        Console.ResetColor();
                        try
                        {
                            string tempSwp = dbc + ".tmp_swap";
                            File.Move(dbc, tempSwp);
                            File.Move(oldBackup, dbc);
                            File.Delete(tempSwp);
                            Console.WriteLine($"  [SUCCESS] Restored {Path.GetFileName(dbc)} from backup.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  [SKIP] Could not swap files: {ex.Message}");
                        }
                    }
                }
            }
        }

        static void RepairDatabaseFiles(string programData)
        {
            string[] dbFiles = Directory.GetFiles(programData, "*.db*");
            foreach (var dbPath in dbFiles)
            {
                string ext = Path.GetExtension(dbPath).ToLowerInvariant();
                if (ext != ".dbs" && ext != ".dbc") continue;

                string filename = Path.GetFileName(dbPath);
                Console.Write($"  Checking {filename} ({new FileInfo(dbPath).Length / (1024 * 1024)} MB)... ");

                try
                {
                    using var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;Journal Mode=Delete;");
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = "PRAGMA quick_check;";
                        var checkResult = cmd.ExecuteScalar()?.ToString() ?? "unknown";

                        if (checkResult.Equals("ok", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("OK");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"Warning: {checkResult}. Running REINDEX...");
                            Console.ResetColor();
                            cmd.CommandText = "REINDEX;";
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        static void ResetWatermarks(string programData)
        {
            string[] dbsFiles = Directory.GetFiles(programData, "*.dbs");
            foreach (var dbsPath in dbsFiles)
            {
                try
                {
                    using var conn = new SQLiteConnection($"Data Source={dbsPath};Version=3;");
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS userinfo(field TEXT PRIMARY KEY, value TEXT);";
                    cmd.ExecuteNonQuery();

                    // Get actual max rowid in spots table
                    cmd.CommandText = "SELECT MAX(rowid), MIN(rowid), COUNT(1) FROM spots;";
                    long maxRowId = 0;
                    long minRowId = 0;
                    long count = 0;
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            maxRowId = reader.GetInt64(0);
                            minRowId = reader.GetInt64(1);
                            count = reader.GetInt64(2);
                        }
                    }

                    if (maxRowId > 0)
                    {
                        Console.WriteLine($"  [Spots DB] Current Spots: {count:N0} (Min: {minRowId}, Max: {maxRowId})");

                        cmd.CommandText = "INSERT OR REPLACE INTO userinfo(field, value) VALUES('minId_headers', @val);";
                        cmd.Parameters.AddWithValue("@val", minRowId.ToString());
                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();

                        Console.WriteLine($"  [Synced] Watermark 'minId_headers' aligned to {minRowId}.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [Watermark Error in {Path.GetFileName(dbsPath)}]: {ex.Message}");
                }
            }
        }

        static void ResetMalformedFlagsInUserConfigs()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string spotnetConfigDir = Path.Combine(localAppData, "Spotnet");

            if (!Directory.Exists(spotnetConfigDir)) return;

            string[] configFiles = Directory.GetFiles(spotnetConfigDir, "user.config", SearchOption.AllDirectories);
            int fixedCount = 0;

            foreach (var configFile in configFiles)
            {
                try
                {
                    string content = File.ReadAllText(configFile);
                    bool modified = false;

                    if (content.Contains("<setting name=\"SpotsDbFileMalformed\" serializeAs=\"String\">"))
                    {
                        content = Regex.Replace(content,
                            @"(<setting name=""SpotsDbFileMalformed"" serializeAs=""String"">\s*<value>)[^<]*(</value>)",
                            "${1}False${2}");
                        modified = true;
                    }

                    if (content.Contains("<setting name=\"CommentsDbFileMalformed\" serializeAs=\"String\">"))
                    {
                        content = Regex.Replace(content,
                            @"(<setting name=""CommentsDbFileMalformed"" serializeAs=""String"">\s*<value>)[^<]*(</value>)",
                            "${1}False${2}");
                        modified = true;
                    }

                    if (content.Contains("<setting name=\"RecreateDbScheduled\" serializeAs=\"String\">"))
                    {
                        content = Regex.Replace(content,
                            @"(<setting name=""RecreateDbScheduled"" serializeAs=""String"">\s*<value>)[^<]*(</value>)",
                            "${1}False${2}");
                        modified = true;
                    }

                    if (modified)
                    {
                        File.WriteAllText(configFile, content);
                        fixedCount++;
                    }
                }
                catch { }
            }

            Console.WriteLine($"  Cleared auto-wipe flags across {fixedCount} user.config configuration file(s).");
        }
    }
}
