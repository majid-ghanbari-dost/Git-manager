using Microsoft.Data.Sqlite;

namespace GitManager.App.Services;

public sealed record ProjectOperationEntry(
    string OperationId,
    string OperationType,
    string OperationKey,
    string? ProjectId,
    string StagingPath,
    string FinalPath,
    string State,
    string? Error,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public class ProjectOperationJournal
{
    private readonly string _connectionString;

    public ProjectOperationJournal(string? databasePath = null)
    {
        databasePath ??= AppLocalState.JournalPath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=FULL;
                CREATE TABLE IF NOT EXISTS project_operations (
                    operation_id TEXT PRIMARY KEY,
                    operation_type TEXT NOT NULL,
                    operation_key TEXT NOT NULL,
                    project_id TEXT NULL,
                    staging_path TEXT NOT NULL,
                    final_path TEXT NOT NULL,
                    state TEXT NOT NULL,
                    error TEXT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_project_operations_state
                    ON project_operations(state, updated_at_utc);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            throw new JournalUnavailableException(ex);
        }
    }

    public async Task EnsureAvailableAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            var result = await command.ExecuteScalarAsync(cancellationToken) as string;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                throw new JournalUnavailableException();
        }
        catch (JournalUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            throw new JournalUnavailableException(ex);
        }
    }

    private static bool IsStorageFailure(Exception ex) =>
        ex is SqliteException or IOException or UnauthorizedAccessException;

    public async Task<ProjectOperationEntry> BeginAsync(
        string operationType,
        string operationKey,
        string? projectId,
        string stagingPath,
        string finalPath,
        CancellationToken cancellationToken = default)
    {
        await EnsureAvailableAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var entry = new ProjectOperationEntry(
            Guid.NewGuid().ToString("N"), operationType, operationKey, projectId,
            Path.GetFullPath(stagingPath), Path.GetFullPath(finalPath),
            "Prepared", null, now, now);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO project_operations(
                operation_id, operation_type, operation_key, project_id,
                staging_path, final_path, state, error, created_at_utc, updated_at_utc)
            VALUES ($id, $type, $key, $project, $staging, $final, $state, NULL, $created, $updated);
            """;
        command.Parameters.AddWithValue("$id", entry.OperationId);
        command.Parameters.AddWithValue("$type", entry.OperationType);
        command.Parameters.AddWithValue("$key", entry.OperationKey);
        command.Parameters.AddWithValue("$project", (object?)entry.ProjectId ?? DBNull.Value);
        command.Parameters.AddWithValue("$staging", entry.StagingPath);
        command.Parameters.AddWithValue("$final", entry.FinalPath);
        command.Parameters.AddWithValue("$state", entry.State);
        command.Parameters.AddWithValue("$created", now.ToString("O"));
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            throw new JournalUnavailableException(ex);
        }
        return entry;
    }

    public virtual async Task SetStateAsync(
        string operationId,
        string state,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeAsync(cancellationToken);
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE project_operations
                SET state = $state, error = $error, updated_at_utc = $updated
                WHERE operation_id = $id;
                """;
            command.Parameters.AddWithValue("$state", state);
            command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
            command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$id", operationId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("رکورد عملیات در Recovery Journal پیدا نشد.");
        }
        catch (JournalUnavailableException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            throw new JournalUnavailableException(ex);
        }
    }

    public virtual async Task<IReadOnlyList<ProjectOperationEntry>> GetInterruptedAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var result = new List<ProjectOperationEntry>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT operation_id, operation_type, operation_key, project_id,
                   staging_path, final_path, state, error, created_at_utc, updated_at_utc
            FROM project_operations
            WHERE state IN ('Prepared', 'Running', 'CommitPending', 'RecoveryRequired')
            ORDER BY created_at_utc;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ProjectOperationEntry(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                DateTime.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
                DateTime.Parse(reader.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        }
        return result;
    }

    public async Task<ProjectOperationEntry?> GetAsync(string operationId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT operation_id, operation_type, operation_key, project_id,
                   staging_path, final_path, state, error, created_at_utc, updated_at_utc
            FROM project_operations WHERE operation_id = $id
            """;
        command.Parameters.AddWithValue("$id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new ProjectOperationEntry(
            reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            DateTime.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
            DateTime.Parse(reader.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind));
    }
}
