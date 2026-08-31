using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GalaSoft.MvvmLight.Threading;
using MahApps.Metro.Controls;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using NLog;
using Spotnet.Controls;
using Spotnet.Downloader;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;
using Spotnet.Properties;

namespace Spotnet.Views;
public partial class SelectProviderWindow : MetroWindow
{
    [Flags]
    public enum HighlightControl
    {
        None = 0,
        Login = 1,
        HeaderAddress = 2,
        DownloadAddress = 4,
        UploadAddress = 8,
        HeaderPort = 0x10,
        DownloadPort = 0x20,
        UploadPort = 0x40,
        All = 0x7F
    }

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly Brush _fieldValidBackground = Brushes.White;
    private readonly Brush _fieldInvalidBackground = Brushes.LemonChiffon;
    private readonly object _lockRoot = new object ();
    public bool BSuc;
    private bool _initializationFinished;
    private string _lastSettingsString;
    private const string AutoConnectionsString = "Auto";
    public static bool IsRunning => DispatcherHelper.UIDispatcher.Invoke(() => Application.Current.Windows.OfType<SelectProviderWindow>().Any());
    private string CurrentSettingsString => HeaderServerTextBox.Text + HeaderServerPortComboBox.Text.Split()[0] + DownloadServerTextBox.Text + DownloadServerPortComboBox.Text.Split()[0] + UploadServerTextBox.Text + UploadServerPortComboBox.Text.Split()[0] + ConnectionsCombo.Text + UserNameTextBox.Text + PasswordTextBox.Password;
    private Storyboard FieldAnimation => (Storyboard)base.Resources["FieldAnimationStoryboard"];
    public HighlightControl HighlightedControl { get; set; }
    private bool AreSettingsChanged => _lastSettingsString != CurrentSettingsString;

    public SelectProviderWindow()
    {
        base.Closing += ProviderSelectie_Closing;
        base.Initialized += ProviderSelectie_Initialized;
        InitializeComponent();
    }

