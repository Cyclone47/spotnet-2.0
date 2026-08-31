using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Threading;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using NLog;
using Spotnet.Controls;
using Spotnet.DAL;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;
using Spotnet.Properties;
using Spotnet.Utilities;
using Spotnet.ViewModel;
using mshtml;

namespace Spotnet.Browser;

public class SpotNativePage : IEWebBrowser, ISpotPage, IPage, ICloseableView, IDisposable
{
	private struct CommentElements
	{
		internal HtmlElement AddToBlackLink;

		internal HtmlElement Avatar;

		internal HtmlElement Desc;

		internal HtmlElement FromLink;
	}

	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private static readonly List<string> FilesToRemoveOnClose = new List<string>();

	private static readonly Dictionary<string, List<Comment>> MessagesFromUserToShowOnNextTabOpen = new Dictionary<string, List<Comment>>();

	private readonly CancellationTokenSource _cancelGettingCommentsSource = new CancellationTokenSource();

	private readonly HashSet<long> _commentIdCache;

	private readonly List<long> _fetchedCache;

	private readonly HashSet<string> _messagesFromUserAndAlreadyShown;

	private readonly object _syncRoot = new object();

	private readonly Dictionary<string, string> _uniqueCache;

	private HtmlDocument _document;

	private HtmlElement _spotImage;

	private HtmlElement _addButton;

	private HtmlElement _closePreview;

	private HtmlElement _closeImdb;

	private HtmlElement _closeSmiles;

	private HtmlElement _commentProgress;

	private HtmlElement _commentsStatus;

	private HtmlElement _downloadButton;

	private HtmlElement _commentBody;

	private HtmlElement _nickname;

	private HtmlElement _reportButton;

	private HtmlElement _favButton;

	private HtmlElement _nzbInfoButton;

	private string _commentProgressCache;

	private bool _commentsRefreshWasClickedAlready;

	private bool _documentCompletedFlag;

	private string _fileToRemoveOnClose;

	private bool _isImageFullSized;

	private bool _isImageResizeable;

	private string _lastBody;

	private DateTime _lastTime;

	private bool _loadImageManually;

	private string _menuFrom;

	private string _menuModulus;

	private bool _showOnActivated;

	private readonly System.Timers.Timer _updateCommentPreviewTimer;

	private HtmlDocument Document
	{
		get
		{
			return _document;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			if (_document != null)
			{
				_document.MouseUp -= _document_MouseUp;
				_document.MouseDown -= _document_MouseDown;
				_document.ContextMenuShowing -= _document_ContextMenuShowing;
			}
			_document = value;
			if (!(_document == null))
			{
				_document.MouseUp += _document_MouseUp;
				_document.MouseDown += _document_MouseDown;
				_document.ContextMenuShowing += _document_ContextMenuShowing;
			}
		}
	}

	private HtmlElement CloseImdb
	{
		set
		{
			if (_closeImdb != null)
			{
				_closeImdb.Click -= CloseImdb_Click;
			}
			_closeImdb = value;
			if (!(_closeImdb == null))
			{
				_closeImdb.Click += CloseImdb_Click;
			}
		}
	}

	private HtmlElement CloseSmiles
	{
		set
		{
			if (_closeSmiles != null)
			{
				_closeSmiles.Click -= CloseSmiles_Click;
			}
			_closeSmiles = value;
			if (!(_closeSmiles == null))
			{
				_closeSmiles.Click += CloseSmiles_Click;
			}
		}
	}

	private HtmlElement AddButton
	{
		get
		{
			return _addButton;
		}
		set
		{
			if (_addButton != null)
			{
				_addButton.Click -= AddButton_Click;
			}
			_addButton = value;
			if (!(_addButton == null) && !SpotEx.IsPreview)
			{
				_addButton.Click += AddButton_Click;
			}
		}
	}

	private HtmlElement ReportButton
	{
		set
		{
			if (_reportButton != null)
			{
				_reportButton.Click -= ReportButton_Click;
			}
			_reportButton = value;
			if (!(_reportButton == null) && !SpotEx.IsPreview)
			{
				_reportButton.Click += ReportButton_Click;
			}
		}
	}

	private HtmlElement FavButton
	{
		set
		{
			if (_favButton != null)
			{
				_favButton.Click -= FavButton_Click;
			}
			_favButton = value;
			if (!(_favButton == null) && !SpotEx.IsPreview)
			{
				_favButton.Click += FavButton_Click;
			}
		}
	}

	private HtmlElement NzbInfoButton
	{
		get
		{
			return _nzbInfoButton;
		}
		set
		{
			if (_nzbInfoButton != null)
			{
				_nzbInfoButton.Click -= NzbInfoButton_Click;
			}
			_nzbInfoButton = value;
			if (!(_nzbInfoButton == null) && !SpotEx.IsPreview)
			{
				_nzbInfoButton.Click += NzbInfoButton_Click;
			}
		}
	}

	private bool IsDownloadButtonDisabled => DownloadButton.GetAttribute("className").Contains("disabled");

	private bool IsAddButtonDisabled => AddButton.GetAttribute("className").Contains("disabled");

	private HtmlElement DownloadButton
	{
		get
		{
			return _downloadButton;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			if (_downloadButton != null)
			{
				_downloadButton.Click -= DownloadButton_Click;
			}
			_downloadButton = value;
			if (!(_downloadButton == null) && !SpotEx.IsPreview)
			{
				_downloadButton.Click += DownloadButton_Click;
			}
		}
	}

	private HtmlElement CommentBody
	{
		get
		{
			return _commentBody;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			if (_commentBody != null)
			{
				_commentBody.KeyUp -= CommentBody_Input;
			}
			_commentBody = value;
			if (!(_commentBody == null))
			{
				_commentBody.KeyUp += CommentBody_Input;
			}
		}
	}

	private HtmlElement PreviewPanel
	{
		get; [MethodImpl(MethodImplOptions.Synchronized)]
		set;
	}

	private HtmlElement ImdbPanel
	{
		get; [MethodImpl(MethodImplOptions.Synchronized)]
		set;
	}

	private HtmlElement ImdbPanel2
	{
		get; [MethodImpl(MethodImplOptions.Synchronized)]
		set;
	}

	private HtmlElement Nickname
	{
		get
		{
			return _nickname;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			if (_nickname != null)
			{
				_nickname.KeyUp -= CommentBody_Input;
			}
			_nickname = value;
			if (!(_nickname == null))
			{
				_nickname.KeyUp += CommentBody_Input;
			}
		}
	}

	private static SpotsListViewModel SpotsListVm => ((ViewModelLocator)System.Windows.Application.Current.Resources["Locator"]).SpotsList;

	private HtmlElement SpotImage
	{
		get
		{
			return _spotImage;
		}
		[MethodImpl(MethodImplOptions.Synchronized)]
		set
		{
			if (!Settings.Default.ActiveTheme.Equals("Default"))
			{
				_spotImage = value;
				return;
			}
			if (_spotImage != null)
			{
				_spotImage.Click -= SpotImage_Click;
			}
			_spotImage = value;
			if (!(_spotImage == null))
			{
				_spotImage.Click += SpotImage_Click;
			}
		}
	}

	public SpotEx SpotEx { get; }

	public SpotNativePage(string title, SpotEx spotEx)
	{
		SpotNativePage spotNativePage = this;
		_lastBody = "";
		_commentIdCache = new HashSet<long>();
		_uniqueCache = new Dictionary<string, string>();
		_fetchedCache = new List<long>();
		_messagesFromUserAndAlreadyShown = new HashSet<string>();
		base.Title = title;
		SpotEx = spotEx;
		base.PageDefaultType = PageTypeEnum.SpotLoaded;
		base.NavigatingEvent += OnNavigatingEvent;
		base.DocumentUnloadingEvent += OnDocumentUnloadingEvent;
		base.DocumentReadyEvent += OnDocumentReadyEvent;
		_updateCommentPreviewTimer = new System.Timers.Timer
		{
			AutoReset = false,
			Interval = 200.0
		};
		_updateCommentPreviewTimer.Elapsed += delegate
		{
			try
			{
				spotNativePage.CreateJecSync(spotNativePage.UpdatePreviewPanel);
			}
			catch (Exception ex2)
			{
				Log.Exception(ex2, showToClient: true);
			}
		};
		Task.Run(delegate
		{
			try
			{
				string documentText = SpotParser.ParseSpot(spotEx, Settings.Default.SpotFontSize);
				spotNativePage.Brows.DocumentText = documentText;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to load page: " + ex.Message);
			}
		});
	}

	private void ReportButton_Click(object sender, HtmlElementEventArgs e)
	{
		Sys.MainWindow.AddComplainReportToTheSpot(SpotEx);
	}

