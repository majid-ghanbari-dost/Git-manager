using System.IO.Compression;
using System.Text.Json;

namespace GitManager.App.Services;

public sealed class ProjectRestoreService(GitProcess git, ProjectBackupService backups)
{
    public async Task<string> RestoreToNewContainerAsync(string backupDirectory, string destinationParent, CancellationToken cancellationToken = default)
    {
        if (!await backups.VerifyBackupAsync(backupDirectory, cancellationToken))
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

        Directory.CreateDirectory(staging);
        try
        {
            var repository = Path.Combine(staging, "repository");
            var bundle = Path.Combine(backupDirectory, "repository.bundle");
            await git.RunAsync(staging, "clone", bundle, repository);
            await git.RunAsync(repository, "remote", "set-url", "origin", manifest.CanonicalRemoteUrl);
            await OverlaySnapshotAsync(Path.Combine(backupDirectory, "working-tree.zip"), repository, cancellationToken);

            Directory.CreateDirectory(Path.Combine(staging, "backups"));
            Directory.CreateDirectory(Path.Combine(staging, "notes"));
            Directory.Move(staging, destination);
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            throw;
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
        public string CanonicalRemoteUrl { get; set; } = "";
    }
}
