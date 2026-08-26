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

Die Anwendung ist kein Einzweck-Fenster, sondern ein **Rahmen für mehrere
Werkzeuge**. Die Asset-Vorschau ist das erste Modul; geplant sind weiter der
vollständige P3D-Debinarizer als Oberfläche und ein Previewer für
ASC-Höhenkarten. Die Shell stellt Navigation, Theme, Einstellungen und
Protokoll bereit; ein Modul bringt nur seine eigene Ansicht mit.

Drei Projekte unter `tools/DayZAssetPreview/`:

```
DzAssets.Formats/     Klassenbibliothek, keine UI
  OdolModelReader     ODOL -> ModelGeometry (nutzt BisDll)
  PaaImage            PAA -> BGRA32-Pixelpuffer
  DxtDecoder          BC1/BC3-Blockdekodierung
  RvmatMaterial       Klartext-RVMAT -> Texturzuweisungen
  TextureResolver     Abschnitt -> Texturdatei auf der Platte
  ConfigClassIndex    config.cpp -> Klassenname <-> Modellpfad
  AssetIndex          Verzeichnisdurchlauf, Suchindex, Cache

DzAssets.Preview/     WPF-Anwendung
  Shell/              Fenster, Modulnavigation, Theme, Einstellungen
    IWerkzeugModul    Schnittstelle, die jedes Werkzeug erfuellt
    WerkzeugKontext   gemeinsame Dienste: P-Drive, Einstellungen, Protokoll
  Module/AssetVorschau/
    ModelViewport     Kamera, Beleuchtung, Gitter, Massstabsfigur
    ThumbnailService  Offscreen-Rendering, Plattencache

DzAssets.Tests/       xUnit, prüft gegen echte DZ-Dateien
```

### Warum ein Rahmen und nicht ein Fenster

Die drei Werkzeuge teilen mehr, als sie unterscheidet: denselben
P-Drive-Pfad, dasselbe Theme, dieselbe Protokolldatei, dieselbe Art,
langlaufende Arbeit mit Fortschritt und Abbruch anzuzeigen. Ein Modul ist
deshalb nur eine `UserControl` samt Titel und Symbol; die Shell weiss
nichts über P3D, PAA oder ASC, und kein Modul kennt ein anderes.

Die Modul-Schnittstelle entsteht in dieser Umsetzung mit **einem** Modul —
bewusst, damit sie an einem echten Fall gemessen wird und nicht auf Vorrat
entworfen ist. Die beiden weiteren Werkzeuge sind ausdrücklich **nicht**
Teil dieser Umsetzung; sie werden getrennt geplant, sobald die Analyse
ihrer Anforderungen vorliegt
(`2026-08-26-werkzeug-module-analyse.md`).

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

Moderne, dunkle Gestaltung: eigenes Fenster-Chrome über `WindowChrome`,
Schrift `Segoe UI Variable Text`, Symbole aus `Segoe Fluent Icons` (beide
unter Windows 11 vorhanden, geprüft). Keine unformatierten
Standard-Steuerelemente. Sämtliche Farben, Radien und Abstände liegen als
Ressourcen in einer einzigen Datei `Theme.xaml`, damit die Gestaltung an
einer Stelle austauschbar bleibt.

```
┌────┬──────────────┬──────────────────────┬──────────────┐
│ ▣  │ P-Drive-Baum │                      │ Info         │
│ ▤  │  DZ/         │    3D-Viewport       │  Maße X/Y/Z  │
│ ▥  │   structures │                      │  Dreiecke    │
│    │   plants     │  Orbit / Zoom / Pan  │  LOD-Auswahl │
│    │  7SBM_...    │  Gitter in Metern    │  Texturen    │
│    ├──────────────┤  Massstabsfigur      │  Klassenname │
│    │ Suche        │                      │              │
│    │ Trefferliste │                      │ [Pfad kopieren]│
└────┴──────────────┴──────────────────────┴──────────────┘
  ↑
  Modulleiste: Asset-Vorschau, später Debinarizer und Höhenkarte
```

Ganz links eine schmale Modulleiste. In dieser Umsetzung enthält sie einen
Eintrag; die beiden künftigen Werkzeuge kommen dort dazu, ohne dass die
Vorschau davon berührt wird.

Bedienung im Viewport: linke Maustaste dreht, mittlere oder rechte
verschiebt, das Rad zoomt. `F` rahmt das Objekt ein, `W` schaltet das
Drahtgitter um, `G` das Bodengitter, `M` die Massstabsfigur.

Die Trefferliste zeigt ab Phase 7 Miniaturbilder statt reiner Namen.

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
2. **Shell** — Fenster-Chrome, Theme, Modulleiste, Einstellungen, Protokoll
3. **WPF-Grundgerüst der Vorschau** — Baum, Auswahl, Viewport ohne Texturen
4. **Texturen und Materialien** — Sections, RVMAT-Rückfall, Alpha
5. **Info-Panel** — Masse, Dreiecke, LOD-Umschaltung, Gitter, Massstabsfigur
6. **Suche** — Index über alle Wurzeln, Cache
7. **Miniaturbilder** — Offscreen-Rendering mit Plattencache
8. **Auslieferung** — Single-File-`.exe`, Eintrag in README und CHANGELOG

Die Punkte 1 bis 5 bilden das nutzbare Minimum: ein Asset auswählen und es
texturiert in korrekter Grösse sehen.

## Spätere Erweiterungen (nicht Teil dieser Umsetzung)

- **P3D-Debinarizer als Modul** — das vorhandene Konsolenwerkzeug bekommt
  eine Oberfläche in derselben Shell.
- **ASC-Höhenkarten-Previewer als Modul** — Esri-ASCII-Grids ansehen, wie
  sie im Terrain-Builder- und QGIS-Ablauf des Anwenders anfallen.
- **Template-Library der geladenen Karte einlesen** (`.tml`) und den Bestand
  auf die dort verwendeten Objekte einschränken. Bewusst nach hinten
  gestellt, damit die Eigenständigkeit des Werkzeugs erhalten bleibt.

Die beiden Module werden getrennt geplant. Diese Umsetzung schafft nur die
Shell, in die sie später eingehängt werden.
