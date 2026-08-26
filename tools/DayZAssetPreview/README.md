# DayZ Asset Preview

Eine Werkzeugsammlung für die Terrain-Arbeit an **BrienZ** — als eine
Windows-Anwendung mit vier Modulen. Sie läuft eigenständig neben Terrain
Builder und QGIS, koppelt sich an nichts und verändert keine
Projektdateien, solange man es nicht ausdrücklich verlangt.

| Modul | Zweck |
|---|---|
| **Asset-Vorschau** | DayZ-Modelle texturiert in 3D ansehen — der Ersatz für die fehlende Vorschau im Terrain Builder |
| **Höhenkarte** | ASC-Höhenkarten als 3D-Relief betrachten |
| **Debinarizer** | Binarisierte P3D (ODOL) in bearbeitbare MLOD umwandeln |
| **Skripte** | Die eigenen Hilfsskripte ansehen, bearbeiten, ausführen und ergänzen |

Neue Werkzeuge kommen als weiteres Modul dazu; die Shell braucht dafür nur
eine Zeile in `HauptFenster`.

---

## Asset-Vorschau

Ordnerbaum und Volltextsuche über den gesamten Bestand — im Test 9.646
Modelle aus `DZ` und den eigenen Mods. Die Trefferliste zeigt
Miniaturbilder, die beim ersten Mal gerendert und danach auf der Platte
zwischengespeichert werden.

Rechts stehen Maße, Dreiecksanzahl, Dateiformat und — sofern in einer
`config.cpp` gefunden — der Klassenname. Über die Detailstufe lassen sich
auch Geometrie-, Roadway- und Memory-LODs ansehen.

| Eingabe | Wirkung |
|---|---|
| Linke Maustaste ziehen | drehen |
| Rechte oder mittlere Maustaste ziehen | verschieben |
| Mausrad | zoomen |
| `F` | Objekt einrahmen |
| `W` | Drahtgitter |
| `G` | Bodengitter |
| `M` | Maßstabsfigur (1,80 m) |

Unterstützt werden **ODOL** (binarisiert, wie ausgeliefert) und **MLOD**
(debinarisiert). Auf einem gewachsenen Arbeitslaufwerk kommen beide
nebeneinander vor.

**Proxy-Platzhalter** — die Pyramide mit Pfeil, die eine Andockstelle
markiert — sind ausgeblendet, weil sie nicht zum sichtbaren Objekt gehören.
Ein Schalter blendet sie ein; die Dreiecksanzahl weist sie getrennt aus.

Verweist ein Modell auf `P:\…`, also auf das Arma-Arbeitslaufwerk, wird der
Laufwerksbuchstabe abgestreift und relativ zu den eingestellten Wurzeln
gesucht — ein Arbeitslaufwerk ist dessen Spiegel.

## Höhenkarte

Liest Esri-ASCII-Grids (`.asc`) und stellt sie als 3D-Relief dar. Die
124-MB-Karte mit 4096 × 4096 Zellen ist in gut zwei Sekunden eingelesen.

Netzfeinheit und Texturauflösung sind getrennt einstellbar: ein Netz von
512 × 512 hat gut eine halbe Million Dreiecke, während die Relieftextur mit
2048 × 2048 praktisch nichts kostet. Das Gelände sieht dadurch feiner aus,
als es vernetzt ist.

Einstellbar sind ausserdem die Farbskala (Gelände, Graustufen, Verlauf),
die Überhöhung und die Stärke der Schummerung. Angezeigt werden Rasterweite,
Ausdehnung in Metern und Kilometern, Höhenbereich und die Zahl der Lücken.

## Debinarizer

Wandelt binarisierte Modelle in die bearbeitbare Fassung — einzeln oder
als ganzer Ordner, mit Fortschritt, Abbruch und einem Bericht je Datei.

Verwendet denselben Umwandler wie das Konsolenwerkzeug **7SBM P3D.DeBin**
(`Conversion.ODOL2MLOD` aus `BisDll`). Dessen Quelldatei bleibt
unangetastet: sie ist ein ausgeliefertes Werkzeug mit eigener
Versionsnummer.

Die Umwandlung läuft **eine Datei nach der anderen**, nicht nebenläufig.
`BisDll` meldet seine Diagnose über `Console.Error`, und dieser Strom gilt
für den ganzen Prozess — gleichzeitige Umwandlungen würden ihre Meldungen
vermischen.

Nicht enthalten sind die übrigen Betriebsarten des Konsolenwerkzeugs
(ANM/RTM-Umwandlung, PBO-Entpacken). Deren Fachlogik steht dort inline in
`Main` zwischen den Konsolenausgaben und wäre nicht wiederverwendbar,
sondern neu zu schreiben. Für diese Aufgaben bleibt `Debinarizer.exe` das
Werkzeug der Wahl.

## Skripte

Findet die eigenen Hilfsskripte (`.py`, `.ps1`, `.bat`, `.cmd`), zeigt zu
jedem die Beschreibung aus seinem Kopfkommentar — bei Python der Docstring,
sonst die führenden Kommentarzeilen — und lässt sie ansehen, bearbeiten,
speichern und ausführen.

