using System.Threading.Tasks;
using NLog;
using Spotnet.Helpers;

namespace Spotnet.Browser;

public class ReleaseNotesNativePage : IEWebBrowser
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	public ReleaseNotesNativePage()
	{
		base.Title = "Release Notes";
		base.PageDefaultType = PageTypeEnum.ReleaseNotes;
		Task.Run(delegate
		{
			base.Uri = ReleaseNotesPage.ReleaseNotesUri;
		});
	}
}
