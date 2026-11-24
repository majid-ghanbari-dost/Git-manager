using GitManager.App.Services;

namespace GitManager.Tests;

public sealed class PathSafetyTests
{
    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("..", "_")]
    [InlineData("project/name", "project_name")]
    [InlineData("folder.", "folder")]
    public void SafeSegment_NormalizesWindowsUnsafeNames(string input, string expected)
    {
        Assert.Equal(expected, PathSafety.SafeSegment(input));
    }

    [Fact]
    public void IsWithin_RejectsSiblingPathWithSharedPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "GitManagerRoot");
        var sibling = Path.Combine(Path.GetTempPath(), "GitManagerRoot-Other", "repo");
        Assert.False(PathSafety.IsWithin(root, sibling));
    }
}
