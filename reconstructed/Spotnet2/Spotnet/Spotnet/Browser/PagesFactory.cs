using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;
using Spotnet.Properties;

namespace Spotnet.Browser;

internal static class PagesFactory
{
	public static readonly List<IPage> AllPages = new List<IPage>();

	public static void DisposeAllPages()
	{
		foreach (IPage item in AllPages.ToList())
		{
			item.Dispose();
		}
	}

	public static IPage GetPage(PageTypeEnum pageType, string url = "", SpotEx spotEx = null)
	{
		if (AllPages == null || !AllPages.Any())
		{
			return null;
		}
		switch (pageType)
		{
		case PageTypeEnum.SpotLoaded:
			if (spotEx == null)
			{
				return null;
			}
			return AllPages.FirstOrDefault((IPage p) => p is ISpotPage { SpotEx: not null } spotPage && spotPage.SpotEx.MessageId == spotEx.MessageId);
		case PageTypeEnum.WebPage:
			if (url.IsNullOrEmpty())
			{
				return null;
			}
			return AllPages.FirstOrDefault((IPage p) => p != null && p.Uri != null && p.Uri.AbsoluteUri == url);
		case PageTypeEnum.ReleaseNotes:
		case PageTypeEnum.ResponseSite:
		case PageTypeEnum.AdvancedDownloads:
			return AllPages.FirstOrDefault((IPage p) => p != null && p.PageType == pageType);
		default:
			return null;
		}
	}

	public static System.Threading.Tasks.Task<IPage> NewPage(PageTypeEnum pageType, TabItem tabItem, string urlOrTitle = "", SpotEx spotEx = null)
	{
		IPage page;
		switch (pageType)
		{
		case PageTypeEnum.ReleaseNotes:
			page = new ReleaseNotesPage();
			break;
		case PageTypeEnum.ResponseSite:
			page = new ResponsePage();
			break;
		case PageTypeEnum.AdvancedDownloads:
			page = new AdvancedDownloadsPage();
			break;
		case PageTypeEnum.SpotLoaded:
			// WebView2 is the engine for spot pages. The MSHTML page is kept behind
			// UseNativeBrowser so a machine where the Evergreen Runtime is missing or
			// misbehaving still has a way to read a spot.
			page = (Settings.Default.UseNativeBrowser || !WebView2Page.IsRuntimeAvailable())
				? (IPage)new SpotNativePage(urlOrTitle, spotEx)
				: new SpotWebView2Page(urlOrTitle, spotEx);
			break;
		default:
			page = new WebView2Page(urlOrTitle);
			break;
		}
		page.TabItem = tabItem;
		AllPages.Add(page);
		page.DocumentUnloadedEvent += delegate
		{
			AllPages.Remove(page);
		};
		return System.Threading.Tasks.Task.FromResult(page);
	}
}