	private void FavButton_Click(object sender, HtmlElementEventArgs e)
	{
		if (Favorites.ContainsMessageId(SpotEx.MessageId))
		{
			Favorites.Remove(SpotEx.MessageId);
			AppHelper.ShowPopupMessage(Words.FavoritesRemoved + "\r\n" + SpotEx.Title, inTheCenter: false, TimeSpan.FromSeconds(3.0));
		}
		else
		{
			Favorites.Add(SpotEx.MessageId);
			AppHelper.ShowPopupMessage(Words.FavoritesAdded + "\r\n" + SpotEx.Title, inTheCenter: false, TimeSpan.FromSeconds(3.0));
		}
		UpdateFavButton();
	}

	private void NzbInfoButton_Click(object sender, HtmlElementEventArgs e)
	{
		throw new NotImplementedException();
	}

	private void OnDocumentReadyEvent(object o, PageReadyEventArgs documentReadyEventArgs)
	{
		lock (_syncRoot)
		{
			if (_documentCompletedFlag || AskUnload)
			{
				return;
			}
			Document = Brows.Document;
			if (Document == null)
			{
				return;
			}
			_documentCompletedFlag = true;
		}
		try
		{
			AddButton = Document.GetElementById("AddComment");
			SpotImage = Document.GetElementById("SpotImage");
			DownloadButton = Document.GetElementById("DownloadButton");
			CommentBody = Document.GetElementById("CommentBody");
			PreviewPanel = Document.GetElementById("PreviewPanel");
			ImdbPanel = Document.GetElementById("ImdbPanel");
			ImdbPanel2 = Document.GetElementById("ImdbPanel2");
			CloseImdb = Document.GetElementById("CloseImdb");
			HtmlElementCollection elementsByName = Document.GetElementsByTagName("input").GetElementsByName("Nickname");
			if (elementsByName.Count > 0)
			{
				Nickname = elementsByName[0];
			}
			_commentProgress = Document.GetElementById("CommentsProgress");
			if (_commentProgress != null)
			{
				_commentProgressCache = _commentProgress.InnerHtml;
			}
			AddSpotWarning();
			if (Settings.Default.ShowTabToolbar)
			{
				Toolbar.InitializeWithViewModel(SpotEx);
				ToolbarPositioningSetup();
			}
			InitializeInternalToolbarControls();
			UpdateImdbPanel();
			UpdateSmileysPanel();
			UpdatePreviewPanel();
			StartProcessImage();
			StartProcessComments();
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
		}
	}

	private void InitializeInternalToolbarControls()
	{
		UpdateFavButton();
		ReportButton = Document.GetElementById("ReportButton");
		FavButton = Document.GetElementById("FavButton");
		NzbInfoButton = Document.GetElementById("NzbInfoButton");
		if (NzbInfoButton != null)
		{
			NzbInfoButton.Style = "visibility: hidden;";
		}
	}

	private void UpdateFavButton()
	{
		Document.GetElementById("FavButton")?.SetAttribute("className", Favorites.ContainsMessageId(SpotEx.MessageId) ? "favdel" : "favadd");
	}

	private void CommentBody_Input(object sender, HtmlElementEventArgs args)
	{
		_updateCommentPreviewTimer.Start();
	}

	private void OnDocumentUnloadingEvent()
	{
		try
		{
			_cancelGettingCommentsSource.Cancel();
			Sys.MainWindow.LocationChanged -= RedrawPopup;
			Sys.MainWindow.SizeChanged -= RedrawPopup;
			Sys.MainWindow.Activated -= MainWindowOnActivated;
			Sys.MainWindow.Deactivated -= MainWindowOnDeactivated;
			if (!_fileToRemoveOnClose.IsNullOrEmpty())
			{
				try
				{
					Toolbar.Dispose();
					System.IO.File.Delete(_fileToRemoveOnClose);
					FilesToRemoveOnClose.Remove(_fileToRemoveOnClose);
					return;
				}
				catch (Exception ex)
				{
					Log.Debug(ex.Message);
					return;
				}
			}
		}
		catch (Exception ex2)
		{
			Log.Exception(ex2, showToClient: true);
		}
	}

