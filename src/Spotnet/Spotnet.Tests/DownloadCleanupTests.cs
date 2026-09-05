using System;
using System.IO;
using System.Threading;
using Spotnet.Downloader.PostProcessing;
using Xunit;

namespace Spotnet.Tests;

public class DownloadCleanupTests
{
    [Fact]
    public void NormalizesAndDeduplicatesExtensions()
    {
        Assert.Equal(new[] { "1", "jpg", "txt" }, DownloadCleanup.Parse(".TXT, jpg; txt\n.1"));
        Assert.Throws<FormatException>(() => DownloadCleanup.Parse("txt, *"));
        Assert.Throws<FormatException>(() => DownloadCleanup.Parse("../txt"));
        Assert.Throws<FormatException>(() => DownloadCleanup.Parse("..txt"));
    }

    [Fact]
    public void RemovesOnlySelectedExtensionsIncludingNestedFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "SpotnetCleanupTests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        try
        {
            foreach (string name in new[] { "movie.mkv", "movie.srt", "part.001", "readme.TXT", "nested/link.url" })
                File.WriteAllText(Path.Combine(root, name), "test");
            DownloadCleanup.Run(root, "", CancellationToken.None, _ => { });
            Assert.True(File.Exists(Path.Combine(root, "readme.TXT")));
            Assert.Throws<OperationCanceledException>(() =>
                DownloadCleanup.Run(root, "txt", new CancellationToken(true), _ => { }));
            Assert.True(File.Exists(Path.Combine(root, "readme.TXT")));
            DownloadCleanup.Run(root, "txt,url", CancellationToken.None, _ => { });
            Assert.False(File.Exists(Path.Combine(root, "readme.TXT")));
            Assert.False(File.Exists(Path.Combine(root, "nested/link.url")));
            foreach (string name in new[] { "movie.mkv", "movie.srt", "part.001" })
                Assert.True(File.Exists(Path.Combine(root, name)));
        }
        finally { Directory.Delete(root, true); }
    }
}
