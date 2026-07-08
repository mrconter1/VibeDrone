; Inno Setup script for the VibeDrone (OpenDrone) Windows installer.
; Compiled in CI with:
;   ISCC.exe /DMyAppVersion=X.Y.Z /DSrcDir=<repo-root> installer\OpenDrone.iss
; SrcDir is an absolute path to the repo root so build\, godot\icon.ico and the output
; folder all resolve regardless of the compiler's working directory.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef SrcDir
  #define SrcDir "."
#endif
#define MyAppName "VibeDrone"
#define MyAppExeName "OpenDrone.exe"
#define MyAppPublisher "mrconter1"
#define MyAppUrl "https://github.com/mrconter1/VibeDrone"

[Setup]
AppId={{4C2B6F1E-9A3D-4E77-A1B2-0D9E5C7A11F0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir={#SrcDir}\installer_out
OutputBaseFilename=OpenDrone-Setup-v{#MyAppVersion}
SetupIconFile={#SrcDir}\godot\icon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SrcDir}\build\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
