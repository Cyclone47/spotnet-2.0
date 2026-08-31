using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Awesomium.Core;
using GalaSoft.MvvmLight.Threading;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using NLog;
using Spotnet.DAL;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;
using Spotnet.Properties;
using Spotnet.Utilities;
using Spotnet.ViewModel;

namespace Spotnet.Browser;

internal class SpotCommentsHandler
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private static readonly Dictionary<string, List<Comment>> MessagesFromUserToShowOnNextTabOpen = new Dictionary<string, List<Comment>>();

	private readonly CancellationTokenSource _cancelGettingCommentsSource = new CancellationTokenSource();

	private readonly HashSet<long> _commentIdCache;

	private readonly string _commentProgressCache;

	private readonly List<long> _fetchedCache = new List<long>();

	private readonly HashSet<string> _messagesFromUserAndAlreadyShown = new HashSet<string>();

	private readonly ISpotPage _page;

	private readonly Dictionary<string, string> _uniqueCache = new Dictionary<string, string>();

	private CancellationToken _cancellationToken;

	private dynamic _commentProgress;

	private dynamic _comments;

	private bool _commentsRefreshWasClickedAlready;

	private string _lastBody;

	private DateTime _lastTime;

	private dynamic CommentsProgress
	{
		get
		{
			if (!((_commentProgress != null && _commentProgress && !_commentProgress.IsDisposed) ? true : false))
			{
				return _commentProgress = Global.Current.document.getElementById("CommentsProgress");
			}
			return _commentProgress;
		}
	}

	private dynamic CommentsSection
	{
		get
		{
			if (!((_comments != null && _comments && !_comments.IsDisposed) ? true : false))
			{
				return _comments = Global.Current.document.getElementById("Comments");
			}
			return _comments;
		}
	}

	private static SpotsListViewModel SpotsListVm => ((ViewModelLocator)Application.Current.Resources["Locator"]).SpotsList;

	public SpotCommentsHandler(ISpotPage page, CancellationToken cancellationToken)
	{
		_commentIdCache = new HashSet<long>();
		_cancellationToken = cancellationToken;
		_page = page;
		if (CommentsProgress)
		{
			_commentProgressCache = CommentsProgress.innerHTML;
		}
	}

	public void StartProcessComments()
	{
		DispatcherHelper.UIDispatcher.BeginInvoke((Action)delegate
		{
			string zErr = "";
			try
			{
				if (!Settings.Default.ShowComments)
				{
					CommentsDone("");
				}
				else if (!StartUpdateComments(_page.SpotEx.MessageId, ref zErr))
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

	private void CommentsDone(string sError)
	{
		try
		{
			if (_cancellationToken.IsCancellationRequested)
			{
				return;
			}
			string str = "<p><A onfocus='this.blur()' HREF='spotnet:reload'><IMG id='reload' onfocus='this.blur()' title='" + Words.Refresh + "' style='border: 0px; cursor:pointer; width: 32px; height:32px;' SRC=\"" + SpotParser.LocalFilePrefix + AppHelper.SettingsFolder.Replace("\\", "/") + "/Images/refresh1.png\"></A>";
			string error = "<center>" + AppHelper.HtmlEncode(sError) + "<br></center>" + str;
			_page.CreateJecAsync(delegate
			{
				if ((!CommentsProgress))
				{
					Log.Warn("Section with id='CommentsProgress' not found in theme file");
				}
				else if (!sError.IsNullOrEmpty())
				{
					CommentsProgress.innerHTML = error;
				}
				else
				{
					string text = "";
					if (_commentIdCache.Count == 0)
					{
						if (!Settings.Default.ShowComments && !_commentsRefreshWasClickedAlready)
						{
							text = text + "<center>" + Words.CommentsNotRetrieved + "<br></center>";
							_commentsRefreshWasClickedAlready = true;
						}
						else
						{
							text = text + "<center>" + Words.CommentsNotFound + "<br></center>";
						}
					}
					if (!Settings.Default.LoadComments)
					{
						if (!text.IsNullOrEmpty())
						{
							text += "<br>";
						}
						text = text + "<center><small>" + Words.CommentsUpdateDisabledWarning + "</small><br></center>";
					}
					else if (!SpotsListVm.IsSpotsDbUpToDate || !SpotsListVm.IsCommentsDbUpToDate)
					{
						if (!text.IsNullOrEmpty())
						{
							text += "<br>";
						}
						text = text + "<center><small>" + Words.CommentsDbIsNotUpToDateWarning + "</small><br></center>";
					}
					CommentsProgress.innerHTML = text + str;
				}
			});
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
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

	private void ShowNewComment(Comment comment, bool isVirtual)
	{
		try
		{
			string newCommentStr = GenerateCommentHtmlCode(comment, isVirtual, isPreview: false);
			if (newCommentStr == null)
			{
				return;
			}
			_page.CreateJecSync(delegate
			{
				if ((!CommentsSection))
				{
					Log.Warn("Section with id='Comments' not found in theme file");
				}
				else
				{
					string text = newCommentStr;
					CommentsSection.innerHTML += text;
				}
			});
			if (comment.Article == 0L)
			{
				return;
			}
			_commentIdCache.Add(comment.Article);
			if (!MessagesFromUserToShowOnNextTabOpen.ContainsKey(_page.SpotEx.MessageId))
			{
				return;
			}
			foreach (Comment item in MessagesFromUserToShowOnNextTabOpen[_page.SpotEx.MessageId])
			{
				if (item.MessageId == SpotHelper.MakeMsg(comment.MessageId, tag: false))
				{
					item.Article = comment.Article;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
		}
	}

	private static bool IsItMyComment(Comment comment)
	{
		return comment.User.Modulus.Equals(UserKeyHelper.GetModulus());
	}

	private string ShowComments()
	{
		try
		{
			if (_cancellationToken.IsCancellationRequested)
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
				string returnStr = "";
				Comments.StartLoadCommentsBody(AppHelper.HeaderPhuse, list, Sys.MainWindow.CommentSettings(bIncludeLast: false), ProgressChanged, delegate(Comment c)
				{
					ShowNewComment(c, isVirtual: false);
				}, _cancelGettingCommentsSource.Token).ContinueWith(delegate(Task t)
				{
					try
					{
						ShowCommentsFromUserPostedBefore();
						if (t.IsFaulted)
						{
							returnStr = ((t.Exception == null) ? "Error on getting comments" : t.Exception.TheMostInnerException().Message);
						}
					}
					catch (Exception e2)
					{
						returnStr = e2.TheMostInnerException().Message;
					}
				}).Wait();
				return returnStr;
			}
			ShowCommentsFromUserPostedBefore();
			Thread.Sleep(500);
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
		if (!MessagesFromUserToShowOnNextTabOpen.ContainsKey(_page.SpotEx.MessageId))
		{
			return;
		}
		foreach (Comment item in MessagesFromUserToShowOnNextTabOpen[_page.SpotEx.MessageId])
		{
			ShowNewComment(item, isVirtual: false);
			_messagesFromUserAndAlreadyShown.Add(SpotHelper.MakeMsg(item.MessageId));
		}
	}

	private void EnableAdd()
	{
		if (_cancellationToken.IsCancellationRequested)
		{
			return;
		}
		_page.CreateJecAsync(delegate
		{
			dynamic elementById = Global.Current.document.getElementById("AddComment");
			if (!((elementById == null) ? true : false))
			{
				elementById.className = "enabled";
				elementById.title = Words.Send;
			}
		});
	}

	private void DisableAdd()
	{
		if (_cancellationToken.IsCancellationRequested)
		{
			return;
		}
		_page.CreateJecAsync(delegate
		{
			dynamic elementById = Global.Current.document.getElementById("AddComment");
			if (!((elementById == null) ? true : false))
			{
				elementById.className = "disabled";
				elementById.title = "";
			}
		});
	}

	public bool StartUpdateComments(string sMsgId, ref string zErr)
	{
		try
		{
			if (_cancellationToken.IsCancellationRequested)
			{
				zErr = "Exiting";
				return false;
			}
			_page.CreateJecSync(delegate
			{
				if (!((!CommentsProgress) ? true : false) && !StringExtension.EqualsIgnoreCase(CommentsProgress.innerHTML, _commentProgressCache))
				{
					CommentsProgress.innerHTML = _commentProgressCache;
				}
			});
			ProgressChanged(Words.CommentsLoading + "...", -1);
			Task.Run(delegate
			{
				UpdateComments(sMsgId);
			}, _cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
			return false;
		}
	}

	private void ProgressChanged(string sMessage, int sValue)
	{
		try
		{
			if (_cancellationToken.IsCancellationRequested)
			{
				return;
			}
			_page.CreateJecAsync(delegate
			{
				dynamic elementById = Global.Current.document.getElementById("CommentsStatus");
				if (elementById == null || elementById.IsUndefined || elementById.IsDisposed)
				{
					Log.Warn("Section with id='CommentsStatus' not found in theme file");
				}
				else
				{
					string text = elementById.innerHTML;
					if (text == null || !text.Equals(sMessage))
					{
						elementById.innerHTML = sMessage;
					}
				}
			});
		}
		catch (Exception ex)
		{
			Log.Exception(ex, showToClient: true);
		}
	}

	private void UpdateComments(string sMsgId)
	{
		try
		{
			if (_cancellationToken.IsCancellationRequested)
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

	public void StartProcessAddCommentButtonClick()
	{
		DisableAdd();
		Task.Run(delegate
		{
			string text = "";
			string nick = "";
			_page.CreateJecSync(delegate
			{
				dynamic elementById = Global.Current.document.getElementById("CommentBody");
				dynamic elementsByName = Global.Current.document.getElementsByName("Nickname");
				text = elementById.value;
				if (elementsByName.length != 0)
				{
					nick = elementsByName[0].value;
				}
			});
			if (text.IsNullOrEmpty())
			{
				Interaction.MsgBox(Words.CannotPostEmptyMessage, MsgBoxStyle.Information, Words.Error);
			}
			else if (AppHelper.StripNonAlphaNumericCharacters(text).EqualsIgnoreCase(AppHelper.StripNonAlphaNumericCharacters(_lastBody)))
			{
				Interaction.MsgBox(Words.CannotPostMessageTwice, MsgBoxStyle.Information, Words.Error);
			}
			else
			{
				if (DateAndTime.DateDiff("s", _lastTime, DateAndTime.Now) >= 10)
				{
					try
					{
						Sys.MainWindow.DoWait(Words.Commenting);
						DoComment(text, nick);
						return;
					}
					finally
					{
						Sys.MainWindow.EndWait();
					}
				}
				Interaction.MsgBox(string.Format(Words.NeedToWaitUntilNewMessage, checked(10 - DateAndTime.DateDiff("s", _lastTime, DateAndTime.Now))), MsgBoxStyle.Information, Words.Error);
			}
		}).ContinueWith(delegate
		{
			EnableAdd();
		});
	}

	private void DoComment(string commentBody, string nickname)
	{
		string zErr = "";
		try
		{
			string text = AppHelper.CreateMsgId(_page.SpotEx.MessageId.Split('@')[0].Replace(".", "").Replace("<", ""));
			Settings.Default.Nickname = AppHelper.StripNonAlphaNumericCharacters(nickname);
			Settings.Default.Save();
			byte[] avatar = AppHelper.GetAvatar();
			if (Spots.CreateComment(AppHelper.UploadPhuse, nickname, commentBody, Settings.Default.ReplyGroup, _page.SpotEx.MessageId, _page.SpotEx.Title, avatar, UserKeyHelper.GetKey(), text, ref zErr))
			{
				_lastBody = commentBody;
				_lastTime = DateAndTime.Now;
				_page.CreateJecAsync(delegate
				{
					Global.Current.document.getElementById("CommentBody").value = "";
				});
				Comment comment = new Comment
				{
					Created = DateAndTime.Now,
					From = Settings.Default.Nickname,
					Body = _lastBody,
					MessageId = SpotHelper.MakeMsg(text, tag: false),
					User = new UserInfo()
				};
				if (avatar != null)
				{
					comment.User.Avatar = Settings.Default.Avatar;
				}
				comment.User.Signature = comment.MessageId;
				comment.User.Modulus = UserKeyHelper.GetModulus();
				comment.User.ValidSignature = true;
				ShowNewComment(comment, isVirtual: true);
				_messagesFromUserAndAlreadyShown.Add(SpotHelper.MakeMsg(text));
				if (!MessagesFromUserToShowOnNextTabOpen.ContainsKey(_page.SpotEx.MessageId))
				{
					MessagesFromUserToShowOnNextTabOpen.Add(_page.SpotEx.MessageId, new List<Comment>());
				}
				MessagesFromUserToShowOnNextTabOpen[_page.SpotEx.MessageId].Add(comment);
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
	}

	public string GenerateCommentHtmlCode(Comment comment, bool isVirtual, bool isPreview)
	{
		try
		{
			string sClass = "comment";
			if (!isPreview)
			{
				if (_cancellationToken.IsCancellationRequested)
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
				if (Settings.Default.HideCommentsWithLinks && _page.SpotEx.User.Modulus != comment.User.Modulus && comment.HasLinks() && !IsItMyComment(comment))
				{
					Log.Debug("Comment has links and ignored: " + comment.MessageId);
					return null;
				}
				if (!_uniqueCache.ContainsKey(_page.SpotEx.Poster.ToUpper()))
				{
					_uniqueCache.Add(_page.SpotEx.Poster.ToUpper(), _page.SpotEx.User.Modulus);
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
				else if (comment.User.Modulus.EqualsIgnoreCase(_page.SpotEx.User.Modulus))
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
			return $"<SPAN style='visibility:visible'>{SpotParser.ParseComment(comment, sClass, text, isPreview)}</SPAN>";
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
			return null;
		}
	}
}
