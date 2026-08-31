using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using GalaSoft.MvvmLight.Threading;
using NLog;
using Spotnet.Extensions;
using Spotnet.Model;
using Spotnet.Properties;

namespace Spotnet.Controls;
public partial class VPNInstallStatusWarning : UserControl
{
    private delegate void SafeCallDelegate();
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public static readonly DependencyProperty StatusWarningIsVisibleProperty = DependencyProperty.Register("StatusWarningIsVisible", typeof(bool), typeof(VPNInstallStatusWarning), new FrameworkPropertyMetadata(false));
    private readonly string _downloadToFile = System.IO.Path.GetTempPath() + System.IO.Path.DirectorySeparatorChar + "VPNNederland-Setup.exe";
    private bool _hiddingScheduled;
    private bool _isPermanent;
    public bool StatusWarningIsVisible
    {
        get
        {
            return (bool)GetValue(StatusWarningIsVisibleProperty);
        }

        set
        {
            if (value)
            {
                _hiddingScheduled = false;
                SafeSwitchWarningOnVPNInstalled();
            }

            SetValue(StatusWarningIsVisibleProperty, value);
        }
    }

    public string InstallStatusText
    {
        get
        {
            if (IsDownloading)
            {
                return string.Format(Words.VPNInstallerDownloading, Sys.MainWindow.VPNInstallStatusWarning.InstallerDownloadPercent);
            }

            if (InstallerDownloadPercent == 100)
            {
                return Words.VPNInstallerCompleted;
            }

            if (VPNStatusChecker.IsVPNNederlandInstalled())
            {
                return Words.NotInstalledVPNWarning;
            }

            return Words.VPNInstallerNotDownloading;
        }
    }

    public int InstallerDownloadPercent { get; private set; }
    public bool IsDownloading { get; private set; }

    public bool IsPermanent
    {
        get
        {
            return _isPermanent;
        }

        set
        {
            _isPermanent = value;
            DispatcherHelper.CheckBeginInvokeOnUI(delegate
            {
                StatusWarningIsVisible = value;
            });
        }
    }

    public static event Action StateChanged;
    public VPNInstallStatusWarning()
    {
        InitializeComponent();
        ResetUI();
        VPNStatusChecker.OnVPNInstalled += delegate (bool s)
        {
            SafeSwitchWarningOnVPNInstalled();
            if (s)
            {
                DispatcherHelper.CheckBeginInvokeOnUI(delegate
                {
                    DownloadStatusMsg.Visibility = Visibility.Collapsed;
                    WarningText.Visibility = Visibility.Visible;
                });
            }
        };
    }

    private void ResetUI()
    {
        DownloadStatusMsg.Visibility = Visibility.Collapsed;
        WarningText.Visibility = Visibility.Visible;
        InstallVPNClient.IsEnabled = true;
        InstallerDownloadPercent = 0;
        IsDownloading = false;
    }

    private void StartHidding()
    {
        _hiddingScheduled = false;
        DispatcherHelper.CheckBeginInvokeOnUI(delegate
        {
            ((Storyboard)FindResource("FadeOut")).Begin();
        });
    }

    private void OnFadeOutCompleted(object sender, EventArgs e)
    {
        ((Popup)base.Parent).IsOpen = false;
        if (InstallerDownloadPercent == 100)
        {
            ResetUI();
        }
    }

    public void ScheduleHide(TimeSpan timeout = default(TimeSpan))
    {
        if (IsPermanent)
        {
            return;
        }

        if (timeout == default(TimeSpan))
        {
            timeout = TimeSpan.FromSeconds(1.0);
        }

        _hiddingScheduled = true;
        EventExtension.RunAfter(delegate
        {
            if (_hiddingScheduled)
            {
                StartHidding();
            }
        }, timeout);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(e.Uri.ToString());
    }

    private void CloseIcon_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_hiddingScheduled && !IsPermanent)
        {
            ScheduleHide(TimeSpan.FromMilliseconds(300.0));
        }
    }

    private void LayoutRoot_OnMouseLeave(object sender, MouseEventArgs e)
    {
    }

    private void InstallVPNClient_Click(object sender, RoutedEventArgs e)
    {
        IsDownloading = false;
        InstallerDownloadPercent = 0;
        InstallVPNClient.IsEnabled = false;
        Log.Log(LogLevel.Info, "Starting download of VPNNederland-Setup.exe...");
        DownloadVPNNederlandAsync();
    }

    private void SafeSwitchWarningOnVPNInstalled()
    {
        SafeCallDelegate method = SwitchWarningOnVPNInstalled;
        base.Dispatcher.Invoke(method);
    }

    private void SwitchWarningOnVPNInstalled()
    {
        Storyboard storyboard = (Storyboard)FindResource("fadeOutControl");
        Storyboard storyboard2 = (Storyboard)FindResource("fadeInControl");
        bool flag = VPNStatusChecker.IsVPNNederlandInstalled();
        if (flag && DockStartButton.Visibility != 0)
        {
            storyboard.Begin(DockInstallButton);
            storyboard2.Begin(DockStartButton);
        }
        else if (!flag && DockInstallButton.Visibility != 0)
        {
            storyboard.Begin(DockStartButton);
            storyboard2.Begin(DockInstallButton);
        }
    }

    private void DownloadVPNNederlandAsync()
    {
        try
        {
            using WebClient webClient = new WebClient();
            string uriString = "https://update.vpnnederland.nl/windows/VPNNederland-Setup.exe";
            webClient.Credentials = CredentialCache.DefaultNetworkCredentials;
            webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
            webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
            IsDownloading = true;
            webClient.DownloadFileTaskAsync(new Uri(uriString), _downloadToFile);
        }
        catch (Exception)
        {
            ResetUI();
            Log.Log(LogLevel.Error, "Failed to download VPNNederland-Setup.exe file");
        }
    }

    private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        InstallerDownloadPercent = e.ProgressPercentage;
        VPNInstallStatusWarning.StateChanged?.Invoke();
        if (DownloadStatusMsg.Visibility == Visibility.Collapsed)
        {
            DownloadStatusMsg.Visibility = Visibility.Visible;
            WarningText.Visibility = Visibility.Collapsed;
            InstallVPNClient.IsEnabled = false;
        }

        DownloadStatusMsg.Text = InstallStatusText;
    }

    private void WebClient_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
        IsDownloading = false;
        InstallerDownloadPercent = 100;
        VPNInstallStatusWarning.StateChanged?.Invoke();
        DownloadStatusMsg.Text = InstallStatusText;
        if (e.Cancelled)
        {
            Log.Log(LogLevel.Error, "The download of VPNNederland-Setup.exe has been cancelled");
            ResetUI();
        }
        else if (e.Error != null)
        {
            Log.Log(LogLevel.Error, "An error ocurred while trying to download VPNNederland-Setup.exe file");
            ResetUI();
        }
        else
        {
            Process.Start(_downloadToFile);
        }
    }

    private void StartVPNClient_Click(object sender, RoutedEventArgs e)
    {
        string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string fileName = folderPath + System.IO.Path.DirectorySeparatorChar + "VPNNederland" + System.IO.Path.DirectorySeparatorChar + "VPNNederland.exe";
        if (VPNStatusChecker.IsVPNNederlandInstalled())
        {
            Process.Start(fileName);
            StartHidding();
        }
    }
}