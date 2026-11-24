using System.IO.Compression;
using GitManager.App.Models;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class ProjectBackupServiceTests
{
    [Fact]
    public async Task CreateBackup_StoresGitHistoryAndWorkingTree_AndDetectsTampering()
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
            await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "backup test");
            await File.WriteAllTextAsync(Path.Combine(repository, "untracked.txt"), "untracked content");
            await git.RunAsync(repository, "add", ".");
            await git.RunAsync(repository, "commit", "-m", "initial");

            var project = new ProjectRecord
            {
                ProjectId = "test-project",
                CanonicalRemoteUrl = "https://github.com/example/project.git",
                Owner = "example",
                Repository = "project",
                ContainerPath = Path.Combine(root, "project"),
                RepositoryPath = repository
            };
            var service = new ProjectBackupService(git);
            var backup = await service.CreateBackupAsync(project);

            Assert.True(await service.VerifyBackupAsync(backup));
            using (var archive = ZipFile.OpenRead(Path.Combine(backup, "working-tree.zip")))
            {
                Assert.Contains(archive.Entries, entry => entry.FullName == "README.md");
                Assert.Contains(archive.Entries, entry => entry.FullName == "untracked.txt");
                Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith(".git/", StringComparison.OrdinalIgnoreCase));
            }

            await File.AppendAllTextAsync(Path.Combine(backup, "repository.bundle"), "tamper");
            Assert.False(await service.VerifyBackupAsync(backup));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
