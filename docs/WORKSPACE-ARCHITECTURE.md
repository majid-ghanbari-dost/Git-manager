# Workspace Architecture

## 1. Design objective

The user selects one root directory. Git Manager turns it into an organized project library. A root directory is a container, not a place where repository files are mixed together.

## 2. Directory contract

```text
<WorkspaceRoot>/
├── 00_Manager/
│   ├── catalog/projects.sqlite3
│   ├── settings/
│   ├── logs/
│   └── temp/
├── 01_Active/
│   └── <Category>/<Owner>/<Repo>/
│       ├── repository/       # git clone root
│       ├── backups/
│       │   └── YYYY/MM/
│       │       └── <UTC-timestamp>_<commit-short-sha>/
│       │           ├── repository.bundle
│       │           ├── MANIFEST.json
│       │           └── SHA256SUMS
│       └── notes/
├── 02_On-Hold/
├── 03_Archived/
├── 04_Quarantine/
└── 05_Exports/
```

The same project substructure applies under Active, On-Hold, Archived, and Quarantine. A lifecycle move relocates the project container atomically where possible; it does not alter the Git remote, branches, tags, or commit history.

## 3. Identity and naming

Canonical identity: `github.com/<owner>/<repository>`, normalized case-insensitively while preserving display casing.

Filesystem path: `<lifecycle>/<category>/<owner>/<repository>`.

- Owner and repository path segments must be sanitized for Windows reserved names and invalid characters.
- The catalog stores the canonical URL and the actual local path.
- If a sanitized path collides, append a short stable hash of the canonical identity.
- Renaming a display label must not change canonical identity.
- Repositories hosted outside github.com may be supported later through a host-aware identity scheme.

## 4. Category versus lifecycle

Category describes what the project is for; lifecycle describes its current state.

Examples:
- Active / Kandoo / majid-ghanbari-dost / Kandoo-Digital-Invoice
- On-Hold / Experiments / owner / repo
- Archived / Client-Work / owner / repo

The first run creates an Unclassified category. Users may add, rename, and reorder categories. The app must never infer a category and silently move a project.

## 5. Import transaction

1. Parse and validate the GitHub URL.
2. Verify repository access using the user's configured Git authentication.
3. Resolve owner, repository, default branch, visibility, and canonical identity.
4. Check the catalog and target path for duplicates.
5. Ask for category and lifecycle (Active by default), showing the exact final path.
6. Clone into a unique temporary staging directory under `00_Manager/temp`.
7. Verify the clone, remote URL, HEAD, refs, and working-tree status.
8. Move the complete project container into its final location.
9. Register it in SQLite in a transaction.
10. If any step fails, clean only the temporary staging folder and leave existing projects untouched.

No project is cloned directly over an existing folder.

## 6. Backup model

Use `git bundle create --all` (or an equivalent verified command) to capture reachable refs and full Git history. The backup also includes a manifest with:

- Canonical repository URL and local project ID
- Backup timestamp (UTC)
- Current branch and commit
- Included refs
- Git version and backup format version
- Bundle file size and SHA-256
- Whether submodules or Git LFS objects require separate backup handling

Git bundle does not automatically include uncommitted working-tree changes, untracked files, submodule contents, or LFS objects. Therefore, the application must either create an additional working-copy snapshot or explicitly report those omissions. A backup must not claim to be complete when it is not.

## 7. Restore model

- Verify manifest schema and all checksums before extraction.
- Restore to a new directory by default.
- Never overwrite an existing working copy without a separate explicit confirmation and a verified pre-restore backup.
- Recreate the Git repository from the bundle, then restore any separately captured working-tree snapshot.
- Report missing submodule/LFS payloads instead of silently presenting an incomplete restore as successful.

## 8. Catalog

SQLite is the source of truth for local inventory, not the GitHub repository.

Minimum project fields:
- project_id (stable UUID)
- canonical_remote_url
- host, owner, repository
- display_name
- category_id
- lifecycle
- local_container_path
- repository_path
- default_branch, current_branch, current_commit
- visibility (when known)
- imported_at, last_seen_at, last_backup_at
- last_operation_status

The catalog can be rebuilt by scanning known project metadata, but it must not move or delete folders during a scan.

## 9. Multi-project isolation

All operations receive a project_id and resolve paths through the catalog. No operation may use a global shared working directory. Before write, backup, restore, or publish, verify that the resolved repository path belongs to the selected project container and that its Git remote matches the catalog identity.
