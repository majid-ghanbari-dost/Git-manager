using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class CategoryManagementService(ProjectCatalog catalog, CategoryRenameService? renameService = null)
{
    public Task<List<CategoryRecord>> GetCategoriesAsync() => catalog.GetCategoriesAsync();

    public Task<CategoryRecord> CreateAsync(string name) => catalog.CreateCategoryAsync(name);

    public async Task RenameAsync(string categoryId, string newName)
    {
        var hasProjects = (await catalog.GetProjectsAsync()).Any(x => x.CategoryId == categoryId);
        if (hasProjects)
        {
            if (renameService is null)
                throw new InvalidOperationException("برای تغییر نام دسته دارای پروژه، سرویس انتقال امن لازم است.");
            await renameService.RenameAsync(categoryId, newName);
            return;
        }
        await catalog.RenameEmptyCategoryAsync(categoryId, newName);
    }

    public Task DeleteAsync(string categoryId) =>
        catalog.DeleteEmptyCategoryAsync(categoryId);
}
