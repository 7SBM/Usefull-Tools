# ============================================================
#  7SBM Deploy Script (INTERACTIVE INTERFACE)
#  Packt Quellen und fragt vor jedem Deploy-Schritt nach "ja/nein"
# ============================================================

$AB            = "C:\Program Files (x86)\Steam\steamapps\common\DayZ Tools\Bin\AddonBuilder\AddonBuilder.exe"
$FB            = "C:\Program Files (x86)\Steam\steamapps\common\DayZ Tools\Bin\PboUtils\FileBank.exe"
$DS            = "C:\Program Files (x86)\Steam\steamapps\common\DayZ Tools\Bin\DsUtils\DSSignFile.exe"
$PACKED        = "P:\7SBM Mod Packed\BrienZ\addons"
$PACKED_KEY    = "P:\7SBM Mod Packed\BrienZ\key"
$PRIVATEKEY    = "P:\7SBM Mod Packed\BrienZ\key\brienz.biprivatekey"
$SERVER_ROOT   = "D:\SteamLibrary\steamapps\common\DayZServer"
$SERVER        = "$SERVER_ROOT\@BrienZ\addons"
$SERVER_KEY    = "$SERVER_ROOT\@BrienZ\key"
$SERVER_GLOBAL = "$SERVER_ROOT\keys"
$STARTBAT      = "$SERVER_ROOT\Start.bat"

# --- Sonderregeln: Prefix und Tool ---
$specialPrefix = @{
    "7SBM_data" = "brienz\data\layers"
}
$filebank = @("7SBM_World", "navmesh")

# --- PRE-PACK SYNC: Quelldaten automatisch aktualisieren ---
# Wird VOR dem Auswahldialog durchgefuehrt, damit immer der aktuelle Stand gepackt wird.

# 7SBM_data: rvmat-Dateien aus P:\brienz\data\layers\ ins Root von P:\7SBM_data\ kopieren
# Das WRP referenziert Pfade als "brienz\data\layers\*.rvmat" -- die Dateien muessen im Root liegen.
$layerSrc  = "P:\brienz\data\layers"
$layerDst  = "P:\7SBM_data"
if (Test-Path $layerSrc) {
    $rvmatFiles = Get-ChildItem $layerSrc -File -Filter "*.rvmat"
    if ($rvmatFiles.Count -gt 0) {
        Write-Host "[PRE-SYNC] Kopiere $($rvmatFiles.Count) rvmat von $layerSrc -> $layerDst ..." -ForegroundColor DarkCyan
        $rvmatFiles | Copy-Item -Destination $layerDst -Force
        Write-Host "           Fertig." -ForegroundColor DarkCyan
    }
    $paaFiles = Get-ChildItem $layerSrc -File -Filter "*.paa"
    if ($paaFiles.Count -gt 0) {
        Write-Host "[PRE-SYNC] Kopiere $($paaFiles.Count) paa von $layerSrc -> $layerDst ..." -ForegroundColor DarkCyan
        $paaFiles | Copy-Item -Destination $layerDst -Force
        Write-Host "           Fertig." -ForegroundColor DarkCyan
    }
} else {
    Write-Host "[PRE-SYNC] WARNUNG: $layerSrc nicht gefunden -- Layer-Sync uebersprungen!" -ForegroundColor Yellow
}

# 7SBM_World: brienz.wrp aus P:\brienz\ ins Root von P:\7SBM_World\ kopieren
$wrpSrc = "P:\brienz\brienz.wrp"
$wrpDst = "P:\7SBM_World\brienz.wrp"
if (Test-Path $wrpSrc) {
    $srcInfo = Get-Item $wrpSrc
    $dstInfo = if (Test-Path $wrpDst) { Get-Item $wrpDst } else { $null }
    if (-not $dstInfo -or $srcInfo.LastWriteTime -gt $dstInfo.LastWriteTime -or $srcInfo.Length -ne $dstInfo.Length) {
        Write-Host "[PRE-SYNC] Kopiere brienz.wrp ($([Math]::Round($srcInfo.Length/1MB,1)) MB) -> $wrpDst ..." -ForegroundColor DarkCyan
        Copy-Item $wrpSrc $wrpDst -Force
        Write-Host "           Fertig." -ForegroundColor DarkCyan
    } else {
        Write-Host "[PRE-SYNC] brienz.wrp ist aktuell -- kein Copy noetig." -ForegroundColor DarkGray
    }
} else {
    Write-Host "[PRE-SYNC] WARNUNG: $wrpSrc nicht gefunden -- WRP-Sync uebersprungen!" -ForegroundColor Yellow
}

Write-Host ""

