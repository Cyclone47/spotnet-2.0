using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using NLog;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;

namespace Spotnet.Browser;

internal static class PagesFactory
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

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

	public static async Task<IPage> NewPage(PageTypeEnum pageType, TabItem tabItem, string urlOrTitle = "", SpotEx spotEx = null)
	{
		IPage page;
		switch (pageType)
		{
		case PageTypeEnum.ReleaseNotes:
			await AwesomiumPage.InitializeWebCore();
			page = new ReleaseNotesPage();
			break;
		case PageTypeEnum.ResponseSite:
			await AwesomiumPage.InitializeWebCore();
			page = new ResponsePage();
			break;
		case PageTypeEnum.AdvancedDownloads:
			await AwesomiumPage.InitializeWebCore();
			page = new AdvancedDownloadsPage();
			break;
		case PageTypeEnum.SpotLoaded:
			page = new SpotNativePage(urlOrTitle, spotEx);
			break;
		default:
			await AwesomiumPage.InitializeWebCore();
			page = new AwesomiumPage(urlOrTitle);
			break;
		}
		page.TabItem = tabItem;
		AllPages.Add(page);
		page.DocumentUnloadedEvent += delegate
		{
			AllPages.Remove(page);
		};
		return page;
	}
}
