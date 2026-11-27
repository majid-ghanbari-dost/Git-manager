namespace GitManager.App.Services;

public sealed record LibraryStartupResult(
    ProjectCatalog Catalog,
    CategoryManagementService CategoryManagement,
    ProjectCategoryMoveService ProjectCategoryMover,
    ProjectLifecycleService Lifecycle,
    RepositoryImportService Importer,
    CategoryRenameRecoveryReport RenameRecovery,
    CategoryMoveRecoveryReport CategoryMoveRecovery,
    LifecycleMoveRecoveryReport LifecycleRecovery,
    RecoveryReport OperationRecovery,
    bool RecoveryLimited,
    bool JournalUsable)
{
    public int AttentionCount =>
        RenameRecovery.RequiresAttention + CategoryMoveRecovery.RequiresAttention
        + LifecycleRecovery.RequiresAttention + OperationRecovery.RequiresAttention;

    public int RolledBackCount =>
        RenameRecovery.RolledBack + CategoryMoveRecovery.RolledBack
        + LifecycleRecovery.RolledBack + OperationRecovery.RolledBack;

    public string StatusMessage(int missingCount)
    {
        if (!JournalUsable)
            return "کتابخانه باز است، اما دفتر ثبت عملیات قابل استفاده نیست. عملیات حساس و دریافت مخزن تا رفع مشکل محدود است.";
        if (RecoveryLimited)
            return "کتابخانه باز است، اما بازیابی عملیات ناقص انجام نشد. عملیات حساس تا رفع مشکل محدود است.";

        return AttentionCount > 0
            ? $"کتابخانه آماده است؛ {AttentionCount} عملیات برای بررسی دستی علامت‌گذاری شد" +
              (missingCount > 0 ? $" و {missingCount} پروژه مفقود یا ناسازگار است." : ".")
            : RolledBackCount > 0
                ? $"کتابخانه آماده است؛ {RolledBackCount} عملیات ناقص به‌صورت امن بازگردانی شد."
                : missingCount > 0
                    ? $"کتابخانه آماده است؛ {missingCount} پروژه مفقود یا ناسازگار است."
                    : "کتابخانه آماده است. مخزن GitHub را برای افزودن وارد کنید.";
    }
}

/// <summary>
/// Opens a workspace catalog even when journal recovery fails. Clone is allowed only when
/// the journal can record an operation; otherwise the library still opens and Clone stays blocked.
/// </summary>
public static class LibraryStartup
{
    public static async Task<LibraryStartupResult> OpenAsync(
        string workspacePath,
        ProjectOperationJournal journal,
        GitProcess? git = null,
        ProjectOperationLock? operationLock = null,
        CancellationToken cancellationToken = default)
    {
        git ??= new GitProcess();
        var logDirectory = DiagnosticLog.DirectoryFor(workspacePath);
        var workspace = new WorkspaceService();
        workspace.Initialize(workspacePath);
        var catalog = new ProjectCatalog(workspacePath);
        await catalog.InitializeAsync();

        var categoryManagement = new CategoryManagementService(catalog,
            new CategoryRenameService(workspacePath, catalog, journal, operationLock));
        var mover = new ProjectCategoryMoveService(workspacePath, catalog, journal, operationLock);
        var lifecycle = new ProjectLifecycleService(workspacePath, catalog, journal, git, operationLock);
        var importer = new RepositoryImportService(workspace, catalog, git, journal, operationLock);

        var recoveryLimited = false;
        var journalUsable = false;
        var rename = new CategoryRenameRecoveryReport(0, 0, 0);
        var categoryMove = new CategoryMoveRecoveryReport(0, 0, 0);
        var lifecycleMove = new LifecycleMoveRecoveryReport(0, 0, 0);
        var operations = new RecoveryReport(0, 0, 0);

        void MarkLimited(string category, string message, Exception ex)
        {
            recoveryLimited = true;
            DiagnosticLog.Write(logDirectory, category, message, ex);
        }

        async Task<T> TryRecoverAsync<T>(string step, Func<Task<T>> action, T fallback)
        {
            try { return await action(); }
            catch (Exception ex)
            {
                MarkLimited("recovery", step + " failed", ex);
                return fallback;
            }
        }

        try
        {
            await journal.EnsureAvailableAsync(cancellationToken);
            journalUsable = true;
        }
        catch (Exception ex)
        {
            journalUsable = false;
            MarkLimited("journal", "operation journal is not usable", ex);
        }

        if (journalUsable)
        {
            rename = await TryRecoverAsync(
                "category-rename recovery",
                () => new CategoryRenameRecoveryService(workspacePath, catalog, journal, operationLock)
                    .RecoverAsync(cancellationToken),
                rename);
            categoryMove = await TryRecoverAsync(
                "category-move recovery",
                () => new CategoryMoveRecoveryService(workspacePath, catalog, journal, operationLock)
                    .RecoverAsync(cancellationToken),
                categoryMove);
            lifecycleMove = await TryRecoverAsync(
                "lifecycle recovery",
                () => new LifecycleMoveRecoveryService(workspacePath, catalog, journal, operationLock)
                    .RecoverAsync(cancellationToken),
                lifecycleMove);
            operations = await TryRecoverAsync(
                "operation recovery",
                () => new ProjectOperationRecoveryService(journal, operationLock).RecoverAsync(cancellationToken),
                operations);
        }

        try
        {
            await new ProjectAvailabilityService(workspacePath, catalog).ProbeAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            MarkLimited("availability", "project availability probe failed", ex);
        }

        return new LibraryStartupResult(
            catalog, categoryManagement, mover, lifecycle, importer,
            rename, categoryMove, lifecycleMove, operations,
            recoveryLimited, journalUsable);
    }
}
