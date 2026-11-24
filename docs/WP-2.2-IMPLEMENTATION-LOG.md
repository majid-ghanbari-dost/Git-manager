# WP-2.2 Implementation Log

Product Owner approval: 2026-11-24.
Implementation branch: `work/wp-2-2-category-management`.
Base: `work/wp-2-1-recovery-journal`.
Merge policy: this PR remains Draft; no merge to `main` or prior phase branches without separate approval.

## Implemented

- Independent branch and architecture decision record.
- Stable category IDs and idempotent SQLite migration.
- Legacy project and custom category preservation; category IDs backfilled without deleting or replacing project records.
- Case-insensitive unique category names and protected `Unclassified` system category, pinned first.
- Create, list, rename, and safe deletion; populated-category rename preserves its category ID.
- Single-project moves that relocate the complete project container (repository, backups, notes) and update catalog paths transactionally.
- Journaled batch rename of populated categories with per-project move journal entries.
- Dedicated crash recovery for single moves and batch rename, coordinated through category and project locks.
- Fail-closed handling for destination collisions, path drift, ambiguous recovery state, and reparse points.
- Persistent category ordering and Persian UI controls for create, rename, delete, reorder, and moving a selected project.
- Unit tests for category CRUD, legacy SQLite migration, stable IDs, ordering, project moves, collisions, batch rename, rollback, and interrupted-operation recovery.
- CI handoff naming updated to WP-2.2.

## Release validation

Final Windows CI run 130 passed on the branch head: build, all 34 unit tests, portable Windows x64 publish, source handoff generation, and both artifact uploads. The handoff manifest contains 46 entries; all 46 hashes were independently verified. The inner handoff ZIP SHA-256 is `900705d20f6557cc7376fc740bf8a50aec28f2d3c4a96d033749b0a17be6f0d9`.

## Explicitly out of scope

- Lifecycle management (Active / On-Hold / Archived) remains a separate work package.
- No operation in WP-2.2 deletes project data as a side effect of category deletion.
- No merge is performed by this implementation workflow.
