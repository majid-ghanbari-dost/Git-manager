# WP-2.3 Technical Challenge — Full Repository Audit, Architecture, and Execution Blueprint

**Document type:** Architecture audit + implementation blueprint (no code, no applied migration)  
**Date:** 2026-11-24  
**Auditor role:** Senior Software Architect / Principal Engineer / QA Architect  
**Repository:** https://github.com/majid-ghanbari-dost/Git-manager  
**This document’s branch:** `arena/9d4846eb-git-manager` (based on `main` @ `6a78326b8cac54aca01b326f82ccba2dcbff8a2e`)  
**Authoritative implementation head for design:** `work/wp-2-2-category-management` @ `d66f2049d8bf850a7cf4679b097531438b202e20`

This mission is audit, analysis, and design only. No WP-2.3 code is implemented here. No migration is created or applied. No PR is merged or edited. `main` is not modified.

**GitHub delivery of this file:** this document is committed on `arena/9d4846eb-git-manager` and opened as a documentation PR against `main`. It is **intentionally not** on `work/wp-2-2-category-management`. Rewriting WP-2.2 was forbidden. Anyone looking for this file on the WP-2.2 branch will not find it; that is policy, not a missing deliverable.

All line numbers below refer to tree `d66f2049d8bf850a7cf4679b097531438b202e20` (`work/wp-2-2-category-management`) unless noted.

---

## EV. Evidence Verification Pack (audit completion)

This section is the response to the completion review. It does **not** change `docs/IMPLEMENTATION-STATUS.md` or `docs/ROADMAP.md`. It reports their drift. It does not change product code.

### EV.1 Why this file is not on the WP-2.2 branch

| Constraint | Effect |
| --- | --- |
| “هیچ شاخه موجودی بازنویسی نشود” | Cannot add a commit to `work/wp-2-2-category-management` |
| “هیچ PR ادغام نشود” | Cannot land the file via merging PR #3 |
| Current session branch | `arena/9d4846eb-git-manager` from `main` @ `6a78326` |

The implementation baseline remains WP-2.2 @ `d66f204`. This audit document is a sibling deliverable on the Arena branch, visible on GitHub through that branch / its documentation PR.

### EV.2 Documentation drift — `IMPLEMENTATION-STATUS.md` and `ROADMAP.md` (not modified)

Source of both files: WP-2.2 head `d66f204`, paths `docs/IMPLEMENTATION-STATUS.md` and `docs/ROADMAP.md`. These files were **not** edited in this audit.

#### EV.2.1 `docs/IMPLEMENTATION-STATUS.md` vs source/CI

| Line | File claim | Actual evidence | Drift |
| --- | --- | --- | --- |
| 34 | “Windows CI build, tests, and publish validation are **pending**; this work is not yet declared complete.” (WP-2.1 section) | PR #2 body cites run [`45344878055`](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45344878055) SUCCESS on `66bb2cf`; 17 tests passed; portable + handoff uploaded | **Stale.** WP-2.1 CI is green on head. |
| 38 | “Category CRUD and lifecycle moves are **not implemented**.” | Category CRUD **is** implemented: `CategoryManagementService` (31 lines), `ProjectCategoryMoveService.MoveProjectCoreAsync` L25–L78, tests `CategoryManagementTests` (8 facts), `ProjectCategoryMoveTests` (4 facts), `CategoryRenameTests` (5 facts). Lifecycle moves are **not** implemented (true). | **Half-stale.** Bundles two different facts. Category CRUD is done on WP-2.2; lifecycle is not. |
| 3–21 | Phase 1 list is accurate for `work/phase-1-foundation` | Matches `91f189b` source + CI `45339388608` | Accurate |
| 23–32 | WP-2.1 journal/lock/recovery list | Matches `ProjectOperationJournal` L38–L49, `ProjectOperationLock.AcquireAsync` L20–L50, `ProjectOperationRecoveryService.RecoverAsync` | Accurate as implementation description |
| 43 | “Neither the foundation PR nor WP-2.1 branch has been merged into `main`.” | PRs #1/#2/#3 all `state=OPEN`, `isDraft=true` | Accurate |
| — | File has **no WP-2.2 section** | WP-2.2 has 80 commits, ADR, implementation log, 34 tests, CI `45352646837` | **Omission.** Status file was not updated when WP-2.2 landed on its branch. |

Authoritative WP-2.2 status is instead in:

- `docs/WP-2.2-IMPLEMENTATION-LOG.md` L8–L31 (implemented + out of scope)
- `docs/decisions/WP-2.2-CATEGORY-MANAGEMENT.md` L7–L14 (ADR, including “WP-2.2 must not alter lifecycle semantics” at L14)
- `docs/ROADMAP.md` L14 (claims category CRUD done)

#### EV.2.2 `docs/ROADMAP.md` vs source/CI

| Line | Checkbox | Claim | Actual | Drift |
| --- | --- | --- | --- | --- |
| 14 | `[x]` | “Category CRUD, project moves, populated-category rename, and ordering (WP-2.2; lifecycle management remains separate)” | Source + 17 category/move/rename tests + CI 34 passed | **Accurate** for WP-2.2 branch. Contradicts `IMPLEMENTATION-STATUS.md` L38. |
| 15 | `[x]` | “Recovery journal … (WP-2.1 implementation; **CI validation pending**)” | WP-2.1 CI SUCCESS `45344878055`, 17 tests | Checkbox done is fair; parenthetical **CI pending is stale**. |
| 16 | `[ ]` | “CI build and unit tests passing” | Phase 1 CI green; WP-2.1 17/17; WP-2.2 34/34; workflow exists on work branches. `main` has no workflow and no tests. | **Ambiguous.** True for work branches, false for `main`. Leaving it unchecked is conservative if “passing” means on `main`. |
| 31 | `[ ]` | “Recovery journal and interrupted-backup recovery” | `ProjectBackupService.CreateBackupAsync` journals Backup L35–L68; `ProjectOperationRecoveryService` recovers Backup staging when final is absent (`IsSafeStagingPath` Backup arm). Test `Recovery_NeverDeletesStagingWhenFinalDestinationExists` L39–L66. | **Stale unchecked.** Interrupted-backup recovery **is** implemented on WP-2.1/2.2. Submodule/LFS reporting (L32) is still genuinely open. |
| 42 | `[x]` | “Windows CI build and portable self-contained publish workflow” | `.github/workflows/ci.yml` on WP-2.2; artifact `git-manager-win-x64` 61,819,133 B on run `45352646837` | Accurate for work branches |
| 47 | `[ ]` | “Full source archive, manifest, and SHA-256 handoff” | WP-2.1 and WP-2.2 CI already produce `GitManager-*-Handoff.zip` + `.sha256`. PR #3 body lists verified hashes. | **Stale unchecked** if interpreted as “CI handoff exists”. Fair if interpreted as “release on `main`”. |

**Net:** ROADMAP over-claims CI-pending on WP-2.1 and under-claims backup recovery and handoff. IMPLEMENTATION-STATUS under-claims WP-2.2 category work and WP-2.1 CI. Neither file was changed in this audit.

### EV.3 `WorkspaceService.GetProjectContainer` — unknown lifecycle → `04_Quarantine`

**File:** `src/GitManager.App/Services/WorkspaceService.cs`  
**Method:** `GetProjectContainer`  
**Lines:** 13–27  
**Head:** `d66f204`

```csharp
public string GetProjectContainer(string root, string lifecycle, string category, string owner, string repository)
{
    var lifecycleFolder = lifecycle switch
    {
        "Active" => "01_Active",       // L17
        "On-Hold" => "02_On-Hold",     // L18
        "Archived" => "03_Archived",   // L19
        _ => "04_Quarantine"           // L20  FAIL-OPEN
    };
```

Callers that pass `project.Lifecycle` into this function (so a garbage value would be quarantined):

| Caller | Method | Lines | What it does with the path |
| --- | --- | --- | --- |
| `ProjectCategoryMoveService.MoveProjectCoreAsync` | destination + expectedSource | L40–L43 | Identity check; mismatch throws path-drift at L44–L48 |
| `CategoryRenameService.RenameAsync` rollback | expected new/old paths | L75–L76, L95–L98 | Decides which projects to roll back |
| `CategoryRenameRecoveryService.RecoverAsync` | expected/oldPath | L82–L85 | Decides Completed vs RecoveryRequired |
| `RepositoryImportService.CloneAsync` | destination | L41 | Hard-codes `"Active"` — not affected by garbage lifecycle |

`Initialize` L8–L10 still **creates** `04_Quarantine` as an empty top-level folder. That is fine. Using it as the default for unknown strings is not.

#### Why this must become explicit validation and rejection

1. **Silent data placement.** A typo (`"archive"`, `"archived"`, `"Hold"`, `"active "`) does not throw. The project container is computed under `04_Quarantine/...`. No UI, no journal type, no recovery path exists for Quarantine.
2. **Breaks WP-2.2 identity.** `MoveProjectCoreAsync` L42–L45 requires `container_path == GetProjectContainer(..., project.Lifecycle, ...)`. If lifecycle in SQLite is `"Archived"` but the folder was created when a previous bug wrote `"Active"`, the move is aborted. If lifecycle is garbage, expectedSource becomes a Quarantine path that does not exist → `DirectoryNotFoundException` L49 or path-drift L48. Operators cannot tell “bad enum” from “missing disk”.
3. **Quarantine is a specified inspection area, not a dump.** `docs/WORKSPACE-ARCHITECTURE.md` on `main` describes `04_Quarantine` as “Repositories needing inspection; never auto-run”. Auto-routing unknown states there is the opposite of inspection.
4. **WP-2.3 archive/restore would inherit the hole.** A lifecycle service that called `GetProjectContainer(root, userInput, ...)` would, on any unexpected string, write into Quarantine without confirmation. That is an unlogged, unjournaled move target.
5. **Fail-closed is already the house style.** Destination exists → throw (`MoveProjectCoreAsync` L51–L52). Reparse → throw (L53–L56). Unknown category → throw (L33–L34). Unknown lifecycle is the only dimension that fail-opens.

**Required WP-2.3 change (design, not implemented here):** replace `_ => "04_Quarantine"` with `throw new InvalidOperationException` naming the bad value. Keep creating the `04_Quarantine` folder in `Initialize` L9. Do not add a user transition into Quarantine in WP-2.3.

No current test covers the default arm. `PathSafetyTests` and category tests never pass a garbage lifecycle.

### EV.4 Complete transition matrix (Active / On-Hold / Archived)

User-facing states: only these three. Quarantine is not a user transition.

Preconditions that apply to **every** Allow/Confirm cell (else Deny, no journal row, no disk change):

- `availability == Available`
- `container_path` equals `GetProjectContainer(root, from, category, owner, repo)`
- `repository_path == container\repository` and both exist
- destination path does not exist as file or directory
- no reparse point on source tree or dest ancestors
- no `.git/index.lock`
- category lock + project lock acquired
- `canonical_remote_url` unchanged; no second catalog row

| From → To | Decision | Confirmations | Filesystem | Catalog | Journal | Recovery if crash |
| --- | --- | --- | --- | --- | --- | --- |
| Active → Active | **No-op** | none | unchanged | unchanged | none | n/a |
| Active → On-Hold | **Allow** | dirty-worktree if dirty | move whole container `01_Active/...` → `02_On-Hold/...` | `lifecycle='On-Hold'`, paths updated, event appended | `LifecycleMove` Prepared→Running→Completed | see EV.5 R-table |
| Active → Archived | **Confirm** | archive + dirty if dirty | move whole container `01_Active/...` → `03_Archived/...` | `lifecycle='Archived'`, paths, event | same | same |
| On-Hold → Active | **Allow** | dirty if dirty | `02_On-Hold` → `01_Active` | `lifecycle='Active'` | same | same |
| On-Hold → On-Hold | **No-op** | none | unchanged | unchanged | none | n/a |
| On-Hold → Archived | **Confirm** | archive + dirty if dirty | `02_On-Hold` → `03_Archived` | `lifecycle='Archived'` | same | same |
| Archived → Active | **Confirm** | restore-from-archive (+ dirty if dirty) | `03_Archived` → `01_Active` | `lifecycle='Active'` | same | same |
| Archived → On-Hold | **Confirm** | restore-from-archive (+ dirty if dirty) | `03_Archived` → `02_On-Hold` | `lifecycle='On-Hold'` | same | same |
| Archived → Archived | **No-op** | none | unchanged | unchanged | none | n/a |
| any → Quarantine | **Deny** | — | no | no | no | n/a |
| any → unknown string | **Deny** | — | no | no | no | n/a |
| Missing / PathDrift → any | **Deny** | — | no | no | no | n/a |

“Restore” in this matrix is **un-archive** (reverse lifecycle move), not `ProjectRestoreService.RestoreToNewContainerAsync` (backup restore). Backup restore stays a separate command and still writes an unregistered `Restored-*` folder (`ProjectRestoreService.cs` L38–L64).

### EV.5 Per-transition: filesystem, database, journal, recovery

All six real transitions share one algorithm. Only the path prefix and confirm flags differ.

**Filesystem (happy path)** — modeled on `ProjectCategoryMoveService.MoveProjectCoreAsync` L70–L71, which already does `Directory.CreateDirectory(parent)` + `Directory.Move(source, destination)` of the **entire container**. Test proof that notes/backups/repository travel together: `ProjectCategoryMoveTests.MoveProject_MovesWholeContainerAndUpdatesCatalog` L19–L36 (`notes/keep.txt` survives; source directory gone; `repository_path` retargeted).

