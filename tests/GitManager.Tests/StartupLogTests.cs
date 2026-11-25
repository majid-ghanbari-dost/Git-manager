using GitManager.App.Services;
using Xunit;

namespace GitManager.Tests;

public sealed class StartupLogTests
{
    [Fact]
    public void Write_CreatesLocalAppDataLog_WithoutThrowing()
    {
        StartupLog.Write("ProcessStarted", "unit-test");
        Assert.True(File.Exists(StartupLog.FilePath));
        var text = File.ReadAllText(StartupLog.FilePath);
        Assert.Contains("ProcessStarted", text, StringComparison.Ordinal);
        Assert.Contains("unit-test", text, StringComparison.Ordinal);
        Assert.StartsWith(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitManager"),
            Path.GetFullPath(StartupLog.FilePath),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeArguments_RedactsSecrets_KeepsCiFlag()
    {
        Assert.Equal("[redacted]", StartupLog.RedactArgument("token=abc"));
        Assert.Equal("[redacted]", StartupLog.RedactArgument("https://user:pass@github.com/x/y"));
        Assert.Equal("--ci-smoke", StartupLog.RedactArgument("--ci-smoke"));
        Assert.Equal(
            "--ci-smoke [redacted]",
            StartupLog.SanitizeArguments(["--ci-smoke", "password=secret"]));
    }
}
