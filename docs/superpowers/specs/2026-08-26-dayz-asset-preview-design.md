# DayZ Asset Preview — Design

**Datum:** 2026-08-26
**Status:** Ansatz genehmigt, Umsetzung freigegeben

## Zweck

Terrain Builder bietet keine Vorschau der Assets, die man auf der Karte
platziert. Dieses Werkzeug schliesst die Lücke: ein eigenständiges
Windows-Programm, das die entpackten DayZ-Gamefiles durchsucht und die
3D-Modelle darstellt — vergleichbar mit Bulldozer oder der Ingame-Vorschau
der Admin-Tools, aber ohne Spielstart.

Das Werkzeug läuft **unabhängig neben** Terrain Builder auf einem zweiten
Bildschirm. Es schreibt nichts in TB-Projekte und wird nicht von TB
aufgerufen. Der Anwender sieht hier, wie ein Asset aussieht, und sucht den
Namen anschliessend selbst im Objekt-Browser von Terrain Builder.

## Nicht-Ziele

- Kein Editor. Modelle werden nur gelesen, nie geschrieben.
- Keine Kopplung an Terrain Builder (kein Plugin, kein Schreiben von
  Objektlisten, kein Starten aus TB heraus).
- Keine Animationen, keine Charakter-Posen, keine Physik.
- Keine originalgetreue Enfusion-Beleuchtung. Ziel ist Wiedererkennbarkeit
  und Massstab, nicht fotorealistische Übereinstimmung mit dem Spiel.

## Datenlage (geprüft am 2026-08-26)

| Sachverhalt | Befund |
|---|---|
| Asset-Wurzel | `H:\P_Drive` — enthält `DZ\` (Gamefiles) und eigene Mods (`7SBM_*`) |
| Modelle | 8.430 `.p3d`, ausnahmslos ODOL (binarisiert, Version 54) |
| Texturen | 29.820 `.paa`, DXT1/DXT5, LZO-komprimiert, mit TAGG-Blöcken |
| Materialien | `.rvmat` liegen als Klartext vor (nicht binarisiert) |
| Klassen | `config.cpp` als Klartext, Modellbezug über `model="DZ\...\x.p3d"` |
| Vorhandener Parser | `BisDll` (in diesem Repo, `DayZ_Arma_p3dDeBin/_SOURCE/`) liest ODOL 28–75, enthält LZO- und LZSS-Decoder |

Der P-Drive-Pfad ist **nicht** fest verdrahtet: Er wird beim ersten Start
erkannt und ist in den Einstellungen änderbar.

## Technische Entscheidung

**C# / WPF auf .NET 10, ausgeliefert als Single-File-`.exe`, ohne
Fremdabhängigkeiten (kein NuGet-Paket ausserhalb des .NET-SDK).**

Begründung:

- Der ODOL-Parser existiert bereits in diesem Repository und ist an genau
  diesen Dateien erprobt. Ihn erneut in einer anderen Sprache zu schreiben
  wäre die grösste einzelne Fehlerquelle des Projekts.
- WPF bringt mit `Viewport3D` einen Retained-Mode-3D-Renderer mit. Für die
  Darstellung eines einzelnen Objekts reicht er; er erspart eine
  Grafik-Bibliothek als Abhängigkeit.
- Eine `.exe` ohne Laufzeit-Installation entspricht der Anforderung
  („starten und läuft").

Verworfene Alternativen: Python + Qt + moderngl (ODOL-Parser müsste neu
entstehen, Python-Laufzeit nötig); Electron + three.js (zwei Sprachen,
schwergewichtig).

**Bekannte Grenze:** WPF 3D kennt kein Alpha-Testing, nur Alpha-Blending.
Vegetation mit Blatt-Texturen weicht dadurch an den Kanten leicht ab.
Gemildert wird das, indem der Alpha-Kanal beim Dekodieren auf 0 oder 255
quantisiert wird. Für den Zweck — Wiedererkennen und Massstab beurteilen —
ist das ausreichend.

## Architektur

Drei Projekte unter `tools/DayZAssetPreview/`:

```
DzAssets.Formats/     Klassenbibliothek, keine UI
  OdolModelReader     ODOL -> ModelGeometry (nutzt BisDll)
  PaaImage            PAA -> BGRA32-Pixelpuffer
  DxtDecoder          BC1/BC3-Blockdekodierung
  RvmatMaterial       Klartext-RVMAT -> Texturzuweisungen
  ConfigClassIndex    config.cpp -> Klassenname <-> Modellpfad
  AssetIndex          Verzeichnisdurchlauf, Suchindex, Cache

DzAssets.Preview/     WPF-Anwendung
  MainWindow          Baum | Viewport | Info
  ModelViewport       Kamera, Beleuchtung, Gitter, Massstabsfigur
  ThumbnailService    Offscreen-Rendering, Plattencache

DzAssets.Tests/       xUnit, prüft gegen echte DZ-Dateien
```

`BisDll` wird nicht kopiert, sondern per `<Compile Include>` aus
`DayZ_Arma_p3dDeBin/_SOURCE/` mitkompiliert. So bleibt eine einzige Quelle
der Wahrheit: Korrekturen am Debinarizer wirken auch im Viewer.

### Datenfluss

```
.p3d (ODOL)  --BisDll-->  LOD-Liste
                              |
                     LOD auswählen (feinste sichtbare)
                              |
                          Sections
                         /        \
              textureIndex        materialIndex
                    |                   |
              Textures[]            Materials[] -> .rvmat -> _co.paa
                    \                  /
                     \                /
                    .paa -> DXT-Decode -> BitmapSource
                              |
                     MeshGeometry3D + DiffuseMaterial
                              |
                          Viewport3D
