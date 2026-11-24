# Git Manager

A Windows desktop application for organizing, cloning, backing up, restoring, and safely publishing GitHub repositories that the user is authorized to access.

**Core rule: one central workspace, one isolated folder per project.** Each repository has its own working copy, backups, notes, and stable project identity. Project category and lifecycle are separate concepts.

## Current implementation

The first Windows desktop foundation is being developed on `work/phase-1-foundation`. It includes a Persian RTL WPF shell, configurable workspace, SQLite inventory, GitHub URL validation, and a staged full-history clone workflow. It is not yet a production backup or publishing tool.

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
- Backups must include a manifest and SHA-256 verification before they can be called complete.
- A Git bundle alone does not preserve uncommitted changes, untracked files, submodule contents, or Git LFS objects; the backup workflow must report and handle these separately.

## Technology

- .NET 10 LTS / WPF / MVVM
- SQLite for the local project catalog
- Git for Windows invoked as a child process with argument-safe APIs
- Git Credential Manager or the user's configured Git credential helper

## Development

Open `src/GitManager.App/GitManager.App.csproj` in Visual Studio 2026 or build with the .NET 10 SDK on Windows.

See:
- [Workspace architecture](docs/WORKSPACE-ARCHITECTURE.md)
- [Safety model](docs/SAFETY-MODEL.md)
- [Roadmap](docs/ROADMAP.md)
- [Implementation status](docs/IMPLEMENTATION-STATUS.md)
