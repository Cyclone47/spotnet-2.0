using System;
using System.Linq;
using System.Windows;
using MahApps.Metro;
using NLog;
using Spotnet.Properties;

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
                    var baseTheme = isDark ? "BaseDark" : "BaseLight";
                    var appTheme = ThemeManager.GetAppTheme(baseTheme);
                    var accent = ThemeManager.GetAccent("Blue");
                    if (appTheme != null && accent != null)
                    {
                        ThemeManager.ChangeAppStyle(app, accent, appTheme);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("MahApps ChangeAppStyle notice: {0}", ex.Message);
                }

                // 2. Swap our custom theme palette dictionary
                string targetDictName = isDark ? "moderndark.xaml" : "blueedited.xaml";
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
                Log.Info("Applied theme: {0}", themeName);
            });
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
        }
    }
}
