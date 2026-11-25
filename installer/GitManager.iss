; Git Manager — Inno Setup 6 script (WP-2.4)
; User workspace, SQLite catalog, backups, notes, journal, and settings live
; outside {app}. Do not add UninstallDelete or InstallDelete sections.

#ifndef MyAppVersion
  #define MyAppVersion "2.5.6"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\GitManager-win-x64"
#endif

#define MyAppName "Git Manager"
#define MyAppExeName "GitManager.exe"
#define MyAppId "{{8E6C3F1A-9B2D-4F70-A4C1-7D5E2B8F0A31}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppName}
AppCopyright=Copyright (C) 2026
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
DefaultDirName={autopf}\Git Manager
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts
OutputBaseFilename=GitManager-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
SetupIconFile=..\src\GitManager.App\Assets\GitManager.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
DirExistsWarning=auto
AllowNoIcons=yes
CloseApplications=yes
RestartApplications=no
RestartIfNeededByRun=no
ChangesAssociations=no
SetupLogging=yes
; Do not touch %LOCALAPPDATA%\GitManager or any user-chosen workspace.
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
ConfirmUninstall=Git Manager will be removed from this computer.%n%nYour workspace, project catalog, backups, notes, and LocalAppData settings will not be deleted.

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Git Manager"; Flags: nowait postinstall skipifsilent
