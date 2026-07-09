; Inno Setup script for the VibeDrone launcher installer.
; Compiled in CI with:
;   ISCC.exe /DMyAppVersion=X.Y.Z /DSrcDir=<repo-root> installer\OpenDrone.iss
; It installs the Avalonia launcher (launcher\publish) into Program Files and points the shortcuts at
; it. The launcher itself downloads/updates the game into %LocalAppData%\VibeDrone\app on first run.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef SrcDir
  #define SrcDir "."
#endif
#define MyAppName "VibeDrone"
#define MyAppExe "VibeDroneLauncher.exe"
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
; Per-user install (no admin/UAC): keeps the launcher and the game's per-user LocalAppData data in
; the same scope, so updates and uninstall always target the right user. {autopf} -> LocalAppData\Programs.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExe}
OutputDir={#SrcDir}\installer_out
OutputBaseFilename=VibeDrone-Setup-v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SrcDir}\launcher\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The launcher downloads the game into %LocalAppData%\VibeDrone; remove it on uninstall (binaries only).
Type: filesandordirs; Name: "{localappdata}\VibeDrone"

[Code]
// After uninstalling, offer to also delete the player's save data (kept by default). The game stores
// it via Godot's user:// which lives in %AppData%\Godot\app_userdata\OpenDrone.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    if MsgBox('Also delete your VibeDrone save data (settings, best laps and custom tracks)?',
              mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{userappdata}\Godot\app_userdata\OpenDrone'), True, True, True);
end;
