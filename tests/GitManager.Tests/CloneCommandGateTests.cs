using GitManager.App.ViewModels;
using Xunit;

namespace GitManager.Tests;

public sealed class CloneCommandGateTests
{
    [Fact]
    public void CanExecute_RequiresOpenLibraryAndUrl()
    {
        Assert.False(CloneCommandGate.CanExecute(false, "https://github.com/octocat/Hello-World"));
        Assert.False(CloneCommandGate.CanExecute(true, ""));
        Assert.False(CloneCommandGate.CanExecute(true, "   "));
        Assert.False(CloneCommandGate.CanExecute(true, null));
        Assert.True(CloneCommandGate.CanExecute(true, "https://github.com/octocat/Hello-World"));
    }

    [Fact]
    public void DisabledReason_IsPersian()
    {
        Assert.Equal(CloneCommandGate.LibraryClosedReason,
            CloneCommandGate.DisabledReason(false, "https://github.com/octocat/Hello-World"));
        Assert.Equal(CloneCommandGate.MissingUrlReason, CloneCommandGate.DisabledReason(true, ""));
        Assert.Equal("", CloneCommandGate.DisabledReason(true, "https://github.com/octocat/Hello-World"));
        Assert.Contains("کتابخانه", CloneCommandGate.LibraryClosedReason);
        Assert.Contains("نشانی", CloneCommandGate.MissingUrlReason);
    }
}
