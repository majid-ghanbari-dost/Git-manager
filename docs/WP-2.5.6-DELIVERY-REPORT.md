# WP-2.5.6 Delivery Report — Runtime stability

Branch: `work/wp-2-5-6-runtime-stability`  
Base: `work/wp-2-5-5-git-workflow` @ `26b61012276ea56333862c867e780780f993c889`  
Product version: **2.5.6**  
Policy: Draft PR only. No merge to `main`. PRs #1–#11 untouched.

This report is updated after Windows CI completes. A green `--ci-smoke` is **not** treated as proof that the WPF shell runs.

## Crash hypotheses

| ID | Hypothesis | Result |
|---|---|---|
| A | `SolidColorBrush` values in `Application.Resources` are frozen; `brush.Color = …` throws `InvalidOperationException` on `Loaded` | **Proven in STA tests** (`ResourceDictionary_Freezes_SolidColorBrushes_MutationThrows`, `AppResources_ThemeBrushes_AreFrozen`). Fix: replace the resource with a new frozen brush (`SetBrush_ReplacesFrozenResource_DoesNotThrow`). |
| B | `Window.Icon` bound to a `DrawingImage` can fail native HICON conversion on some desktops | **Not reproduced as a throw** in CI STA (`DrawingImage_AsWindowIcon_IsRecorded`). Still replaced with a real `.ico` because taskbar/native icon conversion is the Product Owner scenario and CI Session 0 is not that desktop. |
| C | Startup/`Loaded` exceptions vanish (WinExe, no dispatcher log, `DiagnosticLog` only under workspace) | **Proven by code inspection** and addressed: `%LOCALAPPDATA%\GitManager\logs\startup.log` plus dispatcher/AppDomain/task handlers. |
| D | First-run `OpenFolderDialog` can hang a non-interactive session | **Not crashed in code**; hang risk remains if no saved workspace. Installed smoke seeds `settings.json` to a real folder. Dialog is try/caught so a throw is logged. |

No Windows Event Log / WerFault from the Product Owner PC was available. Do **not** claim the 2.5.4 desktop crash is fully reproduced here.

## Fixes

- `StartupLog` to `%LOCALAPPDATA%\GitManager\logs\startup.log` (ProcessStarted, StartupArguments redacted, ApplicationStartup, MainWindowCreated, MainWindowLoaded, StartupException, DispatcherUnhandledException, AppDomainUnhandledException, TaskSchedulerUnobservedTaskException, ApplicationExit). Logging failures are swallowed.
- Theme: replace resource dictionaries instead of mutating frozen brushes.
- `MainWindow.Icon` and `ApplicationIcon` use `src/GitManager.App/Assets/GitManager.ico`. In-window mark stays `GitManagerLogo` DrawingImage.
- `Loaded` is a guarded async handler; theme and `InitializeAsync` failures are logged separately.
- `OpenFolderDialog` failures are logged; they do not become unhandled.
- `--ci-smoke` still exists and still does **not** open `MainWindow`. New tests cover the real window path.

## Files changed (this work package)

- `src/GitManager.App/App.xaml.cs`
- `src/GitManager.App/MainWindow.xaml`
- `src/GitManager.App/MainWindow.xaml.cs`
- `src/GitManager.App/GitManager.App.csproj`
- `src/GitManager.App/Services/StartupLog.cs`
- `src/GitManager.App/ViewModels/MainViewModel.cs`
- `src/GitManager.App/Assets/GitManager.ico`
- `Directory.Build.props`
- `installer/GitManager.iss`
- `.github/workflows/ci.yml`
- `scripts/ci-install-smoke.ps1`
- `tests/GitManager.Tests/GitManager.Tests.csproj`
- `tests/GitManager.Tests/WpfRuntimeTests.cs`
- `tests/GitManager.Tests/StartupLogTests.cs`
- `tests/GitManager.Tests/ProductSmokeTests.cs`
- `docs/IMPLEMENTATION-STATUS.md`
- `docs/WP-2.5.6-DELIVERY-REPORT.md`

Git Clone/Fetch/Pull/Commit/Push behavior, journal fail-closed policy, and uninstall data retention are unchanged.

## STA WPF tests

Expected (Windows CI):

- Frozen resource mutation throws.
- `MainWindow.SetBrush` replacement does not throw.
- `new MainWindow()` + `Show()` reaches `Loaded` and writes `MainWindowLoaded`, then closes.

## Installed smoke (no `--ci-smoke`)

After silent install, CI seeds a workspace path, starts `GitManager.exe` with **no** `--ci-smoke`, requires the process to stay alive 30 seconds, requires `MainWindowLoaded` in `startup.log`, attempts `CloseMainWindow`, then repeats for restart.

Limitation: GitHub `windows-latest` is Session 0. Process-alive + log is **not** visual proof of layout, RTL, or theme on an interactive desktop.

## Version and packages

- ISS default and CI `/DMyAppVersion=2.5.6`
- `Directory.Build.props` assembly version `2.5.6`
- Installer artifact name: `git-manager-installer-x64` (`GitManager-Setup-x64.exe`)
- Portable artifact name: `git-manager-portable-x64` (`GitManager-win-x64-portable.zip`)
- Uninstall still has no `[UninstallDelete]` / `[InstallDelete]`

## CI and hashes

Filled after Windows CI on this branch head.

- Commit SHA: _pending push_
- Push CI: _pending_
- Pull request CI: _pending_
- STA WPF: _pending_
- Installed UI smoke: _pending_
- Installer SHA-256: _pending_
- Portable SHA-256: _pending_
- Artifact links: _pending_

## Not tested / not claimed

- Product Owner computer.
- Interactive visual inspection of every control.
- Signed installer.
- Git for Windows bundling.
- Private GCM clone.
- Branch management, LFS, GitHub repo creation.
