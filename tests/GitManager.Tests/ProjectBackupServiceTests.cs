using System.IO.Compression;
using GitManager.App.Models;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class ProjectBackupServiceTests
{
    [Fact]
    public async Task BackupAndRestore_PreserveHistoryAndWorkingTree_AndDetectTampering()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerBackupTests", Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(root, "project", "repository");
        Directory.CreateDirectory(repository);
        try
        {
            var git = new GitProcess();
            await git.RunAsync(repository, "init");
            await git.RunAsync(repository, "config", "user.name", "Git Manager Test");
            await git.RunAsync(repository, "config", "user.email", "git-manager-test@example.invalid");
            await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "committed content");
            await File.WriteAllTextAsync(Path.Combine(repository, "removed.txt"), "this file is deleted before backup");
            await git.RunAsync(repository, "add", ".");
            await git.RunAsync(repository, "commit", "-m", "initial");
            File.Delete(Path.Combine(repository, "removed.txt"));
            await File.WriteAllTextAsync(Path.Combine(repository, "untracked.txt"), "untracked content");

            var project = new ProjectRecord
            {
                ProjectId = "test-project",
                CanonicalRemoteUrl = "https://github.com/example/project.git",
                Owner = "example",
                Repository = "project",
                ContainerPath = Path.Combine(root, "project"),
                RepositoryPath = repository
            };
            var backupService = new ProjectBackupService(git);
            var backup = await backupService.CreateBackupAsync(project);

            Assert.True(await backupService.VerifyBackupAsync(backup));
            using (var archive = ZipFile.OpenRead(Path.Combine(backup, "working-tree.zip")))
            {
                Assert.Contains(archive.Entries, entry => entry.FullName == "README.md");
                Assert.Contains(archive.Entries, entry => entry.FullName == "untracked.txt");
                Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith(".git/", StringComparison.OrdinalIgnoreCase));
            }

            var restoreParent = Path.Combine(root, "restores");
            var restoreService = new ProjectRestoreService(git, backupService);
            var restoredContainer = await restoreService.RestoreToNewContainerAsync(backup, restoreParent);
            var restoredRepository = Path.Combine(restoredContainer, "repository");
            Assert.Equal("committed content", await File.ReadAllTextAsync(Path.Combine(restoredRepository, "README.md")));
            Assert.Equal("untracked content", await File.ReadAllTextAsync(Path.Combine(restoredRepository, "untracked.txt")));
            Assert.False(File.Exists(Path.Combine(restoredRepository, "removed.txt")));
            var restoredStatus = await git.RunAsync(restoredRepository, "status", "--short");
            Assert.Contains("?? untracked.txt", restoredStatus);
            Assert.Contains("D removed.txt", restoredStatus);
            Assert.Equal(await git.RunAsync(repository, "rev-parse", "HEAD"),
                await git.RunAsync(restoredRepository, "rev-parse", "HEAD"));

            await File.AppendAllTextAsync(Path.Combine(backup, "repository.bundle"), "tamper");
            Assert.False(await backupService.VerifyBackupAsync(backup));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => restoreService.RestoreToNewContainerAsync(backup, restoreParent));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
                    File.SetAttributes(path, FileAttributes.Normal);
                File.SetAttributes(root, FileAttributes.Normal);
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
