using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GitManager.App.Services;
using GitManager.App.ViewModels;

namespace GitManager.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly ThemePreferenceService _themePreferences = new();
    private bool _loadingTheme;

    public MainWindow()
    {
        StartupLog.Write("MainWindowCtor");
        InitializeComponent();
        StartupLog.Write("MainWindowInitializeComponent");
        try
        {
            var icon = LoadWindowIcon();
            if (icon is not null) Icon = icon;
            else StartupLog.Write("WindowIcon", "no embedded or file ico; window has no Icon");
        }
        catch (Exception ex)
        {
            StartupLog.Write("StartupException", "Window.Icon", ex);
        }
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }



    /// <summary>
    /// Pack URIs can throw in single-file publish because Assembly.Location is empty.
    /// Load the Win32 ICO from an embedded stream or a file next to the executable.
    /// </summary>
    internal static ImageSource? LoadWindowIcon()
    {
        var assembly = typeof(MainWindow).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("GitManager.ico", StringComparison.OrdinalIgnoreCase));
        if (resource is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is not null) return FrameFromStream(stream);
        }

        foreach (var candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "Assets", "GitManager.ico"),
                     Path.Combine(AppContext.BaseDirectory, "GitManager.ico")
                 })
        {
            if (!File.Exists(candidate)) continue;
            using var stream = File.OpenRead(candidate);
            return FrameFromStream(stream);
        }

        return null;
    }

    private static BitmapFrame FrameFromStream(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        var frame = BitmapDecoder.Create(copy, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        if (frame.CanFreeze) frame.Freeze();
        return frame;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartupLog.Write("MainWindowLoaded");
        try
        {
            await ApplySavedThemeAsync();
        }
        catch (Exception ex)
        {
            StartupLog.Write("StartupException", "ApplySavedThemeAsync", ex);
        }

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            StartupLog.Write("StartupException", "InitializeAsync", ex);
        }
    }

    private async Task ApplySavedThemeAsync()
    {
        _loadingTheme = true;
        try
        {
            var mode = await _themePreferences.LoadAsync();
            ThemeSelector.SelectedIndex = mode switch { "Light" => 1, "Dark" => 2, _ => 0 };
            ApplyTheme(mode);
        }
        finally
        {
            _loadingTheme = false;
        }
    }

    private async void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingTheme || ThemeSelector.SelectedIndex < 0) return;
        var mode = ThemeSelector.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" };
        try
        {
            ApplyTheme(mode);
            await _themePreferences.SaveAsync(mode);
        }
        catch (Exception ex)
        {
            StartupLog.Write("StartupException", "ThemeSelector_SelectionChanged", ex);
        }
    }

    private void ApplyTheme(string mode)
    {
        var registryValue = Microsoft.Win32.Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1);
        var systemDark = registryValue is int value && value == 0;
        var dark = mode == "Dark" || (mode == "System" && systemDark);
        SetBrush("AppBackgroundBrush", dark ? "#111827" : "#F4F6F9");
        SetBrush("SurfaceBrush", dark ? "#1F2937" : "#FFFFFF");
        SetBrush("PrimaryTextBrush", dark ? "#F3F4F6" : "#203247");
        SetBrush("SecondaryTextBrush", dark ? "#AAB6C5" : "#66788A");
        SetBrush("InputBackgroundBrush", dark ? "#273449" : "#FFFFFF");
        SetBrush("InputForegroundBrush", dark ? "#F3F4F6" : "#203247");
        SetBrush("GridLineBrush", dark ? "#374151" : "#E3E8EF");
    }

    internal static void SetBrush(string key, string color)
    {
        if (Application.Current is null)
            throw new InvalidOperationException("Theme resources require a WPF Application.");
        var converted = ColorConverter.ConvertFromString(color);
        if (converted is not Color parsed)
            throw new InvalidOperationException($"Invalid theme color '{color}'.");
        var brush = new SolidColorBrush(parsed);
        if (brush.CanFreeze) brush.Freeze();
        Application.Current.Resources[key] = brush;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        if (e.ClickCount == 2)
            ToggleMaximize();
        else
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}

/// <summary>
/// STA testhost overrode CenterScreen with Manual. Production XAML still uses
/// CenterScreen, which can throw when Session 0 reports no usable screen.
/// </summary>
internal static class WindowStartupPlacement
{
    public static void Apply(Window window)
    {
        try
        {
            var width = SystemParameters.PrimaryScreenWidth;
            var height = SystemParameters.PrimaryScreenHeight;
            StartupLog.Write("WindowPlacement", $"screen={width}x{height}; location={window.WindowStartupLocation}");
            if (width <= 0 || height <= 0 || double.IsNaN(width) || double.IsNaN(height) ||
                double.IsInfinity(width) || double.IsInfinity(height))
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = 0;
                window.Top = 0;
            }
        }
        catch (Exception ex)
        {
            StartupLog.Write("WindowPlacement", "SystemParameters failed; using Manual", ex);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = 0;
            window.Top = 0;
        }
    }
}
