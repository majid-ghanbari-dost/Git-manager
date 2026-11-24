# Roadmap

## Phase 0 — Product and architecture baseline
- [x] Define central workspace and per-project isolation
- [x] Separate project category from lifecycle
- [x] Define import, backup, restore, and publish safety rules
- [ ] Confirm supported Windows baseline and UI framework

## Phase 1 — Foundation
- [ ] Application shell and Persian RTL interface
- [ ] First-run workspace setup and validation
- [ ] SQLite catalog and migrations
- [ ] Category and lifecycle management
- [ ] Project inventory and search

## Phase 2 — Repository acquisition
- [ ] GitHub URL validation and canonical identity
- [ ] Authentication through Git Credential Manager
- [ ] Permission and repository metadata check
- [ ] Transactional clone through staging
- [ ] Duplicate detection and safe re-import

## Phase 3 — Preservation
- [ ] Full-history Git bundle backup
- [ ] Working-tree snapshot for uncommitted/untracked files
- [ ] Manifest and SHA-256 generation/verification
- [ ] Backup retention and restore to a new location
- [ ] Explicit submodule and Git LFS coverage reporting

## Phase 4 — Safe publishing
- [ ] Local/remote status and diff review
- [ ] Secret scanning and ignore rules
- [ ] Commit creation and push
- [ ] Divergence detection and conflict workflow
- [ ] No force push by default

## Phase 5 — Quality and release
- [ ] Unit and integration tests
- [ ] Windows clean-machine smoke test
- [ ] CI build and test workflow
- [ ] Installer and uninstaller
- [ ] Release notes, checksums, and signed artifacts where feasible
- [ ] Full source archive, manifest, and SHA-256 handoff
