[Setup]
AppName=FApp
AppVersion={Version}
DefaultDirName=C:\Metamation\FApp
DefaultGroupName=FApp
OutputDir=A:\
OutputBaseFilename=Setup.FApp.{Version}
LicenseFile=A:\Tools\Installer\TMM_EULA_EN.rtf
Compression=lzma
SolidCompression=yes


[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name:"desktopicon"; Description:"{cm:CreateDesktopIcon}"

[Files]
Source: "A:\Publish\FApp.exe"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\FApp.dll"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\Nori.Core.dll"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\Nori.Host.WPF.dll"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\Nori.Lux.dll"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\FApp.runtimeconfig.json"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\System.Reactive.dll"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Bin\freetype.dll"; DestDir: "{app}"; Flags:ignoreversion

; wad files ---------------------------------------------------------
Source: "A:\Publish\FApp.wad"; DestDir: "{app}"; Flags:ignoreversion
Source: "A:\Publish\Nori.wad"; DestDir: "{app}"; Flags:ignoreversion

 [Icons]
 Name: "{group}\FApp"; Filename:"{app}FApp.exe"; IconFilename:"{app}FApp.exe"
 Name: "{commondesktop}\FApp"; Filename:"{app}\FApp.exe"; Tasks: desktopicon; IconFilename:"{app}\FApp.exe"

 [Run]
 Filename: "{app}\FApp.exe"; Description: "Launch application"; Flags: postinstall nowait skipifsilent


