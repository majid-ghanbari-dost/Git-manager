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
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString();
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

        var normalizeBlankCategories = connection.CreateCommand();
        normalizeBlankCategories.Transaction = transaction;
        normalizeBlankCategories.CommandText = "UPDATE projects SET category = 'Unclassified' WHERE category IS NULL OR TRIM(category) = ''";
        await normalizeBlankCategories.ExecuteNonQueryAsync();

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

        var pinSystemCategory = connection.CreateCommand();
        pinSystemCategory.Transaction = transaction;
        pinSystemCategory.CommandText = "UPDATE categories SET sort_order = -1 WHERE name = 'Unclassified' COLLATE NOCASE";
        await pinSystemCategory.ExecuteNonQueryAsync();

        var index = connection.CreateCommand();
        index.Transaction = transaction;
        index.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS ux_categories_category_id ON categories(category_id);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_categories_name_nocase ON categories(name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS ix_projects_category_id ON projects(category_id);
            CREATE TRIGGER IF NOT EXISTS trg_projects_category_insert
            BEFORE INSERT ON projects
            WHEN NEW.category_id IS NULL OR NOT EXISTS (
                SELECT 1 FROM categories WHERE category_id = NEW.category_id
            )
            BEGIN
                SELECT RAISE(ABORT, 'project category_id must reference an existing category');
            END;
            CREATE TRIGGER IF NOT EXISTS trg_projects_category_update
            BEFORE UPDATE OF category_id ON projects
            WHEN NEW.category_id IS NULL OR NOT EXISTS (
                SELECT 1 FROM categories WHERE category_id = NEW.category_id
            )
            BEGIN
                SELECT RAISE(ABORT, 'project category_id must reference an existing category');
            END;
            """;
        await index.ExecuteNonQueryAsync();
        await ApplyLifecycleSchemaAsync(connection, transaction);
        transaction.Commit();
    }

    private static async Task ApplyLifecycleSchemaAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        await EnsureColumnAsync(connection, transaction, "projects", "availability", "TEXT NOT NULL DEFAULT 'Unknown'");
        await EnsureColumnAsync(connection, transaction, "projects", "last_seen_at_utc", "TEXT");
        await EnsureColumnAsync(connection, transaction, "projects", "last_backup_at_utc", "TEXT");
        await EnsureColumnAsync(connection, transaction, "projects", "last_operation_status", "TEXT");
        await EnsureColumnAsync(connection, transaction, "projects", "lifecycle_changed_at_utc", "TEXT");

        var schema = connection.CreateCommand();
        schema.Transaction = transaction;
        schema.CommandText = """
            CREATE TABLE IF NOT EXISTS app_meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS project_lifecycle_events (
                event_seq INTEGER PRIMARY KEY AUTOINCREMENT,
                event_id TEXT NOT NULL UNIQUE,
                project_id TEXT NOT NULL,
                from_lifecycle TEXT NOT NULL,
                to_lifecycle TEXT NOT NULL,
                operation_id TEXT,
                result TEXT NOT NULL,
                git_status_summary TEXT,
                availability_at_start TEXT,
                message TEXT,
                created_at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_lifecycle_events_project
                ON project_lifecycle_events(project_id, event_seq);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_lifecycle_events_operation_catalog_committed
                ON project_lifecycle_events(operation_id)
                WHERE result = 'CatalogCommitted' AND operation_id IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ux_lifecycle_events_operation_terminal
                ON project_lifecycle_events(operation_id)
                WHERE result IN ('Completed', 'RolledBack') AND operation_id IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ux_lifecycle_events_operation_recovery_required
                ON project_lifecycle_events(operation_id)
                WHERE result = 'RecoveryRequired' AND operation_id IS NOT NULL;
            """;
        await schema.ExecuteNonQueryAsync();

        var normalize = connection.CreateCommand();
        normalize.Transaction = transaction;
        normalize.CommandText = "UPDATE projects SET lifecycle = 'Active' WHERE lifecycle IS NULL OR TRIM(lifecycle) = ''";
        await normalize.ExecuteNonQueryAsync();

        var triggers = connection.CreateCommand();
        triggers.Transaction = transaction;
        triggers.CommandText = """
            CREATE TRIGGER IF NOT EXISTS trg_projects_lifecycle_insert
            BEFORE INSERT ON projects
            WHEN NEW.lifecycle NOT IN ('Active', 'On-Hold', 'Archived')
            BEGIN
                SELECT RAISE(ABORT, 'invalid project lifecycle');
            END;
            CREATE TRIGGER IF NOT EXISTS trg_projects_lifecycle_update
            BEFORE UPDATE OF lifecycle ON projects
            WHEN NEW.lifecycle NOT IN ('Active', 'On-Hold', 'Archived')
            BEGIN
                SELECT RAISE(ABORT, 'invalid project lifecycle');
            END;
            """;
        await triggers.ExecuteNonQueryAsync();

        var version = connection.CreateCommand();
        version.Transaction = transaction;
        version.CommandText = """
            INSERT OR IGNORE INTO app_meta(key, value) VALUES ('schema_version', '0');
            UPDATE app_meta SET value = '2.3'
            WHERE key = 'schema_version' AND value < '2.3';
            """;
        await version.ExecuteNonQueryAsync();
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
                   lifecycle, container_path, repository_path, current_branch, current_commit, imported_at_utc,
                   availability, last_operation_status, last_seen_at_utc, last_backup_at_utc, lifecycle_changed_at_utc
            FROM projects ORDER BY repository COLLATE NOCASE
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(ReadProject(reader));
        return result;
    }

    private static ProjectRecord ReadProject(SqliteDataReader reader) => new()
    {
        ProjectId = reader.GetString(0), CanonicalRemoteUrl = reader.GetString(1),
        Host = reader.GetString(2), Owner = reader.GetString(3), Repository = reader.GetString(4),
        CategoryId = reader.IsDBNull(5) ? "" : reader.GetString(5), Category = reader.GetString(6),
        Lifecycle = reader.GetString(7), ContainerPath = reader.GetString(8),
        RepositoryPath = reader.GetString(9), CurrentBranch = reader.GetString(10),
        CurrentCommit = reader.GetString(11),
        ImportedAtUtc = DateTime.Parse(reader.GetString(12), null, System.Globalization.DateTimeStyles.RoundtripKind),
        Availability = reader.IsDBNull(13) ? "Unknown" : reader.GetString(13),
        LastOperationStatus = reader.IsDBNull(14) ? null : reader.GetString(14),
        LastSeenAtUtc = ReadNullableDate(reader, 15),
        LastBackupAtUtc = ReadNullableDate(reader, 16),
        LifecycleChangedAtUtc = ReadNullableDate(reader, 17)
    };

    private static DateTime? ReadNullableDate(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null
            : DateTime.Parse(reader.GetString(ordinal), null, System.Globalization.DateTimeStyles.RoundtripKind);

    public async Task AddProjectAsync(ProjectRecord project)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var category = connection.CreateCommand();
        category.Transaction = transaction;
        category.CommandText = "SELECT category_id FROM categories WHERE name = $name COLLATE NOCASE";
        category.Parameters.AddWithValue("$name", project.Category);
        var categoryId = await category.ExecuteScalarAsync() as string
            ?? throw new InvalidOperationException("دسته‌بندی انتخاب‌شده دیگر وجود ندارد.");
        var command = connection.CreateCommand();
        command.Transaction = transaction;
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
        transaction.Commit();
        project.CategoryId = categoryId;
    }

    public async Task UpdateProjectCategoryAndPathAsync(
        string projectId, string categoryId, string categoryName,
        string containerPath, string repositoryPath, bool allowPendingCategoryRename = false)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var categoryCheck = connection.CreateCommand();
        categoryCheck.Transaction = transaction;
        categoryCheck.CommandText = "SELECT name FROM categories WHERE category_id = $id";
        categoryCheck.Parameters.AddWithValue("$id", categoryId);
        var actualName = await categoryCheck.ExecuteScalarAsync() as string;
        if (actualName is null || (!allowPendingCategoryRename &&
            !actualName.Equals(categoryName, StringComparison.Ordinal)))
            throw new InvalidOperationException("دسته‌بندی مقصد در کاتالوگ وجود ندارد یا تغییر کرده است.");

        var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE projects
            SET category_id = $categoryId, category = $category,
                container_path = $container, repository_path = $repository
            WHERE project_id = $projectId
            """;
        update.Parameters.AddWithValue("$categoryId", categoryId);
        update.Parameters.AddWithValue("$category", categoryName);
        update.Parameters.AddWithValue("$container", containerPath);
        update.Parameters.AddWithValue("$repository", repositoryPath);
        update.Parameters.AddWithValue("$projectId", projectId);
        if (await update.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("پروژه برای به‌روزرسانی پیدا نشد.");
        transaction.Commit();
    }

    public async Task CommitLifecycleMoveAsync(
        string projectId, string lifecycle, string containerPath, string repositoryPath,
        LifecycleEventRecord catalogCommittedEvent)
    {
        if (!LifecycleState.IsKnown(lifecycle))
            throw new InvalidOperationException($"وضعیت چرخه عمر «{lifecycle}» نامعتبر است.");
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE projects
            SET lifecycle = $lifecycle, container_path = $container, repository_path = $repository,
                last_operation_status = $status, lifecycle_changed_at_utc = $changed,
                availability = 'Available', last_seen_at_utc = $changed
            WHERE project_id = $id
            """;
        update.Parameters.AddWithValue("$lifecycle", lifecycle);
        update.Parameters.AddWithValue("$container", containerPath);
        update.Parameters.AddWithValue("$repository", repositoryPath);
        update.Parameters.AddWithValue("$status", "LifecycleMove:CatalogCommitted");
        update.Parameters.AddWithValue("$changed", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$id", projectId);
        if (await update.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("پروژه برای به‌روزرسانی چرخه عمر پیدا نشد.");
        await InsertEventCoreAsync(connection, transaction, catalogCommittedEvent);
        transaction.Commit();
    }

    public async Task InsertLifecycleEventAsync(LifecycleEventRecord eventRecord)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        await InsertEventCoreAsync(connection, transaction, eventRecord);
        transaction.Commit();
    }

    public async Task MarkLifecycleOperationCompletedAsync(string projectId, LifecycleEventRecord completedEvent)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        await InsertEventCoreAsync(connection, transaction, completedEvent);
        var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE projects SET last_operation_status = 'LifecycleMove:Completed'
            WHERE project_id = $id
            """;
        update.Parameters.AddWithValue("$id", projectId);
        await update.ExecuteNonQueryAsync();
        transaction.Commit();
    }

    public async Task UpdateGitSnapshotAsync(string projectId, string currentBranch, string currentCommit)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE projects
            SET current_branch = $branch, current_commit = $commit
            WHERE project_id = $id
            """;
        command.Parameters.AddWithValue("$branch", currentBranch);
        command.Parameters.AddWithValue("$commit", currentCommit);
        command.Parameters.AddWithValue("$id", projectId);
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("پروژه برای به‌روزرسانی شاخه و Commit پیدا نشد.");
    }

    public async Task UpdateAvailabilityAsync(string projectId, string availability, DateTime? lastSeenUtc, string? status = null)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE projects
            SET availability = $availability,
                last_seen_at_utc = COALESCE($seen, last_seen_at_utc),
                last_operation_status = COALESCE($status, last_operation_status)
            WHERE project_id = $id
            """;
        command.Parameters.AddWithValue("$availability", availability);
        command.Parameters.AddWithValue("$seen", (object?)lastSeenUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", (object?)status ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", projectId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateLastOperationStatusAsync(string projectId, string status)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE projects SET last_operation_status = $status WHERE project_id = $id";
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$id", projectId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<LifecycleEventRecord>> GetLifecycleEventsAsync(string? projectId = null)
    {
        var result = new List<LifecycleEventRecord>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT event_seq, event_id, project_id, from_lifecycle, to_lifecycle, operation_id, result,
                   git_status_summary, availability_at_start, message, created_at_utc
            FROM project_lifecycle_events
            WHERE $project IS NULL OR project_id = $project
            ORDER BY event_seq
            """;
        command.Parameters.AddWithValue("$project", (object?)projectId ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(new LifecycleEventRecord(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                DateTime.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        return result;
    }

    public async Task<string?> GetSchemaVersionAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_meta WHERE key = 'schema_version'";
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task InsertEventCoreAsync(
        SqliteConnection connection, SqliteTransaction transaction, LifecycleEventRecord eventRecord)
    {
        var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT OR IGNORE INTO project_lifecycle_events(
                event_id, project_id, from_lifecycle, to_lifecycle, operation_id, result,
                git_status_summary, availability_at_start, message, created_at_utc)
            VALUES ($id, $project, $from, $to, $operation, $result, $git, $availability, $message, $created)
            """;
        insert.Parameters.AddWithValue("$id", eventRecord.EventId);
        insert.Parameters.AddWithValue("$project", eventRecord.ProjectId);
        insert.Parameters.AddWithValue("$from", eventRecord.FromLifecycle);
        insert.Parameters.AddWithValue("$to", eventRecord.ToLifecycle);
        insert.Parameters.AddWithValue("$operation", (object?)eventRecord.OperationId ?? DBNull.Value);
        insert.Parameters.AddWithValue("$result", eventRecord.Result);
        insert.Parameters.AddWithValue("$git", (object?)eventRecord.GitStatusSummary ?? DBNull.Value);
        insert.Parameters.AddWithValue("$availability", (object?)eventRecord.AvailabilityAtStart ?? DBNull.Value);
        insert.Parameters.AddWithValue("$message", (object?)eventRecord.Message ?? DBNull.Value);
        insert.Parameters.AddWithValue("$created", eventRecord.CreatedAtUtc.ToString("O"));
        await insert.ExecuteNonQueryAsync();
    }

    public async Task ReorderCategoryAsync(string categoryId, int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        var categories = await GetCategoriesAsync();
        var index = categories.FindIndex(x => x.CategoryId == categoryId);
        if (index < 0) throw new InvalidOperationException("دسته‌بندی پیدا نشد.");
        var targetIndex = index + direction;
        if (targetIndex < 0 || targetIndex >= categories.Count) return;
        if (targetIndex == 0 || (index == 0 && categories[index].IsSystem))
            throw new InvalidOperationException("دسته‌بندی Unclassified همیشه در ابتدای فهرست باقی می‌ماند.");

        (categories[index], categories[targetIndex]) = (categories[targetIndex], categories[index]);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        for (var order = 0; order < categories.Count; order++)
        {
            var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE categories SET sort_order = $order WHERE category_id = $id";
            update.Parameters.AddWithValue("$order", order);
            update.Parameters.AddWithValue("$id", categories[order].CategoryId);
            await update.ExecuteNonQueryAsync();
        }
        transaction.Commit();
    }

    public async Task RenameCategoryRecordAsync(string categoryId, string newName)
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
        if (!PathSafety.SafeSegment(normalized).Equals(normalized, StringComparison.Ordinal))
            throw new ArgumentException("نام دسته‌بندی با قواعد نام‌گذاری پوشه در ویندوز سازگار نیست.", nameof(name));
        return normalized;
    }
}
