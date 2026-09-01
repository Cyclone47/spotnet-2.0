using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Spotnet.Helpers;
using Xunit;

namespace Spotnet.Tests;

[CollectionDefinition("WPF menu resources", DisableParallelization = true)]
public sealed class MenuThemeCollection { }

[Collection("WPF menu resources")]
public sealed class MenuThemeTests
{
    private static ResourceDictionary Load(string path) => new ResourceDictionary
    {
        Source = new Uri("pack://application:,,,/" + path, UriKind.Absolute)
    };

    private static void Layout(FrameworkElement element)
    {
        element.ApplyTemplate();
        element.Measure(new Size(800, 1200));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
    }

    private static Color ColorOf(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    private static double Luminance(Color color)
    {
        double Channel(byte value)
        {
            double v = value / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static void Readable(Brush foreground, Brush background)
    {
        Color fg = ColorOf(foreground), bg = ColorOf(background);
        Assert.Equal(255, fg.A);
        Assert.Equal(255, bg.A);
        double a = Luminance(fg), b = Luminance(bg);
        double contrast = (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        Assert.True(contrast >= 4.5, $"Unreadable menu colors {fg} on {bg}: {contrast:F2}:1");
    }

    private static void Highlight(MenuItem item, bool value) => typeof(MenuItem)
        .GetProperty(nameof(MenuItem.IsHighlighted)).GetSetMethod(true).Invoke(item, new object[] { value });

    private static Border PopupSurface(MenuItem item)
    {
        item.ApplyTemplate();
        var popup = Assert.IsType<Popup>(item.Template.FindName("PART_Popup", item));
        Assert.True(popup.Child is Border, $"{item.Header}: wrong popup {popup.Child?.GetType().Name}; style source={DependencyPropertyHelper.GetValueSource(item, FrameworkElement.StyleProperty).BaseValueSource}; template source={DependencyPropertyHelper.GetValueSource(item, Control.TemplateProperty).BaseValueSource}; expected style={ReferenceEquals(item.Style, Application.Current.FindResource(typeof(MenuItem)))}; expected template={ReferenceEquals(item.Template, Application.Current.FindResource("SpotnetMenuItemRow"))}; style setters={item.Style?.Setters.Count}");
        var border = Assert.IsType<Border>(popup.Child);
        Layout(border); // Real generated submenu containers, without showing a desktop window.
        return border;
    }

    private static void CheckRow(MenuItem item, Brush background)
    {
        item.ApplyTemplate();
        var row = Assert.IsType<Border>(item.Template.FindName("RowBorder", item));
        Assert.Equal(ColorOf(background), ColorOf(row.Background));
        Assert.Equal(1.0, item.Opacity);
        Readable(item.Foreground, row.Background);
        var shortcut = Assert.IsType<TextBlock>(item.Template.FindName("ShortcutText", item));
        Assert.Equal(ColorOf(item.Foreground), ColorOf(shortcut.Foreground));
        var arrow = Assert.IsType<System.Windows.Shapes.Path>(item.Template.FindName("SubmenuArrow", item));
        Assert.Equal(ColorOf(item.Foreground), ColorOf(arrow.Fill));
        var check = Assert.IsType<System.Windows.Shapes.Path>(item.Template.FindName("Checkmark", item));
        Assert.Equal(ColorOf(item.Foreground), ColorOf(check.Stroke));
        Assert.Equal(item.IsChecked ? Visibility.Visible : Visibility.Collapsed, check.Visibility);
    }

    private static void SavePreview(MenuItem parent, string theme)
    {
        string output = Environment.GetEnvironmentVariable("SPOTNET_MENU_PREVIEW_DIR");
        if (string.IsNullOrEmpty(output)) return;
        Border border = PopupSurface(parent);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(border.ActualWidth), (int)Math.Ceiling(border.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(border);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(output);
        using (var stream = File.Create(System.IO.Path.Combine(output, "menu-" + theme + ".png"))) encoder.Save(stream);
    }

    [Fact]
    public void MenusSwitchLiveAndNewContextMenusUseTheCurrentTheme()
    {
        Exception error = null;
        var thread = new Thread(() =>
        {
            Application app = null;
            try
            {
                app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                // Match the production merge order, without starting Spotnet or loading a user profile.
                app.Resources.MergedDictionaries.Add(Load("MahApps.Metro;component/Styles/Controls.xaml"));
                app.Resources.MergedDictionaries.Add(Load("MahApps.Metro;component/Styles/Fonts.xaml"));
                app.Resources.MergedDictionaries.Add(Load("MahApps.Metro;component/Styles/Themes/Light.Blue.xaml"));
                app.Resources.MergedDictionaries.Add(Load("Spotnet;component/Style/MainMenuStyle.xaml"));
                var palette = Load("Spotnet;component/Style/classiclight.xaml");
                app.Resources.MergedDictionaries.Add(palette);

                var menu = new Menu();
                var parent = new MenuItem { Header = "_Settings" };
                var normal = new MenuItem { Header = "Provider settings", InputGestureText = "Ctrl+P" };
                var hovered = new MenuItem { Header = "Download folder", InputGestureText = "Ctrl+D" };
                var disabled = new MenuItem { Header = "Database update in progress", IsEnabled = false };
                var checkable = new MenuItem { Header = "Modern Dark", IsCheckable = true, IsChecked = true };
                var nested = new MenuItem { Header = "View options" };
                var deep = new MenuItem { Header = "Columns" };
                var leaf = new MenuItem { Header = "Show category", IsCheckable = true, IsChecked = true };
                var separator = new Separator();
                parent.Items.Add(normal);
                parent.Items.Add(hovered);
                parent.Items.Add(disabled);
                parent.Items.Add(separator);
                parent.Items.Add(checkable);
                parent.Items.Add(nested);
                nested.Items.Add(deep);
                deep.Items.Add(leaf);
                menu.Items.Add(parent);
                var toolbar = new ToolBar { Style = (Style)app.FindResource("SpotnetToolBarStyle") };
                toolbar.Items.Add(menu);
                // Application resource-change notifications are delivered through windows.
                // This host is never shown and never opens the real application/profile.
                var host = new Window { Content = toolbar, Width = 800, Height = 400 };

                foreach (string theme in new[] { "classiclight", "moderndark", "classiclight", "moderndark" })
                {
                    app.Resources.MergedDictionaries.Remove(palette);
                    palette = Load("Spotnet;component/Style/" + theme + ".xaml");
                    app.Resources.MergedDictionaries.Add(palette);
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    Brush background = (Brush)palette["SpotnetMenuBackground"];
                    Brush highlighted = (Brush)palette["SpotnetMenuHighlightBackground"];
                    Layout(host);
                    Layout(toolbar);
                    Assert.True(menu.ItemContainerStyleSelector is MenuItemStyleSelector,
                        $"Toolbar Menu style source={DependencyPropertyHelper.GetValueSource(menu, FrameworkElement.StyleProperty).BaseValueSource}; selector={menu.ItemContainerStyleSelector}; container style={menu.ItemContainerStyle}");
                    Assert.Equal(ColorOf(background), ColorOf(PopupSurface(parent).Background));
                    Highlight(hovered, true);
                    CheckRow(parent, background);
                    CheckRow(normal, background);
                    CheckRow(hovered, highlighted);
                    CheckRow(disabled, background);
                    CheckRow(checkable, background);
                    Assert.Equal(ColorOf(background), ColorOf(PopupSurface(nested).Background));
                    Assert.Equal(ColorOf(background), ColorOf(PopupSurface(deep).Background));
                    CheckRow(nested, background);
                    CheckRow(deep, background);
                    CheckRow(leaf, background);
                    Assert.Equal(ColorOf((Brush)palette["SpotnetMenuBorder"]), ColorOf(separator.Background));
                    // The spot/download/filter handlers construct a new detached menu on demand.
                    var context = new ContextMenu { Resources = AppHelper.GetMenuResourceDictionary };
                    var contextItem = new MenuItem { Header = "Download spot", InputGestureText = "Enter" };
                    context.Items.Add(contextItem);
                    // Simulate colliding library aliases; our namespaced palette must win.
                    context.Resources["WhiteColorBrush"] = Brushes.White;
                    context.Resources["BlackBrush"] = Brushes.White;
                    Layout(context);
                    Assert.Equal(ColorOf(background), ColorOf(context.Background));
                    CheckRow(contextItem, background);
                    SavePreview(parent, theme);
                }
            }
            catch (Exception ex) { error = ex; }
            finally { app?.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF menu test timed out.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
