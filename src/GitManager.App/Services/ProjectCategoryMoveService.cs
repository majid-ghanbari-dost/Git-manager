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

    public Task<string> MoveProjectAsync(
        string projectId, string targetCategoryId,
        CancellationToken cancellationToken = default) =>
        MoveProjectCoreAsync(projectId, targetCategoryId, null, false, cancellationToken);

    public Task<string> MoveProjectToNameAsync(
        string projectId, string categoryId, string categoryName,
        CancellationToken cancellationToken = default) =>
        MoveProjectCoreAsync(projectId, categoryId, categoryName, true, cancellationToken);

    private async Task<string> MoveProjectCoreAsync(
        string projectId, string targetCategoryId, string? targetCategoryName,
        bool categoryLockAlreadyHeld, CancellationToken cancellationToken)
    {
        var projects = await catalog.GetProjectsAsync();
        var project = projects.FirstOrDefault(x => x.ProjectId == projectId)
            ?? throw new InvalidOperationException("پروژه انتخاب‌شده در کاتالوگ پیدا نشد.");
        var categories = await catalog.GetCategoriesAsync();
        var category = categories.FirstOrDefault(x => x.CategoryId == targetCategoryId)
            ?? throw new InvalidOperationException("دسته‌بندی مقصد وجود ندارد.");
        var destinationCategoryName = targetCategoryName ?? category.Name;
        if (project.CategoryId == category.CategoryId &&
            project.Category.Equals(destinationCategoryName, StringComparison.Ordinal)) return project.ContainerPath;

        var source = Path.GetFullPath(project.ContainerPath);
        var destination = Path.GetFullPath(_workspace.GetProjectContainer(
            _workspaceRoot, project.Lifecycle, destinationCategoryName, project.Owner, project.Repository));
        var expectedSource = Path.GetFullPath(_workspace.GetProjectContainer(
            _workspaceRoot, project.Lifecycle, project.Category, project.Owner, project.Repository));
        if (!PathSafety.IsWithin(_workspaceRoot, source) || !PathSafety.IsWithin(_workspaceRoot, destination) ||
            !source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFullPath(project.RepositoryPath).Equals(
                Path.Combine(source, "repository"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("مسیر ثبت‌شده پروژه با ساختار کتابخانه سازگار نیست؛ انتقال خودکار متوقف شد.");
        if (!Directory.Exists(source) || !Directory.Exists(project.RepositoryPath))
            throw new DirectoryNotFoundException("پوشه پروژه یا مخزن Git در مسیر ثبت‌شده پیدا نشد.");
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("پوشه مقصد از قبل وجود دارد؛ هیچ فایلی بازنویسی نشد.");
        if (HasReparsePointInAncestors(Path.GetDirectoryName(destination)!))
            throw new IOException("مسیر والد مقصد شامل Reparse Point است؛ انتقال خودکار متوقف شد.");
        if (ContainsReparsePoint(source))
            throw new IOException("پروژه یا یکی از والدهای آن شامل Reparse Point است؛ انتقال خودکار متوقف شد.");

        var operationKey = BuildOperationKey(project.CategoryId, category.CategoryId, destinationCategoryName);
        await using var categoryLeases = categoryLockAlreadyHeld
            ? null
            : await AcquireCategoryLocksAsync(project.CategoryId, category.CategoryId, cancellationToken);
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
                destinationCategoryName, destination, repositoryPath, allowPendingCategoryRename: targetCategoryName is not null);
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

    private async Task<IAsyncDisposable> AcquireCategoryLocksAsync(
        string sourceCategoryId, string destinationCategoryId, CancellationToken cancellationToken)
    {
        var leases = new List<IAsyncDisposable>();
        try
        {
            foreach (var categoryId in new[] { sourceCategoryId, destinationCategoryId }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal))
                leases.Add(await _operationLock.AcquireAsync("category:" + categoryId, cancellationToken));
            return new CompositeLease(leases);
        }
        catch
        {
            for (var i = leases.Count - 1; i >= 0; i--) await leases[i].DisposeAsync();
            throw;
        }
    }

    private sealed class CompositeLease(List<IAsyncDisposable> leases) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            for (var i = leases.Count - 1; i >= 0; i--) await leases[i].DisposeAsync();
        }
    }

    internal static string BuildOperationKey(string sourceCategoryId, string targetCategoryId, string categoryName) =>
        $"category-move|{sourceCategoryId}|{targetCategoryId}|{Uri.EscapeDataString(categoryName)}";

    private static bool HasReparsePointInAncestors(string path)
    {
        try
        {
            var current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current) || File.Exists(current))
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
                current = Path.GetDirectoryName(current);
            }
            return false;
        }
        catch { return true; }
    }

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
