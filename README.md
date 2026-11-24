# Git Manager

A Windows desktop application for organizing, cloning, backing up, restoring, and safely publishing GitHub repositories that the user is authorized to access.

**Core rule: one central workspace, one isolated folder per project.** Each repository has its own working copy, backups, notes, and stable project identity. Project category and lifecycle are separate concepts.

## Current implementation

Work is stacked on isolated branches and has not been merged into `main`. WP-2.3 added physical lifecycle moves. WP-2.4 on `work/wp-2-4-windows-productization` adds a self-contained Windows x64 installer (`GitManager-Setup-x64.exe`), portable ZIP, SHA-256 checksums, and a Windows-runner install/uninstall smoke. WP-2.5.1 on `work/wp-2-5-1-journal-clone-bootstrap` bootstraps the operation journal so first-run Clone is not blocked by a missing SQLite table, and keeps the library open if recovery fails. Uninstall does not delete the user workspace or LocalAppData catalog/journal. This is not a signed production release and is not a Product Owner clean-machine test. Publishing and submodule/LFS fidelity remain incomplete.

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

Prerequisites: Windows 10/11 x64, .NET 10 SDK for development, and Git for Windows available on `PATH`. CI publishes a self-contained portable ZIP and `GitManager-Setup-x64.exe`. Git for Windows is not bundled inside the installer.

Open `src/GitManager.App/GitManager.App.csproj` in Visual Studio 2026 or build with the .NET 10 SDK on Windows.

See:
- [Workspace architecture](docs/WORKSPACE-ARCHITECTURE.md)
- [Safety model](docs/SAFETY-MODEL.md)
- [Roadmap](docs/ROADMAP.md)
- [Implementation status](docs/IMPLEMENTATION-STATUS.md)
