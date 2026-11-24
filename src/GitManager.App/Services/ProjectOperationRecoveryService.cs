namespace GitManager.App.Services;

public sealed record RecoveryReport(int RolledBack, int RequiresAttention, int AlreadyMissing);

public sealed class ProjectOperationRecoveryService(ProjectOperationJournal journal)
{
    public async Task<RecoveryReport> RecoverAsync(CancellationToken cancellationToken = default)
    {
        await journal.InitializeAsync(cancellationToken);
        var rolledBack = 0;
        var requiresAttention = 0;
        var alreadyMissing = 0;

        foreach (var operation in await journal.GetInterruptedAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stagingExists = Directory.Exists(operation.StagingPath) || File.Exists(operation.StagingPath);
            var finalExists = Directory.Exists(operation.FinalPath) || File.Exists(operation.FinalPath);

            if (finalExists)
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "مسیر نهایی وجود دارد؛ برای جلوگیری از حذف داده، بازیابی خودکار انجام نشد.", cancellationToken);
                requiresAttention++;
                continue;
            }

            if (!stagingExists)
            {
                await journal.SetStateAsync(operation.OperationId, "RolledBack",
                    "پس از راه‌اندازی مجدد، مسیر staging و مقصد نهایی وجود نداشتند.", cancellationToken);
                alreadyMissing++;
                continue;
            }

            if (!IsSafeStagingPath(operation))
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "مسیر staging با الگوی امن عملیات مطابقت ندارد؛ حذف خودکار ممنوع شد.", cancellationToken);
                requiresAttention++;
                continue;
            }

            if (Directory.Exists(operation.StagingPath))
                Directory.Delete(operation.StagingPath, recursive: true);
            else
                File.Delete(operation.StagingPath);

            await journal.SetStateAsync(operation.OperationId, "RolledBack",
                "داده‌های staging ناقص پس از بررسی مسیر امن پاک‌سازی شدند؛ پروژهٔ اصلی دست‌نخورده ماند.", cancellationToken);
            rolledBack++;
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
}
