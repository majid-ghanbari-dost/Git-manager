using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class ProjectLifecycleService(
    string workspaceRoot,
    ProjectCatalog catalog,
    ProjectOperationJournal journal,
    GitProcess? git = null,
    ProjectOperationLock? operationLock = null)
{
    private readonly ProjectOperationLock _operationLock = operationLock ?? new ProjectOperationLock();
    private readonly LifecycleMoveService _mover = new(workspaceRoot, catalog, journal, operationLock);
    private readonly ProjectAvailabilityService _availability = new(workspaceRoot, catalog);
    private readonly GitProcess _git = git ?? new GitProcess();

    public async Task ChangeLifecycleAsync(
        string projectId,
        string targetLifecycle,
        LifecycleChangeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new LifecycleChangeOptions();
        var target = LifecycleState.Parse(targetLifecycle);
        var project = (await catalog.GetProjectsAsync()).FirstOrDefault(x => x.ProjectId == projectId)
            ?? throw new InvalidOperationException("پروژه انتخاب‌شده در کاتالوگ پیدا نشد.");
        if (LifecycleState.IsNoOp(project.Lifecycle, target))
        {
            await new LifecycleMoveRecoveryService(workspaceRoot, catalog, journal, _operationLock)
                .RecoverAsync(cancellationToken);
            return;
        }
        if (LifecycleState.IsDenied(project.Lifecycle, target))
            throw new InvalidOperationException("این انتقال وضعیت مجاز نیست.");

        var availability = await _availability.ProbeAndStoreAsync(project, cancellationToken);
        if (availability is LifecycleState.Missing)
            throw new InvalidOperationException("پوشه پروژه در مسیر ثبت‌شده پیدا نشد. تغییر وضعیت تا زمان بازیابی پوشه مجاز نیست.");
        if (availability is LifecycleState.PathDrift)
            throw new InvalidOperationException("مسیر ثبت‌شده پروژه با ساختار کتابخانه سازگار نیست؛ انتقال وضعیت متوقف شد.");
        if (LifecycleState.RequiresArchiveConfirm(project.Lifecycle, target) && !options.ConfirmArchive)
            throw new InvalidOperationException("برای بایگانی پروژه تأیید صریح لازم است.");
        if (LifecycleState.RequiresRestoreConfirm(project.Lifecycle, target) && !options.ConfirmRestoreFromArchive)
            throw new InvalidOperationException("برای بازگردانی پروژه از بایگانی تأیید صریح لازم است.");

        var gitStatus = await InspectGitAsync(project.RepositoryPath, cancellationToken);
        if (gitStatus.IndexLock)
            throw new InvalidOperationException("قفل Git (index.lock) فعال است؛ پس از بسته شدن ابزارهای دیگر دوباره تلاش کنید.");
        if (gitStatus.Dirty && !options.ConfirmDirtyWorkingTree)
            throw new InvalidOperationException("مخزن دارای تغییرات Commit‌نشده است؛ برای ادامه باید این موضوع تأیید شود. هیچ فایلی حذف نشد.");

        await _mover.ChangeAsync(project, target, gitStatus.Summary, availability, cancellationToken);
    }

    public async Task<string> RelocateUntilCatalogCommittedAsync(
        string projectId, string targetLifecycle, LifecycleChangeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new LifecycleChangeOptions();
        var target = LifecycleState.Parse(targetLifecycle);
        var project = (await catalog.GetProjectsAsync()).FirstOrDefault(x => x.ProjectId == projectId)
            ?? throw new InvalidOperationException("پروژه انتخاب‌شده در کاتالوگ پیدا نشد.");
        if (LifecycleState.IsNoOp(project.Lifecycle, target)) return "";
        var availability = await _availability.ProbeAndStoreAsync(project, cancellationToken);
        if (availability is LifecycleState.Missing or LifecycleState.PathDrift)
            throw new InvalidOperationException("پروژه برای انتقال وضعیت در دسترس نیست.");
        if (LifecycleState.RequiresArchiveConfirm(project.Lifecycle, target) && !options.ConfirmArchive)
            throw new InvalidOperationException("برای بایگانی پروژه تأیید صریح لازم است.");
        if (LifecycleState.RequiresRestoreConfirm(project.Lifecycle, target) && !options.ConfirmRestoreFromArchive)
            throw new InvalidOperationException("برای بازگردانی پروژه از بایگانی تأیید صریح لازم است.");
        var gitStatus = await InspectGitAsync(project.RepositoryPath, cancellationToken);
        if (gitStatus.IndexLock)
            throw new InvalidOperationException("قفل Git (index.lock) فعال است؛ پس از بسته شدن ابزارهای دیگر دوباره تلاش کنید.");
        if (gitStatus.Dirty && !options.ConfirmDirtyWorkingTree)
            throw new InvalidOperationException("مخزن دارای تغییرات Commit‌نشده است؛ برای ادامه باید این موضوع تأیید شود. هیچ فایلی حذف نشد.");
        return await _mover.RelocateUntilCatalogCommittedAsync(
            project, target, gitStatus.Summary, availability, cancellationToken);
    }

    public Task CompleteJournalAndEventAsync(
        string operationId, ProjectRecord project, string targetLifecycle,
        string gitStatusSummary, string availabilityAtStart, CancellationToken cancellationToken = default) =>
        _mover.CompleteJournalAndEventAsync(
            operationId, project, targetLifecycle, gitStatusSummary, availabilityAtStart, cancellationToken);

    internal async Task<GitInspection> InspectGitAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var gitDir = Path.Combine(repositoryPath, ".git");
        if (!Directory.Exists(gitDir) && !File.Exists(gitDir))
            return new GitInspection(false, false, "unknown");
        if (File.Exists(Path.Combine(gitDir, "index.lock")))
            return new GitInspection(true, true, "index.lock");
        try
        {
            var porcelain = await _git.RunAsync(repositoryPath, cancellationToken, "status", "--porcelain=v1");
            var dirty = !string.IsNullOrWhiteSpace(porcelain);
            var count = porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            return new GitInspection(dirty, false, dirty ? $"dirty: {count} paths" : "clean");
        }
        catch
        {
            return new GitInspection(true, false, "unknown");
        }
    }

    internal readonly record struct GitInspection(bool Dirty, bool IndexLock, string Summary);
}
