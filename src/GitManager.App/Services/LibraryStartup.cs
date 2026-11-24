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
    string? RecoveryError)
{
    public int AttentionCount =>
        RenameRecovery.RequiresAttention + CategoryMoveRecovery.RequiresAttention
        + LifecycleRecovery.RequiresAttention + OperationRecovery.RequiresAttention;

    public int RolledBackCount =>
        RenameRecovery.RolledBack + CategoryMoveRecovery.RolledBack
        + LifecycleRecovery.RolledBack + OperationRecovery.RolledBack;

    public string StatusMessage(int missingCount)
    {
        if (RecoveryLimited)
        {
            var extra = string.IsNullOrWhiteSpace(RecoveryError) ? "" : " " + RecoveryError;
            return "کتابخانه باز است، اما بازیابی عملیات ناقص انجام نشد. عملیات حساس تا رفع مشکل محدود است." + extra;
        }

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
/// Opens a workspace catalog even when journal recovery fails. Clone stays available;
/// journal-backed mutations stay limited until recovery succeeds.
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
        string? recoveryError = null;
        var rename = new CategoryRenameRecoveryReport(0, 0, 0);
        var categoryMove = new CategoryMoveRecoveryReport(0, 0, 0);
        var lifecycleMove = new LifecycleMoveRecoveryReport(0, 0, 0);
        var operations = new RecoveryReport(0, 0, 0);

        void MarkLimited(Exception ex)
        {
            recoveryLimited = true;
            recoveryError = string.IsNullOrWhiteSpace(recoveryError)
                ? ex.Message
                : recoveryError + " | " + ex.Message;
        }

        async Task<T> TryRecoverAsync<T>(Func<Task<T>> action, T fallback)
        {
            try { return await action(); }
            catch (Exception ex)
            {
                MarkLimited(ex);
                return fallback;
            }
        }

        try
        {
            await journal.InitializeAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            MarkLimited(ex);
        }

        rename = await TryRecoverAsync(
            () => new CategoryRenameRecoveryService(workspacePath, catalog, journal, operationLock)
                .RecoverAsync(cancellationToken),
            rename);
        categoryMove = await TryRecoverAsync(
            () => new CategoryMoveRecoveryService(workspacePath, catalog, journal, operationLock)
                .RecoverAsync(cancellationToken),
            categoryMove);
        lifecycleMove = await TryRecoverAsync(
            () => new LifecycleMoveRecoveryService(workspacePath, catalog, journal, operationLock)
                .RecoverAsync(cancellationToken),
            lifecycleMove);
        operations = await TryRecoverAsync(
            () => new ProjectOperationRecoveryService(journal, operationLock).RecoverAsync(cancellationToken),
            operations);
        try
        {
            await new ProjectAvailabilityService(workspacePath, catalog).ProbeAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            MarkLimited(ex);
        }

        return new LibraryStartupResult(
            catalog, categoryManagement, mover, lifecycle, importer,
            rename, categoryMove, lifecycleMove, operations,
            recoveryLimited, recoveryError);
    }
}
