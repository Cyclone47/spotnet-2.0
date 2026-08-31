using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using Awesomium.Core;
using Awesomium.Windows.Controls;
using GalaSoft.MvvmLight.Threading;
using NLog;
using Pri.LongPath;
using Spotnet.Controls;
using Spotnet.Downloader.ViewModel;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;
using Spotnet.Properties;

namespace Spotnet.Browser;
public partial class AwesomiumPage : System.Windows.Controls.UserControl, IPage, ICloseableView, IDisposable, INotifyPropertyChanged
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static WebSession _session;
    private static bool _isWebCoreInitialized;
    private static readonly object IsWebCoreInitializedLock = new object ();
    private readonly object _syncRoot = new object ();
    private Uri _browserSource;
    private bool _isDisposed;
    private PageTypeEnum _oldType;
    private string _title;
    protected CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
    public WebControl Browser { get; private set; }
    public bool IsDomReady { get; private set; }
    public Uri Uri { get; protected set; }

    public Uri BrowserSource
    {
        get
        {
            return _browserSource;
        }

        protected set
        {
            _browserSource = value;
            OnPropertyChanged("BrowserSource");
        }
    }

    public virtual string Title
    {
        get
        {
            return _title;
        }

        protected set
        {
            if (!(_title == value))
            {
                _title = value;
                this.TitleChangedEvent?.Invoke(this);
            }
        }
    }

    public PageTypeEnum PageDefaultType { get; protected set; }

    public PageTypeEnum PageType
    {
        get
        {
            if (!Browser.IsLoading)
            {
                return PageDefaultType;
            }

            return PageTypeEnum.Loading;
        }
    }

    public TabItem TabItem { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;
    public event Action<object> TitleChangedEvent;
    public event Action<object> TypeChangedEvent;
    public event Action<object> AddressChangedEvent;
    public event Action<object, DocumentReadyEventArgs> DocumentReadyEvent;
    public event Action DocumentUnloadedEvent;
    public event Action TemplateAppliedEvent;
    public event Action BrowserGotFocusEvent;
    protected AwesomiumPage()
    {
        lock (_syncRoot)
        {
            InitializeSession();
            Browser = new WebControl
            {
                WebSession = _session
            };
            System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu
            {
                FontFamily = base.FontFamily,
                FontSize = (double)System.Windows.Application.Current.Resources["ContextMenuFontSize"],
                FontStyle = base.FontStyle
            };
            Browser.ContextMenu = contextMenu;
            System.Windows.Data.Binding binding = new System.Windows.Data.Binding("BrowserSource")
            {
                Source = this
            };
            Browser.SetBinding(WebControl.SourceProperty, binding);
            Browser.LoadingFrame += BrowserOnLoadingFrame;
            Browser.LoadingFrameComplete += BrowserOnLoadingFrameComplete;
            Browser.TitleChanged += BrowserOnTitleChanged;
            Browser.PreviewKeyDown += BrowserOnPreviewKeyDown;
            Browser.AddressChanged += BrowserOnAddressChanged;
            Browser.DocumentReady += OnDocumentReady;
            Browser.ShowCreatedWebView += BrowserOnShowCreatedWebView;
            TemplateAppliedEvent += OnTemplateAppliedEvent;
            InitializeComponent();
            WebGrid.Children.Add(Browser);
        }
    }

    public AwesomiumPage(string url) : this()
    {
        if (url.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("URL should be specified");
        }

        PageDefaultType = PageTypeEnum.WebPage;
        Uri = new Uri(url.Trim(), UriKind.RelativeOrAbsolute);
        Title = Uri.Host;
    }

    public void FocusDocument()
    {
        if (!CancellationTokenSource.IsCancellationRequested)
        {
            Browser.Focus();
            this.BrowserGotFocusEvent?.Invoke();
        }
    }

    public virtual void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
        }

        if (!CancellationTokenSource.IsCancellationRequested)
        {
            Unload();
            GC.SuppressFinalize(this);
        }
    }

    private void OnTypeProbablyChanged()
    {
        if (_oldType != PageType)
        {
            _oldType = PageType;
            this.TypeChangedEvent?.Invoke(this);
        }
    }

    public static async Task InitializeWebCore()
    {
        if (_isWebCoreInitialized)
        {
            return;
        }

        await Task.Run(delegate
        {
            lock (IsWebCoreInitializedLock)
            {
                if (!_isWebCoreInitialized)
                {
                    WebCore.Initialized += delegate
                    {
                        WebCore.ResourceInterceptor = new SpotResourceInterceptor();
                    };
                    WebConfig config = default(WebConfig);
                    config.LogLevel = Awesomium.Core.LogLevel.None;
                    WebCore.Initialize(config);
                    WebCore.Download += WebCoreOnDownload;
                    _isWebCoreInitialized = true;
                }
            }
        });
    }

    private static void WebCoreOnDownload(object sender, DownloadEventArgs args)
    {
        if (args.MimeType.Contains("nzb"))
        {
            string url = args.Url.AbsoluteUri;
            url = url.Replace("nzbindex.nl/release", "nzbindex.nl/download");
            Task.Run(delegate
            {
                ProcessNzb(url);
            });
            args.Cancel = true;
            CloseTheTabAsync(args.ViewId);
        }
    }

    private static bool ProcessNzb(string url)
    {
        DownloaderItemViewModel downloaderItemViewModel = null;
        try
        {
            string text = AppHelper.GenerateNzbFilePath(Path.GetFileName(url));
            if (Settings.Default.DownloadAction <= 1)
            {
                downloaderItemViewModel = Sys.Downloader.AddFakeItemBeforeNzbDownloaded(Path.GetFileNameWithoutExtension(text), null, 0);
            }

            if (!DownloadFile(url, text) || new FileInfo(text).Length == 0L)
            {
                return false;
            }

            if (!text.IsNullOrEmpty())
            {
                if (downloaderItemViewModel == null)
                {
                    downloaderItemViewModel = DownloaderItemFactory.New(Path.GetFileNameWithoutExtension(text));
                }

                SpotHelper.DoDownload(text, downloaderItemViewModel);
            }
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
            return false;
        }

        return true;
    }

    private static void CloseTheTabAsync(int viewId)
    {
        DispatcherHelper.RunAsync(delegate
        {
            IPage page = PagesFactory.AllPages.FirstOrDefault((IPage p) => p is AwesomiumPage && ((AwesomiumPage)p).Browser.Identifier == viewId);
            if (page != null && page.TabItem is CloseableTabItem)
            {
                if (Settings.Default.DownloadAction <= 1)
                {
                    Sys.MainWindow.TabControl1.SelectedIndex = 1;
                    ((CloseableTabItem)page.TabItem).SetParentTab(null);
                }

                ((CloseableTabItem)page.TabItem).CloseMe();
            }
        });
    }

    private static bool DownloadFile(string url, string fileName)
    {
        using WebClient webClient = new WebClient();
        try
        {
            webClient.DownloadFile(url, fileName);
            return true;
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
            return false;
        }
    }

    public static void InitializeSession()
    {
        if (_session != null)
        {
            return;
        }

        DispatcherHelper.CheckBeginInvokeOnUI(delegate
        {
            if (_session == null)
            {
                _session = WebCore.CreateWebSession(AppHelper.GetTempPath() + "/AwesomiumCache/", new WebPreferences { SmoothScrolling = true, WebGL = true, EnableGPUAcceleration = true, CanScriptsCloseWindows = false, CanScriptsOpenWindows = false });
            }
        });
    }

    protected virtual void BrowserOnLoadingFrame(object sender, LoadingFrameEventArgs loadingFrameEventArgs)
    {
        OnTypeProbablyChanged();
    }

    private void BrowserOnLoadingFrameComplete(object sender, FrameEventArgs frameEventArgs)
    {
        OnTypeProbablyChanged();
    }

    private void BrowserOnShowCreatedWebView(object sender, ShowCreatedWebViewEventArgs args)
    {
        if (args.TargetURL.AbsoluteUri.StartsWith(ResponsePage.GetResponseSiteUrl()))
        {
            Sys.MainWindow.OpenPage(PageTypeEnum.ResponseSite);
        }
        else
        {
            Sys.MainWindow.OpenPage(PageTypeEnum.WebPage, args.TargetURL.AbsoluteUri);
        }
    }

    private void OnTemplateAppliedEvent()
    {
        if (Uri != null)
        {
            BrowserSource = Uri;
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Browser.ApplyTemplate();
        Task.Run(delegate
        {
            this.TemplateAppliedEvent?.Invoke();
        });
    }

    private void OnDocumentReady(object sender, DocumentReadyEventArgs e)
    {
        WebGrid.Visibility = Visibility.Visible;
        OnTypeProbablyChanged();
        this.DocumentReadyEvent?.Invoke(sender, e);
        if (e.ReadyState == DocumentReadyState.Loaded)
        {
            IsDomReady = true;
        }
    }

    public DispatcherOperation CreateJecAsync(Action action)
    {
        return DispatcherHelper.UIDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
        {
            if (!CancellationTokenSource.IsCancellationRequested)
            {
                Browser.CreateJavascriptExecutionContext(delegate
                {
                    action?.Invoke();
                });
            }
        });
    }

    public void CreateJecSync(Action action)
    {
        DispatcherHelper.UIDispatcher.Invoke(delegate
        {
            if (!CancellationTokenSource.IsCancellationRequested)
            {
                Browser.CreateJavascriptExecutionContext(delegate
                {
                    action?.Invoke();
                });
            }
        });
    }

    private void BrowserOnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs keyEventArgs)
    {
        if (!CancellationTokenSource.IsCancellationRequested && Keyboard.Modifiers == ModifierKeys.Control && Sys.MainWindow.OnKeyDown(new PreviewKeyDownEventArgs((Keys)(0x20000 | KeyInterop.VirtualKeyFromKey(keyEventArgs.Key)))))
        {
            keyEventArgs.Handled = true;
        }
    }

    private void BrowserOnAddressChanged(object sender, UrlEventArgs urlArgs)
    {
        if (!CancellationTokenSource.IsCancellationRequested)
        {
            this.AddressChangedEvent?.Invoke(sender);
        }
    }

    private void BrowserOnTitleChanged(object sender, TitleChangedEventArgs titleChangedEventArgs)
    {
        if (!CancellationTokenSource.IsCancellationRequested)
        {
            Title = titleChangedEventArgs.Title;
        }
    }

    public void Unload()
    {
        lock (_syncRoot)
        {
            if (CancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            CancellationTokenSource.Cancel();
        }

        try
        {
            if (Browser != null && !Browser.IsDisposed)
            {
                Browser.TitleChanged -= BrowserOnTitleChanged;
                Browser.PreviewKeyDown -= BrowserOnPreviewKeyDown;
                Browser.AddressChanged -= BrowserOnAddressChanged;
                Browser.DocumentReady -= OnDocumentReady;
                Browser.Dispose();
            }

            this.DocumentUnloadedEvent?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Exception(ex, showToClient: true);
        }
    }

    public void ExecuteJavascript(string script)
    {
        DispatcherHelper.CheckBeginInvokeOnUI(delegate
        {
            Browser.ExecuteJavascript(script);
        });
    }

    public JSValue ExecuteJavascriptWithResult(string script)
    {
        return DispatcherHelper.UIDispatcher.Invoke(() => Browser.ExecuteJavascriptWithResult(script));
    }

    public bool IsDocumentReady()
    {
        return Browser.IsDocumentReady;
    }

    protected virtual void OnPropertyChanged(string propertyName = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}