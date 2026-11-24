# Git Manager

A Windows desktop application for organizing, cloning, backing up, restoring, and safely publishing GitHub repositories that the user is authorized to access.

## Core product rule

**One central workspace, one isolated folder per project.** Repositories must never be dumped together into a shared directory. Every GitHub repository gets its own stable project identity and its own working copy, backups, manifests, and local notes.

## Planned workspace layout

The user chooses one root folder, for example `D:\GitManager`. Git Manager manages this structure:

```text
D:\GitManager\
├── 00_Manager\
│   ├── catalog\              # Local project registry (SQLite)
│   ├── settings\             # User preferences; no access tokens
│   ├── logs\                 # Operation logs
│   └── temp\                 # Temporary staging; cleaned safely
├── 01_Active\
│   └── <Category>\
│       └── <GitHub-Owner>\
│           └── <Repository>\
│               ├── repository\   # Actual Git clone, including .git history
│               ├── backups\      # Timestamped Git bundles and manifests
│               └── notes\        # Local project notes and metadata
├── 02_On-Hold\
│   └── <Category>\<Owner>\<Repository>\...
├── 03_Archived\
│   └── <Category>\<Owner>\<Repository>\...
├── 04_Quarantine\               # Repositories needing inspection; never auto-run
└── 05_Exports\                  # User-requested portable exports
```

The exact root is configurable. The application creates the structure; users do not need to create it manually.

## Classification rules

- On import, the user can choose a category (for example: Kandoo, Business Tools, Experiments, Personal, Client Work).
- If no category is chosen, the project enters an explicit **Unclassified** category. The app must not guess silently.
- Lifecycle is separate from category: Active, On-Hold, and Archived are top-level states.
- Repository identity is based on normalized GitHub host + owner + repository name, not just the repository name.
- Same-named repositories from different owners must never collide.
- Re-importing an existing project must detect it and offer update, open, or cancel; it must not overwrite the working copy.
- Each project has its own repository directory. No cross-project shared working tree.

## Main workflows

1. **Import / Clone:** validate access, show destination and category, clone full Git history, register the project.
2. **Backup:** create a timestamped Git bundle plus manifest and SHA-256 checksums; preserve refs and history.
3. **Restore:** verify the manifest and checksums before restoring into a new or explicitly selected destination.
4. **Publish:** show changed files and diff, block likely secrets, create a commit, and push only after explicit confirmation.
5. **Organize:** change category or lifecycle without changing repository identity.
6. **Inventory:** searchable project catalog with owner, repository, category, lifecycle, local path, branch, last backup, and sync status.

## Safety requirements

- Never force-push by default.
- Never overwrite an existing project folder silently.
- Never delete a project or backup as a side effect of moving or reclassifying it.
- Do not store GitHub credentials in the application database or settings files. Use Git Credential Manager / Git's configured credential helper or an OS credential store.
- Never execute scripts, hooks, or project code automatically after cloning.
- Treat repository contents as untrusted until inspected.
- Detect and warn about secrets before staging or pushing. Secret detection is a safety net, not a guarantee.
- Preserve full Git history by default. Shallow clone must be an explicit advanced choice.
- Support private repositories only when the authenticated user has sufficient GitHub permission.

## Delivery approach

The application is planned as a native-feeling Windows desktop app, with a signed/packaged installer when release infrastructure is ready. The repository will contain source, tests, documentation, CI, build scripts, and release metadata. Generated binaries and user project backups do not belong in this source repository.

See:
- [Workspace architecture](docs/WORKSPACE-ARCHITECTURE.md)
- [Safety model](docs/SAFETY-MODEL.md)
- [Roadmap](docs/ROADMAP.md)