    private void DoButton()
    {
        string sError1 = "";
        ServerInfo serverForHeaders = new ServerInfo();
        ServerInfo serverForDownload = new ServerInfo();
        ServerInfo serverForUpload = new ServerInfo();
        Task.Factory.StartNew(delegate
        {
            string text2 = DownloadServerTextBox.Text.Trim().ToLower();
            string text3 = UploadServerTextBox.Text.Trim().ToLower();
            string text4 = HeaderServerTextBox.Text.Trim().ToLower();
            serverForHeaders.Server = text4;
            serverForUpload.Server = ((!text3.IsNullOrEmpty()) ? text3 : text4);
            serverForDownload.Server = ((!text2.IsNullOrEmpty()) ? text2 : text4);
            serverForHeaders.Port = Conversions.ToInteger(RemoveStrings(HeaderServerPortComboBox.Text));
            serverForUpload.Port = Conversions.ToInteger(RemoveStrings(UploadServerPortComboBox.Text));
            serverForDownload.Port = Conversions.ToInteger(RemoveStrings(DownloadServerPortComboBox.Text));
            serverForDownload.SSL = serverForDownload.DoesProviderUseSsl();
            serverForHeaders.SSL = serverForHeaders.DoesProviderUseSsl();
            serverForUpload.SSL = serverForUpload.DoesProviderUseSsl();
            if (ConnectionsCombo.Text.Equals("Auto"))
            {
                serverForDownload.Connections = 0;
            }
            else
            {
                serverForDownload.Connections = Conversions.ToInteger(RemoveStrings(ConnectionsCombo.Text)) - 2;
                if (serverForDownload.Connections < 1)
                {
                    serverForDownload.Connections = 1;
                }
            }

            serverForHeaders.Connections = 2;
            serverForUpload.Connections = 1;
            serverForHeaders.Username = UserNameTextBox.Text;
            serverForUpload.Username = serverForHeaders.Username;
            serverForDownload.Username = serverForHeaders.Username;
            serverForHeaders.Password = PasswordTextBox.Password;
            serverForUpload.Password = serverForHeaders.Password;
            serverForDownload.Password = serverForHeaders.Password;
        }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext()).ContinueWith(delegate
        {
            DownloadQueue.StopDownloadQueue();
            return AppHelper.TestConnections(new List<ServerInfo> { serverForDownload, serverForHeaders }, Settings.Default.UseSocksProxy, out sError1);
        }, CancellationToken.None, TaskContinuationOptions.LongRunning, TaskScheduler.Default).ContinueWith(delegate (Task<bool> t)
        {
            try
            {
                base.Cursor = null;
                Mouse.OverrideCursor = null;
                ConnectProgressRing.IsActive = false;
                Sys.EuroUsenetRetention = 0;
                if (t.Result)
                {
                    BSuc = true;
                    AppHelper.ServersDb.OHeader = serverForHeaders;
                    AppHelper.ServersDb.OUp = serverForUpload;
                    AppHelper.ServersDb.ODown = serverForDownload;
                    AppHelper.ResetProviderDetermination();
                    AppHelper.ServersDb.InitCacheServers();
                    string sError2 = "";
                    AppHelper.ServersDb.SaveServers(ref sError2);
                    Log.Info($"Provider changed to {serverForHeaders.Server}: {serverForHeaders.Port}");
                    Log.Debug("Socks5 proxy is used: " + (Settings.Default.UseSocksProxy ? "yes" : "no"));
                    Close();
                }
                else
                {
                    int errorCode = GetErrorCode(sError1);
                    HighlightControl highlightControl = GetHighlightControl(errorCode, sError1);
                    bool num = errorCode == 931 || errorCode == 932 || errorCode == 941 || errorCode == 950;
                    List<ServerInfo> list = new List<ServerInfo>
                    {
                        serverForHeaders
                    };
                    if (serverForHeaders.Server.ToLower().EndsWith(".snelnl.com"))
                    {
                        ServerInfo serverInfo = (ServerInfo)serverForHeaders.Clone();
                        serverInfo.Server = "news.sslusenet.com";
                        list.Add(serverInfo);
                    }

                    AdvancedMessageBox obj = (num ? new AdvancedMessageBox((List<ServerInfo> i) => AppHelper.TestConnections(i, Settings.Default.UseSocksProxy, out sError1), list, new int[4] { 119, 443, 563, 80 }) : new AdvancedMessageBox());
                    obj.Title = Words.Error;
                    obj.TextBlock.Text = GetErrorMessage(errorCode, sError1);
                    obj.Owner = this;
                    string text = obj.ShowDialog();
                    if (text.IsNullOrEmpty())
                    {
                        StartAnimation(highlightControl);
                        EnableVal(bVal: true);
                    }
                    else
                    {
                        string[] serverArr = text.Split(':');
                        base.Dispatcher.BeginInvoke((Action)delegate
                        {
                            HeaderServerTextBox.Text = serverArr[0];
                            HeaderServerPortComboBox.Text = serverArr[1];
                            UploadServerCheckBox.IsEnabled = false;
                            DownloadServerCheckBox.IsEnabled = false;
                            base.Dispatcher.BeginInvoke((Action)delegate
                            {
                                ConnectButton_Click(null, null);
                            });
                        });
                    }
                }
            }
            finally
            {
                Sys.Downloader.RestartProcessAsync();
            }
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void EnableVal(bool bVal)
    {
        ProviderBox.IsEnabled = bVal;
        HeaderServerTextBox.IsEnabled = bVal;
        HeaderServerPortComboBox.IsEnabled = bVal;
        UploadServerTextBox.IsEnabled = UploadServerCheckBox.IsChecked.GetValueOrDefault() && bVal;
        UploadServerPortComboBox.IsEnabled = UploadServerCheckBox.IsChecked.GetValueOrDefault() && bVal;
        DownloadServerTextBox.IsEnabled = DownloadServerCheckBox.IsChecked.GetValueOrDefault() && bVal;
        DownloadServerPortComboBox.IsEnabled = DownloadServerCheckBox.IsChecked.GetValueOrDefault() && bVal;
        ConnectionsCombo.IsEnabled = bVal;
        UserNameTextBox.IsEnabled = bVal;
        PasswordTextBox.IsEnabled = bVal;
        CancelButton.IsEnabled = bVal;
        if (bVal)
        {
            UpdateConnectButtonState();
        }
        else
        {
            ConnectButton.IsEnabled = false;
        }
    }

    private static int GetErrorCode(string errorText)
    {
        if (errorText == "Gebruikersnaam en/of wachtwoord is onjuist.")
        {
            errorText = Words.UsernamePasswordWrong + "(502)";
        }

        if (!int.TryParse(Regex.Match(errorText, ".*\\((?<code>\\d+)\\)").Groups["code"].Value, out var result))
        {
            return -1;
        }

        return result;
    }

    private static string GetErrorMessage(int errorCode, string errorText = "")
    {
        switch (errorCode)
        {
            case 381:
            case 400:
            case 450:
            case 452:
            case 480:
            case 481:
            case 482:
            case 502:
                if (errorText.ToLower().Contains("connection") || errorText.ToLower().Contains("too many"))
                {
                    return errorText;
                }

                return Words.UsernamePasswordWrong + Environment.NewLine + Words.CheckUsernamePasswordAndTryAgain;
            case 941:
            case 950:
                return Words.HostIsUnknown + Environment.NewLine + Words.CheckServerOrPortAndTryAgain;
            case 932:
                return Words.CannotConnectToPort + Environment.NewLine + Words.CheckPortIsCorrect;
            case 931:
                return errorText;
            default:
                return errorText;
        }
    }

    private static HighlightControl GetHighlightControl(int errorCode, string errorText = "")
    {
        switch (errorCode)
        {
            case 381:
            case 400:
            case 450:
            case 452:
            case 480:
            case 481:
            case 482:
            case 502:
                if (!errorText.ToLower().Contains("connection") && !errorText.ToLower().Contains("too many"))
                {
                    return HighlightControl.Login;
                }

                return HighlightControl.None;
            case 941:
            case 950:
                return HighlightControl.HeaderAddress;
            case 931:
                if (!errorText.StartsWith("The connection attempt timed out"))
                {
                    return HighlightControl.None;
                }

                return HighlightControl.HeaderAddress;
            case 932:
                return HighlightControl.HeaderPort;
            default:
                return HighlightControl.None;
        }
    }

    private string RemoveStrings(string msg)
    {
        string empty = string.Empty;
        empty = Regex.Matches(msg, "[0-9.-]").Cast<Match>().Aggregate(empty, (string c, Match m) => c + m.ToStringSafely());
        if (!empty.Contains("-"))
        {
            empty = Regex.Matches(msg, "[0-9.+]").Cast<Match>().Aggregate("", (string c, Match m) => c + m.ToStringSafely());
        }

        if (empty.IsNullOrEmpty())
        {
            empty = "0";
        }

        if (msg.IsNullOrEmpty())
        {
            empty = "0";
        }

        return Strings.Replace(empty, "-", "");
    }

    private void StartAnimation(HighlightControl control)
    {
        switch (control)
        {
            case HighlightControl.Login:
                UserNameTextBoxBorder.BeginStoryboard(FieldAnimation, HandoffBehavior.SnapshotAndReplace, isControllable: true);
                UserNameTextBoxBorder.Visibility = Visibility.Visible;
                PasswordTextBoxBorder.BeginStoryboard(FieldAnimation, HandoffBehavior.SnapshotAndReplace, isControllable: true);
                PasswordTextBoxBorder.Visibility = Visibility.Visible;
                UserNameTextBox.Tag = true;
                break;
            case HighlightControl.HeaderAddress:
                HeaderServerTextBoxBorder.BeginStoryboard(FieldAnimation, HandoffBehavior.SnapshotAndReplace, isControllable: true);
                HeaderServerTextBoxBorder.Visibility = Visibility.Visible;
                HeaderServerTextBox.Tag = true;
                HeaderServerPortComboBoxBorder.BeginStoryboard(FieldAnimation, HandoffBehavior.SnapshotAndReplace, isControllable: true);
                HeaderServerPortComboBoxBorder.Visibility = Visibility.Visible;
                HeaderServerPortComboBoxBorder.Tag = true;
                break;
            case HighlightControl.DownloadAddress:
                DownloadServerTextBoxBorder.BeginStoryboard(FieldAnimation, HandoffBehavior.SnapshotAndReplace, isControllable: true);
                DownloadServerTextBoxBorder.Visibility = Visibility.Visible;
                DownloadServerTextBox.Tag = true;
                DownloadServerPortComboBoxBorder.BeginStoryboard(FieldAnimation, HandoffBehavior.SnapshotAndReplace, isControllable: true);
                DownloadServerPortComboBoxBorder.Visibility = Visibility.Visible;
                DownloadServerPortComboBoxBorder.Tag = true;
                break;
            case HighlightControl.UploadAddress:
                UploadServerTextBoxBorder.BeginStoryboard(FieldAnimation, HandoffBehavior.SnapshotAndReplace, isControllable: true);
                UploadServerTextBoxBorder.Visibility = Visibility.Visible;
                UploadServerTextBox.Tag = true;
                UploadServerPortComboBoxBorder.BeginStoryboard(FieldAnimation, HandoffBehavior.SnapshotAndReplace, isControllable: true);
                UploadServerPortComboBoxBorder.Visibility = Visibility.Visible;
                UploadServerPortComboBoxBorder.Tag = true;
                break;
            case HighlightControl.HeaderPort:
                HeaderServerPortComboBoxBorder.BeginStoryboard(FieldAnimation, HandoffBehavior.SnapshotAndReplace, isControllable: true);
                HeaderServerPortComboBoxBorder.Visibility = Visibility.Visible;
                HeaderServerPortComboBoxBorder.Tag = true;
                break;
            default:
                Log.Warn(Words.WrongValueCheckHighlighted);
                break;
        }
    }

    private void StopAnimation(HighlightControl control = HighlightControl.All)
    {
        bool flag = default(bool);
        int num;
        if (control.HasFlag(HighlightControl.Login))
        {
            object tag;
            if ((tag = UserNameTextBox.Tag) is bool)
            {
                flag = (bool)tag;
                num = 1;
            }
            else
            {
                num = 0;
            }
        }
        else
        {
            num = 0;
        }

        if (((uint)num & (flag ? 1u : 0u)) != 0)
        {
            FieldAnimation.Stop(UserNameTextBoxBorder);
            FieldAnimation.Stop(PasswordTextBoxBorder);
            UserNameTextBox.Tag = false;
        }

        bool flag2 = default(bool);
        int num2;
        if (control.HasFlag(HighlightControl.HeaderAddress))
        {
            object tag;
            if ((tag = HeaderServerTextBox.Tag) is bool)
            {
                flag2 = (bool)tag;
                num2 = 1;
            }
            else
            {
                num2 = 0;
            }
        }
        else
        {
            num2 = 0;
        }

        if (((uint)num2 & (flag2 ? 1u : 0u)) != 0)
        {
            FieldAnimation.Stop(HeaderServerTextBoxBorder);
            HeaderServerTextBox.Tag = false;
        }

        bool flag3 = default(bool);
        int num3;
        if (control.HasFlag(HighlightControl.HeaderPort))
        {
            object tag;
            if ((tag = HeaderServerPortComboBoxBorder.Tag) is bool)
            {
                flag3 = (bool)tag;
                num3 = 1;
            }
            else
            {
                num3 = 0;
            }
        }
        else
        {
            num3 = 0;
        }

        if (((uint)num3 & (flag3 ? 1u : 0u)) != 0)
        {
            FieldAnimation.Stop(HeaderServerPortComboBoxBorder);
            HeaderServerPortComboBoxBorder.Tag = false;
        }

        bool flag4 = default(bool);
        int num4;
        if (control.HasFlag(HighlightControl.UploadAddress))
        {
            object tag;
            if ((tag = UploadServerTextBox.Tag) is bool)
            {
                flag4 = (bool)tag;
                num4 = 1;
            }
            else
            {
                num4 = 0;
            }
        }
        else
        {
            num4 = 0;
        }

        if (((uint)num4 & (flag4 ? 1u : 0u)) != 0)
        {
            FieldAnimation.Stop(UploadServerTextBoxBorder);
            UploadServerTextBox.Tag = false;
        }

        bool flag5 = default(bool);
        int num5;
        if (control.HasFlag(HighlightControl.UploadPort))
        {
            object tag;
            if ((tag = UploadServerPortComboBoxBorder.Tag) is bool)
            {
                flag5 = (bool)tag;
                num5 = 1;
            }
            else
            {
                num5 = 0;
            }
        }
        else
        {
            num5 = 0;
        }

        if (((uint)num5 & (flag5 ? 1u : 0u)) != 0)
        {
            FieldAnimation.Stop(UploadServerPortComboBoxBorder);
            UploadServerPortComboBoxBorder.Tag = false;
        }

        bool flag6 = default(bool);
        int num6;
        if (control.HasFlag(HighlightControl.DownloadAddress))
        {
            object tag;
            if ((tag = DownloadServerTextBox.Tag) is bool)
            {
                flag6 = (bool)tag;
                num6 = 1;
            }
            else
            {
                num6 = 0;
            }
        }
        else
        {
            num6 = 0;
        }

        if (((uint)num6 & (flag6 ? 1u : 0u)) != 0)
        {
            FieldAnimation.Stop(DownloadServerTextBoxBorder);
            DownloadServerTextBox.Tag = false;
        }

        bool flag7 = default(bool);
        int num7;
        if (control.HasFlag(HighlightControl.DownloadPort))
        {
            object tag;
            if ((tag = DownloadServerPortComboBoxBorder.Tag) is bool)
            {
                flag7 = (bool)tag;
                num7 = 1;
            }
            else
            {
                num7 = 0;
            }
        }
        else
        {
            num7 = 0;
        }

        if (((uint)num7 & (flag7 ? 1u : 0u)) != 0)
        {
            FieldAnimation.Stop(DownloadServerPortComboBoxBorder);
            DownloadServerPortComboBoxBorder.Tag = false;
        }
    }

    private void HeaderServerTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        object tag;
        bool flag = default(bool);
        int num;
        if ((tag = HeaderServerTextBox.Tag) is bool)
        {
            flag = (bool)tag;
            num = 1;
        }
        else
        {
            num = 0;
        }

        if (((uint)num & (flag ? 1u : 0u)) != 0)
        {
            StopAnimation(HighlightControl.HeaderAddress);
        }
    }

    private void UploadServerTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        object tag;
        bool flag = default(bool);
        int num;
        if ((tag = UploadServerTextBox.Tag) is bool)
        {
            flag = (bool)tag;
            num = 1;
        }
        else
        {
            num = 0;
        }

        if (((uint)num & (flag ? 1u : 0u)) != 0)
        {
            StopAnimation(HighlightControl.UploadAddress);
        }
    }

    private void DownloadServerTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        object tag;
        bool flag = default(bool);
        int num;
        if ((tag = DownloadServerTextBox.Tag) is bool)
        {
            flag = (bool)tag;
            num = 1;
        }
        else
        {
            num = 0;
        }

        if (((uint)num & (flag ? 1u : 0u)) != 0)
        {
            StopAnimation(HighlightControl.DownloadAddress);
        }
    }

