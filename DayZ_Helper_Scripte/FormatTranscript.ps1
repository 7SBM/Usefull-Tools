Add-Type -AssemblyName System.Windows.Forms

$dlg = New-Object System.Windows.Forms.OpenFileDialog
$dlg.Filter = "Textdateien (*.txt)|*.txt"

if ($dlg.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK)
{
    exit
}

$inputFile = $dlg.FileName

Write-Host "Lese Datei..."
$text = Get-Content $inputFile -Raw -Encoding UTF8

# Zeilenenden vereinheitlichen
$text = $text -replace "`r`n", "`n"
$text = $text -replace "`r", "`n"

# Zeitstempel-Zeilen entfernen
$text = $text -replace '(?m)^\s*\d{1,2}:\d{2}\s*$', ''
$text = $text -replace '(?m)^\s*\d{1,2}:\d{2}:\d{2}\s*$', ''
$text = $text -replace '(?m)^\s*\[\d{1,2}:\d{2}(?::\d{2})?\]\s*$', ''
$text = $text -replace '(?m)^\s*\(\d{1,2}:\d{2}(?::\d{2})?\)\s*$', ''
$text = $text -replace '(?m)^\s*\d{1,2}:\d{2}(?::\d{2})?\s*-->\s*\d{1,2}:\d{2}(?::\d{2})?\s*$', ''

# Zeitstempel auch mitten im Text entfernen
$text = $text -replace '\b\d{1,2}:\d{2}(?::\d{2})?\b', ''

# Alle Zeilen zu einem Fließtext verbinden
$text = $text -replace "`n", ' '

# Mehrfach-Leerzeichen entfernen
$text = $text -replace '\s{2,}', ' '

# Leerzeichen vor Satzzeichen entfernen
$text = $text -replace '\s+([.,!?;:])', '$1'

# Nach Satzende Absatz einfügen
$text = $text -replace '([.!?])\s+', '$1`r`n`r`n'

# Zu viele Leerzeilen verhindern
$text = $text -replace '(\r?\n){3,}', "`r`n`r`n"

# Anfang/Ende trimmen
$text = $text.Trim()

$outputFile = Join-Path `
    ([Environment]::GetFolderPath("Desktop")) `
    ([IO.Path]::GetFileNameWithoutExtension($inputFile) + "_CLEAN.txt")

Set-Content -Path $outputFile -Value $text -Encoding UTF8

Write-Host ""
Write-Host "Fertig:"
Write-Host $outputFile
Pause