using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed record RepositoryPushResult(string Branch, string Commit, bool HasUncommittedChanges);

/// <summary>
/// Pushes the current branch to the already-configured GitHub origin.
/// Never creates remotes, stores credentials, or force-pushes.
/// </summary>
public sealed class RepositoryPublishService
{
    private readonly GitProcess _git;
    private readonly ProjectOperationJournal _journal;
    private readonly ProjectOperationLock _operationLock;

    public RepositoryPublishService(
        GitProcess git,
        ProjectOperationJournal journal,
        ProjectOperationLock operationLock)
    {
        _git = git;
        _journal = journal;
        _operationLock = operationLock;
    }

    public async Task<RepositoryPushResult> PushAsync(
        ProjectRecord project,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(project.RepositoryPath) ||
            !Directory.Exists(project.RepositoryPath))
            throw new InvalidOperationException("مسیر مخزن محلی پیدا نشد؛ ابتدا دسترسی پروژه را بررسی کنید.");

        var gitDirectory = Path.Combine(project.RepositoryPath, ".git");
        if (!Directory.Exists(gitDirectory) && !File.Exists(gitDirectory))
            throw new InvalidOperationException("پوشه انتخاب‌شده یک مخزن Git معتبر نیست.");

        await _journal.EnsureAvailableAsync(cancellationToken);
        var operationKey = "repository-git:" + project.ProjectId;
        await using var lease = await _operationLock.AcquireAsync(operationKey, cancellationToken);

        if (File.Exists(Path.Combine(gitDirectory, "index.lock")))
            throw new InvalidOperationException("قفل Git فعال است؛ پس از بسته‌شدن ابزارهای Git دوباره تلاش کنید.");

        var remoteUrl = await _git.RunAsync(project.RepositoryPath, cancellationToken, "remote", "get-url", "origin");
        var remote = GitHubRemote.Parse(remoteUrl);
        if (!remote.CanonicalUrl.Equals(project.CanonicalRemoteUrl, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("نشانی origin با مخزن ثبت‌شده در کتابخانه یکسان نیست؛ برای جلوگیری از ارسال به مقصد اشتباه، Push متوقف شد.");

        string credentialHelper;
        try
        {
            credentialHelper = await _git.RunAsync(project.RepositoryPath, cancellationToken,
                "config", "--get", "credential.helper");
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("Git Credential Manager پیکربندی نشده است. Git for Windows را با مؤلفه Git Credential Manager نصب یا Repair کنید.");
        }

        if (!credentialHelper.Contains("manager", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("برای امنیت، Push فقط با Git Credential Manager فعال می‌شود. credential.helper فعلی را به manager تنظیم کنید.");

        var branch = await _git.RunAsync(project.RepositoryPath, cancellationToken,
            "symbolic-ref", "--quiet", "--short", "HEAD");
        if (string.IsNullOrWhiteSpace(branch))
            throw new InvalidOperationException("مخزن در حالت Detached HEAD است؛ ابتدا یک شاخه محلی انتخاب کنید.");

        var porcelain = await _git.RunAsync(project.RepositoryPath, cancellationToken, "status", "--porcelain=v1");
        var hasUncommittedChanges = !string.IsNullOrWhiteSpace(porcelain);
        var commit = await _git.RunAsync(project.RepositoryPath, cancellationToken, "rev-parse", "HEAD");

        var operation = await _journal.BeginAsync("Push", operationKey, project.ProjectId,
            project.RepositoryPath, project.RepositoryPath, cancellationToken);

        try
        {
            await _journal.SetStateAsync(operation.OperationId, "Running", cancellationToken: cancellationToken);
            // Explicit branch refspec; no --force / --force-with-lease is ever used.
            await _git.RunAsync(project.RepositoryPath, cancellationToken,
                "push", "--set-upstream", "origin", $"HEAD:refs/heads/{branch}");
            await _journal.SetStateAsync(operation.OperationId, "Completed",
                "شاخه با Push عادی و بدون Force به origin ارسال شد.", cancellationToken);
            return new RepositoryPushResult(branch, commit, hasUncommittedChanges);
        }
        catch (Exception ex)
        {
            // A network failure can occur after the server accepted the update.
            // Keep a recoverable journal entry and never roll back local files.
            try
            {
                await _journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    "نتیجه Push قطعی نیست؛ وضعیت شاخه remote را بررسی کنید. " + ex.Message,
                    CancellationToken.None);
            }
            catch { }
            throw;
        }
    }
}
