using Xunit;
using GitManager.App.Models;
using GitManager.App.Services;

namespace GitManager.Tests;

public sealed class RepositoryPublishServiceTests
{
    [Fact]
    public async Task PushAsync_UsesGitCredentialManagerAndNeverForcePushes()
    {
        var root = Path.Combine(Path.GetTempPath(), "git-manager-push-test-" + Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(root, "repository");
        Directory.CreateDirectory(Path.Combine(repository, ".git"));
        try
        {
            var git = new FakeGitProcess();
            var journal = new ProjectOperationJournal(Path.Combine(root, "journal.sqlite3"));
            var operationLock = new ProjectOperationLock(Path.Combine(root, "locks"));
            var service = new RepositoryPublishService(git, journal, operationLock);
            var project = new ProjectRecord
            {
                ProjectId = "test-project",
                CanonicalRemoteUrl = "https://github.com/example/sample.git",
                Owner = "example",
                Repository = "sample",
                RepositoryPath = repository
            };

            var result = await service.PushAsync(project);

            Assert.Equal("main", result.Branch);
            Assert.Equal("0123456789abcdef", result.Commit);
            var push = Assert.Single(git.Calls.Where(args => args.Length > 0 && args[0] == "push"));
            Assert.Contains("--set-upstream", push);
            Assert.Contains("origin", push);
            Assert.DoesNotContain("--force", push);
            Assert.DoesNotContain("--force-with-lease", push);
            Assert.Contains(git.Calls, args => args.SequenceEqual(new[] { "config", "--get", "credential.helper" }));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PushAsync_RejectsRemoteMismatchBeforePushing()
    {
        var root = Path.Combine(Path.GetTempPath(), "git-manager-push-test-" + Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(root, "repository");
        Directory.CreateDirectory(Path.Combine(repository, ".git"));
        try
        {
            var git = new FakeGitProcess { RemoteUrl = "https://github.com/other/repository.git" };
            var service = new RepositoryPublishService(
                git,
                new ProjectOperationJournal(Path.Combine(root, "journal.sqlite3")),
                new ProjectOperationLock(Path.Combine(root, "locks")));
            var project = new ProjectRecord
            {
                ProjectId = "test-project",
                CanonicalRemoteUrl = "https://github.com/example/sample.git",
                RepositoryPath = repository
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.PushAsync(project));
            Assert.DoesNotContain(git.Calls, args => args.Length > 0 && args[0] == "push");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeGitProcess : GitProcess
    {
        public List<string[]> Calls { get; } = [];
        public string RemoteUrl { get; init; } = "https://github.com/example/sample.git";

        protected override Task<string> RunCoreAsync(
            string workingDirectory,
            CancellationToken cancellationToken,
            string[] arguments)
        {
            Calls.Add(arguments);
            var result = arguments[0] switch
            {
                "remote" => RemoteUrl,
                "config" => "manager",
                "symbolic-ref" => "main",
                "status" => "",
                "rev-parse" => "0123456789abcdef",
                "push" => "",
                _ => throw new InvalidOperationException("Unexpected git command: " + string.Join(" ", arguments))
            };
            return Task.FromResult(result);
        }
    }
}
