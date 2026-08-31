using System;
using System.IO;
using System.Data.SQLite;

namespace DbDiagnostic
{
    class Program
    {
        static void Main(string[] args)
        {
            string programData = @"C:\ProgramData\Spotnet";
            string dbsPath = Path.Combine(programData, "news.newshosting.com.dbs");
            string dbcPath = Path.Combine(programData, "news.newshosting.com.dbc");

            Console.WriteLine("=================================================");
            Console.WriteLine("  Spotnet 2.0 Startup Pipeline Simulation Test   ");
            Console.WriteLine("=================================================");

            // Step 1: Check Spots DB Connection
            Console.WriteLine("\n[Step 1] Testing SpotProvider.Connect() on Spots DB...");
            try
            {
                using var conn = new SQLiteConnection($"Data Source={dbsPath};Version=3;");
                conn.Open();

                // Check indices
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index';";
                    using var r = cmd.ExecuteReader();
                    Console.WriteLine("  Indices found in spots DB:");
                    while (r.Read())
                    {
                        Console.WriteLine($"    - {r.GetString(0)}");
                    }
                }

                // Check Triggers
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='trigger';";
                    using var r = cmd.ExecuteReader();
                    Console.WriteLine("  Triggers found in spots DB:");
                    while (r.Read())
                    {
                        Console.WriteLine($"    - {r.GetString(0)}");
                    }
                }

                // Test min/max query
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT MAX(rowid), MIN(rowid), COUNT(1) FROM spots;";
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        Console.WriteLine($"  [Spots Summary] Max rowid: {r[0]}, Min rowid: {r[1]}, Count: {r[2]}");
                    }
                }

                // Test FTS4 Search Query
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT docid FROM search WHERE search MATCH 'linux' LIMIT 5;";
                    using var r = cmd.ExecuteReader();
                    Console.WriteLine("  FTS4 Search 'linux' test:");
                    int count = 0;
                    while (r.Read())
                    {
                        count++;
                        Console.WriteLine($"    - Match docid: {r[0]}");
                    }
                    Console.WriteLine($"  FTS4 matches: {count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAIL Step 1] Error on Spots DB: {ex}");
            }

            // Step 2: Check Comments DB Connection
            Console.WriteLine("\n[Step 2] Testing Comments DB...");
            try
            {
                using var conn = new SQLiteConnection($"Data Source={dbcPath};Version=3;");
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT MAX(rowid), MIN(rowid), COUNT(1) FROM comments;";
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        Console.WriteLine($"  [Comments Summary] Max: {r[0]}, Min: {r[1]}, Count: {r[2]}");
                    }
                }

                // Test FTS4 Comments Query
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT docid FROM comments WHERE comments MATCH 'bedankt' LIMIT 5;";
                    using var r = cmd.ExecuteReader();
                    Console.WriteLine("  FTS4 Comments Search 'bedankt' test:");
                    int count = 0;
                    while (r.Read())
                    {
                        count++;
                        Console.WriteLine($"    - Match docid: {r[0]}");
                    }
                    Console.WriteLine($"  FTS4 comment matches: {count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAIL Step 2] Error on Comments DB: {ex}");
            }

            // Step 3: Check userinfo table in spots DB (MinimumId)
            Console.WriteLine("\n[Step 3] Checking userinfo & MinimumId state...");
            try
            {
                using var conn = new SQLiteConnection($"Data Source={dbsPath};Version=3;Read Only=True;");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT field, value FROM userinfo;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    Console.WriteLine($"  {r[0]} = {r[1]}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAIL Step 3] userinfo table error: {ex}");
            }
        }
    }
}
