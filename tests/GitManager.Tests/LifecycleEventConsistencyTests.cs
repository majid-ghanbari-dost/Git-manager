using GitManager.App.Models;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class LifecycleEventConsistencyTests
{
    [Fact]
    public async Task SameLifecycleRetry_CompletesPendingJournalViaRecovery()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, service, project) = await ProjectLifecycleTests.SeedAsync(root);
            var opId = await service.RelocateUntilCatalogCommittedAsync(project.ProjectId, LifecycleState.OnHold,
                new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true));
            await service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold);
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            Assert.Equal("Completed", (await journal.GetAsync(opId))!.State);
            var events = await catalog.GetLifecycleEventsAsync(project.ProjectId);
            Assert.Equal(1, events.Count(e => e.Result == LifecycleState.Completed && e.OperationId == opId));
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task CompletedEvent_IsNotInserted_UntilJournalIsCompleted()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, service, project) = await ProjectLifecycleTests.SeedAsync(root);
            var opId = await service.RelocateUntilCatalogCommittedAsync(project.ProjectId, LifecycleState.OnHold,
                new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true));
            var events = await catalog.GetLifecycleEventsAsync(project.ProjectId);
            Assert.Contains(events, e => e.Result == LifecycleState.CatalogCommitted && e.OperationId == opId);
            Assert.DoesNotContain(events, e => e.Result == LifecycleState.Completed);
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var entry = await journal.GetAsync(opId);
            Assert.NotNull(entry);
            Assert.NotEqual("Completed", entry!.State);

            var moved = Assert.Single(await catalog.GetProjectsAsync());
            await service.CompleteJournalAndEventAsync(opId, project, LifecycleState.OnHold, "clean", "Available");
            events = await catalog.GetLifecycleEventsAsync(project.ProjectId);
            Assert.Equal(1, events.Count(e => e.Result == LifecycleState.Completed));
            Assert.True(events.Single(e => e.Result == LifecycleState.CatalogCommitted).EventSeq <
                        events.Single(e => e.Result == LifecycleState.Completed).EventSeq);
            Assert.Equal("Completed", (await journal.GetAsync(opId))!.State);
            Assert.Equal(LifecycleState.OnHold, moved.Lifecycle);
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_DoesNotInsertDuplicateCompleted()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, service, project) = await ProjectLifecycleTests.SeedAsync(root);
            var opId = await service.RelocateUntilCatalogCommittedAsync(project.ProjectId, LifecycleState.OnHold,
                new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true));
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var report = await new LifecycleMoveRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks-b"))).RecoverAsync();
            var again = await new LifecycleMoveRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks-c"))).RecoverAsync();
            Assert.Equal(1, report.Completed);
            var events = await catalog.GetLifecycleEventsAsync(project.ProjectId);
            Assert.Equal(1, events.Count(e => e.Result == LifecycleState.Completed && e.OperationId == opId));
            Assert.Equal(1, events.Count(e => e.Result == LifecycleState.CatalogCommitted));
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_AppendsRecoveryRequired_WhenBothTreesExist()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            Directory.CreateDirectory(Path.Combine(dest, "repository"));
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var op = await journal.BeginAsync("LifecycleMove",
                LifecycleMoveService.BuildOperationKey("Active", "On-Hold", project.CategoryId),
                project.ProjectId, project.ContainerPath, dest);
            await journal.SetStateAsync(op.OperationId, "Running");
            var recovery = new LifecycleMoveRecoveryService(root, catalog, journal);
            await recovery.RecoverAsync();
            await recovery.RecoverAsync();
            var events = await catalog.GetLifecycleEventsAsync(project.ProjectId);
            Assert.Equal(1, events.Count(e => e.Result == LifecycleState.RecoveryRequired && e.OperationId == op.OperationId));
            Assert.Empty(events.Where(e => e.Result == LifecycleState.Completed));
            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.True(Directory.Exists(dest));
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task FailedMove_BeforeCatalogCommit_DoesNotInsertCompleted()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            Directory.Move(project.ContainerPath, dest);
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var op = await journal.BeginAsync("LifecycleMove",
                LifecycleMoveService.BuildOperationKey("Active", "On-Hold", project.CategoryId),
                project.ProjectId, project.ContainerPath, dest);
            await journal.SetStateAsync(op.OperationId, "Running");
            await new LifecycleMoveRecoveryService(root, catalog, journal).RecoverAsync();
            var events = await catalog.GetLifecycleEventsAsync(project.ProjectId);
            Assert.DoesNotContain(events, e => e.Result == LifecycleState.Completed);
            Assert.DoesNotContain(events, e => e.Result == LifecycleState.CatalogCommitted);
            Assert.Contains(events, e => e.Result == LifecycleState.RolledBack);
            Assert.True(Directory.Exists(project.ContainerPath));
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task PreflightReject_DoesNotInsertCompletedOrCatalogCommitted()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, service, project) = await ProjectLifecycleTests.SeedAsync(root);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.Archived));
            Assert.Empty(await catalog.GetLifecycleEventsAsync(project.ProjectId));
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task JournalCompleteFailure_LeavesCatalogCommitted_NotCompleted()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var project = await ProjectLifecycleTests.AddProjectAsync(root, catalog, "Unclassified", "repo");
            var throwing = new ThrowingOnCompleteJournal(Path.Combine(root, "operations.sqlite3"));
            var service = new ProjectLifecycleService(root, catalog, throwing, new GitProcess(),
                new ProjectOperationLock(Path.Combine(root, "locks")));
            await Assert.ThrowsAsync<IOException>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold,
                    new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true)));

            var events = await catalog.GetLifecycleEventsAsync(project.ProjectId);
            Assert.Contains(events, e => e.Result == LifecycleState.CatalogCommitted);
            Assert.DoesNotContain(events, e => e.Result == LifecycleState.Completed);
            var saved = Assert.Single(await catalog.GetProjectsAsync());
            Assert.Equal(LifecycleState.OnHold, saved.Lifecycle);
            Assert.True(Directory.Exists(saved.ContainerPath));

            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var report = await new LifecycleMoveRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks-2"))).RecoverAsync();
            Assert.Equal(1, report.Completed);
            events = await catalog.GetLifecycleEventsAsync(project.ProjectId);
            Assert.Equal(1, events.Count(e => e.Result == LifecycleState.Completed));
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task EventOrder_PreservedAcrossCatalogReopen()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, service, project) = await ProjectLifecycleTests.SeedAsync(root);
            await service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold,
                new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true));
            await service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.Archived,
                new LifecycleChangeOptions(ConfirmArchive: true, ConfirmDirtyWorkingTree: true));
            var first = (await catalog.GetLifecycleEventsAsync(project.ProjectId)).Select(e => (e.EventSeq, e.Result)).ToList();
            var reopened = new ProjectCatalog(root);
            await reopened.InitializeAsync();
            var second = (await reopened.GetLifecycleEventsAsync(project.ProjectId)).Select(e => (e.EventSeq, e.Result)).ToList();
            Assert.Equal(first, second);
            Assert.Equal(new[]
            {
                LifecycleState.CatalogCommitted, LifecycleState.Completed,
                LifecycleState.CatalogCommitted, LifecycleState.Completed
            }, second.Select(x => x.Result));
            Assert.True(second[0].EventSeq < second[1].EventSeq);
            Assert.True(second[1].EventSeq < second[2].EventSeq);
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Migration_SecondInitialize_DoesNotDuplicateEvents()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, service, project) = await ProjectLifecycleTests.SeedAsync(root);
            await service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold,
                new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true));
            var count = (await catalog.GetLifecycleEventsAsync()).Count;
            await catalog.InitializeAsync();
            await catalog.InitializeAsync();
            Assert.Equal(count, (await catalog.GetLifecycleEventsAsync()).Count);
            Assert.Equal("2.3", await catalog.GetSchemaVersionAsync());
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    private sealed class ThrowingOnCompleteJournal(string path) : ProjectOperationJournal(path)
    {
        public override Task SetStateAsync(
            string operationId, string state, string? error = null, CancellationToken cancellationToken = default)
        {
            if (state == "Completed")
                throw new IOException("journal complete failed");
            return base.SetStateAsync(operationId, state, error, cancellationToken);
        }
    }
}
