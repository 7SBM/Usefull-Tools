# 7SBM Usefull-Tools

Werkzeuge rund um DayZ- und Arma-Modding, die im Rahmen der **7SBM**-Projekte
entstanden sind — allen voran **7SBM P3D.DeBin**, ein Konverter für P3D-, PBO-
und Animationsdateien.

> **Sprache:** Die Werkzeuge selbst sind auf Deutsch. Diese README ebenfalls.

---

## 🧰 7SBM P3D.DeBin

Konsolenwerkzeug mit Menüführung. Startet man es ohne Argumente, erscheint die
Modusauswahl:

| Taste | Funktion |
|-------|----------|
| `[P]` | **P3D debinarisieren** — ODOL (binarisiert) → MLOD (bearbeitbar) |
| `[A]` | **ANM konvertieren** — TXA + Template → ANM |
| `[E]` | **ANM exportieren** — ANM → TXA |
| `[I]` | **ANM inspizieren** — Bone-Namen und Quaternionen anzeigen |
| `[R]` | **RTM inspizieren** — Arma-3-BMTR lesen, Ordner-Modus mit RTM→TXA-Export |
| `[B]` | **PBO entpacken** — inklusive automatischer Umwandlung `config.bin` → `config.cpp` |

Nebenfunktionen: Erzeugen einer `model.cfg` aus ODOL-Quellen, optionales Entfernen
des `_mlod`-Suffix, Stapelverarbeitung ganzer Ordner (rekursiv).

### Aufruf

```
Debinarizer.exe                            # Menü (interaktiv)
Debinarizer.exe pfad\model.p3d             # eine Datei konvertieren
Debinarizer.exe eingabeOrdner [ausgabeOrdner]   # alle .p3d rekursiv
```

Ein-/Ausgabe im P3D-Modus: `model.p3d` (ODOL) → `model_mlod.p3d` (MLOD).

### Screenshots

| | |
|---|---|
| ![Schritt 1](/images/step1.png) | ![Schritt 2](/images/step2.png) |
| ![Schritt 3](/images/step3.png) | |

### Installation

Fertige Installer werden über **[Releases](../../releases)** verteilt, nicht über
dieses Repository — Binärdateien blähen die Versionsgeschichte auf. Wer selbst
bauen möchte, findet den vollständigen Quellcode unter
`DayZ_Arma_p3dDeBin/_SOURCE/` (.NET Framework 4.6.1).

> Windows SmartScreen blockiert den Installer beim ersten Start, da er nicht
> signiert ist. „Weitere Informationen" → „Trotzdem ausführen".

---

## 📂 Was sonst im Repository liegt

| Ordner | Inhalt |
|--------|--------|
| `DayZ_Arma_p3dDeBin/_SOURCE/` | Quellcode von P3D.DeBin inkl. `BisDll`-Modellbibliothek und Inno-Setup-Skript |
| `DayZ_Helper_Scripte/` | 19 Skripte: Heightmap-/Terrainbearbeitung (ASC, Shapefiles), `mapgroupproto`-Generator, SHP→DayZ-Koordinaten, RVMAT-Texturpatcher, Deploy- und Backup-Skripte |
| `DayZ_ProxyIcons/` | 36 freigestellte Proxy-Icons für Inventar-Attachment-Slots |
| `7SBM_Animation Projekt/` | Animationsarbeit: Blender-Quellen, ANM/TXA/RTM-Paare, Waterfall-Mod |
| `DayZ-Terrain-Config-Files-main/` | Terrain-Grundgerüst (`layers.cfg`, `buldozer.c`, `config.cpp`) |
| `Terrain Tools/` | QGIS-Addons für Spiel-Terrains |
| `p3d_Debinarizer_VogelFixed_2026/` | die fremde Ausgangsfassung, aus der P3D.DeBin hervorging (Archiv) |

Fremde Werkzeuge und Vendor-Installer liegen **bewusst nicht** hier — Herkunft und
Versionen stehen in [`VENDOR.md`](VENDOR.md). Was aus welchem Grund
ausgeschlossen ist, regelt die [`.gitignore`](.gitignore).

---

## 🧠 Herkunft und Danksagung

P3D.DeBin baut auf fremder Vorarbeit auf:

- **P3DDebinarizer** von *T_D* — der ursprüngliche ODOL→MLOD-Konverter
- <https://github.com/Mekz0/P3D-Debinarizer-Arma-3> — Fassung, aus der die
  `BisDll`-Modellbibliothek stammt
- Kompatibilitätskorrekturen für neuere Umgebungen von **Vogelmensch1989**

Die Ausgangsfassung umfasste 134 Zeilen und konnte ausschließlich ODOL→MLOD.
Alles Weitere — ANM/TXA, RTM, PBO-Entpacken, raP→CPP, `model.cfg`-Erzeugung,
Menüführung und Stapelverarbeitung — kam in diesem Projekt dazu.

---

## ⚠️ Hinweis zur Nutzung

Diese Werkzeuge dienen Lern-, Analyse-, Forschungs- und Kompatibilitätszwecken.
Ob eine konkrete Verwendung mit Lizenzbedingungen, Urheberrechten und den Rechten
Dritter vereinbar ist, hat jede Nutzerin und jeder Nutzer selbst sicherzustellen.
Weder Softwarepiraterie noch das unerlaubte Vervielfältigen, Weitergeben oder
kommerzielle Verwerten fremder Inhalte werden unterstützt.

Bereitstellung **ohne Gewähr**, Nutzung auf eigene Verantwortung.

---

## 👤 Autor

Henry Meissner

## 📜 Lizenz

MIT — siehe [`LICENSE.md`](LICENSE.md).
Fremdanteile unterliegen den Lizenzen ihrer jeweiligen Urheber.
