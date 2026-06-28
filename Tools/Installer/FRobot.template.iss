[Setup]
AppName=FRobot
AppVersion={Version}
DefaultDirName=C:\Metamation\FRobot
DefaultGroupName=FRobot
OutputDir=A:\
OutputBaseFilename=Setup.FRobot.{Version}
LicenseFile=A:\Tools\Installer\TMM_EULA_EN.rtf
Compression=lzma
SolidCompression=yes


[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name:"desktopicon"; Description:"{cm:CreateDesktopIcon}"

[Files]
Source: "A:\Publish\FRobot.exe"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\FRobot.dll"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\Nori.Core.dll"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\Nori.Host.WPF.dll"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\Nori.Lux.dll"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\FRobot.runtimeconfig.json"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\System.Reactive.dll"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Bin\freetype.dll"; DestDir: "{app}"; Flags:ignoreversion

; wad files ---------------------------------------------------------
Source: "A:\Publish\FRobot.wad"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\Nori.wad"; DestDir: "{app}"; Flags:ignoreversion

 [Icons]
 Name: "{group}\FRobot"; Filename:"{app}FRobot.exe"; IconFilename:"{app}FRobot.exe"
 Name: "{commondesktop}\FRobot"; Filename:"{app}\FRobot.exe"; Tasks: desktopicon; IconFilename:"{app}\FRobot.exe"

 [Run]
 Filename: "{app}\FRobot.exe"; Description: "Launch application"; Flags: postinstall nowait skipifsilent


