# Roadmap

## Phase 0 — Product and architecture baseline
- [x] Define central workspace and per-project isolation
- [x] Separate project category from lifecycle
- [x] Define import, backup, restore, and publish safety rules
- [x] Select .NET 10 LTS, WPF/MVVM, SQLite, and Git CLI

## Phase 1 — Foundation
- [x] Application shell and Persian RTL interface
- [x] First-run workspace setup and validation
- [x] SQLite catalog and initial category seed
- [x] Initial project inventory and selection
- [x] Category CRUD, project moves, populated-category rename, and ordering (WP-2.2)
- [x] Lifecycle management: Active / On-Hold / Archived physical moves, history, recovery (WP-2.3; not merged)
- [x] Windows installer, uninstaller, portable ZIP, and SHA-256 (WP-2.4; Windows runner smoke, not merged)
- [x] Recovery journal and interrupted-operation recovery (WP-2.1; Windows CI passed on the work branch)
- [x] CI build and unit tests passing on WP-2.1, WP-2.2, WP-2.3, and WP-2.4 work branches (not on `main`)

## Phase 2 — Repository acquisition
- [x] GitHub URL validation and canonical identity
- [x] Authentication through configured Git credential helper
- [x] Staged full-history clone and remote verification
- [x] Duplicate detection and no-overwrite destination handling
- [x] Journal bootstrap so first-run Clone is not blocked by a missing `project_operations` table (WP-2.5.1; not merged)
- [ ] Permission and repository metadata preview
- [ ] Explicit retry/resume workflow for interrupted clones

## Phase 3 — Preservation
- [x] Full-history Git bundle backup
- [x] Working-tree snapshot including tracked, untracked, and ignored files
- [x] SHA-256 manifest and backup integrity verification
- [x] Restore into a new, empty destination with path-traversal checks
- [x] Recovery journal and interrupted-backup recovery (WP-2.1)
- [ ] Explicit submodule and Git LFS coverage reporting

## Phase 4 — Safe publishing
- [ ] Local/remote status and diff review
- [ ] Secret scanning and ignore rules
- [ ] Commit creation and push
- [ ] Divergence detection and conflict workflow
- [ ] No force push by default

## Phase 5 — Quality and release
- [x] Windows CI build and portable self-contained publish workflow
- [ ] Expanded integration and UI tests
- [ ] Windows clean-machine smoke test (Product Owner)
- [x] Installer and uninstaller (WP-2.4 work-branch CI smoke; not signed)
- [x] SHA-256 checksums for installer, portable ZIP, and source handoff
- [ ] Release notes and signed artifacts
- [x] Source archive, manifest, and SHA-256 handoff on work-branch CI (not a `main` release)
