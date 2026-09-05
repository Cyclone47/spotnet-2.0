using System;
using Spotnet.Community;
using Spotnet.Helpers;

namespace Spotnet.Properties;

internal static class Configuration
{
	/// <summary>
	/// Community-bound endpoints. These are read from <see cref="CommunityConfig"/> rather
	/// than compiled in, so a build can be pointed at a different community - or a
	/// community can move its server - without a new release.
	/// </summary>
	internal static string ResponseSiteUrl => CommunityConfig.Current.Services.ResponseSiteUrl;

	internal static string ResponseSiteUploadLogsUrl => CommunityConfig.Current.Services.LogUploadUrl;

	internal static string UpgradeFailuresUploadUrl => CommunityConfig.Current.Services.UpgradeFailuresUrl;

	internal static string RemoteWhitelistUrl => CommunityConfig.Current.Moderation.WhitelistUrl;

	internal static string RemoteBlacklistUrl => CommunityConfig.Current.Moderation.BlacklistUrl;

	internal static string RemoteSpotWhitelistUrl => CommunityConfig.Current.Moderation.SpotWhitelistUrl;

	internal static string RemoteSpotBlacklistUrl => CommunityConfig.Current.Moderation.SpotBlacklistUrl;

	internal static string RemotePromoFolder => CommunityConfig.Current.Services.PromoFolderUrl;

	internal static string[] UpdateUrls = new string[1] { "https://spotcloud.spotnet.wf/spotnet/" };

	/// <summary>
	/// Where an installed copy looks for its next release. The file lives on the default
	/// branch, so publishing an update is a commit; until its clientUpdate flag is set,
	/// clients read the entry and ignore it.
	/// </summary>
	internal const string UpdateManifestUrl =
		"https://raw.githubusercontent.com/Cyclone47/spotnet-3.0/main/updates/latest.json";

	internal static string[] UpdateGroupsBeta = new string[1] { "free.beer" };

	internal static string[] UpdateGroupsRelease = new string[1] { "free.c" };

	internal static string NzbArchiveFileName = "last.nzb.zip";

	internal const string UpdatesPublicKeyXml = "<RSAKeyValue><Modulus>xJ8rOq1i0xsDWuHgRDbCngSyrYGBsamWnKzlFxHQXyPrNo9UjpFU4hONPTnzo5JJlX7SVnbVvY9k64xe3KbTQmXRnU+0GZQ0ikz0XjJgfHTpI+4MmSILx12ZMbN50rDDWHa6Mda/6O/xwV2Tcpi+dFxL63UoGnIW+13pEHg/Dfc=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

	internal const int ThumbMaxWidth = 143;

	internal const int ThumbMaxHeight = 210;

	internal const string TrayTitle = "Spotnet :: Tray";

	internal const SpotsSourceEnum SpotsSources = SpotsSourceEnum.FileCache | SpotsSourceEnum.ImageFromFullImagesGroup | SpotsSourceEnum.ImageByUrl | SpotsSourceEnum.ImageFromThumbsGroup;

	internal const int DefaultPageSize = 250;

	internal const int OwnSpotDeleteSafePeriodInDays = 5;

	internal static TimeSpan AvgBlockingIssueLongPeriod = TimeSpan.FromDays(1.0);

	internal static TimeSpan AvgBlockingIssueWaitingPeriod = TimeSpan.FromSeconds(3.0);

	public const string StartTestConnectionString = "START TEST CONNECTION";
}
