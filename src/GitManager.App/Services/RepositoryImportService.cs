using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class RepositoryImportService(
    WorkspaceService workspaceService,
    ProjectCatalog catalog,
    GitProcess git)
{
    public async Task<ProjectRecord> CloneAsync(string workspaceRoot, string url, string category)
    {
        var remote = GitHubRemote.Parse(url);
        var existing = (await catalog.GetProjectsAsync()).FirstOrDefault(p =>
            p.CanonicalRemoteUrl.Equals(remote.CanonicalUrl, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new InvalidOperationException("این مخزن قبلاً در کتابخانه ثبت شده است.");

        var destination = workspaceService.GetProjectContainer(workspaceRoot, "Active", category,
            remote.Owner, remote.Repository);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidOperationException("پوشه مقصد از قبل وجود دارد؛ برای جلوگیری از بازنویسی، عملیات متوقف شد.");

        var tempRoot = Path.Combine(workspaceRoot, "00_Manager", "temp", Guid.NewGuid().ToString("N"));
        var stagedRepository = Path.Combine(tempRoot, "repository");
        Directory.CreateDirectory(tempRoot);
        try
        {
            await git.RunAsync(tempRoot, "clone", "--", remote.CanonicalUrl, stagedRepository);
            var actualRemote = await git.RunAsync(stagedRepository, "remote", "get-url", "origin");
            if (!GitHubRemote.Parse(actualRemote).CanonicalUrl.Equals(remote.CanonicalUrl, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("نشانی remote پس از Clone با مخزن انتخاب‌شده مطابقت ندارد.");

            var branch = await git.RunAsync(stagedRepository, "branch", "--show-current");
            var commit = await git.RunAsync(stagedRepository, "rev-parse", "HEAD");
            var record = new ProjectRecord
            {
                CanonicalRemoteUrl = remote.CanonicalUrl, Owner = remote.Owner, Repository = remote.Repository,
                Category = category, Lifecycle = "Active", ContainerPath = destination,
                RepositoryPath = Path.Combine(destination, "repository"),
                CurrentBranch = branch, CurrentCommit = commit
            };

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(tempRoot, destination);
            try { await catalog.AddProjectAsync(record); }
            catch
            {
                if (Directory.Exists(destination)) Directory.Move(destination, tempRoot);
                throw;
            }
            return record;
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }
}
