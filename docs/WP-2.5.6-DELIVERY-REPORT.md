# WP-2.5.6 Delivery Report — Runtime stability

Branch: `work/wp-2-5-6-runtime-stability`  
Base: `work/wp-2-5-5-git-workflow` @ `26b61012276ea56333862c867e780780f993c889`  
Head: `45a28147474d1adef1127113d78bff0a2921db2c`  
Product version: **2.5.6**  
Draft PR: https://github.com/majid-ghanbari-dost/Git-manager/pull/12  

Policy: Draft only. No merge to `main`. PRs #1–#11 untouched.

**Status: NOT READY for Product Owner install.**

A green `--ci-smoke` is **not** proof that the WPF shell runs.
Process-alive in GitHub `windows-latest` Session 0 is **not** visual proof of layout, RTL, or theme.

## Diagnosis of CI `45647609619`

This is the run named in the mission. It was a **pull_request** workflow on SHA `5e49a47f653d64c320b82edb4e64cdba8d7a0027`.

From check-run API (captured while GitHub still answered):

| Field | Value |
|---|---|
| Conclusion | `failure` |
| Job `build` started | 2026-11-25T15:39:17Z |
| Job `build` completed | 2026-11-25T15:39:19Z |
| `steps[]` length | **0** |
| Artifacts | **none** |
| Annotation | *The job was not started because recent account payments have failed or your spending limit needs to be increased.* |

**None of these stages ran:** Build, Publish, Installer creation, Installation, First launch, MainWindowLoaded, 30-second runtime, Graceful shutdown, Restart.

This failure is **not** an application crash. It is GitHub Actions billing on a private repository. No job log exists because the hosted job never started. Artifact upload (`if: always()`) cannot run if the job is refused.

The sibling push run `45647594044` on the same SHA failed the same way.

Re-fetch of this run from the current environment (later) returned GitHub auth/TLS errors (`GH_TOKEN` rejected; `gnutls_handshake` / API EOF). That does not change the earlier billing annotation.

## The only installed-smoke execution after STA passed

| SHA | Run | What actually ran |
|---|---|---|
| `08e4f8b` | push [45633358477](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45633358477) | Restore/Build/Unit tests **success** (STA `MainWindow.Show` + Loaded). Publish **success**. Inno **success**. **Installer smoke failed.** Evidence artifact `git-manager-wp-2-4-install-evidence` id `15032282309` (~868 B). Exception text **not retrieved** (Azure blob EOF from this environment). |
| `08e4f8b` | PR 45633371073 | Same smoke failure. |
| `746d97d` … `5e49a47` | 45634457028, 45634473638, 45634712000, 45646476454, 45646490295, **45647609619**, 45647594044 | Job **not started** (billing). No steps. No artifacts. |

Root cause of the `08e4f8b` installed-process death is **not proven**. Pack URI, window icon, and CenterScreen remain hypotheses. They must not be declared the cause until an installed smoke log shows them.

## Fixes present on the branch (unproven against installed exe)

- Frozen brush replacement (`SetBrush`).
- Startup log under `%LOCALAPPDATA%\GitManager\logs\startup.log`.
- XAML pack URI Icon removed; ICO from embedded stream / file next to exe.
- `WindowStartupPlacement` fallback when screen metrics are unusable.
- Installed smoke launches the **Inno-installed** `GitManager.exe` with **no** `--ci-smoke`, 30s, `MainWindowLoaded`, close, restart.
- Smoke writes `smoke-env.txt`, `windows-version.txt`, `dotnet-info.txt`, `install-layout.txt`, process snapshots, `smoke-exception.txt`, and copies `startup.log` on failure.
- Evidence / installer / portable artifact names include `${{ github.run_id }}-${{ job.status }}` and upload `if: always()`.
- Runner environment is recorded **before** Restore so a later smoke crash still has `artifacts/install-evidence/runner-env.txt` if the job actually started.

STA tests are the test host, not the installed single-file exe. The original passing Show test used Manual placement; a later test calls `WindowStartupPlacement.Apply` then Show.

## Version and packages

- ISS / CI `/DMyAppVersion=2.5.6`
- Installer and portable are **separate** artifact names (not one combined zip).
- Uninstall has no `[UninstallDelete]` / `[InstallDelete]`.
- SHA-256 of installer/portable for a **green smoke** run: **not produced**.
- Independent installer/portable links: **none for a passing run**.

## Not tested / not claimed

- Product Owner computer.
- Interactive visual inspection.
- Signed installer.
- Proven root cause of `08e4f8b` smoke failure.
- Any CI job after the billing lock, including `45647609619`.

## What must happen to finish

1. Reconnect GitHub authentication if `gh` reports an invalid token.
2. Restore GitHub Actions billing / spending limit for this private repository.
3. Re-run Windows CI on `work/wp-2-5-6-runtime-stability`.
4. A passing run must show: install from that run's Setup exe, 30s alive, `MainWindowLoaded`, no critical startup exception, clean close, restart, then separate installer and portable artifacts with SHA-256.
5. Do not merge until that happens.

No merge was performed.
