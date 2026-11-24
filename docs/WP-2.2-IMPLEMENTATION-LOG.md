# WP-2.2 Implementation Log

Product Owner approval: 2026-11-24.
Implementation branch: `work/wp-2-2-category-management`.
Base: `work/wp-2-1-recovery-journal`.
Merge policy: no merge to `main` or prior phase branches without separate approval.

## Completed in the current working slice

- Created an independent WP-2.2 branch.
- Recorded the architecture decision and safety gates.
- Added a stable category identity model.
- Added an idempotent in-place SQLite migration for category IDs, system flags, sort order, and project category references.
- Backfilled category IDs for existing categories and project records; legacy custom category names are preserved.
- Added category list/create/rename-empty/delete-empty service operations.
- Enforced case-insensitive unique names, reserved `Unclassified`, and blocked deletion or database-only rename of categories that still contain projects.
- Added Persian UI controls for safe empty-category management.
- Added unit tests for migration idempotence, stable IDs, CRUD, reserved-category protection, and non-empty-category safety.
- Updated CI artifact naming for the WP-2.2 handoff.

## Not yet complete

- Workspace-aware project move transaction (repository + backups + notes).
- Batch rename of a category containing projects.
- Journaled move recovery after process termination.
- Category reordering UI.
- Expanded tests for destination collisions, reparse points, rollback, interrupted moves, and concurrent moves.
- Final Windows CI pass, source handoff, manifest, and SHA-256 verification.

## Safety note

The current UI intentionally allows rename/delete only for empty categories. This is a temporary safe gate, not a claim that project moves are implemented. No database-only rename is allowed to orphan a project's physical folder.
