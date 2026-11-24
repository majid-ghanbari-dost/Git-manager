using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using GitManager.App.Models;

namespace GitManager.App.Services;

public sealed class ProjectBackupService(GitProcess git)
{
    public async Task<string> CreateBackupAsync(ProjectRecord project, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(project.RepositoryPath))
            throw new DirectoryNotFoundException("پوشه مخزن پروژه پیدا نشد.");

        var backupRoot = Path.Combine(project.ContainerPath, "backups");
        Directory.CreateDirectory(backupRoot);
        var backupId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
        var staging = Path.Combine(backupRoot, ".staging-" + Guid.NewGuid().ToString("N"));
        var final = Path.Combine(backupRoot, backupId);
        Directory.CreateDirectory(staging);

        try
        {
            var bundlePath = Path.Combine(staging, "repository.bundle");
            await git.RunAsync(project.RepositoryPath, "bundle", "create", bundlePath, "--all");
            await git.RunAsync(project.RepositoryPath, "bundle", "verify", bundlePath);

            var snapshotPath = Path.Combine(staging, "working-tree.zip");
            await CreateWorkingTreeSnapshotAsync(project.RepositoryPath, snapshotPath, cancellationToken);

            var deletedOutput = await git.RunAsync(project.RepositoryPath, "ls-files", "--deleted", "-z");
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

            Directory.Move(staging, final);
            return final;
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
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
