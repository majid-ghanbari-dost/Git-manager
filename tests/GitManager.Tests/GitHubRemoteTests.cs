using Xunit;
using GitManager.App.Services;

namespace GitManager.Tests;

public sealed class GitHubRemoteTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo", "owner", "repo")]
    [InlineData("https://github.com/owner/repo.git", "owner", "repo")]
    [InlineData("https://github.com/Owner/Repo/", "Owner", "Repo")]
    public void Parse_AcceptsGitHubRepositoryUrls(string input, string owner, string repository)
    {
        var result = GitHubRemote.Parse(input);
        Assert.Equal(owner, result.Owner);
        Assert.Equal(repository, result.Repository);
        Assert.Equal($"https://github.com/{owner}/{repository}.git", result.CanonicalUrl);
    }

    [Theory]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("https://github.com/owner")]
    [InlineData("file:///C:/repo")]
    [InlineData("https://github.com/owner/repo/extra")]
    [InlineData("https://github.com/owner/repo?redirect=1")]
    public void Parse_RejectsUnsupportedOrAmbiguousUrls(string input)
    {
        Assert.Throws<InvalidOperationException>(() => GitHubRemote.Parse(input));
    }
}
