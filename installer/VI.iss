; Julia's Launcher installer (Inno Setup 6). Built by .github\workflows\release.yml from the files in dist\:
;   iscc /DAppVersion=0.2.0 installer\VI.iss
; Asks for administrator rights once, installs outside Windows' protected folders (C:\Games\VI by
; default, writable by the player so updates install themselves), runs the Windows settings in the
; background (Julia's Launcher.exe --admin-setup: power, virtual memory, Defender's ransomware rule for this
; folder, the high-performance graphics card, Smooth Motion), then opens the launcher without
; administrator rights, which finds the game and prepares the shaders.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId=VI-Launcher-edupode
AppName=Julia's Launcher
AppVersion={#AppVersion}
AppPublisher=edupode
AppPublisherURL=https://github.com/felicienradio/cold-project-vi/tree/main
AppSupportURL=https://discord.gg/648WEXCAsZ
DefaultDirName={sd}\Games\VI
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=VI-Setup-{#AppVersion}
SetupIconFile=..\launcher\icon.ico
UninstallDisplayIcon={app}\Julia's Launcher.exe
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Dirs]
; The player may write here: updates replace the programs while the launcher runs without admin.
Name: "{app}"; Permissions: users-modify

[Files]
Source: "..\dist\Julia's Launcher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\victory.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\launcher-source.zip"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\SHA256SUMS.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\scripts\rt_off_hashes.txt"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"
Source: "..\README.md"; DestDir: "{app}"
Source: "..\README.pt.md"; DestDir: "{app}"

[Icons]
Name: "{autodesktop}\Julia's Launcher"; Filename: "{app}\Julia's Launcher.exe"
Name: "{autoprograms}\Julia's Launcher"; Filename: "{app}\Julia's Launcher.exe"

[Run]
Filename: "{app}\Julia's Launcher.exe"; Parameters: "--admin-setup ""{app}"""; StatusMsg: "Setting up Windows for the game (power, virtual memory, graphics card)..."; Flags: waituntilterminated runhidden
Filename: "{app}\Julia's Launcher.exe"; Description: "Open Julia's Launcher"; Flags: postinstall nowait runasoriginaluser

[UninstallDelete]
Type: filesandordirs; Name: "{app}\tools"
Type: files; Name: "{app}\Julia's Launcher.old.exe"
