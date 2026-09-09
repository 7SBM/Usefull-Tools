# Design — 7SBM DayZ AssetForge (Blender-Addon)

*Stand 2026-09-09 · 7SBM-Laptop · Repo `Usefull-Tools`, Branch `worktree-assetforge`*

## Henrys Auftrag im Wortlaut (09.09.2026)

> ich wuerde gern ein Blender addon bauen: ein addon fuer Blender was mir via chat
> mit dir OHNE API nur mit normaler subscription 3D assets von scratch generiert und
> das HIGH direkt bei beispielsweise hinzufuegen eines High Poly models, das Rebacken
> der NOHQ map und runter dimmen JEDES EINZELNEN AUFFINDBAREN TEILS STUECK FUER STUECK
> UM AUF EINE low poly version mit HIGH POLY nohq zu kommen ohne verluste der
> sichtbaren item Qualitaet zu haben. das ganze soll als in dem githubprojekt
> "usefull Tools" einen Platz bekommen und dadurch mit den usfull tools einfach
> gepublished werden.

Ergänzung (09.09.2026):
> mach alles selbstaengig ich habe dir meine Funktions beschreibung gegeben mehr
> sollte mittlerweile nicht nötig sein … nutze die SKILLS GEZIELT
> nutze alle skills die wir kennen gezielt und verwenden die vaults

## Zweck

