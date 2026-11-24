using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class ProjectOperationRecoveryTests
{
    [Fact]
    public async Task Recovery_RemovesOnlyKnownIncompleteImportStaging()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerRecoveryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var tempRoot = Path.Combine(root, "workspace", "00_Manager", "temp");
            var staging = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
            var final = Path.Combine(root, "workspace", "01_Active", "Tools", "owner", "repo");
            Directory.CreateDirectory(staging);

            var operation = await journal.BeginAsync("Import", "import:test", "project-1", staging, final);
            await journal.SetStateAsync(operation.OperationId, "Running");

            var report = await new ProjectOperationRecoveryService(journal).RecoverAsync();

            Assert.Equal(1, report.RolledBack);
            Assert.Equal(0, report.RequiresAttention);
            Assert.False(Directory.Exists(staging));
            Assert.False(Directory.Exists(final));
            Assert.Empty(await journal.GetInterruptedAsync());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Recovery_NeverDeletesStagingWhenFinalDestinationExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerRecoveryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var backupRoot = Path.Combine(root, "project", "backups");
            Directory.CreateDirectory(backupRoot);
            var staging = Path.Combine(backupRoot, ".staging-" + Guid.NewGuid().ToString("N"));
            var final = Path.Combine(backupRoot, "backup-final");
            Directory.CreateDirectory(staging);
            Directory.CreateDirectory(final);

            var operation = await journal.BeginAsync("Backup", "project-2", "project-2", staging, final);
            await journal.SetStateAsync(operation.OperationId, "CommitPending");

            var recovery = new ProjectOperationRecoveryService(journal);
            var first = await recovery.RecoverAsync();
            var second = await recovery.RecoverAsync();

            Assert.Equal(1, first.RequiresAttention);
            Assert.Equal(0, second.RequiresAttention);
            Assert.True(Directory.Exists(staging));
            Assert.True(Directory.Exists(final));
            Assert.Empty(await journal.GetInterruptedAsync());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OperationLock_RejectsConcurrentUseOfSameKey()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerLockTests", Guid.NewGuid().ToString("N"));
        var operationLock = new ProjectOperationLock(root);
        try
        {
            await using var first = await operationLock.AcquireAsync("project:one");
            await Assert.ThrowsAsync<TimeoutException>(
                () => operationLock.AcquireAsync("project:one", timeout: TimeSpan.FromMilliseconds(50)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
