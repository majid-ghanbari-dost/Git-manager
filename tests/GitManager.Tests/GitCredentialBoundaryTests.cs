using System.Text.Json;
using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class GitCredentialBoundaryTests
{
    [Fact]
    public void GitHubRemote_RejectsEmbeddedCredentials()
    {
        Assert.Throws<InvalidOperationException>(
            () => GitHubRemote.Parse("https://user:ghp_exampletoken@github.com/owner/repo"));
        Assert.Throws<InvalidOperationException>(
            () => GitHubRemote.Parse("https://user@github.com/owner/repo"));
    }

    [Fact]
    public async Task Settings_PersistOnlyWorkspacePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerCredentialBoundary", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var settings = new SettingsService(settingsPath);
            await settings.SaveWorkspaceAsync(Path.Combine(root, "library"));
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
            var names = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
            Assert.Equal(["WorkspacePath"], names);
            Assert.DoesNotContain("token", await File.ReadAllTextAsync(settingsPath), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", await File.ReadAllTextAsync(settingsPath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Clone_PassesOnlyCanonicalHttpsUrlToGit()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerCredentialBoundary", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var git = new CapturingGitProcess();
            var opened = await LibraryStartup.OpenAsync(
                root,
                new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3")),
                git,
                new ProjectOperationLock(Path.Combine(root, "locks")));
            await opened.Importer.CloneAsync(root, "https://github.com/octocat/Hello-World", "Unclassified");

            Assert.NotNull(git.CloneArguments);
            Assert.Equal("clone", git.CloneArguments![0]);
            Assert.Equal("--", git.CloneArguments[1]);
            Assert.Equal("https://github.com/octocat/Hello-World.git", git.CloneArguments[2]);
            Assert.DoesNotContain(git.CloneArguments, argument =>
                argument.Contains('@') ||
                argument.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                argument.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                argument.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GitProcess_DoesNotConfigureCredentialEnvironment()
    {
        var source = FindSource("src/GitManager.App/Services/GitProcess.cs");
        Assert.DoesNotContain("GITHUB_TOKEN", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GIT_ASKPASS", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GH_TOKEN", source, StringComparison.Ordinal);
        Assert.DoesNotContain("password", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", source, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingGitProcess : GitProcess
    {
        public string[]? CloneArguments { get; private set; }

        protected override Task<string> RunCoreAsync(
            string workingDirectory,
            CancellationToken cancellationToken,
            string[] arguments)
        {
            if (arguments.Length >= 4 && arguments[0] == "clone" && arguments[1] == "--")
            {
                CloneArguments = arguments;
                var destination = arguments[3];
                Directory.CreateDirectory(Path.Combine(destination, ".git"));
                File.WriteAllText(Path.Combine(destination, "README.md"), "unit-clone");
                return Task.FromResult("");
            }

            return Task.FromResult(arguments switch
            {
                ["remote", "get-url", "origin"] => "https://github.com/octocat/Hello-World.git",
                ["branch", "--show-current"] => "main",
                ["rev-parse", "HEAD"] => "0123456789abcdef0123456789abcdef01234567",
                _ => throw new InvalidOperationException("unexpected git: " + string.Join(' ', arguments))
            });
        }
    }

    private static string FindSource(string relativePath)
    {
        var copied = Path.Combine(AppContext.BaseDirectory, "source", "GitProcess.cs");
        if (File.Exists(copied)) return File.ReadAllText(copied);
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException(relativePath);
    }
}
