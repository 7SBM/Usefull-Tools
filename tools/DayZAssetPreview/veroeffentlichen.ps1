# Erzeugt eine eigenstaendige "7SBM-DayZ-Tools.exe".
#
# Selbstenthaltend, damit auf dem Zielrechner keine .NET-Installation
# noetig ist. Das kostet Groesse, erspart aber die Frage, warum das
# Programm nicht startet.

$ErrorActionPreference = 'Stop'
$hier = Split-Path -Parent $MyInvocation.MyCommand.Path
$ziel = Join-Path $hier 'veroeffentlicht'

if (Test-Path $ziel) { Remove-Item $ziel -Recurse -Force }

dotnet publish (Join-Path $hier 'DzAssets.Preview\DzAssets.Preview.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $ziel

if ($LASTEXITCODE -ne 0) { throw 'dotnet publish ist fehlgeschlagen.' }

$exe = Get-ChildItem $ziel -Filter '*.exe' | Select-Object -First 1
if ($null -eq $exe) { throw 'Keine .exe im Ausgabeordner gefunden.' }

Write-Output ''
Write-Output "Fertig: $($exe.FullName)"
Write-Output ("Groesse: {0:N1} MB" -f ($exe.Length / 1MB))
