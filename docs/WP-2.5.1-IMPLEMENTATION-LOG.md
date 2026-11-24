# WP-2.5.1 Implementation Log

Implementation branch: `work/wp-2-5-1-journal-clone-bootstrap`.
Base: `work/wp-2-4-windows-productization` @ `810cfde7b00e4b3dad8eb4361bc30375e3dce957`.
Merge policy: Draft PR into WP-2.4 only. No merge to `main`. Prior work-package branches and PRs #1–#6 are untouched.

## Implemented

- `ProjectOperationJournal` creates `project_operations` before `GetInterruptedAsync`, `GetAsync`, and `SetStateAsync` (Begin already did).
- `LibraryStartup` assigns the catalog even when recovery throws. Sensitive journal-backed commands stay limited.
- Clone CanExecute: library open + journal usable + non-empty GitHub URL, with Persian `CloneDisabledReason`. A corrupt journal keeps the library open but blocks Clone before `git clone`, staging, destination, or catalog insert. If the journal fails after Clone has started, the original error is preserved; staging/destination are not deleted when Recovery cannot be recorded.
- Recovery/journal exceptions are written to `00_Manager/logs/git-manager.log`. The status line is Persian and does not include raw exception text or paths.
- Tests: fresh journal, empty SQLite, upgrade without data loss, recovery-does-not-block-catalog, **invalid non-SQLite journal file content**, **malformed SQLite interior pages** (header preserved), journal replaced with non-SQLite text mid-Clone, git-failure then journal-file-content cleanup, Clone gate, scripted Clone registration, real public repository clone, GCM/credential boundary unit tests.
- GCM private-repo review: [WP-2.5.1-GCM-PRIVATE-REPOS.md](decisions/WP-2.5.1-GCM-PRIVATE-REPOS.md). Not an interactive private clone.
- Installer smoke: each child process has a 90s `WaitForExit` timeout; the smoke step has `timeout-minutes: 12` so a hung installer cannot starve the runner heartbeat until GitHub drops the job.

## Release validation

Windows CI run [45386246393](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45386246393) on `a65cfe4ebb232e5a9aad6d6f68c6f7d88ae8ec82` succeeded for the initial bootstrap.

Review follow-up Windows CI run [45388706337](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45388706337) on `c90fff3038c64fbc3ac919f5f8de17ad66971a82` succeeded: full unit test suite (WP-2.1 through WP-2.5.1, including invalid journal file content, Clone fail-closed, credential-boundary tests, and the real public `octocat/Hello-World` clone), self-contained publish, Inno Setup compile, portable ZIP, SHA-256, source handoff, and install/repair/upgrade/uninstall smoke. Pull request run [45388716073](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45388716073) on the same commit also succeeded.

Mid-Clone commit `f5ab0eb` push CI [45399066199](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45399066199) succeeded, including installer smoke (~22s). Pull request CI [45399075128](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45399075128) on the same SHA **failed**: unit tests, publish, and Inno Setup compile succeeded; the smoke step started at `2026-11-24T13:03:36Z` and never completed. GitHub annotation: *The hosted runner lost communication with the server* (`2026-11-24T13:47:52Z`). Job logs were not uploaded (EOF). Root cause: `Invoke-LoggedProcess` used unbounded `WaitForExit()`, so a hung Inno/`--ci-smoke` child could block the step until the runner heartbeat was lost. This is not treated as a green PR CI. The smoke script now kills children after 90s and the workflow step times out at 12 minutes so `if: always()` evidence upload can still run.

This is not a Product Owner clean-machine test and not a signed production release.

## Not claimed

- Product Owner clean-machine installation.
- WP-2.5.2+ (chrome/theme, Fetch/Pull, Commit, Push, progress UI, LFS honesty, installer/portable split, user docs).
