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

This work is not merged to `main`. Later 2.5.1 commits include mid-Clone error preserve (`f5ab0eb`), smoke wait bounds (`746592b`), and separate installer/portable artifacts (`bfe4ef3`). It is not a Product Owner clean-machine test.

## WP-2.5.2 on `work/wp-2-5-2-ui-theme`

- Persian UI refresh, custom title bar (`WindowStyle=None`), Light/Dark/System theme.
- Draft PR #8. Not merged.

## WP-2.5.3 on `work/wp-2-5-3-clone-ux`

- Clearer Clone workflow copy and title-bar alignment.
- Draft PR #9. Not merged.

## WP-2.5.4 on `work/wp-2-5-4-push-and-branding`

- Safe GitHub Push (current branch, GCM, no force-push) and vector brand mark.
- Installer labeled 2.5.4. Draft PR #10. Windows CI `45592390889` succeeded. Product Owner desktop launch crashed; `--ci-smoke` does not open `MainWindow`.

## WP-2.5.5 on `work/wp-2-5-5-git-workflow`

- Fetch (`--prune`), Pull (`--ff-only`, clean tree), local Commit with confirmation.
- Draft PR #11 @ `26b6101`. Windows CI `45593920567` succeeded. Installer metadata was still 2.5.4.

## WP-2.5.6 on `work/wp-2-5-6-runtime-stability`

- Startup log at `%LOCALAPPDATA%\GitManager\logs\startup.log` (independent of workspace).
- Dispatcher / AppDomain / TaskScheduler exception logging.
- Theme updates replace frozen `SolidColorBrush` resources instead of mutating `Color`.
- Window icon is a real `.ico` loaded from an embedded stream (no XAML pack URI); `DrawingImage` remains the in-window mark.
- `Loaded` errors are logged and do not take down the process unhandled.
- Window placement falls back to Manual 0,0 when screen metrics are unusable (production still uses CenterScreen in XAML).
- STA WPF test constructs `MainWindow`, `Show()`, waits for `Loaded`, and closes — **passed** on Windows CI `08e4f8b` / run `45633358477`.
- Installed smoke without `--ci-smoke` is implemented but **failed** on that same run. Exception text was not retrieved.
- CI `45647609619` (HEAD `5e49a47`) **did not start the job** (billing / spending limit). Empty `steps[]`, no artifacts. That is not a smoke, build, or install result.
- Evidence artifacts are named `…-{run_id}-{job.status}` and upload `if: always()` **when the job starts**. A refused job still cannot upload.
- Product version **2.5.6**. Separate installer and portable artifacts. SHA-256 for a **green** smoke run does not exist. **NOT READY** for Product Owner install.
- See [WP-2.5.6-DELIVERY-REPORT.md](WP-2.5.6-DELIVERY-REPORT.md).
- Do not merge until installed UI smoke (30s alive + `MainWindowLoaded` + restart) passes.

## Not yet release-ready

- Code signing and Product Owner clean-machine / interactive UI validation are pending.
- Submodule and Git LFS backup coverage is not implemented.
- Restore-from-backup still writes an unregistered folder and is not a lifecycle un-archive.
- Branch management and new GitHub repository creation are not implemented.

No work-package PR has been merged into `main`.
