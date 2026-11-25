# WP-2.4 Windows runner install / upgrade / repair / uninstall smoke.
# This is not a Product Owner clean-machine test.
#
# Child processes must not wait forever. PR CI 45399075128 lost the hosted
# runner during this step ("lost communication with the server") because
# WaitForExit had no timeout, so the job produced no smoke logs or artifacts.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SetupExe,
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [Parameter(Mandatory = $true)][string]$LogDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-LoggedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$Arguments,
        [Parameter(Mandatory = $true)][string]$Name,
        [int]$TimeoutSeconds = 90
    )
    if (-not (Test-Path -LiteralPath $FilePath)) {
        throw "$Name executable is missing: $FilePath"
    }
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo.FileName = $FilePath
    $process.StartInfo.Arguments = $Arguments
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    if (-not $process.Start()) {
        throw "$Name failed to start."
    }
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill($true) } catch { }
        try { [void]$process.WaitForExit(5000) } catch { }
        throw "$Name timed out after ${TimeoutSeconds}s and was killed. Arguments: $Arguments"
    }
    if ($process.ExitCode -ne 0) {
        throw "$Name exited with code $($process.ExitCode). Arguments: $Arguments"
    }
}

function Invoke-UiRuntimeSmoke {
    param(
        [Parameter(Mandatory = $true)][string]$Exe,
        [Parameter(Mandatory = $true)][string]$Workspace,
        [Parameter(Mandatory = $true)][string]$Label,
        [int]$AliveSeconds = 30,
        [Parameter(Mandatory = $true)][string]$EvidenceName
    )
    New-Item -ItemType Directory -Force -Path $Workspace, (Join-Path $env:LOCALAPPDATA "GitManager\logs") | Out-Null
    $startupLog = Join-Path $env:LOCALAPPDATA "GitManager\logs\startup.log"
    if (Test-Path -LiteralPath $startupLog) { Remove-Item -LiteralPath $startupLog -Force }

    @{ WorkspacePath = $Workspace } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $env:LOCALAPPDATA "GitManager\settings.json") -Encoding utf8

    $proc = Start-Process -FilePath $Exe -PassThru
    if ($null -eq $proc) { throw "$Label failed to start." }
    $start = Get-Date
    while (((Get-Date) - $start).TotalSeconds -lt $AliveSeconds) {
        Start-Sleep -Seconds 2
        $proc.Refresh()
        if ($proc.HasExited) {
            $logTail = ""
            if (Test-Path -LiteralPath $startupLog) { $logTail = Get-Content -LiteralPath $startupLog -Raw }
            throw "$Label exited after $([int](((Get-Date) - $start).TotalSeconds))s with code $($proc.ExitCode). startup.log:`n$logTail"
        }
    }

    if (-not (Test-Path -LiteralPath $startupLog)) {
        throw "$Label: startup.log was not created at $startupLog"
    }
    $log = Get-Content -LiteralPath $startupLog -Raw
    Copy-Item -LiteralPath $startupLog -Destination (Join-Path $LogDirectory $EvidenceName) -Force
    if ($log -notmatch "MainWindowLoaded") {
        throw "$Label: MainWindowLoaded was not recorded. startup.log:`n$log"
    }

    $proc.Refresh()
    $closed = $false
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
        $closed = $proc.CloseMainWindow()
    }
    if (-not $proc.WaitForExit(20000)) {
        try { $proc.Kill($true) } catch { }
        try { [void]$proc.WaitForExit(5000) } catch { }
        throw "$Label did not exit after CloseMainWindow (handle=$($proc.MainWindowHandle), CloseMainWindow=$closed)."
    }
    $report.Add("$Label: stayed alive ${AliveSeconds}s, MainWindowLoaded recorded, exit $($proc.ExitCode), CloseMainWindow=$closed.")
}

function Assert-PathExists([string]$Path, [string]$Why) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing path ($Why): $Path"
    }
}

