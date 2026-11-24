using System.Text;
using Microsoft.Data.Sqlite;

namespace GitManager.Tests;

/// <summary>
/// Test helpers for journal files. Non-SQLite text replacement is a different
/// case from scrambling interior SQLite pages while keeping the file header.
/// </summary>
internal static class SqliteJournalFile
{
    public static async Task CheckpointAsync(string path)
    {
        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await command.ExecuteNonQueryAsync();
    }

    public static void ScrambleInteriorPagesPreservingHeader(string path)
    {
        foreach (var sidecar in new[] { path + "-wal", path + "-shm", path + "-journal" })
        {
            if (File.Exists(sidecar)) File.Delete(sidecar);
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 200)
            throw new InvalidOperationException("SQLite file is too small to scramble interior pages.");

        var magic = Encoding.ASCII.GetString(bytes, 0, 15);
        if (magic != "SQLite format 3")
            throw new InvalidOperationException("Expected a SQLite header before scrambling interior pages.");

        var pageSize = (bytes[16] << 8) | bytes[17];
        if (pageSize == 1) pageSize = 65536;
        if (pageSize < 512) pageSize = bytes.Length;

        var end = Math.Min(bytes.Length, pageSize);
        for (var i = 100; i < end; i++)
            bytes[i] ^= 0xA5;

        File.WriteAllBytes(path, bytes);
    }

    public static string ReadHeaderMagic(string path)
    {
        var header = new byte[15];
        using var stream = File.OpenRead(path);
        var read = stream.Read(header, 0, header.Length);
        if (read != 15)
            throw new InvalidOperationException("SQLite file header is truncated.");
        return Encoding.ASCII.GetString(header);
    }
}
