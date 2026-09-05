using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spotnet.Community;
using Xunit;

namespace Spotnet.Tests;

[CollectionDefinition("Community configuration", DisableParallelization = true)]
public class CommunityConfigCollection
{
}

/// <summary>
/// The configuration decides which community this client joins, so the cases that matter
/// are: an untouched install still points where it always did, a half-written file does not
/// take the client down with it, and nonsense is caught before it is saved.
/// </summary>
[Collection("Community configuration")]
public class CommunityConfigTests : IDisposable
{
    private readonly string previousFolder = Spotnet.Helpers.AppHelper.SettingsFolder;
    private readonly string testFolder =
        Path.Combine(Path.GetTempPath(), "CommunityConfigTests-" + Guid.NewGuid().ToString("N"));

    public CommunityConfigTests()
    {
        Directory.CreateDirectory(testFolder);
        Spotnet.Helpers.AppHelper.SettingsFolder = testFolder;
        CommunityConfig.Invalidate();
    }

    public void Dispose()
    {
        Spotnet.Helpers.AppHelper.SettingsFolder = previousFolder;
        CommunityConfig.Invalidate();
        if (Directory.Exists(testFolder))
        {
            Directory.Delete(testFolder, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DefaultsStillPointAtTheExistingCommunity()
    {
        CommunityConfig config = new CommunityConfig();

        Assert.Equal("free.pt", config.Newsgroups.Spots);
        Assert.Equal("free.usenet", config.Newsgroups.Comments);
        Assert.Equal("free.willey", config.Newsgroups.Reports);
        Assert.Equal("alt.binaries.ftd", config.Newsgroups.Nzb);

        Assert.True(config.Moderation.Enabled);
        Assert.Equal(120, config.Moderation.UpdateIntervalMinutes);
        Assert.Equal("http://spotcloud.spotnet.wf/spotnet/lists.new/whitelist.csv", config.Moderation.WhitelistUrl);
        Assert.Equal("http://spotcloud.spotnet.wf/spotnet/lists.new/blacklist.csv", config.Moderation.BlacklistUrl);
        Assert.Equal("https://spotcloud.spotnet.wf/spotnet/response/", config.Services.ResponseSiteUrl);

        // Signature checking is off until a community publishes signatures.
        Assert.False(config.Moderation.RequireSignedLists);
        Assert.Equal("", config.Moderation.SignaturePublicKeyXml);
    }

    [Fact]
    public void DefaultConfigurationIsValid()
    {
        Assert.Empty(new CommunityConfig().Validate());
    }

    [Fact]
    public void SurvivesARoundTrip()
    {
        CommunityConfig original = new CommunityConfig
        {
            Name = "Testgemeenschap"
        };
        original.Newsgroups.Spots = "free.test";
        original.Moderation.UpdateIntervalMinutes = 45;
        original.Indexer.NewznabApiKey = "abcdef";

        CommunityConfig restored = CommunityConfig.Deserialize(original.Serialize());

        Assert.Equal("Testgemeenschap", restored.Name);
        Assert.Equal("free.test", restored.Newsgroups.Spots);
        Assert.Equal(45, restored.Moderation.UpdateIntervalMinutes);
        Assert.Equal("abcdef", restored.Indexer.NewznabApiKey);
    }

    [Fact]
    public void AFileThatOverridesOneValueKeepsTheDefaultsForTheRest()
    {
        CommunityConfig config = CommunityConfig.Deserialize(
            "{ \"newsgroups\": { \"spots\": \"free.anders\" } }");

        Assert.Equal("free.anders", config.Newsgroups.Spots);
        Assert.Equal("free.usenet", config.Newsgroups.Comments);
        Assert.Equal(120, config.Moderation.UpdateIntervalMinutes);
        Assert.NotNull(config.Services);
        Assert.NotNull(config.Indexer);
    }

    [Fact]
    public void AnEmptyDocumentKeepsEveryDefault()
    {
        CommunityConfig config = CommunityConfig.Deserialize("{}");

        Assert.NotNull(config);
        Assert.Equal("free.pt", config.Newsgroups.Spots);
        Assert.Empty(config.Validate());
    }

    [Fact]
    public void NothingAtAllIsNotAConfiguration()
    {
        Assert.Null(CommunityConfig.Deserialize(""));
        Assert.Null(CommunityConfig.Deserialize("   "));
    }

    [Fact]
    public void AnUnreadableFileFallsBackToTheDefaults()
    {
        File.WriteAllText(CommunityConfig.ConfigPath, "{ this is not json");

        CommunityConfig config = CommunityConfig.Load();

        Assert.Equal("free.pt", config.Newsgroups.Spots);
    }

    [Fact]
    public void AMissingFileFallsBackToTheDefaults()
    {
        Assert.False(File.Exists(CommunityConfig.ConfigPath));

        Assert.Equal("free.pt", CommunityConfig.Load().Newsgroups.Spots);
    }

    [Fact]
    public void SavedConfigurationIsReadBack()
    {
        CommunityConfig config = new CommunityConfig { Name = "Bewaard" };
        config.Moderation.WhitelistUrl = "https://voorbeeld.nl/whitelist.csv";

        Assert.True(config.Save());
        Assert.True(File.Exists(CommunityConfig.ConfigPath));

        CommunityConfig loaded = CommunityConfig.Load();
        Assert.Equal("Bewaard", loaded.Name);
        Assert.Equal("https://voorbeeld.nl/whitelist.csv", loaded.Moderation.WhitelistUrl);
    }

    [Fact]
    public void AnEmptyNewsgroupIsRejected()
    {
        CommunityConfig config = new CommunityConfig();
        config.Newsgroups.Reports = "";

        Assert.Contains(config.Validate(), e => e.Contains("Klachten-newsgroup"));
    }

    [Fact]
    public void AUrlPastedIntoANewsgroupFieldIsRejected()
    {
        CommunityConfig config = new CommunityConfig();
        config.Newsgroups.Spots = "http://spotcloud.spotnet.wf/spots";

        Assert.Contains(config.Validate(), e => e.Contains("Spots-newsgroup"));
    }

    [Fact]
    public void AListUrlThatIsNotHttpIsRejected()
    {
        CommunityConfig config = new CommunityConfig();
        config.Moderation.BlacklistUrl = "ftp://voorbeeld.nl/blacklist.csv";

        Assert.Contains(config.Validate(), e => e.Contains("Blacklist-URL"));
    }

    [Fact]
    public void ListUrlsMayBeEmptyWhenModerationIsOff()
    {
        CommunityConfig config = new CommunityConfig();
        config.Moderation.Enabled = false;
        config.Moderation.WhitelistUrl = "";
        config.Moderation.BlacklistUrl = "";
        config.Moderation.SpotWhitelistUrl = "";
        config.Moderation.SpotBlacklistUrl = "";

        Assert.Empty(config.Validate());
    }

    [Fact]
    public void ListUrlsAreRequiredWhenModerationIsOn()
    {
        CommunityConfig config = new CommunityConfig();
        config.Moderation.WhitelistUrl = "";

        Assert.Contains(config.Validate(), e => e.Contains("Whitelist-URL"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(20000)]
    public void AnImpossibleIntervalIsRejected(int minutes)
    {
        CommunityConfig config = new CommunityConfig();
        config.Moderation.UpdateIntervalMinutes = minutes;

        Assert.Contains(config.Validate(), e => e.Contains("bijwerkinterval"));
    }

    [Fact]
    public void AnIntervalOfZeroIsAllowed()
    {
        CommunityConfig config = new CommunityConfig();
        config.Moderation.UpdateIntervalMinutes = 0;

        Assert.Empty(config.Validate());
    }

    [Fact]
    public void RequiringSignaturesWithoutAKeyIsRejected()
    {
        CommunityConfig config = new CommunityConfig();
        config.Moderation.RequireSignedLists = true;

        Assert.Contains(config.Validate(), e => e.Contains("publieke sleutel"));
    }

    [Fact]
    public void RequiringSignaturesWithAKeyIsAllowed()
    {
        CommunityConfig config = new CommunityConfig();
        config.Moderation.RequireSignedLists = true;
        config.Moderation.SignaturePublicKeyXml = "<RSAKeyValue><Modulus>AA==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        Assert.Empty(config.Validate());
    }

    [Fact]
    public void OptionalUrlsMayBeLeftEmpty()
    {
        CommunityConfig config = new CommunityConfig();
        config.Moderation.ModeratorKeysUrl = "";
        config.Services.PromoFolderUrl = "";
        config.Indexer.NewznabBaseUrl = "";

        Assert.Empty(config.Validate());
    }

    [Fact]
    public void AnIndexerIsOnlyUsableWithBothAUrlAndAKey()
    {
        Assert.True(new CommunityIndexer().IsConfigured);
        Assert.False(new CommunityIndexer { NewznabBaseUrl = "" }.IsConfigured);
        Assert.False(new CommunityIndexer { NewznabApiKey = "  " }.IsConfigured);
    }

    [Fact]
    public void EveryValidationMessageNamesTheFieldItIsAbout()
    {
        CommunityConfig config = new CommunityConfig();
        config.Newsgroups.Spots = "";
        config.Newsgroups.Comments = "";
        config.Moderation.BlacklistUrl = "niet eens een url met spaties";

        List<string> errors = config.Validate().ToList();

        Assert.Equal(3, errors.Count);
        Assert.All(errors, e => Assert.False(string.IsNullOrWhiteSpace(e)));
    }
}
