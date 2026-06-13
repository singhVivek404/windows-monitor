; Developer Workstation Auditor — Inno Setup 6 script
; Run via:  scripts\build-installer.ps1
; Or manually: ISCC.exe installer\setup.iss  (from repo root)

#define MyAppName      "Developer Workstation Auditor"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "WorkstationAuditor"
#define MyAppExeName   "Auditor.UI.exe"
#define MyPublishDir   "..\publish"

[Setup]
AppId={{6D4F3A2B-8E91-4C5D-B7F0-2A1E9D3C8456}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/singhVivek404/windows-monitor
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=WorkstationAuditorSetup
SetupIconFile=
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName}

; Automatically set execution policy for this user so PS1 collectors run
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell"; \
  ValueType: string; ValueName: "ExecutionPolicy"; ValueData: "RemoteSigned"; \
  Flags: uninsdeletevalue

[Files]
; Main executable (self-contained .NET, no runtime install needed on target machine)
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; All PowerShell collector scripts — must live alongside the EXE
Source: "{#MyPublishDir}\AuditCollector.ps1";      DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\Collector-Machine.ps1";   DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\Collector-Processes.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\Collector-Services.ps1";  DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\Collector-Startup.ps1";   DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\Collector-Disk.ps1";      DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\Collector-Software.ps1";  DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\Collector-Network.ps1";   DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\Collector-DevEnv.ps1";    DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Desktop shortcut
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; \
  Comment: "Run a full developer workstation health audit"

; Start Menu shortcut
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; \
  Comment: "Run a full developer workstation health audit"

[Run]
; Offer to launch immediately after install (checkbox, checked by default)
Filename: "{app}\{#MyAppExeName}"; \
  Description: "Launch {#MyAppName} now"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up per-user data/reports directories on uninstall
Type: filesandordirs; Name: "{localappdata}\WorkstationAuditor"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nThe application audits your developer workstation health:%n - System RAM, CPU, and disk usage%n - Startup programs and background services%n - Developer tool inventory (git, dotnet, docker, node, etc.)%n - WSL2 virtual disk sizes and package cache bloat%n%nClick Next to continue.