```

Die Diffuse-Textur kommt vorrangig aus dem Textur-Slot der Section. Nur
wenn dieser leer ist, wird das zugehörige `.rvmat` nach einer `_co.paa`
durchsucht.

### Schnittstellen der Einheiten

Jede Einheit ist ohne die anderen prüfbar:

- `OdolModelReader.Read(path) -> ModelGeometry` — Datei rein,
  Vertizes/Indizes/Texturpfade raus. Kennt keine UI und kein WPF.
- `PaaImage.Load(path) -> (int w, int h, byte[] bgra)` — reine Funktion.
- `DxtDecoder.DecodeBc1/DecodeBc3(byte[], w, h) -> byte[]` — reine Funktion.
- `AssetIndex` — kapselt Verzeichnisdurchlauf und Cache hinter
  `Search(text)` und `Roots`.
- `ModelViewport` — bekommt `ModelGeometry` plus geladene Bitmaps und
  kennt die Dateiformate nicht.

## Oberfläche

```
┌──────────────┬────────────────────────────┬──────────────┐
│ P-Drive-Baum │                            │ Info         │
│  DZ/         │      3D-Viewport           │  Maße X/Y/Z  │
│   structures │                            │  Dreiecke    │
│   plants     │   Orbit / Zoom / Pan       │  LOD-Auswahl │
│  7SBM_...    │   Gitter in Metern         │  Texturen    │
├──────────────┤   Massstabsfigur 1,80 m    │  Klassenname │
│ Suche        │                            │              │
│ Trefferliste │                            │ [Pfad kopieren]│
└──────────────┴────────────────────────────┴──────────────┘
```

Bedienung: linke Maustaste dreht, mittlere oder rechte verschiebt, das Rad
zoomt. `F` rahmt das Objekt ein, `W` schaltet das Drahtgitter um, `G` das
Bodengitter, `M` die Massstabsfigur.

Die Trefferliste zeigt ab Phase 6 Miniaturbilder statt reiner Namen.

## Fehlerbehandlung

Der Bestand ist gross und uneinheitlich; einzelne Fehlschläge dürfen die
Anwendung nie beenden.

| Fall | Verhalten |
|---|---|
| `.p3d` ist MLOD statt ODOL | wird gelesen, falls BisDll es kann; sonst Hinweis im Info-Panel |
| ODOL-Version ausserhalb 28–75 | Meldung „Version n nicht unterstützt", Auswahl bleibt bedienbar |
| Textur fehlt oder ist defekt | Ersatzmaterial in Grau, Name der fehlenden Datei im Info-Panel |
| Modell hat keinen sichtbaren LOD | Hinweis „nur Geometrie- oder Memory-LODs vorhanden" |
| P-Drive nicht gefunden | Einstellungsdialog beim Start statt Absturz |
| Datei über 200 MB | Nachfrage vor dem Laden |

Fehler werden je Asset protokolliert
(`%LOCALAPPDATA%\DayZAssetPreview\log.txt`), nicht als Dialog aufgedrängt.

## Prüfung

Testgetrieben, mit xUnit gegen die echten Dateien auf dem P-Drive. Fehlt das
P-Drive, überspringen sich die betroffenen Tests, statt fehlzuschlagen.

- `DxtDecoder`: bekannte 8-Byte-BC1-Blöcke gegen von Hand berechnete
  Farbwerte — hängt an keiner externen Datei.
- `PaaImage`: lädt echte `.paa`, prüft Grösse, Format und dass der
  Pixelpuffer die erwartete Länge hat.
- `OdolModelReader`: lädt echte `.p3d`, prüft die Anzahl LODs, dass jeder
  Index innerhalb der Vertexliste liegt und die Bounding-Box endlich ist.
- `ConfigClassIndex`: prüft an `plants/config.cpp`, dass bekannte Klassen
  auf den erwarteten Modellpfad zeigen.
- `AssetIndex`: prüft die Cache-Invalidierung über die Änderungszeit.

Die Oberfläche wird von Hand geprüft; ein automatisierter UI-Test lohnt den
Aufwand hier nicht.

## Reihenfolge der Umsetzung

1. **Formatschicht** — ODOL lesen, PAA und DXT dekodieren, mit Tests
2. **WPF-Grundgerüst** — Baum, Auswahl, Viewport ohne Texturen
3. **Texturen und Materialien** — Sections, RVMAT-Rückfall, Alpha
4. **Info-Panel** — Masse, Dreiecke, LOD-Umschaltung, Gitter, Massstabsfigur
5. **Suche** — Index über alle Wurzeln, Cache
6. **Miniaturbilder** — Offscreen-Rendering mit Plattencache
7. **Auslieferung** — Single-File-`.exe`, Eintrag in README und CHANGELOG

Die Punkte 1 bis 4 bilden das nutzbare Minimum: ein Asset auswählen und es
texturiert in korrekter Grösse sehen.

## Späteres Extra (nicht Teil dieser Umsetzung)

Die Template-Library der geladenen Karte einlesen (`.tml`) und den Bestand
auf die dort verwendeten Objekte einschränken. Bewusst nach hinten gestellt,
damit die Eigenständigkeit des Werkzeugs erhalten bleibt.
