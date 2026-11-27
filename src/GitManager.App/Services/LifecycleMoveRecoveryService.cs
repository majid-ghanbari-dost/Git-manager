using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed record LifecycleMoveRecoveryReport(int RolledBack, int Completed, int RequiresAttention);

public sealed class LifecycleMoveRecoveryService(
    string workspaceRoot,
    ProjectCatalog catalog,
    ProjectOperationJournal journal,
    ProjectOperationLock? operationLock = null)
{
    private readonly string _workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly ProjectOperationLock _operationLock = operationLock ?? new ProjectOperationLock();

    public async Task<LifecycleMoveRecoveryReport> RecoverAsync(CancellationToken cancellationToken = default)
    {
        await journal.InitializeAsync(cancellationToken);
        var rolledBack = 0;
        var completed = 0;
        var attention = 0;
        foreach (var operation in await journal.GetInterruptedAsync(cancellationToken))
        {
            if (operation.OperationType != "LifecycleMove") continue;
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(operation.ProjectId))
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "شناسه پروژه برای بازیابی انتقال وضعیت موجود نیست.", cancellationToken);
                attention++;
                continue;
            }

            var operationPathsWithinWorkspace = PathSafety.IsWithin(_workspaceRoot, operation.StagingPath) &&
                PathSafety.IsWithin(_workspaceRoot, operation.FinalPath);
            var projectSnapshot = (await catalog.GetProjectsAsync())
                .FirstOrDefault(x => x.ProjectId == operation.ProjectId);
            if (!operationPathsWithinWorkspace && projectSnapshot is null)
                continue;

            IAsyncDisposable? categoryLease = null;
            try
            {
                var categoryId = projectSnapshot?.CategoryId;
                if (!string.IsNullOrWhiteSpace(categoryId))
                    categoryLease = await _operationLock.AcquireAsync(
                        "category:" + categoryId, cancellationToken, TimeSpan.FromMilliseconds(100));
            }
            catch (TimeoutException)
            {
                if (categoryLease is not null) await categoryLease.DisposeAsync();
                continue;
            }

            IAsyncDisposable projectLease;
            try
            {
                projectLease = await _operationLock.AcquireAsync(
                    "project:" + operation.ProjectId, cancellationToken, TimeSpan.FromMilliseconds(100));
            }
            catch (TimeoutException)
            {
                if (categoryLease is not null) await categoryLease.DisposeAsync();
                continue;
            }

            await using var heldCategory = categoryLease ?? new NoopLease();
            await using var heldProject = projectLease;

            var source = Path.GetFullPath(operation.StagingPath);
            var destination = Path.GetFullPath(operation.FinalPath);
            if (!PathSafety.IsWithin(_workspaceRoot, source) || !PathSafety.IsWithin(_workspaceRoot, destination) ||
                ProjectCategoryMoveService.ContainsReparsePoint(source) ||
                ProjectCategoryMoveService.ContainsReparsePoint(destination))
            {
                await FailClosed(operation, projectSnapshot, "مسیر انتقال خارج از کتابخانه یا شامل Reparse Point است.",
                    cancellationToken);
                attention++;
                continue;
            }

            var sourceExists = Directory.Exists(source);
            var destinationExists = Directory.Exists(destination);
            var project = (await catalog.GetProjectsAsync()).FirstOrDefault(x => x.ProjectId == operation.ProjectId);

            if (sourceExists && !destinationExists)
            {
                if (project is not null && Path.GetFullPath(project.ContainerPath)
                    .Equals(source, StringComparison.OrdinalIgnoreCase))
                {
                    await journal.SetStateAsync(operation.OperationId, "RolledBack",
                        "انتقال پیش از جابه‌جایی پوشه متوقف شده بود؛ مبدأ و کاتالوگ حفظ شدند.", cancellationToken);
                    await Append(project, operation, LifecycleState.RolledBack,
                        "انتقال پیش از جابه‌جایی پوشه متوقف شده بود.");
                    rolledBack++;
                }
                else
                {
                    await FailClosed(operation, project,
                        "پوشه مبدأ وجود دارد اما مسیر ثبت‌شده در کاتالوگ با آن سازگار نیست.", cancellationToken);
                    attention++;
                }
                continue;
            }

            if (!sourceExists && destinationExists)
            {
                if (project is not null && Path.GetFullPath(project.ContainerPath)
                    .Equals(destination, StringComparison.OrdinalIgnoreCase))
                {
                    await journal.SetStateAsync(operation.OperationId, "Completed",
                        "کاتالوگ و پوشه مقصد هر دو ثبت شده‌اند؛ انتقال کامل تأیید شد.", cancellationToken);
                    await catalog.MarkLifecycleOperationCompletedAsync(project.ProjectId,
                        Event(project, operation, LifecycleState.Completed, "بازیابی انتقال وضعیت را نهایی کرد."));
                    completed++;
                    continue;
                }

                if (project is not null && Path.GetFullPath(project.ContainerPath)
                    .Equals(source, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
                        Directory.Move(destination, source);
                        await journal.SetStateAsync(operation.OperationId, "RolledBack",
                            "پوشه به مبدأ بازگردانده شد؛ کاتالوگ تغییر نکرده بود.", cancellationToken);
                        await Append(project, operation, LifecycleState.RolledBack,
                            "پوشه به مبدأ بازگردانده شد؛ کاتالوگ تغییر نکرده بود.");
                        rolledBack++;
                    }
                    catch (Exception ex)
                    {
                        await FailClosed(operation, project, $"بازگردانی انتقال ناموفق بود: {ex.Message}",
                            cancellationToken);
                        attention++;
                    }
                    continue;
                }

                await FailClosed(operation, project, "کاتالوگ به مسیر دیگری اشاره می‌کند؛ هیچ پوشه‌ای حذف نشد.",
                    cancellationToken);
                attention++;
                continue;
            }

            await FailClosed(operation, project,
                "وضعیت مبدأ و مقصد مبهم است؛ برای جلوگیری از حذف یا بازنویسی، هر دو مسیر حفظ شدند.",
                cancellationToken);
            attention++;
        }

        return new LifecycleMoveRecoveryReport(rolledBack, completed, attention);
    }

    private async Task FailClosed(
        ProjectOperationEntry operation, ProjectRecord? project, string message, CancellationToken cancellationToken)
    {
        await journal.SetStateAsync(operation.OperationId, "RecoveryRequired", message, cancellationToken);
        if (project is not null)
            await Append(project, operation, LifecycleState.RecoveryRequired, message);
        await catalog.UpdateLastOperationStatusAsync(operation.ProjectId ?? "", "LifecycleMove:RecoveryRequired");
    }

    private Task Append(ProjectRecord project, ProjectOperationEntry operation, string result, string message) =>
        catalog.InsertLifecycleEventAsync(Event(project, operation, result, message));

    private static LifecycleEventRecord Event(
        ProjectRecord project, ProjectOperationEntry operation, string result, string message)
    {
        var parts = operation.OperationKey.Split('|');
        var to = parts.Length >= 3 ? parts[2] : project.Lifecycle;
        var from = parts.Length >= 2 ? parts[1] : project.Lifecycle;
        return new LifecycleEventRecord(0, Guid.NewGuid().ToString("N"), project.ProjectId, from, to,
            operation.OperationId, result, null, project.Availability, message, DateTime.UtcNow);
    }

    private sealed class NoopLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