function Assert-PathMissing([string]$Path, [string]$Why) {
    if (Test-Path -LiteralPath $Path) {
        throw "Path should not exist ($Why): $Path"
    }
}

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
$report = New-Object System.Collections.Generic.List[string]
$report.Add("# Git Manager WP-2.4 install/uninstall report")
$report.Add("")
$report.Add("Generated (UTC): $([DateTime]::UtcNow.ToString('o'))")
$report.Add("Runner: $env:COMPUTERNAME / $env:USERNAME")
$report.Add("Setup: $SetupExe")
$report.Add("")
$report.Add("This run is a GitHub Actions windows-latest smoke. It is **not** a clean-machine Product Owner test.")
$report.Add("")

$setupFull = (Resolve-Path -LiteralPath $SetupExe).Path
$installDir = Join-Path $env:SystemDrive "CI Test Dir\Git Manager"
$workspace = Join-Path $env:TEMP "کاربر GitManager Workspace"
$localState = Join-Path $env:LOCALAPPDATA "GitManager"
$sidecar = Join-Path $installDir "preexisting-user-file.txt"
$workspaceMarker = Join-Path $workspace "00_Manager\catalog\projects.sqlite3"
$notesMarker = Join-Path $workspace "notes-keep.txt"
$settingsMarker = Join-Path $localState "settings.json"
$journalMarker = Join-Path $localState "operations.sqlite3"

$report.Add("## Paths")
$report.Add("- Install dir (spaces): `$installDir` = $installDir")
$report.Add("- Workspace (non-English): `$workspace` = $workspace")
$report.Add("- LocalAppData state: $localState")
$report.Add("")

