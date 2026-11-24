# WP-2.5.1 ADR: Journal bootstrap and Clone unblock

Status: Implemented on `work/wp-2-5-1-journal-clone-bootstrap`.
Base: `work/wp-2-4-windows-productization` @ `810cfde7b00e4b3dad8eb4361bc30375e3dce957`.

1. Every journal read and write bootstraps `project_operations` (`CREATE TABLE IF NOT EXISTS`) before SQL against that table. Fresh install, a pre-existing empty SQLite file, and a 0-byte file must not throw `no such table: project_operations`.
2. Catalog open is independent of recovery. If journal recovery fails, the library still opens. Journal-backed mutations stay limited until recovery succeeds. Status is Persian. Raw exception text and internal paths go only to `00_Manager/logs/git-manager.log`.
3. Clone is enabled only when the library is open, the journal can record an operation (`EnsureAvailableAsync` / integrity check), and the URL box is non-empty. If the journal file is corrupt or not writable, Clone stays disabled with a Persian reason and `CloneAsync` fails closed before `git clone`, staging, destination, or catalog insert. If the journal becomes unusable after Clone has started, the original error is rethrown (cleanup/journal errors are logged, not substituted). Staging and destination are not deleted when Recovery/RolledBack cannot be recorded. Category is not a Clone gate; Unclassified remains the default.
4. Existing journal rows and catalog projects survive initialize/open (upgrade / reinstall without data loss). Uninstall still must not delete workspace or `%LOCALAPPDATA%\GitManager`.
5. Clone is covered by a scripted Git unit test (catalog registration + container layout) and a real public GitHub clone (`https://github.com/octocat/Hello-World`). A corrupt-SQLite test is separate from simulated recovery exceptions. Private remotes are a GCM policy review, not covered by the public clone; see `WP-2.5.1-GCM-PRIVATE-REPOS.md`.
6. Git for Windows is not bundled. Force push is not added to the UI.
