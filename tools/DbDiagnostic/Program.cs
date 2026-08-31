using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DbDiagnostic
{
    /// <summary>
    /// Diagnostics and benchmarks for the Spotnet database and import hot paths.
    ///
    ///   DbDiagnostic inspect [path]   report on a real database (default: ProgramData)
    ///   DbDiagnostic bench   [rows]   measure the journalling and RSA changes
    ///
    /// The benchmarks are deliberately self-contained - they measure the framework and
    /// SQLite behaviour the application depends on, without dragging in the WPF assembly.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0].ToLowerInvariant() : "inspect";

            try
            {
                switch (command)
                {
                    case "bench":
                        int rows = args.Length > 1 && int.TryParse(args[1], out int parsed) ? parsed : 50000;
                        RunBenchmarks(rows);
                        return 0;

                    case "inspect":
                        Inspect(args.Length > 1 ? args[1] : null);
                        return 0;

                    default:
                        Console.WriteLine("Usage: DbDiagnostic [inspect <path>] | [bench <rows>]");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("FAILED: " + ex.Message);
                return 1;
            }
        }

        // ------------------------------------------------------------------ inspect

        private static void Inspect(string path)
        {
            IEnumerable<string> databases = path != null
                ? new[] { path }
                : DiscoverDatabases();

            Header("Spotnet database inspection");

            bool any = false;
            foreach (string db in databases)
            {
                any = true;
                Console.WriteLine();
                Console.WriteLine(db);
                if (!File.Exists(db))
                {
                    Console.WriteLine("  (not found)");
                    continue;
                }

                var info = new FileInfo(db);
                Console.WriteLine($"  size            {Mb(info.Length)}");
                ReportSidecar(db, "-wal");
                ReportSidecar(db, "-shm");

                try
                {
                    using var conn = new SQLiteConnection($"Data Source={db};Version=3;BusyTimeout=5000;");
                    conn.Open();

                    string journal = Scalar(conn, "PRAGMA journal_mode");
                    Console.WriteLine($"  journal_mode    {journal}{(journal.Equals("wal", StringComparison.OrdinalIgnoreCase) ? "" : "   <-- expected wal")}");
                    Console.WriteLine($"  page_size       {Scalar(conn, "PRAGMA page_size")}");
                    // synchronous is a per-connection setting, not a property of the file,
                    // so this reports what THIS tool negotiated - not what Spotnet uses.
                    // Labelled to stop it being read as evidence about the application.
                    Console.WriteLine($"  synchronous     {Describe(Scalar(conn, "PRAGMA synchronous"))}  (this connection, not Spotnet's)");
                    Console.WriteLine($"  user_version    {Scalar(conn, "PRAGMA user_version")}");

                    Console.WriteLine($"  tables          {string.Join(", ", Names(conn, "table"))}");
                    Console.WriteLine($"  indexes         {string.Join(", ", Names(conn, "index"))}");
                    Console.WriteLine($"  triggers        {string.Join(", ", Names(conn, "trigger"))}");

                    if (Names(conn, "table").Contains("spots"))
                    {
                        var sw = Stopwatch.StartNew();
                        string count = Scalar(conn, "SELECT COUNT(1) FROM spots");
                        sw.Stop();
                        Console.WriteLine($"  spots           {count}  (COUNT took {sw.ElapsedMilliseconds} ms)");
                        Console.WriteLine($"  rowid range     {Scalar(conn, "SELECT IFNULL(MIN(rowid),0) FROM spots")} .. {Scalar(conn, "SELECT IFNULL(MAX(rowid),0) FROM spots")}");
                    }

                    var check = Stopwatch.StartNew();
                    string integrity = Scalar(conn, "PRAGMA quick_check(1)");
                    check.Stop();
                    Console.WriteLine($"  quick_check     {integrity}  ({check.ElapsedMilliseconds} ms)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  ERROR: " + ex.Message);
                }
            }

            if (!any)
            {
                Console.WriteLine();
                Console.WriteLine("No databases found. Pass a path explicitly:");
                Console.WriteLine("  DbDiagnostic inspect C:\\ProgramData\\Spotnet\\<server>.dbs");
            }
        }

        private static IEnumerable<string> DiscoverDatabases()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Spotnet");
            if (!Directory.Exists(folder))
            {
                return Enumerable.Empty<string>();
            }
            return Directory.GetFiles(folder, "*.db?")
                .Where(f => f.EndsWith(".dbs", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".dbc", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f);
        }

        private static void ReportSidecar(string db, string suffix)
        {
            if (File.Exists(db + suffix))
            {
                Console.WriteLine($"  {suffix,-15} {Mb(new FileInfo(db + suffix).Length)}");
            }
        }

        private static string Describe(string synchronous)
        {
            switch (synchronous)
            {
                case "0": return "0 (OFF - not crash safe)";
                case "1": return "1 (NORMAL)";
                case "2": return "2 (FULL)";
                default: return synchronous;
            }
        }

        // ----------------------------------------------------------------- benchmark

        private static void RunBenchmarks(int rows)
        {
            Header($"Spotnet import benchmarks ({rows:N0} rows)");
            Console.WriteLine();
            Console.WriteLine("Numbers are indicative, not absolute: they depend on disk and");
            Console.WriteLine("antivirus. What matters is the ratio between the two rows.");

            BenchmarkJournalling(rows);
            BenchmarkRsaVerification(Math.Min(rows, 20000));
        }

        private static void BenchmarkJournalling(int rows)
        {
            Section("Journalling: rollback journal vs write-ahead log");

            // The old configuration: rollback journal, no durability at all.
            var legacy = TimeInserts(rows, "PRAGMA journal_mode = DELETE", "PRAGMA synchronous = OFF");
            // The old configuration with durability turned on - what it would have cost
            // to make the previous scheme crash safe.
            var legacySafe = TimeInserts(rows, "PRAGMA journal_mode = DELETE", "PRAGMA synchronous = NORMAL");
            // What ships now.
            var wal = TimeInserts(rows, "PRAGMA journal_mode = WAL", "PRAGMA synchronous = NORMAL");

            Row("DELETE + synchronous=OFF", legacy, rows, "was: fast, NOT crash safe");
            Row("DELETE + synchronous=NORMAL", legacySafe, rows, "the honest cost of the old scheme");
            Row("WAL + synchronous=NORMAL", wal, rows, "ships now: crash safe");

            Console.WriteLine();
            Console.WriteLine($"  WAL vs the old unsafe setting: {Ratio(legacy, wal)}");
            Console.WriteLine($"  WAL vs a crash-safe rollback journal: {Ratio(legacySafe, wal)}");
        }

        private static TimeSpan TimeInserts(int rows, params string[] pragmas)
        {
            string file = Path.Combine(Path.GetTempPath(), "spotnet_bench_" + Guid.NewGuid().ToString("N") + ".dbs");
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={file};Version=3;BusyTimeout=5000;"))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "PRAGMA page_size = 8192";
                    cmd.ExecuteNonQuery();
                    foreach (string pragma in pragmas)
                    {
                        cmd.CommandText = pragma;
                        cmd.ExecuteNonQuery();
                    }
                    cmd.CommandText = "CREATE TABLE spots(rowid INTEGER PRIMARY KEY, key INT, cat INT, date INT, filesize INTEGER, cats TEXT, sender TEXT, subject TEXT, msgid TEXT, modulus TEXT)";
                    cmd.ExecuteNonQuery();

                    var sw = Stopwatch.StartNew();
                    // Batched the way the importer does it, rather than one giant commit.
                    const int batchSize = 5000;
                    for (int start = 0; start < rows; start += batchSize)
                    {
                        using var tx = conn.BeginTransaction();
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT INTO spots(rowid, key, cat, date, filesize, cats, sender, subject, msgid, modulus) VALUES(?,?,?,?,?,?,?,?,?,?)";
                        var ps = new SQLiteParameter[10];
                        for (int i = 0; i < ps.Length; i++)
                        {
                            ps[i] = cmd.CreateParameter();
                            cmd.Parameters.Add(ps[i]);
                        }
                        int end = Math.Min(start + batchSize, rows);
                        for (int i = start; i < end; i++)
                        {
                            ps[0].Value = i + 1;
                            ps[1].Value = 3;
                            ps[2].Value = 3;
                            ps[3].Value = 1700000000L;
                            ps[4].Value = 1048576L;
                            ps[5].Value = "3 a01";
                            ps[6].Value = "poster" + (i % 500);
                            ps[7].Value = "A reasonably typical spot subject line " + i;
                            ps[8].Value = "msg" + i + "@spot.net";
                            ps[9].Value = "AAAABBBBCCCC";
                            cmd.ExecuteNonQuery();
                        }
                        tx.Commit();
                        cmd.Parameters.Clear();
                        cmd.Transaction = null;
                    }
                    sw.Stop();
                    return sw.Elapsed;
                }
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
                {
                    try
                    {
                        if (File.Exists(file + suffix))
                        {
                            File.Delete(file + suffix);
                        }
                    }
                    catch (IOException)
                    {
                        // Scratch file; not worth failing the run over.
                    }
                }
            }
        }

        private static void BenchmarkRsaVerification(int verifications)
        {
            Section("Signature verification: per-spot key container vs cached verifier");
            Console.WriteLine();
            Console.WriteLine("  Header import verifies one signature per spot, and used to construct a");
            Console.WriteLine("  fresh RSACryptoServiceProvider - a Windows CryptoAPI key container - for");
            Console.WriteLine("  every one, then leak it. Verifiers are now cached by modulus.");
            Console.WriteLine();
            Console.WriteLine("  The construction cost turned out to be the smaller half: see the split");
            Console.WriteLine("  below. The cache removes a real cost and stops the handle leak, but the");
            Console.WriteLine("  bulk of the work is the verification itself.");

            // A realistic spread of distinct posters for the number of spots.
            const int distinctPosters = 500;
            var moduli = new List<string>(distinctPosters);
            var signatures = new List<byte[]>(distinctPosters);
            var hashes = new List<byte[]>(distinctPosters);

            using (var sha = new SHA1Managed())
            {
                for (int i = 0; i < distinctPosters; i++)
                {
                    using var signer = new RSACryptoServiceProvider(1024);
                    RSAParameters pub = signer.ExportParameters(false);
                    moduli.Add(Convert.ToBase64String(pub.Modulus));
                    byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes("spot subject " + i));
                    hashes.Add(hash);
                    signatures.Add(signer.SignHash(hash, null));
                }
            }

            var exponent = new byte[] { 1, 0, 1 };

            // The old shape: build a fresh provider for every spot, never dispose it.
            var uncached = Stopwatch.StartNew();
            for (int i = 0; i < verifications; i++)
            {
                int p = i % distinctPosters;
                var parameters = new RSAParameters { Exponent = exponent, Modulus = Convert.FromBase64String(moduli[p]) };
                var rsa = new RSACryptoServiceProvider();
                rsa.ImportParameters(parameters);
                rsa.VerifyHash(hashes[p], null, signatures[p]);
            }
            uncached.Stop();

            // What ships now: one provider per distinct modulus, reused.
            var cache = new Dictionary<string, RSACryptoServiceProvider>(StringComparer.Ordinal);
            var cached = Stopwatch.StartNew();
            for (int i = 0; i < verifications; i++)
            {
                int p = i % distinctPosters;
                string modulus = moduli[p];
                if (!cache.TryGetValue(modulus, out RSACryptoServiceProvider rsa))
                {
                    var parameters = new RSAParameters { Exponent = exponent, Modulus = Convert.FromBase64String(modulus) };
                    rsa = new RSACryptoServiceProvider();
                    rsa.ImportParameters(parameters);
                    cache[modulus] = rsa;
                }
                rsa.VerifyHash(hashes[p], null, signatures[p]);
            }
            cached.Stop();
            // Split the two costs apart, because which one dominates decides whether the
            // next optimization is caching or parallelism.
            var constructionOnly = Stopwatch.StartNew();
            for (int i = 0; i < verifications; i++)
            {
                int p = i % distinctPosters;
                var parameters = new RSAParameters { Exponent = exponent, Modulus = Convert.FromBase64String(moduli[p]) };
                var rsa = new RSACryptoServiceProvider();
                rsa.ImportParameters(parameters);
            }
            constructionOnly.Stop();

            var verifyOnly = Stopwatch.StartNew();
            for (int i = 0; i < verifications; i++)
            {
                int p = i % distinctPosters;
                cache[moduli[p]].VerifyHash(hashes[p], null, signatures[p]);
            }
            verifyOnly.Stop();

            Console.WriteLine();
            Row("new provider per spot", uncached.Elapsed, verifications, "the old behaviour");
            Row("cached by modulus", cached.Elapsed, verifications, "ships now");
            Console.WriteLine();
            Row("  of which: construction", constructionOnly.Elapsed, verifications, "removed by the cache");
            Row("  of which: VerifyHash", verifyOnly.Elapsed, verifications, "irreducible per spot");

            foreach (var rsa in cache.Values)
            {
                rsa.Dispose();
            }

            Console.WriteLine();
            Console.WriteLine($"  Speedup from caching: {Ratio(uncached.Elapsed, cached.Elapsed)}");
            Console.WriteLine($"  Key containers leaked by the old path: {verifications:N0} (never disposed)");
            Console.WriteLine();
            double verifyShare = verifyOnly.Elapsed.TotalMilliseconds / Math.Max(uncached.Elapsed.TotalMilliseconds, 0.0001) * 100;
            Console.WriteLine($"  VerifyHash is {verifyShare:N0}% of the old cost. Caching cannot touch that share;");
            Console.WriteLine("  only running verification on more than one thread can.");
        }

        // -------------------------------------------------------------------- output

        private static void Header(string title)
        {
            Console.WriteLine(new string('=', 66));
            Console.WriteLine("  " + title);
            Console.WriteLine(new string('=', 66));
        }

        private static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(title);
            Console.WriteLine(new string('-', title.Length));
        }

        private static void Row(string label, TimeSpan elapsed, int operations, string note)
        {
            double perSecond = operations / Math.Max(elapsed.TotalSeconds, 0.0001);
            Console.WriteLine($"  {label,-30} {elapsed.TotalMilliseconds,9:N0} ms  {perSecond,12:N0}/s   {note}");
        }

        private static string Ratio(TimeSpan before, TimeSpan after)
        {
            if (after.TotalMilliseconds <= 0)
            {
                return "n/a";
            }
            double factor = before.TotalMilliseconds / after.TotalMilliseconds;
            return factor >= 1
                ? $"{factor:N1}x faster"
                : $"{1 / factor:N1}x slower";
        }

        private static string Mb(long bytes)
        {
            return $"{bytes / 1024.0 / 1024.0:N1} MB";
        }

        private static string Scalar(SQLiteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            return Convert.ToString(cmd.ExecuteScalar());
        }

        private static List<string> Names(SQLiteConnection conn, string type)
        {
            var names = new List<string>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = ? ORDER BY name";
            var p = cmd.CreateParameter();
            p.Value = type;
            cmd.Parameters.Add(p);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }
            return names;
        }
    }
}
