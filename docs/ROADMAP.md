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
- [ ] Category CRUD and lifecycle management
- [ ] Recovery journal and interrupted-operation recovery
- [ ] CI build and unit tests passing

## Phase 2 — Repository acquisition
- [x] GitHub URL validation and canonical identity
- [x] Authentication through configured Git credential helper
- [x] Staged full-history clone and remote verification
- [x] Duplicate detection and no-overwrite destination handling
- [ ] Permission and repository metadata preview
- [ ] Explicit retry/resume workflow for interrupted clones

## Phase 3 — Preservation
- [x] Full-history Git bundle backup
- [x] Working-tree snapshot including tracked, untracked, and ignored files
- [x] SHA-256 manifest and backup integrity verification
- [ ] Restore into a new, empty destination
- [ ] Recovery journal and interrupted-backup recovery
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
- [ ] Windows clean-machine smoke test
- [ ] Installer and uninstaller
- [ ] Release notes, checksums, and signed artifacts where feasible
- [ ] Full source archive, manifest, and SHA-256 handoff
