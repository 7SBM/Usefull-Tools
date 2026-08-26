# DayZ Asset Preview

Zeigt die entpackten DayZ-Assets in 3D an — als Ersatz für die fehlende
Vorschau im Terrain Builder.

Das Werkzeug läuft eigenständig. Es koppelt sich nicht an den Terrain
Builder und verändert keine Projektdateien; gedacht ist es für den zweiten
Bildschirm: hier nachsehen, wie ein Objekt aussieht und wie groß es ist,
den Namen dann im Objekt-Browser des Terrain Builder suchen.

Die Anwendung ist als **Rahmen für mehrere Werkzeuge** angelegt. Die
Asset-Vorschau ist das erste Modul; geplant sind der P3D-Debinarizer mit
eigener Oberfläche und ein Previewer für ASC-Höhenkarten.

## Bedienung

| Eingabe | Wirkung |
|---|---|
| Linke Maustaste ziehen | drehen |
| Rechte oder mittlere Maustaste ziehen | verschieben |
| Mausrad | zoomen |
| `F` | Objekt einrahmen |
| `W` | Drahtgitter |
| `G` | Bodengitter |
| `M` | Maßstabsfigur (1,80 m) |

Links der Ordnerbaum über den gesamten Bestand, darüber die Volltextsuche.
Mehrere durch Leerzeichen getrennte Wörter müssen alle im Pfad vorkommen —
`land baracke` grenzt also weiter ein als `baracke`. Die Trefferliste zeigt
Miniaturbilder, die beim ersten Mal gerendert und danach auf der Platte
zwischengespeichert werden.

Rechts stehen Maße, Dreiecksanzahl, Dateiformat und — sofern in einer
`config.cpp` gefunden — der Klassenname. Über die Detailstufe lassen sich
auch Geometrie-, Roadway- und Memory-LODs ansehen.

## Aufruf

```
"DayZ Asset Preview.exe"
"DayZ Asset Preview.exe" H:\P_Drive\DZ\plants\tree\t_betulapendula_1f.p3d
"DayZ Asset Preview.exe" --suche "wall concrete"
```

Ein `.p3d` als Argument wird sofort geöffnet, `--suche` belegt das
Suchfeld vor.

## Bestand

Beim ersten Start sucht das Programm nach einem Arbeitslaufwerk mit einem
Unterordner `DZ`. Gefunden wird üblicherweise `H:\P_Drive`. Weitere Wurzeln
lassen sich eintragen in

```
%LOCALAPPDATA%\DayZAssetPreview\settings.json
```

Der Bestand wird zwischengespeichert. Nach dem Hinzufügen neuer Modelle auf
**Neu einlesen** klicken.

Im selben Ordner liegen `log.txt` (Meldungen, auch die des P3D-Lesers) und
`thumbs\` (die Miniaturbilder).

## Bauen

```
dotnet build tools/DayZAssetPreview
dotnet test  tools/DayZAssetPreview
powershell -ExecutionPolicy Bypass -File tools/DayZAssetPreview/veroeffentlichen.ps1
```

Das Ergebnis ist eine eigenständige `.exe`; auf dem Zielrechner ist keine
.NET-Installation nötig.

Die Tests, die echte Dateien brauchen, überspringen sich, wenn kein
Arbeitslaufwerk vorhanden ist. Mit `DZ_PDRIVE` lässt sich ein abweichender
Pfad vorgeben.

## Herkunft

Das Lesen der P3D-Dateien stammt aus `BisDll`, dem Parser des
**7SBM P3D.DeBin** in diesem Repository. Der Quellcode wird nicht kopiert,
sondern direkt mitkompiliert — Korrekturen wirken in beiden Werkzeugen.
Siehe `VENDOR.md` im Wurzelverzeichnis.

Unterstützt werden **ODOL** (binarisiert, wie ausgeliefert) und **MLOD**
(debinarisiert). Auf einem gewachsenen Arbeitslaufwerk kommen beide
nebeneinander vor.

## Grenzen

- WPF 3D kennt kein Alpha-Testing. Vegetation weicht an den Blattkanten
  vom Spiel ab; der Alphakanal wird deshalb auf 0 oder 255 gerundet.
- Dargestellt wird die Diffusetextur. Normal- und Specular-Maps bleiben
  unberücksichtigt — für „wie sieht das Objekt aus" reicht das.
- Proxies werden nicht aufgelöst: ein Haus zeigt seine eigenen Flächen,
  nicht die über Proxy eingehängten Fenster und Türen.
- Die Beleuchtung ist nicht die des Spiels. Ziel sind Wiedererkennbarkeit
  und Maßstab, nicht fotorealistische Übereinstimmung.
