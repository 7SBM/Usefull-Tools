# ============================================================
#  DayZ RVMAT Textur-Patcher (PNG -> PAA)
# ============================================================

$TARGET_FOLDER = "P:\7SBM_data"

if (-not (Test-Path $TARGET_FOLDER)) {
    Write-Host "FEHLER: Der Ordner $TARGET_FOLDER existiert nicht!" -ForegroundColor Red
    exit
}

# Alle .rvmat Dateien im Ordner finden
$rvmatFiles = Get-ChildItem -Path $TARGET_FOLDER -Filter *.rvmat

if ($rvmatFiles.Count -eq 0) {
    Write-Host "Keine .rvmat Dateien in $TARGET_FOLDER gefunden." -ForegroundColor Yellow
    exit
}

Write-Host "Scanne $($rvmatFiles.Count) Dateien in $TARGET_FOLDER..." -ForegroundColor Cyan
$changedCount = 0

foreach ($file in $rvmatFiles) {
    # Datei einlesen
    $content = Get-Content -Path $file.FullName -Raw
    
    # Prüfen, ob .png überhaupt drin vorkommt
    if ($content -match "\.png") {
        # Alle .png durch .paa ersetzen
        $newContent = $content -replace "\.png", ".paa"
        
        # Datei wieder abspeichern
        Set-Content -Path $file.FullName -Value $newContent -Encoding UTF8
        $changedCount++
    }
}

Write-Host "`nFertig! Es wurden $changedCount von $($rvmatFiles.Count) Dateien erfolgreich auf .paa umgeschrieben." -ForegroundColor Green