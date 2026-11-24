using System.Diagnostics;

namespace GitManager.App.Services;

public sealed class GitProcess
{
    public Task<string> RunAsync(string workingDirectory, params string[] arguments) =>
        RunCoreAsync(workingDirectory, CancellationToken.None, arguments);

    public Task<string> RunAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments) =>
        RunCoreAsync(workingDirectory, cancellationToken, arguments);

    private static async Task<string> RunCoreAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(20));
            await process.WaitForExitAsync(timeout.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Git با خطا متوقف شد: {Sanitize(stderr)}");
            return stdout.Trim();
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            if (cancellationToken.IsCancellationRequested)
                throw;
            throw new TimeoutException("زمان مجاز عملیات Git به پایان رسید.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException("Git for Windows نصب نیست یا در PATH قرار ندارد.");
        }
    }

    private static string Sanitize(string value) =>
        value.Length > 1200 ? value[..1200] : value;
}
