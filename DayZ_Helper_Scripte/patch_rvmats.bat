@echo off
chcp 65001 >nul
set "TARGET_FOLDER=P:\7SBM_data"

echo ============================================================
echo  7SBM RVMAT PATCHER (PNG -^> PAA)
echo ============================================================
echo Ziel-Ordner: %TARGET_FOLDER%
echo.

if not exist "%TARGET_FOLDER%" (
    echo [FEHLER] Der Ordner "%TARGET_FOLDER%" existiert nicht!
    echo Bitte pruefe deinen Pfad auf dem P-Drive.
    echo.
    pause
    exit /b
)

echo Starte Umschreiben der .rvmat Dateien...
echo.

powershell -NoProfile -Command ^
    "Get-ChildItem '%TARGET_FOLDER%' -Filter *.rvmat | ForEach-Object { " ^
    "   $c = Get-Content $_.FullName -Raw; " ^
    "   if ($c -match '\.png') { " ^
    "       $c -replace '\.png', '.paa' | Set-Content $_.FullName -Encoding UTF8; " ^
    "       Write-Host 'Gepatcht:' $_.Name -ForegroundColor Green " ^
    "   } " ^
    "}"

echo.
echo ============================================================
echo Fertig! Alle .png Verweise wurden durch .paa ersetzt.
echo ============================================================
pause