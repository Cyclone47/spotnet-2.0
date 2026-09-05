using System;
using System.IO;
using Spotnet.Remote;
using Xunit;

namespace Spotnet.Tests;

[Collection("Remote configuration")]
public class CloudflareTunnelTests : IDisposable
{
    private readonly string previousFolder = Spotnet.Helpers.AppHelper.SettingsFolder;
    private readonly string testFolder = Path.Combine(Path.GetTempPath(), "CloudflareTunnelTests-" + Guid.NewGuid().ToString("N"));

    public CloudflareTunnelTests()
    {
        Directory.CreateDirectory(testFolder);
        Spotnet.Helpers.AppHelper.SettingsFolder = testFolder;
    }

    public void Dispose()
    {
        Spotnet.Helpers.AppHelper.SettingsFolder = previousFolder;
        if (Directory.Exists(testFolder))
        {
            Directory.Delete(testFolder, true);
        }
    }

    [Fact]
    public void ExtractTunnelUrl_WithValidCloudflareOutput_ExtractsUrlSuccessfully()
    {
        string logLine1 = "2026-09-05T14:15:00Z INF | https://random-words-sample.trycloudflare.com |";
        string url1 = CloudflareTunnelService.ExtractTunnelUrl(logLine1);
        Assert.Equal("https://random-words-sample.trycloudflare.com", url1);

        string logLine2 = "| https://alpha-bravo-charlie.trycloudflare.com |";
        string url2 = CloudflareTunnelService.ExtractTunnelUrl(logLine2);
        Assert.Equal("https://alpha-bravo-charlie.trycloudflare.com", url2);

        string logLineUpper = "Visit at: HTTPS://MY-QUICK-TUNNEL-99.TRYCLOUDFLARE.COM/test";
        string urlUpper = CloudflareTunnelService.ExtractTunnelUrl(logLineUpper);
        Assert.Equal("HTTPS://MY-QUICK-TUNNEL-99.TRYCLOUDFLARE.COM", urlUpper);
    }

    [Fact]
    public void ExtractTunnelUrl_WithUnrelatedLogLine_ReturnsNull()
    {
        Assert.Null(CloudflareTunnelService.ExtractTunnelUrl(null));
        Assert.Null(CloudflareTunnelService.ExtractTunnelUrl(""));
        Assert.Null(CloudflareTunnelService.ExtractTunnelUrl("Starting tunnel connection..."));
        Assert.Null(CloudflareTunnelService.ExtractTunnelUrl("Connected to edge server 198.41.200.100"));
        Assert.Null(CloudflareTunnelService.ExtractTunnelUrl("https://google.com"));
    }

    [Fact]
    public void RemoteConfig_EnableCloudflareTunnel_PersistsCorrectly()
    {
        var config = new RemoteConfig
        {
            Enabled = true,
            EnableCloudflareTunnel = true,
            Port = 8775
        };
        config.Save();

        var loaded = RemoteConfig.Load();
        Assert.True(loaded.Enabled);
        Assert.True(loaded.EnableCloudflareTunnel);
        Assert.Equal(8775, loaded.Port);

        loaded.EnableCloudflareTunnel = false;
        loaded.Save();

        var reloaded = RemoteConfig.Load();
        Assert.False(reloaded.EnableCloudflareTunnel);
    }

    [Fact]
    public void CloudflareTunnelService_InitialState_IsStoppedAndHasPath()
    {
        var service = CloudflareTunnelService.Instance;
        Assert.NotNull(service);
        Assert.True(service.State == TunnelState.Stopped || service.State == TunnelState.Failed);
        
        string exePath = service.GetExecutablePath();
        Assert.False(string.IsNullOrWhiteSpace(exePath));
        Assert.EndsWith("cloudflared.exe", exePath, StringComparison.OrdinalIgnoreCase);
    }
}
