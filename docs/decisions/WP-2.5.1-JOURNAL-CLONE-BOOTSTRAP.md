# WP-2.5.1 ADR: Journal bootstrap and Clone unblock

Status: Implemented on `work/wp-2-5-1-journal-clone-bootstrap`.
Base: `work/wp-2-4-windows-productization` @ `810cfde7b00e4b3dad8eb4361bc30375e3dce957`.

1. Every journal read and write bootstraps `project_operations` (`CREATE TABLE IF NOT EXISTS`) before SQL against that table. Fresh install, a pre-existing empty SQLite file, and a 0-byte file must not throw `no such table: project_operations`.
2. Catalog open is independent of recovery. If journal recovery fails, the library still opens, Clone stays available when a GitHub URL is present, and journal-backed mutations stay limited until recovery succeeds. Status is reported in Persian.
3. The Clone button (`دریافت و ثبت پروژه`) enables when the library is open and the URL box is non-empty. Disabled reasons are Persian and visible in the UI. Category is not a Clone gate; Unclassified remains the default.
4. Existing journal rows and catalog projects survive initialize/open (upgrade / reinstall without data loss). Uninstall still must not delete workspace or `%LOCALAPPDATA%\GitManager`.
5. Clone is covered by a scripted Git unit test (catalog registration + container layout) and a real public GitHub clone (`https://github.com/octocat/Hello-World`) that asserts catalog registration and an openable working copy. Private remotes continue to use Git Credential Manager only.
6. Git for Windows is not bundled. Force push is not added to the UI.
