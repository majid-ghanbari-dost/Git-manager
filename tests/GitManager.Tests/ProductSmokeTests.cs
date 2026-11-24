using GitManager.App;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class ProductSmokeTests
{
    [Fact]
    public async Task Smoke_InitializesWorkspaceAndCatalog_WithoutUsingInstallDirectoryForState()
    {
        var top = Path.Combine(Path.GetTempPath(), "GitManagerProductSmoke", Guid.NewGuid().ToString("N"));
        var root = Path.Combine(top, "path with spaces");
        Directory.CreateDirectory(root);
        try
        {
            await ProductSmokeTest.RunAtAsync(root);
            Assert.True(Directory.Exists(Path.Combine(root, "01_Active")));
            Assert.True(Directory.Exists(Path.Combine(root, "00_Manager", "catalog")));
            Assert.Equal(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitManager"),
                AppLocalState.Root);
            Assert.Equal("settings.json", Path.GetFileName(AppLocalState.SettingsPath));
            Assert.Equal("operations.sqlite3", Path.GetFileName(AppLocalState.JournalPath));
            Assert.False(AppLocalState.Root.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(top, recursive: true); } catch { }
        }
    }

    [Fact]
    public void InstallerScript_DoesNotUninstallUserData_AndCreatesShortcuts()
    {
        var iss = File.ReadAllText(FindInstallerScript());
        Assert.Contains("GitManager-Setup-x64", iss, StringComparison.Ordinal);
        Assert.Contains("{autoprograms}", iss, StringComparison.Ordinal);
        Assert.Contains("{autodesktop}", iss, StringComparison.Ordinal);
        Assert.Contains("UsePreviousAppDir=yes", iss, StringComparison.Ordinal);
        Assert.DoesNotContain("[UninstallDelete]", iss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[InstallDelete]", iss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("settings.json", iss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("projects.sqlite3", iss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operations.sqlite3", iss, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Your workspace, project catalog, backups, notes, and LocalAppData settings will not be deleted.", iss, StringComparison.Ordinal);
        Assert.Contains("2.4.0", iss, StringComparison.Ordinal);
    }

    private static string FindInstallerScript()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "installer", "GitManager.iss");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        throw new FileNotFoundException("installer/GitManager.iss not found from test host.");
    }
}
