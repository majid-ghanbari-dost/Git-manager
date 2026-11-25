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
    [Fact]
    public void ResourceDictionary_Freezes_SolidColorBrushes_MutationThrows()
    {
        StaWpf.Run(() =>
        {
            var dictionary = new ResourceDictionary
            {
                ["AppBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F9")!)
            };
            var brush = Assert.IsType<SolidColorBrush>(dictionary["AppBackgroundBrush"]);
            Assert.True(brush.IsFrozen);
            Assert.Throws<InvalidOperationException>(() => brush.Color = Colors.Black);
        });
    }

    [Fact]
    public void AppResources_ThemeBrushes_AreFrozen()
    {
        StaWpf.Run(() =>
        {
            StaWpf.EnsureApp();
            var brush = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AppBackgroundBrush"]);
            Assert.True(brush.IsFrozen);
        });
    }

    [Fact]
    public void SetBrush_ReplacesFrozenResource_DoesNotThrow()
    {
        StaWpf.Run(() =>
        {
            StaWpf.EnsureApp();
            WpfMainWindow.SetBrush("AppBackgroundBrush", "#111827");
            var brush = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AppBackgroundBrush"]);
            Assert.True(brush.IsFrozen);
            Assert.Equal((Color)ColorConverter.ConvertFromString("#111827")!, brush.Color);
        });
    }

    [Fact]
    public void DrawingImage_AsWindowIcon_IsRecorded()
    {
        StaWpf.Run(() =>
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

            // Record the result; do not treat a successful Show as proof that native
            // HICON conversion is safe on an interactive Product Owner desktop.
            if (caught is not null)
                StartupLog.Write("StartupException", "DrawingImage-as-Icon", caught);
            else
                StartupLog.Write("ProcessStarted", "DrawingImage-as-Icon Show did not throw in this environment");
        });
    }

    [Fact]
    public void MainWindow_Show_ReachesLoaded_AndCloses()
    {
        StaWpf.Run(() =>
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
                var window = new WpfMainWindow();
                window.Loaded += (_, _) => loaded = true;
                window.Show();
                var deadline = DateTime.UtcNow.AddSeconds(20);
                while (!loaded && DateTime.UtcNow < deadline)
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
}

[CollectionDefinition("WpfSta", DisableParallelization = true)]
public sealed class WpfStaCollection;

internal static class StaWpf
{
    public static void Run(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { caught = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(1)))
            throw new TimeoutException("STA WPF work did not finish in 1 minute.");
        if (caught is not null) throw caught;
    }

    public static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    public static WpfApp EnsureApp()
    {
        if (Application.Current is WpfApp existing)
        {
            existing.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            return existing;
        }

        var app = new WpfApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        return app;
    }
}
