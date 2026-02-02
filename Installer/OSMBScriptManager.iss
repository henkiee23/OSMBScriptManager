; Inno Setup script for OSMBScriptManager
; The preprocessor variable MyAppVersion is supplied via ISCC /dMyAppVersion=...

[Setup]
AppName=OSMBScriptManager
AppVersion={#MyAppVersion}
DefaultDirName={pf}\OSMBScriptManager
DefaultGroupName=OSMBScriptManager
OutputBaseFilename=OSMBScriptManager-Setup-{#MyAppVersion}-win-x64
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
PrivilegesRequired=admin

[Files]
; Include the published single-file application and any other files in the publish folder
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\OSMBScriptManager"; Filename: "{app}\OSMBScriptManager.exe"

[Run]
Filename: "{app}\OSMBScriptManager.exe"; Description: "Launch OSMBScriptManager"; Flags: nowait postinstall skipifsilent