Ein Blender-Addon, das die **Mesh-Seite** der DayZ-Asset-Erstellung abdeckt — genau die
Lücke, die der bestehende [[7SBM Item-Generator]] (C#/WPF in `7SBM-DayZ-Tools`) bewusst
offen lässt („FBX → P3D kann kein Werkzeug außer Object Builder/Blender-Toolbox").

Zwei Teile in **einem** Addon:

- **Teil A — Optimier-/Bake-Pipeline (deterministisch, kein KI-Bedarf):** High-Poly rein →
  NOHQ-Normalmap vom High auf ein Low backen → jedes auffindbare Teil einzeln dezimieren →
  Low-Poly-Version, die dank High-Poly-NOHQ sichtbar gleich aussieht. **Wird zuerst gebaut.**
- **Teil B — „Von scratch per Chat" ohne API:** Eine Brücke, über die Claude (Claude Code /
  Claude Desktop, angetrieben von Henrys **Subscription**, **kein API-Key**) live Geometrie
  in Blender baut. **Wird nach A gebaut.**

## Nahtstelle zum bestehenden Werkzeug

AssetForge erzeugt am Ende `<name>_no.png` (High-Poly-Normal auf Low gebacken, 2048²) und
optional `<name>_ao.png`. Diese landen im Texturordner der Mod-Source. Von dort übernimmt der
bestehende **`rvmat_paa_generator.py`** (Item-Generator, Phase 1): `_no.png` → `_nohq.paa`
(DXT5), RVMAT nach Honeybadger-Vorlage. AssetForge baut also **keine** eigene PAA/RVMAT-Kette
— es liefert die Mesh + Normalmap, das bestehende Werkzeug macht den Rest. Keine Dubletten.

## Nicht-Ziele (YAGNI)

- **Kein FBX→P3D.** Das bleibt Object Builder. AssetForge exportiert FBX/OBJ + Normalmap und
  erzeugt (wie der Item-Generator) eine Object-Builder-To-Do-Liste.
- **Kein eigener PAA-Konverter, keine eigene RVMAT-Erzeugung** — macht der Item-Generator.
- **Kein API-Key, keine Cloud-3D-Generierung im Addon.** Teil B nutzt ausschließlich die
  MCP-Brücke zur laufenden Claude-Subscription.

## Architektur

Addon-Ordner `tools/DayZAssetForge/` im Repo `Usefull-Tools`. Blender-5.0-**Extension**
(`blender_manifest.toml`). Strikte Trennung, damit der Kern **headless testbar** ist:

```
tools/DayZAssetForge/
├── blender_manifest.toml         # Extension-Manifest (Blender 5.0)
├── __init__.py                   # register()/unregister(), lädt Untermodule
├── kern/                         # REINE LOGIK auf bpy-Daten — testbar per blender --background
│   ├── teile.py                  # finde_teile(): trennt ein Objekt in "auffindbare Teile"
│   ├── dezimierung.py            # dezimiere_teil(): Decimate-Modifier pro Teil, Zielbudget
│   ├── backen.py                 # backe_nohq(): Normal-Bake High→Low (Cycles), Grün-Kanal-Option
│   ├── uv.py                     # sichere_uv(): Smart-UV-Project nur wenn kein UV vorhanden
│   └── export.py                 # exportiere(): FBX/OBJ + Normal-PNG + Object-Builder-To-Do
├── ui/
│   ├── operatoren.py             # bpy.types.Operator (ruft nur kern/-Funktionen)
│   └── panel.py                  # N-Panel "DayZ AssetForge" im 3D-Viewport
├── bruecke/                      # Teil B (nach A) — MCP-Brücke, kein API-Key
│   ├── server.py                 # Socket-Server IN Blender (127.0.0.1), Main-Thread-Queue
│   └── mcp_server.py             # eigenständiger MCP-Server (stdio) für Claude Code/Desktop
├── tests/                        # blender --background --python-Tests
│   ├── conftest_blender.py
│   ├── test_teile.py
│   ├── test_dezimierung.py
│   └── test_backen.py
└── LIESMICH.md
```

### Teil A — Ablauf (ein Klick pro Teil oder für die Auswahl)

1. **Teile finden** (`kern/teile.py`): „auffindbares Teil" = jedes ausgewählte Mesh-Objekt;
   optional Aufteilung nach losen Inseln (`separate loose`) oder Material-Slots. Jedes Teil
   wird eigenständig verarbeitet (Henrys „Stück für Stück").
2. **UV sichern** (`kern/uv.py`): hat das Teil kein UV, Smart-UV-Project; vorhandenes UV bleibt.
3. **Low erzeugen + dezimieren** (`kern/dezimierung.py`): Kopie des Teils, Decimate-Modifier
   (COLLAPSE-Ratio bzw. Zieldreieckszahl je Teil), Silhouette bleibt erhalten. Budget pro Teil
   aus einem Qualitäts-Preset (z. B. „Inventar-Item", „Weltobjekt", „aggressiv").
4. **NOHQ backen** (`kern/backen.py`): Tangent-Space-Normal vom High auf das Low (Cycles-Bake,
   `use_selected_to_active`, Cage/Extrusion). Ziel-Image 2048² (wie Meshy-Exporte). **DayZ-Grün-
   Kanal-Konvention** als Schalter (Default an existierender 7SBM-`_nohq` verifizieren).
5. **Export** (`kern/export.py`): Low-Mesh (FBX/OBJ) + `<name>_no.png` (+ optional `_ao.png`)
   in den Zielordner, plus `object_builder_todo.txt` (LODs, Named Selections, Memory-Points,
   Material-Zuweisung — analog Item-Generator).

### Teil B — MCP-Brücke ohne API

- **In Blender:** `bruecke/server.py` startet auf Knopfdruck einen TCP-Server auf `127.0.0.1`.
  Kommandos (JSON) werden über einen `bpy.app.timers`-Queue **im Main-Thread** ausgeführt
  (bpy ist nicht thread-sicher).
- **Für Claude:** `bruecke/mcp_server.py` ist ein eigenständiger MCP-Server (stdio), den Claude
  Code / Claude Desktop als MCP-Server einträgt. Claude spricht über die **Subscription** mit
  dem MCP-Server; der leitet Werkzeugaufrufe an den Blender-Socket weiter. **Kein Anthropic-
  API-Key** — genau Henrys „ohne API, nur Subscription".
- **Werkzeuge:** `szene_info()`, `bpy_ausfuehren(code)`, `primitive_erzeugen(...)`,
  sowie High-Level `asset_optimieren()` / `nohq_backen()`, die Teil A aufrufen. So baut Claude
  im Chat Geometrie „von scratch" und übergibt sie direkt an die Optimier-Pipeline.

## Fehlerbehandlung

- Kein Mesh ausgewählt / kein aktives Objekt → klare deutsche Meldung im Operator-Report.
- Kein UV und Smart-UV schlägt fehl → abbrechen mit Hinweis, nichts Halbfertiges hinterlassen.
- Bake ohne Cycles-Renderengine → Engine temporär auf Cycles, danach zurücksetzen.
- Nie vorhandene Dateien überschreiben ohne Schalter (analog rvmat_paa_generator: `.neu` daneben).
- Zielordner nicht schreibbar → abbrechen mit Pfad im Klartext.

## Tests (headless)

`blender --background --python tests/run_tests.py`. Testet **kern/**-Funktionen an
prozedural erzeugter Geometrie (High-Poly-Würfel mit Subdivision/Displacement):

- `finde_teile`: mehrteiliges Objekt → korrekte Anzahl Teile.
- `dezimiere_teil`: Low hat weniger Dreiecke als High, Bounding-Box ~gleich (Silhouette).
- `backe_nohq`: Normal-Image wird erzeugt, ist kein Flat-Blau (Detail vorhanden), Datei auf Platte.
- `export`: erwartete Dateien liegen im Zielordner, To-Do-Liste nicht leer.

Blender 5.0 vorhanden: `C:\Program Files\Blender Foundation\Blender 5.0\blender.exe`.

## Publish

Als Ordner `tools/DayZAssetForge/` im öffentlichen Repo `Usefull-Tools` → README-Eintrag +
CHANGELOG. Damit wird das Addon „mit den usefull tools einfach gepublished". Auslieferung an
Nutzer als Extension-ZIP über GitHub Releases (Binär-/ZIP-Konvention des Repos: nicht in die
Versionsgeschichte, sondern über Releases).

## Reihenfolge

1. Teil A vollständig, getestet, gepusht (nutzbares, publishbares Ergebnis).
2. Teil B (MCP-Brücke) obendrauf.
3. Vault-Notiz `Projekte/7SBM DayZ AssetForge.md` (Regel „Vault-Doku pro Source"),
   Zweitprüfung durch Server-Claude vor der Fertigmeldung.

## Verwandt

- [[7SBM Item-Generator]] — die Config/RVMAT/PAA-Seite; AssetForge liefert dessen Mesh-Input
- [[Repo - Usefull-Tools]] — Zielrepo (öffentlich)
- `DayZAnimationPlugin_Voglefixed/` — bestehender Blender-Addon-Präzedenzfall im selben Repo
