#ifndef Payload
  #error Build with build.ps1 to supply the self-contained application.
#endif
#define AppVersion GetVersionNumbersString(Payload + "\ScreenToGif.exe")

[Setup]
AppId={{DF940D12-E19B-4CDD-A45D-96ABB94524EE}
AppName=K-x录动
AppVersion={#AppVersion}
AppPublisher=开枝工作室
AppCopyright=Copyright © N-Tech + Nicke Manarin 2026
DefaultDirName={localappdata}\Programs\K-x录动
DefaultGroupName=K-x录动
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE.txt
SetupIconFile=..\ScreenToGif\Resources\Logo.ico
UninstallDisplayIcon={app}\ScreenToGif.exe
OutputBaseFilename=K-x录动-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: desktopicon; Description: "创建桌面快捷方式"; Flags: unchecked

[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\K-x录动"; Filename: "{app}\ScreenToGif.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\K-x录动"; Filename: "{app}\ScreenToGif.exe"; WorkingDir: "{app}"; Tasks: desktopicon
