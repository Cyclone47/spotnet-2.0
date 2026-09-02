using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Renders one preview bitmap per style for the Setup wizard's style page.
//
// The previews are drawn from the application's own theme dictionaries rather than
// hand-painted, so a palette edit in Style/*.xaml shows up in Setup on the next
// build instead of quietly drifting out of date. build-installer.ps1 runs this.
//
// Output is 24-bit BMP: Inno Setup's TBitmapImage loads BMP directly, and going
// through PNG would only add a decoder dependency for no visible gain.
internal static class Program
{
    // A font packed in another assembly only resolves through the base-URI +
    // relative-name constructor; the single-string pack URI silently falls back
    // to a default face, which is why the first render showed boxes.
    private static readonly FontFamily FontAwesome =
        new(new Uri("pack://application:,,,/Spotnet;component/"), "./Resources/#FontAwesome");

    // Width/height of one preview tile, in device-independent pixels. The wizard
    // shows three of these side by side on a standard 417px-wide Inno page.
    private const int TileWidth = 128;
    private const int TileHeight = 132;

    // Rendered at 2x so the bitmap still looks clean on a 125%/150% display.
    private const double Scale = 2.0;

    private sealed record Style(string Key, string Dictionary, bool Dark, string FileName);

    private static readonly Style[] Styles =
    {
        new("ModernLight", "ModernLight.xaml", false, "style-modern-light.bmp"),
        new("ModernDark",  "ModernDark.xaml",  true,  "style-modern-dark.bmp"),
        new("ClassicLight","ClassicLight.xaml",false, "style-classic.bmp"),
    };

