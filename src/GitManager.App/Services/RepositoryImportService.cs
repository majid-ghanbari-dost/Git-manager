using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class RepositoryImportService
{
    private readonly WorkspaceService _workspaceService;
    private readonly ProjectCatalog _catalog;
    private readonly GitProcess _git;
    private readonly ProjectOperationJournal _journal;
    private readonly ProjectOperationLock _operationLock;

    public RepositoryImportService(
        WorkspaceService workspaceService,
        ProjectCatalog catalog,
        GitProcess git,
        ProjectOperationJournal? journal = null,
        ProjectOperationLock? operationLock = null)
    {
        _workspaceService = workspaceService;
        _catalog = catalog;
        _git = git;
        _journal = journal ?? new ProjectOperationJournal();
        _operationLock = operationLock ?? new ProjectOperationLock();
    }

    public async Task<ProjectRecord> CloneAsync(
        string workspaceRoot,
        string url,
        string category,
        CancellationToken cancellationToken = default)
    {
        var remote = GitHubRemote.Parse(url);
        await _journal.EnsureAvailableAsync(cancellationToken);
        var lockKey = "import:" + remote.CanonicalUrl;
        await using var lease = await _operationLock.AcquireAsync(lockKey, cancellationToken);
        var existing = (await _catalog.GetProjectsAsync()).FirstOrDefault(p =>
            p.CanonicalRemoteUrl.Equals(remote.CanonicalUrl, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new InvalidOperationException(
                $"این مخزن قبلاً در کتابخانه ثبت شده است (وضعیت: {existing.Lifecycle}، مسیر: {existing.ContainerPath}). دوباره Clone نشود.");

        var destination = _workspaceService.GetProjectContainer(workspaceRoot, "Active", category,
            remote.Owner, remote.Repository);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidOperationException("پوشه مقصد از قبل وجود دارد؛ برای جلوگیری از بازنویسی، عملیات متوقف شد.");

        var tempRoot = Path.Combine(workspaceRoot, "00_Manager", "temp", Guid.NewGuid().ToString("N"));
        var stagedRepository = Path.Combine(tempRoot, "repository");
        var record = new ProjectRecord
        {
            CanonicalRemoteUrl = remote.CanonicalUrl,
            Owner = remote.Owner,
            Repository = remote.Repository,
            Category = category,
            Lifecycle = "Active",
            ContainerPath = destination,
            RepositoryPath = Path.Combine(destination, "repository")
        };
        var operation = await _journal.BeginAsync("Import", lockKey, record.ProjectId, tempRoot, destination, cancellationToken);

        var catalogCommitted = false;
        try
        {
            Directory.CreateDirectory(tempRoot);
            await _journal.SetStateAsync(operation.OperationId, "Running", cancellationToken: cancellationToken);
            await _git.RunAsync(tempRoot, cancellationToken, "clone", "--", remote.CanonicalUrl, stagedRepository);
            var actualRemote = await _git.RunAsync(stagedRepository, cancellationToken, "remote", "get-url", "origin");
            if (!GitHubRemote.Parse(actualRemote).CanonicalUrl.Equals(remote.CanonicalUrl, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("نشانی remote پس از Clone با مخزن انتخاب‌شده مطابقت ندارد.");

            record.CurrentBranch = await _git.RunAsync(stagedRepository, cancellationToken, "branch", "--show-current");
            record.CurrentCommit = await _git.RunAsync(stagedRepository, cancellationToken, "rev-parse", "HEAD");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.CreateDirectory(Path.Combine(tempRoot, "backups"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "notes"));
            await _journal.SetStateAsync(operation.OperationId, "CommitPending", cancellationToken: cancellationToken);
            Directory.Move(tempRoot, destination);
            try
            {
                await _catalog.AddProjectAsync(record);
                catalogCommitted = true;
            }
            catch
            {
                if (Directory.Exists(destination)) Directory.Move(destination, tempRoot);
                throw;
            }

            await _journal.SetStateAsync(operation.OperationId, "Completed", cancellationToken: cancellationToken);
            return record;
        }
        catch (Exception ex)
        {
            var logDirectory = DiagnosticLog.DirectoryFor(workspaceRoot);
            DiagnosticLog.Write(logDirectory, "clone", "clone operation failed", ex);

            var destinationPresent = Directory.Exists(destination) || File.Exists(destination);
            var preserveTrees = catalogCommitted || destinationPresent || ex is JournalUnavailableException;

            try
            {
                if (preserveTrees)
                {
                    if (!await TryRecordStateAsync(operation.OperationId, "RecoveryRequired",
                            "عملیات ناقص ماند؛ برای جلوگیری از حذف داده، مسیرها حفظ شدند.", logDirectory))
                    {
                        DiagnosticLog.Write(logDirectory, "clone",
                            "could not record RecoveryRequired; staging and destination were left in place");
                    }
                }
                else if (await TryRecordStateAsync(operation.OperationId, "RolledBack",
                             "عملیات Clone ناتمام بازگردانی شد.", logDirectory))
                {
                    TryDeleteIncompleteStaging(tempRoot, destination, logDirectory);
                }
                else
                {
                    DiagnosticLog.Write(logDirectory, "clone",
                        "could not record rollback; staging was not deleted");
                }
            }
            catch (Exception secondary)
            {
                DiagnosticLog.Write(logDirectory, "clone",
                    "secondary clone failure handling error; original error is preserved", secondary);
            }

            throw;
        }
    }

    private async Task<bool> TryRecordStateAsync(
        string operationId, string state, string message, string logDirectory)
    {
        try
        {
            await _journal.SetStateAsync(operationId, state, message, CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write(logDirectory, "journal",
                $"failed to record operation {operationId} as {state}", ex);
            return false;
        }
    }

    private static void TryDeleteIncompleteStaging(string tempRoot, string destination, string logDirectory)
    {
        try
        {
            if (Directory.Exists(destination) && !Directory.Exists(tempRoot))
                Directory.Move(destination, tempRoot);
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write(logDirectory, "clone",
                "staging cleanup failed; trees left in place", ex);
        }
    }
}