    private void HeaderServerPortComboBox_GotFocus(object sender, RoutedEventArgs e)
    {
        object tag;
        bool flag = default(bool);
        int num;
        if ((tag = HeaderServerPortComboBoxBorder.Tag) is bool)
        {
            flag = (bool)tag;
            num = 1;
        }
        else
        {
            num = 0;
        }

        if (((uint)num & (flag ? 1u : 0u)) != 0)
        {
            StopAnimation(HighlightControl.HeaderPort);
        }
    }

    private void UploadServerPortComboBox_GotFocus(object sender, RoutedEventArgs e)
    {
        object tag;
        bool flag = default(bool);
        int num;
        if ((tag = UploadServerPortComboBoxBorder.Tag) is bool)
        {
            flag = (bool)tag;
            num = 1;
        }
        else
        {
            num = 0;
        }

        if (((uint)num & (flag ? 1u : 0u)) != 0)
        {
            StopAnimation(HighlightControl.UploadPort);
        }
    }

    private void DownloadServerPortComboBox_GotFocus(object sender, RoutedEventArgs e)
    {
        object tag;
        bool flag = default(bool);
        int num;
        if ((tag = DownloadServerPortComboBoxBorder.Tag) is bool)
        {
            flag = (bool)tag;
            num = 1;
        }
        else
        {
            num = 0;
        }

        if (((uint)num & (flag ? 1u : 0u)) != 0)
        {
            StopAnimation(HighlightControl.DownloadPort);
        }
    }