try {
    New-Item -ItemType Directory -Force -Path $installDir, $workspace, (Split-Path $workspaceMarker), $localState | Out-Null
    Set-Content -LiteralPath $sidecar -Value "sidecar-owned-by-user" -Encoding utf8
    Set-Content -LiteralPath $workspaceMarker -Value "user-catalog-bytes" -Encoding utf8
    Set-Content -LiteralPath $notesMarker -Value "user-notes" -Encoding utf8
    Set-Content -LiteralPath $settingsMarker -Value '{"WorkspacePath":"user-owned"}' -Encoding utf8
    Set-Content -LiteralPath $journalMarker -Value "user-journal-bytes" -Encoding utf8
    $report.Add("Seeded user-owned files before install.")

    $installLog = Join-Path $LogDirectory "inno-install.log"
    Invoke-LoggedProcess -FilePath $setupFull -Name "Inno install" `
        -Arguments "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$installDir`" /LOG=`"$installLog`""
    $report.Add("Install (silent) completed.")

    $exe = Join-Path $installDir "GitManager.exe"
    Assert-PathExists $exe "installed application"
    Assert-PathExists $sidecar "pre-existing sidecar must survive install"

    $startMenu = @(
        (Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\Git Manager.lnk"),
        (Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\Git Manager\Git Manager.lnk")
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    $desktop = @(
        (Join-Path $env:PUBLIC "Desktop\Git Manager.lnk"),
        (Join-Path $env:USERPROFILE "Desktop\Git Manager.lnk")
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $startMenu) { throw "Start Menu shortcut was not created." }
    if (-not $desktop) { throw "Desktop shortcut was not created." }
    $report.Add("Start Menu shortcut: $startMenu")
    $report.Add("Desktop shortcut: $desktop")

    $settingsBackup = $null
    if (Test-Path -LiteralPath $settingsMarker) { $settingsBackup = Get-Content -LiteralPath $settingsMarker -Raw }
    $runtimeWorkspace = Join-Path $env:TEMP "GitManager-Runtime-Smoke"
    Invoke-UiRuntimeSmoke -Exe $exe -Workspace $runtimeWorkspace -Label "UI runtime after install" -AliveSeconds 30 -EvidenceName "startup-first.log"
    Invoke-UiRuntimeSmoke -Exe $exe -Workspace $runtimeWorkspace -Label "UI runtime restart" -AliveSeconds 30 -EvidenceName "startup-restart.log"
    if ($null -ne $settingsBackup) {
        Set-Content -LiteralPath $settingsMarker -Value $settingsBackup -Encoding utf8
    }
    $report.Add("Restored pre-runtime settings.json so uninstall assertions still see user-owned content.")
    $report.Add("Note: process-alive + MainWindowLoaded is not proof that every pixel rendered on an interactive desktop.")

    Invoke-LoggedProcess -FilePath $exe -Name "ci-smoke after install" -Arguments "--ci-smoke"
    $report.Add("Application --ci-smoke after install exited 0.")

    Remove-Item -LiteralPath $exe -Force
    Assert-PathMissing $exe "exe removed to simulate repair"
    $repairLog = Join-Path $LogDirectory "inno-repair.log"
    Invoke-LoggedProcess -FilePath $setupFull -Name "Inno repair" `
        -Arguments "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$installDir`" /LOG=`"$repairLog`""
    Assert-PathExists $exe "repaired application"
    Invoke-LoggedProcess -FilePath $exe -Name "ci-smoke after repair" -Arguments "--ci-smoke"
    $report.Add("Repair restored GitManager.exe; --ci-smoke exited 0.")

    $upgradeLog = Join-Path $LogDirectory "inno-upgrade.log"
    Invoke-LoggedProcess -FilePath $setupFull -Name "Inno upgrade" `
        -Arguments "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$installDir`" /LOG=`"$upgradeLog`""
    Invoke-LoggedProcess -FilePath $exe -Name "ci-smoke after upgrade" -Arguments "--ci-smoke"
    Assert-PathExists $settingsMarker "settings after upgrade"
    Assert-PathExists $journalMarker "journal after upgrade"
    Assert-PathExists $workspaceMarker "workspace catalog after upgrade"
    Assert-PathExists $notesMarker "workspace notes after upgrade"
    $report.Add("Upgrade completed; user workspace and LocalAppData state unchanged.")

    $uninstall = Get-ChildItem -LiteralPath $installDir -Filter "unins*.exe" | Select-Object -First 1
    if ($null -eq $uninstall) { throw "uninstaller was not created." }
    $uninstallLog = Join-Path $LogDirectory "inno-uninstall.log"
    Invoke-LoggedProcess -FilePath $uninstall.FullName -Name "Inno uninstall" `
        -Arguments "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$uninstallLog`""
    $report.Add("Uninstall (silent) completed.")

    Assert-PathMissing $exe "application removed"
    Assert-PathMissing $startMenu "Start Menu shortcut removed"
    Assert-PathMissing $desktop "Desktop shortcut removed"
    Assert-PathExists $sidecar "uninstall must not delete user sidecar in install dir"
    Assert-PathExists $settingsMarker "uninstall must not delete LocalAppData settings"
    Assert-PathExists $journalMarker "uninstall must not delete operation journal"
    Assert-PathExists $workspaceMarker "uninstall must not delete workspace catalog"
    Assert-PathExists $notesMarker "uninstall must not delete workspace notes"

    $settingsText = Get-Content -LiteralPath $settingsMarker -Raw
    $notesText = Get-Content -LiteralPath $notesMarker -Raw
    if ($settingsText -notmatch "user-owned") { throw "settings.json content was changed by uninstall." }
    if ($notesText -notmatch "user-notes") { throw "workspace notes content was changed by uninstall." }

    $report.Add("")
    $report.Add("## Result")
    $report.Add("PASS: install, UI runtime (no --ci-smoke, 30s + restart), launch (`--ci-smoke`), repair, upgrade, and uninstall.")
    $report.Add("PASS: uninstall did not remove user-owned workspace data or LocalAppData GitManager state.")
    $report.Add("PASS: uninstall did not remove a pre-existing sidecar file in the install directory.")
    $report.Add("")
    $report.Add("Not claimed: signed installer, clean-machine Product Owner test, Git-for-Windows bundling.")
}
catch {
    $report.Add("")
    $report.Add("## Result")
    $report.Add("FAIL: $($_.Exception.Message)")
    $report | Set-Content -LiteralPath $ReportPath -Encoding utf8
    throw
}
finally {
    $report | Set-Content -LiteralPath $ReportPath -Encoding utf8
}
