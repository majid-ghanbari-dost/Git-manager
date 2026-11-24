using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class ProjectCategoryMoveService(
    string workspaceRoot,
    ProjectCatalog catalog,
    ProjectOperationJournal journal,
    ProjectOperationLock? operationLock = null)
{
    private readonly string _workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly ProjectOperationLock _operationLock = operationLock ?? new ProjectOperationLock();
    private readonly WorkspaceService _workspace = new();

    public async Task<string> MoveProjectAsync(
        string projectId, string targetCategoryId,
        CancellationToken cancellationToken = default)
    {
        var projects = await catalog.GetProjectsAsync();
        var project = projects.FirstOrDefault(x => x.ProjectId == projectId)
            ?? throw new InvalidOperationException("پروژه انتخاب‌شده در کاتالوگ پیدا نشد.");
        var categories = await catalog.GetCategoriesAsync();
        var category = categories.FirstOrDefault(x => x.CategoryId == targetCategoryId)
            ?? throw new InvalidOperationException("دسته‌بندی مقصد وجود ندارد.");
        if (project.CategoryId == category.CategoryId) return project.ContainerPath;

        var source = Path.GetFullPath(project.ContainerPath);
        var destination = Path.GetFullPath(_workspace.GetProjectContainer(
            _workspaceRoot, project.Lifecycle, category.Name, project.Owner, project.Repository));
        if (!PathSafety.IsWithin(_workspaceRoot, source) || !PathSafety.IsWithin(_workspaceRoot, destination))
            throw new InvalidOperationException("مسیر مبدأ یا مقصد خارج از کتابخانه است.");
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException("پوشه پروژه در مسیر ثبت‌شده پیدا نشد.");
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("پوشه مقصد از قبل وجود دارد؛ هیچ فایلی بازنویسی نشد.");
        if (ContainsReparsePoint(source))
            throw new IOException("پروژه یا یکی از والدهای آن شامل Reparse Point است؛ انتقال خودکار متوقف شد.");

        var operationKey = BuildOperationKey(category.CategoryId, category.Name);
        await using var lease = await _operationLock.AcquireAsync("project:" + project.ProjectId, cancellationToken);
        var operation = await journal.BeginAsync("CategoryMove", operationKey, project.ProjectId,
            source, destination, cancellationToken);
        var moved = false;
        var catalogUpdated = false;
        try
        {
            await journal.SetStateAsync(operation.OperationId, "Running", cancellationToken: cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(source, destination);
            moved = true;
            var repositoryPath = Path.Combine(destination, Path.GetRelativePath(source, project.RepositoryPath));
            await catalog.UpdateProjectCategoryAndPathAsync(project.ProjectId, category.CategoryId,
                category.Name, destination, repositoryPath);
            catalogUpdated = true;
            await journal.SetStateAsync(operation.OperationId, "Completed", cancellationToken: cancellationToken);
            return destination;
        }
        catch (Exception ex)
        {
            if (catalogUpdated)
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    $"پوشه و کاتالوگ منتقل شده‌اند؛ ثبت وضعیت نهایی نیازمند بازیابی است: {ex.Message}", CancellationToken.None);
                throw;
            }

            try
            {
                if (moved && Directory.Exists(destination) && !Directory.Exists(source))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(source)!);
                    Directory.Move(destination, source);
                }
                await journal.SetStateAsync(operation.OperationId, "RolledBack", ex.Message, CancellationToken.None);
            }
            catch (Exception rollbackError)
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    $"{ex.Message} | بازگردانی انتقال ناموفق بود: {rollbackError.Message}", CancellationToken.None);
            }
            throw;
        }
    }

    internal static string BuildOperationKey(string categoryId, string categoryName) =>
        $"category-move|{categoryId}|{Uri.EscapeDataString(categoryName)}";

    internal static bool ContainsReparsePoint(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var current = fullPath;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current) || File.Exists(current))
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
                current = Path.GetDirectoryName(current);
            }

            if (Directory.Exists(fullPath))
            {
                var pending = new Stack<string>();
                pending.Push(fullPath);
                while (pending.Count > 0)
                    foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) return true;
                        if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                    }
            }
            return false;
        }
        catch { return true; }
    }
}
