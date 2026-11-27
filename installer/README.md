# Git Manager installer

The Windows x64 installer is built with [Inno Setup 6](https://jrsoftware.org/isinfo.php) from `GitManager.iss`.

## What it installs

- Self-contained `GitManager.exe` under `C:\Program Files\Git Manager` by default (a path with a space).
- Start Menu and Desktop shortcuts named **Git Manager**.

The installer does **not** bundle Git for Windows. Clone, backup, and restore still require Git on `PATH`.

## What it never deletes

Uninstall removes program files and shortcuts only.

It must not remove:

- `%LOCALAPPDATA%\GitManager\` (settings, operation journal, locks)
- The user-chosen workspace (catalog, repositories, backups, notes)

Do not install Git Manager *into* a workspace folder.

## Build

On Windows, after a self-contained publish to `artifacts/GitManager-win-x64`:

```text
ISCC.exe /DMyAppVersion=2.4.0 /DPublishDir=%CD%\artifacts\GitManager-win-x64 installer\GitManager.iss
```

Output: `artifacts/GitManager-Setup-x64.exe`.