	private void OnNavigatingEvent(object o, WebBrowserNavigatingEventArgs e)
	{
		try
		{
			if (IsNotSpotTab)
			{
				return;
			}
			string text = e.Url.OriginalString.Trim();
			if (text.ToLower().Equals("about:blank") || text.ToLower().StartsWith("res:"))
			{
				return;
			}
			e.Cancel = true;
			Navigating = false;
			string text2 = text.ToLower();
			if (text2.StartsWith("link:") && !text2.StartsWith("link:spotnet://"))
			{
				string text3 = text.Substring(5);
				if (!text3.Equals("undefined"))
				{
					if (Settings.Default.ExternalBrowser)
					{
						AppHelper.LaunchInExternalProgram(text3);
					}
					else
					{
						Sys.MainWindow.OpenPage(PageTypeEnum.WebPage, text3, saveParrentTab: true).Forget();
					}
				}
			}
			else if (text2.StartsWith("query:"))
			{
				Sys.LeftPanel.SearchFilter(HttpUtility.UrlDecode(text.Substring(6).Split('_')[1]), HttpUtility.UrlDecode(text.Substring(6).Split('_')[0]));
			}
			else if (text2.StartsWith("menu:"))
			{
				if (GetMenuSenderInfo(text.Substring("menu:".Length), out var senderName, out var modulus))
				{
					CreateMenu(senderName, modulus);
				}
			}
			else if (text2.StartsWith("spotnet:reload") && !SpotEx.IsPreview)
			{
				string zErr = "";
				if (!StartUpdateComments(SpotEx.MessageId, ref zErr))
				{
					Log.Debug("StartUpdateComments failed: " + zErr);
					AppHelper.Error(zErr);
				}
			}
			else if (text2.StartsWith("loadimg://."))
			{
				_loadImageManually = true;
				StartProcessImage();
			}
			else if (text2.StartsWith("quote:"))
			{
				Quote(text);
			}
			else if (text2.StartsWith("reply:"))
			{
				Reply(text);
			}
			else if (text2.StartsWith("smiley:"))
			{
				string text4 = text.Substring("smiley:".Length);
				if (Regex.IsMatch(text4, "^[a-z]+$"))
				{
					Smiley(text4);
				}
			}
			else if (text2.StartsWith("ubb:"))
			{
				string text5 = text.Substring("ubb:".Length);
				if (Regex.IsMatch(text5, "^[biulc]$"))
				{
					CommentUbbTag(text5);
				}
			}
			else if (text2.StartsWith("show:"))
			{
				string text6 = text.Substring("show:".Length);
				if (text6.Equals("p"))
				{
					Settings.Default.CommentPreviewShow = !Settings.Default.CommentPreviewShow;
					Settings.Default.Save();
					UpdatePreviewPanel();
				}
				else if (text6.Equals("c"))
				{
					Settings.Default.CommentSmilesShow = !Settings.Default.CommentSmilesShow;
					Settings.Default.Save();
					UpdateSmileysPanel();
				}
				else if (text6.Equals("i"))
				{
					Settings.Default.SpotImdbShow = !Settings.Default.SpotImdbShow;
					Settings.Default.Save();
					UpdateImdbPanel();
				}
			}
			else if (text2.StartsWith("spotnet://") || text2.StartsWith("link:spotnet://"))
			{
				if (text2.StartsWith("link:"))
				{
					text = text.Substring("link:".Length);
				}
				Sys.MainWindow.ProcessSpotnetProtocol(text, saveParrentTab: true);
			}
			else if (text2.StartsWith("addtoblack:"))
			{
				if (GetMenuSenderInfo(text.Substring("addtoblack:".Length), out var senderName2, out var modulus2))
				{
					ReverseModulusBlackList(senderName2, modulus2);
				}
			}
			else if (text2.StartsWith("spamreports:"))
			{
				OpenSpamReportsPopup(text.Substring("spamreports:".Length));
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	private void OpenSpamReportsPopup(string messageId)
	{
		DispatcherHelper.CheckBeginInvokeOnUI(delegate
		{
			SpotMenu = new System.Windows.Controls.ContextMenu
			{
				FontFamily = Sys.MainWindow.FontFamily,
				FontSize = (double)System.Windows.Application.Current.Resources["ContextMenuFontSize"],
				FontStyle = Sys.MainWindow.FontStyle,
				Resources = AppHelper.GetMenuResourceDictionary
			};
			SpamReportsGrid spamReportsGrid = new SpamReportsGrid
			{
				MessageId = messageId
			};
			SpotMenu.Items.Add(spamReportsGrid);
			spamReportsGrid.Visibility = Visibility.Visible;
			SpotMenu.IsOpen = true;
		});
	}

	private void UpdatePreviewPanel()
	{
		if (PreviewPanel == null || CommentBody == null)
		{
			return;
		}
		if (Settings.Default.CommentPreviewShow)
		{
			string from = "";
			if (Nickname != null)
			{
				from = Nickname.GetAttribute("Value");
			}
			Comment comment = new Comment
			{
				Created = DateAndTime.Now,
				From = from,
				Body = CommentBody.InnerText,
				MessageId = "preview@spot.com",
				User = new UserInfo()
			};
			if (AppHelper.GetAvatar() != null)
			{
				comment.User.Avatar = Settings.Default.Avatar;
			}
			comment.User.Signature = comment.MessageId;
			comment.User.Modulus = UserKeyHelper.GetModulus();
			comment.User.ValidSignature = true;
			string text = GenerateCommentHtmlCode(comment, isVirtual: true, isPreview: true);
			string text2 = "<span class='Close' id='ClosePreview'>x</span>";
			text2 += text;
			PreviewPanel.InnerHtml = text2;
			PreviewPanel.Style = "";
			_closePreview = Document.GetElementById("ClosePreview");
			if (_closePreview != null)
			{
				_closePreview.Click += ClosePreview_Click;
			}
		}
		else
		{
			PreviewPanel.InnerHtml = $"<a href='show:p' class='fill-div' style='padding:8px 8px 10px 8px;'>{Words.SpotThemePreviewShow}</a>";
			PreviewPanel.Style = "padding:0 16px 0 0;";
		}
	}

	private void UpdateImdbPanel()
	{
		string text = AppHelper.HtmlEncode(AppHelper.CatDesc(SpotEx.Category, 0));
		if (SpotEx.Category == 1 && (SpotEx.SubCat == 12 || SpotEx.SubCat == 13))
		{
			text = AppHelper.HtmlEncode(AppHelper.CatDesc(SpotEx.Category, 5));
		}
		if (!text.Equals(Categories.CatFilms) && !text.Equals(Categories.CatSeries) && !text.Equals(Categories.CatMusic))
		{
			if (ImdbPanel != null)
			{
				ImdbPanel.Style = "display:none";
			}
			if (ImdbPanel2 != null)
			{
				ImdbPanel2.Style = "display:none";
			}
		}
		else if (Settings.Default.SpotImdbShow)
		{
			if (ImdbPanel != null)
			{
				ImdbPanel.Style = "display:true";
			}
			if (ImdbPanel2 != null)
			{
				ImdbPanel2.Style = "display:none";
			}
		}
		else
		{
			if (ImdbPanel != null)
			{
				ImdbPanel.Style = "display:none";
			}
			if (ImdbPanel2 != null)
			{
				ImdbPanel2.Style = "display:true";
			}
		}
	}

	private void ClosePreview_Click(object sender, HtmlElementEventArgs e)
	{
		try
		{
			if (!AskUnload)
			{
				Settings.Default.CommentPreviewShow = false;
				Settings.Default.Save();
				UpdatePreviewPanel();
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	private void CloseImdb_Click(object sender, HtmlElementEventArgs e)
	{
		try
		{
			if (!AskUnload)
			{
				Settings.Default.SpotImdbShow = false;
				Settings.Default.Save();
				UpdateImdbPanel();
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	private void AddButton_Click(object sender, HtmlElementEventArgs e)
	{
		try
		{
			if (!IsAddButtonDisabled && !SpotEx.IsPreview)
			{
				if (AppHelper.StripNonAlphaNumericCharacters(NewLateBinding.LateGet(CommentBody.DomElement, null, "Value", new object[0], null, null, null).ToStringSafely()).Trim().IsNullOrEmpty())
				{
					Interaction.MsgBox(Words.CannotPostEmptyMessage, MsgBoxStyle.Information, Words.Error);
					return;
				}
				if (AppHelper.StripNonAlphaNumericCharacters(NewLateBinding.LateGet(CommentBody.DomElement, null, "Value", new object[0], null, null, null).ToStringSafely()).EqualsIgnoreCase(AppHelper.StripNonAlphaNumericCharacters(_lastBody)))
				{
					Interaction.MsgBox(Words.CannotPostMessageTwice, MsgBoxStyle.Information, Words.Error);
					return;
				}
				if (DateAndTime.DateDiff("s", _lastTime, DateAndTime.Now) < 10)
				{
					Interaction.MsgBox(string.Format(Words.NeedToWaitUntilNewMessage, checked(10 - DateAndTime.DateDiff("s", _lastTime, DateAndTime.Now))), MsgBoxStyle.Information, Words.Error);
					return;
				}
				DisableAdd();
				Sys.MainWindow.DoWait(Words.Commenting);
				base.Dispatcher.BeginInvoke(new Action(DoComment), DispatcherPriority.Background);
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	private void DownloadButton_Click(object sender, HtmlElementEventArgs e)
	{
		try
		{
			if (!IsDownloadButtonDisabled && !SpotEx.IsPreview)
			{
				if (Settings.Default.DownloadAction <= 1)
				{
					Sys.MainWindow.TabControl1.SelectedIndex = 1;
				}
				DisableDownload();
				Task.Factory.StartNew(delegate
				{
					SpotHelper.DownloadNzbAndStartDownloadItem(SpotEx);
				}).ContinueWith(delegate
				{
					EnableDownload();
				}, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	private void SpotImage_Click(object sender, HtmlElementEventArgs e)
	{
		try
		{
			if (!AskUnload && _isImageResizeable)
			{
				ToggleImageSize();
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	private void ToggleImageSize()
	{
		_isImageFullSized = !_isImageFullSized;
		if (_isImageFullSized)
		{
			IHTMLImgElement obj = (IHTMLImgElement)SpotImage.DomElement;
			int height = obj.height;
			int width = obj.width;
			if (SpotImage.Parent != null)
			{
				string style = SpotImage.Parent.Style;
				if (style.IsNullOrWhiteSpace() || !style.Contains("min-height"))
				{
					SpotImage.Parent.Style = "min-height: " + height + "px;" + style;
				}
			}
			string text = SpotImage.GetAttribute("className").Replace(" full", "");
			SpotImage.SetAttribute("className", text + " full");
			int height2 = Brows.Height;
			int width2 = Brows.Width;
			if (1.0 * (double)height2 / (double)width2 > 1.0 * (double)height / (double)width)
			{
				SpotImage.Style = "min-width: 90%";
			}
			else
			{
				SpotImage.Style = "min-height: 90%";
			}
		}
		else
		{
			SpotImage.Style = "";
			string value = SpotImage.GetAttribute("className").Replace(" full", "");
			SpotImage.SetAttribute("className", value);
		}
	}

	private void CommentsDone(string sError)
	{
		try
		{
			if (AskUnload)
			{
				return;
			}
			string text = "<p><A onfocus='this.blur()' HREF='spotnet:reload'><IMG id='reload' onfocus='this.blur()' title='" + Words.Refresh + "' style='border: 0px; cursor:pointer; width: 32px; height:32px;' SRC=\"" + SpotParser.LocalFilePrefix + AppHelper.SettingsFolder + "/Images/refresh1.png\"></A>";
			if (_commentProgress == null)
			{
				return;
			}
			if (!sError.IsNullOrEmpty())
			{
				_commentProgress.InnerHtml = "<center>" + AppHelper.HtmlEncode(sError) + "<br></center>" + text;
				return;
			}
			string text2 = "";
			if (_commentIdCache.Count == 0)
			{
				if (!Settings.Default.ShowComments && !_commentsRefreshWasClickedAlready)
				{
					text2 = text2 + "<center>" + Words.CommentsNotRetrieved + "<br></center>";
					_commentsRefreshWasClickedAlready = true;
				}
				else
				{
					text2 = text2 + "<center>" + Words.CommentsNotFound + "<br></center>";
				}
			}
			if (!Settings.Default.LoadComments)
			{
				if (!text2.IsNullOrEmpty())
				{
					text2 += "<br>";
				}
				text2 = text2 + "<center><small>" + Words.CommentsUpdateDisabledWarning + "</small><br></center>";
			}
			else if (!SpotsListVm.IsSpotsDbUpToDate || !SpotsListVm.IsCommentsDbUpToDate)
			{
				if (!text2.IsNullOrEmpty())
				{
					text2 += "<br>";
				}
				text2 = text2 + "<center><small>" + Words.CommentsDbIsNotUpToDateWarning + "</small><br></center>";
			}
			_commentProgress.InnerHtml = text2 + text;
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	private void CreateMenu(string sFrom, string sModulus)
	{
		SpotMenu = new System.Windows.Controls.ContextMenu
		{
			FontFamily = Sys.MainWindow.FontFamily,
			FontSize = (double)System.Windows.Application.Current.Resources["ContextMenuFontSize"],
			FontStyle = Sys.MainWindow.FontStyle,
			Resources = AppHelper.GetMenuResourceDictionary
		};
		_menuFrom = sFrom;
		_menuModulus = sModulus;
		System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
		{
			Tag = "search",
			Header = Words.SearchByName,
			Icon = AppHelper.GetIcon("search"),
			IsEnabled = true
		};
		NewLateBinding.LateSetComplex(menuItem.Icon, null, "Opacity", (!menuItem.IsEnabled) ? new object[1] { 0.5 } : new object[1] { 1 }, null, null, OptimisticSet: false, RValueBase: true);
		SpotMenu.Items.Add(menuItem);
		System.Windows.Controls.MenuItem menuItem2 = new System.Windows.Controls.MenuItem
		{
			Tag = "searchid",
			Header = Words.SearchById,
			Icon = AppHelper.GetIcon("search"),
			IsEnabled = true
		};
		NewLateBinding.LateSetComplex(menuItem2.Icon, null, "Opacity", (!menuItem2.IsEnabled) ? new object[1] { 0.5 } : new object[1] { 1 }, null, null, OptimisticSet: false, RValueBase: true);
		SpotMenu.Items.Add(menuItem2);
		if (sModulus.EqualsIgnoreCase(UserKeyHelper.GetModulus()))
		{
			SpotMenu.Items.Add(new Separator());
			menuItem = new System.Windows.Controls.MenuItem
			{
				Tag = "ava",
				IsEnabled = true,
				Header = Words.ChangeAvatar,
				Icon = AppHelper.GetIcon("settings")
			};
			SpotMenu.Items.Add(menuItem);
		}
		else if (!sFrom.IsNullOrEmpty())
		{
			SpotMenu.Items.Add(new Separator());
			System.Windows.Controls.MenuItem menuItem3 = new System.Windows.Controls.MenuItem
			{
				Tag = "fav",
				IsEnabled = (!string.IsNullOrEmpty(sModulus) && !BlackAndWhite.BlackList().Contains(sModulus)),
				Header = (BlackAndWhite.WhiteList().Contains(sModulus) ? Words.WhiteListRemoveFrom : Words.WhiteListAddTo),
				Icon = AppHelper.GetIcon("favorite")
			};
			NewLateBinding.LateSetComplex(menuItem3.Icon, null, "Opacity", (!menuItem3.IsEnabled) ? new object[1] { 0.5 } : new object[1] { 1 }, null, null, OptimisticSet: false, RValueBase: true);
			SpotMenu.Items.Add(menuItem3);
			System.Windows.Controls.MenuItem menuItem4 = new System.Windows.Controls.MenuItem
			{
				Tag = "black",
				IsEnabled = (!sModulus.IsNullOrEmpty() & !BlackAndWhite.WhiteList().Contains(sModulus) & !sModulus.EqualsIgnoreCase(UserKeyHelper.GetModulus())),
				Header = (BlackAndWhite.BlackList().Contains(sModulus) ? Words.BlackListRemoveFrom : Words.BlackListAddTo),
				Icon = AppHelper.GetIcon("trash")
			};
			NewLateBinding.LateSetComplex(menuItem4.Icon, null, "Opacity", (!menuItem4.IsEnabled) ? new object[1] { 0.5 } : new object[1] { 1 }, null, null, OptimisticSet: false, RValueBase: true);
			SpotMenu.Items.Add(menuItem4);
		}
		SpotMenu.IsOpen = true;
		SpotMenu.PreviewMouseUp += SpotMenu_PreviewMouseUp;
	}

	private void CreateCopySelectionMenu()
	{
		SpotMenu = new System.Windows.Controls.ContextMenu
		{
			FontFamily = Sys.MainWindow.FontFamily,
			FontSize = (double)System.Windows.Application.Current.Resources["ContextMenuFontSize"],
			FontStyle = Sys.MainWindow.FontStyle,
			Resources = AppHelper.GetMenuResourceDictionary
		};
		System.Windows.Controls.Image icon;
		try
		{
			icon = new System.Windows.Controls.Image
			{
				Source = new BitmapImage(new Uri("pack://application:,,,/Spotnet;component/Resources/ImagesInternal/copy.png", UriKind.Absolute)),
				Width = 16.0,
				Height = 16.0
			};
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
			icon = null;
		}
		System.Windows.Controls.MenuItem newItem = new System.Windows.Controls.MenuItem
		{
			Tag = "copy",
			IsEnabled = true,
			Header = Words.Copy,
			Icon = icon
		};
		SpotMenu.Items.Add(newItem);
		SpotMenu.IsOpen = true;
		SpotMenu.PreviewMouseUp += SpotMenu_PreviewMouseUp;
	}

	private void DisableAdd()
	{
		if (!AskUnload && !(AddButton == null))
		{
			string text = AddButton.GetAttribute("className").Replace(" enabled", "").Replace(" disabled", "");
			AddButton.SetAttribute("className", text + " disabled");
			AddButton.SetAttribute("title", "");
		}
	}

	private void DisableDownload()
	{
		if (!AskUnload && !(DownloadButton == null))
		{
			string text = DownloadButton.GetAttribute("className").Replace(" enabled", "").Replace(" disabled", "");
			DownloadButton.SetAttribute("className", text + " disabled");
			DownloadButton.SetAttribute("title", "");
		}
	}

	private void DoComment()
	{
		string zErr = "";
		try
		{
			string text = AppHelper.CreateMsgId(SpotEx.MessageId.Split('@')[0].Replace(".", "").Replace("<", ""));
			Settings.Default.Nickname = AppHelper.StripNonAlphaNumericCharacters(NewLateBinding.LateGet(Nickname.DomElement, null, "Value", new object[0], null, null, null).ToStringSafely());
			Settings.Default.Save();
			if (Spots.CreateComment(AppHelper.UploadPhuse, NewLateBinding.LateGet(Nickname.DomElement, null, "Value", new object[0], null, null, null).ToStringSafely(), NewLateBinding.LateGet(CommentBody.DomElement, null, "Value", new object[0], null, null, null).ToStringSafely(), Settings.Default.ReplyGroup, SpotEx.MessageId, SpotEx.Title, AppHelper.GetAvatar(), UserKeyHelper.GetKey(), text, ref zErr))
			{
				_lastBody = NewLateBinding.LateGet(CommentBody.DomElement, null, "Value", new object[0], null, null, null).ToStringSafely();
				_lastTime = DateAndTime.Now;
				NewLateBinding.LateSetComplex(CommentBody.DomElement, null, "Value", new object[1] { "" }, null, null, OptimisticSet: false, RValueBase: true);
				Comment comment = new Comment
				{
					Created = DateAndTime.Now,
					From = Settings.Default.Nickname,
					Body = _lastBody,
					MessageId = SpotHelper.MakeMsg(text, tag: false),
					User = new UserInfo()
				};
				if (AppHelper.GetAvatar() != null)
				{
					comment.User.Avatar = Settings.Default.Avatar;
				}
				comment.User.Signature = comment.MessageId;
				comment.User.Modulus = UserKeyHelper.GetModulus();
				comment.User.ValidSignature = true;
				ShowNewComment(comment, bVirtual: true);
				_messagesFromUserAndAlreadyShown.Add(SpotHelper.MakeMsg(text));
				if (!MessagesFromUserToShowOnNextTabOpen.ContainsKey(SpotEx.MessageId))
				{
					MessagesFromUserToShowOnNextTabOpen.Add(SpotEx.MessageId, new List<Comment>());
				}
				MessagesFromUserToShowOnNextTabOpen[SpotEx.MessageId].Add(comment);
				AppHelper.ShowPopupMessage(Words.CommentPosted);
			}
			else
			{
				Log.Error(zErr);
				AppHelper.Error(zErr);
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
		finally
		{
			Sys.MainWindow.EndWait();
			EnableAdd();
		}
	}

	private void StartProcessComments()
	{
		if (AskUnload)
		{
			return;
		}
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			string zErr = "";
			try
			{
				if (!Settings.Default.ShowComments || SpotEx.IsPreview)
				{
					CommentsDone("");
				}
				else if (!StartUpdateComments(SpotEx.MessageId, ref zErr))
				{
					CommentsDone(zErr);
				}
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
				CommentsDone("StartProcessComments: " + ex.Message);
			}
		}, DispatcherPriority.Background);
	}

	private void EnableAdd()
	{
		if (!AskUnload && !(AddButton == null))
		{
			string text = AddButton.GetAttribute("className").Replace(" enabled", "").Replace(" disabled", "");
			AddButton.SetAttribute("title", Words.Send);
			AddButton.SetAttribute("className", text + " enabled");
		}
	}

	private void EnableDownload()
	{
		if (!AskUnload && !(DownloadButton == null))
		{
			string text = DownloadButton.GetAttribute("className").Replace(" enabled", "").Replace(" disabled", "");
			DownloadButton.SetAttribute("className", text + " enabled");
			DownloadButton.SetAttribute("title", Words.Download);
			Sys.MainWindow.EndWait();
		}
	}

	private void GetCommentsFromDb(ISqlDb db, string xMsg)
	{
		string text = SpotHelper.MakeMsg(xMsg, tag: false);
		int length = text.IndexOf("@", StringComparison.Ordinal);
		string text2 = text.Substring(0, length);
		_fetchedCache.Clear();
		using (ISqlDbTransaction transaction = db.BeginReadTransaction())
		{
			DbCommand dbCommand = db.CreateCommand(transaction);
			dbCommand.CommandText = "SELECT docid FROM comments WHERE spot MATCH '" + text2.Replace("'", "") + "' ORDER BY docid ASC";
			using DbDataReader dbDataReader = db.ExecuteReader(dbCommand);
			if (dbDataReader == null)
			{
				Log.Debug("Failed to access comments db");
				return;
			}
			while (dbDataReader.Read())
			{
				if (!Information.IsDBNull(RuntimeHelpers.GetObjectValue(dbDataReader[0])))
				{
					long num = Conversions.ToLong(dbDataReader[0]);
					if (num > 0 && !_fetchedCache.Contains(num))
					{
						_fetchedCache.Add(num);
					}
				}
			}
		}
		Log.Debug("Comments for {0} loaded. Count: {1}", text, _fetchedCache.Count);
	}

	private void ShowNewComment(Comment comment, bool bVirtual)
	{
		string text = GenerateCommentHtmlCode(comment, bVirtual, isPreview: false);
		if (text == null)
		{
			return;
		}
		HtmlElement elementById = Document.GetElementById("Comments");
		if (elementById == null)
		{
			return;
		}
		elementById.InnerHtml += text;
		if (comment.Article == 0L)
		{
			return;
		}
		_commentIdCache.Add(comment.Article);
		if (!MessagesFromUserToShowOnNextTabOpen.ContainsKey(SpotEx.MessageId))
		{
			return;
		}
		foreach (Comment item in MessagesFromUserToShowOnNextTabOpen[SpotEx.MessageId])
		{
			if (item.MessageId == SpotHelper.MakeMsg(comment.MessageId, tag: false))
			{
				item.Article = comment.Article;
			}
		}
	}

	public string GenerateCommentHtmlCode(Comment comment, bool isVirtual, bool isPreview)
	{
		try
		{
			string sClass = "comment";
			if (!isPreview)
			{
				if (AskUnload)
				{
					return null;
				}
				if (comment.MessageId.IsNullOrEmpty())
				{
					Log.Warn("Comment message ID is null or empty");
					return null;
				}
				if (comment.Article != 0L && _commentIdCache.Contains(comment.Article))
				{
					return null;
				}
				if (_messagesFromUserAndAlreadyShown.Contains(SpotHelper.MakeMsg(comment.MessageId)))
				{
					return null;
				}
				if (BlackAndWhite.BlackList().Contains(comment.User.Modulus))
				{
					if (comment.Article != 0L)
					{
						_commentIdCache.Add(comment.Article);
						Log.Debug("User is in black list: " + comment.User.Modulus + ". Skip the comment: " + comment.MessageId);
					}
					else
					{
						Log.Warn("Comment ID is zero: " + comment.User.Modulus);
					}
					return null;
				}
				comment.RemoveAvastMessageFromBody();
				comment.RemovePromoteSpotnetMessageFromBody();
				if (Settings.Default.HideCommentsWithLinks && SpotEx.User.Modulus != comment.User.Modulus && comment.HasLinks() && !IsItMyComment(comment))
				{
					Log.Debug("Comment has links and ignored: " + comment.MessageId);
					return null;
				}
				if (!_uniqueCache.ContainsKey(SpotEx.Poster.ToUpper()))
				{
					_uniqueCache.Add(SpotEx.Poster.ToUpper(), SpotEx.User.Modulus);
				}
			}
			comment.From = AppHelper.StripNonAlphaNumericCharacters(comment.From);
			string text;
			if (isVirtual)
			{
				text = AppHelper.HtmlEncode(AppHelper.MakeUnique(UserKeyHelper.GetModulus()));
			}
			else if (comment.User.Modulus.IsNullOrEmpty() || !comment.User.ValidSignature)
			{
				text = Words.Unknown;
				if (!comment.User.Organisation.IsNullOrEmpty())
				{
					text = text + "\r\n" + AppHelper.HtmlEncode(comment.User.Organisation);
				}
				if (comment.User.Trace.Length > 3)
				{
					text = text + "\r\n" + AppHelper.HtmlEncode(comment.User.Trace);
				}
			}
			else
			{
				if (!_uniqueCache.ContainsKey(comment.From.ToUpper()))
				{
					_uniqueCache.Add(comment.From.ToUpper(), comment.User.Modulus);
				}
				text = AppHelper.HtmlEncode(AppHelper.MakeUnique(comment.User.Modulus));
				if (!comment.User.Organisation.IsNullOrEmpty())
				{
					text = text + "\r\n" + AppHelper.HtmlEncode(comment.User.Organisation);
				}
				if (IsItMyComment(comment) || BlackAndWhite.WhiteList().Contains(comment.User.Modulus))
				{
					sClass = "trusted";
				}
				else if (comment.User.Modulus.EqualsIgnoreCase(SpotEx.User.Modulus))
				{
					sClass = "author";
				}
				else
				{
					if (comment.User.Trace.Length > 3)
					{
						text = text + "\r\n" + AppHelper.HtmlEncode(comment.User.Trace);
					}
					if (!_uniqueCache[comment.From.ToUpper()].EqualsIgnoreCase(comment.User.Modulus))
					{
						comment.From = comment.From.Trim() + " (2)";
					}
				}
			}
			string text2 = SpotParser.ParseComment(comment, sClass, text, isPreview);
			return "<SPAN style='visibility:visible'>" + text2 + "</SPAN>";
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
			return null;
		}
	}

	private bool IsItMyComment(Comment comment)
	{
		return comment.User.Modulus.Equals(UserKeyHelper.GetModulus());
	}

	internal static bool ChangeAvatar(out string newAvatar)
	{
		newAvatar = "";
		try
		{
			string initialDirectory = (Settings.Default.AvatarFolder.IsNullOrEmpty() ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) : Settings.Default.AvatarFolder);
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = Words.ChangeAvatar,
				InitialDirectory = initialDirectory,
				Filter = Words.FilterToAvatar,
				FilterIndex = 1,
				RestoreDirectory = true,
				CheckFileExists = true,
				ShowReadOnly = false,
				DefaultExt = "gif",
				Multiselect = false
			};
			if (openFileDialog.ShowDialog() != DialogResult.OK)
			{
				return false;
			}
			Bitmap bitmap = new Bitmap(openFileDialog.FileName);
			if (bitmap.Width > 32 || bitmap.Height > 32)
			{
				bitmap = bitmap.Resize(32, 32);
			}
			newAvatar = Convert.ToBase64String(bitmap.ToByteArray());
			Settings.Default.AvatarFolder = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
			Settings.Default.Save();
			return true;
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
		}
		return false;
	}

	private string ShowComments()
	{
		try
		{
			if (AskUnload)
			{
				return "";
			}
			List<long> list = new List<long>();
			if (_fetchedCache != null)
			{
				list = _fetchedCache.Where((long e) => !_commentIdCache.Contains(e)).ToList();
			}
			if (list.Any())
			{
				Action<Comment> onNewComment = delegate(Comment c)
				{
					ShowNewComment(c, bVirtual: false);
				};
				Task task = Comments.StartLoadCommentsBody(AppHelper.HeaderPhuse, list, Sys.MainWindow.CommentSettings(bIncludeLast: false), ProgressChanged, onNewComment, _cancelGettingCommentsSource.Token);
				if (task != null)
				{
					string returnStr = "";
					task.ContinueWith(delegate(Task t)
					{
						try
						{
							ShowCommentsFromUserPostedBefore();
							if (t.IsFaulted)
							{
								returnStr = t.Exception?.TheMostInnerException().Message ?? "Error on getting comments";
							}
						}
						catch (Exception e2)
						{
							returnStr = e2.TheMostInnerException().Message;
						}
					}).Wait();
					return returnStr;
				}
			}
			else
			{
				ShowCommentsFromUserPostedBefore();
				Thread.Sleep(500);
			}
			return "";
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
			return "ShowComments: " + ex.Message;
		}
	}

	private void ShowCommentsFromUserPostedBefore()
	{
		if (!MessagesFromUserToShowOnNextTabOpen.ContainsKey(SpotEx.MessageId))
		{
			return;
		}
		foreach (Comment item in MessagesFromUserToShowOnNextTabOpen[SpotEx.MessageId])
		{
			ShowNewComment(item, bVirtual: false);
			_messagesFromUserAndAlreadyShown.Add(SpotHelper.MakeMsg(item.MessageId));
		}
	}

	private bool StartUpdateComments(string sMsgId, ref string zErr)
	{
		try
		{
			if (AskUnload)
			{
				zErr = "Exiting";
				return false;
			}
			if (_commentProgress == null)
			{
				zErr = "Comment progress is null";
				return false;
			}
			if (!_commentProgress.InnerHtml.EqualsIgnoreCase(_commentProgressCache))
			{
				_commentProgress.InnerHtml = _commentProgressCache;
			}
			_commentsStatus = Document.GetElementById("CommentsStatus");
			ProgressChanged(Words.CommentsLoading + "...", -1);
			Task.Factory.StartNew(delegate
			{
				UpdateComments(sMsgId);
			});
			return true;
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
			return false;
		}
	}

	private void UpdateComments(string sMsgId)
	{
		try
		{
			if (AskUnload)
			{
				return;
			}
			Task task = null;
			if (!DbUpdater.IsDbUpdateInProgress)
			{
				task = Sys.MainWindow.ScheduleCommentsDbUpdate();
			}
			using (ISqlDb db = SqlDbFactory.CreateSqlDbComments(isReadOnly: true))
			{
				GetCommentsFromDb(db, sMsgId);
			}
			string sError = ShowComments();
			if (task != null && !task.IsCompleted)
			{
				task.Wait(TimeSpan.FromSeconds(5.0));
				using (ISqlDb db2 = SqlDbFactory.CreateSqlDbComments(isReadOnly: true))
				{
					GetCommentsFromDb(db2, sMsgId);
				}
				sError = ShowComments();
			}
			CommentsDone(sError);
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
			CommentsDone("UpdateComments: " + ex.Message);
		}
	}

	private void SpotMenu_PreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		try
		{
			if (!(e?.Source is System.Windows.Controls.MenuItem { Tag: not null } menuItem))
			{
				return;
			}
			string text = menuItem.Tag.ToString().ToLower();
			if (string.Equals(text, "fav", StringComparison.OrdinalIgnoreCase))
			{
				ReverseModulusWhiteList(_menuFrom, _menuModulus);
			}
			else if (text.EqualsIgnoreCase("black"))
			{
				ReverseModulusBlackList(_menuFrom, _menuModulus);
			}
			else if (text.EqualsIgnoreCase("search"))
			{
				string zQuery = "sender MATCH '" + _menuFrom.ToLower() + "'";
				string menuFrom = _menuFrom;
				Sys.LeftPanel.SearchFilter(zQuery, menuFrom);
			}
			else if (text.EqualsIgnoreCase("searchid"))
			{
				string zQuery2 = "modulus LIKE '" + _menuModulus + "'";
				string sName = _menuFrom + " (" + AppHelper.MakeUnique(_menuModulus) + ")";
				Sys.LeftPanel.SearchFilter(zQuery2, sName);
			}
			else if (text.EqualsIgnoreCase("ava"))
			{
				if (ChangeAvatar(out var newAvatar) && !newAvatar.IsNullOrEmpty())
				{
					Settings.Default.Avatar = newAvatar;
					Settings.Default.Save();
					AppHelper.ShowPopupMessage(Words.AvatarChangedForFuturePosts);
					_updateCommentPreviewTimer.Start();
				}
			}
			else if (text.EqualsIgnoreCase("copy"))
			{
				Document.ExecCommand("copy", showUI: false, null);
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	private void _document_ContextMenuShowing(object sender, HtmlElementEventArgs e)
	{
		if (IsNotSpotTab || Document.ActiveElement == null)
		{
			return;
		}
		try
		{
			string text = Document.ActiveElement.TagName.ToUpper();
			if (text.Equals("TEXTAREA") || text.Equals("INPUT"))
			{
				return;
			}
			HtmlElement elementFromPoint = _document.GetElementFromPoint(e.ClientMousePosition);
			if ((object)elementFromPoint != null && elementFromPoint.Parent?.TagName != null && elementFromPoint.Parent.TagName.ToUpper().Equals("A") && elementFromPoint.Parent.DomElement is HTMLAnchorElement { href: var href } && href.ToLower().StartsWith("menu:"))
			{
				if (GetMenuSenderInfo(href.ToLower().Substring("menu:".Length), out var senderName, out var modulus))
				{
					CreateMenu(senderName, modulus);
				}
				e.BubbleEvent = false;
				e.ReturnValue = false;
				return;
			}
			e.BubbleEvent = false;
			e.ReturnValue = false;
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
			e.BubbleEvent = false;
			e.ReturnValue = false;
		}
		try
		{
			((CloseableTabItem)base.Parent).CloseMe();
		}
		catch (Exception ex2)
		{
			Log.Exception(ex2);
		}
	}

	private void RedrawPopup()
	{
		double horizontalOffset = ToolbarPopup.HorizontalOffset;
		if (!ToolbarPopup.IsOpen && !SpotEx.IsPreview)
		{
			ToolbarPopup.IsOpen = true;
		}
		ToolbarPopup.HorizontalOffset = horizontalOffset + 1.0;
		ToolbarPopup.HorizontalOffset = horizontalOffset;
	}

	private bool GetMenuSenderInfo(string href, out string senderName, out string modulus)
	{
		senderName = null;
		modulus = null;
		if (href.Contains("'"))
		{
			return false;
		}
		ParseMenuLink(href, out senderName, out modulus);
		return !modulus.IsNullOrWhiteSpace();
	}

	private void _document_MouseUp(object sender, HtmlElementEventArgs e)
	{
		if (IsNotSpotTab || AskUnload)
		{
			return;
		}
		try
		{
			if (e.MouseButtonsPressed != MouseButtons.Left)
			{
				return;
			}
			HtmlElement activeElement = Document.ActiveElement;
			if (activeElement != null)
			{
				if (activeElement.OuterHtml.StartsWith("<div class=\"toolbar\">"))
				{
					return;
				}
				string text = activeElement.TagName.ToUpper();
				if (text.Equals("TEXTAREA") || text.Equals("INPUT"))
				{
					return;
				}
			}
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				if (!GetSelectedText().IsNullOrEmpty())
				{
					CreateCopySelectionMenu();
				}
			}, DispatcherPriority.Background);
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
		}
	}

	private void _document_MouseDown(object sender, HtmlElementEventArgs e)
	{
		if (IsNotSpotTab || AskUnload)
		{
			return;
		}
		HtmlElement activeElement = Document.ActiveElement;
		if (activeElement != null)
		{
			string text = activeElement.TagName.ToUpper();
			if (text.Equals("TEXTAREA") || text.Equals("INPUT"))
			{
				return;
			}
		}
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			try
			{
				if (!GetSelectedText().IsNullOrEmpty())
				{
					Document.ExecCommand("Unselect", showUI: false, Type.Missing);
				}
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
			}
		}, DispatcherPriority.Background);
	}

	private void ToolbarPositioningSetup()
	{
		Popup toolbarPopup = ToolbarPopup;
		toolbarPopup.CustomPopupPlacementCallback = (CustomPopupPlacementCallback)Delegate.Combine(toolbarPopup.CustomPopupPlacementCallback, (CustomPopupPlacementCallback)((System.Windows.Size popupSize, System.Windows.Size targetSize, System.Windows.Point offset) => new CustomPopupPlacement[1]
		{
			new CustomPopupPlacement
			{
				Point = new System.Windows.Point(targetSize.Width - popupSize.Width - 25.0, 10.0)
			}
		}));
		if (Brows.Document != null)
		{
			Brows.Document.Focusing += RedrawPopup;
		}
		Sys.MainWindow.LocationChanged += RedrawPopup;
		Sys.MainWindow.SizeChanged += RedrawPopup;
		Sys.MainWindow.Activated += MainWindowOnActivated;
		Sys.MainWindow.Deactivated += MainWindowOnDeactivated;
	}

	private void MainWindowOnDeactivated(object sender, EventArgs eventArgs)
	{
		if (ToolbarPopup.IsOpen)
		{
			ToolbarPopup.IsOpen = false;
			_showOnActivated = true;
		}
	}

	private void MainWindowOnActivated(object sender, EventArgs eventArgs)
	{
		if (_showOnActivated)
		{
			ToolbarPopup.IsOpen = !SpotEx.IsPreview;
			_showOnActivated = false;
		}
	}

	private void RedrawPopup(object sender, EventArgs eventArgs)
	{
		RedrawPopup();
	}

	private void AddSpotWarning()
	{
		if (SpotEx.PosterIdent == PosterIdentType.Black)
		{
			Warning.Text = Words.SenderIsInBlackList;
			Warning.Visibility = Visibility.Visible;
		}
		else if (SpotEx.PosterIdent == PosterIdentType.SpotBlack)
		{
			Warning.Text = Words.SpotIsInBlackList;
			Warning.Visibility = Visibility.Visible;
		}
		else if (SpotEx.PosterIdent == PosterIdentType.Fake)
		{
			Warning.Text = Words.SenderIdIsFake;
			Warning.Visibility = Visibility.Visible;
		}
		if (Warning.Visibility == Visibility.Visible)
		{
			Warning.IsVisibleChanged += delegate
			{
				FocusDocument();
			};
		}
	}

	private void ReverseModulusBlackList(string username, string modulus)
	{
		if (!modulus.IsNullOrEmpty() && !BlackAndWhite.WhiteList().Contains(modulus))
		{
			if (BlackAndWhite.BlackList().Contains(modulus))
			{
				CommentAuthorRemoveFromBlacklist(modulus);
			}
			else
			{
				CommentAuthorAddToBlacklist(username, modulus);
			}
		}
	}

	private void ReverseModulusWhiteList(string username = null, string modulus = null)
	{
		if (!modulus.IsNullOrEmpty() && !BlackAndWhite.BlackList().Contains(modulus))
		{
			if (BlackAndWhite.WhiteList().Contains(modulus))
			{
				CommentAuthorRemoveFromWhitelist(modulus);
			}
			else
			{
				CommentAuthorAddToWhitelist(username, modulus);
			}
		}
	}

	private void CommentAuthorRemoveFromBlacklist(string modulus)
	{
		BlackAndWhite.RemoveBlack(modulus);
		UpdateSpotPosterStatus(modulus);
		AppHelper.ShowPopupMessage(Words.BlackListYouWillReceiveFromSender, inTheCenter: false, TimeSpan.FromSeconds(3.0));
		IEnumerable<CommentElements> commentElements = GetCommentElements(modulus);
		if (commentElements == null)
		{
			return;
		}
		string value = (modulus.EqualsIgnoreCase(SpotEx.User.Modulus) ? "author" : "comment");
		foreach (CommentElements item in commentElements)
		{
			if (item.FromLink != null)
			{
				item.FromLink.SetAttribute("className", value);
			}
			if (item.AddToBlackLink != null)
			{
				item.AddToBlackLink.InnerText = Words.BlackListAddTo;
			}
			if (item.Avatar != null)
			{
				item.Avatar.Style = "display:true";
			}
			if (item.Desc != null)
			{
				item.Desc.Style = "display:true";
			}
		}
	}

	private void CommentAuthorAddToBlacklist(string username, string modulus)
	{
		BlackAndWhite.AddBlack(AppHelper.StripNonAlphaNumericCharacters(username), modulus);
		UpdateSpotPosterStatus(modulus);
		AppHelper.ShowPopupMessage(Words.BlackListYouWillNotReceiveFromSender, inTheCenter: false, TimeSpan.FromSeconds(3.0));
		IEnumerable<CommentElements> commentElements = GetCommentElements(modulus);
		if (commentElements == null)
		{
			return;
		}
		string value = "untrusted";
		foreach (CommentElements item in commentElements)
		{
			if (item.FromLink != null)
			{
				item.FromLink.SetAttribute("className", value);
			}
			if (item.AddToBlackLink != null)
			{
				item.AddToBlackLink.InnerText = Words.BlackListRemoveFrom;
			}
			if (item.Avatar != null)
			{
				item.Avatar.Style = "display:none";
			}
			if (item.Desc != null)
			{
				item.Desc.Style = "display:none";
			}
		}
	}

	private void CommentAuthorRemoveFromWhitelist(string modulus)
	{
		BlackAndWhite.RemoveWhite(modulus);
		UpdateSpotPosterStatus(modulus);
		IEnumerable<CommentElements> commentElements = GetCommentElements(modulus);
		if (commentElements == null)
		{
			return;
		}
		string value = (modulus.EqualsIgnoreCase(SpotEx.User.Modulus) ? "author" : "comment");
		foreach (CommentElements item in commentElements)
		{
			if (item.FromLink != null)
			{
				item.FromLink.SetAttribute("className", value);
			}
			if (item.AddToBlackLink != null)
			{
				item.AddToBlackLink.Style = "display:true";
			}
		}
	}

	private void CommentAuthorAddToWhitelist(string username, string modulus)
	{
		BlackAndWhite.AddWhite(AppHelper.StripNonAlphaNumericCharacters(username), modulus);
		UpdateSpotPosterStatus(modulus);
		IEnumerable<CommentElements> commentElements = GetCommentElements(modulus);
		if (commentElements == null)
		{
			return;
		}
		string value = "trusted";
		foreach (CommentElements item in commentElements)
		{
			if (item.FromLink != null)
			{
				item.FromLink.SetAttribute("className", value);
			}
			if (item.AddToBlackLink != null)
			{
				item.AddToBlackLink.Style = "display:none";
			}
		}
	}

	private IEnumerable<CommentElements> GetCommentElements(string modulus)
	{
		HtmlElement elementById = Document.GetElementById("Comments");
		if (elementById == null)
		{
			yield break;
		}
		foreach (HtmlElement item in elementById.GetElementsByTagName("a"))
		{
			if (!item.GetAttribute("href").StartsWith("menu:" + modulus))
			{
				continue;
			}
			HtmlElement htmlElement2 = item;
			while (htmlElement2 != null && !htmlElement2.TagName.ToLower().Equals("table") && !htmlElement2.Name.ToLower().Equals("header"))
			{
				htmlElement2 = htmlElement2.Parent;
			}
			if (htmlElement2 != null && htmlElement2.Name.ToLower().Equals("header"))
			{
				htmlElement2 = htmlElement2.Parent;
			}
			if (!(htmlElement2 == null))
			{
				HtmlElement desc = null;
				if (FindChildByHref(htmlElement2, "reply:") != null)
				{
					string text = FindChildByHref(htmlElement2, "reply:").GetAttribute("href").Substring("reply:".Length + 1);
					desc = Document.GetElementById("d" + text);
				}
				HtmlElement avatar = htmlElement2.GetElementsByTagName("img").Cast<HtmlElement>().FirstOrDefault(delegate(HtmlElement i)
				{
					string attribute = i.GetAttribute("src");
					return attribute.StartsWith("http://www.gravatar.com/avatar/") || attribute.StartsWith("data:image/");
				});
				yield return new CommentElements
				{
					AddToBlackLink = FindChildByHref(htmlElement2, "addtoblack:"),
					FromLink = item,
					Desc = desc,
					Avatar = avatar
				};
			}
		}
	}

	private void UpdateSpotPosterStatus(string modulus)
	{
		if (modulus.EqualsIgnoreCase(SpotEx.User.Modulus))
		{
			SpotEx.PosterIdent = PosterIdentType.Unspecified;
			HtmlElement elementById = Document.GetElementById("PosterIdentLinks");
			if (elementById != null)
			{
				elementById.InnerHtml = SpotParser.GeneratePosterLinksHtmlCode(SpotEx.Poster, SpotEx.User, SpotEx.PosterIdent);
			}
			HtmlElement elementById2 = Document.GetElementById("PosterIdentLabel");
			if (elementById2 != null)
			{
				elementById2.InnerHtml = SpotParser.GeneratePosterIdentLabelHtmlCode(SpotEx.PosterIdent);
			}
		}
	}

	private void CommentUbbTag(string tag)
	{
		HtmlElement elementById = Document.GetElementById("CommentBody");
		if (elementById == null)
		{
			return;
		}
		elementById.Focus();
		string selectedText = GetSelectedText();
		string value = "";
		if (tag.Equals("l"))
		{
			value = "[url=\"spotnet://MSGID\"]" + selectedText + "[/url]";
		}
		else if (tag.Equals("c"))
		{
			HtmlElement elementById2 = Document.GetElementById("ColorInput");
			if (elementById2 != null)
			{
				string attribute = elementById2.GetAttribute("Value");
				value = "[color=#" + attribute + "]" + selectedText + "[/color]";
			}
		}
		else
		{
			value = string.Format("[{0}]{1}[/{0}]", tag, selectedText);
		}
		Document.ExecCommand("paste", showUI: false, value);
		_updateCommentPreviewTimer.Start();
	}

	private string GetSelectedText()
	{
		return (((Document.DomDocument as IHTMLDocument2)?.selection)?.createRange() as IHTMLTxtRange)?.text ?? "";
	}

	private void Smiley(string smiley)
	{
		if (!(CommentBody == null))
		{
			CommentBody.Focus();
			RunPageJavascript("smiley", new object[1] { smiley });
			_updateCommentPreviewTimer.Start();
		}
	}

	private object RunPageJavascript(string methodName, object[] args)
	{
		return Document.InvokeScript(methodName, args);
	}

	private void Quote(string link)
	{
		if (CommentBody == null)
		{
			return;
		}
		HtmlElement elementById = Document.GetElementById("Comments");
		if (elementById == null)
		{
			return;
		}
		HtmlElement htmlElement = FindChildByHref(elementById, link);
		while (htmlElement != null && !htmlElement.TagName.ToLower().Equals("table") && !htmlElement.Name.ToLower().Equals("header"))
		{
			htmlElement = htmlElement.Parent;
		}
		if (htmlElement != null && htmlElement.Name.ToLower().Equals("header"))
		{
			htmlElement = htmlElement.Parent;
		}
		HtmlElement htmlElement2 = FindChildByHref(htmlElement, "menu:");
		if (GetMenuSenderInfo(htmlElement2.GetAttribute("href"), out var senderName, out var _))
		{
			string text = "";
			string text2 = link.Substring("quote:".Length + 1);
			HtmlElement elementById2 = Document.GetElementById("d" + text2);
			if (elementById2 != null)
			{
				text = elementById2.InnerHtml;
			}
			if (Document.Body != null)
			{
				Document.Body.ScrollIntoView(alignWithTop: false);
				CommentBody.Focus();
			}
			object value = GenerateQuote(text, senderName);
			Document.ExecCommand("paste", showUI: false, value);
			_updateCommentPreviewTimer.Start();
		}
	}

	private void Reply(string link)
	{
		if (CommentBody == null)
		{
			return;
		}
		HtmlElement elementById = Document.GetElementById("Comments");
		if (elementById == null)
		{
			return;
		}
		HtmlElement htmlElement = FindChildByHref(elementById, link);
		while (htmlElement != null && !htmlElement.TagName.ToLower().Equals("table") && !htmlElement.Name.ToLower().Equals("header"))
		{
			htmlElement = htmlElement.Parent;
		}
		if (htmlElement != null && htmlElement.Name.ToLower().Equals("header"))
		{
			htmlElement = htmlElement.Parent;
		}
		HtmlElement htmlElement2 = FindChildByHref(htmlElement, "menu:");
		if (GetMenuSenderInfo(htmlElement2.GetAttribute("href"), out var senderName, out var _))
		{
			if (Document.Body != null)
			{
				Document.Body.ScrollIntoView(alignWithTop: false);
				CommentBody.Focus();
			}
			string value = GenerateReply(senderName);
			Document.ExecCommand("paste", showUI: false, value);
			_updateCommentPreviewTimer.Start();
		}
	}

	private void ParseMenuLink(string link, out string poster, out string modulus)
	{
		poster = null;
		modulus = null;
		string[] array = link.Split('_');
		if (array.Length == 2)
		{
			modulus = array[0];
			poster = array[1];
		}
		else if (array.Length == 3)
		{
			modulus = array[0];
			poster = array[2];
		}
	}

	private HtmlElement FindChildByHref(HtmlElement element, string href)
	{
		if (element == null)
		{
			return null;
		}
		return element.GetElementsByTagName("a").Cast<HtmlElement>().FirstOrDefault((HtmlElement c) => c.GetAttribute("href").StartsWith(href));
	}

	private static object GenerateQuote(string text, string author)
	{
		text = Regex.Replace(text, "<(\\/)?(b|i|u)>", "[$1$2]", RegexOptions.IgnoreCase);
		text = text.ReplaceIgnoreCase("<br>", "\r\n");
		text = text.ReplaceIgnoreCase("&lt;", "<");
		text = text.ReplaceIgnoreCase("&gt;", ">");
		text = Regex.Replace(text, "<img [^>]*title=(\")?([^ \"]+)(\")?[^>]*>", "[img=$2]", RegexOptions.IgnoreCase);
		text = Regex.Replace(text, "<a [^>]*href=\"link:([^ >]+)\"[^>]*>([^<>]*)</a>", "$2", RegexOptions.IgnoreCase);
		text = Regex.Replace(text, "<span onmouseover[^ >']+'(.*)'[^>]*>[^<>]*</span>", "$1", RegexOptions.IgnoreCase);
		text = Regex.Replace(text, "<blockquote><cite style=.display:[ ]+block;.>([a-zA-Z0-9]+) \\w+:</cite>[ \\r\\n]*", "[quote=\"$1\"]", RegexOptions.IgnoreCase);
		text = text.ReplaceIgnoreCase("</blockquote>", "[/quote]");
		return "[quote=\"" + author + "\"]" + text + "[/quote]\r\n";
	}

	private string GenerateReply(string author)
	{
		return "[b]" + author + "[/b]: ";
	}

	private void UpdateSmileysPanel()
	{
		HtmlElement elementById = Document.GetElementById("SmileysPanel");
		if (elementById == null)
		{
			return;
		}
		if (Settings.Default.CommentSmilesShow)
		{
			string[] files = System.IO.Directory.GetFiles(AppHelper.SmileysPath, "*.gif");
			int num = 0;
			string text = "<span class='Close' id='CloseSmiles'>x</span>";
			string[] array = files;
			foreach (string text2 in array)
			{
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text2);
				string text3 = "<a href='smiley:" + fileNameWithoutExtension + "'><img style='vertical-align:bottom;' title='" + fileNameWithoutExtension + "' alt='" + fileNameWithoutExtension + "' src='file://" + text2 + "' border=0></a>&nbsp;&nbsp;";
				if (num++ == 14)
				{
					num = 0;
					text3 += "<br/>";
				}
				text += text3;
			}
			elementById.InnerHtml = text;
			elementById.Style = "";
			CloseSmiles = Document.GetElementById("CloseSmiles");
		}
		else
		{
			elementById.InnerHtml = "<a href='show:c' class='fill-div' style='padding:8px 8px 10px 8px;'>" + Words.SpotThemeShow + "</a>";
			elementById.Style = "padding:0 16px 0 0;";
		}
	}

	private void CloseSmiles_Click(object sender, HtmlElementEventArgs e)
	{
		try
		{
			if (!AskUnload)
			{
				Settings.Default.CommentSmilesShow = false;
				Settings.Default.Save();
				UpdateSmileysPanel();
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	public string LoadAndSaveFullImage()
	{
		try
		{
			byte[] array = ImageHelper.LoadSpotFullImage(SpotEx);
			FileCacheManager.Save(SpotEx, array);
			if (array.IsNullOrEmpty())
			{
				return null;
			}
			return WriteBytesToTmpFile(array);
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
			return null;
		}
	}

	private string WriteBytesToTmpFile(byte[] bytes)
	{
		string tempFileName = AppHelper.GetTempFileName();
		try
		{
			StreamWriter streamWriter = new StreamWriter(tempFileName, append: false, AppHelper.LatinEnc());
			new BinaryWriter(streamWriter.BaseStream, AppHelper.LatinEnc()).Write(bytes);
			streamWriter.Close();
			return tempFileName;
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
			return null;
		}
	}

	private void ProgressChanged(string sMessage, int sValue)
	{
		try
		{
			if (AskUnload || _commentsStatus == null)
			{
				return;
			}
			DispatcherHelper.CheckBeginInvokeOnUI(delegate
			{
				if (_commentsStatus.InnerText == null || !_commentsStatus.InnerText.Equals(sMessage))
				{
					_commentsStatus.InnerText = sMessage;
				}
			});
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	private void StartProcessImage()
	{
		try
		{
			if (AskUnload)
			{
				return;
			}
			if (SpotImage != null)
			{
				if (_loadImageManually)
				{
					SpotImage.OuterHtml = string.Format("<img id='SpotImage' src=\"{0}/Images/loading.gif\" onfocus='this.blur()'>", "file://" + AppHelper.SettingsFolder);
					SpotImage = Document.GetElementById("SpotImage");
					if (SpotImage == null)
					{
						return;
					}
				}
				if (!_loadImageManually && (!Settings.Default.LoadImageOnSpotTab || SpotEx.DoNotLoadImageAutomatically))
				{
					SpotImage.OuterHtml = "<div id='SpotImage' style='border: 1px solid black;padding: 10px;'onmouseover=\"this.style.background='#ffc'; this.style.cursor='pointer'\" onmouseout=\"this.style.background='transparent'; this.style.cursor='default'\" onclick=\"window.location='loadimg://.';\"><i>" + Words.ImageLoadDisabledClickToLoad + "</i></div>";
					SpotImage = Document.GetElementById("SpotImage");
				}
				else if (SpotEx.Image.IsNullOrEmpty() && SpotEx.ImageID.IsNullOrEmpty() && SpotEx.PreviewImage.IsNullOrEmpty())
				{
					SpotImage.OuterHtml = "<center><i>" + Words.ImageSourceNotSpecified + "</i></center>";
				}
				else if (!SpotEx.PreviewImage.IsNullOrEmpty())
				{
					SpotImage.SetAttribute("SRC", SpotEx.PreviewImage);
					Toolbar.SetImageAsync(System.Drawing.Image.FromFile(SpotEx.PreviewImage));
					SpotImage.Style = "cursor:pointer;" + SpotImage.Style;
					_isImageResizeable = true;
					_fileToRemoveOnClose = SpotEx.PreviewImage;
					FilesToRemoveOnClose.Add(_fileToRemoveOnClose);
				}
				else if (!SpotEx.Image.IsNullOrEmpty())
				{
					SpotImage.SetAttribute("SRC", SpotEx.Image);
					Toolbar.SetImageAsync(System.Drawing.Image.FromFile(SpotEx.Image));
					_isImageResizeable = true;
					SpotImage.Style = "cursor:pointer;" + SpotImage.Style;
				}
				else
				{
					UpdateWithFullImageFromTheNet();
				}
			}
			else
			{
				Log.Warn("Section with id='SpotImage' not found in theme file");
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
		}
	}

	public static void CleanAllGarbageImages()
	{
		foreach (string item in FilesToRemoveOnClose)
		{
			try
			{
				System.IO.File.Delete(item);
			}
			catch (Exception)
			{
			}
		}
	}

	private void UpdateWithFullImageFromTheNet()
	{
		string tmpFile = "";
		Task.Factory.StartNew(delegate
		{
			tmpFile = LoadAndSaveFullImage();
		}).ContinueWith(delegate(Task t)
		{
			if (AskUnload)
			{
				return;
			}
			bool flag = false;
			try
			{
				if (t.Exception != null)
				{
					Log.Exception(t.Exception, showToClient: true);
				}
				else if (!tmpFile.IsNullOrEmpty())
				{
					SpotImage.SetAttribute("SRC", tmpFile);
					Toolbar.SetImageAsync(System.Drawing.Image.FromFile(tmpFile));
					_isImageResizeable = true;
					SpotImage.Style = "cursor:pointer;" + SpotImage.Style;
					_fileToRemoveOnClose = tmpFile;
					FilesToRemoveOnClose.Add(tmpFile);
					flag = true;
				}
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
			}
			finally
			{
				if (!AskUnload && !flag)
				{
					SpotImage.OuterHtml = "";
				}
			}
		});
	}
}
