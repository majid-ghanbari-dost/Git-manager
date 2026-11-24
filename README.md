# Git Manager

A Windows desktop application for organizing, cloning, backing up, restoring, and safely publishing GitHub repositories that the user is authorized to access.

**Core rule: one central workspace, one isolated folder per project.** Each repository has its own working copy, backups, notes, and stable project identity. Project category and lifecycle are separate concepts.

## Current implementation

The Windows desktop foundation is being developed on `work/phase-1-foundation`. WP-2.1 adds the recovery journal and operation locking. WP-2.2 is in progress on `work/wp-2-2-category-management`: it introduces stable category IDs, an idempotent SQLite catalog migration, safe category CRUD, single-project moves, journaled batch rename for populated categories, persistent category ordering, and Persian UI controls. The latest crash-recovery and safety tests are being validated in Windows CI. Publishing, lifecycle management, submodule/LFS fidelity, and installer packaging remain incomplete; do not treat this branch as a production release.

See [implementation status](docs/IMPLEMENTATION-STATUS.md) for completed work and limitations.

## Planned workspace layout

```text
<WorkspaceRoot>/
├── 00_Manager/
│   ├── catalog/              # SQLite project registry
│   ├── settings/             # Non-secret preferences
│   ├── logs/
│   └── temp/                 # Transactional staging
├── 01_Active/<Category>/<Owner>/<Repository>/
│   ├── repository/            # Actual Git clone, including .git history
│   ├── backups/
│   └── notes/
├── 02_On-Hold/
├── 03_Archived/
├── 04_Quarantine/
└── 05_Exports/
```

## Safety requirements

- Never silently overwrite an existing project.
- Never force-push by default.
- Never store GitHub credentials in SQLite, settings, logs, or manifests.
- Never execute scripts or hooks automatically after cloning.
- Preserve full Git history by default.
- Backups include a Git bundle, working-tree snapshot, deletion-state manifest, and SHA-256 verification. Restore verifies the backup and writes only to a new destination.
- Current backup coverage excludes submodule contents, Git LFS payloads, symlink restoration, and complete filesystem metadata.
- A Git bundle alone does not preserve uncommitted changes, untracked files, submodule contents, or Git LFS objects; the backup workflow must report and handle these separately.

## Technology

- .NET 10 LTS / WPF / MVVM
- SQLite for the local project catalog
- Git for Windows invoked as a child process with argument-safe APIs
- Git Credential Manager or the user's configured Git credential helper

## Development

Prerequisites: Windows 10/11 x64, .NET 10 SDK for development, and Git for Windows available on `PATH`. The CI artifact is a self-contained portable build; a Setup.exe installer has not yet been produced.

Open `src/GitManager.App/GitManager.App.csproj` in Visual Studio 2026 or build with the .NET 10 SDK on Windows.

See:
- [Workspace architecture](docs/WORKSPACE-ARCHITECTURE.md)
- [Safety model](docs/SAFETY-MODEL.md)
- [Roadmap](docs/ROADMAP.md)
- [Implementation status](docs/IMPLEMENTATION-STATUS.md)
