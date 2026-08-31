using System;
using System.IO;
using System.Data.SQLite;
using Spotnet.DAL;
using Xunit;

namespace Spotnet.Tests
{
    /// <summary>
    /// Guards the journalling configuration that keeps the spots database from being
    /// corrupted by an interrupted bulk import. These assert the pragmas the DAL applies,
    /// against a real on-disk file, because the failure mode they protect against only
    /// exists on disk.
    /// </summary>
    public class DbDurabilityTests : IDisposable
    {
        private readonly string _dbFile;

        public DbDurabilityTests()
        {
            _dbFile = Path.Combine(Path.GetTempPath(), "spotnet_durability_" + Guid.NewGuid().ToString("N") + ".dbs");
        }

        public void Dispose()
        {
            SQLiteConnection.ClearAllPools();
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try
                {
                    if (File.Exists(_dbFile + suffix))
                    {
                        File.Delete(_dbFile + suffix);
                    }
                }
                catch (IOException)
                {
                    // A lingering handle on a temp file is not worth failing a test over.
                }
            }
        }

        private SQLiteConnection OpenWritable()
        {
            // Mirrors the connection string built by SQliteDb for a writable connection.
            var conn = new SQLiteConnection(
                "DataSource=" + _dbFile + ";Synchronous=Normal;Temp Store=Memory;Cache Size=72500;BusyTimeout=5000;Journal Mode=WAL;");
            conn.Open();
            return conn;
        }

        private static string Scalar(SQLiteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            return Convert.ToString(cmd.ExecuteScalar());
        }

        [Fact]
        public void WritableConnection_UsesWalJournal()
        {
            using var conn = OpenWritable();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE spots(rowid INTEGER PRIMARY KEY, subject TEXT)";
                cmd.ExecuteNonQuery();
            }

            Assert.Equal("wal", Scalar(conn, "PRAGMA journal_mode").ToLowerInvariant());
        }

        [Fact]
        public void ImportPragmas_LeaveDatabaseCrashSafe()
        {
            using var conn = OpenWritable();
            using (var cmd = conn.CreateCommand())
            {
                // The exact sequence SpotSaver.SetDbSettingsForInsertionImprove applies.
                cmd.CommandText = "PRAGMA journal_mode = WAL";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA synchronous = NORMAL";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA wal_autocheckpoint = 4000";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA cache_size = -65536";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA temp_store = MEMORY";
                cmd.ExecuteNonQuery();
            }

            Assert.Equal("wal", Scalar(conn, "PRAGMA journal_mode").ToLowerInvariant());
            // synchronous must not be 0 (OFF) - that is the setting that allowed the
            // database to be left torn when an import was interrupted.
            Assert.NotEqual("0", Scalar(conn, "PRAGMA synchronous"));
            Assert.Equal("4000", Scalar(conn, "PRAGMA wal_autocheckpoint"));
        }

        [Fact]
        public void ImportPragmas_AreAcceptedThroughTheDalStartupPath()
        {
            using var db = new SQliteDb(_dbFile);

            // This is the exact method called while Spotnet initializes the comments
            // database. Current System.Data.SQLite returns -1 (not 0) for successful
            // PRAGMA assignments, so the DAL must verify values rather than row counts.
            SpotSaver.SetDbSettingsForInsertionImprove(db);

            Assert.Equal(1L, db.ExecuteScalar("PRAGMA synchronous", null));
            Assert.Equal(4000L, db.ExecuteScalar("PRAGMA wal_autocheckpoint", null));
            Assert.Equal(-65536L, db.ExecuteScalar("PRAGMA cache_size", null));
            Assert.Equal(2L, db.ExecuteScalar("PRAGMA temp_store", null));
        }

        [Fact]
        public void PageSize_IsSettableBeforeWalAndStickyAfter()
        {
            // Documents the ordering constraint in CreateSpotsTablesOnEmptyDatabase:
            // page_size has to be applied before the database switches to WAL.
            using (var conn = new SQLiteConnection("DataSource=" + _dbFile + ";"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA page_size = 8192";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA journal_mode = WAL";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "CREATE TABLE spots(rowid INTEGER PRIMARY KEY, subject TEXT)";
                cmd.ExecuteNonQuery();
            }

            SQLiteConnection.ClearAllPools();

            using var reopened = OpenWritable();
            Assert.Equal("8192", Scalar(reopened, "PRAGMA page_size"));
            Assert.Equal("wal", Scalar(reopened, "PRAGMA journal_mode").ToLowerInvariant());
        }

        [Fact]
        public void FreshInstallerProfile_CanCreateItsFirstSpotsDatabase()
        {
            using var db = new SQliteDb(_dbFile);
            var method = typeof(SpotProvider).GetMethod("CreateSpotsTablesOnEmptyDatabase",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            // The schema method does not need UI/settings state from the constructor.
            var provider = (SpotProvider)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SpotProvider));
            method.Invoke(provider, new object[] { db });
            Assert.Equal(SpotsSchema.CurrentUserVersion, db.ExecuteScalar("PRAGMA user_version", null));
            Assert.Equal(SpotsSchema.SpotsPageSize, db.ExecuteScalar("PRAGMA page_size", null));
            Assert.Equal("wal", db.ExecuteCommand("PRAGMA journal_mode", null).Trim().ToLowerInvariant());
        }

        [Fact]
        public void FreshDatabaseInitializerRefusesExistingUserTables()
        {
            using var db = new SQliteDb(_dbFile);
            db.ExecuteNonQuery("CREATE TABLE personal(value TEXT)", null);
            db.ExecuteNonQuery("INSERT INTO personal VALUES('keep')", null);
            var method = typeof(SpotProvider).GetMethod("CreateSpotsTablesOnEmptyDatabase",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var provider = (SpotProvider)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SpotProvider));
            Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(provider, new object[] { db }));
            Assert.Equal("keep", db.ExecuteCommand("SELECT value FROM personal", null).Trim());
        }
    }
}
