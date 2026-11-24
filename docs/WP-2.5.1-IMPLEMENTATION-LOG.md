# WP-2.5.1 Implementation Log

Implementation branch: `work/wp-2-5-1-journal-clone-bootstrap`.
Base: `work/wp-2-4-windows-productization` @ `810cfde7b00e4b3dad8eb4361bc30375e3dce957`.
Merge policy: Draft PR into WP-2.4 only. No merge to `main`. Prior work-package branches and PRs #1–#6 are untouched.

## Implemented

- `ProjectOperationJournal` creates `project_operations` before `GetInterruptedAsync`, `GetAsync`, and `SetStateAsync` (Begin already did).
- `LibraryStartup` assigns the catalog even when recovery throws. Sensitive journal-backed commands stay limited; Clone does not.
- Clone CanExecute: library open + non-empty GitHub URL, with Persian `CloneDisabledReason` in the UI.
- Tests: fresh journal, empty SQLite, upgrade without data loss, recovery-does-not-block-catalog, Clone gate, scripted Clone registration, real public repository clone.

## Not claimed

- Windows CI green until a run on this branch head succeeds.
- Product Owner clean-machine installation.
- WP-2.5.2+ (chrome/theme, Fetch/Pull, Commit, Push, progress UI, LFS honesty, installer/portable split, user docs).
