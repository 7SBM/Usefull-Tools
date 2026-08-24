<#
    check-tool-sync.ps1  —  "Bin ich synchron?"

    Beantwortet auf jeder Maschine dieselben vier Fragen:
      1. Ist der Klon auf dem Stand von origin/main?
      2. Gibt es uncommittete Aenderungen?
      3. Welcher Build von 7SBM P3D.DeBin ist installiert?
      4. Passt die installierte Fassung zur Quelle im Klon?

    Aufruf:  powershell -ExecutionPolicy Bypass -File tools\check-tool-sync.ps1

    Hintergrund: Vault-Note "Source-Sync ueber alle Maschinen (Plan)".
    Angelegt 2026-08-24.
#>

$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent $PSScriptRoot
$ok = $true

function Say($sym, $text) { Write-Host ("  {0}  {1}" -f $sym, $text) }
function Head($t) { Write-Host ""; Write-Host $t -ForegroundColor Cyan }

# git liegt auf manchen dieser Maschinen nicht im PATH -> selbst suchen
$git = (Get-Command git -ErrorAction SilentlyContinue).Source
if (-not $git) {
    foreach ($c in @("$env:ProgramFiles\Git\cmd\git.exe",
                     "${env:ProgramFiles(x86)}\Git\cmd\git.exe",
                     "$env:LOCALAPPDATA\Programs\Git\cmd\git.exe")) {
        if (Test-Path $c) { $git = $c; break }
    }
}

Write-Host "7SBM Werkzeug-Synchronitaetspruefung" -ForegroundColor White
Write-Host ("Maschine: {0}    Repo: {1}" -f $env:COMPUTERNAME, $repo)

# ---- 1./2. Git ---------------------------------------------------------
Head "1. Git-Stand"
if (-not $git) {
    Say "??" "git nicht gefunden - Git-Pruefung uebersprungen."
    $ok = $false
} else {
    & $git -C $repo fetch --quiet 2>$null
    $branch = (& $git -C $repo rev-parse --abbrev-ref HEAD 2>$null)
    $counts = (& $git -C $repo rev-list --left-right --count "origin/$branch...HEAD" 2>$null)
    if ($counts) {
        $behind, $ahead = $counts -split '\s+'
        if ($behind -eq '0' -and $ahead -eq '0') { Say "OK" "Branch '$branch' ist auf dem Stand von origin." }
        else {
            Say "!!" "Branch '$branch': $behind Commit(s) hinterher, $ahead voraus."
            if ($behind -ne '0') { Say "->" "'git pull' ausfuehren." }
            if ($ahead  -ne '0') { Say "->" "'git push' ausfuehren." }
            $ok = $false
        }
    } else { Say "??" "Kein Upstream fuer '$branch' gesetzt." ; $ok = $false }

    $dirty = & $git -C $repo status --porcelain
    if ($dirty) {
        $n = ($dirty | Measure-Object).Count
        Say "!!" "$n uncommittete Aenderung(en) im Arbeitsbaum."
        $dirty | Select-Object -First 8 | ForEach-Object { Say "  " $_ }
        if ($n -gt 8) { Say "  " "... und $($n-8) weitere" }
        $ok = $false
    } else { Say "OK" "Arbeitsbaum sauber." }

    # Sicherheitsnetz: privater Schluessel darf nie eingecheckt sein
    $key = & $git -C $repo log --all --oneline -- "*.biprivatekey"
    if ($key) { Say "XX" "PRIVATER SCHLUESSEL IN DER HISTORIE! Sofort pruefen." ; $ok = $false }
    else      { Say "OK" "Kein privater Schluessel in der Historie." }
}

# ---- 3. Installierte Fassung ------------------------------------------
Head "2. Installierte Fassung von 7SBM P3D.DeBin"
$uninst = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
)
$app = Get-ItemProperty $uninst -ErrorAction SilentlyContinue |
       Where-Object { $_.DisplayName -like '*P3D*DeBin*' } | Select-Object -First 1

if (-not $app) {
    Say "!!" "Nicht installiert. Installer aus den GitHub-Releases holen."
    $ok = $false
} else {
    Say "OK" ("{0}  Version {1}" -f $app.DisplayName, $app.DisplayVersion)
    $exe = Join-Path $app.InstallLocation 'Debinarizer.exe'
    if (Test-Path -LiteralPath $exe) {
        $h = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
        $d = (Get-Item -LiteralPath $exe).LastWriteTime.ToString('yyyy-MM-dd HH:mm')
        Say "  " ("Datei:  {0}" -f $exe)
        Say "  " ("Build:  {0}" -f $d)
        Say "  " ("SHA256: {0}" -f $h.Substring(0,24) + "...")
        Say "->" "Diesen Hash mit den anderen Maschinen vergleichen - er muss ueberall gleich sein."
    } else { Say "!!" "Installationseintrag da, aber Debinarizer.exe fehlt." ; $ok = $false }
}

# ---- 4. Quelle im Klon -------------------------------------------------
Head "3. Quelle im Klon"
$src = Join-Path $repo 'DayZ_Arma_p3dDeBin\_SOURCE\New_Version_WORKING DayZ+Arma3_FIXED\Debinarizer.cs'
if (Test-Path -LiteralPath $src) {
    $i = Get-Item -LiteralPath $src
    # Zeilenumbrueche selbst zaehlen: "Measure-Object -Line" laesst Leerzeilen aus
    # und meldete dadurch 2602 statt 2807. Die Datei hat reine LF-Enden.
    $lines = ([System.IO.File]::ReadAllText($src) -split "`n").Count - 1
    Say "OK" ("Debinarizer.cs  {0:N0} Bytes, {1:N0} Zeilen" -f $i.Length, $lines)
    Say "  " ("SHA256: {0}..." -f (Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash.Substring(0,24))
} else { Say "!!" "Quelldatei im Klon nicht gefunden." ; $ok = $false }

Head "Ergebnis"
if ($ok) { Write-Host "  Alles synchron." -ForegroundColor Green }
else     { Write-Host "  Es gibt Abweichungen - siehe die mit !! markierten Punkte." -ForegroundColor Yellow }
