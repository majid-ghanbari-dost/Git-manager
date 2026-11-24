using GitManager.App.Models;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class ProjectCategoryMoveTests
{
    [Fact]
    public async Task MoveProject_MovesWholeContainerAndUpdatesCatalog()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var target = await catalog.CreateCategoryAsync("Archive Tools");
            var project = await AddProjectAsync(root, catalog, "Unclassified");
            var marker = Path.Combine(project.ContainerPath, "notes", "keep.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            await File.WriteAllTextAsync(marker, "preserve");

            var mover = new ProjectCategoryMoveService(root, catalog,
                new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3")),
                new ProjectOperationLock(Path.Combine(root, "locks")));

            var destination = await mover.MoveProjectAsync(project.ProjectId, target.CategoryId);

            Assert.False(Directory.Exists(project.ContainerPath));
            Assert.True(File.Exists(Path.Combine(destination, "notes", "keep.txt")));
            Assert.Equal("preserve", await File.ReadAllTextAsync(Path.Combine(destination, "notes", "keep.txt")));
            var saved = Assert.Single(await catalog.GetProjectsAsync());
            Assert.Equal(target.CategoryId, saved.CategoryId);
            Assert.Equal(target.Name, saved.Category);
            Assert.Equal(destination, saved.ContainerPath);
            Assert.Equal(Path.Combine(destination, "repository"), saved.RepositoryPath);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task MoveProject_RejectsExistingDestinationWithoutTouchingEitherProject()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var target = await catalog.CreateCategoryAsync("Destination");
            var project = await AddProjectAsync(root, catalog, "Unclassified");
            var destination = new WorkspaceService().GetProjectContainer(root, "Active", target.Name, "owner", "repo");
            Directory.CreateDirectory(destination);
            await File.WriteAllTextAsync(Path.Combine(destination, "existing.txt"), "do not overwrite");

            var mover = new ProjectCategoryMoveService(root, catalog,
                new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3")),
                new ProjectOperationLock(Path.Combine(root, "locks")));

            await Assert.ThrowsAsync<IOException>(() => mover.MoveProjectAsync(project.ProjectId, target.CategoryId));

            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.Equal("do not overwrite", await File.ReadAllTextAsync(Path.Combine(destination, "existing.txt")));
            var saved = Assert.Single(await catalog.GetProjectsAsync());
            Assert.Equal("Unclassified", saved.Category);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_RollsBackMoveWhenCatalogStillPointsToSource()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var target = await catalog.CreateCategoryAsync("Target");
            var project = await AddProjectAsync(root, catalog, "Unclassified");
            var destination = new WorkspaceService().GetProjectContainer(root, "Active", target.Name, "owner", "repo");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var sourceCategory = Assert.Single(await catalog.GetCategoriesAsync(), x => x.Name == "Unclassified");
            var key = $"category-move|{sourceCategory.CategoryId}|{target.CategoryId}|{Uri.EscapeDataString(target.Name)}";
            var operation = await journal.BeginAsync("CategoryMove", key, project.ProjectId,
                project.ContainerPath, destination);
            await journal.SetStateAsync(operation.OperationId, "Running");
            Directory.Move(project.ContainerPath, destination);

            var report = await new CategoryMoveRecoveryService(root, catalog, journal).RecoverAsync();

            Assert.Equal(1, report.RolledBack);
            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.False(Directory.Exists(destination));
            Assert.Equal(project.ContainerPath, Assert.Single(await catalog.GetProjectsAsync()).ContainerPath);
            Assert.Empty(await journal.GetInterruptedAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_CompletesMoveWhenCatalogAlreadyPointsToDestination()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var target = await catalog.CreateCategoryAsync("Target");
            var project = await AddProjectAsync(root, catalog, "Unclassified");
            var destination = new WorkspaceService().GetProjectContainer(root, "Active", target.Name, "owner", "repo");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var sourceCategory = Assert.Single(await catalog.GetCategoriesAsync(), x => x.Name == "Unclassified");
            var key = $"category-move|{sourceCategory.CategoryId}|{target.CategoryId}|{Uri.EscapeDataString(target.Name)}";
            var operation = await journal.BeginAsync("CategoryMove", key, project.ProjectId,
                project.ContainerPath, destination);
            await journal.SetStateAsync(operation.OperationId, "CommitPending");
            Directory.Move(project.ContainerPath, destination);
            await catalog.UpdateProjectCategoryAndPathAsync(project.ProjectId, target.CategoryId,
                target.Name, destination, Path.Combine(destination, "repository"));

            var report = await new CategoryMoveRecoveryService(root, catalog, journal).RecoverAsync();

            Assert.Equal(1, report.Completed);
            Assert.True(Directory.Exists(destination));
            Assert.False(Directory.Exists(project.ContainerPath));
            Assert.Equal(destination, Assert.Single(await catalog.GetProjectsAsync()).ContainerPath);
            Assert.Empty(await journal.GetInterruptedAsync());
        }
        finally { DeleteRoot(root); }
    }

    private static async Task<ProjectRecord> AddProjectAsync(string root, ProjectCatalog catalog, string category)
    {
        var container = Path.Combine(root, "01_Active", category, "owner", "repo");
        Directory.CreateDirectory(Path.Combine(container, "repository"));
        Directory.CreateDirectory(Path.Combine(container, "backups"));
        Directory.CreateDirectory(Path.Combine(container, "notes"));
        var project = new ProjectRecord
        {
            CanonicalRemoteUrl = "https://github.com/owner/repo.git",
            Owner = "owner", Repository = "repo", Category = category,
            Lifecycle = "Active", ContainerPath = container,
            RepositoryPath = Path.Combine(container, "repository")
        };
        await catalog.AddProjectAsync(project);
        return project;
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerProjectMoveTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