| Step | FS | DB (`projects` + `project_lifecycle_events`) | Journal (`project_operations`) |
| --- | --- | --- | --- |
| 0 preflight fail | unchanged | unchanged; optional Rejected event if we choose to record denies (not required) | none |
| 1 begin | unchanged | unchanged | INSERT type=`LifecycleMove`, staging=source, final=dest, state=`Prepared` |
| 2 running | unchanged | unchanged | state=`Running` |
| 3 `Directory.Move` | source gone, dest exists, inner `repository/`, `backups/`, `notes/` intact, `.git` intact, dirty files intact | unchanged | `Running` |
| 4 catalog commit | dest exists | `lifecycle` = target; `container_path`/`repository_path` = dest; `lifecycle_changed_at_utc` now; event Result=`Completed` | still `Running` (crash window) |
| 5 journal complete | dest exists | same | `Completed` |
| 6 prune empty parents | may remove empty owner/category dirs under the **source** lifecycle folder only; never `01_Active` itself | unchanged | unchanged |

**Crash / recovery** (dedicated `LifecycleMoveRecoveryService`; generic cleaner **must skip** this type, same pattern as L27 of `ProjectOperationRecoveryService`: `if (operation.OperationType is "CategoryMove" or "CategoryRename") continue;` — WP-2.3 adds `"LifecycleMove"` to that set).

| Id | Source dir | Dest dir | Catalog container | Recovery action | Journal |
| --- | --- | --- | --- | --- | --- |
| R1 | exists | absent | source | none on disk | `RolledBack` |
| R2 | exists | absent | dest | **do not move** | `RecoveryRequired` |
| R3 | absent | exists | dest | none on disk | `Completed` |
| R4 | absent | exists | source | `Directory.Move(dest, source)` | `RolledBack` or `RecoveryRequired` if move fails |
| R5 | absent | exists | other | none | `RecoveryRequired` |
| R6 | exists | exists | any | **delete nothing** | `RecoveryRequired` |
| R7 | absent | absent | any | **create nothing** | `RecoveryRequired` |
| R8 | lock held | — | — | skip this pass | unchanged |
| R9 | reparse / outside workspace | — | — | none | `RecoveryRequired` |

This is isomorphic to `CategoryMoveRecoveryService.RecoverAsync` L72–L147 (source-only + catalog match → RolledBack; dest-only + catalog dest → Completed; dest-only + catalog source → move back; else RecoveryRequired). Tests: `ProjectCategoryMoveTests.Recovery_RollsBackMoveWhenCatalogStillPointsToSource` L69–L98 and `Recovery_CompletesMoveWhenCatalogAlreadyPointsToDestination` L101–L131.

### EV.6 Required failure scenarios

#### EV.6.1 Uncommitted / untracked changes

| Today | WP-2.3 rule |
| --- | --- |
| Category `Directory.Move` L71 moves the worktree as-is. No `git stash`, `reset`, or `clean`. There is **no** dirty preflight in WP-2.2. | Same move. Add preflight `git status --porcelain` + refuse if `.git/index.lock` exists. Dirty is **Confirm**, not Deny. Files must still be present at dest (assert in new test, cloned from L19–L31 notes marker test). |

Evidence that a whole-container move preserves extra files: test L19–L31 writes `notes/keep.txt` outside git and asserts it at destination. Uncommitted files under `repository/` are the same class of bytes on disk.

#### EV.6.2 Destination already exists

Today: `MoveProjectCoreAsync` L51–L52 throws `IOException` “پوشه مقصد از قبل وجود دارد؛ هیچ فایلی بازنویسی نشد.” Test `MoveProject_RejectsExistingDestinationWithoutTouchingEitherProject` L41–L66: source still exists, dest `existing.txt` untouched, catalog category still `Unclassified`.

WP-2.3: identical Deny. No journal Completed. No catalog lifecycle change.

Collision example: Active `01_Active/Kandoo/owner/repo` cannot archive if `03_Archived/Kandoo/owner/repo` already exists.

#### EV.6.3 Missing path

Today: L49–L50 `DirectoryNotFoundException` “پوشه پروژه یا مخزن Git در مسیر ثبت‌شده پیدا نشد.” No availability column. Inventory still lists the row (`GetProjectsAsync` L298–L325 reads SQLite only).

WP-2.3: probe sets `availability='Missing'`; transition matrix Deny; no journal; no create-destination.

#### EV.6.4 Power loss / forced close

Today (category, proven):

- Crash after `Directory.Move` before catalog: recovery R4 analog, test `CategoryRenameTests.Recovery_RollsBackMoveThatCrashedBeforeCatalogCommit` L120–L157.
- Crash after catalog before journal Completed: recovery marks Completed, test `Recovery_CompletesMoveWhenCatalogAlreadyPointsToDestination` L101–L131.
- Generic cleaner does **not** delete a path that looks like a final dest: `Recovery_NeverDeletesStagingWhenFinalDestinationExists` L39–L66; and category types are skipped at `ProjectOperationRecoveryService` L27.

WP-2.3: same windows with type `LifecycleMove`. **P0 test:** generic cleaner given a `LifecycleMove` whose staging_path is `01_Active/...` must not `Directory.Delete`.

Journal durability: `PRAGMA synchronous=FULL` + WAL at `ProjectOperationJournal.InitializeAsync` L36–L37. DB path `%LocalAppData%\GitManager\operations.sqlite3` L23–L25. Interrupted states selected by `GetInterruptedAsync` L126: `Prepared`, `Running`, `CommitPending`, `RecoveryRequired`.

Startup order today (`MainViewModel.SetWorkspaceAsync` L124–L126): CategoryRename recovery → CategoryMove recovery → generic recovery. WP-2.3 inserts LifecycleMove recovery **between** CategoryMove and generic.

#### EV.6.5 Two app instances at once

Lock implementation: `ProjectOperationLock.AcquireAsync` L20–L50.

- In-process: static `ConcurrentDictionary` + `SemaphoreSlim` L26–L29.
- Cross-process: `FileStream(..., FileShare.None)` on `%LocalAppData%\GitManager\operation-locks\<sha256>.lock` L34–L46. Timeout → `TimeoutException` “قفل بین‌پردازه‌ای پروژه در مهلت مجاز آزاد نشد.”

Test: `OperationLock_RejectsConcurrentUseOfSameKey` L75–L85.

Recovery respects live locks: `ProjectOperationRecoveryService` L33–L41 acquires with 100 ms timeout and `continue`s on `TimeoutException` (“A live process still owns this operation. Never recover its staging directory.”). `CategoryMoveRecoveryService` L54–L60 same for `project:` key.

WP-2.3 lifecycle move must take:

1. `category:<categoryId>` (serialize with WP-2.2 rename/move — see EV.7)
2. `project:<projectId>`

Second instance attempting the same project waits then times out; it must not start a second `Directory.Move`. Recovery in the second instance must skip the in-flight op.

