using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class CategoryRenameService(
    string workspaceRoot,
    ProjectCatalog catalog,
    ProjectOperationJournal journal,
    ProjectOperationLock? operationLock = null)
{
    private readonly string _workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly ProjectOperationLock _operationLock = operationLock ?? new ProjectOperationLock();
    private readonly ProjectCategoryMoveService _mover =
        new(workspaceRoot, catalog, journal, operationLock);

    public async Task RenameAsync(string categoryId, string newName, CancellationToken cancellationToken = default)
    {
        var categories = await catalog.GetCategoriesAsync();
        var category = categories.FirstOrDefault(x => x.CategoryId == categoryId)
            ?? throw new InvalidOperationException("دسته‌بندی پیدا نشد.");
        if (category.IsSystem || category.Name.Equals("Unclassified", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("دسته‌بندی Unclassified سیستمی است و قابل تغییر نام نیست.");

        var normalized = ValidateName(newName);
        if (categories.Any(x => x.CategoryId != categoryId &&
            x.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("دسته‌بندی با این نام از قبل وجود دارد.");
        if (category.Name.Equals(normalized, StringComparison.Ordinal))
            return;

        var projects = (await catalog.GetProjectsAsync())
            .Where(x => x.CategoryId == categoryId).ToList();
        if (projects.Count == 0)
        {
            await catalog.RenameEmptyCategoryAsync(categoryId, normalized);
            return;
        }

        await using var categoryLease = await _operationLock.AcquireAsync("category:" + categoryId, cancellationToken);
        var operationKey = BuildOperationKey(categoryId, category.Name, normalized);
        var operation = await journal.BeginAsync("CategoryRename", operationKey, null,
            _workspaceRoot, _workspaceRoot, cancellationToken);
        var movedProjectIds = new List<string>();
        var categoryRenamed = false;

        try
        {
            await journal.SetStateAsync(operation.OperationId, "Running", cancellationToken: cancellationToken);
            foreach (var project in projects)
            {
                await _mover.MoveProjectToNameAsync(project.ProjectId, categoryId, normalized, cancellationToken);
                movedProjectIds.Add(project.ProjectId);
            }

            await catalog.RenameCategoryRecordAsync(categoryId, normalized);
            categoryRenamed = true;
            await journal.SetStateAsync(operation.OperationId, "Completed", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            if (categoryRenamed)
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    $"پروژه‌ها و نام دسته‌بندی تغییر کرده‌اند؛ تأیید وضعیت نهایی لازم است: {ex.Message}",
                    CancellationToken.None);
                throw;
            }

            var rollbackErrors = new List<string>();
            var currentProjects = (await catalog.GetProjectsAsync())
                .Where(x => x.CategoryId == categoryId).ToList();
            var rollbackCandidates = new HashSet<string>(movedProjectIds, StringComparer.Ordinal);
            foreach (var project in currentProjects)
            {
                var expectedNewPath = Path.GetFullPath(_workspace.GetProjectContainer(
                    _workspaceRoot, project.Lifecycle, normalized, project.Owner, project.Repository));
                if (project.Category.Equals(normalized, StringComparison.Ordinal) ||
                    Path.GetFullPath(project.ContainerPath).Equals(expectedNewPath, StringComparison.OrdinalIgnoreCase))
                    rollbackCandidates.Add(project.ProjectId);
            }

            foreach (var projectId in rollbackCandidates.Reverse())
            {
                try
                {
                    await _mover.MoveProjectToNameAsync(projectId, categoryId, category.Name, CancellationToken.None);
                }
                catch (Exception rollbackError) { rollbackErrors.Add(rollbackError.Message); }
            }

            var refreshedProjects = (await catalog.GetProjectsAsync())
                .Where(x => x.CategoryId == categoryId).ToList();
            var allAtOldPaths = refreshedProjects.All(project =>
            {
                var oldPath = Path.GetFullPath(_workspace.GetProjectContainer(
                    _workspaceRoot, project.Lifecycle, category.Name, project.Owner, project.Repository));
                var newPath = Path.GetFullPath(_workspace.GetProjectContainer(
                    _workspaceRoot, project.Lifecycle, normalized, project.Owner, project.Repository));
                return Path.GetFullPath(project.ContainerPath).Equals(oldPath, StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(oldPath) && !Directory.Exists(newPath);
            });

            if (rollbackErrors.Count == 0 && allAtOldPaths)
                await journal.SetStateAsync(operation.OperationId, "RolledBack", ex.Message, CancellationToken.None);
            else
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    $"{ex.Message} | بازگردانی کامل نشد: {string.Join(" | ", rollbackErrors)}",
                    CancellationToken.None);
            throw;
        }
    }

    private readonly WorkspaceService _workspace = new();

    internal static string BuildOperationKey(string categoryId, string oldName, string newName) =>
        $"category-rename|{categoryId}|{Uri.EscapeDataString(oldName)}|{Uri.EscapeDataString(newName)}";

    private static string ValidateName(string name)
    {
        var normalized = (name ?? "").Trim();
        if (normalized.Length is < 2 or > 48 || normalized.Equals("Unclassified", StringComparison.OrdinalIgnoreCase) ||
            normalized.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0 ||
            normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || normalized is "." or ".." ||
            !PathSafety.SafeSegment(normalized).Equals(normalized, StringComparison.Ordinal))
            throw new ArgumentException("نام دسته‌بندی باید ۲ تا ۴۸ نویسه و با قواعد نام‌گذاری پوشه در ویندوز سازگار باشد.", nameof(name));
        return normalized;
    }
}