Vor dem Ausführen wird gefragt und genau gezeigt, was gestartet wird:
Programm, Arbeitsordner und Argumente. Die Ausgabe erscheint zeilenweise,
Fehlerausgaben sind mit `!` gekennzeichnet, ein Lauf lässt sich abbrechen.
Vor dem Überschreiben legt das Werkzeug eine `.bak`-Sicherung an.

Ohne eigene Einstellung wird `DayZ_Helper_Scripte` neben dem Programm
gesucht; weitere Ordner lassen sich aufnehmen.

---

## Aufruf

```
"DayZ Asset Preview.exe"
"DayZ Asset Preview.exe" H:\P_Drive\DZ\plants\tree\t_betulapendula_1f.p3d
"DayZ Asset Preview.exe" H:\BrienZ_QGIS\gtt_export\gtt_heightmap.asc
"DayZ Asset Preview.exe" --suche "wall concrete"
"DayZ Asset Preview.exe" --modul hoehenkarte
"DayZ Asset Preview.exe" --skript DayZ_TB_River_steps.py
```

Eine `.p3d` oder `.asc` als Argument wird sofort geöffnet und springt ins
passende Modul. Modulkennungen: `asset-vorschau`, `hoehenkarte`,
`debinarizer`, `skripte`.

## Einstellungen und Daten

```
%LOCALAPPDATA%\DayZAssetPreview\
    settings.json    Wurzeln, Skriptordner, Schalter, zuletzt Geöffnetes
    log.txt          Meldungen, auch die des P3D-Lesers
    index.json       Zwischenspeicher des Bestands
    thumbs\          Miniaturbilder
```

Beim ersten Start sucht das Programm nach einem Arbeitslaufwerk mit einem
Unterordner `DZ`. Gefunden wird üblicherweise `H:\P_Drive`. Nach dem
Hinzufügen neuer Modelle auf **Neu einlesen** klicken.

## Installieren

Es gibt zwei Wege — beide brauchen keine .NET-Installation, die Laufzeit
ist enthalten.

**Setup** (`DayZAssetPreview_Setup.exe`, rund 57 MB) legt das Programm
unter `%ProgramFiles%\7SBM\Asset Preview` ab, erstellt Startmenü- und auf
Wunsch Desktop-Verknüpfung und trägt sich für `.p3d` und `.asc` ins Menü
**Öffnen mit** ein. Die Standardzuordnung bleibt unangetastet — `.p3d`
hängt in aller Regel am Object Builder, und die darf ein Setup nicht
stillschweigend an sich reissen.

**Portabel**: `DayZ Asset Preview.exe` einfach irgendwohin legen und
starten. Einstellungen landen so oder so in
`%LOCALAPPDATA%\DayZAssetPreview`.

Windows SmartScreen meldet sich beim ersten Start, weil der Installer
nicht signiert ist: „Weitere Informationen" → „Trotzdem ausführen".

## Bauen

```
dotnet build tools/DayZAssetPreview
dotnet test  tools/DayZAssetPreview
powershell -ExecutionPolicy Bypass -File tools/DayZAssetPreview/veroeffentlichen.ps1
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" tools\DayZAssetPreview\DayZAssetPreview_Setup.iss
```

Die ersten drei Schritte erzeugen die eigenständige `.exe` in
`veroeffentlicht\`, der vierte daraus das Setup in `_RELEASE\`. Beide
Ordner sind von der Versionierung ausgenommen; fertige Stände werden wie
beim P3D.DeBin über Releases verteilt.

Die Tests, die echte Dateien brauchen, überspringen sich, wenn kein
Arbeitslaufwerk vorhanden ist. Mit `DZ_PDRIVE` lässt sich ein abweichender
Pfad vorgeben.

## Herkunft

Das Lesen und Umwandeln der P3D-Dateien stammt aus `BisDll`, dem Parser des
**7SBM P3D.DeBin** in diesem Repository. Der Quellcode wird nicht kopiert,
sondern direkt mitkompiliert — Korrekturen wirken in beiden Werkzeugen.
Siehe `VENDOR.md` im Wurzelverzeichnis.

## Grenzen

- WPF 3D kennt kein Alpha-Testing. Vegetation weicht an den Blattkanten
  vom Spiel ab; der Alphakanal wird deshalb auf 0 oder 255 gerundet.
- Dargestellt wird die Diffusetextur. Normal- und Specular-Maps bleiben
  unberücksichtigt — für „wie sieht das Objekt aus" reicht das.
- Proxies werden nicht **aufgelöst**: ein Haus zeigt seine eigenen Flächen,
  nicht die über Proxy eingehängten Fenster und Türen. Erkannt und
  ausgeblendet werden nur die Platzhalter selbst.
- Die Beleuchtung ist nicht die des Spiels. Ziel sind Wiedererkennbarkeit
  und Maßstab, nicht fotorealistische Übereinstimmung.
- Fehlt eine Textur, bleibt die Fläche grau und die Datei wird im
  Info-Panel genannt. Das trifft vor allem Modelle, die auf fremde Mods
  verweisen, die nicht auf dem Arbeitslaufwerk liegen.
