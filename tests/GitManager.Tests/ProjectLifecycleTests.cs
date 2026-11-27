using GitManager.App.Models;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class ProjectLifecycleTests
{
    [Fact]
    public void UnknownLifecycle_GetProjectContainer_Throws_AndDoesNotUseQuarantine()
    {
        var root = NewRoot();
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new WorkspaceService().GetProjectContainer(root, "Nope", "Unclassified", "o", "r"));
            Assert.DoesNotContain("04_Quarantine", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("قرنطینه", ex.Message);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Change_ActiveToOnHold_MovesWholeContainerAndPreservesNotesBackupsAndDirtyFiles()
    {
        var root = NewRoot();
        try
        {
            var (catalog, service, project) = await SeedAsync(root, withGit: true, dirty: true);
            var note = Path.Combine(project.ContainerPath, "notes", "keep.txt");
            var backup = Path.Combine(project.ContainerPath, "backups", "b.txt");
            await File.WriteAllTextAsync(note, "note-data");
            await File.WriteAllTextAsync(backup, "backup-data");
            var dirtyFile = Path.Combine(project.RepositoryPath, "dirty.txt");

            await service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold,
                new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true));

            var saved = Assert.Single(await catalog.GetProjectsAsync());
            Assert.Equal(LifecycleState.OnHold, saved.Lifecycle);
            Assert.Contains($"{Path.DirectorySeparatorChar}02_On-Hold{Path.DirectorySeparatorChar}", saved.ContainerPath);
            Assert.False(Directory.Exists(project.ContainerPath));
            Assert.True(Directory.Exists(saved.ContainerPath));
            Assert.True(Directory.Exists(Path.Combine(saved.ContainerPath, "repository", ".git")));
            Assert.Equal("note-data", await File.ReadAllTextAsync(Path.Combine(saved.ContainerPath, "notes", "keep.txt")));
            Assert.Equal("backup-data", await File.ReadAllTextAsync(Path.Combine(saved.ContainerPath, "backups", "b.txt")));
            Assert.Equal("dirty", await File.ReadAllTextAsync(Path.Combine(saved.RepositoryPath, "dirty.txt")));
            Assert.True(File.Exists(dirtyFile) is false);
            var events = await catalog.GetLifecycleEventsAsync(saved.ProjectId);
            Assert.Contains(events, e => e.Result == LifecycleState.CatalogCommitted);
            Assert.Contains(events, e => e.Result == LifecycleState.Completed);
            Assert.True(events.First(e => e.Result == LifecycleState.CatalogCommitted).EventSeq <
                        events.First(e => e.Result == LifecycleState.Completed).EventSeq);
        }
        finally { DeleteRoot(root); }
    }

    [Theory]
    [InlineData("Active", "On-Hold", false, false)]
    [InlineData("Active", "Archived", true, false)]
    [InlineData("On-Hold", "Active", false, false)]
    [InlineData("On-Hold", "Archived", true, false)]
    [InlineData("Archived", "Active", false, true)]
    [InlineData("Archived", "On-Hold", false, true)]
    public async Task AllowedTransitions_MoveContainer(string from, string to, bool archive, bool restore)
    {
        var root = NewRoot();
        try
        {
            var (catalog, service, project) = await SeedAsync(root);
            if (from != LifecycleState.Active)
                await service.ChangeLifecycleAsync(project.ProjectId, from,
                    new LifecycleChangeOptions(ConfirmArchive: true, ConfirmRestoreFromArchive: true, ConfirmDirtyWorkingTree: true));
            await service.ChangeLifecycleAsync(project.ProjectId, to,
                new LifecycleChangeOptions(ConfirmArchive: archive, ConfirmRestoreFromArchive: restore, ConfirmDirtyWorkingTree: true));
            var saved = Assert.Single(await catalog.GetProjectsAsync());
            Assert.Equal(to, saved.Lifecycle);
            Assert.True(Directory.Exists(saved.ContainerPath));
            Assert.True(Directory.Exists(Path.Combine(saved.ContainerPath, "repository")));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Change_SameLifecycle_IsNoOp()
    {
        var root = NewRoot();
        try
        {
            var (catalog, service, project) = await SeedAsync(root);
            await service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.Active);
            Assert.Empty(await catalog.GetLifecycleEventsAsync(project.ProjectId));
            Assert.True(Directory.Exists(project.ContainerPath));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Archive_WithoutConfirm_IsRejected()
    {
        var root = NewRoot();
        try
        {
            var (catalog, service, project) = await SeedAsync(root);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.Archived));
            Assert.Equal(LifecycleState.Active, Assert.Single(await catalog.GetProjectsAsync()).Lifecycle);
            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.Empty(await catalog.GetLifecycleEventsAsync(project.ProjectId));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task UnknownTarget_IsDenied()
    {
        var root = NewRoot();
        try
        {
            var (_, service, project) = await SeedAsync(root);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, "Quarantine"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, "Nope"));
            Assert.True(Directory.Exists(project.ContainerPath));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task MissingSource_IsRejected_AndDoesNotCreateDestination()
    {
        var root = NewRoot();
        try
        {
            var (catalog, service, project) = await SeedAsync(root);
            Directory.Delete(project.ContainerPath, recursive: true);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold,
                    new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true)));
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            Assert.False(Directory.Exists(dest));
            Assert.Equal(LifecycleState.Missing, Assert.Single(await catalog.GetProjectsAsync()).Availability);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task PathDrift_IsRejected()
    {
        var root = NewRoot();
        try
        {
            var (catalog, service, project) = await SeedAsync(root);
            var drifted = Path.Combine(root, "elsewhere", "repo");
            Directory.CreateDirectory(Path.Combine(drifted, "repository"));
            await catalog.CommitLifecycleMoveAsync(project.ProjectId, LifecycleState.Active, drifted,
                Path.Combine(drifted, "repository"),
                new LifecycleEventRecord(0, Guid.NewGuid().ToString("N"), project.ProjectId, "Active", "Active",
                    null, LifecycleState.CatalogCommitted, null, null, "setup", DateTime.UtcNow));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold,
                    new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true)));
            Assert.Equal(LifecycleState.PathDrift, Assert.Single(await catalog.GetProjectsAsync()).Availability);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task DestinationCollision_TouchesNeitherSide()
    {
        var root = NewRoot();
        try
        {
            var (catalog, service, project) = await SeedAsync(root);
            var dest = new WorkspaceService().GetProjectContainer(root, LifecycleState.OnHold, project.Category, project.Owner, project.Repository);
            Directory.CreateDirectory(dest);
            await File.WriteAllTextAsync(Path.Combine(dest, "existing.txt"), "keep");
            await Assert.ThrowsAsync<IOException>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold,
                    new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true)));
            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(dest, "existing.txt")));
            Assert.Equal(LifecycleState.Active, Assert.Single(await catalog.GetProjectsAsync()).Lifecycle);
            Assert.DoesNotContain(await catalog.GetLifecycleEventsAsync(project.ProjectId),
                e => e.Result == LifecycleState.Completed);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task IndexLock_BlocksTransition()
    {
        var root = NewRoot();
        try
        {
            var (_, service, project) = await SeedAsync(root, withGit: true);
            Directory.CreateDirectory(Path.Combine(project.RepositoryPath, ".git"));
            await File.WriteAllTextAsync(Path.Combine(project.RepositoryPath, ".git", "index.lock"), "lock");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold,
                    new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true)));
            Assert.True(Directory.Exists(project.ContainerPath));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task DirtyWorktree_RejectedWithoutConfirm_AllowedWithConfirm()
    {
        var root = NewRoot();
        try
        {
            var (catalog, service, project) = await SeedAsync(root, withGit: true, dirty: true);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold));
            Assert.True(Directory.Exists(project.ContainerPath));
            await service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold,
                new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true));
            var saved = Assert.Single(await catalog.GetProjectsAsync());
            Assert.Equal("dirty", await File.ReadAllTextAsync(Path.Combine(saved.RepositoryPath, "dirty.txt")));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task ConcurrentProjectLock_DoesNotMove()
    {
        var root = NewRoot();
        var locks = Path.Combine(root, "locks");
        try
        {
            var (_, service, project) = await SeedAsync(root, lockDirectory: locks);
            var gate = new ProjectOperationLock(locks);
            await using var held = await gate.AcquireAsync("project:" + project.ProjectId);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                service.ChangeLifecycleAsync(project.ProjectId, LifecycleState.OnHold,
                    new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true), cts.Token));
            Assert.True(Directory.Exists(project.ContainerPath));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task FilterAndSearch_ByLifecycleCategoryAndText()
    {
        var root = NewRoot();
        try
        {
            var (catalog, service, first) = await SeedAsync(root, repo: "alpha");
            var second = await AddProjectAsync(root, catalog, "Unclassified", "beta");
            await service.ChangeLifecycleAsync(second.ProjectId, LifecycleState.OnHold,
                new LifecycleChangeOptions(ConfirmDirtyWorkingTree: true));
            var projects = await catalog.GetProjectsAsync();
            Assert.Contains(projects, p => p.Lifecycle == LifecycleState.OnHold && p.Repository == "beta");
            Assert.Contains(projects.Where(p => p.Repository.Contains("alpha", StringComparison.OrdinalIgnoreCase)),
                p => p.Lifecycle == LifecycleState.Active);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Migration_IsIdempotent_PreservesUnknownLifecycle_AndInsertsNoEvents()
    {
        var root = NewRoot();
        try
        {
            var db = Path.Combine(root, "00_Manager", "catalog", "projects.sqlite3");
            Directory.CreateDirectory(Path.GetDirectoryName(db)!);
            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(
                new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = db, Pooling = false }.ToString()))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE categories(name TEXT PRIMARY KEY, created_at_utc TEXT NOT NULL);
                    CREATE TABLE projects(
                        project_id TEXT PRIMARY KEY, canonical_remote_url TEXT NOT NULL UNIQUE,
                        host TEXT NOT NULL, owner TEXT NOT NULL, repository TEXT NOT NULL,
                        category TEXT NOT NULL, lifecycle TEXT NOT NULL, container_path TEXT NOT NULL,
                        repository_path TEXT NOT NULL, current_branch TEXT NOT NULL,
                        current_commit TEXT NOT NULL, imported_at_utc TEXT NOT NULL);
                    INSERT INTO categories(name, created_at_utc) VALUES ('Unclassified', '2026-01-01T00:00:00.0000000Z');
                    INSERT INTO projects VALUES('p1','https://github.com/a/a.git','github.com','a','a','Unclassified','Nope',
                        'C:/x','C:/x/repository','main','abc','2026-01-01T00:00:00.0000000Z');
                    INSERT INTO projects VALUES('p2','https://github.com/a/b.git','github.com','a','b','Unclassified','',
                        'C:/y','C:/y/repository','main','def','2026-01-01T00:00:00.0000000Z');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            await catalog.InitializeAsync();
            var projects = await catalog.GetProjectsAsync();
            Assert.Equal("Nope", Assert.Single(projects, p => p.ProjectId == "p1").Lifecycle);
            Assert.Equal(LifecycleState.Active, Assert.Single(projects, p => p.ProjectId == "p2").Lifecycle);
            Assert.Empty(await catalog.GetLifecycleEventsAsync());
            Assert.Equal("2.3", await catalog.GetSchemaVersionAsync());
        }
        finally { DeleteRoot(root); }
    }

    internal static async Task<(ProjectCatalog Catalog, ProjectLifecycleService Service, ProjectRecord Project)> SeedAsync(
        string root, string repo = "repo", bool withGit = false, bool dirty = false, string? lockDirectory = null)
    {
        var catalog = new ProjectCatalog(root);
        await catalog.InitializeAsync();
        var project = await AddProjectAsync(root, catalog, "Unclassified", repo, withGit, dirty);
        var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
        var service = new ProjectLifecycleService(root, catalog, journal, new GitProcess(),
            new ProjectOperationLock(lockDirectory ?? Path.Combine(root, "locks")));
        return (catalog, service, project);
    }

    internal static async Task<ProjectRecord> AddProjectAsync(
        string root, ProjectCatalog catalog, string category, string repo, bool withGit = false, bool dirty = false)
    {
        var container = Path.Combine(root, "01_Active", category, "owner", repo);
        var repository = Path.Combine(container, "repository");
        Directory.CreateDirectory(repository);
        Directory.CreateDirectory(Path.Combine(container, "backups"));
        Directory.CreateDirectory(Path.Combine(container, "notes"));
        if (withGit)
        {
            var git = new GitProcess();
            await git.RunAsync(repository, "init");
            await git.RunAsync(repository, "config", "user.name", "Git Manager Test");
            await git.RunAsync(repository, "config", "user.email", "git-manager-test@example.invalid");
            await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "committed");
            await git.RunAsync(repository, "add", ".");
            await git.RunAsync(repository, "commit", "-m", "init");
            if (dirty)
                await File.WriteAllTextAsync(Path.Combine(repository, "dirty.txt"), "dirty");
        }
        var project = new ProjectRecord
        {
            CanonicalRemoteUrl = $"https://github.com/owner/{repo}.git",
            Owner = "owner", Repository = repo, Category = category, Lifecycle = LifecycleState.Active,
            ContainerPath = container, RepositoryPath = repository
        };
        await catalog.AddProjectAsync(project);
        return project;
    }

    internal static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerLifecycleTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    internal static void DeleteRoot(string root)
    {
        if (!Directory.Exists(root)) return;
        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
        catch { }
    }
}
