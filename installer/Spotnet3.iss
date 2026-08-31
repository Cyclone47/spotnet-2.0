; Compile using build-installer.ps1 with Inno Setup 7.1 or newer.
#ifndef PayloadDir
  #error PayloadDir must be supplied by build-installer.ps1
#endif
#define AppVersion GetVersionNumbersString(PayloadDir + "\Spotnet.exe")

[Setup]
AppId={{76851D20-501E-45B0-9869-853F814BE60E}
AppName=Spotnet 3.0 (64-bit)
AppVersion={#AppVersion}
AppPublisher=Spotnet 3.0 contributors
AppPublisherURL=https://github.com/Cyclone47/spotnet-2.0
AppSupportURL=https://github.com/Cyclone47/spotnet-2.0/issues
#ifdef SmokeTestRoot
DefaultDirName={#SmokeTestRoot}\App
#else
DefaultDirName={localappdata}\Programs\Spotnet3
#endif
DefaultGroupName=Spotnet 3.0
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0
WizardStyle=modern
DisableProgramGroupPage=yes
DisableWelcomePage=no
#ifdef SmokeTestRoot
UsePreviousAppDir=no
#else
UsePreviousAppDir=yes
#endif
OutputDir={#OutputDir}
#ifdef SmokeTestRoot
OutputBaseFilename=Spotnet-3.0-x64-Setup-smoke
CreateUninstallRegKey=no
#else
OutputBaseFilename=Spotnet-3.0-x64-Setup
#endif
SetupIconFile=..\reconstructed\Spotnet2\Spotnet\Resources\ImagesInternal\spotnet.ico
UninstallDisplayIcon={app}\Spotnet.exe
Compression=lzma2/normal
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter=Spotnet.exe
RestartApplications=no
SetupMutex=Spotnet3Setup
VersionInfoVersion={#AppVersion}
UninstallDisplayName=Spotnet 3.0 (64-bit)

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"

[Files]
Source: "{#HelperDir}\Spotnet.SetupHelper.exe"; Flags: dontcopy
Source: "{#HelperDir}\Spotnet.SetupHelper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#WebViewBootstrapper}"; DestName: "MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Spotnet.install"; DestDir: "{app}"; Flags: ignoreversion

[Run]
#ifndef SmokeTestRoot
Filename: "{app}\Spotnet.exe"; Description: "Launch Spotnet 3.0"; Flags: nowait postinstall skipifsilent unchecked; Check: ShortcutsSucceeded
#endif

; No UninstallDelete section: user profiles, backups and legacy installations are not removed.
; No extension/protocol hijacking: the user can keep their old installation while validating 3.0.

[Code]
var
  DataPage, SettingsPage: TInputOptionWizardPage;
  CustomDataPage: TInputDirWizardPage;
  CustomSettingsPage: TInputFileWizardPage;
  DataSources, SettingsSources: TArrayOfString;
  ExistingProfile: Boolean;
  Prepared: Boolean;
  ShortcutFailure: Boolean;
  Helper, DetectionFile, ReportFile, Summary: String;

function ProfileRoot: String;
begin
#ifdef SmokeTestRoot
  Result := '{#SmokeTestRoot}\Profile';
#else
  Result := ExpandConstant('{localappdata}\Spotnet3');
#endif
end;

function DesktopRoot: String;
begin
#ifdef SmokeTestRoot
  Result := '{#SmokeTestRoot}\Desktop';
#else
  Result := ExpandConstant('{userdesktop}');
#endif
end;

function ProgramsRoot: String;
begin
#ifdef SmokeTestRoot
  Result := '{#SmokeTestRoot}\Programs';
#else
  Result := ExpandConstant('{userprograms}');
#endif
end;

function ShortcutsSucceeded: Boolean;
begin
  Result := not ShortcutFailure;
end;

function ReadReport(var Text: String): Boolean;
var
  Lines: TArrayOfString;
  Index: Integer;
begin
  Result := LoadStringsFromFile(ReportFile, Lines);
  Text := '';
  if Result then
    for Index := 0 to GetArrayLength(Lines) - 1 do Text := Text + Lines[Index] + #13#10;
end;

function Quote(const Value: String): String;
begin
  { A trailing slash before a closing quote changes Windows argument parsing. }
  Result := '"' + RemoveBackslashUnlessRoot(Value) + '"';
end;

function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net472, 0);
  if not Result then
    SuppressibleMsgBox('.NET Framework 4.7.2 or later is required. Install it from Microsoft, restart Windows if requested, then run Setup again. No Spotnet files have been changed.', mbCriticalError, MB_OK, IDOK);
end;

function WebViewInstalled: Boolean;
var
  Version: String;
begin
  Result := (RegQueryStringValue(HKLM32, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
  if not Result then
    Result := (RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

procedure InitializeWizard;
var
  ExitCode, Count, Index: Integer;
  Description, Installs: String;
begin
  ExtractTemporaryFile('Spotnet.SetupHelper.exe');
  Helper := ExpandConstant('{tmp}\Spotnet.SetupHelper.exe');
  DetectionFile := ExpandConstant('{tmp}\spotnet-detection.ini');
  ReportFile := ExpandConstant('{tmp}\spotnet-migration.txt');
  if not Exec(Helper, 'detect --output ' + Quote(DetectionFile), '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
    RaiseException('Legacy profile detection failed. Setup cannot safely continue.');
  ExistingProfile := FileExists(ProfileRoot + '\Data\profile.ready');
  Count := GetIniInt('Detection', 'InstallCount', 0, 0, 1000, DetectionFile);
  for Index := 0 to Count - 1 do
    Installs := Installs + GetIniString('Detection', 'Install' + IntToStr(Index), '', DetectionFile) + #13#10;
  if Installs = '' then Installs := 'No registered older installation found. Data folders are checked separately.';
  Description := 'Detected installations:' + #13#10 + Installs + #13#10 +
    'Choose one source profile. Setup COPIES data to a separate 3.0 profile and leaves old application/data files unchanged. Your Desktop and Start Menu launch shortcuts will open 3.0. Download queues and caches are not imported.';
  DataPage := CreateInputOptionPage(wpSelectDir, 'Your Spotnet data', 'Fresh installation or migrate an existing profile', Description, True, False);
  DataPage.Add('Start fresh (or keep the existing Spotnet 3.0 profile)');
  Count := GetIniInt('Detection', 'DataCount', 0, 0, 1000, DetectionFile);
  SetArrayLength(DataSources, Count);
  for Index := 0 to Count - 1 do begin
    DataSources[Index] := GetIniString('Detection', 'Data' + IntToStr(Index), '', DetectionFile);
    DataPage.Add('Copy: ' + DataSources[Index]);
  end;
  DataPage.Add('Choose a different data folder...');
  DataPage.SelectedValueIndex := 0;
  if (Count = 1) and not ExistingProfile then DataPage.SelectedValueIndex := 1;

  CustomDataPage := CreateInputDirPage(DataPage.ID, 'Legacy data folder', 'Select the folder containing servers.xml and the databases',
    'Select only the Spotnet data folder, not a drive root or the application installation folder.', False, '');
  CustomDataPage.Add('Data folder:');
  SettingsPage := CreateInputOptionPage(CustomDataPage.ID, 'Your preferences', 'Select the settings belonging to that profile',
    'The legacy .NET user.config may live separately from the databases. Choose the matching file; profiles are never merged automatically. Leave defaults selected if unsure. Server credentials travel with servers.xml.', True, False);
  SettingsPage.Add('Use 3.0 defaults (or keep the existing 3.0 preferences)');
  Count := GetIniInt('Detection', 'SettingsCount', 0, 0, 1000, DetectionFile);
  SetArrayLength(SettingsSources, Count);
  for Index := 0 to Count - 1 do begin
    SettingsSources[Index] := GetIniString('Detection', 'Settings' + IntToStr(Index), '', DetectionFile);
    SettingsPage.Add(SettingsSources[Index]);
  end;
  SettingsPage.Add('Choose another user.config or portable settings.xml...');
  SettingsPage.SelectedValueIndex := 0;
  if (Count = 1) and not ExistingProfile then SettingsPage.SelectedValueIndex := 1;
  CustomSettingsPage := CreateInputFilePage(SettingsPage.ID, 'Legacy preferences file', 'Select user.config or settings.xml',
    'Only Spotnet 2.x/3.x settings are supported. Unsupported or malformed files stop migration without changing the original.');
  CustomSettingsPage.Add('Settings file:', 'Settings files|*.config;*.xml|All files|*.*', '.config');
  WizardForm.WelcomeLabel2.Caption := 'Install Spotnet 3.0 for this Windows user.' + #13#10#13#10 +
    'Setup detects older Spotnet installations and offers a verified, non-destructive profile copy. Existing 3.0 profiles are backed up before an upgrade.' + #13#10#13#10 +
    'Setup will ask Spotnet to exit safely and wait for it to close. Large databases require extra disk space and copying time.' + #13#10#13#10 +
    'Your existing Spotnet Desktop and Start Menu shortcuts will be updated to 3.0. Missing launch shortcuts are created.' + #13#10#13#10 +
    'Microsoft Edge WebView2 is installed if missing (internet access required). Personal data is retained on uninstall.';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if ExistingProfile then
    Result := (PageID = DataPage.ID) or (PageID = CustomDataPage.ID) or
      (PageID = SettingsPage.ID) or (PageID = CustomSettingsPage.ID)
  else if PageID = CustomDataPage.ID then
    Result := DataPage.SelectedValueIndex <> GetArrayLength(DataSources) + 1
  else if PageID = CustomSettingsPage.ID then
    Result := SettingsPage.SelectedValueIndex <> GetArrayLength(SettingsSources) + 1;
end;

function SelectedData: String;
begin
  Result := '';
  if ExistingProfile then exit;
  if DataPage.SelectedValueIndex = GetArrayLength(DataSources) + 1 then Result := CustomDataPage.Values[0]
  else if DataPage.SelectedValueIndex > 0 then Result := DataSources[DataPage.SelectedValueIndex - 1];
end;

function SelectedSettings: String;
begin
  Result := '';
  if ExistingProfile then exit;
  if SettingsPage.SelectedValueIndex = GetArrayLength(SettingsSources) + 1 then Result := CustomSettingsPage.Values[0]
  else if SettingsPage.SelectedValueIndex > 0 then Result := SettingsSources[SettingsPage.SelectedValueIndex - 1];
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Version: String;
  InstalledVersion, PackageVersion: Int64;
begin
  Result := True;
  if CurPageID = wpSelectDir then begin
    { Never install over a legacy binary tree. Same-family upgrades require our marker. }
    if FileExists(ExpandConstant('{app}\Spotnet.exe')) and not FileExists(ExpandConstant('{app}\Spotnet.install')) then begin
      SuppressibleMsgBox('Choose a separate installation folder. Setup will not overwrite a legacy or portable Spotnet installation.', mbError, MB_OK, IDOK);
      Result := False;
    end;
    if GetVersionNumbersString(ExpandConstant('{app}\Spotnet.exe'), Version) and
      StrToVersion(Version, InstalledVersion) and StrToVersion('{#AppVersion}', PackageVersion) and
      (ComparePackedVersion(InstalledVersion, PackageVersion) > 0) then begin
      SuppressibleMsgBox('A newer Spotnet version is installed here. Downgrades are not supported.', mbError, MB_OK, IDOK);
      Result := False;
    end;
  end;
  if (CurPageID = CustomDataPage.ID) and (Trim(CustomDataPage.Values[0]) = '') then Result := False;
  if (CurPageID = CustomSettingsPage.ID) and not FileExists(CustomSettingsPage.Values[0]) then Result := False;
  { Unattended first installs must opt out of migration explicitly; never guess a profile. }
  if (CurPageID = wpReady) and WizardSilent and not ExistingProfile then begin
    if ExpandConstant('{param:FRESH|0}') <> '1' then begin
      Log('Silent first installation requires /FRESH=1. Use the interactive wizard for migration.');
      Result := False;
    end else begin
      DataPage.SelectedValueIndex := 0;
      SettingsPage.SelectedValueIndex := 0;
    end;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := MemoDirInfo + NewLine + NewLine + 'Profile: ' + ProfileRoot + '\Data' + NewLine;
  if ExistingProfile then Result := Result + 'Keep current profile and create a verified pre-upgrade backup.' + NewLine
  else Result := Result + 'Data source: ' + SelectedData + NewLine + 'Preferences: ' + SelectedSettings + NewLine +
    'Empty source means fresh/default settings. Legacy files remain unchanged.' + NewLine;
  Result := Result + NewLine + 'Active download queues are not imported. Existing downloads remain at their original paths.' + NewLine +
    'Update your Spotnet Desktop and Start Menu shortcuts in place; create them if missing. Originals are backed up.' + NewLine +
    'If WebView2 is missing, its Microsoft bootstrapper will download/install the runtime.' + NewLine +
    'Uninstall removes application files, not your profile or backups.' + NewLine + MemoTasksInfo;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
  Parameters: String;
begin
  Result := '';
  if Prepared then exit;
#ifndef SmokeTestRoot
  WizardForm.StatusLabel.Caption := 'Asking Spotnet to exit safely; waiting for database writes to finish...';
  if not Exec(Helper, 'close --report ' + Quote(ReportFile), '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then begin
    if not ReadReport(Result) then Result := 'Spotnet could not be closed safely. Exit it manually and retry.';
    exit;
  end;
#endif
  if not WebViewInstalled then begin
#ifdef SmokeTestRoot
    Result := 'Smoke tests require an already installed WebView2 Runtime; they never install prerequisites.';
    exit;
#else
    WizardForm.StatusLabel.Caption := 'Installing Microsoft Edge WebView2 (internet access required)...';
    ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
    if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or
       (ExitCode <> 0) or not WebViewInstalled then begin
      Result := 'WebView2 installation did not complete. Install the Evergreen Runtime from Microsoft and retry. No Spotnet data has been migrated.';
      exit;
    end;
#endif
  end;
  WizardForm.StatusLabel.Caption := 'Copying and verifying your profile. Large databases may take several minutes...';
  Parameters := 'prepare --profile ' + Quote(ProfileRoot) + ' --report ' + Quote(ReportFile);
  if SelectedData <> '' then Parameters := Parameters + ' --source-data ' + Quote(SelectedData);
  if SelectedSettings <> '' then Parameters := Parameters + ' --source-settings ' + Quote(SelectedSettings);
  if not Exec(Helper, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then begin
    if not ReadReport(Result) then Result := 'Profile preparation failed. Close Spotnet and check available disk space and file permissions, then retry.';
    exit;
  end;
  ReadReport(Summary);
  Prepared := True;
end;

function InitializeUninstall: Boolean;
var
  ExitCode: Integer;
begin
#ifdef SmokeTestRoot
  Result := True;
#else
  Result := Exec(ExpandConstant('{app}\Spotnet.SetupHelper.exe'), 'close', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) and (ExitCode = 0);
  if not Result then
    SuppressibleMsgBox('Exit Spotnet before uninstalling. Your profile and backups will be kept.', mbError, MB_OK, IDOK);
#endif
end;

function ShortcutParameters: String;
begin
  Result := ' --profile ' + Quote(ProfileRoot) + ' --desktop ' + Quote(DesktopRoot) + ' --programs ' + Quote(ProgramsRoot);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExitCode: Integer;
begin
  if CurUninstallStep = usUninstall then begin
    { After confirmation, before application/helper files are removed. }
    if not Exec(ExpandConstant('{app}\Spotnet.SetupHelper.exe'), 'restore-shortcuts' + ShortcutParameters,
        '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
      SuppressibleMsgBox('Some shortcuts could not be restored. Their backups remain in ' + ProfileRoot + '\ShortcutBackups. Your personal data is retained.', mbError, MB_OK, IDOK);
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := 0;
  if ShortcutFailure then Result := 10;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ExitCode: Integer;
  ShortcutReport, Heading: String;
begin
  if CurStep = ssPostInstall then begin
    WizardForm.StatusLabel.Caption := 'Updating your Spotnet Desktop and Start Menu shortcuts...';
    DeleteFile(ReportFile);
    ShortcutFailure := not Exec(ExpandConstant('{app}\Spotnet.SetupHelper.exe'),
      'shortcuts' + ShortcutParameters + ' --executable ' + Quote(ExpandConstant('{app}\Spotnet.exe')) + ' --report ' + Quote(ReportFile),
      '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
    if ExitCode <> 0 then ShortcutFailure := True;
    if not ReadReport(ShortcutReport) then ShortcutReport := 'Shortcut update did not produce a report.';
    Log(ShortcutReport);
    Summary := Summary + #13#10 + ShortcutReport;
    if ShortcutFailure then
      SuppressibleMsgBox('Spotnet is installed, but shortcut setup needs attention. Fix the reported permissions/paths and rerun Setup.' + #13#10#13#10 + ShortcutReport, mbError, MB_OK, IDOK);
    Heading := 'Spotnet 3.0 (64-bit) is installed.';
    if ShortcutFailure then Heading := 'Application installed; shortcut setup needs attention.';
    WizardForm.FinishedLabel.Caption := Heading + #13#10#13#10 + Summary + #13#10#13#10 +
      'Profile: ' + ProfileRoot + '\Data' + #13#10 +
      'Keep your old installation until you have checked your provider, databases and downloads in 3.0.';
  end;
end;
