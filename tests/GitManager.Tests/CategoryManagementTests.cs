using GitManager.App.Models;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class CategoryManagementTests
{
    [Fact]
    public async Task Initialize_IsIdempotent_AndPreservesCategoryIds()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var before = await catalog.GetCategoriesAsync();
            await catalog.InitializeAsync();
            var after = await catalog.GetCategoriesAsync();

            Assert.Equal(before.Select(x => x.CategoryId), after.Select(x => x.CategoryId));
            Assert.Contains(after, x => x.Name == "Unclassified" && x.IsSystem);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task CreateRenameAndDelete_OperateOnStableCategoryIdentity()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var service = new CategoryManagementService(catalog);

            var created = await service.CreateAsync("Research");
            await service.RenameAsync(created.CategoryId, "Research Lab");
            var renamed = Assert.Single((await service.GetCategoriesAsync()).Where(x => x.CategoryId == created.CategoryId));
            Assert.Equal("Research Lab", renamed.Name);
            Assert.Equal(created.CategoryId, renamed.CategoryId);

            await service.DeleteAsync(created.CategoryId);
            Assert.DoesNotContain(await service.GetCategoriesAsync(), x => x.CategoryId == created.CategoryId);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task DuplicateNamesAreRejectedCaseInsensitively()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            await catalog.CreateCategoryAsync("Research");
            await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.CreateCategoryAsync("research"));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task UnclassifiedCannotBeRenamedOrDeleted()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            var unclassified = Assert.Single((await catalog.GetCategoriesAsync()).Where(x => x.Name == "Unclassified"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => catalog.RenameEmptyCategoryAsync(unclassified.CategoryId, "Misc"));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => catalog.DeleteEmptyCategoryAsync(unclassified.CategoryId));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task CategoryWithProjectsCannotBeDeletedOrRenamedWithoutWorkspaceMove()
    {
        var root = NewRoot();
        try
        {
            var catalog = new ProjectCatalog(root);
            await catalog.InitializeAsync();
            await catalog.CreateCategoryAsync("Research");
            var project = new ProjectRecord
            {
                CanonicalRemoteUrl = "https://github.com/example/repo.git",
                Owner = "example", Repository = "repo", Category = "Research",
                ContainerPath = Path.Combine(root, "01_Active", "Research", "example", "repo"),
                RepositoryPath = Path.Combine(root, "01_Active", "Research", "example", "repo", "repository")
            };
            await catalog.AddProjectAsync(project);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => catalog.DeleteEmptyCategoryAsync(project.CategoryId));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => catalog.RenameEmptyCategoryAsync(project.CategoryId, "New Name"));

            var persisted = Assert.Single(await catalog.GetProjectsAsync());
            Assert.Equal(project.CategoryId, persisted.CategoryId);
            Assert.Equal("Research", persisted.Category);
        }
        finally { DeleteRoot(root); }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerCategoryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
