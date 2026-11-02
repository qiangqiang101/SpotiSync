; Inno Setup script for SpotiSync. Built by .github/workflows/build.yml:
;   ISCC.exe /DAppVersion=1.2.0 /DFileVersion=1.2.0.0 /DAppUrl=https://github.com/owner/repo installer\SpotiSync.iss
; Expects the self-contained single-file build in ..\publish (dotnet publish ... --output publish).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef FileVersion
  #define FileVersion "0.0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

[Setup]
; Keep this AppId forever: it is how upgrades and the uninstaller recognise SpotiSync.
AppId={{E106DFE8-D64D-4611-869A-9534DE351D9A}
AppName=SpotiSync
AppVersion={#AppVersion}
AppVerName=SpotiSync {#AppVersion}
AppPublisher=Bartholomew Ho
#ifdef AppUrl
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
#endif
DefaultDirName={autopf}\SpotiSync
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\installer-output
OutputBaseFilename=SpotiSync-{#AppVersion}-setup
SetupIconFile=..\SpotiSync\SpotiSync.ico
UninstallDisplayIcon={app}\SpotiSync.exe
UninstallDisplayName=SpotiSync
VersionInfoVersion={#FileVersion}
VersionInfoProductVersion={#FileVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Admin is needed for Program Files and the firewall rule.
PrivilegesRequired=admin
; Close a running SpotiSync before replacing it on upgrade.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "firewall"; Description: "Let the other rooms connect (adds a Windows Firewall rule for private and work networks)"; GroupDescription: "Network:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\SpotiSync.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\SpotiSync"; Filename: "{app}\SpotiSync.exe"
Name: "{autodesktop}\SpotiSync"; Filename: "{app}\SpotiSync.exe"; Tasks: desktopicon

[Run]
; Remove any rule from a previous install first, so upgrades don't pile up duplicate rules.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""SpotiSync"" program=""{app}\SpotiSync.exe"""; Flags: runhidden; Tasks: firewall
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""SpotiSync"" dir=in action=allow program=""{app}\SpotiSync.exe"" enable=yes profile=private,domain"; Flags: runhidden; Tasks: firewall; StatusMsg: "Adding Windows Firewall rule..."
Filename: "{app}\SpotiSync.exe"; Description: "{cm:LaunchProgram,SpotiSync}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""SpotiSync"" program=""{app}\SpotiSync.exe"""; Flags: runhidden; RunOnceId: "RemoveFirewallRule"
