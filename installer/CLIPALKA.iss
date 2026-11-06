#ifdef TestInstaller
  #define MyAppName "CLIPALKA Installer Test"
  #define MyStartupKey "Software\CLIPALKA\InstallerTest\Run"
#else
  #define MyAppName "CLIPALKA"
  #define MyStartupKey "Software\Microsoft\Windows\CurrentVersion\Run"
#endif
#ifndef MyAppVersion
  #define MyAppVersion "0.9.3-rc.4"
#endif
#ifndef MyAppFileVersion
  #define MyAppFileVersion "0.9.3.3"
#endif
#ifndef MyAppId
  #define MyAppId "{{1B00B859-694D-4C01-AE8C-23148971155A}"
#endif
#ifndef MyAppSourceDir
  #define MyAppSourceDir "..\publish\win-x64"
#endif
#define MyAppPublisher "CLIPALKA"
#define MyAppExeName "CLIPALKA.exe"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
VersionInfoVersion={#MyAppFileVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UsePreviousAppDir=yes
DisableDirPage=auto
UsePreviousTasks=yes
DisableProgramGroupPage=yes
Uninstallable=yes
UninstallFilesDir={app}
UninstallDisplayName={#MyAppName}
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
OutputDir=..\artifacts
OutputBaseFilename=CLIPALKA-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\src\Clipalka.App\Assets\clipalka.ico

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Excludes: ".clipalka-files.txt"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyAppSourceDir}\.clipalka-files.txt"; DestDir: "{app}"; Flags: ignoreversion
#ifndef TestInstaller
Source: "..\artifacts\VC_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
#endif

[Icons]
#ifndef TestInstaller
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{autoprograms}\Удалить CLIPALKA"; Filename: "{uninstallexe}"; IconFilename: "{uninstallexe}"
#endif
Name: "{app}\Удалить CLIPALKA"; Filename: "{uninstallexe}"; IconFilename: "{uninstallexe}"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные значки:"

[Run]
#ifndef TestInstaller
Filename: "{tmp}\VC_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Установка компонентов Visual C++…"; Flags: waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить {#MyAppName}"; Flags: nowait postinstall skipifsilent
#endif

[Code]
var
  PreviousFiles: TArrayOfString;

function ManifestPath(Line: String): String;
var
  Separator: Integer;
begin
  Result := '';
  Separator := Pos('|', Line);
  if Separator = 65 then
    Result := Copy(Line, Separator + 1, Length(Line));
end;

function SafeOwnedPath(RelativePath: String): Boolean;
begin
  Result := False;
  if RelativePath = '' then Exit;
  if (RelativePath[1] = '\') or (Pos(':', RelativePath) > 0) or
     (Pos('/', RelativePath) > 0) or (Pos('..', RelativePath) > 0) then Exit;
  { Never treat user recordings as application files, even with a damaged manifest. }
  if (CompareText(ExtractFileExt(RelativePath), '.mp4') = 0) or
     (CompareText(ExtractFileExt(RelativePath), '.wav') = 0) then Exit;
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  CurrentFiles: TArrayOfString;
  I, J: Integer;
  RelativePath, FullPath, ExpectedHash: String;
  StillInstalled: Boolean;
  StartupCommand: String;
begin
  if CurStep = ssInstall then begin
    { Capture the previous inventory before the new one replaces it. }
    LoadStringsFromFile(ExpandConstant('{app}\.clipalka-files.txt'), PreviousFiles);
  end;
  if CurStep <> ssPostInstall then Exit;
  { Keep an already-enabled startup entry pointing at the installed executable. }
  if RegQueryStringValue(HKCU, '{#MyStartupKey}',
    '{#MyAppName}', StartupCommand) and (StartupCommand <> '') then
    RegWriteStringValue(HKCU, '{#MyStartupKey}',
      '{#MyAppName}', '"' + ExpandConstant('{app}\{#MyAppExeName}') + '"');
  if not LoadStringsFromFile(ExpandConstant('{app}\.clipalka-files.txt'), CurrentFiles) then Exit;

  for I := 0 to GetArrayLength(PreviousFiles) - 1 do begin
    RelativePath := ManifestPath(PreviousFiles[I]);
    if SafeOwnedPath(RelativePath) then begin
      StillInstalled := False;
      for J := 0 to GetArrayLength(CurrentFiles) - 1 do
        if CompareText(ManifestPath(CurrentFiles[J]), RelativePath) = 0 then
          StillInstalled := True;
      if not StillInstalled then begin
        FullPath := ExpandConstant('{app}\') + RelativePath;
        ExpectedHash := Copy(PreviousFiles[I], 1, 64);
        if FileExists(FullPath) then begin
          try
            { Delete only an unchanged file owned by a previous package. }
            if CompareText(GetSHA256OfFile(FullPath), ExpectedHash) = 0 then begin
              if DeleteFile(FullPath) then
                Log('Removed obsolete application file: ' + RelativePath)
              else
                Log('Could not remove obsolete application file: ' + RelativePath);
            end else
              Log('Preserved modified application file: ' + RelativePath);
          except
            Log('Preserved unreadable application file: ' + RelativePath);
          end;
        end;
      end;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
begin
  if CurUninstallStep <> usUninstall then Exit;
  if RegQueryStringValue(HKCU, '{#MyStartupKey}',
    '{#MyAppName}', StartupCommand) then begin
    { A portable copy may have taken over startup; remove only this installation. }
    if CompareText(StartupCommand, '"' + ExpandConstant('{app}\{#MyAppExeName}') + '"') = 0 then
      RegDeleteValue(HKCU, '{#MyStartupKey}', '{#MyAppName}');
  end;
end;
