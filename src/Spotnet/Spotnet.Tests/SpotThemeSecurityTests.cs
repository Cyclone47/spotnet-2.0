using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Spotnet.Tests;

/// <summary>
/// Legt vast wat er in de spotthema's mag staan. De spotweergave toont tekst die door
/// onbekenden op Usenet is geschreven, dus wat daar aan JavaScript omheen zit is een
/// beveiligingsafspraak en geen smaakkwestie.
/// </summary>
public sealed class SpotThemeSecurityTests
{
    private static string ThemesRoot()
    {
        DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "Data", "TabThemes");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Data/TabThemes niet gevonden vanaf " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// jQuery 1.8.3 (CVE-2020-11022, CVE-2020-11023, CVE-2015-9251) en Bootstrap 2.2.0
    /// (CVE-2016-10735) zijn uit de thema's verwijderd. Deze test voorkomt dat ze
    /// terugkomen doordat iemand een oud thema kopieert.
    /// </summary>
    [Fact]
    public void NoThemeShipsTheRetiredJavaScriptLibraries()
    {
        string[] banned = { "jquery.js", "bootstrap.min.js", "html5.js" };
        string[] offenders = Directory
            .EnumerateFiles(ThemesRoot(), "*.js", SearchOption.AllDirectories)
            .Where(f => banned.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>Geen enkel thema mag nog naar die bestanden verwijzen.</summary>
    [Fact]
    public void NoThemeReferencesTheRetiredLibraries()
    {
        foreach (string page in Directory.EnumerateFiles(ThemesRoot(), "*.htm", SearchOption.AllDirectories))
        {
            string html = File.ReadAllText(page);
            Assert.DoesNotContain("js/jquery.js", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("js/bootstrap.min.js", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("js/html5.js", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// De thema's die tabs of een accordeon gebruiken, moeten de vervanger meeladen -
    /// anders klapt de spotweergave stil dicht.
    /// </summary>
    [Fact]
    public void EveryThemeThatUsesDataToggleLoadsItsReplacement()
    {
        foreach (string page in Directory.EnumerateFiles(ThemesRoot(), "spot.htm", SearchOption.AllDirectories))
        {
            string html = File.ReadAllText(page);
            if (!html.Contains("data-toggle=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Assert.Contains("js/theme-ui.js", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>De JSONP-aanroep gaat via een echt script-element, niet via een HTML-string.</summary>
    [Fact]
    public void ThemesInjectScriptsThroughTheSafeLoader()
    {
        foreach (string page in Directory.EnumerateFiles(ThemesRoot(), "spot.htm", SearchOption.AllDirectories))
        {
            string html = File.ReadAllText(page);
            Assert.DoesNotContain("jQuery('head').append", html, StringComparison.OrdinalIgnoreCase);
        }
    }
}
