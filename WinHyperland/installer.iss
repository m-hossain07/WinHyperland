#ifndef Arch
  #define Arch "x64"
#endif

[Setup]
AppName=WinHyperisland
AppVersion=1.0
DefaultDirName={autopf}\WinHyperisland
DefaultGroupName=WinHyperisland
UninstallDisplayIcon={app}\WinHyperisland.exe
Compression=lzma2
SolidCompression=yes
OutputDir=.\Publish
OutputBaseFilename=WinHyperisland_Setup_{#Arch}
LicenseFile=Terms.txt
SetupIconFile=logo.ico
PrivilegesRequired=admin

[Files]
Source: "Publish\{#Arch}\WinHyperisland.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "logo.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\WinHyperisland"; Filename: "{app}\WinHyperisland.exe"; IconFilename: "{app}\logo.ico"
Name: "{userdesktop}\WinHyperisland"; Filename: "{app}\WinHyperisland.exe"; IconFilename: "{app}\logo.ico"

[Run]
Filename: "{app}\WinHyperisland.exe"; Description: "Launch WinHyperisland"; Flags: nowait postinstall skipifsilent
