; Engine-only installer. Deliberately does not include the historical PRIVATE script.
#ifndef Stage
 #error Stage is required
#endif
#ifndef PayloadList
 #error PayloadList is required
#endif
#ifndef Output
 #error Output is required
#endif
#ifndef DisplayVersion
 #define DisplayVersion "1.0.0 RC1"
#endif
#ifndef NumericVersion
 #define NumericVersion "1.0.0.1"
#endif
#ifndef SemanticVersion
 #define SemanticVersion "1.0.0-rc.1"
#endif

[Setup]
AppId={code:ProductId}
AppName=OpenDC
AppVersion={#DisplayVersion}
AppVerName=OpenDC {#DisplayVersion}
AppPublisher=Core Voltage Studios
DefaultDirName={localappdata}\Programs\OpenDC{code:Suffix}
DefaultGroupName=OpenDC{code:Suffix}
DisableProgramGroupPage=yes
UsePreviousLanguage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#Output}
OutputBaseFilename=OpenDC-{#SemanticVersion}-Windows-x64-Setup
SetupIconFile={#Stage}\odc-icon.ico
UninstallDisplayIcon={app}\OpenDC.exe
UninstallDisplayName=OpenDC {#DisplayVersion}{code:Suffix}
VersionInfoVersion={#NumericVersion}
VersionInfoDescription=OpenDC {#DisplayVersion} Installer
VersionInfoProductName=OpenDC
VersionInfoCompany=Core Voltage Studios
VersionInfoProductVersion={#NumericVersion}
VersionInfoProductTextVersion={#DisplayVersion}
Compression=lzma2/normal
SolidCompression=yes
AppMutex=Local\{code:ApplicationId}
SetupMutex=Local\OpenDC.Engine.Setup{code:Suffix}
CloseApplications=no
RestartApplications=no
WizardStyle=modern
InfoBeforeFile=RC1-INSTALL-NOTICE.txt
Uninstallable=yes
SetupLogging=yes

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
#include PayloadList

[Icons]
Name: "{group}\OpenDC{code:Suffix}"; Filename: "{app}\OpenDC.exe"; WorkingDir: "{app}"; IconFilename: "{app}\odc-icon.ico"; AppUserModelID: "{code:ApplicationId}"
Name: "{autodesktop}\OpenDC{code:Suffix}"; Filename: "{app}\OpenDC.exe"; WorkingDir: "{app}"; IconFilename: "{app}\odc-icon.ico"; AppUserModelID: "{code:ApplicationId}"; Tasks: desktopicon

[Run]
Filename: "{app}\OpenDC.exe"; Description: "Launch OpenDC"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\install-profile.json"
Type: files; Name: "{app}\opendc-install.identity"
Type: files; Name: "{app}\opendc-payload.manifest"

[Code]
var Comparison: String;
    PreviousFiles: TArrayOfString;
function GetFileAttributesW(Name: String): LongWord;
  external 'GetFileAttributesW@kernel32.dll stdcall';
function Suffix(Param: String): String;
begin Result := ''; if Comparison <> '' then Result := ' - ' + Comparison; end;
function ProductId(Param: String): String;
begin Result := 'OpenDC.Engine'; if Comparison <> '' then Result := Result + '.' + Comparison; end;
function ApplicationId(Param: String): String;
begin
  { Preserve existing runtime grouping/mutex IDs; display name is OpenDC. }
  Result := 'OpenDarkColony.Private.Normal';
  if Comparison <> '' then Result := 'OpenDarkColony.Private.' + Comparison;
end;
function VersionKey: String;
begin Result := 'Software\OpenDC\Installations\' + ProductId(''); end;
function Part(var V: String): Integer;
var P: Integer; S: String;
begin P := Pos('.',V); if P=0 then P:=Length(V)+1; S:=Copy(V,1,P-1); Delete(V,1,P); Result:=StrToIntDef(S,-1); end;
function CompareVersions(A,B: String): Integer;
var I,X,Y: Integer;
begin
 Result:=0;
 for I:=1 to 4 do begin X:=Part(A); Y:=Part(B); if X>Y then begin Result:=1; exit; end; if X<Y then begin Result:=-1; exit; end; end;
end;
function Downgrade: Boolean;
var Previous: String;
begin Result:=RegQueryStringValue(HKCU,VersionKey,'HighestVersion',Previous) and (CompareVersions(Previous,'{#NumericVersion}')>0); end;
function InitializeSetup: Boolean;
var I: Integer;
begin
 Comparison:=ExpandConstant('{param:COMPARE|}'); Result:=False;
 if Length(Comparison)>32 then exit;
 for I:=1 to Length(Comparison) do if Pos(Comparison[I],'abcdefghijklmnopqrstuvwxyz0123456789-')=0 then begin
  SuppressibleMsgBox('Invalid comparison name. Use 1-32 lowercase letters, digits or hyphens.',mbError,MB_OK,IDOK); exit; end;
 if Downgrade then begin SuppressibleMsgBox('A newer OpenDC version was installed with this identity. Downgrades are blocked. Use its installer or an isolated /COMPARE=name installation.',mbError,MB_OK,IDOK); exit; end;
 Result:=True;
end;
function NonEmptyFolder(Path: String): Boolean;
var F: TFindRec;
begin
 Result:=False;
 if FindFirst(AddBackslash(Path)+'*',F) then try
  repeat if (F.Name<>'.') and (F.Name<>'..') then begin Result:=True; break; end; until not FindNext(F);
 finally FindClose(F); end;
end;
function SafeOwnedPath(Relative: String): String;
var App,Path,Check: String;
begin
 Result:=''; App:=AddBackslash(ExpandFileName(ExpandConstant('{app}')));
 if (Relative='') or (Pos(':',Relative)>0) or (Pos('..',Relative)>0) or (Relative[1]='\') or (Relative[1]='/') then exit;
 Path:=ExpandFileName(App+Relative);
 if CompareText(Copy(Path,1,Length(App)),App)<>0 then exit;
 Check:=Path;
 while Length(Check)>=Length(App)-1 do begin
  if (GetFileAttributesW(Check)<>$FFFFFFFF) and ((GetFileAttributesW(Check) and $400)<>0) then exit;
  Check:=ExtractFileDir(Check);
 end;
 Result:=Path;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Existing: AnsiString;
begin
 Result:='';
 if Downgrade then begin Result:='A newer version is registered. Downgrades are blocked.'; exit; end;
 if CheckForMutexes('Local\'+ApplicationId('')) then begin Result:='Close OpenDC before installing. Save your game first.'; exit; end;
 if (GetFileAttributesW(ExpandConstant('{app}'))<>$FFFFFFFF) and ((GetFileAttributesW(ExpandConstant('{app}')) and $400)<>0) then begin Result:='Choose an ordinary installation folder, not a link.'; exit; end;
 if NonEmptyFolder(ExpandConstant('{app}')) then begin
  if not LoadStringFromFile(ExpandConstant('{app}\opendc-install.identity'),Existing) then begin Result:='This folder is not owned by this OpenDC installer. Choose a new empty folder. Existing files will be preserved.'; exit; end;
  if String(Existing)<>ProductId('') then begin Result:='This folder belongs to a different installation. Choose its original folder or a new empty folder.'; exit; end;
 end;
 LoadStringsFromFile(ExpandConstant('{app}\opendc-payload.manifest'),PreviousFiles);
end;
procedure RemoveObsoleteOwnedFiles;
var Current: TArrayOfString; I,J,P: Integer; Relative,Path,Hash: String; Present: Boolean;
begin
 if not LoadStringsFromFile(ExpandConstant('{app}\opendc-payload.manifest'),Current) then exit;
 for I:=0 to GetArrayLength(PreviousFiles)-1 do begin
  P:=Pos('|',PreviousFiles[I]); if P<>65 then continue;
  Hash:=Copy(PreviousFiles[I],1,64); Relative:=Copy(PreviousFiles[I],66,Length(PreviousFiles[I])); Present:=False;
  for J:=0 to GetArrayLength(Current)-1 do if CompareText(Copy(Current[J],66,Length(Current[J])),Relative)=0 then begin Present:=True; break; end;
  if not Present then begin Path:=SafeOwnedPath(Relative);
   if (Path<>'') and FileExists(Path) and (CompareText(GetSHA256OfFile(Path),Hash)=0) then begin Log('Removing obsolete owned file: '+Relative); DeleteFile(Path); end;
  end;
 end;
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin
 if CurStep=ssPostInstall then begin
  if not SaveStringToFile(ExpandConstant('{app}\install-profile.json'),'{"comparison":"'+Comparison+'"}',False) then RaiseException('Could not write installation identity.');
  if not SaveStringToFile(ExpandConstant('{app}\opendc-install.identity'),ProductId(''),False) then RaiseException('Could not record installation ownership.');
  RemoveObsoleteOwnedFiles;
  if not RegWriteStringValue(HKCU,VersionKey,'HighestVersion','{#NumericVersion}') then RaiseException('Could not record version.');
 end;
end;
function InitializeUninstall: Boolean;
var Identity: AnsiString; Prefix: String;
begin
 Comparison:=''; Prefix:='OpenDC.Engine.';
 if LoadStringFromFile(ExpandConstant('{app}\opendc-install.identity'),Identity) and (Copy(String(Identity),1,Length(Prefix))=Prefix) then Comparison:=Copy(String(Identity),Length(Prefix)+1,Length(Identity));
 Result:=True;
end;
