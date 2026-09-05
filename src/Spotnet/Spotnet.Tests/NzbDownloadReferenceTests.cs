using Spotnet.Helpers;
using Spotnet.Model;
using Xunit;

namespace Spotnet.Tests;

public sealed class NzbDownloadReferenceTests
{
    [Fact]
    public void LegacySpotWithoutReferenceIsUnavailable()
    {
        // SpotEx starts with legacy metadata, but no NZB. There is no fallback
        // URL to fetch and no need to put a placeholder in the download queue.
        Assert.False(SpotHelper.TryGetDownloadReference(new SpotEx(), out _, out _));
    }

    [Theory]
    [InlineData("segment@news.example")]
    [InlineData("https://indexer.example/api?t=get&id=123")]
    public void EmbeddedAndIndexerReferencesRemainAvailable(string reference)
    {
        var spot = new SpotEx { OldInfo = null, NZB = reference };
        Assert.True(SpotHelper.TryGetDownloadReference(spot, out var location, out int key));
        Assert.Equal(reference, location);
        Assert.Equal(-1, key);
    }

    [Fact]
    public void EncryptedReferenceRetainsPriorityAndKey()
    {
        var spot = new SpotEx { OldInfo = null, NZB = "ordinary", NZR = "encrypted", NZRKey = 42 };
        Assert.True(SpotHelper.TryGetDownloadReference(spot, out var location, out int key));
        Assert.Equal("encrypted", location);
        Assert.Equal(42, key);
    }

    [Fact]
    public void LegacyMetadataDoesNotHideAnAvailableNzb()
    {
        var spot = new SpotEx { NZB = "available@news.example" };
        Assert.True(SpotHelper.TryGetDownloadReference(spot, out var location, out _));
        Assert.Equal(spot.NZB, location);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ModernSpotWithoutReferenceIsUnavailable(string reference)
    {
        var spot = new SpotEx { OldInfo = null, NZB = reference };
        Assert.False(SpotHelper.TryGetDownloadReference(spot, out _, out _));
    }
}
