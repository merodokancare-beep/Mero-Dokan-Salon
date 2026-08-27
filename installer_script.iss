; Script generated for Inno Setup 6
; MeroDokan Saloon & Spa Management System Installer

#define MyAppName "Mero Dokan Saloon"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Mero Dokan Technologies"
#define MyAppURL "https://merodokan.com"
#define MyAppExeName "MeroDokanSaloon.exe"
#define MySourceDir "d:\Bhawani Works\Project All\MeroDokanSaloon\publish"

[Setup]
AppId={{D37E8A21-17B4-4B2E-8E5A-7C1268DA87F9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Output installer executable name and location
OutputDir=d:\Bhawani Works\Project All\MeroDokanSaloon\Installer_Output
OutputBaseFilename=MeroDokanSaloon_Setup_v1.0
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Copy all published files and subdirectories
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu and Desktop Shortcuts with custom logo icon
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Option to launch application upon installation finish
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
