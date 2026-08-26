# Werkzeug-Module — Analyse

**Datum:** 2026-08-26
**Status:** Analyse, keine Umsetzung
**Gegenstand:** Zwei weitere Module für die Shell aus
`2026-08-26-dayz-asset-preview-design.md`:
**(A)** der P3D-Debinarizer als Oberfläche, **(B)** ein Previewer für
ASC-Höhenkarten.

Diese Datei enthält bewusst keinen Plan und keine Aufgabenliste. Sie stellt
fest, was da ist, was fehlt und was es kostet.

---

## 1. Debinarizer: Betriebsarten

Quelle: `DayZ_Arma_p3dDeBin/_SOURCE/New_Version_WORKING DayZ+Arma3_FIXED/Debinarizer.cs`
(2853 Zeilen, `net461`, `OutputType=Exe`, `UseWindowsForms=true`).

Die Datei besteht aus **einer** Klasse `P3DDebinarizer.Program` mit
ausschliesslich `private static`-Mitgliedern. Es gibt keinen öffentlichen
Typ, keine Bibliotheks-Fassade und keinen Einstiegspunkt ausser `Main`.

Entscheidend für die Bewertung: `Main` (Zeile 1013–2853, **1841 der 2853
Zeilen**) enthält nicht nur die Menüführung, sondern für mehrere Modi auch
die vollständige Fachlogik inline. Die sechs Modi sind daher unterschiedlich
weit von einer Bibliothek entfernt.

| Modus | Einstiegspunkt | Zeile | konsolengebunden | Aufwand GUI |
|---|---|---|---|---|
| **P** — P3D debinarisieren (ODOL → MLOD), eine Datei | `ConvResult ConvertFile(string srcPath, string dstPath, bool overwrite, out string errorMsg, out string debugLog)` | 164 | **nein** (aber `Console.SetError`, s. u.) | gering |
| **P** — P3D, Stapel | `List<string[]> ConvertFiles(string[] files, string dstFolder, bool overwrite = false)` | 213 | **ja** — 22 `Console.*`-Aufrufe, Fortschritt und Fehlerbericht nur als Text | gering |
| **P** — Nachbereitung (`model.cfg`, Originale löschen, `_mlod` entfernen) | inline in `Main`, nutzt `string BuildModelCfg(string[] srcOdolPaths)` | 1523–1610 / 541 | **ja** — drei `ReadJN()`-Rückfragen (1532, 1563, 1588) | gering |
| **A** — ANM konvertieren (TXA + Template → ANM) | `bool ParseTxa(...)` / `bool ReadAnmHead(...)` / `bool PatchAnm(...)` | 337 / 375 / 496 | **nein** (alle drei) | gering |
| **A** — Ablauf und Stapelmodus | inline in `Main` | 1614–1779 | **ja** — 43 `Console.*`-Aufrufe | mittel |
| **E** — ANM exportieren (ANM → TXA) | `ReadAnmHead` (375) + **TXA-Erzeugung inline** | 2074–2217 (Erzeugung 2101–2128, 2165–2196) | **ja** — 24 `Console.*`-Aufrufe, Textaufbau zwischen den Ausgaben verwoben | mittel |
| **I** — ANM inspizieren | `ReadAnmHead` (375) + **Bericht und `model.cfg` inline** | 1781–2072 (Bericht 1826, cfg 2018) | **ja** — 32 `Console.*`, dazu `Console.ReadKey` (1984) für die Speichern-Rückfrage | mittel |
| **R** — RTM inspizieren (Arma3 BMTR) | **kein Einstiegspunkt** — vollständig inline in `Main` | 2442–2830 | **ja** — 36 `Console.*`, dazu `Console.ReadKey` (2700) | **hoch** |
| **B** — PBO entpacken | `int PboExtractAll(string pboPath, string outputDir, out Dictionary<string,string> props, out string error)` | 771 | **nein** | gering |
| **B** — Nachbereitung (`config.bin` → `config.cpp`, RVMAT) | `bool TryRapToCpp(string binPath, out string cppText, out string error)` | 978 | **nein** | gering |
| **B** — Ablauf und Stapelmodus | inline in `Main` | 2219–2440 | **ja** — 32 `Console.*`-Aufrufe | gering |

### Was ohne Umbau als Bibliothek aufrufbar ist

Der gesamte Bereich **Zeile 164–1010** ist bis auf zwei Stellen (unten) frei
von Konsolenausgaben und liesse sich unverändert aufrufen, sobald die
Sichtbarkeit von `private static` auf `internal`/`public` steigt:

- `ConvertFile` (164), `ParseTxa` (337), `ReadAnmHead` (375),
  `PatchAnm` (496), `BuildModelCfg` (541)
- `PboDecompress` (642), `PboReadString` (692), `PboReadHeader` (700),
  `SanitizePboEntryName` (754), `PboExtractAll` (771)
- `RapReadStr` (847), `RapEscape` (855), `RapReadCompact` (860),
  `RapWriteEntry` (868), `TryRapToCpp` (978)
- `RotVecToQuat` (323), `ShortPath` (130)