    private void ProviderBox_GotFocus(object sender, RoutedEventArgs e)
    {
        StopAnimation();
    }

    private void UserNameTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        object tag;
        bool flag = default(bool);
        int num;
        if ((tag = UserNameTextBox.Tag) is bool)
        {
            flag = (bool)tag;
            num = 1;
        }
        else
        {
            num = 0;
        }

        if (((uint)num & (flag ? 1u : 0u)) != 0)
        {
            StopAnimation(HighlightControl.Login);
        }
    }

    private void PasswordTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        UserNameTextBox_GotFocus(sender, e);
    }

    private void ProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ProviderItem providerItem = (ProviderItem)ProviderBox.SelectedItem;
        if (providerItem.Headers.IsNullOrEmpty())
        {
            AdvancedSettingsExpander.IsExpanded = true;
            return;
        }

        HeaderServerTextBox.Text = providerItem.Headers;
        UploadServerTextBox.Text = providerItem.Upload;
        DownloadServerTextBox.Text = providerItem.Download;
        SetServerPort(providerItem.HeadersPort, HeaderServerPortComboBox);
        SetServerPort(providerItem.UploadPort, UploadServerPortComboBox);
        SetServerPort(providerItem.DownloadPort, DownloadServerPortComboBox);
        SyncCheckBoxesWithHeaderServer();
        EnableVal(bVal: true);
        UserNameTextBox.Clear();
        PasswordTextBox.Clear();
        ConnectionsCombo.Text = "Auto";
        UserNameTextBox.Focus();
    }

    private void ProviderSelectie_Closing(object sender, CancelEventArgs e)
    {
        if (e.Cancel)
        {
            return;
        }

        try
        {
            base.Owner.Activate();
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
        }
    }

    private void SetServerPort(int port, ComboBox portCombo)
    {
        switch (port)
        {
            case 0:
            case 119:
                portCombo.SelectedIndex = 0;
                break;
            case 443:
                portCombo.SelectedIndex = 1;
                break;
            case 563:
                portCombo.SelectedIndex = 2;
                break;
            case 80:
                portCombo.SelectedIndex = 3;
                break;
            default:
                portCombo.Text = port.ToString(CultureInfo.InvariantCulture);
                break;
        }
    }

    private void ProviderSelectie_Initialized(object sender, EventArgs e)
    {
        CreateTextWithTheLink();
        Collection collection = new Collection();
        try
        {
            base.FontSize = (int)Settings.Default.FontSize;
            collection.Add("5 Euro Usenet#reader.5eurousenet.com#80");
            collection.Add("Eweka#newsreader1.eweka.nl#443#upload.eweka.nl#443#textnews.eweka.nl#443");
            collection.Add("KPN v1#nova.planet.nl#563#text.nova.planet.nl#563#text.nova.planet.nl#563");
            collection.Add("KPN v2#textnews.kpn.nl#563#news.kpn.nl#563");
            collection.Add("NewsXS#reader2.newsxs.nl#443");
            collection.Add("XSnews#reader.xsnews.nl#443#upload.xsnews.nl#443");
            collection.Add("SnelNL#reader.snelnl.com#80");
            collection.Add("Extreme Usenet#reader.extremeusenet.nl#443");
            collection.Add("Sunny Usenet#news.sunnyusenet.com#443");
            collection.Add("Pure Usenet#news.pureusenet.nl#443");
            collection.Add("Tele2#tele2news.tweaknews.nl#563");
            collection.Add(" ");
            collection.Add(Words.Other + "...##563");
            foreach (object item in collection)
            {
                string text = item.ToStringSafely();
                if (!text.Trim().IsNullOrEmpty())
                {
                    string[] array = Strings.Split(text, "#");
                    ProviderItem newItem = new ProviderItem
                    {
                        Download = array[1],
                        Upload = ((array.Length > 3) ? array[3] : array[1]),
                        Headers = ((array.Length > 5) ? array[5] : array[1]),
                        Name = array[0],
                        DownloadPort = Conversions.ToInteger(array[2]),
                        UploadPort = Conversions.ToInteger((array.Length > 4) ? array[4] : array[2]),
                        HeadersPort = Conversions.ToInteger((array.Length > 6) ? array[6] : array[2])
                    };
                    ProviderBox.Items.Add(newItem);
                }
                else
                {
                    ProviderBox.Items.Add(new ProviderItem());
                }
            }

            ServerInfo oDown = AppHelper.ServersDb.ODown;
            ServerInfo oHeader = AppHelper.ServersDb.OHeader;
            ServerInfo oUp = AppHelper.ServersDb.OUp;
            DownloadServerTextBox.Text = (oDown.Server.IsNullOrEmpty() ? "" : oDown.Server);
            HeaderServerTextBox.Text = (oHeader.Server.IsNullOrEmpty() ? "" : oHeader.Server);
            UploadServerTextBox.Text = (oUp.Server.IsNullOrEmpty() ? "" : oUp.Server);
            SetServerPort(oDown.Port, DownloadServerPortComboBox);
            SetServerPort(oHeader.Port, HeaderServerPortComboBox);
            SetServerPort(oUp.Port, UploadServerPortComboBox);
            SyncCheckBoxesWithHeaderServer();
            ConnectionsCombo.Text = (oDown.Connections + oHeader.Connections).ToString();
            UserNameTextBox.Text = oDown.Username;
            PasswordTextBox.Password = ((!oDown.Password.IsNullOrEmpty()) ? oDown.Password : "");
            _lastSettingsString = CurrentSettingsString;
            _initializationFinished = true;
            ProviderBox.SelectionChanged += ProviderBox_SelectionChanged;
            UpdateProviderBoxSelection();
            if (ProviderBox.SelectedIndex >= ProviderBox.Items.Count - 2)
            {
                ProviderBox.Focus();
            }
            else
            {
                UserNameTextBox.Focus();
            }

            ValidateAll();
            UpdateConnectButtonState();
            Activate();
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
            Close();
        }
    }

    private void SyncCheckBoxesWithHeaderServer()
    {
        string value = HeaderServerTextBox.Text + ":" + HeaderServerPortComboBox.Text;
        string text = DownloadServerTextBox.Text + ":" + DownloadServerPortComboBox.Text;
        string text2 = UploadServerTextBox.Text + ":" + UploadServerPortComboBox.Text;
        DownloadServerCheckBox.IsChecked = !text.Equals(value);
        UploadServerCheckBox.IsChecked = !text2.Equals(value);
    }

    private void CreateTextWithTheLink()
    {
        Run item = new Run(Words.SelectProviderLinkText1);
        Run childInline = new Run(Words.SelectProviderLinkText2);
        Run item2 = new Run(Words.SelectProviderLinkText3);
        Hyperlink hyperlink = new Hyperlink(childInline)
        {
            NavigateUri = new Uri(Words.SelectProviderLinkURL)
        };
        hyperlink.RequestNavigate += Hyperlink_OnClick;
        TextWithTheLink.Inlines.Clear();
        TextWithTheLink.Inlines.Add(item);
        TextWithTheLink.Inlines.Add(hyperlink);
        TextWithTheLink.Inlines.Add(item2);
    }

    private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(((Hyperlink)sender).NavigateUri.ToString());
    }

    private void UpdateProviderBoxSelection()
    {
        if (!_initializationFinished)
        {
            return;
        }

        lock (_lockRoot)
        {
            ProviderBox.SelectionChanged -= ProviderBox_SelectionChanged;
            try
            {
                int selectedIndex = ProviderBox.SelectedIndex;
                bool flag = false;
                string text = HeaderServerTextBox.Text.Trim();
                bool flag2 = text.ToLower().EndsWith(".snelnl.com");
                for (int i = 0; i < ProviderBox.Items.Count; i++)
                {
                    if ((flag2 && ((ProviderItem)ProviderBox.Items[i]).Name.Equals("SnelNL")) || ((ProviderItem)ProviderBox.Items[i]).Headers.EqualsIgnoreCase(text))
                    {
                        if (selectedIndex != i)
                        {
                            ProviderBox.SelectedIndex = i;
                        }

                        flag = true;
                        break;
                    }
                }

                if (!flag || ProviderBox.SelectedIndex == -1)
                {
                    ProviderBox.SelectedIndex = ProviderBox.Items.Count - 1;
                }
            }
            finally
            {
                ProviderBox.SelectionChanged += ProviderBox_SelectionChanged;
            }
        }
    }

    private void UpdateConnectButtonState()
    {
        if (_initializationFinished)
        {
            List<Control> source = new List<Control>
            {
                HeaderServerTextBox,
                UploadServerTextBox,
                DownloadServerTextBox,
                HeaderServerPortComboBox,
                UploadServerPortComboBox,
                DownloadServerPortComboBox,
                ConnectionsCombo,
                UserNameTextBox,
                PasswordTextBox
            };
            ConnectButton.IsEnabled = AreSettingsChanged && !source.Any((Control f) => object.Equals(f.Background, _fieldInvalidBackground));
        }
    }

    private bool ValidateAll()
    {
        return ValidateAddress(HeaderServerTextBox) & ValidateAddress(DownloadServerTextBox) & ValidateAddress(UploadServerTextBox) & ValidatePort(HeaderServerPortComboBox) & ValidatePort(DownloadServerPortComboBox) & ValidatePort(UploadServerPortComboBox) & ValidateConnections() & ValidateUsername() & ValidatePassword();
    }

    private bool ValidateAddress(TextBox addrBox)
    {
        bool flag = !addrBox.Text.Trim().IsNullOrEmpty() && (AppHelper.IsDomainName(addrBox.Text) || AppHelper.IsIp(addrBox.Text));
        addrBox.Background = (flag ? _fieldValidBackground : _fieldInvalidBackground);
        return flag;
    }

    private bool ValidatePort(ComboBox portCombo)
    {
        if (portCombo.Text.IsNullOrEmpty())
        {
            portCombo.Background = _fieldInvalidBackground;
            return false;
        }

        int result;
        bool flag = int.TryParse(portCombo.Text.Split()[0], out result) && result > 0 && result < 65535;
        portCombo.Background = (flag ? _fieldValidBackground : _fieldInvalidBackground);
        return flag;
    }

    private bool ValidateConnections()
    {
        if (ConnectionsCombo.Text.IsNullOrEmpty())
        {
            ConnectionsCombo.Background = _fieldInvalidBackground;
            return false;
        }

        int result;
        bool flag = ConnectionsCombo.Text.Equals("Auto") || (int.TryParse(ConnectionsCombo.Text, out result) && result >= 3 && result <= 100);
        ConnectionsCombo.Background = (flag ? _fieldValidBackground : _fieldInvalidBackground);
        return flag;
    }

    private bool ValidateUsername()
    {
        bool flag = UserNameTextBox.Text.Length < 200;
        UserNameTextBox.Background = (flag ? _fieldValidBackground : _fieldInvalidBackground);
        return flag;
    }

    private bool ValidatePassword()
    {
        bool flag = PasswordTextBox.Password.Length < 200;
        PasswordTextBox.Background = (flag ? _fieldValidBackground : _fieldInvalidBackground);
        return flag;
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        StopAnimation();
        ConnectButton.Focus();
        EnableVal(bVal: false);
        base.Cursor = Cursors.Wait;
        Mouse.OverrideCursor = Cursors.Wait;
        ConnectProgressRing.IsActive = true;
        UpdateLayout();
        DoButton();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void HeaderServerTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateAddress(HeaderServerTextBox);
        UpdateConnectButtonState();
        UpdateProviderBoxSelection();
        SyncFieldsWithHeaderServer();
    }

    private void SyncFieldsWithHeaderServer()
    {
        if (!DownloadServerCheckBox.IsChecked.GetValueOrDefault())
        {
            DownloadServerTextBox.Text = HeaderServerTextBox.Text;
            DownloadServerPortComboBox.Text = HeaderServerPortComboBox.Text;
        }

        if (!UploadServerCheckBox.IsChecked.GetValueOrDefault())
        {
            UploadServerTextBox.Text = HeaderServerTextBox.Text;
            UploadServerPortComboBox.Text = HeaderServerPortComboBox.Text;
        }
    }

    private void DownloadServerTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateAddress(DownloadServerTextBox);
        UpdateConnectButtonState();
    }

    private void UploadServerTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateAddress(UploadServerTextBox);
        UpdateConnectButtonState();
    }

    private void UserNameTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateUsername();
        UpdateConnectButtonState();
    }

    private void PasswordTextBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ValidatePassword();
        UpdateConnectButtonState();
    }

    private void HiddenPortTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ValidatePort(HeaderServerPortComboBox);
        UpdateConnectButtonState();
    }

    private void HiddenConnectionsTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateConnections();
        UpdateConnectButtonState();
    }

    private void HeaderServerPortComboBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        SyncFieldsWithHeaderServer();
    }

    private void DownloadServerCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        DownloadServerTextBox.IsEnabled = false;
        DownloadServerPortComboBox.IsEnabled = false;
        if (!DownloadServerCheckBox.IsChecked.GetValueOrDefault())
        {
            DownloadServerTextBox.Text = HeaderServerTextBox.Text;
            DownloadServerPortComboBox.Text = HeaderServerPortComboBox.Text;
        }
    }

    private void DownloadServerCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        DownloadServerTextBox.IsEnabled = true;
        DownloadServerPortComboBox.IsEnabled = true;
    }

    private void UploadServerCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        UploadServerTextBox.IsEnabled = false;
        UploadServerPortComboBox.IsEnabled = false;
        if (!UploadServerCheckBox.IsChecked.GetValueOrDefault())
        {
            UploadServerTextBox.Text = HeaderServerTextBox.Text;
            UploadServerPortComboBox.Text = HeaderServerPortComboBox.Text;
        }
    }

    private void UploadServerCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        UploadServerTextBox.IsEnabled = true;
        UploadServerPortComboBox.IsEnabled = true;
    }

    private void SocksProxyCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        UpdateConnectButtonState();
    }

    private void SocksProxyCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        UpdateConnectButtonState();
    }
}