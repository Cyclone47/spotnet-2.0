using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Threading;
using Microsoft.VisualBasic.CompilerServices;
using NLog;
using Spotnet.Controls;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;
using Spotnet.Properties;

namespace Spotnet.Browser;
public partial class IEWebBrowser : System.Windows.Controls.UserControl, IPage, ICloseableView, IDisposable
{
    private static readonly Logger Log;
    private readonly object _syncRoot = new object ();
    protected volatile bool Navigating;
    private string _title;
    private bool _titleChangedFlag;
    private Uri _uri;
    protected bool AskUnload;
    protected bool IsNotSpotTab;
    public Uri Uri
    {
        get
        {
            return Brows.Url ?? _uri;
        }

        set
        {
            if (!AskUnload)
            {
                _uri = value;
                Brows.Url = _uri;
            }
        }
    }

    public TabItem TabItem { get; set; }

    public PageTypeEnum PageType
    {
        get
        {
            if (!Navigating)
            {
                return PageDefaultType;
            }

            return PageTypeEnum.Loading;
        }
    }

    public PageTypeEnum PageDefaultType { get; set; }

    public string Title
    {
        get
        {
            return _title;
        }

        set
        {
            if (_title == null || !_title.Equals(value))
            {
                _title = value;
                this.TitleChangedEvent?.Invoke(this);
            }
        }
    }

    public bool IsDomReady => true;

    public event Action<object> TitleChangedEvent;
    public event Action<object> TypeChangedEvent;
    public event Action<object> AddressChangedEvent;
    public event Action<object, PageReadyEventArgs> DocumentReadyEvent;
    public event Action DocumentUnloadedEvent;
    public event Action<object, WebBrowserNavigatingEventArgs> NavigatingEvent;
    public event Action DocumentUnloadingEvent;
    static IEWebBrowser()
    {
        Log = LogManager.GetCurrentClassLogger();
        Log.Debug("Tab theme used: " + Settings.Default.ActiveTheme);
    }

    public IEWebBrowser()
    {
        AskUnload = false;
        Navigating = true;
        base.Initialized += OnInitialized;
        InitializeComponent();
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (AskUnload)
            {
                return;
            }

            AskUnload = true;
        }

        if (Brows != null && !Brows.IsDisposed)
        {
            DispatcherHelper.UIDispatcher.Invoke(delegate
            {
                Brows.Stop();
            });
        }

        this.DocumentUnloadingEvent?.Invoke();
        this.DocumentUnloadedEvent?.Invoke();
        GC.SuppressFinalize(this);
    }

    public void FocusDocument()
    {
        try
        {
            if (Brows.IsDisposed || !(Brows.Document != null))
            {
                return;
            }

            DispatcherHelper.UIDispatcher.BeginInvoke(DispatcherPriority.Input, (ThreadStart)delegate
            {
                if (!Brows.IsDisposed && Brows.Document != null)
                {
                    Brows.Focus();
                    Brows.Document.Focus();
                }
            });
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
        }
    }

    public DispatcherOperation CreateJecAsync(Action action)
    {
        Brows.BeginInvoke(action);
        return null;
    }

    public void CreateJecSync(Action action)
    {
        if (Brows.InvokeRequired)
        {
            Brows.Invoke(action);
        }
        else
        {
            action?.Invoke();
        }
    }

    private void OnInitialized(object sender, EventArgs eventArgs)
    {
        IsNotSpotTab = !(this is SpotNativePage);
        if (!IsNotSpotTab)
        {
            ToolbarPopup.Visibility = Visibility.Visible;
        }

        try
        {
            Brows.ScriptErrorsSuppressed = IsNotSpotTab;
            Brows.WebBrowserShortcutsEnabled = IsNotSpotTab;
            Brows.IsWebBrowserContextMenuEnabled = true;
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
        }
    }

    private void brows_Disposed(object sender, EventArgs e)
    {
        try
        {
            Dispose();
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
        }
    }

    private void brows_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
    {
        try
        {
            Navigating = false;
            this.TypeChangedEvent?.Invoke(this);
            this.AddressChangedEvent?.Invoke(sender);
            if (IsNotSpotTab && Brows.Document != null)
            {
                Title = Brows.Document.Title;
            }
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
        }
        finally
        {
            this.DocumentReadyEvent?.Invoke(this, null);
        }
    }

    private void brows_DocumentTitleChanged(object sender, EventArgs e)
    {
        try
        {
            if (AskUnload)
            {
                return;
            }

            if (!_titleChangedFlag)
            {
                _titleChangedFlag = true;
                try
                {
                    if (TabItem != null && TabItem.IsSelected)
                    {
                        FocusDocument();
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }

            if (IsNotSpotTab && TabItem != null && TabItem.Tag is UrlInfo && Brows.Document != null)
            {
                Title = Brows.Document.Title;
            }
        }
        catch (Exception ex2)
        {
            Log.Exception(ex2, showToClient: true);
        }
    }

    private void brows_Navigating(object sender, WebBrowserNavigatingEventArgs e)
    {
        Navigating = true;
        this.NavigatingEvent?.Invoke(sender, e);
        this.TypeChangedEvent?.Invoke(this);
    }

    private void brows_NewWindow(object sender, CancelEventArgs e)
    {
        try
        {
            e.Cancel = true;
            string text = NewLateBinding.LateGet(sender, null, "statustext", new object[0], null, null, null).ToStringSafely();
            if (text != null && text.Length >= 6)
            {
                if (text.Substring(0, 5).ToLower().Equals("link:"))
                {
                    text = AppHelper.AddHttp(text.Substring(5));
                }
                else if (!AppHelper.HasHttp(text))
                {
                    return;
                }

                if (Settings.Default.ExternalBrowser || this is SpotNativePage)
                {
                    AppHelper.LaunchInExternalProgram(text);
                }
                else
                {
                    Sys.MainWindow.OpenPage(PageTypeEnum.WebPage, text).Forget();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
        }
    }

    private void brows_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
    {
        try
        {
            if (AskUnload || Brows.IsDisposed || Brows.Document == null || Sys.MainWindow.OnKeyDown(e))
            {
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                Brows.Document.ExecCommand("delete", showUI: false, null);
            }

            _ = e.KeyCode;
            _ = 38;
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.C:
                        Brows.Document.ExecCommand("copy", showUI: false, null);
                        break;
                    case Keys.X:
                        Brows.Document.ExecCommand("cut", showUI: false, null);
                        break;
                    case Keys.V:
                        Brows.Document.ExecCommand("paste", showUI: false, null);
                        break;
                    case Keys.Z:
                        Brows.Document.ExecCommand("undo", showUI: false, null);
                        break;
                    case Keys.A:
                        Brows.Document.ExecCommand("selectAll", showUI: false, null);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
        }
    }
}