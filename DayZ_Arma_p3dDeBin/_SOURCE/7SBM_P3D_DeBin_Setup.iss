#define AppName "7SBM P3D.DeBin"
#define AppVersion "1.9"
#define AppPublisher "7SpeedBlendMaster"
#define AppExeName "Debinarizer.exe"
#define AppDir "New_Version_WORKING DayZ+Arma3_FIXED"

[Setup]
AppId={{7SBM-P3D-DEBIN-2026-ARMA3-DAYZ}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\7SBM\P3D.DeBin
DefaultGroupName=7SBM
DisableProgramGroupPage=yes
DisableDirPage=no
OutputDir=..\_RELEASE
OutputBaseFilename=7SBM_P3D_DeBin_Setup
SetupIconFile={#AppDir}\7SBM_P3d.DeBin.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=classic
WizardImageFile=wizard_left.bmp
WizardSmallImageFile=wizard_small.bmp
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
PrivilegesRequired=admin

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknuepfung erstellen"

[Files]
Source: "{#AppDir}\Debinarizer.exe";     DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppDir}\BisDll.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppDir}\7SBM_P3d.DeBin.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppDir}\7SBM_P3d.DeBin.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppDir}\theme_loop.wav";      DestDir: "{tmp}"; Flags: dontcopy

[Icons]
Name: "{group}\{#AppName}";                Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\7SBM_P3d.DeBin.ico"
Name: "{group}\{#AppName} deinstallieren"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";        Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\7SBM_P3d.DeBin.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{#AppName} jetzt starten"; Flags: nowait postinstall skipifsilent

[Code]

procedure PlaySound(lpszName: String; hModule: Cardinal; dwFlags: Cardinal);
  external 'PlaySoundW@winmm.dll stdcall';

const
  SND_FILENAME  = $00020000;
  SND_ASYNC     = $0001;
  SND_LOOP      = $0008;
  SND_NODEFAULT = $0002;
  clTurquoise   = $00D0E040;  // CSS turquoise #40E0D0 in Windows BGR
  BorderW       = 3;

var
  B0, B1, B2, B3: TPanel;  // turquoise border strips shown on wpReady page

function NewBorderPanel(X, Y, W, H: Integer): TPanel;
var P: TPanel;
begin
  P := TPanel.Create(WizardForm);
  P.Parent  := WizardForm;
  P.SetBounds(X, Y, W, H);
  P.BevelOuter := bvNone;
  P.BevelInner := bvNone;
  P.Color   := clTurquoise;
  P.Visible := False;
  Result := P;
end;

procedure StopSound();
begin
  PlaySound('', 0, SND_NODEFAULT);
end;

procedure SetBlack();
begin
  WizardForm.Color           := clBlack;
  WizardForm.Font.Color      := clLime;
  WizardForm.MainPanel.Color := clBlack;

  WizardForm.InnerPage.Color := clBlack;

  // All named pages
  WizardForm.WelcomePage.Color    := clBlack;
  WizardForm.SelectDirPage.Color  := clBlack;
  WizardForm.SelectTasksPage.Color := clBlack;
  WizardForm.InstallingPage.Color  := clBlack;
  WizardForm.FinishedPage.Color    := clBlack;

  // Header stripe
  WizardForm.PageNameLabel.Color             := clBlack;
  WizardForm.PageNameLabel.Font.Color        := clLime;
  WizardForm.PageNameLabel.Font.Name         := 'Courier New';
  WizardForm.PageNameLabel.Font.Size         := 10;
  WizardForm.PageNameLabel.Font.Style        := [fsBold];
  WizardForm.PageNameLabel.ParentColor       := False;
  WizardForm.PageDescriptionLabel.Color      := clBlack;
  WizardForm.PageDescriptionLabel.Font.Color := clLime;
  WizardForm.PageDescriptionLabel.Font.Name  := 'Courier New';
  WizardForm.PageDescriptionLabel.ParentColor := False;

  // Labels on inner pages
  WizardForm.SelectDirLabel.Color        := clBlack;
  WizardForm.SelectDirLabel.Font.Color   := clLime;
  WizardForm.SelectDirLabel.Font.Name    := 'Courier New';
  WizardForm.SelectTasksLabel.Color      := clBlack;
  WizardForm.SelectTasksLabel.Font.Color := clLime;
  WizardForm.SelectTasksLabel.Font.Name  := 'Courier New';

  // Dir edit + button
  WizardForm.DirEdit.Color              := clBlack;
  WizardForm.DirEdit.Font.Color         := clLime;
  WizardForm.DirEdit.Font.Name          := 'Courier New';
  WizardForm.DirBrowseButton.Font.Color := clLime;
  WizardForm.DirBrowseButton.Font.Name  := 'Courier New';
  WizardForm.DiskSpaceLabel.Font.Color  := clGreen;
  WizardForm.DiskSpaceLabel.Font.Name   := 'Courier New';

  // Tasks list
  WizardForm.TasksList.Color          := clBlack;
  WizardForm.TasksList.Font.Color     := clLime;
  WizardForm.TasksList.Font.Name      := 'Courier New';

  // Progress
  WizardForm.StatusLabel.Color         := clBlack;
  WizardForm.StatusLabel.Font.Color    := clLime;
  WizardForm.StatusLabel.Font.Name     := 'Courier New';
  WizardForm.FilenameLabel.Color       := clBlack;
  WizardForm.FilenameLabel.Font.Color  := clGreen;
  WizardForm.FilenameLabel.Font.Name   := 'Courier New';

  // Welcome labels
  WizardForm.WelcomeLabel1.Color      := clBlack;
  WizardForm.WelcomeLabel1.Font.Color := clLime;
  WizardForm.WelcomeLabel1.Font.Name  := 'Courier New';
  WizardForm.WelcomeLabel1.Font.Size  := 12;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  WizardForm.WelcomeLabel2.Color      := clBlack;
  WizardForm.WelcomeLabel2.Font.Color := clLime;
  WizardForm.WelcomeLabel2.Font.Name  := 'Courier New';

  // Ready to install page
  WizardForm.ReadyPage.Color             := clBlack;
  WizardForm.ReadyLabel.Color            := clBlack;
  WizardForm.ReadyLabel.Font.Color       := clLime;
  WizardForm.ReadyLabel.Font.Name        := 'Courier New';
  WizardForm.ReadyMemo.Color             := clBlack;
  WizardForm.ReadyMemo.Font.Color        := clLime;
  WizardForm.ReadyMemo.Font.Name         := 'Courier New';

  // Finish labels
  WizardForm.FinishedHeadingLabel.Color      := clBlack;
  WizardForm.FinishedHeadingLabel.Font.Color := clLime;
  WizardForm.FinishedHeadingLabel.Font.Name  := 'Courier New';
  WizardForm.FinishedHeadingLabel.Font.Size  := 12;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
  WizardForm.FinishedLabel.Color             := clBlack;
  WizardForm.FinishedLabel.Font.Color        := clLime;
  WizardForm.FinishedLabel.Font.Name         := 'Courier New';

  // Buttons
  WizardForm.NextButton.Font.Color   := clLime;
  WizardForm.NextButton.Font.Name    := 'Courier New';
  WizardForm.BackButton.Font.Color   := clLime;
  WizardForm.BackButton.Font.Name    := 'Courier New';
  WizardForm.CancelButton.Font.Color := clLime;
  WizardForm.CancelButton.Font.Name  := 'Courier New';
