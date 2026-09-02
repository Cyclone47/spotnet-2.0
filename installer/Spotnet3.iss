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
AppPublisherURL=https://github.com/Cyclone47/spotnet-3.0
AppSupportURL=https://github.com/Cyclone47/spotnet-3.0/issues
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
; Authenticode signing is opt-in: build-installer.ps1 defines SignSetup and supplies the
; named "spotnet" tool only when a certificate was given. Without it the compiler must
; not know about a sign tool at all, or it refuses to build.
#ifdef SignSetup
SignTool=spotnet
; The uninstaller is a separate executable and gets its own warning if left unsigned.
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"

[CustomMessages]
english.LaunchSpotnet=Launch Spotnet 3.0
dutch.LaunchSpotnet=Spotnet 3.0 starten
english.DotNetRequired=.NET Framework 4.7.2 or later is required. Install it from Microsoft, restart Windows if requested, then run Setup again. No Spotnet files have been changed.
english.DotNetRuntimeFailed=Installing the .NET 8 Desktop Runtime did not complete. Install it from Microsoft and run Setup again. No Spotnet data has been migrated.
english.StatusDotNet=Installing the .NET 8 Desktop Runtime (internet access required)...
dutch.DotNetRequired=.NET Framework 4.7.2 of nieuwer is vereist. Installeer dit via Microsoft, start Windows opnieuw op indien gevraagd en voer Setup daarna opnieuw uit. Er zijn geen Spotnet-bestanden gewijzigd.
dutch.DotNetRuntimeFailed=De installatie van de .NET 8 Desktop Runtime is niet voltooid. Installeer deze via Microsoft en voer Setup opnieuw uit. Er zijn geen Spotnet-gegevens gemigreerd.
dutch.StatusDotNet=.NET 8 Desktop Runtime installeren (internettoegang vereist)...
english.DetectionFailed=Legacy profile detection failed. Setup cannot safely continue.
dutch.DetectionFailed=Het zoeken naar oudere profielen is mislukt. Setup kan niet veilig doorgaan.
english.NoOldInstall=No registered older installation found. Data folders are checked separately.
dutch.NoOldInstall=Geen geregistreerde oudere installatie gevonden. Gegevensmappen worden afzonderlijk gecontroleerd.
english.DetectedInstalls=Detected installations:
dutch.DetectedInstalls=Gevonden installaties:
english.SourceDescription=Choose one source profile. Setup COPIES data to a separate 3.0 profile and leaves old application/data files unchanged. Your Desktop and Start Menu launch shortcuts will open 3.0. Download queues and caches are not imported.
dutch.SourceDescription=Kies één bronprofiel. Setup KOPIEERT gegevens naar een afzonderlijk 3.0-profiel en laat oude programma- en gegevensbestanden ongewijzigd. Snelkoppelingen op het bureaublad en in het Startmenu openen voortaan 3.0. Downloadwachtrijen en caches worden niet geïmporteerd.
english.DataTitle=Your Spotnet data
dutch.DataTitle=Uw Spotnet-gegevens
english.DataSubtitle=Fresh installation or migrate an existing profile
dutch.DataSubtitle=Nieuwe installatie of een bestaand profiel migreren
english.FreshProfile=Start fresh (or keep the existing Spotnet 3.0 profile)
dutch.FreshProfile=Opnieuw beginnen (of het bestaande Spotnet 3.0-profiel behouden)
english.CopyPrefix=Copy:
dutch.CopyPrefix=Kopiëren:
english.ChooseData=Choose a different data folder...
dutch.ChooseData=Een andere gegevensmap kiezen...
english.LegacyDataTitle=Legacy data folder
dutch.LegacyDataTitle=Gegevensmap van oudere Spotnet-versie
english.LegacyDataSubtitle=Select the folder containing servers.xml and the databases
dutch.LegacyDataSubtitle=Selecteer de map met servers.xml en de databases
english.LegacyDataDescription=Select only the Spotnet data folder, not a drive root or the application installation folder.
dutch.LegacyDataDescription=Selecteer alleen de Spotnet-gegevensmap, niet de hoofdmap van een schijf of de installatiemap van het programma.
english.DataFolder=Data folder:
dutch.DataFolder=Gegevensmap:
english.LanguageTitle=Language
dutch.LanguageTitle=Taal
english.LanguageSubtitle=Choose the language Spotnet starts in
dutch.LanguageSubtitle=Kies de taal waarin Spotnet start
english.LanguageDescription=This sets the language of the application itself. You can change it later from Edit, Language.
dutch.LanguageDescription=Dit stelt de taal van het programma zelf in. U kunt dit later wijzigen via Bewerken, Taal.
english.LanguageDutch=Nederlands
dutch.LanguageDutch=Nederlands
english.LanguageEnglish=English
dutch.LanguageEnglish=Engels
english.StyleTitle=Style
dutch.StyleTitle=Stijl
english.StyleSubtitle=Choose how Spotnet looks
dutch.StyleSubtitle=Kies hoe Spotnet eruitziet
english.StyleDescription=Each preview shows the filter list and its icons. You can change the style later from Edit, Style.
dutch.StyleDescription=Elke voorbeeldweergave toont de filterlijst en de bijbehorende pictogrammen. U kunt de stijl later wijzigen via Bewerken, Stijl.
english.StyleModernLight=Modern (light)
dutch.StyleModernLight=Modern (licht)
english.StyleModernDark=Modern (dark)
dutch.StyleModernDark=Modern (donker)
english.StyleClassic=Classic
dutch.StyleClassic=Klassiek
english.SettingsTitle=Your preferences
dutch.SettingsTitle=Uw voorkeuren
english.SettingsSubtitle=Select the settings belonging to that profile
dutch.SettingsSubtitle=Selecteer de instellingen die bij dit profiel horen
english.SettingsDescription=The legacy .NET user.config may live separately from the databases. Choose the matching file; profiles are never merged automatically. Leave defaults selected if unsure. Server credentials travel with servers.xml.
dutch.SettingsDescription=Het oudere .NET-bestand user.config kan los van de databases staan. Kies het bijbehorende bestand; profielen worden nooit automatisch samengevoegd. Laat bij twijfel de standaardkeuze staan. Servergegevens staan in servers.xml.
english.DefaultSettings=Use 3.0 defaults (or keep the existing 3.0 preferences)
dutch.DefaultSettings=Standaardinstellingen van 3.0 gebruiken (of bestaande 3.0-voorkeuren behouden)
english.ChooseSettings=Choose another user.config or portable settings.xml...
dutch.ChooseSettings=Een ander user.config- of draagbaar settings.xml-bestand kiezen...
english.LegacySettingsTitle=Legacy preferences file
dutch.LegacySettingsTitle=Voorkeurenbestand van oudere Spotnet-versie
english.LegacySettingsSubtitle=Select user.config or settings.xml
dutch.LegacySettingsSubtitle=Selecteer user.config of settings.xml
english.LegacySettingsDescription=Only Spotnet 2.x/3.x settings are supported. Unsupported or malformed files stop migration without changing the original.
dutch.LegacySettingsDescription=Alleen Spotnet 2.x/3.x-instellingen worden ondersteund. Bij een niet-ondersteund of ongeldig bestand stopt de migratie zonder het origineel te wijzigen.
english.SettingsFile=Settings file:
dutch.SettingsFile=Instellingenbestand:
english.SettingsFilter=Settings files|*.config;*.xml|All files|*.*
dutch.SettingsFilter=Instellingenbestanden|*.config;*.xml|Alle bestanden|*.*
english.Welcome1=Install Spotnet 3.0 for this Windows user.
dutch.Welcome1=Spotnet 3.0 voor deze Windows-gebruiker installeren.
english.Welcome2=Setup detects older Spotnet installations and offers a verified, non-destructive profile copy. Existing 3.0 profiles are backed up before an upgrade.
dutch.Welcome2=Setup zoekt oudere Spotnet-installaties en biedt een gecontroleerde, niet-destructieve kopie van het profiel aan. Van bestaande 3.0-profielen wordt vóór een upgrade een back-up gemaakt.
english.Welcome3=Setup will ask Spotnet to exit safely and wait for it to close. Large databases require extra disk space and copying time.
dutch.Welcome3=Setup vraagt Spotnet veilig af te sluiten en wacht tot het programma is gestopt. Grote databases vereisen extra schijfruimte en kopieertijd.
english.Welcome4=Your existing Spotnet Desktop and Start Menu shortcuts will be updated to 3.0. Missing launch shortcuts are created.
dutch.Welcome4=Bestaande Spotnet-snelkoppelingen op het bureaublad en in het Startmenu worden bijgewerkt naar 3.0. Ontbrekende snelkoppelingen worden aangemaakt.
english.Welcome5=The .NET 8 Desktop Runtime and Microsoft Edge WebView2 are installed if missing (internet access required). Personal data is retained on uninstall.
dutch.Welcome5=De .NET 8 Desktop Runtime en Microsoft Edge WebView2 worden geïnstalleerd als ze ontbreken (internettoegang vereist). Persoonlijke gegevens blijven behouden bij verwijderen.
english.SeparateFolder=Choose a separate installation folder. Setup will not overwrite a legacy or portable Spotnet installation.
dutch.SeparateFolder=Kies een afzonderlijke installatiemap. Setup overschrijft geen oudere of draagbare Spotnet-installatie.
english.NoDowngrade=A newer Spotnet version is installed here. Downgrades are not supported.
dutch.NoDowngrade=Hier is een nieuwere Spotnet-versie geïnstalleerd. Downgraden wordt niet ondersteund.
english.ProfileLabel=Profile:
dutch.ProfileLabel=Profiel:
english.KeepProfile=Keep current profile and create a verified pre-upgrade backup.
dutch.KeepProfile=Huidig profiel behouden en vóór de upgrade een gecontroleerde back-up maken.
english.DataSource=Data source:
dutch.DataSource=Gegevensbron:
english.Preferences=Preferences:
dutch.Preferences=Voorkeuren:
english.EmptySource=Empty source means fresh/default settings. Legacy files remain unchanged.
dutch.EmptySource=Een lege bron betekent een nieuw profiel met standaardinstellingen. Oudere bestanden blijven ongewijzigd.
english.QueueNotice=Active download queues are not imported. Existing downloads remain at their original paths.
dutch.QueueNotice=Actieve downloadwachtrijen worden niet geïmporteerd. Bestaande downloads blijven op hun oorspronkelijke locatie.
english.ShortcutNotice=Update your Spotnet Desktop and Start Menu shortcuts in place; create them if missing. Originals are backed up.
dutch.ShortcutNotice=Bestaande Spotnet-snelkoppelingen op het bureaublad en in het Startmenu worden bijgewerkt; ontbrekende worden aangemaakt. Van originelen wordt een back-up gemaakt.
english.WebViewNotice=If the .NET 8 Desktop Runtime or WebView2 is missing, Microsoft's own installer will add it.
dutch.WebViewNotice=Als de .NET 8 Desktop Runtime of WebView2 ontbreekt, installeert Microsofts eigen installatieprogramma die alsnog.
english.UninstallNotice=Uninstall removes application files, not your profile or backups.
dutch.UninstallNotice=Verwijderen wist programmabestanden, maar niet uw profiel of back-ups.
english.StatusClosing=Asking Spotnet to exit safely; waiting for database writes to finish...
dutch.StatusClosing=Spotnet wordt veilig afgesloten; wachten tot databasebewerkingen gereed zijn...
english.ProgressTitle=Preparing Spotnet 3.0
dutch.ProgressTitle=Spotnet 3.0 voorbereiden
english.ProgressDescription=Setup is working. This can take several minutes for a large profile.
dutch.ProgressDescription=Setup is bezig. Bij een groot profiel kan dit enkele minuten duren.
english.ProgressDetail=Please wait; Setup has not stopped responding.
dutch.ProgressDetail=Even geduld; Setup reageert nog en werkt verder.
english.CloseFailed=Spotnet could not be closed safely. Exit it manually and retry.
dutch.CloseFailed=Spotnet kon niet veilig worden afgesloten. Sluit het handmatig af en probeer opnieuw.
english.StatusWebView=Installing Microsoft Edge WebView2 (internet access required)...
dutch.StatusWebView=Microsoft Edge WebView2 installeren (internettoegang vereist)...
english.WebViewFailed=WebView2 installation did not complete. Install the Evergreen Runtime from Microsoft and retry. No Spotnet data has been migrated.
dutch.WebViewFailed=De installatie van WebView2 is niet voltooid. Installeer de Evergreen Runtime via Microsoft en probeer opnieuw. Er zijn geen Spotnet-gegevens gemigreerd.
english.StatusProfile=Copying and verifying your profile. Large databases may take several minutes...
dutch.StatusProfile=Uw profiel kopiëren en controleren. Grote databases kunnen enkele minuten duren...
english.ProfileFailed=Profile preparation failed. Close Spotnet and check available disk space and file permissions, then retry.
dutch.ProfileFailed=Het voorbereiden van het profiel is mislukt. Sluit Spotnet, controleer beschikbare schijfruimte en bestandsrechten en probeer opnieuw.
english.UninstallClose=Exit Spotnet before uninstalling. Your profile and backups will be kept.
dutch.UninstallClose=Sluit Spotnet af voordat u het verwijdert. Uw profiel en back-ups blijven behouden.
english.RestoreShortcutFailed=Some shortcuts could not be restored. Their backups remain in
dutch.RestoreShortcutFailed=Sommige snelkoppelingen konden niet worden hersteld. De back-ups blijven staan in
english.DataRetained=Your personal data is retained.
dutch.DataRetained=Uw persoonlijke gegevens blijven behouden.
english.StatusShortcuts=Updating your Spotnet Desktop and Start Menu shortcuts...
dutch.StatusShortcuts=Spotnet-snelkoppelingen op het bureaublad en in het Startmenu bijwerken...
english.ShortcutReportMissing=Shortcut update did not produce a report.
dutch.ShortcutReportMissing=Het bijwerken van snelkoppelingen heeft geen rapport opgeleverd.
english.ShortcutDone=Spotnet shortcuts were updated or created. Originals were stored safely.
dutch.ShortcutDone=Spotnet-snelkoppelingen zijn bijgewerkt of aangemaakt. Originelen zijn veilig opgeslagen.
english.ShortcutAttention=Spotnet is installed, but shortcut setup needs attention. Fix the reported permissions/paths and rerun Setup.
dutch.ShortcutAttention=Spotnet is geïnstalleerd, maar de snelkoppelingen vereisen aandacht. Corrigeer de gemelde rechten of paden en voer Setup opnieuw uit.
english.Installed=Spotnet 3.0 (64-bit) is installed.
dutch.Installed=Spotnet 3.0 (64-bit) is geïnstalleerd.
english.InstalledAttention=Application installed; shortcut setup needs attention.
dutch.InstalledAttention=Programma geïnstalleerd; de snelkoppelingen vereisen aandacht.
english.CheckOld=Keep your old installation until you have checked your provider, databases and downloads in 3.0.
dutch.CheckOld=Behoud uw oude installatie totdat u uw provider, databases en downloads in 3.0 hebt gecontroleerd.

