# WP-2.4 ADR: Windows installer and user-data boundary

Status: Implemented on `work/wp-2-4-windows-productization`.

1. Distribution is a self-contained win-x64 build plus an Inno Setup installer named `GitManager-Setup-x64.exe`. A portable ZIP is also produced.
2. The application remains the WP-2.3 product. Productization does not change lifecycle, catalog, or recovery behavior.
3. Install directory (Program Files by default) is not the workspace. Settings, journal, and locks stay under `%LOCALAPPDATA%\GitManager`.
4. Uninstall and upgrade must not delete workspace catalogs, repositories, backups, notes, or LocalAppData state. The Inno script has no `[UninstallDelete]` / `[InstallDelete]` entries.
5. Same `AppId` enables upgrade and repair by re-running the installer. Shortcuts are created for Start Menu and Desktop.
6. Windows CI compiles the installer and smokes silent install, `--ci-smoke` launch, repair, upgrade, and uninstall. That is not a Product Owner clean-machine test.
7. Git for Windows is not bundled; the app already fails closed when `git` is missing.
