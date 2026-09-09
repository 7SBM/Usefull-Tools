# Änderungsprotokoll

Vor dem 2026-08-24 wurde keines geführt. Die Einträge bis dahin sind daher
**aus dem Quellcode und aus Dateizeitstempeln rekonstruiert** und entsprechend
gekennzeichnet. Ab hier wird jede Änderung von Hand eingetragen.

Format lose nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/).

---

## [Unveröffentlicht]

### Hinzugefügt
- `tools/DayZAssetForge/` — **eigenes Blender-Addon (ab 5.0)** für die Mesh-Seite der
  DayZ-Item-Erstellung: teilt ein High-Poly-Modell Stück für Stück, dezimiert jedes Teil,
  backt die NOHQ-Normalmap vom High auf das Low (Cycles, Grün-Kanal umschaltbar) und
  exportiert Low-Mesh (OBJ) + `_no.png` + Object-Builder-To-Do. Reiner Kern getrennt von der
  UI, headless getestet in Blender 5.0 (9/9 grün). Liefert die Vorlage für den
  `rvmat_paa_generator.py` des Item-Generators; erzeugt selbst kein PAA/RVMAT. Design/Plan
  unter `docs/superpowers/`. (2026-09-09)
- `.gitignore`, `VENDOR.md`, dieses Änderungsprotokoll
- README auf den tatsächlichen Funktionsstand gebracht (beschrieb bis dahin nur
  die reine ODOL→MLOD-Fassung)
- `.rvmat`-Debinarisierung beim PBO-Entpacken (`[B]`-Modus, Batch- und
  Einzelmodus). Bisher wurden binarisierte `.rvmat`-Dateien nur roh als Bytes
  rausgeschrieben; jetzt nutzt derselbe `TryRapToCpp()`-Decoder, der schon
  `config.bin` verarbeitet, auch `*.rvmat`.
- `DayZAnimationPlugin_Voglefixed/` — Blender-Addon für Waffen-Handanimationen
  (IK-Poses, TXA/TXO Import/Export). Fremdcode (Mrtea101/JD) mit
  Kompatibilitätsfixes von Vogelmensch1989, siehe [`VENDOR.md`](VENDOR.md) für
  Attribution und die bewusst ausgelassenen Demo-Assets.

### Behoben
- Drei tote Bildverweise in der README (`before.png`, `console.png`,
  `after.png` — im Repository liegen `step1.png`–`step3.png`)

### Geplant
- Versionsnummer und `--version`-Schalter im Werkzeug selbst. `AssemblyVersion`
  steht derzeit auf dem Vorlagenwert `1.0.0.0`, während der Installer `1.9`
  meldet — die beiden widersprechen sich.

---

## [1.9] — 2026-06-22  *(rekonstruiert)*

Letzter Build von `Debinarizer.exe` (2026-06-22 01:00, MD5 `C8F5470B9872…`);
Versionsnummer aus `7SBM_P3D_DeBin_Setup.iss`.

### Hinzugefügt
- **`[R]` RTM inspizieren** — Arma-3-BMTR-Animationen lesen (Bone-Namen,
  Quaternionen), Ordner-Modus mit automatischem RTM→TXA-Export.
  *Als jüngste Erweiterung erkannt: `RtmInspect` steht im `enum ToolMode`
  hinter `PboExtract`, und der zugehörige `case`-Block liegt als letzter im
  Switch.*

## [vor 1.9] — Mai/Juni 2026  *(rekonstruiert, Reihenfolge aus der Enum-Folge)*

### Hinzugefügt
- **`[B]` PBO entpacken** samt eigenem PBO-Leser (LZSS-Dekompression,
  Header-Auswertung, Namensbereinigung) und automatischer Umwandlung
  `config.bin` → `config.cpp` über einen selbst geschriebenen raP-Decoder
- **`[E]` ANM exportieren** (ANM → TXA)
- **`[I]` ANM inspizieren** (Bone-Namen und Quaternionen)
- **`[A]` ANM konvertieren** (TXA + Template → ANM)
- Erzeugung einer `model.cfg` aus ODOL-Quellen
- Menüführung, Stapelverarbeitung ganzer Ordner, eingebettete Titelmusik,
- Inno-Setup-Installer mit Haftungshinweis

## [Ausgangsfassung] — 2026-04-14  *(fremd)*

`P3DDebinarizer` von *T_D*, Kompatibilitätsfassung („VogelFixed"), 134 Zeilen.
Konnte ausschließlich ODOL → MLOD. Liegt als Archiv unter
`p3d_Debinarizer_VogelFixed_2026/`.