Alle melden Fehler über `out string error` oder einen `bool`-Rückgabewert —
das passt bereits zum Muster „kein Absturz durch eine einzelne Datei" aus
dem Design.

### Was zwingend entkoppelt werden muss

- `ConvertFiles` (213) — reine Schleife über `ConvertFile` plus Textbericht.
  Neu schreiben ist billiger als umbauen.
- Alles innerhalb von `Main` ab 1495 („Arbeit"). Besonders **Modus R**: die
  BMTR-Kopfauswertung, die Knochennamensliste, die Frame-Schleife, der
  TXA-Export und die `model.cfg`-Erzeugung stehen dort zeilenweise zwischen
  `Console.WriteLine`-Aufrufen und teilen lokale Variablen von `Main`
  (`rd`, `rtmBones`, `rtmFramePhases`, …). Für dieses Modul gibt es nichts
  wiederzuverwenden ausser dem Format-Wissen.
- `ReadMenuKey` (141) und `ReadJN` (155) — jede Rückfrage der Anwendung
  läuft über diese beiden. In einer Oberfläche ersetzt sie ein Dialog oder,
  besser, eine Optionsleiste, die **vor** dem Start gefüllt wird.

### Geteilter globaler Zustand

| Stelle | Art | Störung im GUI-Betrieb |
|---|---|---|
| `_player` (24), `_musicThread` (25) | statisch, Hintergrund-Thread mit `while(true)` | Endlosschleife ohne Abbruchmöglichkeit. Entfällt im Modul ersatzlos. |
| `_bannerCts` (66), `_bannerThread` (67), `_bannerTopRow` (68) | statisch, schreibt per `Console.SetCursorPosition` | Nur Konsole. Entfällt ersatzlos. |
| `Console.SetError` in `ConvertFile` (188–199) | **prozessweit** | Ernst. `ConvertFile` leitet `Console.Error` auf einen `StringWriter` um, um die Diagnose von `BisDll` einzufangen, und stellt danach den alten Writer wieder her. `Console.SetError` gilt für den **ganzen Prozess**, nicht je Thread. Zwei parallel laufende Konvertierungen würden ihre Diagnosen vermischen oder verlieren. |
| `Console.SetError` in `BuildModelCfg` (554–557) | **prozessweit** | Gleiches Problem. |
| `Console.Error.WriteLine` in `BisDll` | fest verdrahtet | `BisDll.Model.ODOL/ODOL.cs` (110, 140, 153, 154, 275, 311) und `LOD.cs` (147–176) schreiben unbedingt nach `Console.Error`. Der Plan verbietet, `BisDll` zu ändern. In einer WPF-Anwendung gibt es keine Konsole; diese Meldungen gehen ohne ein einmaliges `Console.SetError(...)` beim Start **verloren**. |
| Arbeitsverzeichnis | — | Wird nirgends gesetzt oder gelesen. `Directory.GetCurrentDirectory`, `Environment.CurrentDirectory` und `Environment.Exit` kommen in der Datei **nicht** vor. Alle Pfade laufen über `Path.GetFullPath` bzw. die Dialogergebnisse. Hier gibt es kein Problem. |
| `[STAThread]` (1012) | Attribut auf `Main` | WPF-`Main` ist ohnehin STA. Kein Problem — aber die Dateidialoge müssen weiterhin auf dem UI-Thread laufen, nicht im Arbeits-Task. |

**Folgerung zum Umbau:** Weil `BisDll` unverändert nach `Console.Error`
schreibt und `Console.SetError` prozessweit wirkt, darf das
Debinarizer-Modul seine Konvertierungen **nicht parallelisieren**, solange
die Diagnose gebraucht wird. Ein Worker-Thread, der die Dateien der Reihe
nach abarbeitet, ist die einzige Variante, die ohne Änderung an `BisDll`
korrekte Fehlermeldungen liefert. Das ist keine Einschränkung der
Bequemlichkeit, sondern eine Korrektheitsfrage.

---

## 2. Debinarizer: nötige Entkopplungsschritte

Nummeriert, mit Zeilenbezug. Reihenfolge ist Abhängigkeitsreihenfolge, nicht
Wichtigkeit.

1. **Neue Klassenbibliothek statt Aufruf der `.exe`.** Ein neues Projekt
   (Vorschlag `DzAssets.Debin`) bindet `Debinarizer.cs` **nicht** ein,
   sondern erhält Kopien der wiederverwendbaren Methoden bzw. bindet sie per
   `<Compile Include>` ein, sobald sie in eine eigene Datei gewandert sind.
   Das bestehende Konsolenprojekt bleibt unangetastet lauffähig — es ist ein
   ausgeliefertes Werkzeug mit eigener Versionsnummer (v1.9/v1.10) und darf
   nicht kaputtgehen.

2. **Zeile 164–1010 aus `Program` herauslösen.** Die 16 unter Abschnitt 1
   genannten Methoden in Datei-je-Format-Einheiten verschieben
   (`P3dConverter`, `AnmFormat`, `PboArchive`, `RapConfig`) und die
   Sichtbarkeit auf `public static` heben. Signaturen bleiben unverändert —
   sie sind bereits `out`-basiert und werfen nicht.

3. **`Console.SetError` in `ConvertFile` (188–199) durch einen Parameter
   ersetzen.** Neue Signatur:
   `ConvertFile(string srcPath, string dstPath, bool overwrite, TextWriter diagnose, out string errorMsg)`.
   Der Aufrufer legt den `StringWriter` an. Damit verschwindet der
   prozessweite Seiteneffekt aus der Bibliothek — er wandert an genau eine
   Stelle im Modul, die ihn unter einer Sperre setzt.
   Gleiches für `BuildModelCfg` (554–557).

4. **Einmaliges `Console.SetError(...)` beim Start der Shell.** Weil
   `BisDll` (ODOL.cs 110/140/153/154/275/311, LOD.cs 147–176) unveränderbar
   nach `Console.Error` schreibt, richtet die Shell beim Start einen
   `TextWriter` ein, der in `log.txt` schreibt. Ohne das gehen die
   ODOL-Warnungen in der GUI ersatzlos verloren.

5. **`ConvertFiles` (213–301) ersatzlos streichen** und durch eine Methode
   ohne Ausgabe ersetzen, die je Datei ein Ergebnisobjekt liefert:
   `IEnumerable<KonvertierErgebnis> Konvertiere(IReadOnlyList<string> dateien, string zielOrdner, bool ueberschreiben, IProgress<Fortschritt>? fortschritt, CancellationToken abbruch)`.
   Der Textbericht aus 258–301 wird zur Aufgabe der Oberfläche.

6. **Die drei `ReadJN()`-Rückfragen (1532, 1563, 1588) in Optionen
   umwandeln.** „`model.cfg` erzeugen", „Originale behalten", „`_mlod`-Suffix
   entfernen" werden zu drei Ankreuzfeldern, die **vor** dem Start gesetzt
   werden. Das ist auch fachlich besser: heute muss man den ganzen Lauf
   abwarten, bevor man die Fragen beantworten kann.

7. **Modus E (2101–2128, 2165–2196): TXA-Erzeugung herauslösen** zu
   `string BaueTxa(IReadOnlyList<AnimBone> knochen)`. Der Code ist heute
   zwischen `Console.WriteLine`-Aufrufen verteilt und muss von Hand
   herausgetrennt werden — das ist die Stelle mit dem höchsten Risiko, beim
   Kopieren einen Formatierungsdetail zu verlieren. Ein Vergleichstest gegen
   eine mit v1.10 erzeugte `.txa` ist hier Pflicht.

8. **Modus I (1826, 2018): Berichtserzeugung und `model.cfg`-aus-Knochen
   herauslösen** zu `string BaueInspektionsbericht(...)` und
   `string BaueModelCfgAusKnochen(...)`. Der `Console.ReadKey` bei 1984
   (Speichern ja/nein) entfällt — in der GUI gibt es einen
   „Bericht speichern"-Knopf neben der Tabelle.

9. **Modus R (2442–2830) neu schreiben.** Es gibt nichts zu extrahieren; die
   BMTR-Auswertung existiert nur als inline-Code. Als Vorlage dient der
   bestehende Code, aber es entsteht eine neue Einheit
   `RtmFormat.Lies(string pfad, out RtmDatei datei, out string fehler)` samt
   `BaueTxa`. Aufwand hoch, Risiko hoch — dieser Modus gehört als Letztes
   ins Modul, nicht als Erstes.

10. **`ReadMenuKey` (141) / `ReadJN` (155) / `SetColor` (138) /
    `ResetColor` (139) / `StartTheme` (27) / `StartScrollBanner` (70) /
    `StopScrollBanner` (121) nicht übernehmen.** Sie haben in einer
    Bibliothek keinen Platz. Die Musik (`theme_loop.wav`, 2 MB eingebettet)
    und das Laufband entfallen im Modul.

11. **Dateidialoge ersetzen.** Zeilen 1131–1425 nutzen
    `System.Windows.Forms.FolderBrowserDialog` / `OpenFileDialog` /
    `SaveFileDialog`. Ein WPF-Modul verwendet stattdessen
    `Microsoft.Win32.OpenFileDialog` und `Microsoft.Win32.OpenFolderDialog`
    (seit .NET 8 in WPF enthalten). Damit bleibt die Vorgabe „kein
    NuGet-Paket" gewahrt und `UseWindowsForms` wird nicht gebraucht.

12. **Abbruch nachrüsten.** Keine der bestehenden Methoden kennt einen
    `CancellationToken`. Für `ConvertFile` und `PboExtractAll` genügt die
    Prüfung zwischen den Dateien (Schritt 5); innerhalb einer einzelnen
    grossen Datei bleibt der Vorgang unterbrechbar erst nach dem Ende. Das
    ist vertretbar: die grösste `.p3d` auf dem P-Drive liegt weit unter einer
    Sekunde Verarbeitungszeit.

**Ehrliche Einschätzung des Gesamtaufwands:** Die Modi **P** und **B** —
also genau die beiden, die am häufigsten gebraucht werden — sind mit den
Schritten 1–6 und 11–12 erreichbar und stecken bereits zu 90 % in
aufrufbaren Methoden. Die Modi **A**, **E**, **I** kosten je einen halben
Tag Extraktionsarbeit mit Vergleichstests. Modus **R** ist eine
Neuimplementierung von rund 390 Zeilen. Ein Modul, das nur P und B kann, ist
deshalb ein sinnvolles erstes Ziel und deckt den Alltag ab.

---

## 3. ASC-Höhenkarten: Befund

### Gefundene Dateien

Gesucht wurde auf allen erreichbaren Laufwerken (`C:`, `H:`). Auf `H:`
liegen **80 `.asc`-Dateien**. Sie verteilen sich auf die QGIS- und
Terrain-Builder-Projekte des Nutzers:

| Pfad | Grösse | Raster | Zellgrösse | Ausdehnung |
|---|---|---|---|---|
| `H:\BrienZ_QGIS\gtt_export\gtt_heightmap.asc` | 130.013.305 B (124,0 MiB) | 4096 × 4096 | 5,0 m | 20.480 m |
| `H:\BrienzMeiringen_QGIS\gtt_export_16K\gtt_heightmap.asc` | 129.483.883 B | 4096 × 4096 | 3,75 m | 15.360 m |
| `H:\BrienZ_Meiringen_TerrainBuilderProjekt\source\TerrainBuilder\BrienZ_Meiringen_highmap.asc` | 129.483.879 B | 4096 × 4096 | 3,75 m | 15.360 m |
| `H:\BrienzMeiringen\gtt_export_1k\gtt_heightmap.asc` (und `gtt_export_10k`, identisch) | 129.350.131 B | 4096 × 4096 | 3,662109375 m | exakt 15.000 m |
| `H:\BrienZ_QGIS\gtt_export_20k\HighField_Records\*.asc` (24 Dateien: Rivers, Roads, Rail, See, Trails, Offset) | 30–48 MB | 2048 × 2048 | 10,0 m | 20.480 m |
| `H:\alter backup brienz\...` | 30–48 MB | 2048 × 2048 | 10,0 m | 20.480 m |
| `H:\BrienZ_Meiringen_TerrainBuilderProjekt\source\shapefiles\gtt_export_export nach Youtuber\gtt_heightmap.asc` | 129.483.883 B | 4096 × 4096 | 3,75 m | 15.360 m |

Verteilung über alle 80 Dateien (`ncols nrows xllcorner yllcorner cellsize NODATA_value`):

```
  45×  2048 2048 200000.000000 0.000000 10.000000        -9999.000000
  27×  2048 2048 200000.000000 0.000000 10.000000        -9999
   2×  4096 4096 200000.000000 0.000000  5.0             -9999.0
   2×  4096 4096 200000.000000 0.000000  3.750000        -9999
   2×  4096 4096 200000.000000 0.000000  3.75            -9999.0
   2×  4096 4096 200000.000000 0.000000  3.662109375     -9999.0
```

### Kopfzeilen im Original

`H:\BrienZ_QGIS\gtt_export\gtt_heightmap.asc` (grösste Datei, CRLF):

```
ncols         4096
nrows         4096
xllcorner     200000.000000
yllcorner     0.000000
cellsize      5.0
NODATA_value  -9999.0
1755.47 1759.84 1764.19 1768.46 1772.62 1776.69 1780.69 1784.60 …
```

`H:\BrienZ_Meiringen_TerrainBuilderProjekt\source\TerrainBuilder\BrienZ_Meiringen_highmap.asc` (LF, nicht CRLF):

```
ncols         4096
nrows         4096
xllcorner     200000.000000
yllcorner     0.000000
cellsize      3.750000
NODATA_value  -9999
1358.84 1359.59 1360.28 1360.89 1361.42 1361.87 1362.28 1362.67 …
```

`H:\BrienZ_QGIS\gtt_export_20k\HighField_Records\heightmap_Rivers_1_8_Offset.asc`
(vom eigenen Python-Skript `adjust_heightmap_for_rail.py` geschrieben,
Zeilen 218–223 — **Schlüssel und Wert durch genau ein Leerzeichen getrennt**,
Werte mit sechs Nachkommastellen):

```
ncols 2048
nrows 2048
xllcorner 200000.000000
yllcorner 0.000000
cellsize 10.000000
NODATA_value -9999.000000
1197.910000 1206.569900 1214.900000 1222.890000 1230.550000 …
```

### Festgestellte Formateigenschaften

| Eigenschaft | Befund |
|---|---|
| Kopfzeilen | **immer 6**, immer in der Reihenfolge `ncols`, `nrows`, `xllcorner`, `yllcorner`, `cellsize`, `NODATA_value` |
| Ecken- oder Mittelpunktbezug | **ausnahmslos `xllcorner`/`yllcorner`.** In keiner der 80 Dateien kommt `xllcenter`/`yllcenter` vor |
| Trennung Schlüssel/Wert | mal Leerzeichen-Auffüllung auf Spalte 15 (QGIS, Terrain Builder), mal genau ein Leerzeichen (eigenes Python-Skript) → **auf beliebige Folge von Leerraum aufteilen** |
| Dezimaltrennzeichen | **Punkt**, ohne Ausnahme, auch in den Kopfzeilen |
| Tausendertrennzeichen | keins |
| Spaltentrennung in den Datenzeilen | **genau ein Leerzeichen**, kein Tabulator, kein führendes Leerzeichen |
| Zeilenaufteilung | **eine Rasterzeile = eine Textzeile.** 4096 Werte je Zeile, keine Umbrüche innerhalb einer Zeile |
| Zeilenenden | **gemischt** — QGIS-Export CRLF, Terrain-Builder-Datei LF |
| Zeilenlänge | bis 32.767 Zeichen (4096 Werte à 7–8 Zeichen) |
| Zeilenzahl gesamt | 4102 = 6 Kopf + 4096 Daten |
| Nachkommastellen | 2 (QGIS/TB) bzw. 6 (Python-Skript) |
| NODATA-Schreibweise | `-9999`, `-9999.0` und `-9999.000000` **nebeneinander** → numerisch vergleichen, nie als Zeichenkette |
| NODATA im Datenteil | in der grössten Datei **kein einziger** Treffer — die Raster sind lückenlos |
| Höhenbereich (Stichprobe, 20k-Karte) | ca. 1124 m bis 2332 m, Brienzersee-Region |
| Georeferenz | `xllcorner = 200000`, `yllcorner = 0` bei **allen 80 Dateien**. Das ist der Terrain-Builder-Kartenursprung, keine echte LV95-Koordinate. Ein Previewer sollte die Ausdehnung in Metern (`ncols × cellsize`) anzeigen und nicht so tun, als sei die Datei verortet |

### Abgeleitete Anforderungen an einen Leser

**Speicherbedarf, gerechnet an der grössten gefundenen Datei**
(4096 × 4096 = 16.777.216 Werte, Datei 124,0 MiB):

| Ansatz | Spitzenbedarf | Bewertung |
|---|---|---|
| `File.ReadAllText` + `Split(' ')` | ~248 MiB Zeichenkette (UTF-16 verdoppelt) + 16,8 Mio. Teilzeichenketten (grob 800 MiB Zuweisungen) + 64 MiB Raster | **unbrauchbar**, provoziert `OutOfMemoryException` und minutenlange GC-Pausen |
| `File.ReadAllLines` + `Split` je Zeile | 4102 Zeichenketten à bis 64 KiB + gleiche Teilzeichenketten-Flut | besser, aber immer noch 16,8 Mio. unnötige Objekte |
| `StreamReader.ReadLine` + `float.TryParse(ReadOnlySpan<char>, …)` über die Zeile schneiden | **64 MiB Raster + 64 KiB Zeilenpuffer** | **Empfehlung.** Keine einzige Teilzeichenkette wird angelegt |
| ganze Datei als `byte[]` + `Utf8Parser` | 124 MiB Puffer + 64 MiB Raster = 188 MiB | schneller, aber dreimal so viel Speicher; nur nötig, wenn die Ladezeit stört |

Das Raster selbst: `float[16.777.216]` = **64 MiB**. Als `double[]` wären es
128 MiB — überflüssig, denn die Quelle hat höchstens 6 Nachkommastellen bei
vierstelligen Höhen, das passt bequem in `float`. Ein 2048er-Raster braucht
entsprechend 16 MiB.

Weitere zwingende Punkte:

1. **`CultureInfo.InvariantCulture` bei jedem `Parse`.** Das System des
   Nutzers läuft mit deutschem Gebietsschema; `float.Parse("1358.84")` würde
   dort `135884` ergeben. Das ist der wahrscheinlichste stille Fehler dieses
   Moduls überhaupt.
2. **Minimum und Maximum während des Einlesens mitführen** (NODATA
   ausgenommen). Sie werden für die Farbskala gebraucht; ein zweiter Durchlauf
   wäre vermeidbarer Aufwand.
3. **Kopfzeilen tolerant lesen:** Schlüssel ohne Beachtung der Gross- und
   Kleinschreibung, Trennung an beliebigem Leerraum. `xllcenter`/`yllcenter`
   sollten trotzdem behandelt werden (Esri-Standard erlaubt sie), aber im
   Bewusstsein, dass diese Variante hier **nie geprüft** werden kann — sie
   kommt in keiner vorhandenen Datei vor. Diese Zweige gehören ins Protokoll,
   damit auffällt, wenn sie erstmals greifen.
4. **Zeilenzahl gegen `nrows` prüfen** und bei Abweichung abbrechen statt
   still weiterzulesen; ebenso die Feldzahl je Zeile gegen `ncols`.

**Nötige Herunterrechnung fürs Anzeigen:**

Ein 4096 × 4096-Bild ist als `WriteableBitmap` in `Bgra32` **64 MiB** und
passt auf keinen Bildschirm. Für die Übersicht genügt eine Verkleinerung auf
höchstens 2048 × 2048 (16 MiB in `Bgra32`), besser noch auf die tatsächliche
Anzeigegrösse. Zwei Verfahren, beide sinnvoll:

- **Übersicht:** Blockmittelwert über `n×n` Zellen (bei 4096 → 1024 also
  4 × 4). Mittelwert, nicht Stichprobe — sonst verschwinden schmale Grate
  und Bahntrassen, also genau die Strukturen, wegen derer der Nutzer die
  Datei ansieht.
- **Ausschnitt:** beim Hineinzoomen den sichtbaren Rechteckbereich in voller
  Auflösung aus dem bereits geladenen `float[]` schneiden. Das kostet nichts
  extra, weil das Raster ohnehin vollständig im Speicher liegt.

Für die Darstellung selbst reicht eine Farbrampe über `min…max` plus eine
einfache Schummerung (Nachbardifferenz in X und Y); Höhenlinien wären ein
späteres Extra. Da sich benachbarte `.asc`-Stände nur in wenigen Zellen
unterscheiden (Rivers, Roads, Rail, See, Trails sind Varianten desselben
Rasters), wäre ein **Differenzbild zwischen zwei Dateien** der mit Abstand
nützlichste Zusatz — genau dafür gibt es im QGIS-Ordner 24 Varianten
desselben 2048er-Rasters.

**Ladezeit:** ehrlich unbekannt. Eine Messung mit `cat` ergab 0,04 s, aber die
Datei lag im Dateisystem-Cache; das ist keine belastbare Zahl. Realistisch
ist die Zerlegung von 16,8 Mio. Zahlen der bestimmende Anteil, nicht die
Platte. Ein spanbasierter Leser landet erfahrungsgemäss im Bereich weniger
Sekunden — genug, dass das Einlesen **in einen Hintergrund-Task mit
Fortschrittsanzeige** gehört und nicht auf den UI-Thread.

---

## 4. Vorgeschlagene Modul-Schnittstelle

### Code

```csharp
namespace DzAssets.Preview.Shell;

/// <summary>Ein Werkzeug, das die Shell in ihre Navigation aufnimmt.</summary>
public interface IWerkzeugModul
{
    /// <summary>Unveraenderlicher Schluessel, z. B. "asset-vorschau".
    /// Dient als Eintrag in settings.json und als Praefix im Protokoll.
    /// Nicht uebersetzen, nicht aendern.</summary>
    string Id { get; }

    /// <summary>Anzeigename, deutsch, z. B. "Asset-Vorschau".</summary>
    string Titel { get; }

    /// <summary>Glyphe aus "Segoe Fluent Icons", z. B. "\uE7C5".</summary>
    string Symbol { get; }

    /// <summary>Ein Satz fuer den Navigationseintrag und den Startbildschirm.</summary>
    string Beschreibung { get; }

    /// <summary>Warum das Werkzeug gerade nicht benutzbar ist —
    /// null, wenn es benutzbar ist. Die Shell graut den Eintrag dann aus
    /// und zeigt diesen Grund an, statt eine kaputte Ansicht zu oeffnen.</summary>
    string? NichtVerfuegbarGrund { get; }

    /// <summary>Erzeugt die Ansicht. Die Shell ruft das hoechstens einmal
    /// auf und haelt das Ergebnis. Wird beim ersten Oeffnen aufgerufen,
    /// nicht beim Start der Anwendung.</summary>
    UserControl ErzeugeAnsicht();
}
```

Gemeinsame Dienste, per Konstruktor an das Modul gereicht:

```csharp
/// <summary>Alles, was die Shell bereitstellt und mehr als ein Werkzeug braucht.
/// Bewusst ein Objekt und keine fuenf Konstruktorparameter — sonst aendert
/// jedes neue Shell-Detail die Signatur jedes Moduls.</summary>
public sealed class WerkzeugKontext
{
    public Einstellungen Einstellungen { get; }   // INotifyPropertyChanged
    public IProtokoll Protokoll { get; }          // schreibt log.txt
    public IVorgangsanzeige Vorgaenge { get; }    // Fortschrittsleiste der Shell

    public WerkzeugKontext(Einstellungen e, IProtokoll p, IVorgangsanzeige v)
        => (Einstellungen, Protokoll, Vorgaenge) = (e, p, v);
}

public interface IProtokoll
{
    void Schreibe(string modulId, string text);
    void Fehler(string modulId, string text, Exception? ex = null);
}
```

Langlaufende Arbeit — ein Typ, den Shell und alle Module teilen:

```csharp
/// <summary>Eine Fortschrittsmeldung. readonly struct, weil davon
/// tausende entstehen und keine davon den Heap belasten soll.</summary>
public readonly record struct Fortschritt(
    int Erledigt,
    int Gesamt,
    string Text);      // z. B. "…\structures\haus_01.p3d"

/// <summary>Die Fortschrittsleiste der Shell. Ein Modul startet einen
/// Vorgang und bekommt ein Handle, ueber das es meldet; die Shell zeigt
/// Balken, Text und Abbrechen-Knopf und besitzt den CancellationToken.</summary>
public interface IVorgangsanzeige
{
    Task<T> Starte<T>(
        string titel,
        Func<IProgress<Fortschritt>, CancellationToken, Task<T>> arbeit);
}
```

Ein Modul benutzt das so:

```csharp
var bericht = await _kontext.Vorgaenge.Starte("P3D debinarisieren",
    async (fortschritt, abbruch) =>
        await Task.Run(() => P3dConverter.Konvertiere(
            dateien, zielOrdner, ueberschreiben, fortschritt, abbruch), abbruch));
```

### Begründung der Entwurfsentscheidungen

**Wann werden Module erzeugt — sofort oder verzögert?**
Zweistufig. Die **Modulobjekte** entstehen sofort beim Start: sie sind
leichtgewichtig (vier Zeichenketten und ein Kontextverweis) und die Shell
braucht Titel, Symbol und Verfügbarkeit, um die Navigation überhaupt zeichnen
zu können. Die **Ansichten** entstehen verzögert, beim ersten Öffnen, und
werden danach behalten. Der Grund ist messbar: Ein ASC-Modul, das beim Start
eine 124-MiB-Datei einliest, oder ein Vorschaumodul, das 8430 `.p3d`
indiziert, würde die Startzeit der ganzen Anwendung bestimmen. Behalten statt
neu erzeugen ist wichtig, weil der Nutzer sonst bei jedem Wechsel zwischen
Vorschau und Debinarizer sein geladenes Raster und seine Auswahl verliert.

**Konstruktorparameter oder `WerkzeugKontext`?**
Ein `WerkzeugKontext`, per Konstruktor. Nicht als Parameter von
`ErzeugeAnsicht`, weil das Modul den Kontext schon vorher braucht — die
Eigenschaft `NichtVerfuegbarGrund` der Asset-Vorschau lautet
„P-Drive nicht gefunden" und liest dafür die Einstellungen. Und ein Objekt
statt einzelner Parameter, weil sonst jeder neue Shell-Dienst die Signatur
sämtlicher Module ändert. Die Verdrahtung passiert an genau einer Stelle in
`App.xaml.cs`:

```csharp
var kontext = new WerkzeugKontext(einstellungen, protokoll, vorgaenge);
Module = [ new AssetVorschauModul(kontext),
           new DebinarizerModul(kontext),
           new AscVorschauModul(kontext) ];
```

Kein Behälter, keine Reflexion, kein Aufspüren von Zusammenstellungen —
drei Zeilen, die man lesen kann, und die Reihenfolge in der Navigation steht
sichtbar da.

**Warum die Zusätze gegenüber dem Entwurf aus der Aufgabenstellung?**

- `Id` — `Titel` ist ein deutscher Anzeigename und darf sich ändern. Was
  sich merken lässt („zuletzt geöffnetes Werkzeug" in `settings.json`) und
  was ins Protokoll geschrieben wird, braucht einen stabilen Schlüssel.
- `NichtVerfuegbarGrund` — ohne das gibt es nur zwei schlechte Varianten:
  Eintrag verstecken (der Nutzer sucht ihn) oder Eintrag öffnen und eine
  leere, kaputte Ansicht zeigen. Der ASC-Previewer braucht kein P-Drive, die
  Asset-Vorschau schon; die Shell muss diesen Unterschied ausdrücken können.
- **Kein `IDisposable` an der Schnittstelle.** Nur das ASC-Modul hält
  nennenswerten Speicher. Statt alle drei Module mit einem Vertrag zu
  belasten, den zwei davon leer erfüllen, prüft die Shell beim Beenden
  `if (ansicht is IDisposable d) d.Dispose();`. Wenn sich später zeigt, dass
  mehr Module aufräumen müssen, kann der Vertrag immer noch wachsen.

**Wie melden lange Vorgänge Fortschritt und Abbruch?**
Über `IProgress<Fortschritt>` und `CancellationToken`, beide von der Shell
gestellt, nicht vom Modul. Damit gibt es **eine** Fortschrittsleiste und
**einen** Abbrechen-Knopf im Fenster, statt drei verschiedene Anzeigen in
drei Modulen. Vier Punkte, die dabei zählen:

1. **Meldungen drosseln.** `Progress<T>` schickt jeden Aufruf einzeln über
   den `SynchronizationContext` auf den UI-Thread. 8430 Meldungen in wenigen
   Sekunden legen die Oberfläche lahm. Die Bibliothek meldet deshalb nur,
   wenn seit der letzten Meldung mehr als etwa 50 ms vergangen sind oder die
   Arbeit fertig ist — die Drosselung gehört in den Aufrufer, nicht in die
   Schleife.
2. **Der Abbruch ist grobkörnig.** Geprüft wird zwischen zwei Dateien
   (Schritt 12 in Abschnitt 2), nicht innerhalb einer. Das reicht: eine
   einzelne `.p3d` braucht Bruchteile einer Sekunde. Für das Einlesen einer
   ASC-Datei wird zusätzlich alle 256 Zeilen geprüft.
3. **Kein `async void`.** `Starte` liefert `Task<T>`; ein Fehler im
   Hintergrund landet als Ausnahme im `await` des Moduls und von dort im
   Protokoll — nicht im Nirgendwo.
4. **Ein Vorgang zur Zeit.** `IVorgangsanzeige` lehnt einen zweiten Start ab,
   solange einer läuft. Das ist keine Bequemlichkeitsentscheidung: solange
   `BisDll` prozessweit nach `Console.Error` schreibt (Abschnitt 1), wären
   parallele Konvertierungen schlicht falsch.

**Was ausdrücklich nicht in die Schnittstelle gehört:** Menüeinträge,
Werkzeugleisten, Tastenkürzel. Ein Modul ist eine `UserControl` mit eigenem
Innenleben; wenn die Shell anfängt, Werkzeugleisten von Modulen einzusammeln,
kennt sie plötzlich wieder ihre Module. Tastenkürzel behandelt jede
`UserControl` selbst über ihre eigenen `InputBindings`.

---

## 5. Empfohlene Reihenfolge

### Was in der Shell vorhanden sein muss, bevor Modul A oder B gebaut wird

Die folgenden Teile werden von **allen drei** Modulen gebraucht. Sie
entstehen im laufenden Vorhaben mit dem Vorschaumodul und sollten dort
bereits so geschnitten werden, dass ein zweites Modul nichts daran ändern
muss:

1. **`IWerkzeugModul` und die Modulliste in `App.xaml.cs`** — mit dem einen
   vorhandenen Modul. Der Design-Entwurf sagt zu Recht, dass die
   Schnittstelle an einem echten Fall gemessen werden soll und nicht auf
   Vorrat entsteht.
2. **`WerkzeugKontext` mit `Einstellungen` und `IProtokoll`** — beides
   verlangt der Plan ohnehin (`settings.json`, `log.txt`).
3. **`IVorgangsanzeige` samt Fortschrittsleiste und Abbrechen-Knopf im
   Fensterrahmen.** Die Asset-Vorschau braucht das für den Verzeichnisdurchlauf
   in Phase 5 — dort entsteht es also von selbst. **Ohne diesen Teil ist
   weder A noch B sinnvoll baubar**, denn beide bestehen im Kern aus einem
   langlaufenden Vorgang.
4. **Einmaliges `Console.SetError(...)` beim Start**, das nach `log.txt`
   schreibt (Schritt 4 in Abschnitt 2). Betrifft die Asset-Vorschau bereits
   heute: sie ruft `BisDll` auf, und dessen ODOL-Warnungen gehen sonst
   verloren.
5. **Ein Dateiauswahl-Helfer der Shell** über `Microsoft.Win32.OpenFileDialog`
   und `OpenFolderDialog`, damit nicht jedes Modul eigene Dialoge baut und
   sich niemand versehentlich `UseWindowsForms` einhandelt.

Punkte 1 bis 3 sind der eigentliche Rahmen. Solange sie fehlen, entstünden
in einem zweiten Modul zwangsläufig Notlösungen, die später wieder
verschwinden müssen.

### Reihenfolge der beiden Module

**Zuerst B, der ASC-Previewer.** Begründung:

- Er hängt an **keinem** Umbau fremden Codes. Das Format ist geklärt
  (Abschnitt 3), der Leser ist eine Datei, es gibt echte Testdateien in zwei
  Grössen (2048 und 4096) und aus drei verschiedenen Schreibern.
- Er misst den Rahmen an einem zweiten, ganz anders gearteten Fall: ein
  langer Ladevorgang mit Fortschritt, viel Speicher, eine Bilddarstellung
  statt 3D. Genau das prüft, ob `IWerkzeugModul` und `IVorgangsanzeige`
  taugen — und zwar bevor am Debinarizer Arbeit hängt, die man ungern
  zweimal macht.
- Der Nutzen tritt sofort ein: 80 Dateien, davon 24 Varianten desselben
  Rasters, die heute nur in QGIS oder Terrain Builder unterscheidbar sind.

**Danach A, der Debinarizer**, und darin gestaffelt:

1. Modus **P** und Modus **B** (PBO) — beide stecken bereits in aufrufbaren
   Methoden (`ConvertFile` 164, `PboExtractAll` 771, `TryRapToCpp` 978,
   `BuildModelCfg` 541). Das ist der Alltagsbedarf und der kleinste Schnitt.
2. Modi **A**, **E**, **I** — Extraktion aus `Main` mit Vergleichstests
   gegen Ausgaben der Konsolenfassung v1.10.
3. Modus **R** zuletzt — Neuimplementierung von rund 390 Zeilen ohne
   wiederverwendbare Vorlage.

Vor Schritt 1 dieser Staffelung muss die Aufteilung aus Abschnitt 2,
Schritte 1–5 stehen — insbesondere, dass das ausgelieferte Konsolenwerkzeug
dabei unverändert lauffähig bleibt. Es hat eine eigene Versionsnummer und
eigene Nutzer; ein Umbau, der es beschädigt, wäre ein Rückschritt, auch wenn
die Oberfläche danach schöner ist.
