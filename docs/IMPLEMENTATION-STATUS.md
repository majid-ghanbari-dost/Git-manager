# Implementation status

## Phase 1 foundation — in progress

Implemented on branch `work/phase-1-foundation`:

- WPF desktop shell with Persian right-to-left layout.
- First-run workspace selection and persisted workspace path (Local AppData; no credentials).
- Workspace folder initialization.
- SQLite project catalog and starter category seed.
- GitHub HTTPS repository URL validation and canonical identity.
- Safe process invocation through Git's argument list (no shell interpolation).
- Full-history clone into a staging directory, remote verification, and no-overwrite destination handling.
- Initial project inventory grid.
- Windows CI build and portable self-contained publish workflow.
- Unit tests for repository URL parsing and Windows path normalization.

## Known limitations — not yet release-ready

- Backup and restore engines are not implemented.
- Recovery journal and crash recovery are not implemented.
- Category CRUD and lifecycle moves are not implemented; initial categories are seeded.
- Publish, diff review, secret scanning, and push confirmation are not implemented.
- Submodule and Git LFS backup coverage is not implemented.
- Installer, signing, and clean-machine validation are pending.
- CI must pass before this phase is accepted.

The branch is intentionally not merged into `main`. Do not use this build as a production backup or publishing tool.
