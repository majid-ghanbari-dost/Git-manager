# WP-2.3 Implementation Log

Implementation branch: `work/wp-2-3-lifecycle-management`.
Base: `work/wp-2-2-category-management` @ `d66f2049d8bf850a7cf4679b097531438b202e20`.
Merge policy: Draft PR into WP-2.2 only. No merge to `main`. PRs #1–#3 untouched.

## Implemented

- Fail-closed `WorkspaceService.GetProjectContainer` for unknown lifecycle.
- Idempotent catalog migration to schema 2.3 with append-only `project_lifecycle_events`.
- Physical whole-container moves for Active / On-Hold / Archived.
- Confirmation for archive, un-archive, and dirty worktrees; `index.lock` blocks.
- Missing / PathDrift probe; transitions denied.
- Journal type `LifecycleMove`; dedicated recovery; generic cleaner skips it.
- Events: `CatalogCommitted` in the catalog TX after `Directory.Move`; `Completed` only after journal `Completed`.
- `RecoveryRequired` is intermediate and does not block a later `Completed`.
- Persian UI filter, search, availability, archive/restore actions.
- Automated tests for the transition matrix, recovery windows, and event consistency.
- A same-lifecycle retry completes a leftover `CommitPending` journal row via recovery.

## Release validation

Windows CI run [45361673077](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45361673077) on `f2ca734` succeeded: build, unit tests, portable win-x64 publish, WP-2.3 source handoff, and both artifact uploads. This is not installer or production readiness.

## Known limitations

- No installer/uninstaller.
- Restore-from-backup still creates an unregistered folder.
- Submodule / Git LFS fidelity unchanged.
- Quarantine is not a user lifecycle.
