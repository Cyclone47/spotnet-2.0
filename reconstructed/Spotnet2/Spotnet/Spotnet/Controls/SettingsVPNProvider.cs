using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;
using NLog;
using Spotnet.Helpers;
using Spotnet.Properties;

namespace Spotnet.Controls;
public partial class SettingsVPNProvider : UserControl, IAdvancedSettingsControl
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly Brush _fieldInvalidBackground = Brushes.LemonChiffon;
    private readonly Brush _fieldValidBackground = Brushes.White;
    public SettingsVPNProvider()
    {
        base.Initialized += OnInitialized;
        InitializeComponent();
    }

    public bool Save()
    {
        try
        {
            if (!VerifyFields())
            {
                return false;
            }

            string vPNProvider = Settings.Default.VPNProvider;
            Settings.Default.VPNProvider = (string)ProviderBox.SelectedItem;
            _ = Settings.Default.VPNProvider != vPNProvider;
            Settings.Default.Save();
            return true;
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
            return false;
        }
    }

    public bool VerifyFields()
    {
        return ProviderBox.SelectedIndex >= 0;
    }

    private void OnInitialized(object sender, EventArgs e)
    {
        PopulateVPNProviderComboBox();
        ProviderBox.SelectedIndex = ProviderBox.Items.IndexOf(Settings.Default.VPNProvider);
        ProviderBox.Background = ((ProviderBox.SelectedIndex >= 0) ? _fieldValidBackground : _fieldInvalidBackground);
        VPNNotInstalledText.Visibility = ((ProviderBox.Items.Count != 0) ? Visibility.Hidden : Visibility.Visible);
        spacer.Visibility = ((ProviderBox.Items.Count != 0) ? Visibility.Hidden : Visibility.Visible);
    }

    private void ProviderBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ProviderBox.Background = ((ProviderBox.SelectedIndex >= 0) ? _fieldValidBackground : _fieldInvalidBackground);
    }

    private void PopulateVPNProviderComboBox()
    {
        string[] obj = new string[2]
        {
            "VPNNederland",
            "5EuroVPN"
        };
        string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        ProviderBox.Items.Clear();
        string[] array = obj;
        foreach (string text in array)
        {
            if (File.Exists(folderPath + Path.DirectorySeparatorChar + text + "Core" + Path.DirectorySeparatorChar + text + "Service.exe"))
            {
                ProviderBox.Items.Add(text);
            }
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(e.Uri.ToString());
    }
}