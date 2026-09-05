using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using Xunit;

namespace Spotnet.Tests;

/// <summary>
/// Dezelfde reden als bij <see cref="SettingsForCommunityTests"/>: een pagina die niet
/// construeert verschijnt als een leeg vlak zonder logregel. Deze tests bouwen hem direct.
/// Daarnaast leggen ze vast wat de bedoeling van deze pagina is: leeg betekent uit.
/// </summary>
[Collection("Community pane")]
public sealed class SettingsForIntegrationsTests : IDisposable
{
    private readonly string previousFolder = Spotnet.Helpers.AppHelper.SettingsFolder;
    private readonly string testFolder =
        Path.Combine(Path.GetTempPath(), "IntegrationsPaneTests-" + Guid.NewGuid().ToString("N"));

    public SettingsForIntegrationsTests()
    {
        Directory.CreateDirectory(testFolder);
        Spotnet.Helpers.AppHelper.SettingsFolder = testFolder;
        Spotnet.Community.CommunityConfig.Invalidate();
    }

    public void Dispose()
    {
        Spotnet.Helpers.AppHelper.SettingsFolder = previousFolder;
        Spotnet.Community.CommunityConfig.Invalidate();
        if (Directory.Exists(testFolder))
        {
            Directory.Delete(testFolder, true);
        }
    }

    private static void OnUiThread(Action body)
    {
        Exception error = null;
        Thread thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    [Fact]
    public void ThePaneConstructs()
    {
        OnUiThread(() =>
        {
            Spotnet.Controls.SettingsForIntegrations pane = new Spotnet.Controls.SettingsForIntegrations();
            Assert.NotNull(pane.FindName("NewznabUrlTextBox"));
            Assert.NotNull(pane.FindName("OmdbKeyTextBox"));
        });
    }

    [Fact]
    public void EveryIntegrationStartsOutEmptyAndDisabled()
    {
        OnUiThread(() =>
        {
            Spotnet.Controls.SettingsForIntegrations pane = new Spotnet.Controls.SettingsForIntegrations();
            TextBox url = (TextBox)pane.FindName("NewznabUrlTextBox");
            TextBlock newznabStatus = (TextBlock)pane.FindName("NewznabStatusTextBlock");
            TextBlock omdbStatus = (TextBlock)pane.FindName("OmdbStatusTextBlock");

            Assert.Equal("", url.Text);
            Assert.Equal("Uitgeschakeld", newznabStatus.Text);
            Assert.Equal("Uitgeschakeld", omdbStatus.Text);
        });
    }

    [Fact]
    public void AKeyWithoutAServerStaysDisabled()
    {
        OnUiThread(() =>
        {
            Spotnet.Controls.SettingsForIntegrations pane = new Spotnet.Controls.SettingsForIntegrations();
            TextBox key = (TextBox)pane.FindName("NewznabKeyTextBox");
            TextBlock status = (TextBlock)pane.FindName("NewznabStatusTextBlock");

            key.IsReadOnly = false;
            key.Text = "mijnsleutel";

            Assert.StartsWith("Uitgeschakeld", status.Text);
        });
    }

    [Fact]
    public void AServerAndAKeyTogetherEnableTheIndexer()
    {
        OnUiThread(() =>
        {
            Spotnet.Controls.SettingsForIntegrations pane = new Spotnet.Controls.SettingsForIntegrations();
            TextBox url = (TextBox)pane.FindName("NewznabUrlTextBox");
            TextBox key = (TextBox)pane.FindName("NewznabKeyTextBox");
            TextBlock status = (TextBlock)pane.FindName("NewznabStatusTextBlock");

            key.IsReadOnly = false;
            key.Text = "mijnsleutel";
            url.Text = "https://idx.example";

            Assert.Equal("Ingeschakeld", status.Text);
        });
    }

    [Fact]
    public void ThePaneAcceptsItsOwnValuesBack()
    {
        OnUiThread(() =>
        {
            Spotnet.Controls.SettingsForIntegrations pane = new Spotnet.Controls.SettingsForIntegrations();
            Assert.True(pane.VerifyFields());
        });
    }
}
