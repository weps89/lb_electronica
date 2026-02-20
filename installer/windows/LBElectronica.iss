#define MyAppName "LB Electronica"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef MySourceDir
  #define MySourceDir "..\\..\\artifacts\\win-installer\\app"
#endif

[Setup]
AppId={{F13C2B90-C51D-4E8C-BF45-BB67A5CE893A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=LB Electronica
DefaultDirName={localappdata}\\LBElectronica
DefaultGroupName=LB Electronica
UninstallDisplayIcon={app}\\LBElectronica.Server.exe
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
OutputDir=..\\..\\artifacts\\win-installer
OutputBaseFilename=LB-Electronica-Setup-{#MyAppVersion}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked
Name: "firewall"; Description: "Habilitar acceso LAN en Firewall (puerto 5080)"; GroupDescription: "Configuracion recomendada:"
Name: "autostart"; Description: "Iniciar automaticamente al abrir sesion de Windows"; GroupDescription: "Configuracion recomendada:"

[Files]
Source: "{#MySourceDir}\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\\Iniciar LB Electronica"; Filename: "{app}\\start-lb-electronica.cmd"
Name: "{group}\\Detener LB Electronica"; Filename: "{app}\\stop-lb-electronica.cmd"
Name: "{group}\\Configurar LB Electronica"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\\configure-lb-electronica.ps1"""
Name: "{group}\\Diagnostico LB Electronica"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\\diagnose-lb-electronica.ps1"""
Name: "{group}\\Desinstalar LB Electronica"; Filename: "{uninstallexe}"
Name: "{autodesktop}\\LB Electronica"; Filename: "{app}\\start-lb-electronica.cmd"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\\configure-lb-electronica.ps1"" -EnableFirewallRule"; Flags: runhidden; Tasks: firewall
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\\configure-lb-electronica.ps1"" -CreateStartupTask"; Flags: runhidden; Tasks: autostart
Filename: "{app}\\start-lb-electronica.cmd"; Description: "Iniciar LB Electronica ahora"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\\stop-lb-electronica.cmd"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}\\logs"
