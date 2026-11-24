using GitManager.App.Models;
using GitManager.App.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GitManager.Tests;

public sealed class LibraryStartupTests
{
    [Fact]
    public async Task Open_FreshInstall_CreatesCatalogAndJournal()
    {
        var root = NewRoot();
        try
        {
            var journalPath = Path.Combine(root, "operations.sqlite3");
            var journal = new ProjectOperationJournal(journalPath);
            var opened = await LibraryStartup.OpenAsync(root, journal, new GitProcess(),
                new ProjectOperationLock(Path.Combine(root, "locks")));

            Assert.False(opened.RecoveryLimited);
            Assert.True(opened.JournalUsable);
            Assert.NotNull(opened.Catalog);
            Assert.NotNull(opened.Importer);
            Assert.True(File.Exists(journalPath));
            Assert.NotEmpty(await opened.Catalog.GetCategoriesAsync());
            Assert.Empty(await journal.GetInterruptedAsync());
            Assert.Contains("کتابخانه آماده است", opened.StatusMessage(0));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Open_PreExistingEmptySqlite_DoesNotBlockCatalog()
    {
        var root = NewRoot();
        try
        {
            var journalPath = Path.Combine(root, "operations.sqlite3");
            await using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = journalPath, Pooling = false }.ToString()))
            {
                await connection.OpenAsync();
            }

            var journal = new ProjectOperationJournal(journalPath);
            var opened = await LibraryStartup.OpenAsync(root, journal, new GitProcess(),
                new ProjectOperationLock(Path.Combine(root, "locks")));

            Assert.False(opened.RecoveryLimited);
            Assert.True(opened.JournalUsable);
            Assert.NotNull(opened.Catalog);
            Assert.Empty(await journal.GetInterruptedAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Open_Upgrade_PreservesCatalogProjectsAndJournalRows()
    {
        var root = NewRoot();
        try
        {
            var workspace = new WorkspaceService();
            workspace.Initialize(root);
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var project = await AddProjectAsync(root, catalog, "keep-me");

            var journalPath = Path.Combine(root, "operations.sqlite3");
            var journal = new ProjectOperationJournal(journalPath);
            var staging = Path.Combine(root, "00_Manager", "temp", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var operation = await journal.BeginAsync("Import", "import:keep-me", project.ProjectId, staging, project.ContainerPath);
            await journal.SetStateAsync(operation.OperationId, "Completed");

            var reopenedJournal = new ProjectOperationJournal(journalPath);
            var opened = await LibraryStartup.OpenAsync(root, reopenedJournal, new GitProcess(),
                new ProjectOperationLock(Path.Combine(root, "locks")));

            Assert.False(opened.RecoveryLimited);
            Assert.True(opened.JournalUsable);
            var saved = Assert.Single(await opened.Catalog.GetProjectsAsync());
            Assert.Equal(project.ProjectId, saved.ProjectId);
            Assert.Equal("keep-me", saved.Repository);
            Assert.Equal(project.CanonicalRemoteUrl, saved.CanonicalRemoteUrl);
            var journalRow = await reopenedJournal.GetAsync(operation.OperationId);
            Assert.NotNull(journalRow);
            Assert.Equal("Completed", journalRow!.State);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Open_WhenRecoveryThrows_StillReturnsCatalogAndMarksLimited()
    {
        var root = NewRoot();
        try
        {
            var journal = new ThrowingOnReadJournal(Path.Combine(root, "operations.sqlite3"));
            var opened = await LibraryStartup.OpenAsync(root, journal, new GitProcess(),
                new ProjectOperationLock(Path.Combine(root, "locks")));

            Assert.True(opened.RecoveryLimited);
            Assert.True(opened.JournalUsable);
            Assert.NotNull(opened.Catalog);
            Assert.NotNull(opened.Importer);
            var status = opened.StatusMessage(0);
            Assert.Contains("کتابخانه باز است", status);
            Assert.Contains("عملیات حساس تا رفع مشکل محدود است", status);
            Assert.DoesNotContain("simulated interrupted-operation read failure", status);
            Assert.DoesNotContain(root, status);
            var log = File.ReadAllText(DiagnosticLog.FilePath(DiagnosticLog.DirectoryFor(root)));
            Assert.Contains("simulated interrupted-operation read failure", log);
            Assert.NotEmpty(await opened.Catalog.GetCategoriesAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Open_CorruptJournalFile_OpensCatalog_DoesNotLeakExceptionToUi()
    {
        var root = NewRoot();
        try
        {
            var journalPath = Path.Combine(root, "operations.sqlite3");
            await File.WriteAllTextAsync(journalPath, "CORRUPT-NOT-SQLITE-" + new string('X', 1024));
            var journal = new ProjectOperationJournal(journalPath);
            var opened = await LibraryStartup.OpenAsync(root, journal, new GitProcess(),
                new ProjectOperationLock(Path.Combine(root, "locks")));

            Assert.True(opened.RecoveryLimited);
            Assert.False(opened.JournalUsable);
            Assert.NotNull(opened.Catalog);
            Assert.NotEmpty(await opened.Catalog.GetCategoriesAsync());
            var status = opened.StatusMessage(0);
            Assert.Contains("دفتر ثبت عملیات قابل استفاده نیست", status);
            Assert.DoesNotContain("SQLite", status, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("file is not a database", status, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(journalPath, status);
            Assert.DoesNotContain("Exception", status, StringComparison.OrdinalIgnoreCase);
            var log = File.ReadAllText(DiagnosticLog.FilePath(DiagnosticLog.DirectoryFor(root)));
            Assert.Contains("journal is not usable", log);
            Assert.Contains("SQLite", log, StringComparison.OrdinalIgnoreCase);
        }
        finally { DeleteRoot(root); }
    }

    private static async Task<ProjectRecord> AddProjectAsync(string root, ProjectCatalog catalog, string repo)
    {
        var container = Path.Combine(root, "01_Active", "Unclassified", "owner", repo);
        Directory.CreateDirectory(Path.Combine(container, "repository"));
        Directory.CreateDirectory(Path.Combine(container, "backups"));
        Directory.CreateDirectory(Path.Combine(container, "notes"));
        var project = new ProjectRecord
        {
            CanonicalRemoteUrl = $"https://github.com/owner/{repo}.git",
            Owner = "owner",
            Repository = repo,
            Category = "Unclassified",
            Lifecycle = "Active",
            ContainerPath = container,
            RepositoryPath = Path.Combine(container, "repository")
        };
        await catalog.AddProjectAsync(project);
        return project;
    }

    private sealed class ThrowingOnReadJournal(string path) : ProjectOperationJournal(path)
    {
        public override Task<IReadOnlyList<ProjectOperationEntry>> GetInterruptedAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated interrupted-operation read failure");
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerLibraryStartupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
