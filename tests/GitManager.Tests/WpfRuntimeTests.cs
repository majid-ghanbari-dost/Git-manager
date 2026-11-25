using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using GitManager.App.Services;
using Xunit;
using WpfApp = GitManager.App.App;
using WpfMainWindow = GitManager.App.MainWindow;

namespace GitManager.Tests;

[Collection("WpfSta")]
public sealed class WpfRuntimeTests
{
    [StaFact]
    public void FrozenSolidColorBrush_ColorMutation_Throws()
    {
        Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F9")!);
        brush.Freeze();
        Assert.True(brush.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => brush.Color = Colors.Black);
    }

    [StaFact]
    public void AppResources_ThemeBrush_MutationThrowsWhenFrozen()
    {
        StaWpf.EnsureApp();
        var brush = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AppBackgroundBrush"]);
        if (brush.IsFrozen)
            Assert.Throws<InvalidOperationException>(() => brush.Color = Colors.Black);
    }

    [StaFact]
    public void SetBrush_ReplacesFrozenResource_DoesNotThrow()
    {
        StaWpf.EnsureApp();
        WpfMainWindow.SetBrush("AppBackgroundBrush", "#111827");
        var brush = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AppBackgroundBrush"]);
        Assert.True(brush.IsFrozen);
        Assert.Equal((Color)ColorConverter.ConvertFromString("#111827")!, brush.Color);
    }

    [StaFact]
    public void DrawingImage_AsWindowIcon_IsRecorded()
    {
        StaWpf.EnsureApp();
        var logo = Assert.IsType<DrawingImage>(Application.Current.Resources["GitManagerLogo"]);
        Exception? caught = null;
        var window = new Window
        {
            Width = 120,
            Height = 80,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            Icon = logo
        };
        try
        {
            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        finally
        {
            try { window.Close(); } catch { }
        }

        if (caught is not null)
            StartupLog.Write("StartupException", "DrawingImage-as-Icon", caught);
    }

    [StaFact]
    public void MainWindow_Show_ReachesLoaded_AndCloses()
    {
        StaWpf.EnsureApp();
        var workspace = Path.Combine(Path.GetTempPath(), "GitManagerWpfRuntimeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        var settingsPath = AppLocalState.SettingsPath;
        string? previous = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
        try
        {
            new SettingsService().SaveWorkspaceAsync(workspace).GetAwaiter().GetResult();
            var loaded = false;
            var window = new WpfMainWindow
            {
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 0,
                Top = 0
            };
            window.Loaded += (_, _) => loaded = true;
            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
            Assert.True(loaded, "MainWindow.Loaded did not fire.");
            Assert.True(File.Exists(StartupLog.FilePath), "startup.log was not created.");
            var log = File.ReadAllText(StartupLog.FilePath);
            Assert.Contains("MainWindowLoaded", log, StringComparison.Ordinal);
            window.Close();
        }
        finally
        {
            if (previous is null)
            {
                if (File.Exists(settingsPath)) File.Delete(settingsPath);
            }
            else File.WriteAllText(settingsPath, previous);
            try { Directory.Delete(workspace, recursive: true); } catch { }
        }
    }
}

[CollectionDefinition("WpfSta", DisableParallelization = true)]
public sealed class WpfStaCollection;

internal static class StaWpf
{
    public static WpfApp EnsureApp()
    {
        if (Application.Current is WpfApp existing)
        {
            existing.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            return existing;
        }

        if (Application.Current is not null)
            throw new InvalidOperationException($"Unexpected Application type: {Application.Current.GetType().FullName}");

        var app = new WpfApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        return app;
    }
}
