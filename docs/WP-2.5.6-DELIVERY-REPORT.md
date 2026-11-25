# WP-2.5.6 Delivery Report — Runtime stability

Branch: `work/wp-2-5-6-runtime-stability`  
Base: `work/wp-2-5-5-git-workflow` @ `26b61012276ea56333862c867e780780f993c889`  
Head: `746d97d3c4ce8d6f62830a0b3e15fc9d31a7a850`  
Product version: **2.5.6**  
Draft PR: https://github.com/majid-ghanbari-dost/Git-manager/pull/12  

Policy: Draft only. No merge to `main`. PRs #1–#11 untouched.

A green `--ci-smoke` is **not** treated as proof that the WPF shell runs.

## Crash hypotheses

| ID | Hypothesis | Result |
|---|---|---|
| A | `SolidColorBrush` values are frozen; `brush.Color = …` throws on `Loaded` | **Proven** in STA tests: `FrozenSolidColorBrush_ColorMutation_Throws`. App resource mutation throws when frozen (`AppResources_ThemeBrush_MutationThrowsWhenFrozen`). Fix proven: `SetBrush_ReplacesFrozenResource_DoesNotThrow`. |
| B | `Window.Icon` as `DrawingImage` fails native HICON conversion | **Not reproduced as a throw** in CI STA (`DrawingImage_AsWindowIcon_IsRecorded`). Still replaced with a real `.ico` for interactive desktops. Pack URI is `GitManager;component/Assets/GitManager.ico` so tests (different entry assembly) resolve it. |
| C | Startup/`Loaded` exceptions vanish | **Proven by code inspection.** Addressed: `%LOCALAPPDATA%\GitManager\logs\startup.log` and dispatcher/AppDomain/task handlers. |
| D | First-run `OpenFolderDialog` hangs non-interactive sessions | Dialog is skipped when `Environment.UserInteractive` is false; installed smoke seeds `settings.json`. |

No Event Log from the Product Owner PC was available. The 2.5.4 desktop crash is **not** claimed fully reproduced.

## Fixes in this branch

- Startup log independent of workspace.
- Theme replaces frozen brushes.
- Real ICO for window / `ApplicationIcon`.
- Guarded `Loaded`; dialog failures logged.
- STA tests construct `MainWindow`, `Show()`, wait for `Loaded`, close — **passed** on Windows CI `08e4f8b`.
- Installed UI smoke without `--ci-smoke` (30s + restart) — **failed** on `08e4f8b`; reason not in check annotations. Later CI did not start (billing).

## CI

| SHA | Run | Result |
|---|---|---|
| `08e4f8b` | push [45633358477](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45633358477) | Unit tests **success** (includes STA WPF). Publish **success**. Inno **success**. Installer smoke **failure**. Release artifacts skipped. Test results + install-evidence uploaded (`if: always`). |
| `08e4f8b` | PR [45633371073](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45633371073) | Same failure at smoke. |
| `746d97d` | push [45634457028](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45634457028) | **Job not started:** *recent account payments have failed or spending limit needs to be increased.* |

This agent cannot download TRX/evidence zips from Azure blob storage, so the smoke exception text is not in hand. The failing step is `Installer install/upgrade/uninstall smoke` after unit tests already proved `MainWindow.Loaded`.

## STA WPF tests (`08e4f8b`)

Passed on `windows-latest` with a dedicated STA dispatcher:

- Frozen brush color mutation throws.
- SetBrush replacement does not throw.
- `new MainWindow()` + `Show()` + `Loaded` + `MainWindowLoaded` in `startup.log` + close.

Limitation: this is the test host, not the installed single-file `GitManager.exe`.

## Installed smoke (no `--ci-smoke`)

**Not passed.** Required: install, seed workspace settings, start `GitManager.exe` with no `--ci-smoke`, 30s alive, `MainWindowLoaded`, clean close, restart. That step failed on `08e4f8b`. It was **not** skipped.

Limitation: GitHub `windows-latest` is Session 0. Process-alive is not visual proof of layout/RTL/theme.

## Version and packages

- ISS / CI `/DMyAppVersion=2.5.6`
- `Directory.Build.props` version `2.5.6`
- Installer and portable remain separate artifact names (`git-manager-installer-x64`, `git-manager-portable-x64`)
- Uninstall still has no `[UninstallDelete]` / `[InstallDelete]`
- SHA-256 of installer/portable: **not produced** for a green smoke run

## Not tested / not claimed

- Product Owner computer.
- Interactive visual inspection.
- Signed installer.
- Why installed smoke failed (log not retrieved).
- Further CI after billing lock.

## What Product Owner needs to do to finish

1. Restore GitHub Actions billing / spending limit.
2. Re-run the workflow on `work/wp-2-5-6-runtime-stability`.
3. If smoke still fails, the new `::error::` annotation will carry the exception (from `746d97d`).
4. Do not merge until installed UI smoke and restart pass.

No merge was performed.
