using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class RepositoryImportTests
{
    public const string PublicCloneUrl = "https://github.com/octocat/Hello-World";

    [Fact]
    public async Task Clone_Unit_RegistersProjectInCatalogAndOpensContainer()
    {
        var root = NewRoot();
        try
        {
            var opened = await OpenLibraryAsync(root, new ScriptedGitProcess("https://github.com/octocat/Hello-World.git"));
            var project = await opened.Importer.CloneAsync(root, PublicCloneUrl, "Unclassified");

            var saved = Assert.Single(await opened.Catalog.GetProjectsAsync());
            Assert.Equal(project.ProjectId, saved.ProjectId);
            Assert.Equal("octocat", saved.Owner);
            Assert.Equal("Hello-World", saved.Repository);
            Assert.Equal("https://github.com/octocat/Hello-World.git", saved.CanonicalRemoteUrl);
            Assert.Equal("Unclassified", saved.Category);
            Assert.Equal("Active", saved.Lifecycle);
            Assert.Equal("main", saved.CurrentBranch);
            Assert.Equal("0123456789abcdef0123456789abcdef01234567", saved.CurrentCommit);
            Assert.True(Directory.Exists(saved.ContainerPath));
            Assert.True(Directory.Exists(saved.RepositoryPath));
            Assert.True(Directory.Exists(Path.Combine(saved.ContainerPath, "backups")));
            Assert.True(Directory.Exists(Path.Combine(saved.ContainerPath, "notes")));
            Assert.True(File.Exists(Path.Combine(saved.RepositoryPath, "README.md")));
            Assert.Empty(await new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3")).GetInterruptedAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Clone_Unit_RejectsDuplicateCanonicalRemote()
    {
        var root = NewRoot();
        try
        {
            var opened = await OpenLibraryAsync(root, new ScriptedGitProcess("https://github.com/octocat/Hello-World.git"));
            await opened.Importer.CloneAsync(root, PublicCloneUrl, "Unclassified");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => opened.Importer.CloneAsync(root, PublicCloneUrl + ".git", "Unclassified"));
            Assert.Contains("قبلاً در کتابخانه ثبت شده است", ex.Message);
            Assert.Single(await opened.Catalog.GetProjectsAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Clone_Unit_DoesNotOverwriteExistingDestination()
    {
        var root = NewRoot();
        try
        {
            var opened = await OpenLibraryAsync(root, new ScriptedGitProcess("https://github.com/octocat/Hello-World.git"));
            var destination = new WorkspaceService().GetProjectContainer(root, "Active", "Unclassified", "octocat", "Hello-World");
            Directory.CreateDirectory(destination);
            await File.WriteAllTextAsync(Path.Combine(destination, "keep.txt"), "existing");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => opened.Importer.CloneAsync(root, PublicCloneUrl, "Unclassified"));
            Assert.Contains("پوشه مقصد از قبل وجود دارد", ex.Message);
            Assert.Equal("existing", await File.ReadAllTextAsync(Path.Combine(destination, "keep.txt")));
            Assert.Empty(await opened.Catalog.GetProjectsAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Clone_CorruptJournal_DoesNotCreatePartialProject()
    {
        var root = NewRoot();
        try
        {
            var journalPath = Path.Combine(root, "operations.sqlite3");
            await File.WriteAllTextAsync(journalPath, "CORRUPT-NOT-SQLITE-" + new string('X', 1024));
            var git = new ScriptedGitProcess("https://github.com/octocat/Hello-World.git");
            var opened = await LibraryStartup.OpenAsync(
                root,
                new ProjectOperationJournal(journalPath),
                git,
                new ProjectOperationLock(Path.Combine(root, "locks")));
            Assert.False(opened.JournalUsable);

            var destination = new WorkspaceService().GetProjectContainer(
                root, "Active", "Unclassified", "octocat", "Hello-World");
            var ex = await Assert.ThrowsAsync<JournalUnavailableException>(
                () => opened.Importer.CloneAsync(root, PublicCloneUrl, "Unclassified"));
            Assert.Equal(JournalUnavailableException.UserMessage, ex.Message);
            Assert.Empty(await opened.Catalog.GetProjectsAsync());
            Assert.False(Directory.Exists(destination));
            Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(root, "00_Manager", "temp")));
            Assert.False(git.CloneWasInvoked);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Clone_JournalCorruptMidOperation_PreservesStagingAndOriginalError()
    {
        var root = NewRoot();
        try
        {
            var journalPath = Path.Combine(root, "operations.sqlite3");
            var git = new ScriptedGitProcess("https://github.com/octocat/Hello-World.git");
            var journal = new CorruptOnStateJournal(journalPath, "CommitPending");
            var opened = await OpenLibraryAsync(root, git, journal);
            var destination = new WorkspaceService().GetProjectContainer(
                root, "Active", "Unclassified", "octocat", "Hello-World");

            var ex = await Assert.ThrowsAsync<JournalUnavailableException>(
                () => opened.Importer.CloneAsync(root, PublicCloneUrl, "Unclassified"));
            Assert.Equal(JournalUnavailableException.UserMessage, ex.Message);
            Assert.True(git.CloneWasInvoked);
            Assert.Empty(await opened.Catalog.GetProjectsAsync());
            Assert.False(Directory.Exists(destination));
            var staging = Assert.Single(Directory.GetDirectories(Path.Combine(root, "00_Manager", "temp")));
            Assert.True(File.Exists(Path.Combine(staging, "repository", "README.md")));
            var log = File.ReadAllText(DiagnosticLog.FilePath(DiagnosticLog.DirectoryFor(root)));
            Assert.Contains("could not record RecoveryRequired", log);
            Assert.Contains("staging and destination were left in place", log);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Clone_GitFailureThenJournalCorrupt_PreservesOriginalErrorAndStaging()
    {
        var root = NewRoot();
        try
        {
            var journalPath = Path.Combine(root, "operations.sqlite3");
            var gitError = new InvalidOperationException("Git با خطا متوقف شد: simulated clone failure");
            var git = new ScriptedGitProcess("https://github.com/octocat/Hello-World.git", gitError);
            var journal = new CorruptOnStateJournal(journalPath, "RolledBack");
            var opened = await OpenLibraryAsync(root, git, journal);
            var destination = new WorkspaceService().GetProjectContainer(
                root, "Active", "Unclassified", "octocat", "Hello-World");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => opened.Importer.CloneAsync(root, PublicCloneUrl, "Unclassified"));
            Assert.Same(gitError, ex);
            Assert.Contains("simulated clone failure", ex.Message);
            Assert.DoesNotContain("SQLite", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await opened.Catalog.GetProjectsAsync());
            Assert.False(Directory.Exists(destination));
            var staging = Assert.Single(Directory.GetDirectories(Path.Combine(root, "00_Manager", "temp")));
            Assert.True(File.Exists(Path.Combine(staging, "repository", "README.md")));
            var log = File.ReadAllText(DiagnosticLog.FilePath(DiagnosticLog.DirectoryFor(root)));
            Assert.Contains("could not record rollback", log);
            Assert.Contains("staging was not deleted", log);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Clone_RealPublicRepository_RegistersInCatalogAndIsOpenable()
    {
        var root = NewRoot();
        try
        {
            Environment.SetEnvironmentVariable("GIT_TERMINAL_PROMPT", "0");
            Environment.SetEnvironmentVariable("GCM_INTERACTIVE", "never");
            var opened = await OpenLibraryAsync(root, new GitProcess());
            var project = await opened.Importer.CloneAsync(root, PublicCloneUrl, "Unclassified");

            var saved = Assert.Single(await opened.Catalog.GetProjectsAsync());
            Assert.Equal(project.ProjectId, saved.ProjectId);
            Assert.Equal("octocat", saved.Owner);
            Assert.Equal("Hello-World", saved.Repository);
            Assert.Equal("https://github.com/octocat/Hello-World.git", saved.CanonicalRemoteUrl);
            Assert.True(Directory.Exists(saved.RepositoryPath));
            Assert.True(Directory.Exists(Path.Combine(saved.RepositoryPath, ".git")));
            var head = await new GitProcess().RunAsync(saved.RepositoryPath, "rev-parse", "HEAD");
            Assert.False(string.IsNullOrWhiteSpace(head));
            Assert.Equal(head, saved.CurrentCommit);
            var origin = await new GitProcess().RunAsync(saved.RepositoryPath, "remote", "get-url", "origin");
            Assert.Equal(saved.CanonicalRemoteUrl, GitHubRemote.Parse(origin).CanonicalUrl);
        }
        finally { DeleteRoot(root); }
    }

    private static Task<LibraryStartupResult> OpenLibraryAsync(
        string root, GitProcess git, ProjectOperationJournal? journal = null) =>
        LibraryStartup.OpenAsync(
            root,
            journal ?? new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3")),
            git,
            new ProjectOperationLock(Path.Combine(root, "locks")));

    private sealed class CorruptOnStateJournal(string path, string corruptOnState) : ProjectOperationJournal(path)
    {
        public override async Task SetStateAsync(
            string operationId, string state, string? error = null, CancellationToken cancellationToken = default)
        {
            if (state == corruptOnState)
                await File.WriteAllTextAsync(path, "CORRUPT-NOT-SQLITE-" + new string('X', 1024), cancellationToken);
            await base.SetStateAsync(operationId, state, error, cancellationToken);
        }
    }

    private sealed class ScriptedGitProcess(string originUrl, Exception? cloneError = null) : GitProcess
    {
        public bool CloneWasInvoked { get; private set; }
        public string[]? LastCloneArguments { get; private set; }

        protected override Task<string> RunCoreAsync(
            string workingDirectory,
            CancellationToken cancellationToken,
            string[] arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (arguments.Length >= 4 && arguments[0] == "clone" && arguments[1] == "--")
            {
                CloneWasInvoked = true;
                LastCloneArguments = arguments;
                var destination = arguments[3];
                Directory.CreateDirectory(Path.Combine(destination, ".git"));
                File.WriteAllText(Path.Combine(destination, "README.md"), "unit-clone");
                if (cloneError is not null) throw cloneError;
                return Task.FromResult("");
            }

            return Task.FromResult(arguments switch
            {
                ["remote", "get-url", "origin"] => originUrl,
                ["branch", "--show-current"] => "main",
                ["rev-parse", "HEAD"] => "0123456789abcdef0123456789abcdef01234567",
                _ => throw new InvalidOperationException("unexpected git: " + string.Join(' ', arguments))
            });
        }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerRepositoryImportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        catch
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
