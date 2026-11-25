using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using GitManager.App.Services;
using Xunit;
using WpfApp = GitManager.App.App;
using WpfMainWindow = GitManager.App.MainWindow;

namespace GitManager.Tests;

public sealed class WpfRuntimeTests
{
    [Fact]
    public void FrozenSolidColorBrush_ColorMutation_Throws() => StaWpf.Run(() =>
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F9")!);
        brush.Freeze();
        Assert.True(brush.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => brush.Color = Colors.Black);
    });

    [Fact]
    public void AppResources_ThemeBrush_MutationThrowsWhenFrozen() => StaWpf.Run(() =>
    {
        StaWpf.EnsureApp();
        var brush = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AppBackgroundBrush"]);
        if (brush.IsFrozen)
            Assert.Throws<InvalidOperationException>(() => brush.Color = Colors.Black);
    });

    [Fact]
    public void SetBrush_ReplacesFrozenResource_DoesNotThrow() => StaWpf.Run(() =>
    {
        StaWpf.EnsureApp();
        WpfMainWindow.SetBrush("AppBackgroundBrush", "#111827");
        var brush = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AppBackgroundBrush"]);
        Assert.True(brush.IsFrozen);
        Assert.Equal((Color)ColorConverter.ConvertFromString("#111827")!, brush.Color);
    });

    [Fact]
    public void DrawingImage_AsWindowIcon_IsRecorded() => StaWpf.Run(() =>
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
            StaWpf.Pump();
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
    });

    [Fact]
    public void MainWindow_Show_ReachesLoaded_AndCloses() => StaWpf.Run(() =>
    {
        StaWpf.EnsureApp();
        var workspace = Path.Combine(Path.GetTempPath(), "GitManagerWpfRuntimeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        var settingsPath = AppLocalState.SettingsPath;
        string? previous = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, "{\"WorkspacePath\":" + System.Text.Json.JsonSerializer.Serialize(workspace) + "}");
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
            StaWpf.Pump();
            Assert.True(loaded, "MainWindow.Loaded did not fire.");
            Assert.True(File.Exists(StartupLog.FilePath), "startup.log was not created.");
            var log = File.ReadAllText(StartupLog.FilePath);
            Assert.Contains("MainWindowLoaded", log, StringComparison.Ordinal);
            window.Close();
            StaWpf.Pump();
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
    });
}

internal static class StaWpf
{
    private static readonly Dispatcher UiDispatcher;
    private static readonly Thread UiThread;

    static StaWpf()
    {
        Dispatcher? dispatcher = null;
        using var started = new ManualResetEventSlim(false);
        UiThread = new Thread(() =>
        {
            dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            started.Set();
            System.Windows.Threading.Dispatcher.Run();
        });
        UiThread.Name = "GitManager-Wpf-STA";
        UiThread.SetApartmentState(ApartmentState.STA);
        UiThread.IsBackground = true;
        UiThread.Start();
        started.Wait();
        UiDispatcher = dispatcher!;
    }

    public static void Run(Action action)
    {
        Exception? caught = null;
        UiDispatcher.Invoke(() =>
        {
            try { action(); }
            catch (Exception ex) { caught = ex; }
        });
        if (caught is not null) throw caught;
    }

    public static void Pump()
    {
        var frame = new DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

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
