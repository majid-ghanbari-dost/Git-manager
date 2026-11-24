namespace GitManager.App.Services;

public sealed record CategoryMoveRecoveryReport(int RolledBack, int Completed, int RequiresAttention);

public sealed class CategoryMoveRecoveryService(
    string workspaceRoot,
    ProjectCatalog catalog,
    ProjectOperationJournal journal,
    ProjectOperationLock? operationLock = null)
{
    private readonly string _workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly ProjectOperationLock _operationLock = operationLock ?? new ProjectOperationLock();

    public async Task<CategoryMoveRecoveryReport> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var rolledBack = 0;
        var completed = 0;
        var attention = 0;
        foreach (var operation in await journal.GetInterruptedAsync(cancellationToken))
        {
            if (operation.OperationType != "CategoryMove") continue;
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(operation.ProjectId))
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "شناسه پروژه برای بازیابی انتقال موجود نیست؛ مسیرها حفظ شدند.", cancellationToken);
                attention++;
                continue;
            }

            var keyParts = operation.OperationKey.Split('|');
            var projectSnapshot = (await catalog.GetProjectsAsync())
                .FirstOrDefault(x => x.ProjectId == operation.ProjectId);
            var operationPathsWithinWorkspace = PathSafety.IsWithin(_workspaceRoot, operation.StagingPath) &&
                PathSafety.IsWithin(_workspaceRoot, operation.FinalPath);
            if (!operationPathsWithinWorkspace && projectSnapshot is null)
                continue; // Ignore journal entries belonging to another workspace.
            var categoryIds = keyParts.Length == 4 && keyParts[0] == "category-move"
                ? new[] { keyParts[1], keyParts[2] }
                : new[] { projectSnapshot?.CategoryId ?? "", keyParts.Length > 1 ? keyParts[1] : "" };
            var heldCategoryLeases = new List<IAsyncDisposable>();
            try
            {
                foreach (var categoryId in categoryIds.Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
                    heldCategoryLeases.Add(await _operationLock.AcquireAsync(
                        "category:" + categoryId, cancellationToken, TimeSpan.FromMilliseconds(100)));
            }
            catch (TimeoutException)
            {
                for (var i = heldCategoryLeases.Count - 1; i >= 0; i--)
                    await heldCategoryLeases[i].DisposeAsync();
                continue;
            }
            await using var categoryLeases = new CompositeLease(heldCategoryLeases);

            IAsyncDisposable projectLease;
            try
            {
                projectLease = await _operationLock.AcquireAsync(
                    "project:" + operation.ProjectId, cancellationToken, TimeSpan.FromMilliseconds(100));
            }
            catch (TimeoutException) { continue; }
            await using var operationLease = projectLease;

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
                var sourceProject = (await catalog.GetProjectsAsync()).FirstOrDefault(x => x.ProjectId == operation.ProjectId);
                if (sourceProject is not null && Path.GetFullPath(sourceProject.ContainerPath)
                    .Equals(source, StringComparison.OrdinalIgnoreCase))
                {
                    await journal.SetStateAsync(operation.OperationId, "RolledBack",
                        "انتقال پیش از جابه‌جایی پوشه متوقف شده بود؛ مبدأ و کاتالوگ حفظ شدند.", cancellationToken);
                    rolledBack++;
                }
                else
                {
                    await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                        "پوشه مبدأ وجود دارد اما مسیر ثبت‌شده در کاتالوگ با آن سازگار نیست؛ هیچ تغییری اعمال نشد.", cancellationToken);
                    attention++;
                }
                continue;
            }

            if (!sourceExists && destinationExists)
            {
                var project = (await catalog.GetProjectsAsync()).FirstOrDefault(x => x.ProjectId == operation.ProjectId);
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

                if (!Path.GetFullPath(project.ContainerPath).Equals(source, StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFullPath(project.RepositoryPath).Equals(
                        Path.Combine(source, "repository"), StringComparison.OrdinalIgnoreCase))
                {
                    await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                        "کاتالوگ به مسیر دیگری اشاره می‌کند؛ مقصد برای جلوگیری از جابه‌جایی اشتباه حفظ شد.", cancellationToken);
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

    private sealed class CompositeLease(List<IAsyncDisposable> leases) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            for (var i = leases.Count - 1; i >= 0; i--) await leases[i].DisposeAsync();
        }
    }

    private static bool CategoryMoveUnsafe(string path) =>
        ProjectCategoryMoveService.ContainsReparsePoint(path);
}
