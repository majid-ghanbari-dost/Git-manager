using GitManager.App.Services;
using GitManager.App.ViewModels;
using Xunit;

namespace GitManager.Tests;

public sealed class MainViewModelCloneTests
{
    [Fact]
    public async Task CloneCommand_EnablesWhenLibraryIsOpenAndUrlIsPresent()
    {
        var root = NewRoot();
        try
        {
            var vm = CreateViewModel(root);
            Assert.False(vm.IsCatalogOpen);
            Assert.False(vm.CloneCommand.CanExecute(null));
            Assert.Equal(CloneCommandGate.LibraryClosedReason, vm.CloneDisabledReason);

            vm.RepositoryUrl = "https://github.com/octocat/Hello-World";
            Assert.False(vm.CloneCommand.CanExecute(null));
            Assert.Equal(CloneCommandGate.LibraryClosedReason, vm.CloneDisabledReason);

            await vm.OpenWorkspaceAsync(Path.Combine(root, "workspace"));

            Assert.True(vm.IsCatalogOpen);
            Assert.False(vm.SensitiveOperationsLimited);
            Assert.True(vm.CloneCommand.CanExecute(null));
            Assert.Equal("", vm.CloneDisabledReason);
            Assert.True(vm.RestoreCommand.CanExecute(null));

            vm.RepositoryUrl = "";
            Assert.False(vm.CloneCommand.CanExecute(null));
            Assert.Equal(CloneCommandGate.MissingUrlReason, vm.CloneDisabledReason);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task RecoveryFailure_DoesNotDisableClone_AndLimitsSensitiveOperations()
    {
        var root = NewRoot();
        try
        {
            var settings = new SettingsService(Path.Combine(root, "settings.json"));
            var journal = new ThrowingOnReadJournal(Path.Combine(root, "operations.sqlite3"));
            var vm = new MainViewModel(settings, journal, new GitProcess(),
                new ProjectOperationLock(Path.Combine(root, "locks")));
            await vm.OpenWorkspaceAsync(Path.Combine(root, "workspace"));

            Assert.True(vm.IsCatalogOpen);
            Assert.True(vm.SensitiveOperationsLimited);
            Assert.Contains("عملیات حساس تا رفع مشکل محدود است", vm.StatusMessage);

            vm.RepositoryUrl = "https://github.com/octocat/Hello-World";
            Assert.True(vm.CloneCommand.CanExecute(null));
            Assert.Equal("", vm.CloneDisabledReason);
            Assert.False(vm.RestoreCommand.CanExecute(null));
            Assert.False(vm.BackupCommand.CanExecute(null));
            vm.RenameCategoryName = "NewName";
            Assert.False(vm.RenameCategoryCommand.CanExecute(null));
        }
        finally { DeleteRoot(root); }
    }

    private static MainViewModel CreateViewModel(string root) =>
        new(
            new SettingsService(Path.Combine(root, "settings.json")),
            new ProjectOperationJournal(Path.Combine(root, "operations.sqlite3")),
            new GitProcess(),
            new ProjectOperationLock(Path.Combine(root, "locks")));

    private sealed class ThrowingOnReadJournal(string path) : ProjectOperationJournal(path)
    {
        public override Task<IReadOnlyList<ProjectOperationEntry>> GetInterruptedAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated interrupted-operation read failure");
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerMainViewModelCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
