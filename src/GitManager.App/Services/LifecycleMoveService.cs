using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class LifecycleMoveService(
    string workspaceRoot,
    ProjectCatalog catalog,
    ProjectOperationJournal journal,
    ProjectOperationLock? operationLock = null)
{
    private readonly string _workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly ProjectOperationLock _operationLock = operationLock ?? new ProjectOperationLock();
    private readonly WorkspaceService _workspace = new();

    public async Task ChangeAsync(
        ProjectRecord project,
        string targetLifecycle,
        string gitStatusSummary,
        string availabilityAtStart,
        CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(project, targetLifecycle);
        await using var categoryLease = await _operationLock.AcquireAsync(
            "category:" + project.CategoryId, cancellationToken);
        await using var projectLease = await _operationLock.AcquireAsync(
            "project:" + project.ProjectId, cancellationToken);
        var operationId = await RelocateCoreAsync(
            project, paths.Target, gitStatusSummary, availabilityAtStart,
            paths.Source, paths.Destination, cancellationToken);
        await CompleteJournalAndEventAsync(
            operationId, project, paths.Target, gitStatusSummary, availabilityAtStart, cancellationToken);
    }

    public async Task<string> RelocateUntilCatalogCommittedAsync(
        ProjectRecord project,
        string targetLifecycle,
        string gitStatusSummary,
        string availabilityAtStart,
        CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(project, targetLifecycle);
        await using var categoryLease = await _operationLock.AcquireAsync(
            "category:" + project.CategoryId, cancellationToken);
        await using var projectLease = await _operationLock.AcquireAsync(
            "project:" + project.ProjectId, cancellationToken);
        return await RelocateCoreAsync(
            project, paths.Target, gitStatusSummary, availabilityAtStart,
            paths.Source, paths.Destination, cancellationToken);
    }

    public async Task CompleteJournalAndEventAsync(
        string operationId,
        ProjectRecord project,
        string targetLifecycle,
        string gitStatusSummary,
        string availabilityAtStart,
        CancellationToken cancellationToken = default)
    {
        await journal.SetStateAsync(operationId, "Completed", cancellationToken: cancellationToken);
        await catalog.MarkLifecycleOperationCompletedAsync(
            project.ProjectId,
            NewEvent(project, targetLifecycle, operationId, LifecycleState.Completed,
                gitStatusSummary, availabilityAtStart, "انتقال چرخه عمر کامل شد."));
        TryPruneEmptyParents(Path.GetFullPath(project.ContainerPath));
    }

    internal static string BuildOperationKey(string from, string to, string categoryId) =>
        $"lifecycle-move|{from}|{to}|{categoryId}";

    internal static LifecycleEventRecord NewEvent(
        ProjectRecord project, string to, string? operationId, string result,
        string gitStatus, string availability, string? message) =>
        new(0, Guid.NewGuid().ToString("N"), project.ProjectId, project.Lifecycle, to, operationId,
            result, gitStatus, availability, message, DateTime.UtcNow);

    private readonly record struct MovePaths(string Target, string Source, string Destination);

    private MovePaths ResolvePaths(ProjectRecord project, string targetLifecycle)
    {
        var target = LifecycleState.Parse(targetLifecycle);
        var source = Path.GetFullPath(project.ContainerPath);
        var destination = Path.GetFullPath(_workspace.GetProjectContainer(
            _workspaceRoot, target, project.Category, project.Owner, project.Repository));
        var expectedSource = Path.GetFullPath(_workspace.GetProjectContainer(
            _workspaceRoot, project.Lifecycle, project.Category, project.Owner, project.Repository));

        if (!PathSafety.IsWithin(_workspaceRoot, source) || !PathSafety.IsWithin(_workspaceRoot, destination) ||
            !source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFullPath(project.RepositoryPath).Equals(
                Path.Combine(source, "repository"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("مسیر ثبت‌شده پروژه با ساختار کتابخانه سازگار نیست؛ انتقال وضعیت متوقف شد.");
        if (!Directory.Exists(source) || !Directory.Exists(project.RepositoryPath))
            throw new DirectoryNotFoundException("پوشه پروژه یا مخزن Git در مسیر ثبت‌شده پیدا نشد.");
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("پوشه مقصد از قبل وجود دارد؛ هیچ فایلی بازنویسی نشد.");
        if (HasReparsePointInAncestors(Path.GetDirectoryName(destination)!) ||
            ProjectCategoryMoveService.ContainsReparsePoint(source) ||
            ProjectCategoryMoveService.ContainsReparsePoint(destination))
            throw new IOException("مسیر شامل Reparse Point است؛ انتقال وضعیت متوقف شد.");
        return new MovePaths(target, source, destination);
    }

    private async Task<string> RelocateCoreAsync(
        ProjectRecord project,
        string target,
        string gitStatusSummary,
        string availabilityAtStart,
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        var operationKey = BuildOperationKey(project.Lifecycle, target, project.CategoryId);
        var operation = await journal.BeginAsync(
            "LifecycleMove", operationKey, project.ProjectId, source, destination, cancellationToken);
        var moved = false;
        var catalogUpdated = false;
        try
        {
            await journal.SetStateAsync(operation.OperationId, "Running", cancellationToken: cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(source, destination);
            moved = true;
            var repositoryPath = Path.Combine(destination, "repository");
            await catalog.CommitLifecycleMoveAsync(
                project.ProjectId, target, destination, repositoryPath,
                NewEvent(project, target, operation.OperationId, LifecycleState.CatalogCommitted,
                    gitStatusSummary, availabilityAtStart, "کاتالوگ و مسیر پس از انتقال فیزیکی به‌روز شد."));
            catalogUpdated = true;
            try
            {
                await journal.SetStateAsync(operation.OperationId, "CommitPending", cancellationToken: cancellationToken);
            }
            catch
            {
                // Catalog already committed. Recovery treats Running/CommitPending + dest as success.
            }
            return operation.OperationId;
        }
        catch (Exception ex)
        {
            if (catalogUpdated)
                throw;
            try
            {
                if (moved && Directory.Exists(destination) && !Directory.Exists(source))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(source)!);
                    Directory.Move(destination, source);
                }
                await journal.SetStateAsync(operation.OperationId, "RolledBack", ex.Message, CancellationToken.None);
                await catalog.InsertLifecycleEventAsync(NewEvent(
                    project, target, operation.OperationId, LifecycleState.RolledBack,
                    gitStatusSummary, availabilityAtStart, ex.Message));
            }
            catch (Exception rollbackError)
            {
                await journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    $"{ex.Message} | بازگردانی انتقال ناموفق بود: {rollbackError.Message}", CancellationToken.None);
                await catalog.InsertLifecycleEventAsync(NewEvent(
                    project, target, operation.OperationId, LifecycleState.RecoveryRequired,
                    gitStatusSummary, availabilityAtStart,
                    $"{ex.Message} | {rollbackError.Message}"));
            }
            throw;
        }
    }

    private void TryPruneEmptyParents(string sourceContainer)
    {
        try
        {
            var current = Path.GetDirectoryName(Path.GetFullPath(sourceContainer));
            var stop = Path.GetFullPath(_workspaceRoot);
            while (!string.IsNullOrEmpty(current) &&
                   PathSafety.IsWithin(stop, current) &&
                   !current.Equals(stop, StringComparison.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(current);
                if (name is "01_Active" or "02_On-Hold" or "03_Archived" or "04_Quarantine" or "05_Exports" or "00_Manager")
                    break;
                if (!Directory.Exists(current) || Directory.EnumerateFileSystemEntries(current).Any())
                    break;
                Directory.Delete(current);
                current = Path.GetDirectoryName(current);
            }
        }
        catch
        {
            // Best-effort hygiene only.
        }
    }

    private static bool HasReparsePointInAncestors(string path)
    {
        try
        {
            var current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((Directory.Exists(current) || File.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return true;
                current = Path.GetDirectoryName(current);
            }
            return false;
        }
        catch { return true; }
    }
}