# --- Alle packbaren Ordner auf P:\ finden ---
$skip = @("7SBM Mod Packed", "7SBM_Animation Projekt")
$sources = Get-ChildItem "P:\" -Directory |
    Where-Object { ($_.Name -match "^7SBM" -or $_.Name -eq "navmesh") -and $skip -notcontains $_.Name } |
    ForEach-Object {
        $name   = $_.Name
        $prefix = if ($specialPrefix.ContainsKey($name)) { $specialPrefix[$name] } else { $name }
        $tool   = if ($filebank -contains $name) { "FileBank" } else { "AddonBuilder -packonly" }
        [PSCustomObject]@{
            Ordner = $name
            Prefix = $prefix
            Tool   = $tool
            Pfad   = $_.FullName
        }
    }

# --- Auswahl per GUI ---
Write-Host "Ordner-Auswahl wird geoeffnet..." -ForegroundColor Cyan
$selected = $sources | Out-GridView -Title "Welche Ordner packen? (Strg+Klick = Mehrfachauswahl, dann OK)" -PassThru

if (-not $selected) {
    Write-Host "Nichts ausgewaehlt. Abbruch." -ForegroundColor Yellow
    exit
}

# --- Binarisierung: zweiter GridView nur fuer AddonBuilder-Eintraege ---
$abEntries = $selected | Where-Object { $_.Tool -eq "AddonBuilder -packonly" }
$binarize  = @()
if ($abEntries) {
    Write-Host "Binarisierungs-Auswahl wird geoeffnet..." -ForegroundColor Cyan
    $binarize = $abEntries | Out-GridView `
        -Title "Welche davon BINARISIERT packen? (Strg+Klick = Mehrfachauswahl; leer lassen = alle packonly)" `
        -PassThru
}
$binarizeNames = $binarize | ForEach-Object { $_.Ordner }

Write-Host "`n=== PACK PHASE ===" -ForegroundColor Cyan

