using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed record CategoryRenameRecoveryReport(int RolledBack, int Completed, int RequiresAttention);

public sealed class CategoryRenameRecoveryService(
    string workspaceRoot,
    ProjectCatalog catalog,
    ProjectOperationJournal journal,
    ProjectOperationLock? operationLock = null)
{
    private readonly string _workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly ProjectOperationLock _operationLock = operationLock ?? new ProjectOperationLock();
    private readonly WorkspaceService _workspace = new();

    public async Task<CategoryRenameRecoveryReport> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var rolledBack = 0;
        var completed = 0;
        var attention = 0;

        foreach (var operation in await journal.GetInterruptedAsync(cancellationToken))
        {
            if (operation.OperationType != "CategoryRename") continue;
            cancellationToken.ThrowIfCancellationRequested();

            var parts = operation.OperationKey.Split('|');
            if (parts.Length != 4 || !Guid.TryParseExact(parts[1], "N", out _))
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "اطلاعات عملیات تغییر نام دسته‌بندی قابل خواندن نیست؛ داده‌ها حفظ شدند.", cancellationToken);
                attention++;
                continue;
            }

            if (!Path.GetFullPath(operation.StagingPath).Equals(_workspaceRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFullPath(operation.FinalPath).Equals(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
                continue; // The global journal may contain operations from a different workspace.

            var categoryId = parts[1];
            var oldName = Uri.UnescapeDataString(parts[2]);
            var newName = Uri.UnescapeDataString(parts[3]);
            IAsyncDisposable categoryLease;
            try
            {
                categoryLease = await _operationLock.AcquireAsync(
                    "category:" + categoryId, cancellationToken, TimeSpan.FromMilliseconds(100));
            }
            catch (TimeoutException) { continue; }
            await using var heldCategoryLease = categoryLease;

            var category = (await catalog.GetCategoriesAsync()).FirstOrDefault(x => x.CategoryId == categoryId);
            if (category is null)
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "رکورد دسته‌بندی پیدا نشد؛ مسیرهای پروژه‌ها حفظ شدند.", cancellationToken);
                attention++;
                continue;
            }

            var projects = (await catalog.GetProjectsAsync()).Where(x => x.CategoryId == categoryId)
                .OrderBy(x => x.ProjectId, StringComparer.Ordinal).ToList();
            var leases = new List<IAsyncDisposable>();
            try
            {
                foreach (var project in projects)
                    leases.Add(await _operationLock.AcquireAsync(
                        "project:" + project.ProjectId, cancellationToken, TimeSpan.FromMilliseconds(100)));
            }
            catch (TimeoutException)
            {
                for (var i = leases.Count - 1; i >= 0; i--) await leases[i].DisposeAsync();
                continue;
            }

            await using var heldProjectLeases = new CompositeLease(leases);
            if (category.Name.Equals(newName, StringComparison.Ordinal))
            {
                var allCommitted = projects.All(project =>
                {
                    var expected = Path.GetFullPath(_workspace.GetProjectContainer(
                        _workspaceRoot, project.Lifecycle, newName, project.Owner, project.Repository));
                    var oldPath = Path.GetFullPath(_workspace.GetProjectContainer(
                        _workspaceRoot, project.Lifecycle, oldName, project.Owner, project.Repository));
                    return Path.GetFullPath(project.ContainerPath).Equals(expected, StringComparison.OrdinalIgnoreCase)
                        && Directory.Exists(expected) && !Directory.Exists(oldPath)
                        && Path.GetFullPath(project.RepositoryPath).Equals(
                            Path.Combine(expected, "repository"), StringComparison.OrdinalIgnoreCase)
                        && !ProjectCategoryMoveService.ContainsReparsePoint(expected)
                        && !ProjectCategoryMoveService.ContainsReparsePoint(oldPath);
                });
                if (allCommitted)
                {
                    await journal.SetStateAsync(operation.OperationId, "Completed",
                        "نام دسته و تمام مسیرهای پروژه‌ها تغییر کرده‌اند؛ عملیات نهایی شد.", cancellationToken);
                    completed++;
                }
                else
                {
                    await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                        "نام دسته تغییر کرده اما وضعیت همه پوشه‌های پروژه با آن سازگار نیست؛ هیچ پوشه‌ای حذف نشد.", cancellationToken);
                    attention++;
                }
                continue;
            }

            if (!category.Name.Equals(oldName, StringComparison.Ordinal))
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "نام فعلی دسته با نام مبدأ و مقصد عملیات مطابقت ندارد؛ هیچ تغییری اعمال نشد.", cancellationToken);
                attention++;
                continue;
            }

            var rollbackPlan = new List<(ProjectRecord Project, string OldPath, string NewPath, bool MoveDirectory)>();
            var ambiguous = false;
            foreach (var project in projects)
            {
                var oldPath = Path.GetFullPath(_workspace.GetProjectContainer(
                    _workspaceRoot, project.Lifecycle, oldName, project.Owner, project.Repository));
                var newPath = Path.GetFullPath(_workspace.GetProjectContainer(
                    _workspaceRoot, project.Lifecycle, newName, project.Owner, project.Repository));
                var currentPath = Path.GetFullPath(project.ContainerPath);
                if (!Path.GetFullPath(project.RepositoryPath).Equals(
                    Path.Combine(currentPath, "repository"), StringComparison.OrdinalIgnoreCase))
                {
                    ambiguous = true;
                    break;
                }
                if (currentPath.Equals(oldPath, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(oldPath) && !Directory.Exists(newPath))
                    continue;
                if (currentPath.Equals(newPath, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(newPath) && !Directory.Exists(oldPath) &&
                    PathSafety.IsWithin(_workspaceRoot, oldPath) &&
                    PathSafety.IsWithin(_workspaceRoot, newPath) &&
                    !ProjectCategoryMoveService.ContainsReparsePoint(newPath) &&
                    !ProjectCategoryMoveService.ContainsReparsePoint(oldPath))
                {
                    rollbackPlan.Add((project, oldPath, newPath, true));
                    continue;
                }
                if (currentPath.Equals(oldPath, StringComparison.OrdinalIgnoreCase) &&
                    !Directory.Exists(oldPath) && Directory.Exists(newPath) &&
                    PathSafety.IsWithin(_workspaceRoot, oldPath) &&
                    PathSafety.IsWithin(_workspaceRoot, newPath) &&
                    !ProjectCategoryMoveService.ContainsReparsePoint(newPath) &&
                    !ProjectCategoryMoveService.ContainsReparsePoint(oldPath))
                {
                    // The process may have crashed after Directory.Move but before the catalog commit.
                    rollbackPlan.Add((project, oldPath, newPath, true));
                    continue;
                }
                if (currentPath.Equals(newPath, StringComparison.OrdinalIgnoreCase) &&
                    !Directory.Exists(newPath) && Directory.Exists(oldPath) &&
                    PathSafety.IsWithin(_workspaceRoot, oldPath) &&
                    !ProjectCategoryMoveService.ContainsReparsePoint(oldPath))
                {
                    // A previous recovery already moved the directory but crashed before catalog update.
                    rollbackPlan.Add((project, oldPath, newPath, false));
                    continue;
                }
                ambiguous = true;
                break;
            }

            if (ambiguous)
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "حداقل یک پروژه وضعیت مبهم دارد؛ تمام مسیرها برای بررسی دستی حفظ شدند.", cancellationToken);
                attention++;
                continue;
            }

            try
            {
                foreach (var item in rollbackPlan)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(item.OldPath)!);
                    if (item.MoveDirectory)
                        Directory.Move(item.NewPath, item.OldPath);
                    var repositoryPath = Path.Combine(item.OldPath, "repository");
                    await catalog.UpdateProjectCategoryAndPathAsync(item.Project.ProjectId,
                        categoryId, oldName, item.OldPath, repositoryPath);
                }
                await journal.SetStateAsync(operation.OperationId, "RolledBack",
                    "تغییر نام نیمه‌تمام به‌صورت امن به نام قبلی بازگردانده شد.", cancellationToken);
                rolledBack++;
            }
            catch (Exception ex)
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    $"بازیابی دسته‌بندی کامل نشد: {ex.Message}", cancellationToken);
                attention++;
            }
        }

        return new CategoryRenameRecoveryReport(rolledBack, completed, attention);
    }

    private sealed class CompositeLease(List<IAsyncDisposable> leases) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            for (var i = leases.Count - 1; i >= 0; i--) await leases[i].DisposeAsync();
        }
    }
}
