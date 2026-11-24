using GitManager.App.Models;
using Microsoft.Data.Sqlite;

namespace GitManager.App.Services;

public sealed class ProjectCatalog
{
    private static readonly string[] DefaultCategories =
        ["Unclassified", "Kandoo", "Business Tools", "Experiments", "Personal", "Client Work"];

    private readonly string _connectionString;

    public ProjectCatalog(string workspaceRoot)
    {
        var dbPath = Path.Combine(workspaceRoot, "00_Manager", "catalog", "projects.sqlite3");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
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
                name TEXT PRIMARY KEY COLLATE NOCASE,
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

        using var transaction = connection.BeginTransaction();
        await EnsureColumnAsync(connection, transaction, "categories", "category_id", "TEXT");
        await EnsureColumnAsync(connection, transaction, "categories", "is_system", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(connection, transaction, "categories", "sort_order", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(connection, transaction, "projects", "category_id", "TEXT");

        foreach (var category in DefaultCategories)
        {
            var seed = connection.CreateCommand();
            seed.Transaction = transaction;
            seed.CommandText = """
                INSERT OR IGNORE INTO categories(name, created_at_utc, is_system, sort_order)
                VALUES ($name, $date, $system, $order)
                """;
            seed.Parameters.AddWithValue("$name", category);
            seed.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("O"));
            seed.Parameters.AddWithValue("$system", category == "Unclassified" ? 1 : 0);
            seed.Parameters.AddWithValue("$order", Array.IndexOf(DefaultCategories, category));
            await seed.ExecuteNonQueryAsync();
        }

        var categories = connection.CreateCommand();
        categories.Transaction = transaction;
        categories.CommandText = "SELECT name FROM categories WHERE category_id IS NULL OR category_id = ''";
        var missingNames = new List<string>();
        await using (var reader = await categories.ExecuteReaderAsync())
            while (await reader.ReadAsync()) missingNames.Add(reader.GetString(0));

        foreach (var name in missingNames)
        {
            var assign = connection.CreateCommand();
            assign.Transaction = transaction;
            assign.CommandText = "UPDATE categories SET category_id = $id, is_system = CASE WHEN name = 'Unclassified' THEN 1 ELSE is_system END WHERE name = $name";
            assign.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            assign.Parameters.AddWithValue("$name", name);
            await assign.ExecuteNonQueryAsync();
        }

        // Preserve custom legacy category names that may not be in the default seed set.
        var orphaned = connection.CreateCommand();
        orphaned.Transaction = transaction;
        orphaned.CommandText = """
            SELECT DISTINCT p.category
            FROM projects p
            WHERE p.category IS NOT NULL AND TRIM(p.category) <> ''
              AND NOT EXISTS (
                  SELECT 1 FROM categories c WHERE c.name = p.category COLLATE NOCASE
              )
            """;
        var orphanedNames = new List<string>();
        await using (var reader = await orphaned.ExecuteReaderAsync())
            while (await reader.ReadAsync()) orphanedNames.Add(reader.GetString(0));
        foreach (var name in orphanedNames)
        {
            var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO categories(category_id, name, created_at_utc, is_system, sort_order)
                VALUES ($id, $name, $date, 0, 999)
                """;
            insert.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            insert.Parameters.AddWithValue("$name", name);
            insert.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync();
        }

        var backfill = connection.CreateCommand();
        backfill.Transaction = transaction;
        backfill.CommandText = """
            UPDATE projects
            SET category_id = (
                SELECT category_id FROM categories
                WHERE categories.name = projects.category COLLATE NOCASE
            )
            WHERE category_id IS NULL OR category_id = ''
            """;
        await backfill.ExecuteNonQueryAsync();

        var index = connection.CreateCommand();
        index.Transaction = transaction;
        index.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS ux_categories_category_id ON categories(category_id);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_categories_name_nocase ON categories(name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS ix_projects_category_id ON projects(category_id);
            """;
        await index.ExecuteNonQueryAsync();
        transaction.Commit();
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        string table, string column, string declaration)
    {
        var check = connection.CreateCommand();
        check.Transaction = transaction;
        check.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await check.ExecuteReaderAsync();
        var exists = false;
        while (await reader.ReadAsync())
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                exists = true;
        reader.Close();
        if (exists) return;

        var alter = connection.CreateCommand();
        alter.Transaction = transaction;
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {declaration}";
        await alter.ExecuteNonQueryAsync();
    }

    public async Task<List<CategoryRecord>> GetCategoriesAsync()
    {
        var result = new List<CategoryRecord>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT category_id, name, is_system, sort_order, created_at_utc
            FROM categories ORDER BY sort_order, name COLLATE NOCASE
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(new CategoryRecord(reader.GetString(0), reader.GetString(1),
                reader.GetInt64(2) != 0, reader.GetInt32(3),
                DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        return result;
    }

    public async Task<CategoryRecord> CreateCategoryAsync(string name)
    {
        var normalized = ValidateCategoryName(name);
        var category = new CategoryRecord(Guid.NewGuid().ToString("N"), normalized, false,
            (await GetCategoriesAsync()).Count, DateTime.UtcNow);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO categories(category_id, name, created_at_utc, is_system, sort_order)
            VALUES ($id, $name, $date, 0, $order)
            """;
        command.Parameters.AddWithValue("$id", category.CategoryId);
        command.Parameters.AddWithValue("$name", category.Name);
        command.Parameters.AddWithValue("$date", category.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$order", category.SortOrder);
        try { await command.ExecuteNonQueryAsync(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        { throw new InvalidOperationException("دسته‌بندی با این نام از قبل وجود دارد.", ex); }
        return category;
    }

    // A non-empty category rename is deliberately rejected here. The workspace-aware
    // CategoryManagementService performs the physical move and catalog update as one
    // recoverable operation; a database-only rename would orphan project paths.
    public async Task RenameEmptyCategoryAsync(string categoryId, string newName)
    {
        var normalized = ValidateCategoryName(newName);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var lookup = connection.CreateCommand();
        lookup.Transaction = transaction;
        lookup.CommandText = "SELECT name, is_system FROM categories WHERE category_id = $id";
        lookup.Parameters.AddWithValue("$id", categoryId);
        string oldName;
        bool isSystem;
        await using (var reader = await lookup.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) throw new InvalidOperationException("دسته‌بندی پیدا نشد.");
            oldName = reader.GetString(0);
            isSystem = reader.GetInt64(1) != 0;
        }
        if (isSystem || oldName.Equals("Unclassified", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("دسته‌بندی Unclassified سیستمی است و قابل تغییر نام نیست.");

        var count = connection.CreateCommand();
        count.Transaction = transaction;
        count.CommandText = "SELECT COUNT(*) FROM projects WHERE category_id = $id";
        count.Parameters.AddWithValue("$id", categoryId);
        if (Convert.ToInt64(await count.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("این دسته‌بندی پروژه دارد؛ تغییر نام باید از طریق انتقال فیزیکی پروژه‌ها انجام شود.");

        var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE categories SET name = $name WHERE category_id = $id";
        update.Parameters.AddWithValue("$name", normalized);
        update.Parameters.AddWithValue("$id", categoryId);
        try { await update.ExecuteNonQueryAsync(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        { throw new InvalidOperationException("دسته‌بندی با این نام از قبل وجود دارد.", ex); }
        transaction.Commit();
    }

    public async Task DeleteEmptyCategoryAsync(string categoryId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var lookup = connection.CreateCommand();
        lookup.Transaction = transaction;
        lookup.CommandText = "SELECT name, is_system FROM categories WHERE category_id = $id";
        lookup.Parameters.AddWithValue("$id", categoryId);
        string name;
        bool isSystem;
        await using (var reader = await lookup.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) throw new InvalidOperationException("دسته‌بندی پیدا نشد.");
            name = reader.GetString(0);
            isSystem = reader.GetInt64(1) != 0;
        }
        if (isSystem || name.Equals("Unclassified", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("دسته‌بندی Unclassified سیستمی است و قابل حذف نیست.");

        var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = """
            DELETE FROM categories
            WHERE category_id = $id
              AND NOT EXISTS (SELECT 1 FROM projects WHERE category_id = $id)
            """;
        delete.Parameters.AddWithValue("$id", categoryId);
        if (await delete.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("دسته‌بندی دارای پروژه است یا قبلاً حذف شده؛ هیچ داده‌ای حذف نشد.");
        transaction.Commit();
    }

    public async Task<List<ProjectRecord>> GetProjectsAsync()
    {
        var result = new List<ProjectRecord>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT project_id, canonical_remote_url, host, owner, repository, category_id, category,
                   lifecycle, container_path, repository_path, current_branch, current_commit, imported_at_utc
            FROM projects ORDER BY repository COLLATE NOCASE
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(new ProjectRecord
            {
                ProjectId = reader.GetString(0), CanonicalRemoteUrl = reader.GetString(1),
                Host = reader.GetString(2), Owner = reader.GetString(3), Repository = reader.GetString(4),
                CategoryId = reader.IsDBNull(5) ? "" : reader.GetString(5), Category = reader.GetString(6),
                Lifecycle = reader.GetString(7), ContainerPath = reader.GetString(8),
                RepositoryPath = reader.GetString(9), CurrentBranch = reader.GetString(10),
                CurrentCommit = reader.GetString(11),
                ImportedAtUtc = DateTime.Parse(reader.GetString(12), null, System.Globalization.DateTimeStyles.RoundtripKind)
            });
        return result;
    }

    public async Task AddProjectAsync(ProjectRecord project)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var category = connection.CreateCommand();
        category.CommandText = "SELECT category_id FROM categories WHERE name = $name COLLATE NOCASE";
        category.Parameters.AddWithValue("$name", project.Category);
        var categoryId = await category.ExecuteScalarAsync() as string
            ?? throw new InvalidOperationException("دسته‌بندی انتخاب‌شده دیگر وجود ندارد.");
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO projects(project_id, canonical_remote_url, host, owner, repository, category_id, category,
                lifecycle, container_path, repository_path, current_branch, current_commit, imported_at_utc)
            VALUES ($id, $url, $host, $owner, $repo, $categoryId, $category, $lifecycle,
                $container, $path, $branch, $commit, $date)
            """;
        command.Parameters.AddWithValue("$id", project.ProjectId);
        command.Parameters.AddWithValue("$url", project.CanonicalRemoteUrl);
        command.Parameters.AddWithValue("$host", project.Host);
        command.Parameters.AddWithValue("$owner", project.Owner);
        command.Parameters.AddWithValue("$repo", project.Repository);
        command.Parameters.AddWithValue("$categoryId", categoryId);
        command.Parameters.AddWithValue("$category", project.Category);
        command.Parameters.AddWithValue("$lifecycle", project.Lifecycle);
        command.Parameters.AddWithValue("$container", project.ContainerPath);
        command.Parameters.AddWithValue("$path", project.RepositoryPath);
        command.Parameters.AddWithValue("$branch", project.CurrentBranch);
        command.Parameters.AddWithValue("$commit", project.CurrentCommit);
        command.Parameters.AddWithValue("$date", project.ImportedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync();
        project.CategoryId = categoryId;
    }

    private static string ValidateCategoryName(string name)
    {
        var normalized = (name ?? "").Trim();
        if (normalized.Length is < 2 or > 48)
            throw new ArgumentException("نام دسته‌بندی باید بین ۲ تا ۴۸ نویسه باشد.", nameof(name));
        if (normalized.Equals("Unclassified", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("نام Unclassified رزرو شده است.", nameof(name));
        if (normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            normalized.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0 ||
            normalized is "." or "..")
            throw new ArgumentException("نام دسته‌بندی شامل نویسه غیرمجاز است.", nameof(name));
        return normalized;
    }
}