    // The rows each preview shows, taken from the filter set a new install starts
    // with: the artwork Classic draws, and the FontAwesome glyph Modern swaps in for
    // it. Showing the same four filters both ways is the whole point of the page.
    private static readonly (string Icon, string Glyph, string Label, bool Selected)[] Rows =
    {
        ("2/Overzicht.png",                          "", "Overzicht", false),
        ("cc/48px-Crystal_Clear_app_camera.png",     "", "Beeld",     true),
        ("cc/48px-Crystal_Clear_app_mp3.png",        "", "Muziek",    false),
        ("cc/48px-Crystal_Clear_device_pda_blue.png","", "Boeken",    false),
    };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string outputDirectory = null;
            string iconDirectory = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--output") outputDirectory = args[i + 1];
                if (args[i] == "--icons") iconDirectory = args[i + 1];
            }

            if (outputDirectory == null)
            {
                Console.Error.WriteLine("Usage: Spotnet.ThemePreview --output <dir> [--icons <dir>]");
                return 2;
            }

            Directory.CreateDirectory(outputDirectory);
            var app = new Application();

            foreach (var style in Styles)
            {
                LoadTheme(app, style);
                var tile = Build(style, iconDirectory);
                var path = Path.Combine(outputDirectory, style.FileName);
                Save(tile, path);
                Console.WriteLine("wrote " + path);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    /// <summary>
    /// Rebuilds the resource stack for one style, matching the order ThemeHelper uses
    /// at runtime so the previews resolve exactly the brushes the app would.
    /// </summary>
    private static void LoadTheme(Application app, Style style)
    {
        var merged = app.Resources.MergedDictionaries;
        merged.Clear();
        foreach (var source in new[]
        {
            "pack://application:,,,/MahApps.Metro;component/Styles/Controls.xaml",
            "pack://application:,,,/MahApps.Metro;component/Styles/Fonts.xaml",
            "pack://application:,,,/MahApps.Metro;component/Styles/Themes/" + (style.Dark ? "Dark.Blue" : "Light.Blue") + ".xaml",
            "pack://application:,,,/Spotnet;component/Style/Fonts.xaml",
            "pack://application:,,,/Spotnet;component/Style/Shared.xaml",
            "pack://application:,,,/Spotnet;component/Style/" + style.Dictionary,
        })
        {
            merged.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Absolute) });
        }
    }

    private static Brush Brush(string key, string fallback)
    {
        var found = Application.Current.TryFindResource(key) as Brush;
        return found ?? (Brush)new BrushConverter().ConvertFromString(fallback);
    }

    private static FrameworkElement Build(Style style, string iconDirectory)
    {
        var titleBackground = Brush("WindowTitleColorBrush", "#FF2B579A");
        var titleForeground = Brush("WindowTitleForegroundBrush", "#FFFFFFFF");
        var panel = Brush("AccentColor4BackgroundBrush", "#FFEFF7FF");
        var surface = Brush("SpotBackgroundBrush", "#FFFFFFFF");
        var text = Brush("BlackColorBrush", "#FF000000");
        var accent = Brush("AccentColorBrush", "#FF327ECA");
        var onAccent = Brush("IdealForegroundColorBrush", "#FFFFFFFF");
        var selectedBackground = Brush("TreeSelectedBackgroundBrush", "#55327ECA");
        var selectedForeground = Brush("TreeSelectedForegroundBrush", "#FF000000");
        var tabActive = Brush("BackgroundSelected", "#FFFFFFFF");
        var tabIdle = Brush("BackgroundNotSelected", "#FFE5F0FE");
        var tabActiveText = Brush("TabSelectedForegroundBrush", "#FF000000");
        var underline = Brush("TabUnderlineBrush", "Transparent");

        var root = new Border
        {
            Width = TileWidth,
            Height = TileHeight,
            Background = surface,
            BorderBrush = Brush("GrayBrush7", "#FFE5E5E5"),
            BorderThickness = new Thickness(1),
            SnapsToDevicePixels = true,
        };

        var rows = new Grid();
        rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // title bar
        rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // tab strip
        rows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // --- title bar -------------------------------------------------------
        var title = new Border { Background = titleBackground, Padding = new Thickness(6, 3, 6, 3) };
        title.Child = new TextBlock
        {
            Text = "SPOTNET",
            Foreground = titleForeground,
            FontSize = 7.5,
            FontWeight = FontWeights.SemiBold,
        };
        Grid.SetRow(title, 0);
        rows.Children.Add(title);

        // --- tab strip -------------------------------------------------------
        var tabs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = panel,
        };
        tabs.Children.Add(Tab("Spots", true, tabActive, tabActiveText, underline));
        tabs.Children.Add(Tab("Downloads", false, tabIdle, text, underline));
        Grid.SetRow(tabs, 1);
        rows.Children.Add(tabs);

        // --- filter tree -----------------------------------------------------
        var body = new Border { Background = panel, Padding = new Thickness(4) };
        var list = new StackPanel();

        var header = new Border
        {
            Background = accent,
            Padding = new Thickness(4, 2, 4, 2),
            CornerRadius = new CornerRadius(2, 2, 0, 0),
        };
        header.Child = new TextBlock
        {
            Text = "FILTERS",
            Foreground = onAccent,
            FontSize = 6.5,
            FontWeight = FontWeights.SemiBold,
        };
        list.Children.Add(header);

        var tree = new Border
        {
            Background = surface,
            BorderBrush = Brush("GrayBrush7", "#FFE5E5E5"),
            BorderThickness = new Thickness(1, 0, 1, 1),
            Padding = new Thickness(2),
        };
        var treeRows = new StackPanel();
        foreach (var (icon, glyph, label, selected) in Rows)
        {
            treeRows.Children.Add(Row(style, icon, glyph, label, selected, iconDirectory,
                text, accent, selectedBackground, selectedForeground));
        }
        tree.Child = treeRows;
        list.Children.Add(tree);

        body.Child = list;
        Grid.SetRow(body, 2);
        rows.Children.Add(body);

        root.Child = rows;
        return root;
    }

    private static UIElement Tab(string label, bool active, Brush background, Brush foreground, Brush underline)
    {
        return new Border
        {
            Background = background,
            Padding = new Thickness(6, 3, 6, 2),
            Margin = new Thickness(2, 2, 0, 0),
            CornerRadius = new CornerRadius(2, 2, 0, 0),
            BorderBrush = active ? underline : Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, active ? 1.5 : 0),
            Child = new TextBlock
            {
                Text = label,
                Foreground = foreground,
                FontSize = 6.5,
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            },
        };
    }

    private static UIElement Row(Style style, string icon, string rowGlyph, string label, bool selected, string iconDirectory,
        Brush text, Brush accent, Brush selectedBackground, Brush selectedForeground)
    {
        var foreground = selected ? selectedForeground : text;
        var content = new StackPanel { Orientation = Orientation.Horizontal };

        // Classic keeps the bitmap; the Modern styles swap in the FontAwesome glyph.
        // Showing that difference is the point of the preview.
        string glyph = style.Key == "ClassicLight" ? null : rowGlyph;
        if (glyph != null)
        {
            content.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = FontAwesome,
                FontSize = 11,
                Foreground = selected ? selectedForeground : accent,
                Width = 16,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        else
        {
            var image = LoadIcon(iconDirectory, icon);
            content.Children.Add(image != null
                ? new Image
                {
                    Source = image,
                    Width = 14,
                    Height = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(1, 0, 1, 0),
                }
                : (UIElement)new Border { Width = 16 });
        }

        content.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = foreground,
            FontSize = 9,
            Margin = new Thickness(3, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
        });

        return new Border
        {
            Background = selected ? selectedBackground : Brushes.Transparent,
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(3, 2.5, 3, 2.5),
            Child = content,
        };
    }


    private static BitmapImage LoadIcon(string iconDirectory, string name)
    {
        if (iconDirectory == null) return null;
        var path = Path.Combine(iconDirectory, name.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return null;
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 32;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static void Save(FrameworkElement element, string path)
    {
        element.Measure(new Size(TileWidth, TileHeight));
        element.Arrange(new Rect(0, 0, TileWidth, TileHeight));
        element.UpdateLayout();

        var target = new RenderTargetBitmap(
            (int)Math.Round(TileWidth * Scale), (int)Math.Round(TileHeight * Scale),
            96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        target.Render(element);

        // Inno's TBitmapImage wants a plain opaque bitmap, so flatten to 24-bit.
        var opaque = new FormatConvertedBitmap(target, PixelFormats.Bgr24, null, 0);
        var encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(opaque));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
