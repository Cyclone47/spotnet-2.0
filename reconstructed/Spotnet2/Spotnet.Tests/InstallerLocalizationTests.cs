using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Spotnet.Tests;

public sealed class InstallerLocalizationTests
{
    private static string InstallerScript()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "installer", "Spotnet3.iss");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException("Cannot find installer/Spotnet3.iss from the test output.");
    }

    [Fact]
    public void EveryEnglishCustomMessageHasADutchTranslationAndEveryCodeKeyExists()
    {
        string source = InstallerScript();
        string section = Regex.Match(source, @"(?ms)^\[CustomMessages\]\s*(.*?)^\[").Groups[1].Value;
        var messages = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["english"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ["dutch"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
        foreach (Match match in Regex.Matches(section, @"(?m)^(english|dutch)\.([A-Za-z0-9]+)=(.+)$"))
        {
            Assert.False(string.IsNullOrWhiteSpace(match.Groups[3].Value));
            Assert.True(messages[match.Groups[1].Value].Add(match.Groups[2].Value), "Duplicate translation key: " + match.Value);
        }
        Assert.True(messages["english"].Count >= 60, "Too few localized installer strings.");
        Assert.Equal(messages["english"].OrderBy(x => x), messages["dutch"].OrderBy(x => x));
        foreach (Match reference in Regex.Matches(source, @"CM\('([A-Za-z0-9]+)'\)"))
            Assert.Contains(reference.Groups[1].Value, messages["english"]);
        Assert.Contains("Description: \"{cm:LaunchSpotnet}\"", source);
        for (int index = 1; index <= 5; index++) Assert.Contains("CM('Welcome" + index + "')", source);
    }

    [Fact]
    public void LengthyPreparationUsesAVisibleProgressPageAndAlwaysHidesIt()
    {
        string source = InstallerScript();
        Assert.Contains("CreateOutputProgressPage(CM('ProgressTitle'), CM('ProgressDescription'))", source);
        Assert.Contains("ProgressPage.Show", source);
        Assert.Contains("ProgressPage.SetText(CM('StatusProfile'), CM('ProgressDetail'))", source);
        Assert.Matches(@"(?s)try.*finally\s+if ShowProgress then ProgressPage\.Hide", source);
    }
}
