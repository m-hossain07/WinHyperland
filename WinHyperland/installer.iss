#ifndef Arch
  #define Arch "x64"
#endif

[Setup]
AppName=WinHyperland
AppVersion=1.0
DefaultDirName={autopf}\WinHyperland
DefaultGroupName=WinHyperland
UninstallDisplayIcon={app}\WinHyperland.exe
Compression=lzma2
SolidCompression=yes
OutputDir=.\Publish
OutputBaseFilename=WinHyperland_Setup_{#Arch}
LicenseFile=Terms.txt
SetupIconFile=logo.ico
PrivilegesRequired=admin

[Files]
Source: "Publish\{#Arch}\WinHyperland.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "logo.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\WinHyperland"; Filename: "{app}\WinHyperland.exe"; IconFilename: "{app}\logo.ico"
Name: "{userdesktop}\WinHyperland"; Filename: "{app}\WinHyperland.exe"; IconFilename: "{app}\logo.ico"

[Run]
Filename: "{app}\WinHyperland.exe"; Description: "Launch WinHyperland"; Flags: nowait postinstall skipifsilent
