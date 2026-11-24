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
- Restore runs only after manifest/hash verification and writes into a new isolated container.
- Restore overlays working files, restores deleted tracked-file state, retains the Git commit history, and rejects unsafe archive paths.
- Automated integration test covers backup, restore, untracked files, deleted tracked files, and SHA-256 tamper rejection.
- Windows CI builds, runs tests, and publishes a self-contained win-x64 portable output.

## WP-2.1 implementation on `work/wp-2-1-recovery-journal`

- Durable SQLite operation journal stored separately under Local AppData, with FULL synchronous mode and WAL.
- Operation state transitions: Prepared, Running, CommitPending, Completed, RolledBack, and RecoveryRequired.
- Journal integration for Import, Backup, and Restore.
- Per-operation in-process and cross-process file locks.
- Startup recovery checks the journal and acquires the same operation lock before touching staging paths, so another live Git Manager process is not interrupted.
- Recovery removes only recognized staging paths when the final destination does not exist; final destinations, unexpected paths, reparse points, and cleanup failures are preserved for manual review.
- Git process execution now supports cancellation and terminates the process tree when cancellation or timeout occurs.
- Added automated tests for safe interrupted-import cleanup, preservation of final destinations, recovery idempotence, and lock contention.

**Validation status:** implementation is committed to the isolated WP-2.1 branch. Windows CI build, tests, and publish validation are pending; this work is not yet declared complete.

## Not yet release-ready

- Category CRUD and lifecycle moves are not implemented.
- Publish, diff review, secret scanning, and push confirmation are not implemented.
- Submodule and Git LFS backup coverage is not implemented and must be reported/handled before claiming full fidelity.
- Installer, signing, and clean-machine validation are pending.

Backups currently protect repository history and ordinary working-tree files, but do not claim to preserve submodule contents, LFS payloads, or all filesystem metadata. Neither the foundation PR nor WP-2.1 branch has been merged into `main`.
