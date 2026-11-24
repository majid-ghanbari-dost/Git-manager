# WP-2.2 Architecture Decision Record: Category Identity and Safe Moves

Status: Accepted for implementation (Product Owner approval recorded 2026-11-24).

## Decisions

1. A category has an immutable opaque ID. Its display name is mutable and unique under SQLite's case-insensitive comparison.
2. Projects reference categories by ID. The legacy category-name column remains during migration for compatibility with the current UI and path layout.
3. `Unclassified` is a protected system category. It cannot be renamed or deleted.
4. A category cannot be deleted while projects reference it. The user must explicitly move those projects first; deletion never cascades into project data.
5. Category rename is a physical workspace operation, not a database-only label edit, because category names are part of project container paths.
6. Moving a project must preserve the complete project container (repository, backups, notes), verify source and destination, reject collisions and reparse points, and update the catalog only after the filesystem move succeeds.
7. Multi-project category rename is a journaled batch operation. Each project move is individually recoverable; no project is deleted as rollback.
8. Category and lifecycle remain independent dimensions. WP-2.2 must not alter lifecycle semantics.
9. Existing catalogs are upgraded in place. Existing project IDs, repository paths, remotes, commits, and backups are preserved.

## Migration strategy

- Add `category_id` and system/order metadata to the existing categories table.
- Backfill one stable ID per existing category.
- Add `category_id` to projects and backfill it by matching the existing category name.
- Keep the old category text column until all dependent code has moved to ID-based access.
- Run migration in a SQLite transaction and make it safe to run repeatedly.

## Acceptance gates

- Migration preserves all existing projects and categories.
- Category IDs remain unchanged across rename and restart.
- Duplicate names are rejected case-insensitively.
- Protected category rules are enforced in the service layer.
- No category delete operation removes or moves project data.
- A failed or interrupted move never deletes either the source project or an existing destination.
- Tests cover collisions, rollback, interruption recovery, and concurrent operations.
