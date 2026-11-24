# Implementation status

## Implemented on `work/phase-1-foundation`

- Persian RTL WPF desktop shell.
- First-run workspace selection and persisted workspace path (Local AppData; no credentials).
- Workspace folder initialization and SQLite project catalog.
- GitHub HTTPS repository URL validation and canonical identity.
- Safe Git process invocation without shell interpolation.
- Full-history clone into a staging directory, remote verification, and no-overwrite destination handling.
- Initial project inventory with project selection.
- Per-project backup creation (Git bundle + working-tree snapshot + SHA-256).
- Restore after manifest/hash verification into a new isolated container.
- Windows CI builds, runs tests, and publishes a self-contained win-x64 portable output.

## WP-2.1 on `work/wp-2-1-recovery-journal`

- Durable SQLite operation journal (Local AppData, WAL, `synchronous=FULL`).
- Journaled Import, Backup, and Restore with crash recovery and cross-process locks.
- Windows CI on head `66bb2cf` succeeded (17 tests). This work is **not** pending CI.

## WP-2.2 on `work/wp-2-2-category-management`

- Stable category IDs, idempotent catalog migration, Unclassified protection, CRUD, populated rename, ordering.
- Whole-container project moves with dedicated recovery.
- Windows CI on head `d66f204` succeeded (34 tests). Category CRUD **is implemented**.

## WP-2.3 on `work/wp-2-3-lifecycle-management`

- Physical lifecycle moves for Active / On-Hold / Archived of the complete project container.
- Fail-closed unknown lifecycle (never maps to `04_Quarantine`).
- Append-only lifecycle events: `CatalogCommitted` then `Completed` only after the crash journal is `Completed`.
- `RecoveryRequired` is intermediate; a later successful recovery may still append `Completed`.
- Missing / PathDrift probe; missing projects cannot transition.
- Persian UI: filter, search, archive/unarchive with confirmation, availability column.
- Idempotent catalog migration to schema 2.3 (blank lifecycle → Active; unknown non-empty values preserved).

Lifecycle is implemented on `work/wp-2-3-lifecycle-management`. It is not merged to `main`. Windows CI run `45361673077` on `f2ca734` succeeded. Unit tests plus Windows CI do **not** mean installer or production readiness.

## WP-2.4 on `work/wp-2-4-windows-productization`

- Self-contained win-x64 Release publish and portable ZIP.
- Inno Setup installer `GitManager-Setup-x64.exe` with Start Menu and Desktop shortcuts.
- Upgrade/repair via a stable AppId.
- Uninstall removes program files and shortcuts only; workspace and `%LOCALAPPDATA%\GitManager` are preserved.
- `--ci-smoke` headless launch; Windows CI smokes install, launch, repair, upgrade, and uninstall.
- SHA-256 hashes for installer, portable ZIP, and source handoff.

Windows CI run `45365248926` on `eb434a9` succeeded, including installer smoke. This is not a signed production release. It is not a Product Owner clean-machine installation test.

## WP-2.5.1 on `work/wp-2-5-1-journal-clone-bootstrap`

- Operation journal bootstraps `project_operations` before any read/write.
- Recovery failure no longer leaves the catalog unset; the library opens and journal-backed mutations stay limited with a Persian status. Exception details stay in the technical log.
- Clone enables when the library is open, the journal can record an operation, and a GitHub URL is present; an unusable journal (invalid file content or malformed SQLite pages) disables Clone with a Persian reason and does not create a partial project.
- Tests cover fresh install, empty SQLite, invalid non-SQLite journal file content, malformed SQLite interior pages (header preserved), upgrade without data loss, scripted Clone registration, a real public GitHub clone, and credential-boundary checks. Private GCM clone is reviewed in docs, not executed in CI.

This work is not merged to `main`. Review follow-up Windows CI run `45388706337` on `c90fff3` succeeded. Mid-Clone push CI `45399066199` on `f5ab0eb` succeeded; pull request CI `45399075128` failed when the hosted runner lost communication during installer smoke (unbounded `WaitForExit`). It is not a Product Owner clean-machine test.

## Not yet release-ready

- Publish, diff review, secret scanning, and push confirmation are not implemented.
- Submodule and Git LFS backup coverage is not implemented.
- Code signing and Product Owner clean-machine validation are pending.
- Restore-from-backup still writes an unregistered folder and is not a lifecycle un-archive.

Neither the foundation PR, WP-2.1, WP-2.2, WP-2.3, nor WP-2.4 has been merged into `main`.
