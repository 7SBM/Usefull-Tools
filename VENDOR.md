# Fremde Werkzeuge (nicht in diesem Repository)

Der lokale Werkzeugkasten enthält neben dem Eigenbau eine Reihe **fremder**
Projekte. Sie liegen bewusst **nicht** in diesem Repository: es sind fremde
Arbeiten mit eigenen Lizenzen, und sie im eigenen öffentlichen Repo
mitzuveröffentlichen wäre Weiterverbreitung unter fremdem Namen. Mehrere davon
überschreiten außerdem GitHubs 100-MB-Grenze pro Datei.

Diese Liste hält fest, **was gebraucht wird und woher es kommt**, damit sich eine
Arbeitsumgebung jederzeit neu aufbauen lässt.

## Blender-Erweiterungen

| Werkzeug | Herkunft | Anmerkung |
|---|---|---|
| **DayZ Animation Tools** | Mrtea101 / JD | Für Blender 4.x/5.x korrigiert von *Vogelmensch1989* (Stand 08.06.2026). Drei Korrekturen: `import bpy_types` entfernt, `bpy_types.*`-Annotationen auf `bpy.types.*` umgestellt, `bpy.utils.unregister_module()` entfernt. **Der Ordner muss im Addons-Verzeichnis `DayzAnimationTools` heißen**, sonst lädt das Addon nicht. |
| **Arma Toolbox for Blender** | Alwarren | Release 4.0.5. Import/Export von Arma-/DayZ-Assets. |

## Referenzmaterial

| Werkzeug | Herkunft | Anmerkung |
|---|---|---|
| **DayZ-Misc** | Bohemia Interactive | Offizielles Beispielmaterial: Body parts, Character Proxies, Powerlines, Rig and Animations, Road Parts, Water |
| **DayZ Community Offline Mode** | Community-Projekt | Offline-Testumgebung mit vollständigem Central-Economy-Satz |
| **ZenTemplate** | Zenarchist | Mod-Vorlage samt Workbench-Anleitung |
| **DayZ-Central-Economy** | Bohemia Interactive | Referenz-Wirtschaftsdaten |

## Anwendungen

| Werkzeug | Zweck |
|---|---|
| **QGIS** (3.44 / 4.0) | Terrainbearbeitung; die zugehörigen Spiel-Terrain-Addons liegen unter `Terrain Tools/` **in** diesem Repo |
| **Mikero AiO** (DePbo, ExtractPbo …) | PBO-Werkzeuge — das kommerzielle Gegenstück zum eigenen `[B]`-Modus |
| **pboProject** | PBO-Erzeugung |
| **DayZ Tools** (Steam) | AddonBuilder, FileBank, DSUtils — von `DayZ_Helper_Scripte/deploy.ps1` vorausgesetzt |
| **L3DT**, **FreeCAD**, **GIMP** | Heightmaps, Modellierung, Texturen |

## Was ausdrücklich nirgends hingehört

- **Vendor-Installer** jeder Art. Sie sind groß, teils kommerziell und in einem
  Git-Repo grundsätzlich fehl am Platz. Immer bei der jeweiligen Quelle laden.
- **Private Signaturschlüssel** (`*.biprivatekey`). Wer sie besitzt, kann PBOs
  signieren, die die Server als echt akzeptieren. Die öffentliche `*.bikey`
  hingegen ist zur Verteilung gedacht.
- **Persönliche DayZ-Profile** (`*.DayZProfile`) — enthalten Spielernamen und
  persönliche Einstellungen.
