using GitManager.App.Models;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class LifecycleMoveRecoveryTests
{
    [Fact]
    public async Task Recovery_R1_SourceOnly_MarksRolledBack()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var op = await journal.BeginAsync("LifecycleMove",
                LifecycleMoveService.BuildOperationKey("Active", "On-Hold", project.CategoryId),
                project.ProjectId, project.ContainerPath, dest);
            await journal.SetStateAsync(op.OperationId, "Running");

            var report = await new LifecycleMoveRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks"))).RecoverAsync();

            Assert.Equal(1, report.RolledBack);
            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.DoesNotContain(await catalog.GetLifecycleEventsAsync(project.ProjectId),
                e => e.Result == LifecycleState.Completed);
            Assert.Empty(await journal.GetInterruptedAsync());
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_R3_DestAndCatalog_MarksCompleted()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            Directory.Move(project.ContainerPath, dest);
            await catalog.CommitLifecycleMoveAsync(project.ProjectId, LifecycleState.OnHold, dest,
                Path.Combine(dest, "repository"),
                LifecycleMoveService.NewEvent(project, LifecycleState.OnHold, "setup", LifecycleState.CatalogCommitted, "clean", "Available", "setup"));
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var op = await journal.BeginAsync("LifecycleMove",
                LifecycleMoveService.BuildOperationKey("Active", "On-Hold", project.CategoryId),
                project.ProjectId, project.ContainerPath, dest);
            await journal.SetStateAsync(op.OperationId, "Running");
            // Fix event operation id by inserting CatalogCommitted with real id via unique ignore — catalog already has one CatalogCommitted with operation setup.
            // Recovery completes using dest+catalog dest.

            var report = await new LifecycleMoveRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks"))).RecoverAsync();

            Assert.Equal(1, report.Completed);
            Assert.True(Directory.Exists(dest));
            Assert.Contains(await catalog.GetLifecycleEventsAsync(project.ProjectId),
                e => e.Result == LifecycleState.Completed && e.OperationId == op.OperationId);
            Assert.Empty(await journal.GetInterruptedAsync());
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_R4_DestOnlyCatalogSource_MovesBack()
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

            var report = await new LifecycleMoveRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks"))).RecoverAsync();

            Assert.Equal(1, report.RolledBack);
            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.False(Directory.Exists(dest));
            Assert.DoesNotContain(await catalog.GetLifecycleEventsAsync(project.ProjectId),
                e => e.Result == LifecycleState.Completed);
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_R6_BothExist_RequiresAttention_DeletesNothing()
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

            var report = await new LifecycleMoveRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks"))).RecoverAsync();
            var second = await new LifecycleMoveRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks"))).RecoverAsync();

            Assert.Equal(1, report.RequiresAttention);
            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.True(Directory.Exists(dest));
            Assert.DoesNotContain(await catalog.GetLifecycleEventsAsync(project.ProjectId),
                e => e.Result == LifecycleState.Completed);
            Assert.Equal(1, (await catalog.GetLifecycleEventsAsync(project.ProjectId))
                .Count(e => e.Result == LifecycleState.RecoveryRequired && e.OperationId == op.OperationId));
            Assert.Equal("RecoveryRequired", Assert.Single(await journal.GetInterruptedAsync()).State);
            Assert.Equal(1, second.RequiresAttention);
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_R7_NeitherExist_RequiresAttention()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            Directory.Delete(project.ContainerPath, recursive: true);
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var op = await journal.BeginAsync("LifecycleMove",
                LifecycleMoveService.BuildOperationKey("Active", "On-Hold", project.CategoryId),
                project.ProjectId, project.ContainerPath, dest);
            await journal.SetStateAsync(op.OperationId, "Running");

            var report = await new LifecycleMoveRecoveryService(root, catalog, journal).RecoverAsync();
            Assert.Equal(1, report.RequiresAttention);
            Assert.False(Directory.Exists(dest));
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task GenericStagingRecovery_DoesNotDeleteLifecyclePaths()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (_, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var op = await journal.BeginAsync("LifecycleMove", "lifecycle-move|Active|On-Hold|x",
                project.ProjectId, project.ContainerPath, dest);
            await journal.SetStateAsync(op.OperationId, "Running");

            var report = await new ProjectOperationRecoveryService(journal).RecoverAsync();
            Assert.Equal(0, report.RolledBack);
            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.Equal("Running", Assert.Single(await journal.GetInterruptedAsync()).State);
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_SkipsWhenProjectLockHeld()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var locks = new ProjectOperationLock(Path.Combine(root, "locks"));
            var op = await journal.BeginAsync("LifecycleMove",
                LifecycleMoveService.BuildOperationKey("Active", "On-Hold", project.CategoryId),
                project.ProjectId, project.ContainerPath, dest);
            await journal.SetStateAsync(op.OperationId, "Running");
            await using var held = await locks.AcquireAsync("project:" + project.ProjectId);
            var report = await new LifecycleMoveRecoveryService(root, catalog, journal, locks).RecoverAsync();
            Assert.Equal(0, report.RolledBack + report.Completed + report.RequiresAttention);
            Assert.Equal("Running", Assert.Single(await journal.GetInterruptedAsync()).State);
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_IgnoresOtherWorkspace()
    {
        var root = ProjectLifecycleTests.NewRoot();
        var other = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, _) = await ProjectLifecycleTests.SeedAsync(root);
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var op = await journal.BeginAsync("LifecycleMove", "lifecycle-move|Active|On-Hold|x",
                "foreign", Path.Combine(other, "src"), Path.Combine(other, "dst"));
            await journal.SetStateAsync(op.OperationId, "Running");
            var report = await new LifecycleMoveRecoveryService(root, catalog, journal).RecoverAsync();
            Assert.Equal(0, report.RolledBack + report.Completed + report.RequiresAttention);
            Assert.Equal("Running", Assert.Single(await journal.GetInterruptedAsync()).State);
        }
        finally
        {
            ProjectLifecycleTests.DeleteRoot(root);
            ProjectLifecycleTests.DeleteRoot(other);
        }
    }

    [Fact]
    public async Task RecoveryRequired_ThenSuccessfulRecovery_AppendsCompleted()
    {
        var root = ProjectLifecycleTests.NewRoot();
        try
        {
            var (catalog, _, project) = await ProjectLifecycleTests.SeedAsync(root);
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            Directory.Move(project.ContainerPath, dest);
            await catalog.CommitLifecycleMoveAsync(project.ProjectId, LifecycleState.OnHold, dest,
                Path.Combine(dest, "repository"),
                LifecycleMoveService.NewEvent(project, LifecycleState.OnHold, "tmp", LifecycleState.CatalogCommitted, "clean", "Available", "setup"));
            Directory.CreateDirectory(project.ContainerPath);
            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var op = await journal.BeginAsync("LifecycleMove",
                LifecycleMoveService.BuildOperationKey("Active", "On-Hold", project.CategoryId),
                project.ProjectId, project.ContainerPath, dest);
            await journal.SetStateAsync(op.OperationId, "Running");
            var locks = new ProjectOperationLock(Path.Combine(root, "locks"));

            var first = await new LifecycleMoveRecoveryService(root, catalog, journal, locks).RecoverAsync();
            Assert.Equal(1, first.RequiresAttention);

            Directory.Delete(project.ContainerPath, recursive: true);
            var second = await new LifecycleMoveRecoveryService(root, catalog, journal, locks).RecoverAsync();
            Assert.Equal(1, second.Completed);
            var events = await catalog.GetLifecycleEventsAsync(project.ProjectId);
            Assert.Contains(events, e => e.Result == LifecycleState.RecoveryRequired && e.OperationId == op.OperationId);
            Assert.Contains(events, e => e.Result == LifecycleState.Completed && e.OperationId == op.OperationId);
        }
        finally { ProjectLifecycleTests.DeleteRoot(root); }
    }
}
