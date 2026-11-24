using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class ProjectBackupService
{
    private readonly GitProcess _git;
    private readonly ProjectOperationJournal _journal;
    private readonly ProjectOperationLock _operationLock;

    public ProjectBackupService(
        GitProcess git,
        ProjectOperationJournal? journal = null,
        ProjectOperationLock? operationLock = null)
    {
        _git = git;
        _journal = journal ?? new ProjectOperationJournal();
        _operationLock = operationLock ?? new ProjectOperationLock();
    }

    public async Task<string> CreateBackupAsync(ProjectRecord project, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(project.RepositoryPath))
            throw new DirectoryNotFoundException("پوشه مخزن پروژه پیدا نشد.");

        await using var lease = await _operationLock.AcquireAsync("project:" + project.ProjectId, cancellationToken);
        var backupRoot = Path.Combine(project.ContainerPath, "backups");
        Directory.CreateDirectory(backupRoot);
        var backupId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
        var staging = Path.Combine(backupRoot, ".staging-" + Guid.NewGuid().ToString("N"));
        var final = Path.Combine(backupRoot, backupId);
        var operation = await _journal.BeginAsync("Backup", "project:" + project.ProjectId, project.ProjectId, staging, final, cancellationToken);

        try
        {
            Directory.CreateDirectory(staging);
            await _journal.SetStateAsync(operation.OperationId, "Running", cancellationToken: cancellationToken);

            var bundlePath = Path.Combine(staging, "repository.bundle");
            await _git.RunAsync(project.RepositoryPath, cancellationToken, "bundle", "create", bundlePath, "--all");
            await _git.RunAsync(project.RepositoryPath, cancellationToken, "bundle", "verify", bundlePath);

            var snapshotPath = Path.Combine(staging, "working-tree.zip");
            await CreateWorkingTreeSnapshotAsync(project.RepositoryPath, snapshotPath, cancellationToken);

            var deletedOutput = await _git.RunAsync(project.RepositoryPath, cancellationToken, "ls-files", "--deleted", "-z");
            var deletedFiles = deletedOutput.Split((char)0, StringSplitOptions.RemoveEmptyEntries).ToList();
            var manifest = new BackupManifest
            {
                ProjectId = project.ProjectId,
                DeletedTrackedFiles = deletedFiles,
                CanonicalRemoteUrl = project.CanonicalRemoteUrl,
                CreatedAtUtc = DateTime.UtcNow,
                GitBundleSha256 = await HashFileAsync(bundlePath, cancellationToken),
                WorkingTreeSha256 = await HashFileAsync(snapshotPath, cancellationToken),
                IncludesGitHistory = true,
                IncludesTrackedAndUntrackedFiles = true,
                ExcludesGitInternals = true
            };
            await File.WriteAllTextAsync(Path.Combine(staging, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

            await _journal.SetStateAsync(operation.OperationId, "CommitPending", cancellationToken: cancellationToken);
            Directory.Move(staging, final);
            await _journal.SetStateAsync(operation.OperationId, "Completed", cancellationToken: cancellationToken);
            return final;
        }
        catch (Exception ex)
        {
            try
            {
                if (Directory.Exists(final))
                {
                    await _journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                        "نسخه نهایی ساخته شده اما ثبت وضعیت عملیات کامل نشد؛ فایل نهایی حفظ شد.", CancellationToken.None);
                }
                else
                {
                    if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
                    await _journal.SetStateAsync(operation.OperationId, "RolledBack", ex.Message, CancellationToken.None);
                }
            }
            catch (Exception cleanupError)
            {
                await _journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    $"{ex.Message} | پاک‌سازی staging ناموفق بود: {cleanupError.Message}", CancellationToken.None);
            }
            throw;
        }
    }

    public async Task<bool> VerifyBackupAsync(string backupDirectory, CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(backupDirectory, "manifest.json");
        if (!File.Exists(manifestPath)) return false;
        var manifest = JsonSerializer.Deserialize<BackupManifest>(
            await File.ReadAllTextAsync(manifestPath, cancellationToken));
        if (manifest is null) return false;

        var bundle = Path.Combine(backupDirectory, "repository.bundle");
        var snapshot = Path.Combine(backupDirectory, "working-tree.zip");
        if (!File.Exists(bundle) || !File.Exists(snapshot)) return false;
        return string.Equals(await HashFileAsync(bundle, cancellationToken), manifest.GitBundleSha256, StringComparison.OrdinalIgnoreCase)
            && string.Equals(await HashFileAsync(snapshot, cancellationToken), manifest.WorkingTreeSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CreateWorkingTreeSnapshotAsync(string repositoryPath, string destination, CancellationToken cancellationToken)
    {
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var root = Path.GetFullPath(repositoryPath);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            var relative = Path.GetRelativePath(root, file);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0]
                .Equals(".git", StringComparison.OrdinalIgnoreCase)) continue;

            var entry = archive.CreateEntry(relative.Replace(Path.DirectorySeparatorChar, '/'), CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            await input.CopyToAsync(entryStream, cancellationToken);
        }
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class BackupManifest
    {
        public string ProjectId { get; set; } = "";
        public List<string> DeletedTrackedFiles { get; set; } = [];
        public string CanonicalRemoteUrl { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
        public string GitBundleSha256 { get; set; } = "";
        public string WorkingTreeSha256 { get; set; } = "";
        public bool IncludesGitHistory { get; set; }
        public bool IncludesTrackedAndUntrackedFiles { get; set; }
        public bool ExcludesGitInternals { get; set; }
    }
}
