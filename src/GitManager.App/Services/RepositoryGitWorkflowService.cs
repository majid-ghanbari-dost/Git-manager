using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed record RepositoryGitResult(string Branch, string Commit, bool HasUncommittedChanges);

/// <summary>
/// Safe branch operations for repositories already registered in the workspace.
/// Pull is fast-forward-only; commits stage all current worktree changes after explicit UI confirmation.
/// </summary>
public sealed class RepositoryGitWorkflowService
{
    private readonly GitProcess _git;
    private readonly ProjectOperationJournal _journal;
    private readonly ProjectOperationLock _operationLock;

    public RepositoryGitWorkflowService(
        GitProcess git,
        ProjectOperationJournal journal,
        ProjectOperationLock operationLock)
    {
        _git = git;
        _journal = journal;
        _operationLock = operationLock;
    }

    public async Task FetchAsync(ProjectRecord project, CancellationToken cancellationToken = default)
    {
        await RunJournaledAsync(project, "Fetch", async () =>
        {
            var remote = await ValidateRepositoryAsync(project, requireCredentialManager: true, cancellationToken);
            await _git.RunAsync(project.RepositoryPath, cancellationToken, "fetch", "--prune", "origin");
            return remote.CanonicalUrl;
        }, cancellationToken);
    }

    public async Task<RepositoryGitResult> PullAsync(ProjectRecord project, CancellationToken cancellationToken = default)
    {
        return await RunJournaledAsync(project, "Pull", async () =>
        {
            await ValidateRepositoryAsync(project, requireCredentialManager: true, cancellationToken);
            var branch = await GetCurrentBranchAsync(project, cancellationToken);
            var status = await _git.RunAsync(project.RepositoryPath, cancellationToken, "status", "--porcelain=v1");
            if (!string.IsNullOrWhiteSpace(status))
                throw new InvalidOperationException("برای Pull ابتدا تغییرات محلی را Commit کنید یا از آن‌ها پشتیبان بگیرید؛ Pull روی مخزن دارای تغییرات محلی مسدود شد.");
            await _git.RunAsync(project.RepositoryPath, cancellationToken,
                "pull", "--ff-only", "origin", branch);
            var commit = await _git.RunAsync(project.RepositoryPath, cancellationToken, "rev-parse", "HEAD");
            return new RepositoryGitResult(branch, commit, false);
        }, cancellationToken);
    }

    public async Task<RepositoryGitResult> CommitAllAsync(
        ProjectRecord project,
        string message,
        CancellationToken cancellationToken = default)
    {
        var normalizedMessage = (message ?? "").Trim();
        if (normalizedMessage.Length is < 3 or > 120 || normalizedMessage.Any(char.IsControl))
            throw new InvalidOperationException("پیام Commit باید بین ۳ تا ۱۲۰ نویسه و بدون نویسه کنترلی باشد.");

        return await RunJournaledAsync(project, "Commit", async () =>
        {
            EnsureRepository(project);
            var branch = await GetCurrentBranchAsync(project, cancellationToken);
            var before = await _git.RunAsync(project.RepositoryPath, cancellationToken, "status", "--porcelain=v1");
            if (string.IsNullOrWhiteSpace(before))
                throw new InvalidOperationException("تغییری برای Commit وجود ندارد.");
            await _git.RunAsync(project.RepositoryPath, cancellationToken, "add", "--all");
            var staged = await _git.RunAsync(project.RepositoryPath, cancellationToken, "status", "--porcelain=v1");
            if (string.IsNullOrWhiteSpace(staged))
                throw new InvalidOperationException("پس از Stage کردن، تغییری برای Commit باقی نماند.");
            await _git.RunAsync(project.RepositoryPath, cancellationToken, "commit", "-m", normalizedMessage);
            var commit = await _git.RunAsync(project.RepositoryPath, cancellationToken, "rev-parse", "HEAD");
            return new RepositoryGitResult(branch, commit, false);
        }, cancellationToken);
    }

    private async Task<GitHubRemote> ValidateRepositoryAsync(
        ProjectRecord project,
        bool requireCredentialManager,
        CancellationToken cancellationToken)
    {
        EnsureRepository(project);
        var remoteUrl = await _git.RunAsync(project.RepositoryPath, cancellationToken, "remote", "get-url", "origin");
        var remote = GitHubRemote.Parse(remoteUrl);
        if (!remote.CanonicalUrl.Equals(project.CanonicalRemoteUrl, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("نشانی origin با مخزن ثبت‌شده در کتابخانه یکسان نیست؛ عملیات متوقف شد.");

        if (requireCredentialManager)
        {
            string helper;
            try
            {
                helper = await _git.RunAsync(project.RepositoryPath, cancellationToken,
                    "config", "--get", "credential.helper");
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException("Git Credential Manager پیکربندی نشده است. Git for Windows را با مؤلفه Git Credential Manager نصب یا Repair کنید.");
            }
            if (!helper.Contains("manager", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("برای امنیت، عملیات شبکه فقط با Git Credential Manager فعال می‌شود.");
        }
        return remote;
    }

    private static void EnsureRepository(ProjectRecord project)
    {
        if (string.IsNullOrWhiteSpace(project.RepositoryPath) || !Directory.Exists(project.RepositoryPath))
            throw new InvalidOperationException("مسیر مخزن محلی پیدا نشد؛ ابتدا دسترسی پروژه را بررسی کنید.");
        var gitDirectory = Path.Combine(project.RepositoryPath, ".git");
        if (!Directory.Exists(gitDirectory) && !File.Exists(gitDirectory))
            throw new InvalidOperationException("پوشه انتخاب‌شده یک مخزن Git معتبر نیست.");
        if (File.Exists(Path.Combine(gitDirectory, "index.lock")))
            throw new InvalidOperationException("قفل Git فعال است؛ پس از بسته‌شدن ابزارهای Git دوباره تلاش کنید.");
    }

    private async Task<string> GetCurrentBranchAsync(ProjectRecord project, CancellationToken cancellationToken)
    {
        var branch = await _git.RunAsync(project.RepositoryPath, cancellationToken,
            "symbolic-ref", "--quiet", "--short", "HEAD");
        if (string.IsNullOrWhiteSpace(branch))
            throw new InvalidOperationException("مخزن در حالت Detached HEAD است؛ ابتدا یک شاخه محلی انتخاب کنید.");
        return branch;
    }

    private async Task<T> RunJournaledAsync<T>(
        ProjectRecord project,
        string operationType,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        await _journal.EnsureAvailableAsync(cancellationToken);
        var operationKey = "repository-git:" + project.ProjectId;
        await using var lease = await _operationLock.AcquireAsync(operationKey, cancellationToken);
        var operation = await _journal.BeginAsync(operationType, operationKey, project.ProjectId,
            project.RepositoryPath, project.RepositoryPath, cancellationToken);
        try
        {
            await _journal.SetStateAsync(operation.OperationId, "Running", cancellationToken: cancellationToken);
            var result = await action();
            await _journal.SetStateAsync(operation.OperationId, "Completed",
                operationType + " completed.", cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            try
            {
                await _journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "وضعیت عملیات Git پس از توقف قطعی نیست؛ وضعیت مخزن را بررسی کنید. " + ex.Message,
                    CancellationToken.None);
            }
            catch { }
            throw;
        }
    }
}
