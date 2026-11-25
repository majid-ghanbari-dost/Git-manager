using System.Linq;
using System.Windows;
using System.Windows.Threading;
using GitManager.App.Services;

namespace GitManager.App;

public partial class App : Application
{
    public const string CiSmokeArgument = "--ci-smoke";

    static App()
    {
        StartupLog.Write(
            "AppTypeInitializer",
            $"base={AppContext.BaseDirectory}; location={typeof(App).Assembly.Location}; interactive={Environment.UserInteractive}");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            StartupLog.Write(
                "AppDomainUnhandledException",
                args.IsTerminating ? "terminating" : "non-terminating",
                args.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupLog.Write("TaskSchedulerUnobservedTaskException", exception: args.Exception);
            args.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        StartupLog.Write("ProcessStarted", $"pid={Environment.ProcessId}");
        StartupLog.Write("StartupArguments", SanitizeStartupArguments(e.Args));
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        if (e.Args.Any(argument => string.Equals(argument, CiSmokeArgument, StringComparison.OrdinalIgnoreCase)))
        {
            StartupLog.Write("ApplicationStartup", "ci-smoke");
            var code = 1;
            try { code = ProductSmokeTest.Run(); }
            catch (Exception ex)
            {
                StartupLog.Write("StartupException", "ci-smoke", ex);
                code = 1;
            }

            StartupLog.Write("ApplicationExit", $"ci-smoke exit={code}");
            Environment.Exit(code);
            return;
        }

        try
        {
            base.OnStartup(e);
            StartupLog.Write("ApplicationStartup");
            var window = new MainWindow();
            StartupLog.Write("MainWindowCreated");
            WindowStartupPlacement.Apply(window);
            this.MainWindow = window;
            window.Show();
            StartupLog.Write("MainWindowShown");
        }
        catch (Exception ex)
        {
            StartupLog.Write("StartupException", "OnStartup", ex);
            try { Shutdown(1); } catch { Environment.Exit(1); }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StartupLog.Write("ApplicationExit", $"code={e.ApplicationExitCode}");
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupLog.Write("DispatcherUnhandledException", exception: e.Exception);
        e.Handled = true;
    }

    private static string SanitizeStartupArguments(IReadOnlyList<string> args) =>
        args.Count == 0 ? "(none)" : StartupLog.SanitizeArguments(args);
}
