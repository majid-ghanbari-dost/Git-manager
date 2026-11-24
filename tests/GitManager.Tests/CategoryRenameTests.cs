using GitManager.App.Models;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class CategoryRenameTests
{
    [Fact]
    public async Task RenamePopulatedCategory_MovesEveryProjectAndPreservesStableId()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var category = await catalog.CreateCategoryAsync("Project Group");
            var first = await AddProjectAsync(root, catalog, category.Name, "alpha");
            var second = await AddProjectAsync(root, catalog, category.Name, "beta");
            await File.WriteAllTextAsync(Path.Combine(first.ContainerPath, "notes", "alpha.txt"), "keep-a");
            await File.WriteAllTextAsync(Path.Combine(second.ContainerPath, "notes", "beta.txt"), "keep-b");

            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var service = new CategoryManagementService(catalog, new CategoryRenameService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks"))));

            await service.RenameAsync(category.CategoryId, "Renamed Group");

            var renamed = Assert.Single(await catalog.GetCategoriesAsync(), x => x.CategoryId == category.CategoryId);
            Assert.Equal("Renamed Group", renamed.Name);
            var projects = await catalog.GetProjectsAsync();
            Assert.Equal(2, projects.Count);
            Assert.All(projects, project =>
            {
                Assert.Equal(category.CategoryId, project.CategoryId);
                Assert.Equal("Renamed Group", project.Category);
                Assert.Contains($"{Path.DirectorySeparatorChar}Renamed Group{Path.DirectorySeparatorChar}", project.ContainerPath);
                Assert.True(Directory.Exists(project.ContainerPath));
            });
            Assert.Equal("keep-a", await File.ReadAllTextAsync(Path.Combine(projects.Single(x => x.Repository == "alpha").ContainerPath, "notes", "alpha.txt")));
            Assert.Equal("keep-b", await File.ReadAllTextAsync(Path.Combine(projects.Single(x => x.Repository == "beta").ContainerPath, "notes", "beta.txt")));
            Assert.Empty(await journal.GetInterruptedAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task RenamePopulatedCategory_RollsBackEarlierMovesWhenLaterDestinationCollides()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var category = await catalog.CreateCategoryAsync("Original Group");
            var first = await AddProjectAsync(root, catalog, category.Name, "alpha");
            var second = await AddProjectAsync(root, catalog, category.Name, "beta");
            var collision = new WorkspaceService().GetProjectContainer(root, "Active", "New Group", "owner", "beta");
            Directory.CreateDirectory(collision);
            await File.WriteAllTextAsync(Path.Combine(collision, "do-not-touch.txt"), "keep");

            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var service = new CategoryRenameService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks")));

            await Assert.ThrowsAsync<IOException>(() => service.RenameAsync(category.CategoryId, "New Group"));

            var savedCategory = Assert.Single(await catalog.GetCategoriesAsync(), x => x.CategoryId == category.CategoryId);
            Assert.Equal("Original Group", savedCategory.Name);
            var projects = await catalog.GetProjectsAsync();
            Assert.All(projects, project => Assert.Equal("Original Group", project.Category));
            Assert.True(Directory.Exists(first.ContainerPath));
            Assert.True(Directory.Exists(second.ContainerPath));
            Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(collision, "do-not-touch.txt")));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_RollsBackPartiallyMovedBatchWhenCategoryRowStillHasOldName()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var category = await catalog.CreateCategoryAsync("Before");
            var project = await AddProjectAsync(root, catalog, category.Name, "alpha");
            var newPath = new WorkspaceService().GetProjectContainer(root, "Active", "After", "owner", "alpha");
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);

            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var parentKey = $"category-rename|{category.CategoryId}|Before|After";
            var parent = await journal.BeginAsync("CategoryRename", parentKey, null, root, root);
            await journal.SetStateAsync(parent.OperationId, "Running");
            var childKey = $"category-move|{category.CategoryId}|{category.CategoryId}|After";
            var child = await journal.BeginAsync("CategoryMove", childKey, project.ProjectId, project.ContainerPath, newPath);
            await journal.SetStateAsync(child.OperationId, "Running");
            Directory.Move(project.ContainerPath, newPath);
            await catalog.UpdateProjectCategoryAndPathAsync(project.ProjectId, category.CategoryId,
                "After", newPath, Path.Combine(newPath, "repository"), allowPendingCategoryRename: true);

            var renameReport = await new CategoryRenameRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks"))).RecoverAsync();
            var moveReport = await new CategoryMoveRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks"))).RecoverAsync();

            Assert.Equal(1, renameReport.RolledBack);
            Assert.Equal(1, moveReport.RolledBack);
            Assert.True(Directory.Exists(project.ContainerPath));
            Assert.False(Directory.Exists(newPath));
            var saved = Assert.Single(await catalog.GetProjectsAsync());
            Assert.Equal("Before", saved.Category);
            Assert.Equal(project.ContainerPath, saved.ContainerPath);
            Assert.Empty(await journal.GetInterruptedAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_FinalizesBatchWhenCategoryRowAndAllProjectPathsAreRenamed()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var category = await catalog.CreateCategoryAsync("Before");
            var project = await AddProjectAsync(root, catalog, category.Name, "alpha");
            var newPath = new WorkspaceService().GetProjectContainer(root, "Active", "After", "owner", "alpha");
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);

            var journal = new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3"));
            var parentKey = $"category-rename|{category.CategoryId}|Before|After";
            var parent = await journal.BeginAsync("CategoryRename", parentKey, null, root, root);
            await journal.SetStateAsync(parent.OperationId, "CommitPending");
            Directory.Move(project.ContainerPath, newPath);
            await catalog.UpdateProjectCategoryAndPathAsync(project.ProjectId, category.CategoryId,
                "After", newPath, Path.Combine(newPath, "repository"), allowPendingCategoryRename: true);
            await catalog.RenameCategoryRecordAsync(category.CategoryId, "After");

            var report = await new CategoryRenameRecoveryService(root, catalog, journal,
                new ProjectOperationLock(Path.Combine(root, "locks"))).RecoverAsync();

            Assert.Equal(1, report.Completed);
            Assert.Equal("After", Assert.Single(await catalog.GetCategoriesAsync(), x => x.CategoryId == category.CategoryId).Name);
            Assert.Equal(newPath, Assert.Single(await catalog.GetProjectsAsync()).ContainerPath);
            Assert.Empty(await journal.GetInterruptedAsync());
        }
        finally { DeleteRoot(root); }
    }

    private static async Task<ProjectRecord> AddProjectAsync(string root, ProjectCatalog catalog, string category, string repo)
    {
        var container = Path.Combine(root, "01_Active", category, "owner", repo);
        Directory.CreateDirectory(Path.Combine(container, "repository"));
        Directory.CreateDirectory(Path.Combine(container, "backups"));
        Directory.CreateDirectory(Path.Combine(container, "notes"));
        var project = new ProjectRecord
        {
            CanonicalRemoteUrl = $"https://github.com/owner/{repo}.git",
            Owner = "owner", Repository = repo, Category = category, Lifecycle = "Active",
            ContainerPath = container, RepositoryPath = Path.Combine(container, "repository")
        };
        await catalog.AddProjectAsync(project);
        return project;
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerCategoryRenameTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
