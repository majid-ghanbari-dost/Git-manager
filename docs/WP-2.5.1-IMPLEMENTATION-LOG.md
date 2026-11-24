# WP-2.5.1 Implementation Log

Implementation branch: `work/wp-2-5-1-journal-clone-bootstrap`.
Base: `work/wp-2-4-windows-productization` @ `810cfde7b00e4b3dad8eb4361bc30375e3dce957`.
Merge policy: Draft PR into WP-2.4 only. No merge to `main`. Prior work-package branches and PRs #1–#6 are untouched.

## Implemented

- `ProjectOperationJournal` creates `project_operations` before `GetInterruptedAsync`, `GetAsync`, and `SetStateAsync` (Begin already did).
- `LibraryStartup` assigns the catalog even when recovery throws. Sensitive journal-backed commands stay limited; Clone does not.
- Clone CanExecute: library open + non-empty GitHub URL, with Persian `CloneDisabledReason` in the UI.
- Tests: fresh journal, empty SQLite, upgrade without data loss, recovery-does-not-block-catalog, Clone gate, scripted Clone registration, real public repository clone.

## Release validation

Windows CI run [45386246393](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45386246393) on `a65cfe4ebb232e5a9aad6d6f68c6f7d88ae8ec82` succeeded: unit tests (including journal bootstrap, empty SQLite, upgrade without data loss, scripted Clone registration, and a real public `octocat/Hello-World` clone), self-contained publish, Inno Setup compile, portable ZIP, SHA-256, source handoff, and install/repair/upgrade/uninstall smoke. Pull request run [45386272862](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45386272862) on the same commit also succeeded. This is not a Product Owner clean-machine test and not a signed production release.

## Not claimed

- Product Owner clean-machine installation.
- WP-2.5.2+ (chrome/theme, Fetch/Pull, Commit, Push, progress UI, LFS honesty, installer/portable split, user docs).
