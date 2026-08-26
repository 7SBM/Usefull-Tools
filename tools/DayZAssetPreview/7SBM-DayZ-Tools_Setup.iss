; Setup fuer 7SBM-DayZ-Tools.
;
; Erwartet die veroeffentlichte Anwendung in .\veroeffentlicht\ —
; also zuerst veroeffentlichen.ps1 laufen lassen.
;
; Uebersetzen:  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" 7SBM-DayZ-Tools_Setup.iss

#define AppName "7SBM-DayZ-Tools"
#define AppVersion "1.0"
#define AppPublisher "7SpeedBlendMaster"
#define AppExeName "7SBM-DayZ-Tools.exe"

[Setup]
AppId={{7SBM-DAYZ-TOOLS-2026}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\7SBM\Asset Preview
DefaultGroupName=7SBM
DisableProgramGroupPage=yes
DisableDirPage=no
OutputDir=_RELEASE
OutputBaseFilename=7SBM-DayZ-Tools_Setup
SetupIconFile=DzAssets.Preview\7SBM-DayZ-Tools.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=classic
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Die Anwendung ist selbstenthaltend — rund 62 MB.
MinVersion=10.0

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknuepfung erstellen"
Name: "oeffnenmit"; Description: "Zum Menue ""Oeffnen mit"" fuer .p3d und .asc hinzufuegen"; \
    GroupDescription: "Dateizuordnung"

[Files]
Source: "veroeffentlicht\{#AppExeName}";            DestDir: "{app}"; Flags: ignoreversion
Source: "DzAssets.Preview\7SBM-DayZ-Tools.ico";    DestDir: "{app}"; Flags: ignoreversion
Source: "README.md";                                DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#AppName}";                Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\7SBM-DayZ-Tools.ico"
Name: "{group}\{#AppName} deinstallieren"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";        Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\7SBM-DayZ-Tools.ico"; Tasks: desktopicon

[Registry]
; Bewusst nur "Oeffnen mit" und keine Standardzuordnung: .p3d haengt in
; aller Regel schon am Object Builder, und die darf ein Setup nicht
; stillschweigend an sich reissen.
Root: HKA; Subkey: "Software\Classes\7SBM.DayZTools.p3d"; \
    ValueType: string; ValueName: ""; ValueData: "DayZ-Modell"; \
    Flags: uninsdeletekey; Tasks: oeffnenmit
Root: HKA; Subkey: "Software\Classes\7SBM.DayZTools.p3d\DefaultIcon"; \
    ValueType: string; ValueName: ""; ValueData: "{app}\7SBM-DayZ-Tools.ico"; \
    Tasks: oeffnenmit
Root: HKA; Subkey: "Software\Classes\7SBM.DayZTools.p3d\shell\open\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; \
    Tasks: oeffnenmit
Root: HKA; Subkey: "Software\Classes\.p3d\OpenWithProgids"; \
    ValueType: string; ValueName: "7SBM.DayZTools.p3d"; ValueData: ""; \
    Flags: uninsdeletevalue; Tasks: oeffnenmit

Root: HKA; Subkey: "Software\Classes\7SBM.DayZTools.asc"; \
    ValueType: string; ValueName: ""; ValueData: "ASC-Hoehenkarte"; \
    Flags: uninsdeletekey; Tasks: oeffnenmit
Root: HKA; Subkey: "Software\Classes\7SBM.DayZTools.asc\DefaultIcon"; \
    ValueType: string; ValueName: ""; ValueData: "{app}\7SBM-DayZ-Tools.ico"; \
    Tasks: oeffnenmit
Root: HKA; Subkey: "Software\Classes\7SBM.DayZTools.asc\shell\open\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; \
    Tasks: oeffnenmit
Root: HKA; Subkey: "Software\Classes\.asc\OpenWithProgids"; \
    ValueType: string; ValueName: "7SBM.DayZTools.asc"; ValueData: ""; \
    Flags: uninsdeletevalue; Tasks: oeffnenmit

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{#AppName} jetzt starten"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Der Zwischenspeicher liegt im Profil und gehoert nicht zum Programm.
; Er wird bewusst NICHT geloescht: dort stehen auch die Einstellungen
; mit den Pfaden zum Arbeitslaufwerk.
Type: files; Name: "{app}\*.log"

[Code]

const
  FarbeGrund   = $00161A21;  { #211A16 in Windows-BGR = #161A21 }
  FarbeText    = $00F0EAE6;
  FarbeAkzent  = $00FF8D4C;  { #4C8DFF }
  FarbeSchwach = $00B3A298;
  RahmenB      = 3;

var
  R0, R1, R2, R3: TPanel;

function NeuerRahmen(X, Y, W, H: Integer): TPanel;
var P: TPanel;
begin
  P := TPanel.Create(WizardForm);
  P.Parent := WizardForm;
  P.SetBounds(X, Y, W, H);
  P.BevelOuter := bvNone;
  P.BevelInner := bvNone;
  P.Color := FarbeAkzent;
  P.Visible := False;
  Result := P;
end;

procedure Einfaerben();
begin
  WizardForm.Color := FarbeGrund;
  WizardForm.Font.Color := FarbeText;
  WizardForm.Font.Name := 'Segoe UI';
  WizardForm.MainPanel.Color := FarbeGrund;
  WizardForm.InnerPage.Color := FarbeGrund;

  WizardForm.WelcomePage.Color := FarbeGrund;
  WizardForm.SelectDirPage.Color := FarbeGrund;
  WizardForm.SelectTasksPage.Color := FarbeGrund;
  WizardForm.InstallingPage.Color := FarbeGrund;
  WizardForm.FinishedPage.Color := FarbeGrund;
  WizardForm.ReadyPage.Color := FarbeGrund;

  WizardForm.PageNameLabel.Color := FarbeGrund;
  WizardForm.PageNameLabel.Font.Color := FarbeText;
  WizardForm.PageNameLabel.Font.Name := 'Segoe UI';
  WizardForm.PageNameLabel.Font.Style := [fsBold];
  WizardForm.PageNameLabel.ParentColor := False;

  WizardForm.PageDescriptionLabel.Color := FarbeGrund;
  WizardForm.PageDescriptionLabel.Font.Color := FarbeSchwach;
  WizardForm.PageDescriptionLabel.Font.Name := 'Segoe UI';
  WizardForm.PageDescriptionLabel.ParentColor := False;

  WizardForm.WelcomeLabel1.Color := FarbeGrund;
  WizardForm.WelcomeLabel1.Font.Color := FarbeAkzent;
  WizardForm.WelcomeLabel1.Font.Name := 'Segoe UI';
  WizardForm.WelcomeLabel1.Font.Size := 14;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  WizardForm.WelcomeLabel2.Color := FarbeGrund;
  WizardForm.WelcomeLabel2.Font.Color := FarbeText;
  WizardForm.WelcomeLabel2.Font.Name := 'Segoe UI';

  WizardForm.SelectDirLabel.Color := FarbeGrund;
  WizardForm.SelectDirLabel.Font.Color := FarbeText;
  WizardForm.SelectDirLabel.Font.Name := 'Segoe UI';
  WizardForm.SelectTasksLabel.Color := FarbeGrund;
  WizardForm.SelectTasksLabel.Font.Color := FarbeText;
  WizardForm.SelectTasksLabel.Font.Name := 'Segoe UI';

  WizardForm.DirEdit.Color := FarbeGrund;
  WizardForm.DirEdit.Font.Color := FarbeText;
  WizardForm.DirEdit.Font.Name := 'Segoe UI';
  WizardForm.DirBrowseButton.Font.Name := 'Segoe UI';
  WizardForm.DiskSpaceLabel.Font.Color := FarbeSchwach;
  WizardForm.DiskSpaceLabel.Font.Name := 'Segoe UI';

  WizardForm.TasksList.Color := FarbeGrund;
  WizardForm.TasksList.Font.Color := FarbeText;
  WizardForm.TasksList.Font.Name := 'Segoe UI';

  WizardForm.StatusLabel.Color := FarbeGrund;
  WizardForm.StatusLabel.Font.Color := FarbeText;
  WizardForm.StatusLabel.Font.Name := 'Segoe UI';
  WizardForm.FilenameLabel.Color := FarbeGrund;
  WizardForm.FilenameLabel.Font.Color := FarbeSchwach;
  WizardForm.FilenameLabel.Font.Name := 'Segoe UI';

  WizardForm.ReadyLabel.Color := FarbeGrund;
  WizardForm.ReadyLabel.Font.Color := FarbeText;
  WizardForm.ReadyLabel.Font.Name := 'Segoe UI';
  WizardForm.ReadyMemo.Color := FarbeGrund;
  WizardForm.ReadyMemo.Font.Color := FarbeText;
  WizardForm.ReadyMemo.Font.Name := 'Consolas';

  WizardForm.FinishedHeadingLabel.Color := FarbeGrund;
  WizardForm.FinishedHeadingLabel.Font.Color := FarbeAkzent;
  WizardForm.FinishedHeadingLabel.Font.Name := 'Segoe UI';
  WizardForm.FinishedHeadingLabel.Font.Size := 14;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
  WizardForm.FinishedLabel.Color := FarbeGrund;
  WizardForm.FinishedLabel.Font.Color := FarbeText;
  WizardForm.FinishedLabel.Font.Name := 'Segoe UI';

  WizardForm.NextButton.Font.Name := 'Segoe UI';
  WizardForm.BackButton.Font.Name := 'Segoe UI';
  WizardForm.CancelButton.Font.Name := 'Segoe UI';
end;

procedure InitializeWizard();
var CW, CH: Integer;
begin
  Einfaerben();

  CW := WizardForm.ClientWidth;
  CH := WizardForm.ClientHeight;
  R0 := NeuerRahmen(0, 0, CW, RahmenB);
  R1 := NeuerRahmen(0, CH - RahmenB, CW, RahmenB);
  R2 := NeuerRahmen(0, RahmenB, RahmenB, CH - 2 * RahmenB);
  R3 := NeuerRahmen(CW - RahmenB, RahmenB, RahmenB, CH - 2 * RahmenB);
  R0.BringToFront; R1.BringToFront; R2.BringToFront; R3.BringToFront;
end;

function UpdateReadyMemo(Space, NewLine, MemoDir, MemoPgm, MemoType,
  MemoComponents, MemoGroup, MemoTasks: String): String;
var S: String;
begin
  S := '';
  if MemoDir <> '' then S := S + MemoDir + NewLine + NewLine;
  if MemoTasks <> '' then S := S + MemoTasks + NewLine + NewLine;

  S := S + '--- Was installiert wird ---' + NewLine + NewLine;
  S := S + 'Eine eigenstaendige Anwendung mit vier Werkzeugen:' + NewLine + NewLine;
  S := S + '  Asset-Vorschau   DayZ-Modelle texturiert in 3D' + NewLine;
  S := S + '  Hoehenkarte      ASC-Raster als 3D-Relief' + NewLine;
  S := S + '  Debinarizer      ODOL nach MLOD umwandeln' + NewLine;
  S := S + '  Skripte          Hilfsskripte ansehen und ausfuehren' + NewLine + NewLine;
  S := S + 'Eine .NET-Installation ist nicht noetig; die Laufzeit' + NewLine;
  S := S + 'ist enthalten. Die Anwendung liest die entpackten' + NewLine;
  S := S + 'Gamefiles nur — sie schreibt dort nichts.' + NewLine + NewLine;
  S := S + 'Einstellungen, Protokoll und Zwischenspeicher liegen in' + NewLine;
  S := S + '%LOCALAPPDATA%\7SBM-DayZ-Tools und bleiben bei einer' + NewLine;
  S := S + 'Deinstallation erhalten.' + NewLine + NewLine;

  S := S + '--- Hinweis zur Nutzung ---' + NewLine + NewLine;
  S := S + 'Das Werkzeug dient dem Ansehen und Bearbeiten eigener' + NewLine;
  S := S + 'Terrain- und Modding-Dateien. Der Nutzer ist selbst' + NewLine;
  S := S + 'dafuer verantwortlich, dass die Verwendung mit den' + NewLine;
  S := S + 'geltenden Lizenzbedingungen und den Rechten Dritter' + NewLine;
  S := S + 'vereinbar ist.' + NewLine + NewLine;
  S := S + 'Das Skript-Modul fuehrt Skripte mit den Rechten des' + NewLine;
  S := S + 'angemeldeten Benutzers aus. Vor jedem Lauf wird gefragt' + NewLine;
  S := S + 'und gezeigt, was gestartet wird.';

  Result := S;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  Einfaerben();

  if CurPageID = wpReady then begin
    R0.Visible := True; R1.Visible := True;
    R2.Visible := True; R3.Visible := True;
  end else begin
    R0.Visible := False; R1.Visible := False;
    R2.Visible := False; R3.Visible := False;
  end;
end;
