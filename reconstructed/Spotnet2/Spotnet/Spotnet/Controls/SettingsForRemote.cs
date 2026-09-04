using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Spotnet.Remote;

namespace Spotnet.Controls;

public partial class SettingsForRemote : UserControl, IAdvancedSettingsControl
{
    private RemoteConfig _config;

    public SettingsForRemote()
    {
        InitializeComponent();
        LoadConfig();
    }

    private void LoadConfig()
    {
        _config = RemoteConfig.Load();
        EnableRemoteCheckBox.IsChecked = _config.Enabled;
        PortTextBox.Text = _config.Port.ToString();
        AllowLanCheckBox.IsChecked = _config.AllowLan;
        RequireAuthCheckBox.IsChecked = _config.RequireAuth;
        KeepAwakeCheckBox.IsChecked = _config.KeepAwake;

        AuthUsernameTextBox.Text = string.IsNullOrWhiteSpace(_config.AuthUsername) ? "admin" : _config.AuthUsername;
        AuthPasswordBox.Password = "";
        AuthPasswordVisibleTextBox.Text = "";
        ShowPasswordCheckBox.IsChecked = false;

        UpdateUiState();
        UpdateAuthUiState();
        UpdatePasswordHint();
        RefreshDevicesList();
    }

    private void UpdateUiState()
    {
        bool isEnabled = EnableRemoteCheckBox.IsChecked == true;
        RemoteConfigPanel.IsEnabled = isEnabled;

        if (isEnabled && RemoteServer.Instance.IsRunning)
        {
            ServerStatusTextBlock.Text = "● Actief";
            ServerStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            string url = RemoteServer.Instance.GetRemoteUrl(_config.AllowLan);
            ServerUrlTextBlock.Text = url;
            OpenBrowserButton.IsEnabled = true;
            PairDeviceButton.IsEnabled = true;
        }
        else if (isEnabled)
        {
            ServerStatusTextBlock.Text = "● Starten mislukt (poort mogelijk bezet)";
            ServerStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            ServerUrlTextBlock.Text = $"http://{RemoteServer.GetLocalIpAddress()}:{_config.Port}";
            OpenBrowserButton.IsEnabled = false;
            PairDeviceButton.IsEnabled = false;
        }
        else
        {
            ServerStatusTextBlock.Text = "Uitgeschakeld";
            ServerStatusTextBlock.Foreground = Brushes.Gray;
            ServerUrlTextBlock.Text = "-";
            OpenBrowserButton.IsEnabled = false;
            PairDeviceButton.IsEnabled = false;
        }
    }

    private void UpdateAuthUiState()
    {
        bool authRequired = RequireAuthCheckBox.IsChecked == true;
        AuthCredentialsPanel.Visibility = authRequired ? Visibility.Visible : Visibility.Collapsed;
        NoAuthWarningTextBlock.Visibility = authRequired ? Visibility.Collapsed : Visibility.Visible;
    }

    private string GetEnteredPassword()
    {
        if (ShowPasswordCheckBox.IsChecked == true)
        {
            return AuthPasswordVisibleTextBox.Text;
        }
        return AuthPasswordBox.Password;
    }

    private void FocusPasswordBox()
    {
        if (ShowPasswordCheckBox.IsChecked == true)
        {
            AuthPasswordVisibleTextBox.Focus();
        }
        else
        {
            AuthPasswordBox.Focus();
        }
    }

    private void UpdatePasswordHint()
    {
        string pwd = GetEnteredPassword();
        if (string.IsNullOrEmpty(pwd))
        {
            if (!string.IsNullOrEmpty(_config.PasswordHash))
            {
                AuthPasswordHintTextBlock.Text = "✓ Wachtwoord ingesteld (laat leeg om te behouden)";
                AuthPasswordHintTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            else
            {
                AuthPasswordHintTextBlock.Text = "⚠️ Voer een nieuw wachtwoord in (minimaal 6 tekens)";
                AuthPasswordHintTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }
        }
        else if (pwd.Length < 6)
        {
            AuthPasswordHintTextBlock.Text = $"⚠️ Te kort ({pwd.Length}/6 tekens - minimaal 6 tekens vereist)";
            AuthPasswordHintTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
        else
        {
            AuthPasswordHintTextBlock.Text = "✓ Wachtwoord voldoet aan minimale lengte";
            AuthPasswordHintTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
    }

    private void RefreshDevicesList()
    {
        _config = RemoteConfig.Load();
        PairedDevicesListBox.ItemsSource = null;
        PairedDevicesListBox.ItemsSource = _config.PairedDevices.OrderByDescending(d => d.LastSeenAt).ToList();
    }

    private void EnableRemoteCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (EnableRemoteCheckBox.IsChecked == true)
        {
            if (!VerifyFields())
            {
                EnableRemoteCheckBox.IsChecked = false;
                return;
            }
        }
        // Save and start/stop server immediately without waiting for OK
        Save();
    }

    private void AllowLanCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (EnableRemoteCheckBox.IsChecked == true)
        {
            Save();
        }
    }

    private void RequireAuthCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateAuthUiState();
        if (RequireAuthCheckBox.IsChecked == true)
        {
            // If turning on auth and no password configured yet, focus password box
            if (string.IsNullOrEmpty(_config.PasswordHash) && string.IsNullOrEmpty(GetEnteredPassword()))
            {
                FocusPasswordBox();
                return;
            }
        }

        if (VerifyFields())
        {
            Save();
        }
    }

    private void KeepAwakeCheckBox_Click(object sender, RoutedEventArgs e)
    {
        Save();
    }

