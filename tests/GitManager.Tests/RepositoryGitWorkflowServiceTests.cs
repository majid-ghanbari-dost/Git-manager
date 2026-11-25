using GitManager.App.Models;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class RepositoryGitWorkflowServiceTests
{
    [Fact]
    public async Task PullAsync_UsesFastForwardOnlyAndNeverForcePushes()
    {
        using var sandbox = new TestSandbox();
        var git = new FakeGitProcess();
        var service = sandbox.CreateService(git);

        var result = await service.PullAsync(sandbox.Project);

        Assert.Equal("main", result.Branch);
        var pull = Assert.Single(git.Calls, args => args[0] == "pull");
        Assert.Contains("--ff-only", pull);
        Assert.DoesNotContain("--force", pull);
        Assert.DoesNotContain("--force-with-lease", pull);
    }

    [Fact]
    public async Task PullAsync_RejectsDirtyWorkingTreeBeforeNetworkMutation()
    {
        using var sandbox = new TestSandbox();
        var git = new FakeGitProcess { StatusBefore = " M local.txt" };
        var service = sandbox.CreateService(git);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PullAsync(sandbox.Project));

        Assert.DoesNotContain(git.Calls, args => args[0] == "pull");
    }

    [Fact]
    public async Task CommitAllAsync_StagesAndCommitsOnlyAfterCallerSuppliesMessage()
    {
        using var sandbox = new TestSandbox();
        var git = new FakeGitProcess
        {
            StatusBefore = " M local.txt",
            StatusAfterAdd = "M  local.txt"
        };
        var service = sandbox.CreateService(git);

        var result = await service.CommitAllAsync(sandbox.Project, "Update local files");

        Assert.Equal("main", result.Branch);
        Assert.Contains(git.Calls, args => args.SequenceEqual(new[] { "add", "--all" }));
        Assert.Contains(git.Calls, args => args.SequenceEqual(new[] { "commit", "-m", "Update local files" }));
    }

    [Fact]
    public async Task FetchAsync_UsesGcmAndPrunesRemoteTrackingBranches()
    {
        using var sandbox = new TestSandbox();
        var git = new FakeGitProcess();
        var service = sandbox.CreateService(git);

        await service.FetchAsync(sandbox.Project);

        Assert.Contains(git.Calls, args => args.SequenceEqual(new[] { "config", "--get", "credential.helper" }));
        Assert.Contains(git.Calls, args => args.SequenceEqual(new[] { "fetch", "--prune", "origin" }));
    }

    private sealed class TestSandbox : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "git-manager-workflow-test-" + Guid.NewGuid().ToString("N"));
        public ProjectRecord Project { get; }

        public TestSandbox()
        {
            var repository = Path.Combine(_root, "repository");
            Directory.CreateDirectory(Path.Combine(repository, ".git"));
            Project = new ProjectRecord
            {
                ProjectId = Guid.NewGuid().ToString("N"),
                CanonicalRemoteUrl = "https://github.com/example/sample.git",
                Owner = "example",
                Repository = "sample",
                RepositoryPath = repository
            };
        }

        public RepositoryGitWorkflowService CreateService(GitProcess git) =>
            new(git,
                new ProjectOperationJournal(Path.Combine(_root, "journal.sqlite3")),
                new ProjectOperationLock(Path.Combine(_root, "locks")));

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeGitProcess : GitProcess
    {
        private int _statusCalls;
        public List<string[]> Calls { get; } = [];
        public string StatusBefore { get; init; } = "";
        public string StatusAfterAdd { get; init; } = "";

        protected override Task<string> RunCoreAsync(
            string workingDirectory,
            CancellationToken cancellationToken,
            string[] arguments)
        {
            Calls.Add(arguments);
            var result = arguments[0] switch
            {
                "remote" => "https://github.com/example/sample.git",
                "config" => "manager",
                "symbolic-ref" => "main",
                "status" => ++_statusCalls == 1 ? StatusBefore : StatusAfterAdd,
                "rev-parse" => "0123456789abcdef",
                "fetch" or "pull" or "add" or "commit" => "",
                _ => throw new InvalidOperationException("Unexpected git command: " + string.Join(" ", arguments))
            };
            return Task.FromResult(result);
        }
    }
}
