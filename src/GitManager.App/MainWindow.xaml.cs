using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
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
