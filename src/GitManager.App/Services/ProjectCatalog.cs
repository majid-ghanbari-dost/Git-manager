using GitManager.App.Models;
using Microsoft.Data.Sqlite;

namespace GitManager.App.Services;

public sealed class ProjectCatalog
{
    private readonly string _connectionString;

    public ProjectCatalog(string workspaceRoot)
    {
        var dbPath = Path.Combine(workspaceRoot, "00_Manager", "catalog", "projects.sqlite3");
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;
            CREATE TABLE IF NOT EXISTS categories (
                name TEXT PRIMARY KEY,
                created_at_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS projects (
                project_id TEXT PRIMARY KEY,
                canonical_remote_url TEXT NOT NULL UNIQUE,
                host TEXT NOT NULL,
                owner TEXT NOT NULL,
                repository TEXT NOT NULL,
                category TEXT NOT NULL,
                lifecycle TEXT NOT NULL,
                container_path TEXT NOT NULL,
                repository_path TEXT NOT NULL,
                current_branch TEXT NOT NULL,
                current_commit TEXT NOT NULL,
                imported_at_utc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
        foreach (var category in new[] { "Unclassified", "Kandoo", "Business Tools", "Experiments", "Personal", "Client Work" })
        {
            var seed = connection.CreateCommand();
            seed.CommandText = "INSERT OR IGNORE INTO categories(name, created_at_utc) VALUES ($name, $date)";
            seed.Parameters.AddWithValue("$name", category);
            seed.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("O"));
            await seed.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<ProjectRecord>> GetProjectsAsync()
    {
        var result = new List<ProjectRecord>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, canonical_remote_url, host, owner, repository, category, lifecycle, container_path, repository_path, current_branch, current_commit, imported_at_utc FROM projects ORDER BY repository COLLATE NOCASE";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ProjectRecord
            {
                ProjectId = reader.GetString(0), CanonicalRemoteUrl = reader.GetString(1),
                Host = reader.GetString(2), Owner = reader.GetString(3), Repository = reader.GetString(4),
                Category = reader.GetString(5), Lifecycle = reader.GetString(6),
                ContainerPath = reader.GetString(7), RepositoryPath = reader.GetString(8),
                CurrentBranch = reader.GetString(9), CurrentCommit = reader.GetString(10),
                ImportedAtUtc = DateTime.Parse(reader.GetString(11), null, System.Globalization.DateTimeStyles.RoundtripKind)
            });
        }
        return result;
    }

    public async Task AddProjectAsync(ProjectRecord project)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO projects(project_id, canonical_remote_url, host, owner, repository, category, lifecycle,
                container_path, repository_path, current_branch, current_commit, imported_at_utc)
            VALUES ($id, $url, $host, $owner, $repo, $category, $lifecycle, $container, $path, $branch, $commit, $date)
            """;
        command.Parameters.AddWithValue("$id", project.ProjectId);
        command.Parameters.AddWithValue("$url", project.CanonicalRemoteUrl);
        command.Parameters.AddWithValue("$host", project.Host);
        command.Parameters.AddWithValue("$owner", project.Owner);
        command.Parameters.AddWithValue("$repo", project.Repository);
        command.Parameters.AddWithValue("$category", project.Category);
        command.Parameters.AddWithValue("$lifecycle", project.Lifecycle);
        command.Parameters.AddWithValue("$container", project.ContainerPath);
        command.Parameters.AddWithValue("$path", project.RepositoryPath);
        command.Parameters.AddWithValue("$branch", project.CurrentBranch);
        command.Parameters.AddWithValue("$commit", project.CurrentCommit);
        command.Parameters.AddWithValue("$date", project.ImportedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }
}
