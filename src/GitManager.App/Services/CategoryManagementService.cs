using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class CategoryManagementService(ProjectCatalog catalog)
{
    public Task<List<CategoryRecord>> GetCategoriesAsync() => catalog.GetCategoriesAsync();

    public Task<CategoryRecord> CreateAsync(string name) => catalog.CreateCategoryAsync(name);

    public Task RenameAsync(string categoryId, string newName) =>
        catalog.RenameEmptyCategoryAsync(categoryId, newName);

    public Task DeleteAsync(string categoryId) =>
        catalog.DeleteEmptyCategoryAsync(categoryId);
}