[Files]
Source: "{#HelperDir}\Spotnet.SetupHelper.exe"; Flags: dontcopy
Source: "{#HelperDir}\Spotnet.SetupHelper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PreviewDir}\style-modern-light.bmp"; Flags: dontcopy
Source: "{#PreviewDir}\style-modern-dark.bmp"; Flags: dontcopy
Source: "{#PreviewDir}\style-classic.bmp"; Flags: dontcopy
Source: "{#WebViewBootstrapper}"; DestName: "MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy
Source: "{#DotNetBootstrapper}"; DestName: "windowsdesktop-runtime.exe"; Flags: dontcopy
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Spotnet.install"; DestDir: "{app}"; Flags: ignoreversion

[Run]
#ifndef SmokeTestRoot
Filename: "{app}\Spotnet.exe"; Description: "{cm:LaunchSpotnet}"; Flags: nowait postinstall skipifsilent unchecked; Check: ShortcutsSucceeded
#endif

; No UninstallDelete section: user profiles, backups and legacy installations are not removed.
; No extension/protocol hijacking: the user can keep their old installation while validating 3.0.

[Code]
var
  DataPage, SettingsPage, LanguagePage: TInputOptionWizardPage;
  StylePage: TWizardPage;
  StyleButtons: array[0..2] of TRadioButton;
  StyleCaption: TLabel;
  CustomDataPage: TInputDirWizardPage;
  CustomSettingsPage: TInputFileWizardPage;
  ProgressPage: TOutputProgressWizardPage;
  DataSources, SettingsSources: TArrayOfString;
  ExistingProfile: Boolean;
  Prepared: Boolean;
  ShortcutFailure: Boolean;
  Helper, DetectionFile, ReportFile, Summary: String;
  CurrentTheme, CurrentLanguage: String;