foreach ($entry in $selected) {
    $isBinarize = $binarizeNames -contains $entry.Ordner
    $modeLabel  = if ($isBinarize) { "AddonBuilder BINARIZE" } else { $entry.Tool }
    Write-Host "`n>> $($entry.Ordner) [$modeLabel] Prefix: $($entry.Prefix)" -ForegroundColor White

    # AddonBuilder Temp loeschen
    $tempName = $entry.Ordner.ToLower()
    Remove-Item "$env:TEMP\$tempName" -Recurse -Force -ErrorAction SilentlyContinue

    if ($entry.Tool -eq "FileBank") {
        $proc = Start-Process -FilePath $FB `
            -ArgumentList "-dst `"$PACKED`" `"$($entry.Pfad)`"" `
            -Wait -PassThru -NoNewWindow
    } elseif ($isBinarize) {
        $proc = Start-Process -FilePath $AB `
            -ArgumentList "`"$($entry.Pfad)`" `"$PACKED`" `"-prefix=$($entry.Prefix)`"" `
            -Wait -PassThru -NoNewWindow
    } else {
        $proc = Start-Process -FilePath $AB `
            -ArgumentList "`"$($entry.Pfad)`" `"$PACKED`" -packonly `"-prefix=$($entry.Prefix)`"" `
            -Wait -PassThru -NoNewWindow
    }

    if ($proc.ExitCode -eq 0) {
        # AddonBuilder meldet manchmal ExitCode 0 obwohl der Copy-Schritt fehlschlug.
        # Fallback: PBO direkt aus Temp kopieren falls noetig.
        $tempPbo = "$env:TEMP\$($entry.Ordner).pbo"
        $dstPbo  = "$PACKED\$($entry.Ordner).pbo"
        if ((Test-Path $tempPbo) -and (Test-Path $dstPbo)) {
            $tempInfo = Get-Item $tempPbo
            $dstInfo  = Get-Item $dstPbo
            if ($tempInfo.LastWriteTime -gt $dstInfo.LastWriteTime -or $tempInfo.Length -ne $dstInfo.Length) {
                Write-Host "   Fallback-Copy aus Temp..." -ForegroundColor Yellow
                try { Copy-Item $tempPbo $dstPbo -Force; Write-Host "   Fallback OK" -ForegroundColor Green }
                catch { Write-Host "   Fallback fehlgeschlagen: $_" -ForegroundColor Red }
            }
        } elseif (Test-Path $tempPbo) {
            Write-Host "   Ziel-PBO existiert nicht, kopiere aus Temp..." -ForegroundColor Yellow
            try { Copy-Item $tempPbo $dstPbo -Force; Write-Host "   OK" -ForegroundColor Green }
            catch { Write-Host "   Fehler beim Kopieren: $_" -ForegroundColor Red }
        }
        Write-Host "   OK" -ForegroundColor Green
    } else {
        Write-Host "   FEHLER (ExitCode $($proc.ExitCode))" -ForegroundColor Red
    }
}

# --- SIGNIEREN (nur was fehlt oder veraltet ist) ---
Write-Host "`n=== SIGN PHASE ===" -ForegroundColor Cyan
if (Test-Path $PRIVATEKEY) {
    foreach ($pbo in (Get-ChildItem $PACKED -Filter *.pbo)) {
        $bisign = "$($pbo.FullName).brienz.bisign"
        $needsSign = (-not (Test-Path $bisign)) -or ((Get-Item $bisign).LastWriteTime -lt $pbo.LastWriteTime)
        if (-not $needsSign) {
            Write-Host "   OK (aktuell): $($pbo.Name)" -ForegroundColor DarkGray
            continue
        }
        Remove-Item $bisign -Force -ErrorAction SilentlyContinue
        $proc = Start-Process -FilePath $DS `
            -ArgumentList "`"$PRIVATEKEY`" `"$($pbo.FullName)`"" `
            -Wait -PassThru -NoNewWindow
        if ($proc.ExitCode -eq 0) {
            Write-Host "   Signiert: $($pbo.Name)" -ForegroundColor Green
        } else {
            Write-Host "   FEHLER: $($pbo.Name) (ExitCode $($proc.ExitCode))" -ForegroundColor Red
        }
    }
} else {
    Write-Host "   FEHLER: Private Key nicht gefunden unter $PRIVATEKEY" -ForegroundColor Red
}

# --- bikey in key-Ordner kopieren (falls neu generiert) ---
$bikey = Get-ChildItem $PACKED -Filter *.bikey -ErrorAction SilentlyContinue
if ($bikey) {
    foreach ($k in $bikey) {
        Move-Item $k.FullName $PACKED_KEY -Force
        Write-Host "   bikey verschoben: $($k.Name) -> $PACKED_KEY" -ForegroundColor Green
    }
}

# --- ABFRAGE: Server killen ---
Write-Host ""
$chooseKill = Read-Host "[?] Soll der DayZ-Server gestoppt/gekillt werden? (ja/nein)"
if ($chooseKill -match "^(j|ja|y|yes)$") {
    Write-Host "`n=== SERVER STOP ===" -ForegroundColor Cyan
    taskkill /F /IM DayZServer_x64.exe 2>$null
    Start-Sleep -Seconds 2
    Write-Host "   Server gestoppt." -ForegroundColor Green
} else {
    Write-Host "   -> Server-Stopp uebersprungen." -ForegroundColor Yellow
}

# --- ABFRAGE: Mod auf Server spiegeln ---
Write-Host ""
$chooseCopy = Read-Host "[?] Soll P:\7SBM Mod Packed\BrienZ auf den Server gespiegelt werden? (ja/nein)"
if ($chooseCopy -match "^(j|ja|y|yes)$") {
    Write-Host "`n=== MOD MIRROR -> @BrienZ ===" -ForegroundColor Cyan
    $modSrc = "P:\7SBM Mod Packed\BrienZ"

    $mirrorTargets = @(
        "$SERVER_ROOT\@BrienZ",
        "D:\SteamLibrary\steamapps\common\DayZServer_Export_NavProto\@BrienZ"
    )

    foreach ($modDst in $mirrorTargets) {
        robocopy $modSrc $modDst /MIR /NFL /NDL /NP
        Write-Host "   Gespiegelt: $modSrc -> $modDst" -ForegroundColor Green
    }

    # bikey in globalen Server-Keys-Ordner spiegeln (nur Game Server)
    robocopy "$SERVER_ROOT\@BrienZ\key" $SERVER_GLOBAL /MIR /NFL /NDL /NP | Out-Null
    Write-Host "   keys gespiegelt -> $SERVER_GLOBAL" -ForegroundColor Green
} else {
    Write-Host "   -> Spiegeln uebersprungen." -ForegroundColor Yellow
}

# --- MISSION SYNC (automatisch) ---
Write-Host "`n=== MISSION SYNC ===" -ForegroundColor Cyan
$missionSrc = "P:\7SBM_ce"
$missionDst = "$SERVER_ROOT\mpmissions\empty.brienz"
if (Test-Path $missionSrc) {
    robocopy $missionSrc $missionDst /MIR /XD "storage_-1" "storage_0" "storage_1" "Exports and navmesh" /NFL /NDL /NP
    Write-Host "   $missionSrc -> $missionDst" -ForegroundColor Green
} else {
    Write-Host "   FEHLER: $missionSrc nicht gefunden!" -ForegroundColor Red
}

# --- ABFRAGE: Server starten ---
Write-Host ""
$chooseStart = Read-Host "[?] Soll der DayZ-Server jetzt gestartet werden? (ja/nein)"
if ($chooseStart -match "^(j|ja|y|yes)$") {
    Write-Host "`n=== SERVER START ===" -ForegroundColor Cyan
    if (Test-Path $STARTBAT) {
        Start-Process $STARTBAT -WorkingDirectory (Split-Path $STARTBAT)
        Write-Host "   Start.bat ausgefuehrt" -ForegroundColor Green
    } else {
        Write-Host "   FEHLER: Start.bat nicht gefunden unter $STARTBAT" -ForegroundColor Red
    }
} else {
    Write-Host "   -> Server-Start uebersprungen." -ForegroundColor Yellow
}

Write-Host "`nScript-Durchlauf beendet!" -ForegroundColor Cyan
