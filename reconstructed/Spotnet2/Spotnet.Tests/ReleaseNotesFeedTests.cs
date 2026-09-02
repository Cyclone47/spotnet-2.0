using System;
using Spotnet.Browser;
using Xunit;

namespace Spotnet.Tests;

/// <summary>
/// Turning the GitHub releases feed into the markup the notes page renders. No network:
/// the API's answer is the input, which is the part that has to survive a bad day.
/// </summary>
public sealed class ReleaseNotesFeedTests
{
    private const string Body = "<p>Something changed.</p>";

    private static string Feed(string extra = "", string name = "Spotnet 3.0.6.2",
        string body = Body, bool draft = false, bool prerelease = false) =>
        $@"[{{
            ""name"": ""{name}"",
            ""tag_name"": ""v3.0.6.2"",
            ""draft"": {draft.ToString().ToLowerInvariant()},
            ""prerelease"": {prerelease.ToString().ToLowerInvariant()},
            ""published_at"": ""2026-09-02T19:10:58Z"",
            ""body_html"": ""{body}""
        }}{extra}]";

    [Fact]
    public void AReleaseBecomesASectionWithItsNameDateAndBody()
    {
        string html = ReleaseNotesFeed.BuildHtml(Feed());
        Assert.Contains("class=\"notes-section gh-release\"", html, StringComparison.Ordinal);
        Assert.Contains("Spotnet 3.0.6.2", html, StringComparison.Ordinal);
        // The month name follows the interface language, so assert the parts that do not:
        // the date is shown, and it is the release's own date rather than today's.
        Assert.Contains("class=\"gh-date\"", html, StringComparison.Ordinal);
        Assert.Contains("2026", html, StringComparison.Ordinal);
        Assert.Contains(Body, html, StringComparison.Ordinal);
    }

    [Fact]
    public void ADraftIsNotShown()
    {
        // A draft is not published to anyone, so it has no place in the notes.
        Assert.Null(ReleaseNotesFeed.BuildHtml(Feed(draft: true)));
    }

    [Fact]
    public void APrereleaseIsShownButMarked()
    {
        string html = ReleaseNotesFeed.BuildHtml(Feed(prerelease: true));
        Assert.Contains("gh-tag", html, StringComparison.Ordinal);
        Assert.Contains("pre-release", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AReleaseWithNoDescriptionIsSkipped()
    {
        Assert.Null(ReleaseNotesFeed.BuildHtml(Feed(body: "")));
    }

    [Fact]
    public void AReleaseWithNoNameFallsBackToItsTag()
    {
        string html = ReleaseNotesFeed.BuildHtml(Feed(name: ""));
        Assert.Contains("v3.0.6.2", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ANameCarryingMarkupIsEscaped()
    {
        // The description is GitHub's own sanitised HTML, but the title is a plain string
        // and has to be escaped where this class writes it into the page.
        string html = ReleaseNotesFeed.BuildHtml(Feed(name: "3.0 <script>x</script>"));
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"message\":\"Not Found\"}")]
    [InlineData("[]")]
    public void AnAnswerThatIsNotAListOfReleasesFallsBackRatherThanShowingNothing(string json)
    {
        // Null is the signal to keep the notes built into the application.
        Assert.Null(ReleaseNotesFeed.BuildHtml(json));
    }

    /// <summary>
    /// Writes the page as the application renders it, for looking at. Off unless
    /// SPOTNET_NOTES_PREVIEW_DIR points somewhere, and it needs the network: it asks
    /// GitHub for the real releases, which is the point of looking.
    /// </summary>
    [Fact]
    public void RenderTheRealPageWhenAskedTo()
    {
        string output = Environment.GetEnvironmentVariable("SPOTNET_NOTES_PREVIEW_DIR");
        if (string.IsNullOrEmpty(output)) return;
        System.IO.Directory.CreateDirectory(output);
        string notes = ReleaseNotesFeed.GetNotesHtml(Spotnet.Properties.Resources.whatsnew);
        string page = Spotnet.Properties.Resources.ReleaseNotes
            .Replace("{VERSION}", "3.0.6.2")
            .Replace("{JAVASCRIPT}", string.Empty)
            .Replace("{RESPONSEURL}", "https://github.com/Cyclone47/spotnet-3.0")
            .Replace("{WHATSNEW}", notes);
        System.IO.File.WriteAllText(System.IO.Path.Combine(output, "ReleaseNotes.css"),
            Spotnet.Properties.Resources.ReleaseNotesCss);
        System.IO.File.WriteAllText(System.IO.Path.Combine(output, "release-notes.html"), page);
    }

    [Fact]
    public void EveryPublishedReleaseIsKept()
    {
        string second = @",{
            ""name"": ""Spotnet 3.0.6.1"", ""tag_name"": ""v3.0.6.1"", ""draft"": false,
            ""prerelease"": false, ""published_at"": ""2026-09-02T19:06:36Z"",
            ""body_html"": ""<p>Earlier.</p>""
        }";
        string html = ReleaseNotesFeed.BuildHtml(Feed(extra: second));
        Assert.Contains("Spotnet 3.0.6.2", html, StringComparison.Ordinal);
        Assert.Contains("Spotnet 3.0.6.1", html, StringComparison.Ordinal);
        // Newest first, the order the API returns them in.
        Assert.True(html.IndexOf("3.0.6.2", StringComparison.Ordinal)
            < html.IndexOf("3.0.6.1", StringComparison.Ordinal));
    }
}
