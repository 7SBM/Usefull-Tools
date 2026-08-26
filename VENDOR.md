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
| **Arma Toolbox for Blender** | Alwarren | Release 4.0.5. Import/Export von Arma-/DayZ-Assets. |

> [!Ausnahme] DayZ Animation Tools — doch im Repo
> **DayZ Animation Tools** (Original: Mrtea101 / JD, für Blender 4.x/5.x korrigiert
> von *Vogelmensch1989*, Stand 08.06.2026) liegt entgegen der obigen Regel **doch**
> in diesem Repository, unter `DayZAnimationPlugin_Voglefixed/` — analog zum
> Präzedenzfall der QGIS-Terrain-Tools oben (ebenfalls ein gepatchter Fremd-Fork,
> der direkt eingecheckt ist). Attribution steht in der `ANLEITUNG.md` des Ordners.
> Drei Korrekturen gegenüber dem Original: `import bpy_types` entfernt,
> `bpy_types.*`-Annotationen auf `bpy.types.*` umgestellt,
> `bpy.utils.unregister_module()` entfernt. **Der Ordner muss beim Installieren im
> Blender-Addons-Verzeichnis in `DayzAnimationTools` umbenannt werden**, sonst lädt
> das Addon nicht. Die vom Original mitgelieferten Demo-Assets (`_AssetSamples/`,
> `_Referenz/` — Blend-Rigs, komplette Beispiel-Mission, ca. 330 MB) sind bewusst
> **nicht** mit übernommen; sie liegen weiterhin nur lokal auf `H:\Usefull Tools\`.

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
