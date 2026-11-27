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
    public void LoadWindowIcon_DoesNotThrow_AndReturnsFrame() => StaWpf.Run(() =>
    {
        var icon = WpfMainWindow.LoadWindowIcon();
        Assert.NotNull(icon);
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
        var window = new Window
        {
            Width = 120,
            Height = 80,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            Icon = logo
        };

        // Verify WPF accepts and retains the DrawingImage as its icon without
        // showing a native window. Window.Show can hang on headless Session 0 runners.
        Assert.Same(logo, window.Icon);
    });

    [Fact]
    public void MainWindow_Show_ReachesLoaded_AndCloses() => StaWpf.Run(() =>
    {
        StaWpf.EnsureApp();
        var workspace = Path.Combine(Path.GetTempPath(), "GitManagerWpfRuntimeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        var settingsPath = AppLocalState.SettingsPath;
        string? previous = null;
        try
        {
            if (File.Exists(settingsPath)) previous = File.ReadAllText(settingsPath);
        }
        catch (IOException) { }

        WpfMainWindow? window = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            WriteSettings(settingsPath, "{\"WorkspacePath\":" + System.Text.Json.JsonSerializer.Serialize(workspace) + "}");
            var loaded = false;
            window = new WpfMainWindow
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
            try { window?.Close(); } catch { }
            StaWpf.Pump();
            try
            {
                if (previous is null) TryDelete(settingsPath);
                else WriteSettings(settingsPath, previous);
            }
            catch { }
            try { Directory.Delete(workspace, recursive: true); } catch { }
        }
    });

    [Fact]
    public void MainWindow_Show_CenterScreen_ReachesLoaded() => StaWpf.Run(() =>
    {
        StaWpf.EnsureApp();
        var workspace = Path.Combine(Path.GetTempPath(), "GitManagerWpfRuntimeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        var settingsPath = AppLocalState.SettingsPath;
        string? previous = null;
        try
        {
            if (File.Exists(settingsPath)) previous = File.ReadAllText(settingsPath);
        }
        catch (IOException) { }

        WpfMainWindow? window = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            WriteSettings(settingsPath, "{\"WorkspacePath\":" + System.Text.Json.JsonSerializer.Serialize(workspace) + "}");
            var loaded = false;
            window = new WpfMainWindow { ShowInTaskbar = false };
            GitManager.App.WindowStartupPlacement.Apply(window);
            window.Loaded += (_, _) => loaded = true;
            window.Show();
            StaWpf.Pump();
            Assert.True(loaded, "MainWindow.Loaded did not fire with production placement.");
            window.Close();
            StaWpf.Pump();
        }
        finally
        {
            try { window?.Close(); } catch { }
            StaWpf.Pump();
            try
            {
                if (previous is null) TryDelete(settingsPath);
                else WriteSettings(settingsPath, previous);
            }
            catch { }
            try { Directory.Delete(workspace, recursive: true); } catch { }
        }
    });

    private static void WriteSettings(string path, string json)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                File.WriteAllText(path, json);
                return;
            }
            catch (IOException)
            {
                StaWpf.Pump();
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
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

    public static void Run(Action action, TimeSpan? timeout = null)
    {
        Exception? caught = null;
        var op = UiDispatcher.BeginInvoke(() =>
        {
            try { action(); }
            catch (Exception ex) { caught = ex; }
        });
        var status = op.Wait(timeout ?? TimeSpan.FromSeconds(20));
        if (status != DispatcherOperationStatus.Completed)
            throw new TimeoutException($"STA WPF work did not complete ({status}). Window.Show may hang in Session 0.");
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
