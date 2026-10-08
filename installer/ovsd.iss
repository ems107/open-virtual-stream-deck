; Inno Setup script for Open Virtual Stream Deck. Built by scripts\build-installer.ps1:
;   ISCC.exe /DAppVersion=1.0.0 /DSourceDir=..\dist installer\ovsd.iss  ->  dist\OVSD-Setup-1.0.0.exe
; Installs per user by default (no admin prompt); choosing "all users" also adds the firewall rule.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist"
#endif

[Setup]
AppId={{5E0C9B8A-3F4D-4C1E-9B7A-0F2D6E8A1C31}
AppName=Open Virtual Stream Deck
AppVersion={#AppVersion}
AppVerName=Open Virtual Stream Deck {#AppVersion}
AppPublisher=OVSD
DefaultDirName={autopf}\OVSD
DefaultGroupName=Open Virtual Stream Deck
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#SourceDir}
OutputBaseFilename=OVSD-Setup-{#AppVersion}
SetupIconFile=..\server\src\OVSD.Host\ovsd.ico
UninstallDisplayIcon={app}\OVSD.exe
UninstallDisplayName=Open Virtual Stream Deck
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
es.AutostartTask=Iniciar OVSD con Windows
en.AutostartTask=Start OVSD with Windows
es.LaunchApp=Abrir Open Virtual Stream Deck
en.LaunchApp=Launch Open Virtual Stream Deck
es.DeleteData=¿Borrar también tus perfiles, imágenes y ajustes?%n%n(%1)%n%nSi vas a reinstalar OVSD, responde No para conservarlos.
en.DeleteData=Also delete your profiles, images and settings?%n%n(%1)%n%nAnswer No to keep them if you plan to reinstall OVSD.

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\OVSD.exe"; DestDir: "{app}"; Flags: ignoreversion
; Holds the user's port/name overrides: never overwrite it on upgrade.
Source: "{#SourceDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{autoprograms}\Open Virtual Stream Deck"; Filename: "{app}\OVSD.exe"
Name: "{autodesktop}\Open Virtual Stream Deck"; Filename: "{app}\OVSD.exe"; Tasks: desktopicon

[Registry]
; Same value the app's "Start with Windows" toggle uses, so both stay in sync.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OpenVirtualStreamDeck"; ValueData: """{app}\OVSD.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""Open Virtual Stream Deck"" dir=in action=allow program=""{app}\OVSD.exe"" profile=private enable=yes"; Flags: runhidden; Check: IsAdminInstallMode
Filename: "{app}\OVSD.exe"; Description: "{cm:LaunchApp}"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Open Virtual Stream Deck"""; Flags: runhidden; Check: IsAdminInstallMode; RunOnceId: "DelFirewallRule"

[Code]
procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  { OVSD lives in the tray; it must exit before its executable can be replaced or removed. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM OVSD.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\OVSD');
    if DirExists(DataDir) and not UninstallSilent() then
      if MsgBox(FmtMessage(CustomMessage('DeleteData'), [DataDir]), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
  end;
end;
