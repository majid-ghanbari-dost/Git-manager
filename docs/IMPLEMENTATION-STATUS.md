# Implementation status

## Implemented on `work/phase-1-foundation`

- Persian RTL WPF desktop shell.
- First-run workspace selection and persisted workspace path (Local AppData; no credentials).
- Workspace folder initialization and SQLite project catalog.
- GitHub HTTPS repository URL validation and canonical identity.
- Safe Git process invocation without shell interpolation.
- Full-history clone into a staging directory, remote verification, and no-overwrite destination handling.
- Initial project inventory with project selection.
- Per-project backup creation:
  - Full Git history and refs saved as a verified Git bundle.
  - Working-tree snapshot includes tracked, untracked, and ignored files.
  - The .git internals are excluded from the snapshot; Git history is retained in the bundle.
  - SHA-256 hashes are recorded in a JSON manifest and checked after creation.
  - Backup is staged and atomically moved into its final directory.
- Automated test covers backup contents and SHA-256 tamper detection.
- Windows CI builds, runs tests, and publishes a self-contained win-x64 portable output.

## Not yet release-ready

- Restore from backup into a new destination is not implemented.
- Recovery journal and crash recovery are not implemented.
- Category CRUD and lifecycle moves are not implemented.
- Publish, diff review, secret scanning, and push confirmation are not implemented.
- Submodule and Git LFS backup coverage is not implemented and must be reported/handled before claiming full fidelity.
- Installer, signing, and clean-machine validation are pending.

Backups currently protect repository history and ordinary working-tree files, but do not claim to preserve submodule contents, LFS payloads, or all filesystem metadata. The branch is intentionally not merged into `main`.
