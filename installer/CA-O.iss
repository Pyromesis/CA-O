; CA-O Inno Setup — instalador de un solo .exe con asistente.
; Se compila con: powershell -File scripts\build-inno.ps1
; (lee la versión de BuildConstants.cs y firma con CAO_SIGN_THUMBPRINT si existe)

#ifndef AppVersion
  #define AppVersion "2.1.23"
#endif
#ifndef RepoRoot
  #define RepoRoot ".."
#endif

#define AppName "CA-O"
#define AppPublisher "CA"
#define AppURL "https://github.com/Pyromesis/CA-O"
#define ServiceName "CAO.Privileged"

[Setup]
AppId={{0F6CD057-7DBE-4AF2-BE80-8ECF03A3DC2C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile={#RepoRoot}\LICENSE
SetupIconFile={#RepoRoot}\assets\app-icon-minimal.ico
UninstallDisplayIcon={app}\ui\CA-O.UI.exe
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0.22000
PrivilegesRequired=admin
SolidCompression=yes
Compression=lzma2/ultra64
LZMAUseSeparateProcess=yes
OutputDir={#RepoRoot}\artifacts
OutputBaseFilename=CA-O-Instalador
UninstallDisplayName={#AppName} {#AppVersion}
CloseApplications=yes
CloseApplicationsFilter=CA-O.UI.exe,CA-O.InstallerGui.exe
RestartApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear icono en el escritorio"; GroupDescription: "Iconos adicionales:"

[Files]
; App WinUI3 (con su .pri y dependencias)
Source: "{#RepoRoot}\artifacts\release\ui\*"; DestDir: "{app}\ui"; Flags: ignoreversion recursesubdirs createallsubdirs
; Servicio privilegiado
Source: "{#RepoRoot}\artifacts\release\service\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\ui\CA-O.UI.exe"; Comment: "CA-O — Plataforma de rendimiento para Windows"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\ui\CA-O.UI.exe"; Comment: "CA-O — Plataforma de rendimiento para Windows"; Tasks: desktopicon

[Run]
; shellexec: CA-O.UI.exe exige elevación (requireAdministrator); con
; CreateProcess fallaba con 740. Por Shell sale el UAC como debe.
Filename: "{app}\ui\CA-O.UI.exe"; Description: "Abrir CA-O ahora"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM CA-O.UI.exe"; Flags: runhidden; RunOnceId: "KillUi"
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden; RunOnceId: "StopService"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden; RunOnceId: "DeleteService"

[Code]
const
  ServiceName = '{#ServiceName}';

procedure StopServiceForUpdate();
var
  Res: Integer;
begin
  // Cerrar la app (bloquea sus DLLs) y detener el servicio (bloquea las suyas)
  // ANTES de que Inno copie los archivos nuevos. Orden: cierre limpio, espera,
  // y remate forzoso de remanentes (un stop lento dejaba DLLs bloqueadas y la
  // copia fallaba de forma interminable).
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM CA-O.UI.exe', '', SW_HIDE, ewWaitUntilTerminated, Res);
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, Res);
  Sleep(6000);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM CA-O.Privileged.exe', '', SW_HIDE, ewWaitUntilTerminated, Res);
  Sleep(2000);
end;

procedure RegisterService();
var
  SvcExe: string;
  Res: Integer;
begin
  // Limpieza era pre-Inno: su clave ARP apuntaba al desinstalador propio
  // (ya no se instala) y duplicaba "Programas y características".
  RegDeleteKeyIncludingSubkeys(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O');
  RegDeleteKeyIncludingSubkeys(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CA-O');
  SvcExe := ExpandConstant('{app}\service\CA-O.Privileged.exe');
  // Reinstalación limpia: si existía de una versión anterior, fuera.
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, Res);
  Sleep(800);
  Exec(ExpandConstant('{sys}\sc.exe'), 'delete ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, Res);
  Sleep(800);
  Exec(ExpandConstant('{sys}\sc.exe'), 'create ' + ServiceName + ' binPath= "' + SvcExe + '" start= demand DisplayName= "CA-O Privileged Service"', '', SW_HIDE, ewWaitUntilTerminated, Res);
  if Res <> 0 then
    Log('WARN: sc create devolvió ' + IntToStr(Res));
  Exec(ExpandConstant('{sys}\sc.exe'), 'failure ' + ServiceName + ' reset= 86400 actions= restart/5000/restart/10000/reboot/60000', '', SW_HIDE, ewWaitUntilTerminated, Res);
  Exec(ExpandConstant('{sys}\sc.exe'), 'description ' + ServiceName + ' "CA-O servicio privilegiado - IPC Named Pipe con ACL + replay guard"', '', SW_HIDE, ewWaitUntilTerminated, Res);
  Exec(ExpandConstant('{sys}\sc.exe'), 'start ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, Res);
  if Res <> 0 then
    Log('WARN: sc start devolvió ' + IntToStr(Res));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StopServiceForUpdate();
  if CurStep = ssPostInstall then
    RegisterService();
end;
