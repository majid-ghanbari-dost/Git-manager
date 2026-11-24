namespace GitManager.App.Services;

public sealed record RecoveryReport(int RolledBack, int RequiresAttention, int AlreadyMissing);

public sealed class ProjectOperationRecoveryService
{
    private readonly ProjectOperationJournal _journal;
    private readonly ProjectOperationLock _operationLock;

    public ProjectOperationRecoveryService(ProjectOperationJournal journal, ProjectOperationLock? operationLock = null)
    {
        _journal = journal;
        _operationLock = operationLock ?? new ProjectOperationLock();
    }

    public async Task<RecoveryReport> RecoverAsync(CancellationToken cancellationToken = default)
    {
        await _journal.InitializeAsync(cancellationToken);
        var rolledBack = 0;
        var requiresAttention = 0;
        var alreadyMissing = 0;

        foreach (var operation in await _journal.GetInterruptedAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Category moves have a dedicated recovery routine that reconciles both paths and catalog state.
            if (operation.OperationType is "CategoryMove" or "CategoryRename") continue;
            if (operation.State == "RecoveryRequired")
            {
                requiresAttention++;
                continue;
            }
            IAsyncDisposable lease;
            try
            {
                lease = await _operationLock.AcquireAsync(
                    operation.OperationKey, cancellationToken, TimeSpan.FromMilliseconds(100));
            }
            catch (TimeoutException)
            {
                // A live process still owns this operation. Never recover its staging directory.
                continue;
            }

            await using var operationLease = lease;
            var stagingExists = Directory.Exists(operation.StagingPath) || File.Exists(operation.StagingPath);
            var finalExists = Directory.Exists(operation.FinalPath) || File.Exists(operation.FinalPath);

            if (finalExists)
            {
                await _journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "مسیر نهایی وجود دارد؛ برای جلوگیری از حذف داده، بازیابی خودکار انجام نشد.", cancellationToken);
                requiresAttention++;
                continue;
            }

            if (!stagingExists)
            {
                await _journal.SetStateAsync(operation.OperationId, "RolledBack",
                    "پس از راه‌اندازی مجدد، مسیر staging و مقصد نهایی وجود نداشتند.", cancellationToken);
                alreadyMissing++;
                continue;
            }

            if (!IsSafeStagingPath(operation) || ContainsReparsePoint(operation.StagingPath))
            {
                await _journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "مسیر staging با الگوی امن مطابقت ندارد یا شامل Reparse Point است؛ حذف خودکار ممنوع شد.", cancellationToken);
                requiresAttention++;
                continue;
            }

            try
            {
                if (Directory.Exists(operation.StagingPath))
                    Directory.Delete(operation.StagingPath, recursive: true);
                else
                    File.Delete(operation.StagingPath);

                await _journal.SetStateAsync(operation.OperationId, "RolledBack",
                    "داده‌های staging ناقص پس از بررسی مسیر امن پاک‌سازی شدند؛ پروژهٔ اصلی دست‌نخورده ماند.", cancellationToken);
                rolledBack++;
            }
            catch (Exception ex)
            {
                await _journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    $"پاک‌سازی staging انجام نشد: {ex.Message}", cancellationToken);
                requiresAttention++;
            }
        }

        return new RecoveryReport(rolledBack, requiresAttention, alreadyMissing);
    }

    private static bool IsSafeStagingPath(ProjectOperationEntry operation)
    {
        var staging = Path.GetFullPath(operation.StagingPath);
        var final = Path.GetFullPath(operation.FinalPath);
        if (!string.Equals(Path.GetDirectoryName(staging), Path.GetDirectoryName(final), StringComparison.OrdinalIgnoreCase)
            && operation.OperationType != "Import")
            return false;

        var name = Path.GetFileName(staging);
        return operation.OperationType switch
        {
            "Backup" => name.StartsWith(".staging-", StringComparison.Ordinal)
                && string.Equals(Path.GetDirectoryName(staging), Path.GetDirectoryName(final), StringComparison.OrdinalIgnoreCase),
            "Restore" => name.EndsWith(".staging", StringComparison.Ordinal)
                && string.Equals(staging, final + ".staging", StringComparison.OrdinalIgnoreCase),
            "Import" => name.Length == 32 && name.All(Uri.IsHexDigit)
                && Path.GetFileName(Path.GetDirectoryName(staging)) == "temp",
            _ => false
        };
    }

    private static bool ContainsReparsePoint(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var current = fullPath;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current) || File.Exists(current))
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        return true;
                }
                current = Path.GetDirectoryName(current);
            }

            if (Directory.Exists(fullPath))
            {
                var pending = new Stack<string>();
                pending.Push(fullPath);
                while (pending.Count > 0)
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            return true;
                        if ((attributes & FileAttributes.Directory) != 0)
                            pending.Push(entry);
                    }
                }
            }

            return false;
        }
        catch
        {
            // If the tree cannot be inspected completely, fail closed.
            return true;
        }
    }
}