end;

procedure InitializeWizard();
var CW, CH: Integer;
begin
  SetBlack();
  ExtractTemporaryFile('theme_loop.wav');
  PlaySound(ExpandConstant('{tmp}\theme_loop.wav'), 0,
    SND_FILENAME or SND_ASYNC or SND_LOOP or SND_NODEFAULT);

  // Create 4 turquoise border strips around the wizard window
  CW := WizardForm.ClientWidth;
  CH := WizardForm.ClientHeight;
  B0 := NewBorderPanel(0,          0,           CW,     BorderW);           // top
  B1 := NewBorderPanel(0,          CH - BorderW, CW,    BorderW);           // bottom
  B2 := NewBorderPanel(0,          BorderW,      BorderW, CH - 2*BorderW);  // left
  B3 := NewBorderPanel(CW - BorderW, BorderW,   BorderW, CH - 2*BorderW);  // right
  B0.BringToFront; B1.BringToFront; B2.BringToFront; B3.BringToFront;
end;

procedure DeinitializeSetup();
begin
  StopSound();
end;

function UpdateReadyMemo(Space, NewLine, MemoDir, MemoPgm, MemoType,
  MemoComponents, MemoGroup, MemoTasks: String): String;
var
  S: String;
begin
  S := '';
  if MemoDir <> '' then
    S := S + MemoDir + NewLine + NewLine;
  if MemoTasks <> '' then
    S := S + MemoTasks + NewLine + NewLine;
  S := S + '--- Haftungsausschluss & Hinweis zur Nutzung ---' + NewLine + NewLine;
  S := S + 'Dieses Tool dient ausschliesslich zu Lern-, Analyse-,' + NewLine;
  S := S + 'Forschungs- und Kompatibilitaetszwecken. Es wurde entwickelt,' + NewLine;
  S := S + 'um die Struktur von P3D-Dateien besser nachvollziehen und' + NewLine;
  S := S + 'technische Inhalte zu Ausbildungs- und Entwicklungszwecken' + NewLine;
  S := S + 'untersuchen zu koennen.' + NewLine + NewLine;
  S := S + 'Der Nutzer ist selbst dafuer verantwortlich, sicherzustellen,' + NewLine;
  S := S + 'dass die Verwendung dieses Tools mit den geltenden Gesetzen,' + NewLine;
  S := S + 'Lizenzbedingungen, Urheberrechten sowie den Rechten Dritter' + NewLine;
  S := S + 'vereinbar ist. Das Debinarisieren, Analysieren, Modifizieren' + NewLine;
  S := S + 'oder Weiterverbreiten von Dateien kann rechtlichen' + NewLine;
  S := S + 'Einschraenkungen unterliegen.' + NewLine + NewLine;
  S := S + 'Der Autor dieses Tools unterstuetzt weder Softwarepiraterie' + NewLine;
  S := S + 'noch die unerlaubte Vervielfaeltigung, Weitergabe, Ver-' + NewLine;
  S := S + 'oeffentlichung oder kommerzielle Nutzung fremder Inhalte.' + NewLine;
  S := S + 'Die Nutzung erfolgt ausschliesslich auf eigene Verantwortung.' + NewLine + NewLine;
  S := S + 'Der Autor uebernimmt keinerlei Haftung fuer Schaeden,' + NewLine;
  S := S + 'Rechtsverletzungen oder sonstige Folgen, die sich aus der' + NewLine;
  S := S + 'missbraeuchlichen Verwendung dieses Tools ergeben.';
  Result := S;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  SetBlack();
  // Show turquoise border only on the "Bereit zum Installieren" page
  if CurPageID = wpReady then begin
    B0.Visible := True; B1.Visible := True;
    B2.Visible := True; B3.Visible := True;
  end else begin
    B0.Visible := False; B1.Visible := False;
    B2.Visible := False; B3.Visible := False;
  end;
end;
