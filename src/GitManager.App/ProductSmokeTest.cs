using GitManager.App.Services;

namespace GitManager.App;

/// <summary>
/// Headless verification used by <c>--ci-smoke</c> and unit tests. It never opens the
/// WPF shell and never writes into the real user workspace.
/// </summary>
public static class ProductSmokeTest
{
    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManager-CI-Smoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            RunAtAsync(root).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(ex.ToString());
            try { Console.Error.WriteLine(ex); } catch { }
            return 1;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    public static async Task RunAtAsync(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Smoke workspace root is required.", nameof(root));

        var workspace = new WorkspaceService();
        workspace.Initialize(root);
        foreach (var folder in new[] { "00_Manager", "01_Active", "02_On-Hold", "03_Archived" })
        {
            if (!Directory.Exists(Path.Combine(root, folder)))
                throw new InvalidOperationException($"Smoke initialization did not create {folder}.");
        }

        var catalog = new ProjectCatalog(root);
        await catalog.InitializeAsync();
        if ((await catalog.GetCategoriesAsync()).Count == 0)
            throw new InvalidOperationException("Smoke catalog has no categories.");

        var journal = new ProjectOperationJournal(Path.Combine(root, "smoke-operations.sqlite3"));
        await journal.InitializeAsync();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData) ||
            !AppLocalState.Root.StartsWith(Path.GetFullPath(localAppData), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("App local state is not under LocalAppData.");

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) &&
            Path.GetFileNameWithoutExtension(processPath).Equals("GitManager", StringComparison.OrdinalIgnoreCase))
        {
            var exeDir = Path.GetFullPath(Path.GetDirectoryName(processPath)!);
            var stateRoot = Path.GetFullPath(AppLocalState.Root);
            if (stateRoot.StartsWith(exeDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) ||
                stateRoot.Equals(exeDir, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("App local state must not live under the install directory.");
        }
    }
}