    private void PortTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (EnableRemoteCheckBox.IsChecked == true && VerifyFields())
        {
            Save();
        }
    }

    private void AuthUsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (RequireAuthCheckBox.IsChecked == true && VerifyFields())
        {
            Save();
        }
    }

    private void AuthPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ShowPasswordCheckBox.IsChecked != true)
        {
            UpdatePasswordHint();
        }
    }

    private void AuthPasswordVisibleTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ShowPasswordCheckBox.IsChecked == true)
        {
            UpdatePasswordHint();
        }
    }

    private void ShowPasswordCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (ShowPasswordCheckBox.IsChecked == true)
        {
            AuthPasswordVisibleTextBox.Text = AuthPasswordBox.Password;
            AuthPasswordVisibleTextBox.Visibility = Visibility.Visible;
            AuthPasswordBox.Visibility = Visibility.Collapsed;
            AuthPasswordVisibleTextBox.Focus();
        }
        else
        {
            AuthPasswordBox.Password = AuthPasswordVisibleTextBox.Text;
            AuthPasswordBox.Visibility = Visibility.Visible;
            AuthPasswordVisibleTextBox.Visibility = Visibility.Collapsed;
            AuthPasswordBox.Focus();
        }
        UpdatePasswordHint();
    }

    private void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string url = RemoteServer.Instance.GetRemoteUrl(_config.AllowLan);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Kon browser niet openen: " + ex.Message, "Fout", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PairDeviceButton_Click(object sender, RoutedEventArgs e)
    {
        // Save first so port & settings are up to date and server is active
        if (!VerifyFields()) return;
        Save();

        var pairingWindow = new RemotePairingWindow();
        pairingWindow.Owner = Window.GetWindow(this);
        pairingWindow.ShowDialog();

        RefreshDevicesList();
    }

    private void RevokeDevice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string deviceId)
        {
            if (MessageBox.Show("Wil je de toegang voor dit apparaat intrekken?", "Apparaat ontkoppelen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                RemoteAuthManager.Instance.RevokeDevice(deviceId);
                RefreshDevicesList();
            }
        }
    }

    private void RevokeAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Weet je zeker dat je ALLE mobiele apparaten wilt ontkoppelen?", "Alle apparaten intrekken", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            RemoteAuthManager.Instance.RevokeAllDevices();
            RefreshDevicesList();
        }
    }

    public bool VerifyFields()
    {
        if (EnableRemoteCheckBox.IsChecked == true)
        {
            if (!int.TryParse(PortTextBox.Text.Trim(), out int port) || port < 1024 || port > 65535)
            {
                MessageBox.Show("Voer een geldig poortnummer in tussen 1024 en 65535.", "Ongeldige poort", MessageBoxButton.OK, MessageBoxImage.Warning);
                PortTextBox.Focus();
                return false;
            }

            if (RequireAuthCheckBox.IsChecked == true)
            {
                string username = AuthUsernameTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(username))
                {
                    MessageBox.Show("Voer een gebruikersnaam in voor authenticatie.", "Gebruikersnaam vereist", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AuthUsernameTextBox.Focus();
                    return false;
                }

                string enteredPassword = GetEnteredPassword();
                if (string.IsNullOrEmpty(_config.PasswordHash) && string.IsNullOrEmpty(enteredPassword))
                {
                    MessageBox.Show("Voer een wachtwoord in (minimaal 6 tekens) om authenticatie in te schakelen.", "Wachtwoord vereist", MessageBoxButton.OK, MessageBoxImage.Warning);
                    FocusPasswordBox();
                    return false;
                }

                if (!string.IsNullOrEmpty(enteredPassword) && enteredPassword.Length < 6)
                {
                    MessageBox.Show("Het wachtwoord moet minimaal 6 tekens lang zijn om veilig te zijn tegen kraken.", "Wachtwoord te kort", MessageBoxButton.OK, MessageBoxImage.Warning);
                    FocusPasswordBox();
                    return false;
                }
            }
        }
        return true;
    }

    public bool Save()
    {
        if (!VerifyFields())
        {
            return false;
        }

        bool wasEnabled = _config.Enabled;
        int oldPort = _config.Port;
        bool oldLan = _config.AllowLan;

        _config.Enabled = EnableRemoteCheckBox.IsChecked == true;
        if (int.TryParse(PortTextBox.Text.Trim(), out int port))
        {
            _config.Port = port;
        }
        _config.AllowLan = AllowLanCheckBox.IsChecked == true;
        _config.RequireAuth = RequireAuthCheckBox.IsChecked == true;
        _config.KeepAwake = KeepAwakeCheckBox.IsChecked == true;
        _config.AuthUsername = string.IsNullOrWhiteSpace(AuthUsernameTextBox.Text) ? "admin" : AuthUsernameTextBox.Text.Trim();

        string enteredPassword = GetEnteredPassword();
        if (!string.IsNullOrEmpty(enteredPassword))
        {
            _config.SetPassword(enteredPassword);
            AuthPasswordBox.Password = "";
            AuthPasswordVisibleTextBox.Text = "";
        }

        _config.Save();
        RemoteAuthManager.Instance.ReloadConfig();

        UpdatePasswordHint();

        // Start, stop or restart server if settings changed
        if (_config.Enabled)
        {
            if (!wasEnabled || oldPort != _config.Port || oldLan != _config.AllowLan)
            {
                RemoteServer.Instance.Restart();
            }
            else if (!RemoteServer.Instance.IsRunning)
            {
                RemoteServer.Instance.Start();
            }
            else
            {
                // Server was already running, just update sleep preventer
                SleepPreventer.UpdateState(_config.KeepAwake);
            }
        }
        else if (wasEnabled || RemoteServer.Instance.IsRunning)
        {
            RemoteServer.Instance.Stop();
        }
        else
        {
            SleepPreventer.UpdateState(false);
        }

        UpdateUiState();
        return true;
    }
}
