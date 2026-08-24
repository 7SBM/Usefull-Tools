# Ermittelt den Ordner, in dem dieses Skript liegt
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Löscht alle PNG-Dateien in diesem Ordner
Remove-Item -Path "$ScriptDir\*.png" -Force -ErrorAction SilentlyContinue