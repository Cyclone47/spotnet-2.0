using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NLog;
using Spotnet.Community;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;

namespace Spotnet.Controls;

/// <summary>
/// Settings pane for everything that binds this client to a particular Spotnet community.
/// Edits are made against a working copy and only reach <see cref="CommunityConfig.Current"/>
/// when the settings window is saved, so Cancel really cancels.
/// </summary>
public partial class SettingsForCommunity : UserControl, IAdvancedSettingsControl
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>Shown in place of the API key until the user chooses to replace it.</summary>
    private const string MaskedKeyPlaceholder = "••••••••";

    private CommunityConfig _working;

    /// <summary>
    /// The key as loaded. While it is non-null the box is showing a mask, and the stored
    /// key is kept rather than whatever the mask happens to read as.
    /// </summary>
    private string _originalApiKey;

    public SettingsForCommunity()
    {
        base.Initialized += OnInitialized;
        InitializeComponent();
    }

    private void OnInitialized(object sender, EventArgs e)
    {
        _working = CloneCurrent();
        LoadToUi(_working);
        UpdateListStatus();
    }

    private static CommunityConfig CloneCurrent()
    {
        try
        {
            return CommunityConfig.Deserialize(CommunityConfig.Current.Serialize()) ?? new CommunityConfig();
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
            return new CommunityConfig();
        }
    }

    private void LoadToUi(CommunityConfig c)
    {
        CommunityNameTextBlock.Text = c.Name.IsNullOrWhiteSpace() ? "(naamloos)" : c.Name;

        SpotsGroupTextBox.Text = c.Newsgroups.Spots;
        CommentsGroupTextBox.Text = c.Newsgroups.Comments;
        ReportsGroupTextBox.Text = c.Newsgroups.Reports;
        NzbGroupTextBox.Text = c.Newsgroups.Nzb;

        ModerationEnabledCheckBox.IsChecked = c.Moderation.Enabled;
        IntervalTextBox.Text = c.Moderation.UpdateIntervalMinutes.ToString();

        WhitelistUrlTextBox.Text = c.Moderation.WhitelistUrl;
        BlacklistUrlTextBox.Text = c.Moderation.BlacklistUrl;
        SpotWhitelistUrlTextBox.Text = c.Moderation.SpotWhitelistUrl;
        SpotBlacklistUrlTextBox.Text = c.Moderation.SpotBlacklistUrl;
        ModeratorKeysUrlTextBox.Text = c.Moderation.ModeratorKeysUrl;
        RequireSignedListsCheckBox.IsChecked = c.Moderation.RequireSignedLists;
        SignatureKeyTextBox.Text = c.Moderation.SignaturePublicKeyXml;

        ResponseSiteUrlTextBox.Text = c.Services.ResponseSiteUrl;
        LogUploadUrlTextBox.Text = c.Services.LogUploadUrl;
        UpgradeFailuresUrlTextBox.Text = c.Services.UpgradeFailuresUrl;
        PromoFolderUrlTextBox.Text = c.Services.PromoFolderUrl;

        NewznabUrlTextBox.Text = c.Indexer.NewznabBaseUrl;
        ShowKeyMasked(c.Indexer.NewznabApiKey);

        ApplyModerationEnabledState();
    }

    private void ShowKeyMasked(string key)
    {
        _originalApiKey = key ?? "";
        NewznabKeyTextBox.IsReadOnly = true;
        ReplaceKeyButton.IsEnabled = true;
        NewznabKeyTextBox.Text = _originalApiKey.Length <= 4
            ? (_originalApiKey.Length == 0 ? "(niet ingesteld)" : MaskedKeyPlaceholder)
            : MaskedKeyPlaceholder + _originalApiKey.Substring(_originalApiKey.Length - 4);
    }

    /// <summary>Reads the pane back into <paramref name="c"/>.</summary>
    private void CollectFromUi(CommunityConfig c)
    {
        c.Newsgroups.Spots = SpotsGroupTextBox.Text.Trim();
        c.Newsgroups.Comments = CommentsGroupTextBox.Text.Trim();
        c.Newsgroups.Reports = ReportsGroupTextBox.Text.Trim();
        c.Newsgroups.Nzb = NzbGroupTextBox.Text.Trim();

        c.Moderation.Enabled = ModerationEnabledCheckBox.IsChecked.GetValueOrDefault();
        c.Moderation.UpdateIntervalMinutes = ParseInterval(IntervalTextBox.Text, c.Moderation.UpdateIntervalMinutes);

        c.Moderation.WhitelistUrl = WhitelistUrlTextBox.Text.Trim();
        c.Moderation.BlacklistUrl = BlacklistUrlTextBox.Text.Trim();
        c.Moderation.SpotWhitelistUrl = SpotWhitelistUrlTextBox.Text.Trim();
        c.Moderation.SpotBlacklistUrl = SpotBlacklistUrlTextBox.Text.Trim();
        c.Moderation.ModeratorKeysUrl = ModeratorKeysUrlTextBox.Text.Trim();
        c.Moderation.RequireSignedLists = RequireSignedListsCheckBox.IsChecked.GetValueOrDefault();
        c.Moderation.SignaturePublicKeyXml = SignatureKeyTextBox.Text.Trim();

        c.Services.ResponseSiteUrl = ResponseSiteUrlTextBox.Text.Trim();
        c.Services.LogUploadUrl = LogUploadUrlTextBox.Text.Trim();
        c.Services.UpgradeFailuresUrl = UpgradeFailuresUrlTextBox.Text.Trim();
        c.Services.PromoFolderUrl = PromoFolderUrlTextBox.Text.Trim();

        c.Indexer.NewznabBaseUrl = NewznabUrlTextBox.Text.Trim();
        // A read-only box is still showing the mask, so the stored key stands.
        c.Indexer.NewznabApiKey = NewznabKeyTextBox.IsReadOnly
            ? _originalApiKey
            : NewznabKeyTextBox.Text.Trim();
    }

    private static int ParseInterval(string text, int fallback)
    {
        return int.TryParse(text?.Trim(), out int minutes) ? minutes : fallback;
    }

    public bool VerifyFields()
    {
        try
        {
            CollectFromUi(_working);
            IList<string> errors = _working.Validate();
            if (!int.TryParse(IntervalTextBox.Text?.Trim(), out int _))
            {
                errors.Add("Het bijwerkinterval moet een heel getal zijn.");
            }

            ValidationTextBlock.Text = string.Join(Environment.NewLine, errors);
            return errors.Count == 0;
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
            return false;
        }
    }

    public bool Save()
    {
        try
        {
            if (!VerifyFields())
            {
                return false;
            }

            bool moderationWasEnabled = CommunityConfig.Current.Moderation.Enabled;

            if (!_working.Save())
            {
                return false;
            }

            CommunityConfig.Replace(_working);
            _working.ApplyNewsgroupsToSettings();
            BlackAndWhite.RescheduleExternalListUpdates();

            // Turning moderation back on should not wait for the next tick.
            if (_working.Moderation.Enabled && !moderationWasEnabled)
            {
                BlackAndWhite.UpdateExternalListsAsync();
            }

            // Editing is done against a copy, so hand the pane a fresh one to work on.
            _working = CloneCurrent();
            UpdateListStatus();
            return true;
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
            return false;
        }
    }

    private void ApplyModerationEnabledState()
    {
        bool on = ModerationEnabledCheckBox.IsChecked.GetValueOrDefault();
        IntervalTextBox.IsEnabled = on;
        RefreshListsButton.IsEnabled = on;
    }

    private void ModerationEnabledCheckBox_Click(object sender, RoutedEventArgs e)
    {
        ApplyModerationEnabledState();
    }

    private void ReplaceKeyButton_Click(object sender, RoutedEventArgs e)
    {
        NewznabKeyTextBox.IsReadOnly = false;
        NewznabKeyTextBox.Text = "";
        NewznabKeyTextBox.Focus();
        ReplaceKeyButton.IsEnabled = false;
    }

    private void RefreshListsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BlackAndWhite.UpdateExternalListsAsync();
            ListStatusTextBlock.Text = "Bijwerken gestart…";
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
        }
    }

    /// <summary>Reports what the client currently holds, so the pane says something true even offline.</summary>
    private void UpdateListStatus()
    {
        try
        {
            if (!CommunityConfig.Current.Moderation.Enabled)
            {
                ListStatusTextBlock.Text = "Moderatielijsten staan uit.";
                return;
            }

            string newest = NewestListTimestamp();
            ListStatusTextBlock.Text = string.Format(
                "{0} vertrouwd, {1} geblokkeerd{2}",
                BlackAndWhite.WhiteList().Count,
                BlackAndWhite.BlackList().Count,
                newest == null ? "" : " · laatst bijgewerkt " + newest);
        }
        catch (Exception ex)
        {
            Log.Debug("Kon lijststatus niet bepalen: {0}", ex.Message);
            ListStatusTextBlock.Text = "";
        }
    }

    private static string NewestListTimestamp()
    {
        string[] files =
        {
            "whitelist.srv.csv", "blacklist.srv.csv", "spot_whitelist.srv.csv", "spot_blacklist.srv.csv"
        };

        DateTime? newest = null;
        foreach (string name in files)
        {
            string path = Path.Combine(AppHelper.SettingsFolder, name);
            if (!File.Exists(path))
            {
                continue;
            }

            DateTime stamp = File.GetLastWriteTime(path);
            if (newest == null || stamp > newest.Value)
            {
                newest = stamp;
            }
        }

        return newest?.ToString("dd-MM-yyyy HH:mm");
    }

    private void ToggleRawButton_Click(object sender, RoutedEventArgs e)
    {
        if (RawPanel.Visibility == Visibility.Visible)
        {
            RawPanel.Visibility = Visibility.Collapsed;
            ToggleRawButton.Content = "Toon raw";
            return;
        }

        CollectFromUi(_working);
        RawJsonTextBox.Text = _working.Serialize();
        RawStatusTextBlock.Text = "";
        RawPanel.Visibility = Visibility.Visible;
        ToggleRawButton.Content = "Verberg raw";
    }

    private void ApplyRawButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CommunityConfig parsed = CommunityConfig.Deserialize(RawJsonTextBox.Text);
            if (parsed == null)
            {
                RawStatusTextBlock.Text = "Leeg of onleesbaar.";
                return;
            }

            IList<string> errors = parsed.Validate();
            if (errors.Count > 0)
            {
                RawStatusTextBlock.Text = string.Join(Environment.NewLine, errors);
                return;
            }

            _working = parsed;
            LoadToUi(_working);
            RawStatusTextBlock.Text = "Overgenomen. Klik op Opslaan om het vast te leggen.";
            ValidationTextBlock.Text = "";
        }
        catch (Exception ex)
        {
            RawStatusTextBlock.Text = "Ongeldige JSON: " + ex.Message;
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Alle community-instellingen terugzetten naar de standaardwaarden van deze Spotnet?",
                "Herstel standaardwaarden", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _working = new CommunityConfig();
        LoadToUi(_working);
        ValidationTextBlock.Text = "";
        if (RawPanel.Visibility == Visibility.Visible)
        {
            RawJsonTextBox.Text = _working.Serialize();
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CollectFromUi(_working);
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Community-profiel exporteren",
                Filter = "Spotnet community-profiel (*.json)|*.json|Alle bestanden (*.*)|*.*",
                FileName = SuggestProfileFileName(_working.Name),
                AddExtension = true,
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, _working.Serialize());
            ValidationTextBlock.Text = "";
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
        }
    }

    private static string SuggestProfileFileName(string name)
    {
        string cleaned = new string((name ?? "community")
            .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-').ToArray()).Trim('-');
        return (cleaned.IsNullOrWhiteSpace() ? "community" : cleaned.ToLowerInvariant()) + "-profiel.json";
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Community-profiel importeren",
                Filter = "Spotnet community-profiel (*.json)|*.json|Alle bestanden (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            CommunityConfig imported = CommunityConfig.Deserialize(File.ReadAllText(dialog.FileName));
            if (imported == null)
            {
                ValidationTextBlock.Text = "Dat bestand bevat geen community-profiel.";
                return;
            }

            IList<string> errors = imported.Validate();
            if (errors.Count > 0)
            {
                ValidationTextBlock.Text = "Het profiel is niet geldig:" + Environment.NewLine +
                                           string.Join(Environment.NewLine, errors);
                return;
            }

            _working = imported;
            LoadToUi(_working);
            ValidationTextBlock.Text = "Profiel geladen. Klik op Opslaan om het in gebruik te nemen.";
            if (RawPanel.Visibility == Visibility.Visible)
            {
                RawJsonTextBox.Text = _working.Serialize();
            }
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
            ValidationTextBlock.Text = "Importeren mislukt: " + ex.Message;
        }
    }
}
