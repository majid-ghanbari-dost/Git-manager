namespace GitManager.App.Services;

public sealed record CategoryMoveRecoveryReport(int RolledBack, int Completed, int RequiresAttention);

public sealed class CategoryMoveRecoveryService(
    string workspaceRoot,
    ProjectCatalog catalog,
    ProjectOperationJournal journal)
{
    private readonly string _workspaceRoot = Path.GetFullPath(workspaceRoot);

    public async Task<CategoryMoveRecoveryReport> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var rolledBack = 0;
        var completed = 0;
        var attention = 0;
        var projects = await catalog.GetProjectsAsync();

        foreach (var operation in await journal.GetInterruptedAsync(cancellationToken))
        {
            if (operation.OperationType != "CategoryMove") continue;
            cancellationToken.ThrowIfCancellationRequested();

            var source = Path.GetFullPath(operation.StagingPath);
            var destination = Path.GetFullPath(operation.FinalPath);
            if (!PathSafety.IsWithin(_workspaceRoot, source) || !PathSafety.IsWithin(_workspaceRoot, destination) ||
                CategoryMoveUnsafe(source) || CategoryMoveUnsafe(destination))
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "مسیر انتقال خارج از کتابخانه یا شامل Reparse Point است؛ هیچ تغییری اعمال نشد.", cancellationToken);
                attention++;
                continue;
            }

            var sourceExists = Directory.Exists(source);
            var destinationExists = Directory.Exists(destination);
            if (sourceExists && !destinationExists)
            {
                await journal.SetStateAsync(operation.OperationId, "RolledBack",
                    "انتقال پیش از جابه‌جایی پوشه متوقف شده بود؛ مبدأ حفظ شد.", cancellationToken);
                rolledBack++;
                continue;
            }

            if (!sourceExists && destinationExists)
            {
                var project = projects.FirstOrDefault(x => x.ProjectId == operation.ProjectId);
                if (project is not null && Path.GetFullPath(project.ContainerPath)
                    .Equals(destination, StringComparison.OrdinalIgnoreCase))
                {
                    await journal.SetStateAsync(operation.OperationId, "Completed",
                        "کاتالوگ و پوشه مقصد هر دو ثبت شده‌اند؛ انتقال کامل تأیید شد.", cancellationToken);
                    completed++;
                    continue;
                }

                if (project is null)
                {
                    await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                        "رکورد پروژه پیدا نشد؛ مقصد برای بررسی دستی حفظ شد.", cancellationToken);
                    attention++;
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(source)!);
                    Directory.Move(destination, source);
                    await journal.SetStateAsync(operation.OperationId, "RolledBack",
                        "پوشه به مبدأ بازگردانده شد؛ کاتالوگ تغییر نکرده بود.", cancellationToken);
                    rolledBack++;
                }
                catch (Exception ex)
                {
                    await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                        $"بازگردانی انتقال ناموفق بود: {ex.Message}", cancellationToken);
                    attention++;
                }
                continue;
            }

            await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                "وضعیت مبدأ و مقصد مبهم است؛ برای جلوگیری از حذف یا بازنویسی، هر دو مسیر حفظ شدند.", cancellationToken);
            attention++;
        }

        return new CategoryMoveRecoveryReport(rolledBack, completed, attention);
    }

    private bool CategoryMoveUnsafe(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (CategoryMoveProjectMoveServicePlaceholder(path)) return true;
        if (parent is null || !Directory.Exists(parent)) return false;
        return ProjectCategoryMoveService.ContainsReparsePoint(parent);
    }

    private static bool CategoryMoveProjectMoveServicePlaceholder(string path) =>
        Directory.Exists(path) && ProjectCategoryMoveService.ContainsReparsePoint(path);
}
