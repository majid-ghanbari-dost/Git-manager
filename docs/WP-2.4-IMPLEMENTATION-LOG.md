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

## Release validation

Windows CI run [45365248926](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45365248926) on `eb434a9901f29aa1cf6534105c106da1a431bd0d` succeeded: unit tests, self-contained publish, Inno Setup compile, portable ZIP, SHA-256, source handoff, and install/repair/upgrade/uninstall smoke (path with spaces, non-English workspace folder, sidecar + LocalAppData + workspace preserved). This is not a Product Owner clean-machine test and not a signed production release.

Artifacts: `git-manager-wp-2-4-release` (`GitManager-Setup-x64.exe`, portable ZIP, checksums), `git-manager-wp-2-4-source-handoff`, `git-manager-wp-2-4-install-evidence`.

## Not claimed

- Product Owner clean-machine installation.
- Code-signed installer.
- Bundled Git for Windows.
- Production store/release readiness.
