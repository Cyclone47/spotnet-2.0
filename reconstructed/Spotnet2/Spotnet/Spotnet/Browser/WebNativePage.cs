using System;
using NLog;
using Spotnet.Helpers;

namespace Spotnet.Browser;

public class WebNativePage : IEWebBrowser
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	public WebNativePage(string url)
	{
		base.Title = url;
		base.PageDefaultType = PageTypeEnum.WebPage;
		base.Uri = new Uri(url, UriKind.Absolute);
	}
}
