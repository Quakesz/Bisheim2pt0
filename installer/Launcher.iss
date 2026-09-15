#ifndef PayloadDir
  #error Supply /DPayloadDir with the extracted approved launcher directory
#endif
[Setup]
AppId={{84E30140-3700-47AB-869C-1DF119DA9005}
AppName=Bisheim2pt0 Launcher
AppVersion=1.0 Alpha
AppVerName=Bisheim2pt0 Launcher 1.0 Alpha
VersionInfoVersion=1.0.0.0
AppPublisher=Bisheim2pt0
AppPublisherURL=https://github.com/Quakesz/Bisheim2pt0
DefaultDirName={localappdata}\Programs\Bisheim2pt0
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
DisableProgramGroupPage=yes
DisableDirPage=yes
LicenseFile={#PayloadDir}\LICENSE
OutputDir=..
OutputBaseFilename=Bisheim2pt0-Setup-Windows-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Bisheim2pt0.exe
AppMutex=Local\Bisheim2pt0Launcher
CloseApplications=yes
RestartApplications=no

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userdesktop}\Bisheim2pt0"; Filename: "{app}\Bisheim2pt0.exe"; WorkingDir: "{app}"
Name: "{userprograms}\Bisheim2pt0"; Filename: "{app}\Bisheim2pt0.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\Bisheim2pt0.exe"; Description: "Open Bisheim2pt0 Launcher"; Flags: nowait postinstall skipifsilent
