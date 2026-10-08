; Inno Setup script for Open Virtual Stream Deck. Built by scripts\build-installer.ps1:
;   ISCC.exe /DAppVersion=1.0.0 /DSourceDir=..\dist installer\ovsd.iss  ->  dist\OVSD-Setup-1.0.0.exe
; Installs per user by default (no admin needed for the files). The two system-wide options (firewall
; rules, CPU sensor service) run together in one elevated "OVSD.exe --setup" call: a single UAC prompt.

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
AppPublisherURL=https://github.com/ems107/open-virtual-stream-deck
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
es.SystemGroup=Permisos del sistema (Windows pedirá confirmación una sola vez):
en.SystemGroup=System permissions (Windows asks for confirmation once):
es.FirewallTask=Permitir que tus móviles y tablets se conecten (regla del firewall solo para redes privadas)
en.FirewallTask=Let your phones and tablets connect (firewall rule for private networks only)
es.SensorsTask=Temperatura de CPU (instala el driver PawnIO y un servicio que solo lee los sensores)
en.SensorsTask=CPU temperature (installs the PawnIO driver and a service that only reads the sensors)
es.LaunchApp=Abrir Open Virtual Stream Deck
en.LaunchApp=Launch Open Virtual Stream Deck
es.SetupFailed=No se han podido aplicar los permisos del sistema. Puedes hacerlo más tarde desde OVSD: Ajustes → Este PC.
en.SetupFailed=The system permissions could not be applied. You can do it later in OVSD: Settings → This PC.
es.DeleteData=¿Borrar también tus perfiles, imágenes y ajustes?%n%n(%1)%n%nSi vas a reinstalar OVSD, responde No para conservarlos.
en.DeleteData=Also delete your profiles, images and settings?%n%n(%1)%n%nAnswer No to keep them if you plan to reinstall OVSD.

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "firewall"; Description: "{cm:FirewallTask}"; GroupDescription: "{cm:SystemGroup}"
Name: "sensors"; Description: "{cm:SensorsTask}"; GroupDescription: "{cm:SystemGroup}"; Flags: unchecked

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
Filename: "{app}\OVSD.exe"; Description: "{cm:LaunchApp}"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\firewall.configured"

[Code]
const
  SensorServiceKey = 'SYSTEM\CurrentControlSet\Services\OVSDSensors';

{ Runs "OVSD.exe --setup <tasks>" with administrator rights: directly when the installer already has
  them, otherwise through one UAC prompt. }
function RunElevatedSetup(const Exe, Tasks: String): Boolean;
var
  ResultCode: Integer;
begin
  if IsAdmin() then
    Result := Exec(Exe, '--setup ' + Tasks, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0)
  else
    Result := ShellExec('runas', Exe, '--setup ' + Tasks, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  { OVSD lives in the tray; it must exit before its executable can be replaced or removed. Only the
    user's copies (session > 0): the sensor service runs in session 0 from its own folder. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM OVSD.exe /FI "SESSION ne 0"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Tasks: String;
begin
  if CurStep <> ssPostInstall then Exit;
  Tasks := '';
  if WizardIsTaskSelected('firewall') then Tasks := Tasks + ' firewall';
  { An existing sensor service is refreshed with the new version of OVSD. }
  if WizardIsTaskSelected('sensors') or RegKeyExists(HKLM, SensorServiceKey) then Tasks := Tasks + ' sensors';
  if Tasks <> '' then
    if not RunElevatedSetup(ExpandConstant('{app}\OVSD.exe'), Trim(Tasks)) then
      if not WizardSilent() then MsgBox(CustomMessage('SetupFailed'), mbInformation, MB_OK);
end;

function InitializeUninstall(): Boolean;
var
  Tasks: String;
begin
  StopRunningApp();
  Tasks := '';
  if FileExists(ExpandConstant('{app}\firewall.configured')) then Tasks := Tasks + ' remove-firewall';
  if RegKeyExists(HKLM, SensorServiceKey) then Tasks := Tasks + ' remove-sensors';
  if Tasks <> '' then RunElevatedSetup(ExpandConstant('{app}\OVSD.exe'), Trim(Tasks));
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
