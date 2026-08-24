# ============================================================
#  P:\ Backup Script — Spiegel ausgewaehlter Ordner nach H:\P_Drive
# ============================================================

$SRC_ROOT = "P:\"
$DST_ROOT = "H:\P_Drive"

# Alle Ordner auf P:\ laden - KEINE Ausnahmen, du entscheidest selbst
$allFolders = Get-ChildItem $SRC_ROOT -Directory |
    ForEach-Object {
        $dst = Join-Path $DST_ROOT $_.Name
        $dstExists = Test-Path $dst
        $srcSize = [Math]::Round((Get-ChildItem $_.FullName -Recurse -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum).Sum / 1MB, 1)
        [PSCustomObject]@{
            Ordner               = $_.Name
            "Groesse MB"         = $srcSize
            "Backup existiert"   = if ($dstExists) { "JA" } else { "NEIN" }
            Quelle               = $_.FullName
            Ziel                 = $dst
        }
    } | Sort-Object Ordner

# GUI Auswahl
$selected = $allFolders | Out-GridView `
    -Title "Backup P:\ -> H:\P_Drive | Strg+Klick = Mehrfachauswahl | OK = Starten" `
    -PassThru

if (-not $selected) {
    Write-Host "Nichts ausgewaehlt. Abbruch." -ForegroundColor Yellow
    Read-Host "Enter druecken..."
    exit
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  BACKUP START: $($selected.Count) Ordner" -ForegroundColor Cyan
Write-Host "  Quelle : $SRC_ROOT" -ForegroundColor Cyan
Write-Host "  Ziel   : $DST_ROOT" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

$errors  = 0
$success = 0
$start   = Get-Date

foreach ($entry in $selected) {
    Write-Host ""
    Write-Host ">> $($entry.Ordner)" -ForegroundColor White
    Write-Host "   $($entry.Quelle) -> $($entry.Ziel)" -ForegroundColor DarkGray

    if (-not (Test-Path $entry.Ziel)) {
        New-Item -ItemType Directory -Path $entry.Ziel -Force | Out-Null
    }

    robocopy $entry.Quelle $entry.Ziel /MIR /R:2 /W:3 /NFL /NDL /NP /NJS /NJH | Out-Null

    if ($LASTEXITCODE -ge 8) {
        Write-Host "   FEHLER (robocopy exit $LASTEXITCODE)" -ForegroundColor Red
        $errors++
    } else {
        Write-Host "   OK" -ForegroundColor Green
        $success++
    }
}

$elapsed = [Math]::Round(((Get-Date) - $start).TotalSeconds, 1)

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  FERTIG in $elapsed Sekunden" -ForegroundColor Cyan
Write-Host "  OK: $success   FEHLER: $errors" -ForegroundColor $(if ($errors -gt 0) { "Red" } else { "Green" })
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Read-Host "Enter druecken zum Schliessen..."
