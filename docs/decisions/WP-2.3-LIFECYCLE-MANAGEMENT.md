# WP-2.3 ADR: Lifecycle moves and append-only events

Status: Implemented on `work/wp-2-3-lifecycle-management`.

1. Lifecycle is a physical move of the complete project container. Logical-only updates are forbidden.
2. Unknown lifecycle values throw; they never map to `04_Quarantine`.
3. Archive is not delete. Un-archive is a reverse move, not a backup restore.
4. Crash journal and catalog are different SQLite files. History is append-only in the catalog.
5. After `Directory.Move`, the catalog TX writes paths + `CatalogCommitted`. `Completed` is inserted only after the journal row is `Completed`.
6. `RecoveryRequired` is not a terminal unique result; a later recovery may append `Completed`.
7. Event rows are never updated or deleted. Idempotence uses `INSERT OR IGNORE` plus unique indexes.
8. Category and lifecycle remain independent. WP-2.2 path math keeps using `project.Lifecycle`.