function CM(const Key: String): String;
begin
  Result := ExpandConstant('{cm:' + Key + '}');
end;

function IsDutch: Boolean;
begin
  Result := CompareText(ActiveLanguage, 'dutch') = 0;
end;

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
    SuppressibleMsgBox(CM('DotNetRequired'), mbCriticalError, MB_OK, IDOK);
end;

{ The Windows Desktop shared framework the application runs on. Present when at least
  one 8.x (or newer) version directory exists next to the dotnet host. }
function DotNetDesktopInstalled: Boolean;
var
  Root: String;
  Search: TFindRec;
  Major: Integer;
begin
  Result := False;
  Root := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(Root) then exit;
  if FindFirst(Root + '\*', Search) then begin
    try
      repeat
        if (Search.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then begin
          Major := StrToIntDef(Copy(Search.Name, 1, Pos('.', Search.Name) - 1), 0);
          if Major >= 8 then begin
            Result := True;
            exit;
          end;
        end;
      until not FindNext(Search);
    finally
      FindClose(Search);
    end;
  end;
end;

function WebViewInstalled: Boolean;
var
  Version: String;
begin
  Result := (RegQueryStringValue(HKLM32, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
  if not Result then
    Result := (RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;


{ One preview tile: the bitmap rendered from the application's own theme
  dictionaries by Spotnet.ThemePreview, with its radio button underneath. }
procedure AddStyleTile(Page: TWizardPage; Index: Integer; const FileName, Caption: String; Left: Integer);
var
  Image: TBitmapImage;
begin
  ExtractTemporaryFile(FileName);
  Image := TBitmapImage.Create(Page);
  Image.Parent := Page.Surface;
  Image.Bitmap.LoadFromFile(ExpandConstant('{tmp}\') + FileName);
  Image.Left := ScaleX(Left);
  Image.Top := ScaleY(34);
  Image.Width := ScaleX(128);
  Image.Height := ScaleY(132);
  Image.Stretch := True;

  StyleButtons[Index] := TRadioButton.Create(Page);
  StyleButtons[Index].Parent := Page.Surface;
  StyleButtons[Index].Left := ScaleX(Left);
  StyleButtons[Index].Top := ScaleY(172);
  StyleButtons[Index].Width := ScaleX(128);
  StyleButtons[Index].Caption := Caption;
end;

{ The style written into the fresh profile's user.config. Must match the values
  ThemeHelper accepts. }
function SelectedTheme: String;
begin
  if StyleButtons[1].Checked then Result := 'ModernDark'
  else if StyleButtons[2].Checked then Result := 'ClassicLight'
  else Result := 'ModernLight';
end;

function SelectedLanguage: String;
begin
  if LanguagePage.SelectedValueIndex = 1 then Result := 'en' else Result := 'nl';
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
    RaiseException(CM('DetectionFailed'));
  ExistingProfile := FileExists(ProfileRoot + '\Data\profile.ready');
  Count := GetIniInt('Detection', 'InstallCount', 0, 0, 1000, DetectionFile);
  for Index := 0 to Count - 1 do
    Installs := Installs + GetIniString('Detection', 'Install' + IntToStr(Index), '', DetectionFile) + #13#10;
  if Installs = '' then Installs := CM('NoOldInstall');
  Description := CM('DetectedInstalls') + #13#10 + Installs + #13#10 + CM('SourceDescription');
  { Onboarding: language, then style. Both sit between Welcome and the folder
    page so the choices are made before anything is written. }
  LanguagePage := CreateInputOptionPage(wpWelcome, CM('LanguageTitle'), CM('LanguageSubtitle'),
    CM('LanguageDescription'), True, False);
  LanguagePage.Add(CM('LanguageDutch'));
  LanguagePage.Add(CM('LanguageEnglish'));
  CurrentLanguage := GetIniString('Detection', 'CurrentLanguage', '', DetectionFile);
  if CurrentLanguage = 'nl' then LanguagePage.SelectedValueIndex := 0
  else if CurrentLanguage = 'en' then LanguagePage.SelectedValueIndex := 1
  else if IsDutch then LanguagePage.SelectedValueIndex := 0
  else LanguagePage.SelectedValueIndex := 1;

  StylePage := CreateCustomPage(LanguagePage.ID, CM('StyleTitle'), CM('StyleSubtitle'));
  StyleCaption := TLabel.Create(StylePage);
  StyleCaption.Parent := StylePage.Surface;
  StyleCaption.Left := 0;
  StyleCaption.Top := 0;
  StyleCaption.Width := StylePage.SurfaceWidth;
  StyleCaption.WordWrap := True;
  StyleCaption.Caption := CM('StyleDescription');
  AddStyleTile(StylePage, 0, 'style-modern-light.bmp', CM('StyleModernLight'), 0);
  AddStyleTile(StylePage, 1, 'style-modern-dark.bmp', CM('StyleModernDark'), 140);
  AddStyleTile(StylePage, 2, 'style-classic.bmp', CM('StyleClassic'), 280);
  { An upgrade opens on the style it already uses, so clicking straight through Setup
    never repaints an existing install. A fresh install opens on Modern (light). }
  CurrentTheme := GetIniString('Detection', 'CurrentTheme', '', DetectionFile);
  if CurrentTheme = 'ModernDark' then StyleButtons[1].Checked := True
  else if CurrentTheme = 'ClassicLight' then StyleButtons[2].Checked := True
  else StyleButtons[0].Checked := True;

  DataPage := CreateInputOptionPage(wpSelectDir, CM('DataTitle'), CM('DataSubtitle'), Description, True, False);
  DataPage.Add(CM('FreshProfile'));
  Count := GetIniInt('Detection', 'DataCount', 0, 0, 1000, DetectionFile);
  SetArrayLength(DataSources, Count);
  for Index := 0 to Count - 1 do begin
    DataSources[Index] := GetIniString('Detection', 'Data' + IntToStr(Index), '', DetectionFile);
    DataPage.Add(CM('CopyPrefix') + ' ' + DataSources[Index]);
  end;
  DataPage.Add(CM('ChooseData'));
  DataPage.SelectedValueIndex := 0;
  if (Count = 1) and not ExistingProfile then DataPage.SelectedValueIndex := 1;

  CustomDataPage := CreateInputDirPage(DataPage.ID, CM('LegacyDataTitle'), CM('LegacyDataSubtitle'),
    CM('LegacyDataDescription'), False, '');
  CustomDataPage.Add(CM('DataFolder'));
  SettingsPage := CreateInputOptionPage(CustomDataPage.ID, CM('SettingsTitle'), CM('SettingsSubtitle'),
    CM('SettingsDescription'), True, False);
  SettingsPage.Add(CM('DefaultSettings'));
  Count := GetIniInt('Detection', 'SettingsCount', 0, 0, 1000, DetectionFile);
  SetArrayLength(SettingsSources, Count);
  for Index := 0 to Count - 1 do begin
    SettingsSources[Index] := GetIniString('Detection', 'Settings' + IntToStr(Index), '', DetectionFile);
    SettingsPage.Add(SettingsSources[Index]);
  end;
  SettingsPage.Add(CM('ChooseSettings'));
  SettingsPage.SelectedValueIndex := 0;
  if (Count = 1) and not ExistingProfile then SettingsPage.SelectedValueIndex := 1;
  CustomSettingsPage := CreateInputFilePage(SettingsPage.ID, CM('LegacySettingsTitle'), CM('LegacySettingsSubtitle'),
    CM('LegacySettingsDescription'));
  CustomSettingsPage.Add(CM('SettingsFile'), CM('SettingsFilter'), '.config');
  WizardForm.WelcomeLabel2.Caption := CM('Welcome1') + #13#10#13#10 +
    CM('Welcome2') + #13#10#13#10 + CM('Welcome3') + #13#10#13#10 +
    CM('Welcome4') + #13#10#13#10 + CM('Welcome5');
  ProgressPage := CreateOutputProgressPage(CM('ProgressTitle'), CM('ProgressDescription'));
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
      SuppressibleMsgBox(CM('SeparateFolder'), mbError, MB_OK, IDOK);
      Result := False;
    end;
    if GetVersionNumbersString(ExpandConstant('{app}\Spotnet.exe'), Version) and
      StrToVersion(Version, InstalledVersion) and StrToVersion('{#AppVersion}', PackageVersion) and
      (ComparePackedVersion(InstalledVersion, PackageVersion) > 0) then begin
      SuppressibleMsgBox(CM('NoDowngrade'), mbError, MB_OK, IDOK);
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
  Result := MemoDirInfo + NewLine + NewLine + CM('ProfileLabel') + ' ' + ProfileRoot + '\Data' + NewLine;
  if ExistingProfile then Result := Result + CM('KeepProfile') + NewLine
  else Result := Result + CM('DataSource') + ' ' + SelectedData + NewLine + CM('Preferences') + ' ' + SelectedSettings + NewLine +
    CM('EmptySource') + NewLine;
  Result := Result + NewLine + CM('QueueNotice') + NewLine + CM('ShortcutNotice') + NewLine +
    CM('WebViewNotice') + NewLine + CM('UninstallNotice') + NewLine + MemoTasksInfo;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
  Parameters: String;
  ShowProgress: Boolean;
begin
  Result := '';
  if Prepared then exit;
  ShowProgress := not WizardSilent;
  if ShowProgress then begin
    ProgressPage.SetText(CM('StatusClosing'), CM('ProgressDetail'));
    ProgressPage.SetProgress(0, 4);
    ProgressPage.Show;
  end;
  try
#ifndef SmokeTestRoot
  if ShowProgress then ProgressPage.SetText(CM('StatusClosing'), CM('ProgressDetail'));
  if not Exec(Helper, 'close --report ' + Quote(ReportFile), '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then begin
    if IsDutch or not ReadReport(Result) then Result := CM('CloseFailed');
    exit;
  end;
#endif
  if ShowProgress then begin
    ProgressPage.SetProgress(1, 4);
    ProgressPage.SetText(CM('StatusDotNet'), CM('ProgressDetail'));
  end;
  if not DotNetDesktopInstalled then begin
#ifdef SmokeTestRoot
    Result := 'Smoke tests require an already installed .NET Desktop Runtime; they never install prerequisites.';
    exit;
#else
    ExtractTemporaryFile('windowsdesktop-runtime.exe');
    if not Exec(ExpandConstant('{tmp}\windowsdesktop-runtime.exe'), '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or
       ((ExitCode <> 0) and (ExitCode <> 3010)) or not DotNetDesktopInstalled then begin
      Result := CM('DotNetRuntimeFailed');
      exit;
    end;
#endif
  end;
  if ShowProgress then begin
    ProgressPage.SetProgress(1, 4);
    ProgressPage.SetText(CM('StatusWebView'), CM('ProgressDetail'));
  end;
  if not WebViewInstalled then begin
#ifdef SmokeTestRoot
    Result := 'Smoke tests require an already installed WebView2 Runtime; they never install prerequisites.';
    exit;
#else
    ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
    if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or
       (ExitCode <> 0) or not WebViewInstalled then begin
      Result := CM('WebViewFailed');
      exit;
    end;
#endif
  end;
  if ShowProgress then begin
    ProgressPage.SetProgress(2, 4);
    ProgressPage.SetText(CM('StatusProfile'), CM('ProgressDetail'));
  end;
  Parameters := 'prepare --profile ' + Quote(ProfileRoot) + ' --report ' + Quote(ReportFile);
  { A new profile starts in the language and style picked on the wizard's first two
    pages; an imported profile keeps whatever it already states. }
  Parameters := Parameters + ' --language ' + SelectedLanguage;
  Parameters := Parameters + ' --app-theme ' + SelectedTheme;
  if SelectedData <> '' then Parameters := Parameters + ' --source-data ' + Quote(SelectedData);
  if SelectedSettings <> '' then Parameters := Parameters + ' --source-settings ' + Quote(SelectedSettings);
  if not Exec(Helper, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then begin
    if IsDutch or not ReadReport(Result) then Result := CM('ProfileFailed');
    exit;
  end;
  ReadReport(Summary);
  if IsDutch then begin
    if ExistingProfile then Summary := CM('KeepProfile')
    else Summary := CM('EmptySource');
  end;
  if ShowProgress then begin
    ProgressPage.SetProgress(4, 4);
    ProgressPage.SetText(CM('StatusProfile'), CM('ShortcutDone'));
  end;
  Prepared := True;
  finally
    if ShowProgress then ProgressPage.Hide;
  end;
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
    SuppressibleMsgBox(CM('UninstallClose'), mbError, MB_OK, IDOK);
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
      SuppressibleMsgBox(CM('RestoreShortcutFailed') + ' ' + ProfileRoot + '\ShortcutBackups. ' + CM('DataRetained'), mbError, MB_OK, IDOK);
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := 0;
  if ShortcutFailure then Result := 10;
end;

// Removes everything a previous version put in the application directory, keeping the
// profile data, the install marker and the uninstaller. The payload is self-contained,
// so anything else is a leftover - and leftovers are not harmless: upgrading from the
// .NET Framework layout left x64\SQLite.Interop.dll behind beside the new copy under
// runtimes, which loaded a second SQLite into the process and corrupted its heap on
// the first query.
procedure CleanApplicationDirectory;
var
  Search: TFindRec;
  Target, Root: String;
begin
  Root := ExpandConstant('{app}');
  if not DirExists(Root) then exit;
  if FindFirst(Root + '\*', Search) then begin
    try
      repeat
        if (Search.Name = '.') or (Search.Name = '..') then continue;
        if CompareText(Search.Name, 'Data') = 0 then continue;
        if CompareText(Search.Name, 'Spotnet.install') = 0 then continue;
        if CompareText(Copy(Search.Name, 1, 5), 'unins') = 0 then continue;
        Target := Root + '\' + Search.Name;
        if (Search.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
          DelTree(Target, True, True, True)
        else
          DeleteFile(Target);
      until not FindNext(Search);
    finally
      FindClose(Search);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ExitCode: Integer;
  ShortcutReport, Heading: String;
begin
  if CurStep = ssInstall then CleanApplicationDirectory;
  if CurStep = ssPostInstall then begin
    WizardForm.StatusLabel.Caption := CM('StatusShortcuts');
    DeleteFile(ReportFile);
    ShortcutFailure := not Exec(ExpandConstant('{app}\Spotnet.SetupHelper.exe'),
      'shortcuts' + ShortcutParameters + ' --executable ' + Quote(ExpandConstant('{app}\Spotnet.exe')) + ' --report ' + Quote(ReportFile),
      '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
    if ExitCode <> 0 then ShortcutFailure := True;
    if not ReadReport(ShortcutReport) then ShortcutReport := CM('ShortcutReportMissing');
    if IsDutch and not ShortcutFailure then ShortcutReport := CM('ShortcutDone');
    Log(ShortcutReport);
    Summary := Summary + #13#10 + ShortcutReport;
    if ShortcutFailure then
      SuppressibleMsgBox(CM('ShortcutAttention') + #13#10#13#10 + ShortcutReport, mbError, MB_OK, IDOK);
    Heading := CM('Installed');
    if ShortcutFailure then Heading := CM('InstalledAttention');
    WizardForm.FinishedLabel.Caption := Heading + #13#10#13#10 + Summary + #13#10#13#10 +
      CM('ProfileLabel') + ' ' + ProfileRoot + '\Data' + #13#10 + CM('CheckOld');
  end;
end;
