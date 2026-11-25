# WP-2.5.6 Delivery Report — Runtime stability

Branch: `work/wp-2-5-6-runtime-stability`  
Base: `work/wp-2-5-5-git-workflow` @ `26b61012276ea56333862c867e780780f993c889`  
Head: `b0811790d4b416023e3da3a07b389641d1f81876`  
Product version: **2.5.6**  
Draft PR: https://github.com/majid-ghanbari-dost/Git-manager/pull/12  

Policy: Draft only. No merge to `main`. PRs #1–#11 untouched.

A green `--ci-smoke` is **not** treated as proof that the WPF shell runs.
Process-alive in GitHub `windows-latest` Session 0 is **not** visual proof of layout, RTL, or theme.
This build is **not** ready for Product Owner install until installed UI smoke (30s + restart) passes.

## Crash hypotheses

| ID | Hypothesis | Result |
|---|---|---|
| A | `SolidColorBrush` values are frozen; `brush.Color = …` throws on `Loaded` | **Proven** in STA tests: `FrozenSolidColorBrush_ColorMutation_Throws`. Fix proven: `SetBrush_ReplacesFrozenResource_DoesNotThrow`. |
| B | `Window.Icon` as `DrawingImage` fails native HICON conversion | **Not reproduced as a throw** in CI STA (`DrawingImage_AsWindowIcon_IsRecorded`). Replaced with a real `.ico`. |
| B2 | WPF pack URI `GitManager;component/Assets/GitManager.ico` throws in **single-file** publish (`Assembly.Location` empty) | **Plausible, not proven.** STA testhost loads `GitManager.dll` from disk (passed). Installed smoke uses `PublishSingleFile=true`. XAML pack Icon removed; ICO loaded from embedded stream / file next to exe. No installed-smoke log yet. |
| C | Startup/`Loaded` exceptions vanish | **Proven by code inspection.** Addressed: `%LOCALAPPDATA%\GitManager\logs\startup.log` and dispatcher/AppDomain/task handlers. |
| D | First-run `OpenFolderDialog` hangs non-interactive sessions | Dialog is skipped when `Environment.UserInteractive` is false; installed smoke seeds `settings.json`. |
| E | Production `WindowStartupLocation=CenterScreen` throws when Session 0 reports no screen | **Plausible, not proven.** STA `MainWindow_Show` overrode location to Manual before `Show()`, so it did **not** exercise production placement. `WindowStartupPlacement.Apply` now logs screen size and falls back to Manual 0,0. New STA test calls that helper then `Show()`. |
| F | Installed process dies before any managed log | Unknown. Smoke now copies `startup.log` on failure and writes `smoke-env.txt` / `install-layout.txt` even when UI never starts. |

No Event Log from the Product Owner PC was available. The 2.5.4 desktop crash is **not** claimed fully reproduced.

## Fixes in this branch

- Startup log independent of workspace.
- Theme replaces frozen brushes.
- Real ICO for window / `ApplicationIcon`; **not** via pack URI in XAML.
- Guarded `Loaded`; dialog failures logged.
- Safe window placement when screen metrics are unusable.
- STA tests construct `MainWindow`, `Show()`, wait for `Loaded`, close — **passed** on Windows CI `08e4f8b`.
- Installed UI smoke without `--ci-smoke` (30s + restart) — **failed** on `08e4f8b`; exception text **not retrieved** (evidence zip download EOF from this environment). Later CI **did not start** (billing).

## CI

| SHA | Run | Result |
|---|---|---|
| `08e4f8b` | push [45633358477](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45633358477) | Unit tests **success** (includes STA WPF). Publish **success**. Inno **success**. Installer smoke **failure**. Release artifacts skipped (`if: success()`). Test results + install-evidence uploaded (`if: always`). Evidence artifact id `15032282309` (~868 B) — contents not readable here (Azure blob EOF). |
| `08e4f8b` | PR [45633371073](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45633371073) | Same failure at smoke. |
| `746d97d` | push [45634457028](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45634457028) | **Job not started:** *recent account payments have failed or spending limit needs to be increased.* Empty `steps[]`. No artifacts. This is **not** the installed-smoke crash. |
| `746d97d` | PR [45634473638](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45634473638) | Same billing; job not started. |
| `60d6c67` | PR [45634712000](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45634712000) | Same billing; job not started. |
| `fdab8ed` | push [45646476454](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45646476454) / PR [45646490295](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45646490295) | Same billing; job not started. Pack-URI change **not CI-proven**. |

The only run that executed installed smoke after STA tests passed is `45633358477`. That is the real smoke failure to diagnose. Billing failures must not be described as smoke crashes.

## STA WPF tests (`08e4f8b`)

Passed on `windows-latest` with a dedicated STA dispatcher:

- Frozen brush color mutation throws.
- SetBrush replacement does not throw.
- `new MainWindow()` + `Show()` + `Loaded` + `MainWindowLoaded` in `startup.log` + close.

Limitation: this is the test host, not the installed single-file `GitManager.exe`. The passing Show test set `WindowStartupLocation = Manual` before `Show()`.

## Installed smoke (no `--ci-smoke`)

**Not passed.** Required: install, seed workspace settings, start `GitManager.exe` with no `--ci-smoke`, 30s alive, `MainWindowLoaded`, clean close, restart.

That step failed on `08e4f8b`. It was **not** skipped. Later SHAs never ran the job.

SHA-256 installer/portable files are produced in the publish step **before** smoke. They are **not** a green-smoke release. Do not install them as Product Owner until smoke passes.

Limitation: GitHub `windows-latest` is Session 0. Process-alive is not visual proof of layout/RTL/theme.

## Version and packages

- ISS / CI `/DMyAppVersion=2.5.6`
- `Directory.Build.props` version `2.5.6`
- Installer and portable remain separate artifact names (`git-manager-installer-x64`, `git-manager-portable-x64`)
- Uninstall still has no `[UninstallDelete]` / `[InstallDelete]`
- SHA-256 of installer/portable: **not produced for a green smoke run**

## Not tested / not claimed

- Product Owner computer.
- Interactive visual inspection.
- Signed installer.
- Why installed smoke failed on `08e4f8b` (log not retrieved).
- Pack URI or CenterScreen as the proven root cause.
- Further CI after billing lock.

## What Product Owner needs to do to finish

1. Restore GitHub Actions billing / spending limit for this private repository.
2. Re-run Windows CI on `work/wp-2-5-6-runtime-stability` (push or workflow_dispatch).
3. Read check annotation `::error::` and artifact `git-manager-wp-2-4-install-evidence` (`install-uninstall-report.md`, `startup.log` / `startup-first.log`, `smoke-env.txt`).
4. Do not merge, and do not install on the Product Owner PC, until installed UI smoke and restart pass.

No merge was performed.
