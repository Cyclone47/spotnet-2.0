using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Data.SQLite;
using MahApps.Metro.Controls;
using NLog;
using Spotnet.Helpers;
using Spotnet.Properties;

namespace Spotnet.Views
{
    public enum DbRecoveryAction
    {
        Repaired,
        Cleaned,
        Closed
    }

    public partial class DbRecoveryWindow : MetroWindow
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public DbRecoveryAction SelectedAction { get; private set; } = DbRecoveryAction.Closed;

        public DbRecoveryWindow(string reason = "")
        {
            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                ReasonTextBlock.Text = reason;
            }
        }

        public static DbRecoveryAction Prompt(Window owner = null, string reason = "")
        {
            App.CloseSplash();
            DbRecoveryWindow dlg = new DbRecoveryWindow(reason);
            if (owner != null) dlg.Owner = owner;
            dlg.ShowDialog();
            return dlg.SelectedAction;
        }

        private async void RepairButton_Click(object sender, RoutedEventArgs e)
        {
            SetWorkingState("Running quick repair and checkpointing SQLite databases...");

            bool success = await Task.Run(() => PerformQuickRepair());
            if (success)
            {
                SelectedAction = DbRecoveryAction.Repaired;
                DialogResult = true;
                Close();
            }
            else
            {
                StatusTextBlock.Text = "Quick repair encountered an issue. You can try 'Clean Reset' instead.";
                ProgressBarControl.Visibility = Visibility.Collapsed;
                EnableButtons(true);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            SetWorkingState("Backing up databases and resetting...");

            try
            {
                string programData = AppHelper.SettingsFolder;
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // Backup .dbs and .dbc
                foreach (var file in Directory.GetFiles(programData, "*.db*"))
                {
                    if (file.EndsWith(".dbs") || file.EndsWith(".dbc"))
                    {
                        string backupName = $"{file}.{timestamp}.bak";
                        try
                        {
                            File.Move(file, backupName);
                            Log.Info("Backed up {0} to {1}", file, backupName);
                        }
                        catch (Exception ex)
                        {
                            Log.Warn("Could not backup file: {0} ({1})", file, ex.Message);
                        }
                    }
                }

                ClearConfigurationFlags();
                SelectedAction = DbRecoveryAction.Cleaned;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                StatusTextBlock.Text = "Reset failed: " + ex.Message;
                ProgressBarControl.Visibility = Visibility.Collapsed;
                EnableButtons(true);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = DbRecoveryAction.Closed;
            DialogResult = false;
            Close();
        }

        private bool PerformQuickRepair()
        {
            try
            {
                string programData = AppHelper.SettingsFolder;

                // 1. Checkpoint and unlock all .dbs and .dbc SQLite files
                string[] dbFiles = Directory.GetFiles(programData, "*.db*");
                foreach (var db in dbFiles)
                {
                    if (db.EndsWith(".dbs") || db.EndsWith(".dbc"))
                    {
                        try
                        {
                            using var conn = new SQLiteConnection($"Data Source={db};Version=3;Journal Mode=Delete;");
                            conn.Open();
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                            cmd.ExecuteNonQuery();

                            cmd.CommandText = "REINDEX;";
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            Log.Warn("Error checkpointing db {0}: {1}", db, ex.Message);
                        }
                    }
                }

                // 2. Synchronize watermarks in userinfo
                string[] dbsFiles = Directory.GetFiles(programData, "*.dbs");
                foreach (var dbs in dbsFiles)
                {
                    try
                    {
                        using var conn = new SQLiteConnection($"Data Source={dbs};Version=3;");
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "CREATE TABLE IF NOT EXISTS userinfo(field TEXT PRIMARY KEY, value TEXT);";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = "SELECT MIN(rowid), MAX(rowid) FROM spots;";
                        using var reader = cmd.ExecuteReader();
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            long minId = reader.GetInt64(0);
                            reader.Close();

                            cmd.CommandText = "INSERT OR REPLACE INTO userinfo(field, value) VALUES('minId_headers', @val);";
                            cmd.Parameters.AddWithValue("@val", minId.ToString());
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Error aligning watermarks in {0}: {1}", dbs, ex.Message);
                    }
                }

                // 3. Clear auto-wipe malformed flags in user settings
                ClearConfigurationFlags();

                return true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                return false;
            }
        }

        private void ClearConfigurationFlags()
        {
            Settings.Default.SpotsDbFileMalformed = false;
            Settings.Default.CommentsDbFileMalformed = false;
            Settings.Default.RecreateDbScheduled = false;
            Settings.Default.Save();

            // Also clean user.config on disk
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string spotnetConfigDir = Path.Combine(localAppData, "Spotnet");
                if (Directory.Exists(spotnetConfigDir))
                {
                    foreach (var configFile in Directory.GetFiles(spotnetConfigDir, "user.config", SearchOption.AllDirectories))
                    {
                        string content = File.ReadAllText(configFile);
                        bool modified = false;
                        if (content.Contains("SpotsDbFileMalformed"))
                        {
                            content = Regex.Replace(content, @"(<setting name=""SpotsDbFileMalformed""[^>]*>\s*<value>)[^<]*(</value>)", "${1}False${2}");
                            modified = true;
                        }
                        if (content.Contains("CommentsDbFileMalformed"))
                        {
                            content = Regex.Replace(content, @"(<setting name=""CommentsDbFileMalformed""[^>]*>\s*<value>)[^<]*(</value>)", "${1}False${2}");
                            modified = true;
                        }
                        if (content.Contains("RecreateDbScheduled"))
                        {
                            content = Regex.Replace(content, @"(<setting name=""RecreateDbScheduled""[^>]*>\s*<value>)[^<]*(</value>)", "${1}False${2}");
                            modified = true;
                        }
                        if (modified) File.WriteAllText(configFile, content);
                    }
                }
            }
            catch { }
        }

        private void SetWorkingState(string status)
        {
            StatusTextBlock.Text = status;
            ProgressBarControl.Visibility = Visibility.Visible;
            EnableButtons(false);
        }

        private void EnableButtons(bool enable)
        {
            RepairButton.IsEnabled = enable;
            ResetButton.IsEnabled = enable;
            CloseButton.IsEnabled = enable;
        }
    }
}
