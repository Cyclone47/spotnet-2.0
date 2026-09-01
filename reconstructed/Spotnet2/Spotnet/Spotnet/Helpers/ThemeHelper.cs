using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using MahApps.Metro;
using NLog;
using Spotnet.Model;
using Spotnet.Properties;
using Spotnet.Utilities;

namespace Spotnet.Helpers;

public static class ThemeHelper
{
    public const string ClassicLight = "ClassicLight";
    public const string ModernDark = "ModernDark";

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static event Action ThemeChanged;

    public static string CurrentTheme => Settings.Default.AppTheme ?? ClassicLight;

    public static bool IsModernDark => string.Equals(CurrentTheme, ModernDark, StringComparison.OrdinalIgnoreCase);

    public static void Initialize()
    {
        try
        {
            string savedTheme = Settings.Default.AppTheme;
            if (string.IsNullOrWhiteSpace(savedTheme))
            {
                savedTheme = ClassicLight;
            }

            ApplyTheme(savedTheme, persist: false);
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
        }
    }

    public static void ApplyTheme(string themeName, bool persist = true)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            themeName = ClassicLight;
        }

        bool isDark = string.Equals(themeName, ModernDark, StringComparison.OrdinalIgnoreCase);

        if (persist)
        {
            Settings.Default.AppTheme = isDark ? ModernDark : ClassicLight;
            Settings.Default.Save();
        }

        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var app = Application.Current;
                if (app == null) return;

                // 1. Swap MahApps Metro BaseDark / BaseLight theme
                try
                {
                    // MahApps 2 replaced the separate accent and base theme with one
                    // named theme, and moved the manager itself into ControlzEx.
                    ControlzEx.Theming.ThemeManager.Current.ChangeTheme(
                        app, isDark ? "Dark.Blue" : "Light.Blue");
                }
                catch (Exception ex)
                {
                    Log.Warn("MahApps ChangeAppStyle notice: {0}", ex.Message);
                }

                // 2. Swap our custom theme palette dictionary.
                //
                // The light theme loads classiclight.xaml, not blueedited.xaml. The latter
                // defines only 31 of the 51 keys the controls reference - it is missing
                // BackgroundSelected, BackgroundNotSelected, SpotBackgroundBrush and the
                // whole GrayBrush set. Those resolved to nothing after a theme switch, so
                // the selected tab lost its background while keeping a white foreground
                // and became unreadable. classiclight.xaml is the complete counterpart to
                // moderndark.xaml and defines every key.
                string targetDictName = isDark ? "moderndark.xaml" : "classiclight.xaml";
                var merged = app.Resources.MergedDictionaries;

                // Remove existing theme dictionaries
                var existing = merged.Where(d =>
                    d.Source != null && (
                        d.Source.OriginalString.IndexOf("moderndark.xaml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        d.Source.OriginalString.IndexOf("classiclight.xaml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        d.Source.OriginalString.IndexOf("blueedited.xaml", StringComparison.OrdinalIgnoreCase) >= 0
                    )).ToList();

                foreach (var d in existing)
                {
                    merged.Remove(d);
                }

                // Insert the new theme dictionary
                var newDict = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/Spotnet;component/Style/{targetDictName}", UriKind.Absolute)
                };
                merged.Add(newDict);

                // 3. Update active windows
                foreach (Window win in app.Windows)
                {
                    try
                    {
                        var winMerged = win.Resources.MergedDictionaries;
                        var winExisting = winMerged.Where(d =>
                            d.Source != null && (
                                d.Source.OriginalString.IndexOf("moderndark.xaml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                d.Source.OriginalString.IndexOf("classiclight.xaml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                d.Source.OriginalString.IndexOf("blueedited.xaml", StringComparison.OrdinalIgnoreCase) >= 0
                            )).ToList();

                        foreach (var d in winExisting)
                        {
                            winMerged.Remove(d);
                        }

                        winMerged.Add(new ResourceDictionary
                        {
                            Source = new Uri($"pack://application:,,,/Spotnet;component/Style/{targetDictName}", UriKind.Absolute)
                        });

                        win.InvalidateVisual();
                    }
                    catch
                    {
                        // Ignore per-window refresh errors
                    }
                }

                ThemeChanged?.Invoke();

                // Refresh spots list rows and reload open spot pages so they re-render immediately
                try
                {
                    SpotParser.ResetThemeFiles();
                    Sys.MainWindow?.RefreshSpotsList(force: true);
                    Sys.MainWindow?.ReloadAllSpotPages();
                }
                catch { }

                Log.Info("Applied theme: {0}", themeName);
            });
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
        }
    }
}
