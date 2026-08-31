using System;
using System.Collections.Generic;
using System.Web;
using Awesomium.Core;
using NLog;
using Pri.LongPath;
using Spotnet.Extensions;
using Spotnet.Helpers;

namespace Spotnet.Browser;

internal class SpotResourceInterceptor : IResourceInterceptor
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private static readonly List<string> SupportedExtentions = new List<string>
	{
		".html", ".js", ".css", ".png", ".gif", ".ico", ".jpeg", ".jpg", ".zip", ".txt",
		".swf", ".mp4", ".ogv", ".flv", ".webm"
	};

	public ResourceResponse OnRequest(ResourceRequest request)
	{
		try
		{
			bool flag = request.Url.Scheme.EqualsIgnoreCase("asset");
			bool flag2 = request.Url.Scheme.EqualsIgnoreCase("file");
			string item = Path.GetExtension(request.Url.AbsoluteUri).ToLower();
			if (!flag && !flag2)
			{
				return null;
			}
			if (!SupportedExtentions.Contains(item))
			{
				return null;
			}
			string text = HttpUtility.UrlDecode(request.Url.AbsoluteUri.Replace("..", ""));
			if (text.IsNullOrWhiteSpace())
			{
				return null;
			}
			text = text.Substring(flag ? "asset://".Length : "file://".Length);
			text = text[0] + ":" + text.Substring(1);
			if (!File.Exists(text))
			{
				return null;
			}
			return ResourceResponse.Create(text);
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
			return null;
		}
	}

	public bool OnFilterNavigation(NavigationRequest request)
	{
		return false;
	}
}
