using GitManager.App.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GitManager.Tests;

public sealed class JournalBootstrapTests
{
    [Fact]
    public async Task FreshInstall_GetInterrupted_CreatesTableAndReturnsEmpty()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "operations.sqlite3");
            Assert.False(File.Exists(path));
            var journal = new ProjectOperationJournal(path);
            var interrupted = await journal.GetInterruptedAsync();
            Assert.Empty(interrupted);
            Assert.True(File.Exists(path));
            Assert.True(await TableExistsAsync(path, "project_operations"));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task EmptySqliteFile_GetInterrupted_CreatesTableAndReturnsEmpty()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "operations.sqlite3");
            await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
            {
                await connection.OpenAsync();
            }

            Assert.True(File.Exists(path));
            Assert.False(await TableExistsAsync(path, "project_operations"));

            var journal = new ProjectOperationJournal(path);
            var interrupted = await journal.GetInterruptedAsync();
            Assert.Empty(interrupted);
            Assert.True(await TableExistsAsync(path, "project_operations"));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task ZeroByteSqliteFile_GetInterrupted_CreatesTableAndReturnsEmpty()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "operations.sqlite3");
            await File.WriteAllBytesAsync(path, Array.Empty<byte>());
            var journal = new ProjectOperationJournal(path);
            var interrupted = await journal.GetInterruptedAsync();
            Assert.Empty(interrupted);
            Assert.True(await TableExistsAsync(path, "project_operations"));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Upgrade_ExistingRowsSurviveInitializeAndRead()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "operations.sqlite3");
            var journal = new ProjectOperationJournal(path);
            var staging = Path.Combine(root, "staging");
            var final = Path.Combine(root, "final");
            Directory.CreateDirectory(staging);
            var operation = await journal.BeginAsync("Import", "import:keep", "project-keep", staging, final);
            await journal.SetStateAsync(operation.OperationId, "Completed");

            var reopened = new ProjectOperationJournal(path);
            await reopened.InitializeAsync();
            var saved = await reopened.GetAsync(operation.OperationId);
            Assert.NotNull(saved);
            Assert.Equal("Completed", saved!.State);
            Assert.Equal("import:keep", saved.OperationKey);
            Assert.Equal("project-keep", saved.ProjectId);
            Assert.Empty(await reopened.GetInterruptedAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task CategoryRenameRecovery_OnFreshJournal_DoesNotThrow()
    {
        var root = NewRoot();
        try
        {
            var workspace = new WorkspaceService();
            workspace.Initialize(root);
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var locks = new ProjectOperationLock(Path.Combine(root, "locks"));

            var report = await new CategoryRenameRecoveryService(root, catalog, journal, locks).RecoverAsync();

            Assert.Equal(0, report.RolledBack);
            Assert.Equal(0, report.Completed);
            Assert.Equal(0, report.RequiresAttention);
        }
        finally { DeleteRoot(root); }
    }

    // Invalid file content (not a SQLite database). This is not interior page corruption.
    [Fact]
    public async Task CorruptSqliteFile_GetInterrupted_ThrowsJournalUnavailable()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "operations.sqlite3");
            await File.WriteAllTextAsync(path, "CORRUPT-NOT-SQLITE-" + new string('X', 1024));
            var journal = new ProjectOperationJournal(path);
            var ex = await Assert.ThrowsAsync<JournalUnavailableException>(() => journal.GetInterruptedAsync());
            Assert.Equal(JournalUnavailableException.UserMessage, ex.Message);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("SQLite", ex.InnerException!.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally { DeleteRoot(root); }
    }

    // Invalid file content (not a SQLite database). This is not interior page corruption.
    [Fact]
    public async Task CorruptSqliteFile_EnsureAvailable_ThrowsJournalUnavailable()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "operations.sqlite3");
            await File.WriteAllTextAsync(path, "CORRUPT-NOT-SQLITE-" + new string('X', 1024));
            var journal = new ProjectOperationJournal(path);
            await Assert.ThrowsAsync<JournalUnavailableException>(() => journal.EnsureAvailableAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task MalformedSqlitePages_EnsureAvailable_ThrowsJournalUnavailable()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "operations.sqlite3");
            var journal = new ProjectOperationJournal(path);
            var staging = Path.Combine(root, "staging");
            var final = Path.Combine(root, "final");
            Directory.CreateDirectory(staging);
            await journal.BeginAsync("Import", "import:malformed-pages", "project-malformed", staging, final);
            await SqliteJournalFile.CheckpointAsync(path);
            SqliteJournalFile.ScrambleInteriorPagesPreservingHeader(path);
            Assert.Equal("SQLite format 3", SqliteJournalFile.ReadHeaderMagic(path));

            var ex = await Assert.ThrowsAsync<JournalUnavailableException>(() => journal.EnsureAvailableAsync());
            Assert.Equal(JournalUnavailableException.UserMessage, ex.Message);
            await Assert.ThrowsAsync<JournalUnavailableException>(() => journal.GetInterruptedAsync());
        }
        finally { DeleteRoot(root); }
    }

    private static async Task<bool> TableExistsAsync(string databasePath, string table)
    {
        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name";
        command.Parameters.AddWithValue("$name", table);
        return await command.ExecuteScalarAsync() is not null;
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerJournalBootstrapTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
