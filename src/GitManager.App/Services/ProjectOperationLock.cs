using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace GitManager.App.Services;

public sealed class ProjectOperationLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProcessLocks = new(StringComparer.Ordinal);
    private readonly string _lockDirectory;

    public ProjectOperationLock(string? lockDirectory = null)
    {
        _lockDirectory = lockDirectory ?? AppLocalState.LockDirectory;
        Directory.CreateDirectory(_lockDirectory);
    }

    public async Task<IAsyncDisposable> AcquireAsync(
        string operationKey,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        var normalizedKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationKey))).ToLowerInvariant();
        var gate = ProcessLocks.GetOrAdd(normalizedKey, _ => new SemaphoreSlim(1, 1));
        var waitLimit = timeout ?? TimeSpan.FromSeconds(30);
        if (!await gate.WaitAsync(waitLimit, cancellationToken))
            throw new TimeoutException("عملیات دیگری روی همین پروژه در حال اجراست.");

        FileStream? fileLock = null;
        try
        {
            var path = Path.Combine(_lockDirectory, normalizedKey + ".lock");
            var started = DateTime.UtcNow;
            while (fileLock is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    fileLock = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    if (DateTime.UtcNow - started >= waitLimit)
                        throw new TimeoutException("قفل بین‌پردازه‌ای پروژه در مهلت مجاز آزاد نشد.");
                    await Task.Delay(100, cancellationToken);
                }
            }
            return new Releaser(gate, fileLock);
        }
        catch
        {
            fileLock?.Dispose();
            gate.Release();
            throw;
        }
    }

    private sealed class Releaser(SemaphoreSlim gate, FileStream fileLock) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                fileLock.Dispose();
                gate.Release();
            }
            return ValueTask.CompletedTask;
        }
    }
}
