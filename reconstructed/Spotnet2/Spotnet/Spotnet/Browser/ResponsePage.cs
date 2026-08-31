using System;
using System.Net;
using Awesomium.Core;
using NLog;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Properties;

namespace Spotnet.Browser;

internal class ResponsePage : AwesomiumPage
{
	private const string PageTitleOfResponseSite = "Feedback";

	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private bool _alreadyBinded;

	private bool _uploadDisabled;

	private readonly object _lockRoot = new object();

	public ResponsePage()
	{
		base.Uri = new Uri(GetResponseSiteUrl(), UriKind.RelativeOrAbsolute);
		Title = "Feedback";
		base.PageDefaultType = PageTypeEnum.ResponseSite;
		base.DocumentReadyEvent += OnDocumentReady;
	}

	private void OnDocumentReady(object o, DocumentReadyEventArgs args)
	{
		if (args.ReadyState == DocumentReadyState.Loaded)
		{
			BindingUploadLogs();
		}
	}

	public static string GetResponseSiteUrl()
	{
		string providerDomainHtmlSave = AppHelper.GetProviderDomainHtmlSave();
		string text = ((providerDomainHtmlSave == null) ? "" : ("&provider=" + providerDomainHtmlSave));
		return string.Format("{0}/?id={1}&appversion={2}&lang={3}{4}", Configuration.ResponseSiteUrl, UserKeyHelper.GetModulusUriCompatable(), AppHelper.AppVersion, (UserLanguageHelper.Language == "en") ? "en" : "nl", text);
	}

	private void BindingUploadLogs()
	{
		lock (_lockRoot)
		{
			if (_alreadyBinded)
			{
				return;
			}
			_alreadyBinded = true;
		}
		using JSObject jSObject = (JSObject)base.Browser.CreateGlobalJavascriptObject("app");
		jSObject.BindAsync("UploadLogs", (JavascriptAsyncMethodHandler)delegate
		{
			lock (_lockRoot)
			{
				if (_uploadDisabled)
				{
					return;
				}
				_uploadDisabled = true;
			}
			bool flag = false;
			try
			{
				ChangeStateOfResponseSubmitButton(enabled: false);
				flag = ZipAndUploadLogs();
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
			}
			finally
			{
				if (!flag)
				{
					ChangeStateOfResponseSubmitButton(enabled: true);
				}
			}
		});
	}

	private bool ZipAndUploadLogs()
	{
		string text = LogHelper.ZipLogFiles();
		if (text.IsNullOrEmpty())
		{
			Log.Debug("No log files to attach.");
			return false;
		}
		return AppHelper.UploadFile(text, Configuration.ResponseSiteUploadLogsUrl, delegate(object s, UploadFileCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				Log.Exception(e.Error);
			}
			ChangeStateOfResponseSubmitButton(enabled: true);
		});
	}

	private void ChangeStateOfResponseSubmitButton(bool enabled)
	{
		string script = string.Format("document.getElementById('form-submit-btn').disabled = {0};", enabled ? "false" : "true");
		base.Browser.ExecuteJavascript(script);
	}
}
