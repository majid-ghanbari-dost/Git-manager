using System.IO.Compression;
using System.Text.Json;

namespace GitManager.App.Services;

public sealed class ProjectRestoreService
{
    private readonly GitProcess _git;
    private readonly ProjectBackupService _backups;
    private readonly ProjectOperationJournal _journal;
    private readonly ProjectOperationLock _operationLock;

    public ProjectRestoreService(
        GitProcess git,
        ProjectBackupService backups,
        ProjectOperationJournal? journal = null,
        ProjectOperationLock? operationLock = null)
    {
        _git = git;
        _backups = backups;
        _journal = journal ?? new ProjectOperationJournal();
        _operationLock = operationLock ?? new ProjectOperationLock();
    }

    public async Task<string> RestoreToNewContainerAsync(
        string backupDirectory,
        string destinationParent,
        CancellationToken cancellationToken = default)
    {
        if (!await _backups.VerifyBackupAsync(backupDirectory, cancellationToken))
            throw new InvalidOperationException("اعتبار پشتیبان یا SHA-256 آن تأیید نشد؛ بازیابی متوقف شد.");

        var manifest = JsonSerializer.Deserialize<RestoreManifest>(
            await File.ReadAllTextAsync(Path.Combine(backupDirectory, "manifest.json"), cancellationToken))
            ?? throw new InvalidDataException("فایل manifest پشتیبان معتبر نیست.");
        if (string.IsNullOrWhiteSpace(manifest.CanonicalRemoteUrl))
            throw new InvalidDataException("نشانی مخزن اصلی در manifest موجود نیست.");

        Directory.CreateDirectory(destinationParent);
        var destination = Path.Combine(Path.GetFullPath(destinationParent),
            "Restored-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        var staging = destination + ".staging";
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("مسیر مقصد از قبل وجود دارد.");

        var lockKey = "restore:" + Path.GetFullPath(backupDirectory);
        await using var lease = await _operationLock.AcquireAsync(lockKey, cancellationToken);
        var operation = await _journal.BeginAsync("Restore", lockKey, manifest.ProjectId, staging, destination, cancellationToken);
        Directory.CreateDirectory(staging);
        try
        {
            await _journal.SetStateAsync(operation.OperationId, "Running", cancellationToken: cancellationToken);
            var repository = Path.Combine(staging, "repository");
            var bundle = Path.Combine(backupDirectory, "repository.bundle");
            await _git.RunAsync(staging, cancellationToken, "clone", bundle, repository);
            await _git.RunAsync(repository, cancellationToken, "remote", "set-url", "origin", manifest.CanonicalRemoteUrl);
            await OverlaySnapshotAsync(Path.Combine(backupDirectory, "working-tree.zip"), repository, cancellationToken);
            ApplyDeletedTrackedFiles(repository, manifest.DeletedTrackedFiles);

            Directory.CreateDirectory(Path.Combine(staging, "backups"));
            Directory.CreateDirectory(Path.Combine(staging, "notes"));
            await _journal.SetStateAsync(operation.OperationId, "CommitPending", cancellationToken: cancellationToken);
            Directory.Move(staging, destination);
            await _journal.SetStateAsync(operation.OperationId, "Completed", cancellationToken: cancellationToken);
            return destination;
        }
        catch (Exception ex)
        {
            try
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
                await _journal.SetStateAsync(operation.OperationId,
                    Directory.Exists(destination) ? "RecoveryRequired" : "RolledBack",
                    ex.Message, CancellationToken.None);
            }
            catch (Exception cleanupError)
            {
                await _journal.SetStateAsync(operation.OperationId, "RecoveryRequired",
                    $"{ex.Message} | پاک‌سازی staging ناموفق بود: {cleanupError.Message}", CancellationToken.None);
            }
            throw;
        }
    }

    private static void ApplyDeletedTrackedFiles(string repositoryRoot, IEnumerable<string> deletedFiles)
    {
        var root = Path.GetFullPath(repositoryRoot);
        foreach (var relative in deletedFiles)
        {
            var segments = relative.Split(new[] { '/', (char)92 });
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)
                || segments.Any(part => part is "." or ".."))
                throw new InvalidDataException("فهرست حذف‌ها شامل مسیر غیرمجاز است.");
            if (segments[0].Equals(".git", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("فهرست حذف‌ها به فایل‌های داخلی Git اشاره می‌کند.");
            var target = Path.GetFullPath(Path.Combine(root, relative));
            if (!PathSafety.IsWithin(root, target))
                throw new InvalidDataException("مسیر حذف خارج از پوشه بازیابی قرار دارد.");
            if (File.Exists(target)) File.Delete(target);
        }
    }

    private static async Task OverlaySnapshotAsync(string snapshotPath, string repositoryRoot, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(repositoryRoot);
        using var archive = ZipFile.OpenRead(snapshotPath);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var normalized = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized) || normalized.Split(Path.DirectorySeparatorChar).Any(part => part == ".."))
                throw new InvalidDataException("مسیر غیرمجاز در فایل پشتیبان شناسایی شد.");
            if (normalized.Split(Path.DirectorySeparatorChar)[0].Equals(".git", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("آرشیو تلاش می‌کند فایل‌های داخلی Git را بازنویسی کند.");

            var target = Path.GetFullPath(Path.Combine(root, normalized));
            if (!PathSafety.IsWithin(root, target))
                throw new InvalidDataException("مسیر یکی از فایل‌های پشتیبان خارج از مقصد قرار می‌گیرد.");
            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixType == 0xA000)
                throw new InvalidDataException("بازیابی لینک نمادین از این پشتیبان پشتیبانی نمی‌شود.");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = entry.Open();
            await using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, cancellationToken);
        }
    }

    private sealed class RestoreManifest
    {
        public string ProjectId { get; set; } = "";
        public string CanonicalRemoteUrl { get; set; } = "";
        public List<string> DeletedTrackedFiles { get; set; } = [];
    }
}
