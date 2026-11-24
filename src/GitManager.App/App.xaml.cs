using System.Linq;

namespace GitManager.App;

public partial class App : System.Windows.Application
{
    public const string CiSmokeArgument = "--ci-smoke";

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        if (e.Args.Any(argument => string.Equals(argument, CiSmokeArgument, StringComparison.OrdinalIgnoreCase)))
        {
            var code = 1;
            try { code = ProductSmokeTest.Run(); }
            catch { code = 1; }
            Environment.Exit(code);
            return;
        }

        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
