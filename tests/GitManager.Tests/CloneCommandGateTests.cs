using GitManager.App.ViewModels;
using Xunit;

namespace GitManager.Tests;

public sealed class CloneCommandGateTests
{
    [Fact]
    public void CanExecute_RequiresOpenLibraryJournalAndUrl()
    {
        Assert.False(CloneCommandGate.CanExecute(false, true, "https://github.com/octocat/Hello-World"));
        Assert.False(CloneCommandGate.CanExecute(true, false, "https://github.com/octocat/Hello-World"));
        Assert.False(CloneCommandGate.CanExecute(true, true, ""));
        Assert.False(CloneCommandGate.CanExecute(true, true, "   "));
        Assert.False(CloneCommandGate.CanExecute(true, true, null));
        Assert.True(CloneCommandGate.CanExecute(true, true, "https://github.com/octocat/Hello-World"));
    }

    [Fact]
    public void DisabledReason_IsPersian()
    {
        Assert.Equal(CloneCommandGate.LibraryClosedReason,
            CloneCommandGate.DisabledReason(false, true, "https://github.com/octocat/Hello-World"));
        Assert.Equal(CloneCommandGate.JournalUnavailableReason,
            CloneCommandGate.DisabledReason(true, false, "https://github.com/octocat/Hello-World"));
        Assert.Equal(CloneCommandGate.MissingUrlReason, CloneCommandGate.DisabledReason(true, true, ""));
        Assert.Equal("", CloneCommandGate.DisabledReason(true, true, "https://github.com/octocat/Hello-World"));
        Assert.Contains("کتابخانه", CloneCommandGate.LibraryClosedReason);
        Assert.Contains("نشانی", CloneCommandGate.MissingUrlReason);
        Assert.Contains("دفتر ثبت عملیات", CloneCommandGate.JournalUnavailableReason);
    }
}