External Git/IDE processes are **not** covered by this lock (PR #2 body already states this limitation). File locks from VS show up as `Directory.Move` IOException → RolledBack/RecoveryRequired, not as data loss.

### EV.7 Proof that lifecycle transitions do not collide with WP-2.2

#### EV.7.1 Category move keeps lifecycle, so it cannot substitute for archive

`MoveProjectCoreAsync` L40–L43:

```text
destination    = GetProjectContainer(root, project.Lifecycle, newCategoryName, ...)
expectedSource = GetProjectContainer(root, project.Lifecycle, oldCategoryName, ...)
```

Both calls pass **the same** `project.Lifecycle`. Category move never changes the top-level `01_Active` / `02_On-Hold` / `03_Archived` folder.

Catalog write `UpdateProjectCategoryAndPathAsync` L383–L386:

```sql
UPDATE projects
SET category_id = $categoryId, category = $category,
    container_path = $container, repository_path = $repository
WHERE project_id = $projectId
```

**No `lifecycle` column.** WP-2.2 cannot accidentally archive. ADR L14: “Category and lifecycle remain independent dimensions. WP-2.2 must not alter lifecycle semantics.” Implementation log L29: lifecycle is a separate work package.

#### EV.7.2 Category rename also keys paths by `project.Lifecycle`

`CategoryRenameService` L75–L76, L95–L98 and `CategoryRenameRecoveryService` L82–L85 all call `GetProjectContainer(..., project.Lifecycle, ...)`. If WP-2.3 left an Archived project physically in `01_Active` while setting `lifecycle='Archived'`, these checks would compute `03_Archived/...` as expectedSource, disagree with `container_path`, and throw path-drift (`MoveProjectCoreAsync` L44–L48) or mark RecoveryRequired. **Therefore WP-2.3 must physically move**, or it breaks WP-2.2.

Conversely, if WP-2.3 physically moves and updates both `lifecycle` and paths in one catalog transaction (same order as category: disk first, catalog second), WP-2.2’s expectedSource formula stays true.

#### EV.7.3 Lock interleaving

| Op | Locks (order) | Evidence |
| --- | --- | --- |
| Category move | categories (sorted) then `project:` | `AcquireCategoryLocksAsync` L109–L118; project L62 |
| Category rename | `category:` then per-project via mover (`categoryLockAlreadyHeld: true` L23, L37) | `CategoryRenameService` L37; mover L59–L61 |
| Lifecycle move (design) | `category:` (the project’s one category) then `project:` | same keys, same sort rule if two categories ever needed |

Deadlock avoidance: category ids locked in `OrderBy(x => x, StringComparer.Ordinal)` L116. Lifecycle move locks a single category id, which is a subset. Timeout on contention, no override.

#### EV.7.4 Recovery interleaving

Generic cleaner already skips CategoryMove/CategoryRename (L27) so it cannot `Directory.Delete` a real project tree. LifecycleMove must be added to that skip set. Startup order: rename → category move → **lifecycle move** → generic. Rename first because it may still reverse in-flight category path changes that lifecycle recovery would misread as PathDrift.

#### EV.7.5 Import duplicate still unique across lifecycles

`RepositoryImportService.CloneAsync` L36–L39 rejects any existing `canonical_remote_url` (case-insensitive in memory) **before** looking at lifecycle. An Archived project cannot be cloned a second time into Active. SQLite `UNIQUE` on `canonical_remote_url` (`ProjectCatalog` L34) is the second line. WP-2.3 only needs a better error string that includes the current lifecycle and path.

### EV.8 Archive and un-archive must move the whole container

**Yes. Entire container: `repository/`, `backups/`, `notes/`, and any other siblings (uncommitted files live inside `repository/`).**

Evidence this is already the product rule for the only existing physical move:

| Evidence | Where |
| --- | --- |
| Architecture | `docs/WORKSPACE-ARCHITECTURE.md` (main and WP-2.2): “A lifecycle move relocates the project container atomically where possible; it does not alter the Git remote, branches, tags, or commit history.” |
| Safety model | `docs/SAFETY-MODEL.md`: “Moving a project between lifecycle folders must not delete its backups.” |
| ADR | `docs/decisions/WP-2.2-CATEGORY-MANAGEMENT.md` L12 decision 6: “Moving a project must preserve the complete project container (repository, backups, notes)” |
| Implementation log | L15: “relocate the complete project container (repository, backups, notes)” |
| Code | `Directory.Move(source, destination)` where `source` is `project.ContainerPath`, not `RepositoryPath` — `MoveProjectCoreAsync` L39, L71 |
| Test | `MoveProject_MovesWholeContainerAndUpdatesCatalog` L19–L36 |

Forbidden archive implementations (would fail WP-2.3):

- Update `lifecycle` only (logical archive) — breaks EV.7.2
- `git bundle` + delete worktree
- Move only `repository/` and leave `backups/` in Active
- Copy then delete without journal (two live trees, split-brain)
- Leave original and copy to Archived

Un-archive is the same `Directory.Move` in reverse, not `ProjectRestoreService` (that clones a bundle into a **new** unregistered folder, L38–L64).

### EV.9 Claim → file → method → line → test/CI index

| # | Claim | File | Method | Lines | Test / CI |
| --- | --- | --- | --- | --- | --- |
| 1 | Unknown lifecycle maps to Quarantine | `WorkspaceService.cs` | `GetProjectContainer` | L15–L20 | **No test** (gap) |
| 2 | Workspace creates all lifecycle roots | `WorkspaceService.cs` | `Initialize` | L8–L10 | none dedicated |
| 3 | Lifecycle column exists, default Active | `ProjectRecord.cs` / `ProjectCatalog.cs` | field / `CREATE TABLE` | L12 / L39 | import always Active |
| 4 | Import hard-codes Active | `RepositoryImportService.cs` | `CloneAsync` | L41, L54 | no lifecycle test |
| 5 | UI shows lifecycle, no change control | `MainWindow.xaml` | DataGrid column | L84 | none |
| 6 | Category move uses lifecycle in path math | `ProjectCategoryMoveService.cs` | `MoveProjectCoreAsync` | L40–L43 | `MoveProject_MovesWholeContainerAndUpdatesCatalog` L10 |
| 7 | Category catalog update does not touch lifecycle | `ProjectCatalog.cs` | `UpdateProjectCategoryAndPathAsync` | L383–L386 | move tests assert category, not lifecycle |
| 8 | Whole container moved, not repo only | `ProjectCategoryMoveService.cs` | `Directory.Move(source, destination)` | L39, L71 | L19–L36 notes marker |
| 9 | Dest collision fail-closed | same | L51–L52 | `MoveProject_RejectsExistingDestinationWithoutTouchingEitherProject` L41 |
| 10 | Missing folder throws, no auto-create | same | L49–L50 | no dedicated Missing test (gap for WP-2.3) |
| 11 | Category types skipped by generic recovery | `ProjectOperationRecoveryService.cs` | `RecoverAsync` | L27 | implied by category recovery tests still seeing rows until dedicated service runs |
| 12 | Final dest never auto-deleted | same | L47–L54 | `Recovery_NeverDeletesStagingWhenFinalDestinationExists` L39 |
| 13 | Cross-process lock | `ProjectOperationLock.cs` | `AcquireAsync` | L20–L46 | `OperationLock_RejectsConcurrentUseOfSameKey` L75 |
| 14 | Recovery skips live lock | `ProjectOperationRecoveryService.cs` | `RecoverAsync` | L33–L41 | lock test + comment L38 |
| 15 | Journal in LocalAppData, FULL sync | `ProjectOperationJournal.cs` | ctor / `InitializeAsync` | L23–L25, L36–L37 | WP-2.1 PR #2 |
| 16 | Interrupted states | same | `GetInterruptedAsync` | L126 | recovery tests |
| 17 | Startup recovery order | `MainViewModel.cs` | `SetWorkspaceAsync` | L124–L126 | none (manual) |
| 18 | Duplicate URL rejected regardless of lifecycle | `RepositoryImportService.cs` | `CloneAsync` | L36–L39 | unique L34 schema |
| 19 | Category rename path uses lifecycle | `CategoryRenameService.cs` | `RenameAsync` | L75–L76, L95–L98 | `CategoryRenameTests` L9–L44 |
| 20 | WP-2.2 out of scope for lifecycle | `docs/WP-2.2-IMPLEMENTATION-LOG.md` | — | L29 | n/a |
| 21 | WP-2.2 ADR independence | `docs/decisions/WP-2.2-CATEGORY-MANAGEMENT.md` | decision 8 | L14 | n/a |
| 22 | WP-2.2 CI 34 tests | Actions run `45352646837` | job `build` | SUCCESS 2026-11-24T08:03:33Z | PR #3 body |
| 23 | WP-2.1 CI 17 tests | Actions run `45344878055` | job `build` | SUCCESS | PR #2 body |
| 24 | PRs unmerged Draft | GitHub API | PR 1/2/3 | `isDraft=true`, `state=OPEN`, `mergeable=MERGEABLE` | `gh pr list` |
| 25 | IMPLEMENTATION-STATUS denies category CRUD | `docs/IMPLEMENTATION-STATUS.md` | — | L38 | contradicts ROADMAP L14 and source |
| 26 | ROADMAP says category CRUD done | `docs/ROADMAP.md` | — | L14 | matches source; contradicts status L38 |
| 27 | ROADMAP says WP-2.1 CI pending | `docs/ROADMAP.md` | — | L15 | contradicts run `45344878055` |
| 28 | ROADMAP leaves backup recovery unchecked | `docs/ROADMAP.md` | — | L31 | contradicts Backup journal L35–L68 + recovery test L39 |

### EV.10 Architecture verdict for this completion review

Design is complete enough to implement WP-2.3 without ambiguity:

- Physical whole-container move
- Fail-closed unknown lifecycle
- Journal type `LifecycleMove` skipped by generic cleaner
- Matrix, crash table, lock order, WP-2.2 non-interference
- Docs drift recorded, files not rewritten

Not blocked by missing source access. Not blocked by contradictory product behavior — the contradictions are in **docs**, and the source of truth is WP-2.2 code + CI.

---

## 0. Verdict in one page

### What is actually healthy

- Stacked work branches exist, are MERGEABLE, remain Draft, and were not merged into `main`.
- Phase 1 foundation (WPF RTL, workspace, SQLite catalog, clone, backup, restore, portable publish) is real source on `work/phase-1-foundation` @ `91f189b`, CI green.
- WP-2.1 journal + locks + startup recovery is real source on `work/wp-2-1-recovery-journal` @ `66bb2cf`, CI green, 17 tests.
- WP-2.2 category identity, CRUD, physical project moves, batch rename, recovery, ordering, Persian UI is real source on `work/wp-2-2-category-management` @ `d66f204`, CI green, 34 tests.
- Safety posture of existing path-changing operations is fail-closed: no overwrite, no cascade delete, no automatic staging wipe when the final path exists, reparse points rejected, Windows-invalid category names rejected.
- Credentials are not stored in SQLite, settings, or manifests.
- Project isolation (`repository/` + `backups/` + `notes/` per container) is implemented, not just documented.

### What is incomplete

- **Lifecycle management is not implemented.** `projects.lifecycle` is a stored string defaulting to `"Active"`. Import hard-codes Active. UI only displays the column. There is no transition API, no history, no filter, no archive/restore.
- `main` contains only product docs + `.gitignore`. It has no application source and no CI workflow.
- PRs #1, #2, #3 are Draft, unreviewed, unmerged. The product is a stacked branch, not a releasable `main`.
- No installer, no uninstaller, no catalog-level backup, no missing-project inventory, no search, no publish flow, no submodule/LFS fidelity.
- `docs/IMPLEMENTATION-STATUS.md` on WP-2.2 still claims category CRUD is not implemented. That file is stale relative to the source.

### What must be corrected before or during WP-2.3

1. **Unknown lifecycle currently maps to `04_Quarantine`.** `WorkspaceService.GetProjectContainer` fail-opens. WP-2.3 must fail closed.
2. **Journal schema cannot express lifecycle history or workspace identity.** Crash journal lives in `%LocalAppData%\GitManager\operations.sqlite3`, not in the workspace catalog. Lifecycle history must live in the workspace catalog. Journal needs `workspace_root` + `payload_json` and a new operation type `LifecycleMove`.
3. **Physical lifecycle moves are mandatory**, not optional. WP-2.2 identity checks recompute the expected container from `lifecycle + category + owner + repository`. Leaving an Archived project physically under `01_Active` would make every subsequent category move/rename treat it as path drift and refuse to operate.
4. **Archive must not delete, bundle-only, gc, detach remotes, or drop backups.** Archive is a complete-container move into `03_Archived`.
5. **Do not start WP-2.3 from `main`.** Stack on `work/wp-2-2-category-management`. Do not merge PRs #1–#3 as part of WP-2.3.

### Is WP-2.3 architecture ready for approval?

**Yes — ready for Product Owner / architecture approval as a design.**  
Not ready to ship. Not ready to merge. Ready for Mission 2 implementation on an isolated stacked branch after this document is accepted.

Preconditions for implementation:

- Base branch: `work/wp-2-2-category-management` (`d66f204`).
- New branch: `work/wp-2-3-lifecycle-management`.
- Draft PR against WP-2.2, not against `main`.
- No merge of existing PRs.
- No destructive operations. Archive ≠ delete.

### Mission 2 files (exact)

See §10.1. Short answer: catalog + journal + new lifecycle/availability/recovery services + Persian UI filter/search/status + tests + docs. Do not invent a second move engine that ignores WP-2.2 invariants.

---

## 1. Evidence base (GitHub reality, not assumptions)

### 1.1 Repository

| Field | Evidence |
| --- | --- |
| Default branch | `main` |
| Visibility | private |
| Created | 2026-11-24T05:43:20Z |
| Last push | 2026-11-24T08:02:07Z |
| Open issues | 3 (these are the three PRs; `gh issue list` is empty) |
| Tags | none |
| Workflow | `.github/workflows/ci.yml` named **Windows CI**, state `active`, id `428667539` (exists only on work branches, not on `main`) |

### 1.2 Branches (live `git ls-remote` / GitHub API)

| Branch | HEAD SHA | Protected | Role |
| --- | --- | --- | --- |
| `main` | `6a78326b8cac54aca01b326f82ccba2dcbff8a2e` | no | Docs-only product baseline |
| `work/phase-1-foundation` | `91f189bedc9a2e1261e73e57030c1521d818205a` | no | Phase 1 app + backup/restore |
| `work/wp-2-1-recovery-journal` | `66bb2cf60770df7300080a9e7b5f6ad920fc45ac` | no | Journal + locks + recovery |
| `work/wp-2-2-category-management` | `d66f2049d8bf850a7cf4679b097531438b202e20` | no | Category CRUD + safe moves |

Commit counts (exclusive):

- `main` → Phase 1: **27** commits
- Phase 1 → WP-2.1: **25** commits
- WP-2.1 → WP-2.2: **80** commits

Stack is linear:

```text
main (6a78326)
  └── work/phase-1-foundation (91f189b)     PR #1
        └── work/wp-2-1-recovery-journal (66bb2cf)     PR #2
              └── work/wp-2-2-category-management (d66f204)     PR #3
```

No `work/wp-2-3-*` branch exists.

### 1.3 Pull requests

All three PRs are **OPEN + DRAFT + MERGEABLE**. Zero review comments, zero reviews.

| PR | Title | Head | Base | Add/Del | Files | CI on head |
| --- | --- | --- | --- | --- | --- | --- |
| [#1](https://github.com/majid-ghanbari-dost/Git-manager/pull/1) | Build Windows Git Manager foundation | `work/phase-1-foundation` | `main` | +1138 / −97 | 28 | SUCCESS `45339388608` |
| [#2](https://github.com/majid-ghanbari-dost/Git-manager/pull/2) | WP-2.1: Durable operation journal and crash recovery | `work/wp-2-1-recovery-journal` | `work/phase-1-foundation` | +711 / −45 | 12 | SUCCESS `45344878055` (17 tests) |
| [#3](https://github.com/majid-ghanbari-dost/Git-manager/pull/3) | WP-2.2: Category management and safe project moves | `work/wp-2-2-category-management` | `work/wp-2-1-recovery-journal` | +1970 / −28 (diff vs WP-2.1) | 19 files in that diff | SUCCESS `45352646837` (34 tests) |

PR #3 body (authoritative self-report, corroborated by the Actions run on the same head):

- Build passed
- Unit tests: **34 passed, 0 failed, 0 skipped**
- Portable win-x64 publish passed
- Source handoff: 46 manifest entries verified
- Inner handoff ZIP SHA-256: `b033fc5addfb24762ff61849567e4b0b9305f9b757b03ac67947c72db797527f`
- Source ZIP SHA-256: `3d1cab56a0e1ced66d7538ccff0c4b0add05047e727cf4b8091a276e95c74c6e`
- Portable ZIP SHA-256: `919d017909b85c1a66d175ce9f92bc05aaf6c3bcf1992d38ab63c728dfa1f8b5`
- Remains Draft; no merge authorized

### 1.4 GitHub Actions artifacts (not expired at audit time)

| Run | Branch | Artifacts |
| --- | --- | --- |
| `45352646837` (WP-2.2 head) | `work/wp-2-2-category-management` | `git-manager-wp-2-2-source-handoff` 60,550 B; `git-manager-win-x64` 61,819,133 B |
| `45349173470` / `45344878055` (WP-2.1) | `work/wp-2-1-recovery-journal` | `git-manager-wp-2-1-source-handoff` 37,064 B; `git-manager-win-x64` 61,790,291 B |
| `45339388608` (Phase 1 head) | `work/phase-1-foundation` | `git-manager-win-x64` 61,783,988 B only (no source handoff yet) |

CI trigger on WP-2.1/WP-2.2: `push` to `main` and `work/**`, plus `pull_request` to `main`. PRs #2 and #3 are **not** targeted at `main`, so they are validated by **push** to `work/**`, not by the `pull_request` trigger.

WP-2.2 had a long red window (category migration/UI/move commits) then a sustained green window through head. Failures were real; they were fixed on the same branch. Head is green. That is not the same as “never failed”.

### 1.5 `main` working tree (this checkout)

```text
.gitignore
README.md
docs/ROADMAP.md
docs/SAFETY-MODEL.md
docs/WORKSPACE-ARCHITECTURE.md
```

No `src/`, no tests, no `.github/workflows`. Product architecture is specified; product code is not on `main`.

### 1.6 WP-2.2 source tree (design baseline)

45 tracked files. Application is a single WPF project `src/GitManager.App` targeting `net10.0-windows`, package `Microsoft.Data.Sqlite 10.0.15`. Tests: `tests/GitManager.Tests` (xUnit).

Services actually present:

- `WorkspaceService`, `ProjectCatalog`, `SettingsService`
- `GitHubRemote`, `GitProcess`
- `RepositoryImportService`, `ProjectBackupService`, `ProjectRestoreService`
- `ProjectOperationJournal`, `ProjectOperationLock`, `ProjectOperationRecoveryService`
- `CategoryManagementService`, `CategoryRenameService`, `CategoryRenameRecoveryService`
- `ProjectCategoryMoveService`, `CategoryMoveRecoveryService`
- `PathSafety`
- `MainViewModel` + Persian `MainWindow.xaml`

No lifecycle service exists.

---

## 2. Full architecture audit of the current product

Scope of this section: the **implemented** architecture on WP-2.2, compared with the **specified** architecture on `main`/`docs/*`. Claims cite files.

### 2.1 Overall application architecture

**Specified:** native-feeling Windows desktop app, one central workspace, one isolated folder per project, category ⊥ lifecycle.

**Implemented:**

- .NET 10 WPF MVVM, Persian RTL (`MainWindow.xaml` `FlowDirection="RightToLeft"`).
- First-run folder picker (`OpenFolderDialog`) persists workspace path in `%LocalAppData%\GitManager\settings.json` (`SettingsService`). JSON contains only `WorkspacePath`. No tokens.
- Workspace folders created by `WorkspaceService.Initialize`: `00_Manager/catalog`, `00_Manager/logs`, `00_Manager/temp`, `01_Active`, `02_On-Hold`, `03_Archived`, `04_Quarantine`, `05_Exports`.
- `00_Manager/settings` from the architecture doc is **not** created. Settings live in LocalAppData, not in the workspace.
- `00_Manager/logs` is created and **never written**.
- Single-window UI. No navigation, no project detail page, no confirmation `MessageBox`.
- Process model: Git CLI child process, argument-safe `ProcessStartInfo.ArgumentList`, 20-minute timeout, `Kill(true)` on cancel/timeout (`GitProcess.cs`).

**Judgement:** sound skeleton for a local library manager. Not yet a product shell (no installer, no update, no structured logging, no multi-window).

### 2.2 Project management

Catalog table `projects` (WP-2.2 `ProjectCatalog.InitializeAsync`):

```text
project_id TEXT PRIMARY KEY
canonical_remote_url TEXT NOT NULL UNIQUE
host, owner, repository TEXT NOT NULL
category TEXT NOT NULL                 -- denormalized display name
category_id TEXT                       -- added by WP-2.2 migration
lifecycle TEXT NOT NULL                -- stored, never transitioned
container_path, repository_path TEXT NOT NULL
current_branch, current_commit TEXT NOT NULL
imported_at_utc TEXT NOT NULL
```

Missing versus `docs/WORKSPACE-ARCHITECTURE.md` §8:

| Specified field | In schema? |
| --- | --- |
| display_name | no |
| default_branch | no (only current_branch) |
| visibility | no |
| last_seen_at | no |
| last_backup_at | no |
| last_operation_status | no |
| availability / missing | no |

Identity:

- Canonical URL from `GitHubRemote.Parse`: HTTPS, host `github.com` only, no userinfo/query/fragment, exactly `owner/repo`.
- Duplicate import: in-memory `OrdinalIgnoreCase` match on `CanonicalRemoteUrl`, then SQLite `UNIQUE` (binary, not NOCASE).
- Path: `<root>/<lifecycleFolder>/<SafeSegment(category)>/<SafeSegment(owner)>/<SafeSegment(repository)>`.
- Hash-suffix on sanitized collision (**specified**, **not implemented**). Collision is fail-closed via `Directory.Exists` on import/move.

Import (`RepositoryImportService.CloneAsync`):

1. Parse URL, lock `import:<canonical>`.
2. Reject if catalog already has that URL.
3. Compute destination under **Active** (hard-coded).
4. Reject if destination exists.
5. Clone into `00_Manager/temp/<32-hex>/repository` with `git clone -- <url> <staged>`.
6. Verify `remote get-url origin` canonicalizes to the same URL.
7. Read branch + HEAD.
8. Create `backups/` and `notes/` in staging.
9. `Directory.Move` staging → destination, then `AddProjectAsync`.
10. On catalog failure, move destination back to staging and rethrow.
11. Journal states: Prepared → Running → CommitPending → Completed | RolledBack | RecoveryRequired.

**Judgement:** import is transactional and conservative. It always lands in Active. It does not offer Open/Update/Cancel as specified; it only rejects duplicates. Restored folders are **not** registered in the catalog (`ProjectRestoreService` writes a sibling `Restored-*` directory and returns the path).

### 2.3 Category management (WP-2.2)

Implemented and tested. See §3 for the WP-2.2 status verdict.

Invariants in source:

- Opaque `category_id` (GUID `"N"`).
- Name unique `COLLATE NOCASE`.
- `Unclassified` is system, cannot rename/delete, pinned first (`sort_order = -1` on migrate; UI blocks moving index 0).
- Empty rename is DB-only (`RenameEmptyCategoryAsync`).
- Populated rename is physical batch move (`CategoryRenameService`) preserving category ID.
- Delete only if zero projects; SQL `DELETE ... AND NOT EXISTS (SELECT 1 FROM projects ...)`. Never cascades.
- Create/rename reject Windows reserved names and names that `PathSafety.SafeSegment` would alter (`CON`, trailing `.`, etc.).
- Project rows are dual-written: `category_id` + `category` text. Triggers abort insert/update if `category_id` is null or unknown.
- SQLite `PRAGMA foreign_keys=ON` is set, but there is **no FOREIGN KEY clause**. Referential integrity is trigger-based.

### 2.4 SQLite and migration

Two databases:

| DB | Path | WAL | sync | Pooling |
| --- | --- | --- | --- | --- |
| Catalog | `<workspace>/00_Manager/catalog/projects.sqlite3` | yes | default | **false** (WP-2.2) |
| Journal | `%LocalAppData%\GitManager\operations.sqlite3` | yes | FULL | false |

Catalog migration (WP-2.2) is in-place and idempotent:

- `CREATE TABLE IF NOT EXISTS` original schema (name PK on categories, no `category_id`).
- `EnsureColumnAsync` adds `categories.category_id`, `is_system`, `sort_order`, `projects.category_id`.
- Seed default names with `INSERT OR IGNORE`.
- Assign IDs to rows missing them.
- Blank project categories → `Unclassified`.
- Orphan custom names from projects → inserted as non-system categories (`sort_order=999`).
- Backfill `projects.category_id` by `COLLATE NOCASE` name match.
- Pin Unclassified, create unique indexes and triggers.
- Wrapped in one SQLite transaction.

Covered by `CategoryManagementTests.Initialize_MigratesLegacyCatalogWithoutLosingProjectsOrCustomCategories`.

Gaps:

- No `schema_version` / `PRAGMA user_version`.
- No catalog backup before migrate.
- `ALTER TABLE ... ADD COLUMN` cannot add CHECK/FK; WP-2.3 must keep this style (additive columns + triggers), not table rebuild.
- WP-2.1 catalog still used pooling default (`true`). WP-2.2 disabled it after CI failures (`fix(wp-2.2): disable SQLite pooling for reliable catalog lifecycle`, run `45350010909`). WP-2.3 must keep `Pooling = false`.

### 2.5 Recovery and journal (WP-2.1 + WP-2.2 extensions)

Journal table `project_operations`:

```text
operation_id PK
operation_type, operation_key
project_id NULL
staging_path, final_path
state, error NULL
created_at_utc, updated_at_utc
```

States used: `Prepared`, `Running`, `CommitPending`, `Completed`, `RolledBack`, `RecoveryRequired`.

Operation types in source:

| Type | Writer | Recovered by |
| --- | --- | --- |
| `Import` | `RepositoryImportService` | `ProjectOperationRecoveryService` (delete staging only) |
| `Backup` | `ProjectBackupService` | same (`.staging-*` next to final) |
| `Restore` | `ProjectRestoreService` | same (`final.staging`) |
| `CategoryMove` | `ProjectCategoryMoveService` | `CategoryMoveRecoveryService` (reconcile both trees + catalog) |
| `CategoryRename` | `CategoryRenameService` | `CategoryRenameRecoveryService` (batch) |

`ProjectOperationRecoveryService` **skips** `CategoryMove` and `CategoryRename` (WP-2.2 one-line change). It must skip `LifecycleMove` the same way.

Locks (`ProjectOperationLock`):

- In-process: static `ConcurrentDictionary<string, SemaphoreSlim>`.
- Cross-process: exclusive `FileStream` on `%LocalAppData%\GitManager\operation-locks\<sha256(key)>.lock`.
- Keys: `project:<id>`, `category:<id>`, `import:<url>`, `restore:<backupPath>`, plus composite category-move keys.

Startup order in `MainViewModel.SetWorkspaceAsync`:

1. Initialize workspace folders + catalog migration.
2. `CategoryRenameRecoveryService.RecoverAsync`
3. `CategoryMoveRecoveryService.RecoverAsync`
4. `ProjectOperationRecoveryService.RecoverAsync`
5. Then bind importer and refresh UI.

Recovery policy (verified in tests):

- If another process holds the lock → skip (do not touch live ops).
- If final exists → `RecoveryRequired`, never delete staging.
- If staging missing and final missing → `RolledBack`.
- If staging looks unsafe or has reparse points → `RecoveryRequired`.
- Category move: source exists & catalog points at source → mark RolledBack; dest exists & catalog points at dest → Completed; dest exists & catalog still at source → move dest back to source; anything else → RecoveryRequired, **delete nothing**.

**Journal is not sufficient for WP-2.3 as-is.** Reasons in §4.3.

### 2.6 Physical project moves

`ProjectCategoryMoveService.MoveProjectCoreAsync` is the only production move engine.

Sequence:

1. Load project + target category.
2. No-op if already at that category id **and** name.
3. Compute `source` from catalog `container_path`.
4. Compute `expectedSource` from lifecycle+old category+owner+repo.
5. Compute `destination` from lifecycle+**new** category+owner+repo.
6. Reject unless:
   - both paths inside workspace
   - `source == expectedSource`
   - `repository_path == source\repository`
   - source container and repository exist
   - destination does not exist (file or directory)
   - no reparse point in source tree or destination ancestors
7. Lock categories (ordered) then project.
8. Journal `CategoryMove`.
9. `Directory.CreateDirectory(parent(dest))` + `Directory.Move(source, dest)`.
10. Update catalog paths + category in one transaction.
11. On failure after catalog commit → `RecoveryRequired` (never roll back filesystem after catalog commit).
12. On failure after move but before catalog → attempt `Directory.Move` back; if that fails → `RecoveryRequired`.

This relocates the **entire container** (repository, backups, notes, uncommitted files, `.git`). Git remotes, branches, and history are not rewritten.

Empty parent folders (`01_Active/<oldCategory>/<owner>/`) are **not** pruned.

`Directory.Move` on NTFS same-volume is a rename. Workspace layout keeps all lifecycle folders under one root, and reparse points are rejected, so same-volume is the intended case. If a future change introduced junctions, WP-2.2 already fail-closes.

### 2.7 Project path management

`WorkspaceService.GetProjectContainer`:

```csharp
"Active"   => "01_Active"
"On-Hold"  => "02_On-Hold"
"Archived" => "03_Archived"
_          => "04_Quarantine"   // FAIL-OPEN — must change in WP-2.3
```

PathSafety:

- `SafeSegment` replaces invalid filename chars, strips trailing `.`, prefixes reserved device names with `_`.
- `IsWithin` uses `GetFullPath` + prefix + `OrdinalIgnoreCase`. Test covers sibling `GitManagerRoot-Other`.

Category names that would be altered by `SafeSegment` are rejected at create/rename, so category folder names equal display names. Owner/repo still go through `SafeSegment` at path build time (GitHub parser already limits charset).

**Critical invariant for WP-2.3:** every path-changing service trusts `GetProjectContainer(lifecycle, category, owner, repo)` as the canonical location. Catalog `container_path` must equal that function. WP-2.2 move recovery uses that equality as the definition of “catalog agrees with disk”.

### 2.8 Git repository management

- Clone: full history (`git clone` without `--depth`).
- Backup: `git bundle create --all` + `git bundle verify` + zip of working tree excluding `.git` + deleted-tracked list via `git ls-files --deleted -z`.
- Restore: verify SHA-256, `git clone <bundle>`, `remote set-url origin <canonical>`, overlay zip, delete listed tracked files. Rejects `..`, rooted paths, `.git` writes, symlink zip entries (`unixType == 0xA000`).
- No submodule recurse, no LFS fetch, no hook lockdown (`core.hooksPath`), no `GIT_TERMINAL_PROMPT=0`.
- Git stderr sanitized only by truncation to 1200 chars — may still contain local paths.
- Backup tests require a real `git` on the CI image (Windows runner has Git).

Uncommitted / untracked / ignored files are in the zip snapshot, not in the bundle. Restore reconstructs dirty state. Lifecycle move does **not** need a backup first because it moves the live working copy; it must not stash, reset, or commit.

### 2.9 Error handling and incomplete operations

Strengths:

- Explicit journal states.
- Fail-closed recovery.
- Category rename rollback of already-moved projects if a later destination collides (`CategoryRenameTests.RenamePopulatedCategory_RollsBackEarlierMovesWhenLaterDestinationCollides`).
- Catalog update after filesystem move (never “catalog first”).

Weaknesses:

- No operator UI for `RecoveryRequired` beyond a count in the status bar.
- No structured log file.
- Restore in the view-model is constructed as `new ProjectRestoreService(_git, _backups)` — it uses the default journal path (same AppData file) but a **new** lock instance. In-process lock dictionary is static, so it still serializes. Fragile wiring; WP-2.3 should inject the shared journal/lock everywhere, including Restore.
- Import/Backup/Restore recovery will **delete staging** when final is absent. That is correct for those types and **must not** be reused for lifecycle/category moves (those have no disposable staging; both paths are real project data).

### 2.10 Persian UI

Implemented on WP-2.2 `MainWindow.xaml`:

- Title «مدیریت پروژه‌های Git»
- Workspace path, project count
- Clone URL + category combo
- Category create / rename / delete / up / down
- Inventory DataGrid: Repository, Owner, Category, **Lifecycle**, Branch
- Buttons: backup, move project to selected category, restore backup
- Status line for recovery counts

Not implemented:

- Lifecycle change control
- Filter / search
- Missing badge
- Dirty-working-tree warning
- Confirmation dialogs
- Per-project path display in the grid
- Accessibility / keyboard polish beyond WPF defaults

Lifecycle column is visible and always `"Active"` for imported projects.

### 2.11 Settings and user data

| Data | Location | Travels with workspace copy? |
| --- | --- | --- |
| Workspace path | `%LocalAppData%\GitManager\settings.json` | no |
| Catalog | `<workspace>/00_Manager/catalog/projects.sqlite3` | yes |
| Crash journal | `%LocalAppData%\GitManager\operations.sqlite3` | **no** |
| Locks | `%LocalAppData%\GitManager\operation-locks\` | no |
| Project files | under workspace lifecycle folders | yes |
| Credentials | Git Credential Manager / Git helper | n/a |

Copying the workspace folder to another PC copies projects + catalog, **not** in-flight journal entries. That is acceptable for completed operations. It is a recovery hole for interrupted moves. WP-2.3 must keep crash recovery conservative (fail closed if journal missing) and persist **lifecycle history in the catalog** so history survives machine change.

### 2.12 Security and data protection

Healthy:

- No PAT/password fields.
- Settings JSON has only a path.
- Git invoked without shell interpolation.
- Untrusted clone: no post-clone script execution by the app. (Git itself may still run smudge filters from the user’s global config — residual risk, document, do not “fix” by executing more Git config magic in WP-2.3.)
- Restore path-traversal checks.
- Reparse-point refusal on moves.

Gaps:

- No secret scanning (Phase 4).
- Git stderr may leak paths.
- `canonical_remote_url UNIQUE` is case-sensitive in SQLite while import pre-check is case-insensitive — concurrent two-process import of `Owner/Repo` vs `owner/repo` could insert two rows. P2. WP-2.3 duplicate rules should add a NOCASE unique index or normalized key.
- No file-integrity check that `origin` still matches catalog before a lifecycle move (category move also does not re-read `git remote`). Acceptable for WP-2.3 if we verify `repository/` exists and path identity; optional preflight `git remote get-url origin`.

### 2.13 Tests and failure-scenario coverage

WP-2.2 test inventory (34 CI cases):

| File | Cases | What they prove |
| --- | --- | --- |
| `CategoryManagementTests.cs` | 8 | idempotent migrate, stable IDs, duplicate names, Unclassified protection, populated delete/rename blocked, reserved names, ordering pin, legacy catalog + blank category → Unclassified |
| `ProjectCategoryMoveTests.cs` | 4 | whole-container move, destination collision untouched, recovery rollback when catalog at source, recovery complete when catalog at dest |
| `CategoryRenameTests.cs` | 5 | populated rename, rollback on later collision, recovery of partial batch, crash between `Directory.Move` and catalog commit, finalize when names already new |
| `ProjectOperationRecoveryTests.cs` | 3 | import staging deleted only when final absent, backup staging preserved when final exists (idempotent RecoveryRequired), lock contention |
| `ProjectBackupServiceTests.cs` | 1 | history + untracked + deleted tracked + tamper reject |
| `PathSafetyTests.cs` | 5 (1 theory×4 + 1 fact) | reserved names, sibling path |
| `GitHubRemoteTests.cs` | 8 (3+5) | URL accept/reject |

Not covered (relevant to WP-2.3):

- Reparse points (code exists, no test on Windows junctions)
- Cross-volume copy+delete behavior of `Directory.Move`
- Concurrent category rename vs project move
- Missing project folder
- Dirty working tree / `index.lock`
- Lifecycle anything
- Import duplicate / restore-not-in-catalog
- UI
- Crash after catalog commit but before journal `Completed` for category move (code marks RecoveryRequired; no dedicated test)

### 2.14 Portable publication

CI (`ci.yml` on WP-2.2):

```text
dotnet publish ... --runtime win-x64 --self-contained true
  -p:PublishSingleFile=true
  -p:IncludeNativeLibrariesForSelfExtract=true
  --output artifacts/GitManager-win-x64
```

Artifact uploaded. This is a portable folder/single-file exe, **not** an installer. README on WP-2.2: “The CI artifact is a self-contained portable build; a Setup.exe installer has not yet been produced.”

### 2.15 Windows Installer / Uninstaller readiness

**Not started.** No WiX, no MSIX, no Inno, no `*.wxs`, no uninstall story.

Required future constraints (Mission 3, not WP-2.3 code):

- Installer must not place the user’s workspace inside `Program Files`.
- Uninstaller must not delete `01_Active` / `02_On-Hold` / `03_Archived` or the catalog.
- Uninstaller may remove the exe, Start Menu shortcut, and optionally `%LocalAppData%\GitManager\settings.json` + journal + locks, with an explicit choice.
- Upgrade must not migrate/overwrite an existing workspace.

---

## 3. Actual status of WP-2.2

### 3.1 Claim vs evidence

| Claim | Evidence | Verdict |
| --- | --- | --- |
| Independent branch stacked on WP-2.1 | `git merge-base` chain; PR #3 base = `work/wp-2-1-recovery-journal` | true |
| Stable category IDs + idempotent migration | `ProjectCatalog.InitializeAsync` + `CategoryManagementTests` | true |
| Legacy projects/custom categories preserved | legacy SQLite fixture test | true |
| Unclassified protected and pinned | tests + `sort_order=-1` | true |
| CRUD + populated rename preserving ID | `CategoryManagementService` + `CategoryRenameService` + tests | true |
| Single-project physical move of whole container | `ProjectCategoryMoveService` + tests | true |
| Journaled batch rename + recovery | `CategoryRenameRecoveryService` + tests including pre-catalog-commit crash | true |
| Fail-closed collisions, path drift, reparse | source; reparse **untested** | mostly true |
| Persistent ordering + Persian UI | `ReorderCategoryAsync`, `MainWindow.xaml` | true |
| 34 unit tests, portable publish, handoff | PR #3 body + Actions run `45352646837` SUCCESS | true |
| Lifecycle in scope | ADR §8: “WP-2.2 must not alter lifecycle semantics.” Implementation log: “Lifecycle management remains a separate work package.” | **out of scope, correctly excluded** |
| `docs/IMPLEMENTATION-STATUS.md` updated | file on WP-2.2 still says “Category CRUD and lifecycle moves are not implemented” | **stale documentation** |
| Merged | PR Draft, not merged | not merged |

### 3.2 WP-2.2 completeness score

**Scope-complete. Product-incomplete. Merge-blocked by policy (Draft / no PO merge).**

Safe to treat as the implementation baseline for WP-2.3.

Not safe to treat as production.

Known leftover defects that WP-2.3 must not ignore (but need not all fix):

| ID | Item | WP-2.3 action |
| --- | --- | --- |
| D-22-1 | `IMPLEMENTATION-STATUS.md` / `CI-VALIDATION.md` / parts of `ROADMAP.md` stale | update docs on the WP-2.3 branch for the new work; do not rewrite WP-2.2 history |
| D-22-2 | Unknown lifecycle → Quarantine | **must fix** in `WorkspaceService` |
| D-22-3 | Journal has no workspace_root / payload | **must extend** |
| D-22-4 | Empty parent folders after move | prune only empty owner/category dirs, fail-closed; small BUILD NOW |
| D-22-5 | Restore not registered in catalog | out of WP-2.3 scope (ARCHITECT NOW) |
| D-22-6 | No confirmation dialogs | add for archive/restore lifecycle |
| D-22-7 | Dual `category` + `category_id` | keep; do not drop text column in WP-2.3 |
| D-22-8 | UNIQUE URL is case-sensitive | optional P2 in WP-2.3 duplicate rules |
| D-22-9 | No `schema_version` | add additive `app_meta` table in WP-2.3 migration |
| D-22-10 | Shared journal across workspaces | recovery already ignores foreign paths; add `workspace_root` column |

### 3.3 What WP-2.2 proved that WP-2.3 must reuse

1. Catalog-after-filesystem, never filesystem-after-catalog-rollback.
2. Whole-container move (not `git` rewrite, not file copy of `repository/` alone).
3. Project lock + ordered category locks.
4. Dedicated recovery service per operation family, skip those types in the generic staging cleaner.
5. Fail closed on ambiguity; operator review via `RecoveryRequired`.
6. Tests that **simulate crash windows** by writing journal rows and moving directories without going through the happy-path API.

---

## 4. Conflicts, dependencies, and architecture decisions

### 4.1 Compatibility with WP-2.1 and WP-2.2

**Compatible if and only if lifecycle change is a physical container move that updates `lifecycle` + paths the same way category change updates `category_id` + paths.**

WP-2.2 `ProjectCategoryMoveService` recomputes:

```text
expectedSource = GetProjectContainer(root, project.Lifecycle, project.Category, owner, repo)
destination    = GetProjectContainer(root, project.Lifecycle, newCategoryName, owner, repo)
```

Note: **lifecycle is an input to the path**, not just a label. If WP-2.3 changed `lifecycle` in SQLite and left the folder in `01_Active`:

- `expectedSource` would become `03_Archived/...`
- `container_path` would still be `01_Active/...`
- the inequality would throw «مسیر ثبت‌شده پروژه با ساختار کتابخانه سازگار نیست»
- category rename of an archived project would be impossible
- recovery would mark `RecoveryRequired` on later operations

Therefore:

- **Logical-only lifecycle is rejected.**
- **Keeping Archived projects inside `01_Active` is rejected.**
- **Archive packing (bundle-only, delete working copy) is rejected** — it would destroy the WP-2.2 container contract (`repository/` + `backups/` + `notes/`) and the “archive is not delete” rule.

### 4.2 Is a new migration required?

**Yes, additive, in-place, idempotent, same style as WP-2.2.** No table rebuild. No data deletion. Exact plan in §6.

### 4.3 Is the current journal enough for lifecycle?

**Enough as a crash log skeleton. Not enough as a product history. Not enough without a new operation type.**

| Need | Journal today | Decision |
| --- | --- | --- |
| Crash window of a physical move | `staging_path`/`final_path`/`state` | Reuse, type = `LifecycleMove` |
| From/to lifecycle | not a column | Put in `payload_json` and `operation_key` |
| Workspace identity | inferred by path prefix | Add `workspace_root` column; recovery ignores other roots (same as WP-2.2 rename recovery) |
| Git dirty snapshot | no | `payload_json.git_status` (summary only, no diffs/secrets) |
| Durable status history across machines | journal is in AppData | **New catalog table** `project_lifecycle_events` |
| Operator timeline in UI | `GetInterruptedAsync` only | Read history from catalog, not from crash journal |
| Generic staging cleaner | would see a lifecycle source path as “staging” | **Must skip `LifecycleMove`** exactly like CategoryMove |

Do **not** store lifecycle history only in the AppData journal. Do **not** treat the crash journal as an audit log (it is compacted by state, has no user-facing query API, and does not travel with the workspace).

### 4.4 Are physical moves really necessary?

**Yes.** Reasons, in order:

1. Specified directory contract (`docs/WORKSPACE-ARCHITECTURE.md` §2): lifecycle is a top-level folder.
2. WP-2.2 path identity uses lifecycle as a path segment (source evidence in §4.1).
3. The product promise is “not a dump of repositories in one folder”. `01_Active` must not accumulate archived trees.
4. Same move engine already exists and is tested.
5. `Directory.Move` of the container preserves uncommitted files, `.git`, backups, notes — which is exactly what archive must preserve.

Cost: same crash windows as category moves; file locks from IDEs; leftover empty parents. All already accepted by WP-2.2.

### 4.5 Archived in-place vs separate tree

**Decision: separate tree `03_Archived/<Category>/<Owner>/<Repo>/`, same inner layout.**

| Option | Verdict |
| --- | --- |
| Stay in `01_Active`, flag in DB | Breaks WP-2.2 path checks; fills Active; rejected |
| Separate `03_Archived` copy (leave original) | Duplicates Git data; split-brain; rejected |
| Bundle into `03_Archived` and delete working copy | Data loss of uncommitted work unless extra snapshot; not “archive”; rejected |
| Move container to `03_Archived` | **Accepted** |
| Move to an external archive volume | Out of scope (FUTURE) |

On-Hold uses `02_On-Hold` with the same rule. On-Hold is not a lighter archive; it is a first-class lifecycle with a full working copy.

### 4.6 Uncommitted changes

`Directory.Move` does not touch Git objects or the worktree content. Dirty state survives.

Risks:

- `index.lock` or other Git lock files → `Directory.Move` may fail → journal RolledBack/RecoveryRequired. **Fail closed. Never delete locks. Never `git reset`.**
- Files held exclusive by Visual Studio / antivirus → same.
- User expects archive to “clean” the repo → **must not**.

WP-2.3 preflight:

1. Confirm `repository/` exists and is a Git work tree (`rev-parse --is-inside-work-tree`).
2. If `.git/index.lock` or `*.lock` under `.git` exists → refuse with a Persian message naming the lock.
3. `git status --porcelain=v1 -z` (or `--porcelain`) → if dirty, **warn and require explicit confirm**, then proceed.
4. Never stash, commit, clean, or gc.

### 4.7 Missing / unreachable projects

Today: move throws `DirectoryNotFoundException`; inventory still lists the row; no badge.

WP-2.3 availability (catalog field, computed at startup and before every path op, **never moves or deletes during scan**):

| Value | Meaning | Lifecycle transition |
| --- | --- | --- |
| `Available` | container and `repository/` exist; path equals canonical layout | allowed |
| `Missing` | catalog row exists; container or `repository/` absent | **forbidden** |
| `PathDrift` | container exists but not at canonical layout, or `repository_path` ≠ `container\repository` | **forbidden** |
| `Unknown` | not yet probed | probe first |

Scan must not create folders, must not “fix” drift, must not remove rows. Re-attach / locate-on-disk is FUTURE (Mission 3+). Restore-from-backup remains the existing Restore UI (new container, not in catalog).

### 4.8 Is crash restore trustworthy?

For category moves: **trustworthy within the tested windows**, fail-closed outside them. Tests cover:

- moved, catalog not updated → roll disk back
- moved, catalog updated, journal still open → mark Completed
- both exist / neither / mismatch → RecoveryRequired

Same algorithm is proposed for `LifecycleMove`. Additional non-goals:

- Do not auto-complete a lifecycle change when catalog and disk disagree.
- Do not delete the extra copy if both `01_Active/...` and `03_Archived/...` exist.
- Do not run recovery if the project lock is held.

Residual: crash **during** `Directory.Move` on a future cross-volume copy could leave a partial dest. Reparse rejection + single-root workspace make this unlikely. If both trees exist → RecoveryRequired, operator message, no delete.

### 4.9 Duplicate registration

Rules WP-2.3 must enforce (mostly already true, must keep):

1. `canonical_remote_url` unique (extend to case-insensitive if cheap; otherwise keep in-memory case-insensitive check + document the race).
2. Import of an already-registered project is rejected **even if Archived**. Message must mention current lifecycle and path, and tell the user to change lifecycle, not re-clone.
3. Destination path occupancy blocks import and lifecycle move.
4. Restore-to-new-folder may create an unregistered clone of the same remote. That is a pre-existing hole (D-22-5). WP-2.3 must **not** auto-register restores. If the restored path is inside the workspace and collides, Restore already uses a unique `Restored-<timestamp>-<id>` name under a user-chosen parent; collision risk is low.

### 4.10 Locking vs category operations

Lifecycle move lock order (must match WP-2.2 deadlock avoidance):

1. `category:<id>` (the project’s category — one id, still take it so rename cannot interleave)
2. `project:<id>`

Category rename already holds `category:<id>` then per-project locks. Taking the category lock on lifecycle move serializes with rename.

Do **not** invent `lifecycle:` locks. Two projects in Active can move independently.

Startup recovery order becomes:

1. CategoryRename
2. CategoryMove
3. **LifecycleMove (new)**
4. Import/Backup/Restore staging cleaner

Rename first, because it may still reverse in-flight category path changes that lifecycle recovery would otherwise misread.

---

## 5. WP-2.3 architecture design — Lifecycle Management

### 5.1 Goal

Give every project a first-class lifecycle `{ Active, On-Hold, Archived }` with safe transitions, Persian UI, history, crash recovery, missing-project visibility, and **zero data loss**.

Archive means: “this project is out of the daily working set.”  
Archive does **not** mean: delete, uninstall, gc, detach, encrypt, or reduce fidelity.

### 5.2 Domain model

```text
Lifecycle ∈ { Active, On-Hold, Archived }

Project
  ...existing fields...
  Lifecycle                 // one of the three; never empty; never Quarantine via UI
  Availability              // Available | Missing | PathDrift | Unknown
  LastSeenAtUtc
  LastBackupAtUtc           // nullable; not updated unless we cheaply observe backup folder; optional
  LastOperationStatus       // last finished op summary
  LifecycleChangedAtUtc

ProjectLifecycleEvent       // append-only in catalog
  EventId
  ProjectId
  FromLifecycle, ToLifecycle
  OperationId               // journal id if any
  Result                    // Completed | RolledBack | RecoveryRequired | Rejected
  GitStatusSummary          // "clean" | "dirty: N paths" | "unknown"
  AvailabilityAtStart
  Message                   // sanitized
  CreatedAtUtc
```

`04_Quarantine` remains a folder created at workspace init. **No UI transition into Quarantine in WP-2.3.** Invalid stored lifecycle values fail closed.

### 5.3 Path function (normative)

```text
Active    → <root>/01_Active/<Category>/<Owner>/<Repo>/
On-Hold   → <root>/02_On-Hold/<Category>/<Owner>/<Repo>/
Archived  → <root>/03_Archived/<Category>/<Owner>/<Repo>/

<Repo> container
  ├── repository/     Git worktree + .git
  ├── backups/
  └── notes/
```

Inner layout is identical. Git identity (remote, branches, tags, objects, worktree) is unchanged by a transition.

### 5.4 Service split

| Service | Responsibility |
| --- | --- |
| `LifecycleState` | const strings + parse/validate; throw on unknown |
| `ProjectLifecycleService` | user-facing transitions; preflight; confirmation contract; writes events |
| `ProjectContainerRelocator` (preferred extract) **or** `LifecycleMoveService` | physical move + journal `LifecycleMove` + catalog path/lifecycle update; copy the **invariants** of `ProjectCategoryMoveService`, changing only the path dimension (lifecycle instead of category) |
| `LifecycleMoveRecoveryService` | startup recovery; isomorphic to `CategoryMoveRecoveryService` |
| `ProjectAvailabilityService` | non-mutating probe; updates availability + last_seen |
| `ProjectCatalog` | migration, queries (filter/search), `UpdateProjectLifecycleAndPathAsync`, event insert, availability update |

Do **not** call `ProjectCategoryMoveService.MoveProjectAsync` to implement lifecycle by “moving to the same category” — that API keeps `project.Lifecycle` and therefore the same top-level folder.

If extracting a shared relocator, the shared checks must remain:

- workspace containment
- expected source from **current** catalog lifecycle+category
- `repository_path == container\repository`
- dest must not exist
- reparse fail-closed
- catalog update only after successful move
- no filesystem rollback after catalog commit

That extract is justified by dependency analysis, not drive-by refactor. Tests of category move must still pass unchanged.

### 5.5 Transition API (normative)

```text
Task<LifecycleTransitionResult> ChangeLifecycleAsync(
    string projectId,
    string targetLifecycle,
    LifecycleChangeOptions options,
    CancellationToken ct)

options:
  ConfirmDirtyWorkingTree   // required true if dirty
  ConfirmArchive            // required true if target == Archived
  ConfirmRestoreFromArchive // required true if source == Archived
```

UI must collect those confirmations before calling the service. The service **re-checks** and throws if flags are missing. Never trust UI-only confirmation.

Idempotent no-op: source == target and path already canonical and Available → success, no journal row, no event? **Write an event only when a real transition happens.** No-op returns without event.

### 5.6 Happy-path algorithm

```text
1. availability probe (must be Available)
2. validate target ∈ {Active, On-Hold, Archived}
3. if source == target → return no-op
4. validate transition matrix (§7)
5. acquire category lock then project lock
6. re-read catalog (TOCTOU)
7. re-probe availability; re-verify expected source path
8. git preflight (inside-work-tree, no index.lock)
9. if dirty and !ConfirmDirtyWorkingTree → throw
10. if Archived involved and corresponding confirm false → throw
11. dest = GetProjectContainer(root, target, category, owner, repo)
12. if dest exists → throw (no overwrite)
13. journal Begin LifecycleMove
      staging_path = source container
      final_path   = dest container
      payload_json = { from, to, categoryId, gitStatus, workspaceRoot }
14. state Running
15. Directory.CreateDirectory(parent(dest))
16. Directory.Move(source, dest)
17. catalog transaction:
      UPDATE projects SET
        lifecycle = $to,
        container_path = $dest,
        repository_path = $dest/repository,
        last_operation_status = 'LifecycleMove:Completed',
        lifecycle_changed_at_utc = $now,
        availability = 'Available',
        last_seen_at_utc = $now
      WHERE project_id = $id
      INSERT project_lifecycle_events (...)
18. journal Completed
19. best-effort prune empty parent dirs under the source lifecycle
    (only if directory empty; never prune 01_Active / 02_On-Hold / 03_Archived themselves)
```

Failure after step 16 before 17: attempt move back; journal RolledBack or RecoveryRequired; insert event Result=RolledBack/RecoveryRequired if catalog still at source.

Failure after 17 before 18: journal RecoveryRequired; **do not** move disk back (catalog already new). Recovery will mark Completed.

### 5.7 Import / duplicate interaction

- Import remains default Active (WP-2.3 may add an optional lifecycle combo; default Active).
- If URL exists: throw with Persian text including `Owner/Repo`, current lifecycle, container path. Do not clone. Do not offer silent un-archive.
- Optional later UX: “باز کردن پروژه موجود” / “بازگردانی از بایگانی” as explicit buttons — nice, not required if the message is clear. Prefer a dedicated command `RevealProject` that selects the row. BUILD NOW if cheap in the view-model after lookup.

### 5.8 UI contract (Persian)

Inventory:

- Keep DataGrid; add columns: Availability (موجود / مفقود / ناسازگار), path tooltip.
- Filter bar: همه / فعال / معلق / بایگانی‌شده + دسته + جعبه جست‌وجو (owner, repo, URL, category).
- Search is in-memory over catalog list (N is small); no FTS required.
- Status badge colors: Active default, On-Hold warning, Archived muted, Missing error.

Actions on selected project:

- «انتقال به فعال»
- «انتقال به معلق»
- «بایگانی پروژه» (confirm)
- «بازگردانی از بایگانی» (confirm)

Confirm archive (RTL MessageBox):

```text
عنوان: بایگانی پروژه
متن: پروژه {owner}/{repo} به پوشه 03_Archived منتقل می‌شود.
مخزن Git، تاریخچه، فایل‌های ذخیره‌نشده، پشتیبان‌ها و یادداشت‌ها حذف نمی‌شوند.
مسیر جدید: {dest}
ادامه می‌دهید؟
```

Dirty extra line:

```text
این مخزن دارای تغییرات Commit‌نشده یا فایل ردیابی‌نشده است.
بایگانی این تغییرات را حذف نمی‌کند؛ فقط پوشه پروژه جابه‌جا می‌شود.
```

Missing project:

```text
پوشه پروژه در مسیر ثبت‌شده پیدا نشد. تغییر وضعیت تا زمان بازیابی پوشه مجاز نیست.
هیچ فایلی حذف نشد.
```

Startup status (extend existing recovery sentence):

```text
کتابخانه آماده است؛ N پروژه مفقود و M عملیات برای بررسی دستی علامت‌گذاری شد.
```

No automatic modal storm. Missing count in the status bar is enough; filter `مفقود` shows them.

### 5.9 What WP-2.3 will not do

- Delete project / uninstall workspace
- Make archived repos read-only
- `git gc`, `git bundle` as a substitute for archive
- Change remotes, branches, or commit
- Recurse submodules
- Register Restore destinations
- Installer
- Multi-workspace switcher
- Quarantine workflow
- Publish / push
- Background file watcher

---

## 6. Database migration plan (design only — do not apply)

### 6.1 Principles (copy WP-2.2)

- Run inside `ProjectCatalog.InitializeAsync` in a single transaction after the WP-2.2 category migration block.
- Idempotent: `EnsureColumnAsync`, `CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`, `CREATE TRIGGER IF NOT EXISTS`.
- Never `DROP TABLE`. Never `DELETE FROM projects`. Never rewrite `project_id`.
- Keep `category` text column.
- Keep existing `lifecycle` values; normalize only truly blank/null to `Active`.
- Invalid non-empty lifecycle values (not in the three) → **do not auto-map to Quarantine**. Leave the value, set `availability='PathDrift'` on first probe, refuse transitions until repaired. Optionally write `last_operation_status='InvalidLifecycle'`. Do not rewrite the column in migration.

### 6.2 Catalog DDL (target)

Additive columns on `projects`:

```sql
ALTER TABLE projects ADD COLUMN availability TEXT NOT NULL DEFAULT 'Unknown';
ALTER TABLE projects ADD COLUMN last_seen_at_utc TEXT;
ALTER TABLE projects ADD COLUMN last_backup_at_utc TEXT;
ALTER TABLE projects ADD COLUMN last_operation_status TEXT;
ALTER TABLE projects ADD COLUMN lifecycle_changed_at_utc TEXT;
```

`EnsureColumnAsync` already used for this style. `NOT NULL DEFAULT` is valid on SQLite ADD COLUMN.

New tables:

```sql
CREATE TABLE IF NOT EXISTS app_meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
INSERT OR IGNORE INTO app_meta(key, value) VALUES ('schema_version', '2.3');
-- if present and lower, set to 2.3 at end of successful migration
-- never decrease

CREATE TABLE IF NOT EXISTS project_lifecycle_events (
    event_id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    from_lifecycle TEXT NOT NULL,
    to_lifecycle TEXT NOT NULL,
    operation_id TEXT,
    result TEXT NOT NULL,
    git_status_summary TEXT,
    availability_at_start TEXT,
    message TEXT,
    created_at_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_lifecycle_events_project
    ON project_lifecycle_events(project_id, created_at_utc);
```

Triggers (fail closed):

```sql
CREATE TRIGGER IF NOT EXISTS trg_projects_lifecycle_insert
BEFORE INSERT ON projects
WHEN NEW.lifecycle NOT IN ('Active', 'On-Hold', 'Archived')
BEGIN
    SELECT RAISE(ABORT, 'invalid project lifecycle');
END;

CREATE TRIGGER IF NOT EXISTS trg_projects_lifecycle_update
BEFORE UPDATE OF lifecycle ON projects
WHEN NEW.lifecycle NOT IN ('Active', 'On-Hold', 'Archived')
BEGIN
    SELECT RAISE(ABORT, 'invalid project lifecycle');
END;
```

**Migration order problem:** if a legacy row already has a weird lifecycle, creating the UPDATE trigger is fine; creating the INSERT trigger is fine; a CHECK on existing rows is not retroactive. Do **not** add a CHECK that would break opening the catalog. Triggers only fire on new writes. Probe code handles legacy weird values.

Normalize blanks:

```sql
UPDATE projects
SET lifecycle = 'Active'
WHERE lifecycle IS NULL OR TRIM(lifecycle) = '';
```

Do not create a FOREIGN KEY from events to projects (keep WP-2.2 style; avoid CASCADE). Application inserts events only for known ids.

### 6.3 Journal DDL (AppData DB)

In `ProjectOperationJournal.InitializeAsync`:

```sql
-- EnsureColumn style on project_operations:
ALTER TABLE project_operations ADD COLUMN workspace_root TEXT;
ALTER TABLE project_operations ADD COLUMN payload_json TEXT;
```

Begin API extension (backward compatible):

```text
BeginAsync(..., workspaceRoot = null, payloadJson = null)
```

Existing Import/Backup/Restore callers keep working. Lifecycle/category writers should pass workspace root. Category services **may** be updated to fill `workspace_root` (small, reduces cross-workspace ambiguity). If touched, add a test that a foreign workspace_root is ignored. If not touched, LifecycleMove still works because recovery compares paths with `PathSafety.IsWithin`.

`GetInterruptedAsync` unchanged (still selects non-terminal states).

### 6.4 Catalog write API to add

```text
UpdateProjectLifecycleAndPathAsync(
    projectId, lifecycle, containerPath, repositoryPath)

InsertLifecycleEventAsync(event)

UpdateAvailabilityAsync(projectId, availability, lastSeenUtc?, status?)

GetProjectsAsync(filter)  // lifecycle, categoryId, availability, text
GetLifecycleEventsAsync(projectId)
```

`UpdateProjectLifecycleAndPathAsync` must:

- verify lifecycle ∈ allowed
- verify project exists (exactly 1 row)
- not change `category_id` / `category`
- run in a transaction together with the event insert when called from the lifecycle service

### 6.5 Rollback of the migration itself

If `InitializeAsync` throws, SQLite transaction abort leaves the previous catalog. WP-2.2 already uses this. WP-2.3 must keep the whole 2.3 block inside the same or a nested transaction after WP-2.2 columns exist.

Opening a WP-2.3 catalog with WP-2.2 code: extra columns are ignored by old `SELECT` column lists (old SELECTs are explicit, not `SELECT *`). **Forward-safe.**  
Opening a WP-2.2 catalog with WP-2.3 code: migration adds columns. **Backward-compatible upgrade.**

### 6.6 What not to migrate

- Do not move crash journal into the workspace in WP-2.3 (behavior change for WP-2.1 recovery paths; out of scope).
- Do not rebuild `categories` to drop name-PK (still `name TEXT PRIMARY KEY` plus unique `category_id` index).
- Do not hash-rename colliding folders.

---

## 7. Lifecycle transition matrix

Legend: **Allow** = permitted when Available + dest free + locks acquired. **Confirm** = Allow after explicit UI+service flags. **Deny** = throw, no journal, no disk change. **N/A** = no-op.

| From \ To | Active | On-Hold | Archived | Quarantine | other |
| --- | --- | --- | --- | --- | --- |
| Active | N/A | Allow | Confirm (archive + dirty if needed) | Deny | Deny |
| On-Hold | Allow | N/A | Confirm (archive + dirty if needed) | Deny | Deny |
| Archived | Confirm (restore-from-archive) | Confirm (restore-from-archive) | N/A | Deny | Deny |
| Missing (any stored lifecycle) | Deny | Deny | Deny | Deny | Deny |
| PathDrift | Deny | Deny | Deny | Deny | Deny |
| blank/null (after migrate → Active) | (treated as Active) |  |  |  |  |
| garbage string | Deny all; do not rewrite |  |  |  |  |

Additional Deny conditions (all transitions):

- dest path exists (file or directory)
- reparse point
- path outside workspace
- `repository/` missing or not equal to `container\repository`
- expected source ≠ catalog container
- `index.lock` present
- category lock or project lock timeout
- target category id missing (should not happen; refuse)
- another in-flight journal row for the same project in non-terminal state after lock (should be impossible if locks held; if found, RecoveryRequired rather than starting a second move)

Allow conditions never include:

- force overwrite
- delete dest
- skip catalog
- skip journal
- merge two project ids

---

## 8. Recovery scenarios (normative)

Assume type `LifecycleMove`, `staging_path=source`, `final_path=dest`.

| # | Disk source | Disk dest | Catalog container | Action | New journal state |
| --- | --- | --- | --- | --- | --- |
| R1 | exists | absent | source | nothing on disk; mark rolled back | RolledBack |
| R2 | exists | absent | dest | **do not move**; RecoveryRequired (catalog/disk disagree) | RecoveryRequired |
| R3 | absent | exists | dest | nothing on disk; mark completed | Completed |
| R4 | absent | exists | source | `Directory.Move(dest, source)` then RolledBack | RolledBack or RecoveryRequired on failure |
| R5 | absent | exists | other path | do not move | RecoveryRequired |
| R6 | exists | exists | any | do not delete either | RecoveryRequired |
| R7 | absent | absent | any | do not create folders | RecoveryRequired (data missing) |
| R8 | lock timeout | — | — | skip this pass | unchanged |
| R9 | path outside workspace | — | — | ignore if project not in this catalog; else RecoveryRequired | see WP-2.2 move recovery |
| R10 | reparse | — | — | RecoveryRequired | RecoveryRequired |
| R11 | project_id null | — | — | RecoveryRequired | RecoveryRequired |
| R12 | journal `RecoveryRequired` already | — | — | count for UI; do not retry disk | unchanged |
| R13 | crash after catalog commit, journal still Running | dest exists, catalog dest | Completed (R3) | Completed |
| R14 | category rename also interrupted | — | — | rename recovery runs first | — |

**Never:** `Directory.Delete` of source or dest for `LifecycleMove`.

Generic `ProjectOperationRecoveryService` must `continue` on `LifecycleMove` (and still on CategoryMove/CategoryRename). If this skip is forgotten, the cleaner might treat `01_Active/...` as staging and attempt deletion. That is a **P0 regression test**.

Parent-folder prune runs only on the **happy path after Completed**, never during recovery.

---

## 9. Test plan and acceptance criteria

### 9.1 Mandatory automated tests (Mission 2)

New file `tests/GitManager.Tests/ProjectLifecycleTests.cs`:

1. `Change_ActiveToOnHold_MovesWholeContainerAndPreservesGitAndNotesAndBackups`
2. `Change_ActiveToArchived_RequiresConfirm_AndDoesNotDeleteRepository`
3. `Change_ArchivedToActive_RestoresToActivePath_KeepsCategoryAndRemote`
4. `Change_SameLifecycle_IsNoOp`
5. `Change_DeniedWhenDestinationExists_NeitherSideTouched`
6. `Change_DeniedWhenProjectMissing_CatalogUnchanged`
7. `Change_DeniedWhenPathDrift`
8. `Change_DeniedWhenIndexLockPresent`
9. `Change_DirtyWorktree_RejectedWithoutConfirm_AllowedWithConfirm_FilesPreserved`
10. `Change_InvalidLifecycle_Throws_NoDiskChange`
11. `ImportDuplicate_OfArchivedProject_DoesNotClone`
12. `UnknownLifecycleInWorkspaceService_Throws_NotQuarantine`
13. `FilterAndSearch_ByLifecycleCategoryAndText` (service/catalog level)
14. `History_AppendsEvent_OnCompletedTransition`
15. `Migration_IsIdempotent_PreservesProjectIdsAndLifecycle`

New file `tests/GitManager.Tests/LifecycleMoveRecoveryTests.cs` (crash fixtures like WP-2.2):

16. `Recovery_R1_SourceOnly_MarksRolledBack`
17. `Recovery_R3_DestAndCatalog_MarksCompleted`
18. `Recovery_R4_DestOnlyCatalogSource_MovesBack`
19. `Recovery_R6_BothExist_RequiresAttention_DeletesNothing`
20. `Recovery_R7_NeitherExist_RequiresAttention`
21. `GenericStagingRecovery_DoesNotDeleteLifecyclePaths`
22. `Recovery_SkipsWhenProjectLockHeld`
23. `Recovery_IgnoresOtherWorkspace`

New file `tests/GitManager.Tests/ProjectAvailabilityTests.cs`:

24. `Probe_MarksMissingWithoutCreatingFolders`
25. `Probe_MarksPathDriftWhenRepositoryNotCanonical`
26. `Probe_DoesNotMoveOrDelete`

Keep **all 34** existing tests green.

### 9.2 Manual / Windows acceptance (not CI)

- Archive a real cloned repo with uncommitted edits; confirm files and `git status` identical at new path.
- Kill the process after `Directory.Move` (or simulate as tests do); restart; either Completed or RolledBack; no duplicate trees deleted.
- Close Visual Studio with the repo open; archive should fail closed with a lock/IO message; retry after close succeeds.
- Unclassified + custom category archived project; category rename still works after un-archive.
- Persian UI: filter Archived, search by owner, confirm dialogs RTL.

### 9.3 Acceptance gates (PO)

- [ ] Archive never deletes `repository/`, `backups/`, `notes/`, or `.git`.
- [ ] Un-archive is a reverse move, not a clone.
- [ ] Duplicate import of an archived URL is refused.
- [ ] Missing projects are visible and frozen.
- [ ] Crash recovery never deletes a lifecycle path.
- [ ] WP-2.2 tests still 34/34; new tests added; Windows CI green on the WP-2.3 branch.
- [ ] PR is Draft against WP-2.2; `main` untouched; PRs #1–#3 untouched.
- [ ] Docs: ADR + implementation log + README/roadmap lines for WP-2.3. `IMPLEMENTATION-STATUS.md` updated to stop claiming category CRUD is missing.

### 9.4 Explicit non-acceptance

A build that:

- sets `lifecycle='Archived'` without moving the folder, or
- copies instead of moving and leaves two working copies, or
- uses `git bundle` + delete as archive, or
- maps unknown lifecycle to Quarantine,

**fails the work package** even if CI is green.

---

## 10. Product remaining work — BUILD NOW / ARCHITECT NOW / FUTURE

### 10.1 BUILD NOW (Mission 2 — WP-2.3)

Implement only:

- Lifecycle transitions + physical moves + matrix
- Availability probe + Missing/PathDrift UI
- Filter + search
- Lifecycle history table + status bar
- Journal type `LifecycleMove` + recovery + skip in generic cleaner
- `WorkspaceService` fail-closed
- Import duplicate message for archived projects
- Confirm dialogs for archive / un-archive / dirty
- Tests listed in §9.1
- Docs/ADR/CI handoff rename to WP-2.3
- Empty-parent prune (fail-closed)

### 10.2 ARCHITECT NOW (Mission 3 design, little or no WP-2.3 code)

These need a written design before coding. Do **not** sneak them into Mission 2.

| Topic | Why architect now | Direction |
| --- | --- | --- |
| Windows Installer + Uninstaller | Required for real Windows users; easy to destroy workspaces if naive | Per-user install of portable exe; workspace chosen at first run; uninstall never deletes workspace; optional delete of AppData journal/settings |
| Versioning / Update | Portable artifact has no upgrade story | File version + GitHub release checksum; no auto-overwrite of workspace |
| Catalog backup / restore | Catalog loss orphans all projects on disk | Copy `projects.sqlite3` into `00_Manager/catalog/backups/` on startup; restore is copy-back + reopen; never rebuild by scanning-and-moving |
| Restore-into-catalog | Current Restore creates unregistered folders | Explicit “Register restored container” with duplicate URL checks |
| Missing-project reattach | Users will move folders in Explorer | Locate folder, verify `repository/` + remote, update paths only after confirm; never auto-scan-move |
| Operation operator console | RecoveryRequired is only a count | List interrupted journal rows, path pair, copy path, “I fixed it, mark completed” manual ack — still no delete button |
| Shared relocator extract | Category and lifecycle share 90% of invariants | Allowed in Mission 2 if tests stay green; otherwise duplicate-with-same-checks |

### 10.3 FUTURE (do not build)

- Publish, diff review, secret scan, force-push guards (Phase 4)
- Submodule / Git LFS complete fidelity (Phase 3 remainder)
- Signed installer, SmartScreen, clean-machine suite
- Multi-root workspaces, network drives, Git hosts other than github.com
- Quarantine inspection workflow
- Automatic updates
- Making archived trees read-only
- Background watchers / Explorer integration
- Cloud sync of the catalog
- Hash-suffix path collisions (keep fail-closed)

### 10.4 Windows user experience notes (inform Mission 3)

Current UX is a single RTL window that is usable for clone/backup/category. Pain for real use:

- No installer → users run a folder from Actions artifacts.
- First-run folder dialog can point at `Documents` and create `01_Active` there — acceptable if documented.
- No search today; WP-2.3 adds it.
- Restore asks for two folders and does not show the project in the grid.
- Killing the app during clone leaves staging for recovery — good; user only sees a status sentence.
- Git credential UI is Git’s own (GCM), which is correct.

---

## 11. Execution plan — Mission 2 and Mission 3

### 11.1 Mission 2 — implement WP-2.3 (code)

**Branch:** `work/wp-2-3-lifecycle-management`  
**Base:** `work/wp-2-2-category-management` @ `d66f2049d8bf850a7cf4679b097531438b202e20`  
**PR:** Draft against `work/wp-2-2-category-management`. Do not retarget to `main`. Do not merge #1/#2/#3.

#### Exact files to change

**Modify:**

| File | Change |
| --- | --- |
| `src/GitManager.App/Models/ProjectRecord.cs` | Availability, LastSeenAtUtc, LastBackupAtUtc, LastOperationStatus, LifecycleChangedAtUtc |
| `src/GitManager.App/Services/ProjectCatalog.cs` | §6 migration; filter/search; lifecycle+path update; events; availability update |
| `src/GitManager.App/Services/WorkspaceService.cs` | throw on unknown lifecycle; keep mapping for the three known values |
| `src/GitManager.App/Services/ProjectOperationJournal.cs` | `workspace_root`, `payload_json`; BeginAsync extras |
| `src/GitManager.App/Services/ProjectOperationRecoveryService.cs` | skip `LifecycleMove` |
| `src/GitManager.App/Services/RepositoryImportService.cs` | duplicate error includes lifecycle+path; still default Active |
| `src/GitManager.App/ViewModels/MainViewModel.cs` | commands, filters, search, confirm flags, availability refresh, recovery order |
| `src/GitManager.App/MainWindow.xaml` | filter/search, lifecycle actions, availability column |
| `src/GitManager.App/ViewModels/RelayCommand.cs` | only if confirm/async helpers needed; prefer not |
| `.github/workflows/ci.yml` | handoff names `WP-2.3` |
| `README.md` | current implementation paragraph |
| `docs/ROADMAP.md` | lifecycle checkbox; do not claim installer |
| `docs/IMPLEMENTATION-STATUS.md` | record WP-2.2 as implemented on its branch; add WP-2.3 section |
| `docs/WORKSPACE-ARCHITECTURE.md` | note lifecycle moves are implemented per WP-2.3 ADR (on the work branch) |
| `tests/GitManager.Tests/GitManager.Tests.csproj` | only if new refs needed (should not) |

**Optionally modify (shared relocator):**

| File | Change |
| --- | --- |
| `src/GitManager.App/Services/ProjectCategoryMoveService.cs` | delegate disk+journal invariants to shared helper; behavior must remain identical |
| `src/GitManager.App/Services/CategoryMoveRecoveryService.cs` | only if shared recovery predicate extracted |
| `src/GitManager.App/Services/GitProcess.cs` | tiny helpers for `status --porcelain` / `rev-parse` if it avoids duplicating process flags |

**Do not modify unless a test forces it:** backup, restore, path safety, GitHub remote parser, settings, App.xaml, lock implementation internals.

**Add:**

| File | Role |
| --- | --- |
| `src/GitManager.App/Models/LifecycleState.cs` | constants + parse |
| `src/GitManager.App/Models/LifecycleEventRecord.cs` | event DTO |
| `src/GitManager.App/Services/ProjectLifecycleService.cs` | matrix + preflight + orchestrate |
| `src/GitManager.App/Services/LifecycleMoveService.cs` **or** `ProjectContainerRelocator.cs` | physical move |
| `src/GitManager.App/Services/LifecycleMoveRecoveryService.cs` | R1–R14 |
| `src/GitManager.App/Services/ProjectAvailabilityService.cs` | non-mutating probe |
| `tests/GitManager.Tests/ProjectLifecycleTests.cs` | §9.1 |
| `tests/GitManager.Tests/LifecycleMoveRecoveryTests.cs` | §9.1 |
| `tests/GitManager.Tests/ProjectAvailabilityTests.cs` | §9.1 |
| `docs/decisions/WP-2.3-LIFECYCLE-MANAGEMENT.md` | ADR copied from this document’s decisions |
| `docs/WP-2.3-IMPLEMENTATION-LOG.md` | fill during Mission 2 |

**Forbidden in Mission 2:** installer scripts, catalog rewriter, DROP TABLE, merge of PRs, force-push, changing `main`, deleting user fixtures, implementing publish.

#### Suggested implementation order

1. `WorkspaceService` fail-closed + unit test (unblocks honest path math).
2. Catalog migration + model fields + idempotence test.
3. Availability probe + tests.
4. Journal columns + skip `LifecycleMove` in generic recovery + regression test that staging cleaner cannot delete a fake Active path.
5. Physical lifecycle move + recovery tests R1/R3/R4/R6.
6. `ProjectLifecycleService` matrix + dirty/lock/missing denies + history.
7. Import duplicate message.
8. UI filter/search/actions/confirm.
9. Startup: availability probe after lifecycle recovery, before status message.
10. Docs + CI handoff rename.
11. Full Windows CI; Draft PR.

#### Definition of done (Mission 2)

- CI green on `work/wp-2-3-lifecycle-management`
- Existing 34 tests + new tests all pass
- Draft PR body lists SHA of tests and artifacts
- No merge

### 11.2 Mission 3 — Windows packaging and catalog durability (after WP-2.3)

**Branch (later):** `work/wp-3-installer` stacked on the approved WP-2.3 head.  
**Still do not merge into `main` without a dedicated release decision.**

Scope (ARCHITECT NOW → then implement):

1. Choose packaging (recommend: keep self-contained publish; wrap with a per-user installer that only installs the app binary + shortcut).
2. Uninstaller contract: never delete workspace; never delete `projects.sqlite3`; optional AppData cleanup.
3. Catalog file backup on startup.
4. Operator list of `RecoveryRequired` rows.
5. Design only for restore-into-catalog and missing reattach; implement if time, else leave designed.

Out of Mission 3: publish/push, LFS, code signing if cert not available.

---

## 12. Risk register (priority)

**P0 — must not ship WP-2.3 without**

| ID | Risk | Mitigation |
| --- | --- | --- |
| P0-1 | Generic recovery deletes a lifecycle path | Skip `LifecycleMove`; test `GenericStagingRecovery_DoesNotDeleteLifecyclePaths` |
| P0-2 | Archive implemented as delete or bundle-only | Acceptance gate §9.4 |
| P0-3 | Logical lifecycle without physical move | Breaks WP-2.2; forbidden in §4 |
| P0-4 | Unknown lifecycle → Quarantine | Fail closed |
| P0-5 | Both Active and Archived trees exist and recovery deletes one | R6: delete nothing |

**P1 — fix in Mission 2**

| ID | Risk | Mitigation |
| --- | --- | --- |
| P1-1 | Dirty worktree discarded | Never git clean/reset; confirm-only |
| P1-2 | Missing project “fixed” by creating empty dest | Probe does not create; transition denied |
| P1-3 | Category rename interleaved with lifecycle | Same category lock |
| P1-4 | Duplicate clone of archived URL | Import check + message |
| P1-5 | Journal without payload/workspace | Additive columns |

**P2 — known holes, do not block WP-2.3**

| ID | Risk | Handling |
| --- | --- | --- |
| P2-1 | SQLite UNIQUE URL case-sensitive | Document; optional NOCASE index |
| P2-2 | Restore unregistered | ARCHITECT NOW |
| P2-3 | Empty parent dirs | Best-effort prune |
| P2-4 | No reparse unit test | Optional; production code already fail-closed |
| P2-5 | Git stderr truncation | Phase 4 |
| P2-6 | Stale WP-2.2 IMPLEMENTATION-STATUS | Fix on WP-2.3 docs |
| P2-7 | Cross-volume `Directory.Move` | Unlikely; R6 fail-closed |
| P2-8 | External editor file locks | Fail closed; user retry |

**P3 — cosmetic / later**

- No `.sln` file
- Logs folder unused
- Dual category columns
- Unclassified `sort_order` flip-flop −1 vs 0
- Restore service not sharing injected journal instance (works via defaults)

---

## 13. Closing answers (required)

### چه چیزهایی واقعاً سالم هستند؟

- قرارداد فضای کاری و ایزوله بودن هر پروژه در سورس WP-2.2 پیاده شده است.
- Clone مرحله‌ای، Backup+SHA-256، Restore در پوشه جدید، بدونoverwrite.
- Journal + قفل بین‌پردازه‌ای + بازیابی fail-closed برای Import/Backup/Restore.
- Category ID پایدار، مهاجرت idempotent، Unclassified محافظت‌شده، انتقال فیزیکی کل container، rename دسته‌های پر با rollback و recovery تست‌شده.
- CI ویندوز روی head هر سه شاخهٔ کاری سبز است؛ آرتیفکت portable و handoff موجود است.
- هیچ‌کدام از PRها merge نشده‌اند؛ `main` تمیز و فقط مستندات است.
- اعتبارنامه در برنامه ذخیره نمی‌شود.

### چه چیزهایی ناقص‌اند؟

- Lifecycle فقط یک ستون متنی است؛ هیچ انتقال، تاریخچه، فیلتر، بایگانی یا بازگردانی وجود ندارد.
- پروژهٔ مفقود شناسایی UI ندارد.
- Journal برای تاریخچهٔ محصول و هویت workspace کافی نیست.
- Installer/Uninstaller، به‌روزرسانی، Backup کاتالوگ، Publish، LFS/submodule.
- Restore داخل کاتالوگ ثبت نمی‌شود.
- مستند `IMPLEMENTATION-STATUS.md` روی WP-2.2 عقب‌تر از سورس است.
- `main` برنامه را شامل نمی‌شود.

### چه چیزهایی باید اصلاح شوند؟

- `WorkspaceService` fail-open به Quarantine.
- Skip کردن `LifecycleMove` در cleaner عمومی (هنگام پیاده‌سازی).
- پیام duplicate برای پروژهٔ Archived.
- تزریق یکسان journal/lock در Restore (پایداری، نه بلاک‌کننده).
- به‌روزرسانی مستندات وضعیت.
- (توصیه) `workspace_root` + `payload_json` روی journal.

### آیا معماری WP-2.3 آماده تأیید است؟

**بله. این سند برای تأیید معماری کافی است.**  
پیاده‌سازی باید روی شاخهٔ جداگانهٔ متکی به WP-2.2 انجام شود، بدون merge، بدون حذف داده، با انتقال فیزیکی به `02_On-Hold` / `03_Archived` و با recovery معادل WP-2.2.

### برای مأموریت دوم دقیقاً چه فایل‌هایی باید تغییر کنند؟

فهرست هنجاری: **§11.1**. خلاصه:

- تغییر: `ProjectRecord.cs`, `ProjectCatalog.cs`, `WorkspaceService.cs`, `ProjectOperationJournal.cs`, `ProjectOperationRecoveryService.cs`, `RepositoryImportService.cs`, `MainViewModel.cs`, `MainWindow.xaml`, `ci.yml`, docs (`README`, `ROADMAP`, `IMPLEMENTATION-STATUS`, `WORKSPACE-ARCHITECTURE`).
- افزودن: `LifecycleState.cs`, `LifecycleEventRecord.cs`, `ProjectLifecycleService.cs`, `LifecycleMoveService` یا relocator مشترک, `LifecycleMoveRecoveryService.cs`, `ProjectAvailabilityService.cs`, سه فایل تست، ADR و implementation log.
- اختیاری: استخراج مشترک از `ProjectCategoryMoveService.cs` در صورت سبز ماندن تست‌های WP-2.2.
- ممنوع: تغییر `main`، merge PRهای ۱–۳، migration اجرایی در این مأموریت (این سند)، installer، حذف جدول.

---

## Appendix A — Source map (WP-2.2 head `d66f204`)

```text
src/GitManager.App/Services/ProjectCatalog.cs
src/GitManager.App/Services/WorkspaceService.cs
src/GitManager.App/Services/ProjectCategoryMoveService.cs
src/GitManager.App/Services/CategoryMoveRecoveryService.cs
src/GitManager.App/Services/CategoryRenameService.cs
src/GitManager.App/Services/CategoryRenameRecoveryService.cs
src/GitManager.App/Services/ProjectOperationJournal.cs
src/GitManager.App/Services/ProjectOperationLock.cs
src/GitManager.App/Services/ProjectOperationRecoveryService.cs
src/GitManager.App/Services/RepositoryImportService.cs
src/GitManager.App/ViewModels/MainViewModel.cs
src/GitManager.App/MainWindow.xaml
docs/decisions/WP-2.2-CATEGORY-MANAGEMENT.md
docs/WP-2.2-IMPLEMENTATION-LOG.md
```

## Appendix B — Decision log (WP-2.3)

| # | Decision | Status |
| --- | --- | --- |
| D1 | Physical move of the whole container for every lifecycle change | Accepted |
| D2 | Archived lives under `03_Archived`, never in `01_Active` | Accepted |
| D3 | Archive is not delete / not bundle-only | Accepted |
| D4 | Crash journal stays in LocalAppData; history table in workspace catalog | Accepted |
| D5 | Additive SQLite migration, no rebuild | Accepted |
| D6 | Unknown lifecycle fail-closed | Accepted |
| D7 | Dirty worktree allowed with confirm; never discarded | Accepted |
| D8 | Missing/PathDrift cannot transition | Accepted |
| D9 | Stack on WP-2.2; do not merge existing PRs | Accepted |
| D10 | Quarantine UI out of scope | Accepted |
| D11 | Shared relocator optional, behavior-identical | Accepted |
| D12 | Installer is Mission 3, not WP-2.3 | Accepted |

---

*End of WP-2.3 technical challenge. No code in this mission. No migration applied. No PR merged.*
