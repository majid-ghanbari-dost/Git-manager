# WP-2.4 Implementation Log

Implementation branch: `work/wp-2-4-windows-productization`.
Base: `work/wp-2-3-lifecycle-management` @ `8f810242eebb9e022c3b392f351879540e98d03a`.
Merge policy: Draft PR into WP-2.3 only. No merge to `main`. Prior work-package branches and PRs #1–#5 are untouched.

## Implemented

- Self-contained win-x64 Release publish (single-file).
- Inno Setup installer `GitManager-Setup-x64.exe` with Start Menu and Desktop shortcuts.
- Upgrade/repair via a stable AppId; uninstall removes program files only.
- `--ci-smoke` headless launch for CI (no WPF shell).
- Windows CI: installer compile, portable ZIP, source handoff, SHA-256, install/repair/upgrade/uninstall smoke on a path with spaces and a non-English workspace folder.
- Unit tests for smoke initialization and installer script user-data guards.

## Not claimed

- Product Owner clean-machine installation.
- Code-signed installer.
- Bundled Git for Windows.
- Production store/release readiness.
