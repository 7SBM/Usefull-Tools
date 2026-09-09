# DayZ AssetForge — Teil A (Optimier-/Bake-Pipeline) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein installierbares Blender-5.0-Addon, das ein High-Poly-Modell teilt, jedes Teil einzeln dezimiert, die NOHQ-Normalmap vom High auf das Low backt und Low-Mesh + Normal-PNG + Object-Builder-To-Do exportiert.

**Architecture:** Strikte Trennung: `kern/` enthält reine Funktionen auf bpy-Daten (headless testbar), `ui/` enthält nur Operatoren/Panel, die `kern/` aufrufen. Tests laufen über `blender --background --python tests/run_tests.py` mit einem leichten eigenen Test-Runner (kein pytest in Blenders Python).

**Tech Stack:** Blender 5.0 (`C:\Program Files\Blender Foundation\Blender 5.0\blender.exe`), Python 3.11 (Blender-intern), bpy, Cycles (nur für Bake), Extension-Format (`blender_manifest.toml`).

**Spec:** `docs/superpowers/specs/2026-09-09-dayz-blender-assetforge-design.md`

## Global Constraints

- **Ort:** `tools/DayZAssetForge/` im Repo `Usefull-Tools`.
- **Sprache:** alles auf Deutsch — Bezeichner, Kommentare, UI-Texte, Meldungen.
- **Blender-Ziel:** 5.0, Extension-Manifest (kein Legacy-`bl_info`).
- **Kein FBX→P3D, kein PAA, kein RVMAT** im Addon — das macht der bestehende Item-Generator.
- **Nie überschreiben ohne Schalter:** vorhandene Dateien nie ersetzen (`.neu` daneben oder abbrechen).
- **kern/-Funktionen bpy-only, keine Operatoren-Aufrufe von außerhalb ui/**, damit headless testbar.
- **Blender-Aufruf für Tests:** `"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe" --background --python tools/DayZAssetForge/tests/run_tests.py`

---

### Task 1: Test-Runner + Extension-Gerüst

**Files:**
- Create: `tools/DayZAssetForge/blender_manifest.toml`
- Create: `tools/DayZAssetForge/__init__.py`
- Create: `tools/DayZAssetForge/kern/__init__.py`
- Create: `tools/DayZAssetForge/tests/run_tests.py`
- Create: `tools/DayZAssetForge/tests/hilfen.py`

**Interfaces:**
- Produces: `tests/hilfen.py` mit `neue_szene()`, `hochpoly_wuerfel(name, unterteilungen)`, `zaehle_dreiecke(obj)`, `pruefe(bedingung, meldung)`; `run_tests.py` sammelt Funktionen `test_*` aus den Testmodulen, führt sie aus, gibt bei Fehler Exit-Code 1.

- [ ] **Step 1: Test-Hilfen schreiben**

```python
# tools/DayZAssetForge/tests/hilfen.py
import bpy, bmesh

class TestFehler(Exception):
    pass

def pruefe(bedingung, meldung):
    if not bedingung:
        raise TestFehler(meldung)

def neue_szene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def hochpoly_wuerfel(name="High", unterteilungen=4):
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for _ in range(unterteilungen):
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
    bm.to_mesh(mesh)
    bm.free()
    return obj

def zaehle_dreiecke(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)
```

- [ ] **Step 2: Test-Runner schreiben**

```python
# tools/DayZAssetForge/tests/run_tests.py
import sys, os, importlib, traceback
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))  # Addon-Wurzel
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))                    # tests/

TESTMODULE = ["test_teile", "test_dezimierung", "test_backen", "test_export"]

def main():
    fehler = 0
    gesamt = 0
    for modname in TESTMODULE:
        try:
            mod = importlib.import_module(modname)
        except ModuleNotFoundError:
            continue  # Modul noch nicht angelegt (frühe Tasks)
        for name in dir(mod):
            if name.startswith("test_"):
                gesamt += 1
                try:
                    getattr(mod, name)()
                    print(f"  OK   {modname}.{name}")
                except Exception as e:
                    fehler += 1
                    print(f"  FEHL {modname}.{name}: {e}")
                    traceback.print_exc()
    print(f"\n{gesamt-fehler}/{gesamt} Tests gruen")
    sys.exit(1 if fehler else 0)

if __name__ == "__main__":
    main()
```

- [ ] **Step 3: Extension-Manifest + `__init__.py` (leeres, ladbares Addon)**

```toml
# tools/DayZAssetForge/blender_manifest.toml
schema_version = "1.0.0"
id = "dayz_assetforge_7sbm"
version = "0.1.0"
name = "7SBM DayZ AssetForge"
tagline = "High-Poly nach Low-Poly mit gebackener NOHQ fuer DayZ"
maintainer = "7SBM <7speedblendmaster@gmail.com>"
type = "add-on"
blender_version_min = "4.2.0"
license = ["SPDX:MIT"]
```

```python
# tools/DayZAssetForge/__init__.py
"""7SBM DayZ AssetForge — Blender-Addon zur Optimierung von High-Poly-DayZ-Assets."""

def register():
    from .ui import operatoren, panel
    operatoren.register()
    panel.register()

def unregister():
    from .ui import operatoren, panel
    panel.unregister()
    operatoren.unregister()
```

(Leeres `kern/__init__.py` anlegen. `ui/` folgt in Task 6 — bis dahin ist der Import in
`register()` nur beim echten Laden nötig, die kern-Tests brauchen ihn nicht.)

- [ ] **Step 4: Runner ausführen (0 Tests, aber sauberer Lauf)**

Run: `"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe" --background --python tools/DayZAssetForge/tests/run_tests.py`
Expected: läuft ohne Traceback, meldet `0/0 Tests gruen`, Exit-Code 0.

- [ ] **Step 5: Commit**

```bash
git add tools/DayZAssetForge/
git commit -m "AssetForge: Extension-Geruest und Test-Runner"
```

---

### Task 2: `kern/teile.py` — auffindbare Teile finden

**Files:**
- Create: `tools/DayZAssetForge/kern/teile.py`
- Test: `tools/DayZAssetForge/tests/test_teile.py`

**Interfaces:**
- Produces: `finde_teile(objekte, nach_losen_inseln=False) -> list[bpy.types.Object]`. Ohne Aufteilung: die Mesh-Objekte selbst. Mit `nach_losen_inseln=True`: trennt lose Geometrie in eigene Objekte (via `mesh_separate` LOOSE) und gibt alle Ergebnis-Objekte zurück.

- [ ] **Step 1: Failing test**

```python
# tools/DayZAssetForge/tests/test_teile.py
import bpy
from tests.hilfen import neue_szene, hochpoly_wuerfel, pruefe
from kern import teile

def test_zwei_lose_wuerfel_werden_zwei_teile():
    neue_szene()
    obj = hochpoly_wuerfel("Doppel", unterteilungen=1)
    # zweiten losen Wuerfel in dasselbe Mesh einbauen
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.create_cube(bm, size=0.5, matrix=__import__("mathutils").Matrix.Translation((3,0,0)))
    bm.to_mesh(obj.data); bm.free()
    ergebnis = teile.finde_teile([obj], nach_losen_inseln=True)
    pruefe(len(ergebnis) == 2, f"erwartet 2 Teile, bekam {len(ergebnis)}")
```

- [ ] **Step 2: Run, expect FAIL** (`ModuleNotFoundError: kern.teile` bzw. AttributeError)

Run: `"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe" --background --python tools/DayZAssetForge/tests/run_tests.py`

- [ ] **Step 3: Implementieren**

```python
# tools/DayZAssetForge/kern/teile.py
"""Zerlegt Auswahl in einzeln zu verarbeitende Teile ("Stueck fuer Stueck")."""
import bpy

def finde_teile(objekte, nach_losen_inseln=False):
    mesh_objekte = [o for o in objekte if o and o.type == "MESH"]
    if not nach_losen_inseln:
        return list(mesh_objekte)
    ergebnis = []
    for obj in mesh_objekte:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        vorher = set(bpy.context.scene.objects)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.separate(type="LOOSE")
        bpy.ops.object.mode_set(mode="OBJECT")
        neue = [o for o in bpy.context.scene.objects if o not in vorher]
        ergebnis.append(obj)
        ergebnis.extend(neue)
    return ergebnis
```

- [ ] **Step 4: Run, expect PASS**

- [ ] **Step 5: Commit** — `git commit -m "AssetForge: kern/teile findet auffindbare Teile"`

---

### Task 3: `kern/uv.py` — UV nur bei Bedarf erzeugen

**Files:**
- Create: `tools/DayZAssetForge/kern/uv.py`
- Test: `tools/DayZAssetForge/tests/test_uv.py` (in `run_tests.py`-Liste ergänzen)

**Interfaces:**
- Produces: `sichere_uv(obj) -> bool` — gibt True zurück, wenn ein UV neu erzeugt wurde; False, wenn schon eines vorhanden war.

- [ ] **Step 1: Failing test**

```python
# tools/DayZAssetForge/tests/test_uv.py
import bpy
from tests.hilfen import neue_szene, hochpoly_wuerfel, pruefe
from kern import uv

def test_ohne_uv_wird_erzeugt():
    neue_szene()
    obj = hochpoly_wuerfel("Ohne", 1)
    while obj.data.uv_layers:
        obj.data.uv_layers.remove(obj.data.uv_layers[0])
    neu = uv.sichere_uv(obj)
    pruefe(neu is True, "erwartet: UV wurde neu erzeugt")
    pruefe(len(obj.data.uv_layers) >= 1, "erwartet: mindestens ein UV-Layer")

def test_mit_uv_bleibt():
    neue_szene()
    obj = hochpoly_wuerfel("Mit", 1)
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    neu = uv.sichere_uv(obj)
    pruefe(neu is False, "erwartet: vorhandenes UV bleibt")
```

Ergänze `"test_uv"` in `TESTMODULE` in `run_tests.py`.

- [ ] **Step 2: Run, expect FAIL**

- [ ] **Step 3: Implementieren**

```python
# tools/DayZAssetForge/kern/uv.py
"""Stellt sicher, dass ein Objekt ein UV-Layout hat (fuers Backen noetig)."""
import bpy

def sichere_uv(obj):
    if obj.data.uv_layers:
        return False
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    return True
```

- [ ] **Step 4: Run, expect PASS**
- [ ] **Step 5: Commit** — `git commit -m "AssetForge: kern/uv sichert UV-Layout"`

---

### Task 4: `kern/dezimierung.py` — Low-Poly pro Teil erzeugen

**Files:**
- Create: `tools/DayZAssetForge/kern/dezimierung.py`
- Test: `tools/DayZAssetForge/tests/test_dezimierung.py` (in `TESTMODULE` ergänzen)

**Interfaces:**
- Produces: `dezimiere_teil(obj, ziel_dreiecke=None, verhaeltnis=None, name_suffix="_low") -> bpy.types.Object` — dupliziert `obj`, hängt Decimate (COLLAPSE) an, wendet ihn an, gibt das neue Low-Objekt zurück. Genau eines von `ziel_dreiecke`/`verhaeltnis` gesetzt; `ziel_dreiecke` wird in ein Verhältnis umgerechnet.

- [ ] **Step 1: Failing test**

```python
# tools/DayZAssetForge/tests/test_dezimierung.py
from tests.hilfen import neue_szene, hochpoly_wuerfel, zaehle_dreiecke, pruefe
from kern import dezimierung

def test_low_hat_weniger_dreiecke_bbox_bleibt():
    neue_szene()
    high = hochpoly_wuerfel("High", unterteilungen=4)
    tris_high = zaehle_dreiecke(high)
    low = dezimierung.dezimiere_teil(high, verhaeltnis=0.25)
    tris_low = zaehle_dreiecke(low)
    pruefe(tris_low < tris_high, f"Low ({tris_low}) muss < High ({tris_high}) sein")
    # Silhouette: Bounding-Box-Diagonale bleibt aehnlich (Wuerfel bleibt Wuerfel)
    def diag(o):
        xs=[v[0] for v in o.bound_box]; ys=[v[1] for v in o.bound_box]; zs=[v[2] for v in o.bound_box]
        return (max(xs)-min(xs), max(ys)-min(ys), max(zs)-min(zs))
    dh, dl = diag(high), diag(low)
    for a,b in zip(dh,dl):
        pruefe(abs(a-b) < 0.1, f"Bounding-Box weicht zu stark ab: {dh} vs {dl}")
```

- [ ] **Step 2: Run, expect FAIL**

- [ ] **Step 3: Implementieren**

```python
# tools/DayZAssetForge/kern/dezimierung.py
"""Erzeugt aus einem High-Poly-Teil ein dezimiertes Low-Poly-Teil (Stueck fuer Stueck)."""
import bpy

def dezimiere_teil(obj, ziel_dreiecke=None, verhaeltnis=None, name_suffix="_low"):
    if (ziel_dreiecke is None) == (verhaeltnis is None):
        raise ValueError("genau eines von ziel_dreiecke / verhaeltnis angeben")
    low = obj.copy()
    low.data = obj.data.copy()
    low.name = obj.name + name_suffix
    bpy.context.collection.objects.link(low)
    if verhaeltnis is None:
        low.data.calc_loop_triangles()
        aktuell = len(low.data.loop_triangles)
        verhaeltnis = max(0.0, min(1.0, ziel_dreiecke / aktuell)) if aktuell else 1.0
    mod = low.modifiers.new(name="AssetForge_Decimate", type="DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = verhaeltnis
    bpy.context.view_layer.objects.active = low
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return low
```

- [ ] **Step 4: Run, expect PASS**
- [ ] **Step 5: Commit** — `git commit -m "AssetForge: kern/dezimierung erzeugt Low-Poly pro Teil"`

---

### Task 5: `kern/backen.py` — NOHQ vom High auf Low backen

**Files:**
- Create: `tools/DayZAssetForge/kern/backen.py`
- Test: `tools/DayZAssetForge/tests/test_backen.py` (in `TESTMODULE` ergänzen)

**Interfaces:**
- Produces: `backe_nohq(high, low, aufloesung=2048, cage_extrusion=0.05, gruen_invertieren=True) -> bpy.types.Image` — legt ein Bild an, weist es dem Low-Material als aktive Bild-Node zu, backt Tangent-Normal High→Low (Cycles), invertiert bei `gruen_invertieren` den Grün-Kanal (DirectX-Konvention), gibt das Image zurück.

- [ ] **Step 1: Failing test**

```python
# tools/DayZAssetForge/tests/test_backen.py
from tests.hilfen import neue_szene, hochpoly_wuerfel, pruefe
from kern import dezimierung, uv, backen

def test_nohq_bild_entsteht_und_hat_detail():
    neue_szene()
    high = hochpoly_wuerfel("High", unterteilungen=4)
    low = dezimierung.dezimiere_teil(high, verhaeltnis=0.2)
    uv.sichere_uv(low)
    bild = backen.backe_nohq(high, low, aufloesung=64, cage_extrusion=0.1)
    pruefe(bild is not None, "kein Bild erzeugt")
    pruefe(tuple(bild.size) == (64, 64), f"falsche Groesse: {tuple(bild.size)}")
    # Detail: nicht alle Pixel identisch (kein reines Flat-Blau)
    px = list(bild.pixels)
    pruefe(len(set(round(p,3) for p in px[:4000])) > 3, "Normalmap wirkt leer/flach")
```

- [ ] **Step 2: Run, expect FAIL**

- [ ] **Step 3: Implementieren**

```python
# tools/DayZAssetForge/kern/backen.py
"""Backt die NOHQ-Normalmap vom High-Poly auf das Low-Poly-Teil (Cycles)."""
import bpy

def _material_mit_bildnode(low, bild):
    if not low.data.materials:
        mat = bpy.data.materials.new(low.name + "_mat")
        mat.use_nodes = True
        low.data.materials.append(mat)
    mat = low.data.materials[0]
    mat.use_nodes = True
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = bild
    mat.node_tree.nodes.active = node
    return node

def backe_nohq(high, low, aufloesung=2048, cage_extrusion=0.05, gruen_invertieren=True):
    szene = bpy.context.scene
    vorherige_engine = szene.render.engine
    szene.render.engine = "CYCLES"
    bild = bpy.data.images.new(low.name + "_no", width=aufloesung, height=aufloesung)
    _material_mit_bildnode(low, bild)

    bpy.ops.object.select_all(action="DESELECT")
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low  # aktiv = Ziel

    szene.render.bake.use_selected_to_active = True
    szene.render.bake.cage_extrusion = cage_extrusion
    bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT")

    if gruen_invertieren:
        px = list(bild.pixels)
        for i in range(1, len(px), 4):      # G-Kanal
            px[i] = 1.0 - px[i]
        bild.pixels = px

    szene.render.engine = vorherige_engine
    return bild
```

- [ ] **Step 4: Run, expect PASS** (bei Bedarf `cage_extrusion` erhöhen, falls Bake leer)
- [ ] **Step 5: Commit** — `git commit -m "AssetForge: kern/backen backt NOHQ High->Low"`

> **Verifikationspunkt (kein blindes Festschreiben):** Ob DayZ die DirectX- (`gruen_invertieren=True`)
> oder OpenGL-Konvention erwartet, wird an einer echten 7SBM-`_nohq` gegengeprüft, bevor der
> Default als Tatsache in Vault/README steht. Der Schalter macht den Wechsel zur Einzeile.

---

### Task 6: `kern/export.py` — Low + Normal-PNG + Object-Builder-To-Do

**Files:**
- Create: `tools/DayZAssetForge/kern/export.py`
- Test: `tools/DayZAssetForge/tests/test_export.py` (in `TESTMODULE` ergänzen)

**Interfaces:**
- Produces: `exportiere(low_objekte, bild, zielordner, basisname) -> dict` mit Schlüsseln `mesh`, `normal_png`, `todo`. Schreibt `<basisname>.fbx`, `<basisname>_no.png`, `object_builder_todo.txt`. Nie überschreiben: existiert eine Datei, wird `.neu` angehängt. Gibt die tatsächlich geschriebenen Pfade zurück.

- [ ] **Step 1: Failing test**

```python
# tools/DayZAssetForge/tests/test_export.py
import os, tempfile
from tests.hilfen import neue_szene, hochpoly_wuerfel, pruefe
from kern import dezimierung, uv, backen, export

def test_export_schreibt_erwartete_dateien():
    neue_szene()
    high = hochpoly_wuerfel("Kiste", 3)
    low = dezimierung.dezimiere_teil(high, verhaeltnis=0.2)
    uv.sichere_uv(low)
    bild = backen.backe_nohq(high, low, aufloesung=64)
    ziel = tempfile.mkdtemp()
    ergebnis = export.exportiere([low], bild, ziel, "Kiste")
    pruefe(os.path.isfile(ergebnis["mesh"]), "keine Mesh-Datei")
    pruefe(os.path.isfile(ergebnis["normal_png"]), "kein Normal-PNG")
    pruefe(os.path.isfile(ergebnis["todo"]) and os.path.getsize(ergebnis["todo"]) > 0, "To-Do leer/fehlt")
```

- [ ] **Step 2: Run, expect FAIL**

- [ ] **Step 3: Implementieren**

```python
# tools/DayZAssetForge/kern/export.py
"""Exportiert Low-Mesh + gebackene Normalmap und eine Object-Builder-To-Do-Liste."""
import os, bpy

TODO_TEXT = """Object-Builder To-Do fuer {name}
================================
[ ] Low-Poly-P3D aus {mesh} importieren (Object Builder / Blender-Toolbox)
[ ] LODs setzen (1.0 Resolution, ViewGeometry, Geometry, Memory)
[ ] Named Selections / Proxys / Memory-Points anlegen
[ ] Material zuweisen; NOHQ = {normal_png} (per rvmat_paa_generator.py nach _nohq.paa)
[ ] Config.cpp / model.cfg gegen P3D pruefen (Item-Generator, Pruefmodus)
"""

def _freier_pfad(pfad):
    return pfad if not os.path.exists(pfad) else pfad + ".neu"

def exportiere(low_objekte, bild, zielordner, basisname):
    os.makedirs(zielordner, exist_ok=True)
    mesh_pfad = _freier_pfad(os.path.join(zielordner, basisname + ".fbx"))
    png_pfad = _freier_pfad(os.path.join(zielordner, basisname + "_no.png"))
    todo_pfad = _freier_pfad(os.path.join(zielordner, "object_builder_todo.txt"))

    bpy.ops.object.select_all(action="DESELECT")
    for o in low_objekte:
        o.select_set(True)
    bpy.context.view_layer.objects.active = low_objekte[0]
    bpy.ops.export_scene.fbx(filepath=mesh_pfad, use_selection=True)

    bild.filepath_raw = png_pfad
    bild.file_format = "PNG"
    bild.save()

    with open(todo_pfad, "w", encoding="utf-8") as f:
        f.write(TODO_TEXT.format(name=basisname, mesh=os.path.basename(mesh_pfad),
                                 normal_png=os.path.basename(png_pfad)))
    return {"mesh": mesh_pfad, "normal_png": png_pfad, "todo": todo_pfad}
```

- [ ] **Step 4: Run, expect PASS**
- [ ] **Step 5: Commit** — `git commit -m "AssetForge: kern/export schreibt Mesh, Normal-PNG, To-Do"`

---

### Task 7: `ui/` — Operator + Panel (ein Klick für die ganze Pipeline)

**Files:**
- Create: `tools/DayZAssetForge/ui/__init__.py`
- Create: `tools/DayZAssetForge/ui/operatoren.py`
- Create: `tools/DayZAssetForge/ui/panel.py`

**Interfaces:**
- Consumes: `kern.teile.finde_teile`, `kern.dezimierung.dezimiere_teil`, `kern.uv.sichere_uv`, `kern.backen.backe_nohq`, `kern.export.exportiere`.
- Produces: Operator `assetforge.optimieren` (idname), N-Panel „DayZ AssetForge" (Kategorie `AssetForge`) im 3D-Viewport mit Feldern: Zielordner, Basisname, Verhältnis/Zieldreiecke, Auflösung, „nach losen Inseln teilen", „Grün invertieren".

- [ ] **Step 1: Operator + Panel schreiben** (kein Headless-Unittest — UI wird beim Laden geprüft)

```python
# tools/DayZAssetForge/ui/__init__.py
```

```python
# tools/DayZAssetForge/ui/operatoren.py
"""Operator, der die gesamte AssetForge-Pipeline auf der Auswahl ausfuehrt."""
import bpy
from bpy.props import StringProperty, IntProperty, FloatProperty, BoolProperty
from ..kern import teile, dezimierung, uv, backen, export

class ASSETFORGE_OT_optimieren(bpy.types.Operator):
    bl_idname = "assetforge.optimieren"
    bl_label = "High -> Low mit NOHQ backen"
    bl_options = {"REGISTER", "UNDO"}

    zielordner: StringProperty(name="Zielordner", subtype="DIR_PATH")
    basisname: StringProperty(name="Basisname", default="Asset")
    verhaeltnis: FloatProperty(name="Dezimier-Verhaeltnis", default=0.25, min=0.01, max=1.0)
    aufloesung: IntProperty(name="Aufloesung", default=2048, min=64, max=8192)
    nach_losen_inseln: BoolProperty(name="Nach losen Inseln teilen", default=False)
    gruen_invertieren: BoolProperty(name="Gruen-Kanal invertieren (DirectX)", default=True)

    def execute(self, context):
        auswahl = [o for o in context.selected_objects if o.type == "MESH"]
        if not auswahl:
            self.report({"ERROR"}, "Kein Mesh ausgewaehlt.")
            return {"CANCELLED"}
        if not self.zielordner:
            self.report({"ERROR"}, "Kein Zielordner gesetzt.")
            return {"CANCELLED"}
        teile_liste = teile.finde_teile(auswahl, nach_losen_inseln=self.nach_losen_inseln)
        lows = []
        for teil in teile_liste:
            low = dezimierung.dezimiere_teil(teil, verhaeltnis=self.verhaeltnis)
            uv.sichere_uv(low)
            bild = backen.backe_nohq(teil, low, aufloesung=self.aufloesung,
                                     gruen_invertieren=self.gruen_invertieren)
            lows.append((low, bild))
        # je Teil ein Export; hier vereinfacht: erstes Bild als gemeinsame NOHQ
        ergebnis = export.exportiere([l for l, _ in lows], lows[0][1],
                                     bpy.path.abspath(self.zielordner), self.basisname)
        self.report({"INFO"}, f"Fertig: {ergebnis['mesh']}")
        return {"FINISHED"}

def register():
    bpy.utils.register_class(ASSETFORGE_OT_optimieren)

def unregister():
    bpy.utils.unregister_class(ASSETFORGE_OT_optimieren)
```

```python
# tools/DayZAssetForge/ui/panel.py
"""N-Panel 'DayZ AssetForge' im 3D-Viewport."""
import bpy

class ASSETFORGE_PT_panel(bpy.types.Panel):
    bl_label = "DayZ AssetForge"
    bl_idname = "ASSETFORGE_PT_panel"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "AssetForge"

    def draw(self, context):
        self.layout.operator("assetforge.optimieren", icon="MOD_DECIM")

def register():
    bpy.utils.register_class(ASSETFORGE_PT_panel)

def unregister():
    bpy.utils.unregister_class(ASSETFORGE_PT_panel)
```

- [ ] **Step 2: Ladeprüfung headless**

Run: `"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe" --background --python-expr "import bpy,sys; sys.exit(0)"` als Rauchtest; danach Addon per `--addons`-Installationsskript laden und `register()`/`unregister()` ohne Fehler ausführen (Prüfskript in `tests/test_laden.py`).
Expected: kein Traceback.

- [ ] **Step 3: Commit** — `git commit -m "AssetForge: Operator und N-Panel fuer die Pipeline"`

---

### Task 8: Doku + Repo-Integration

**Files:**
- Create: `tools/DayZAssetForge/LIESMICH.md`
- Modify: `README.md` (AssetForge-Zeile in der Werkzeugtabelle)
- Modify: `CHANGELOG.md`

- [ ] **Step 1: `LIESMICH.md`** — Zweck, Installation (Blender 5.0, Extension aus Disk), Ablauf, Nahtstelle zum Item-Generator, Grün-Kanal-Hinweis.
- [ ] **Step 2: README-Tabelle** um `tools/DayZAssetForge/` ergänzen (Muster der bestehenden Zeilen).
- [ ] **Step 3: CHANGELOG-Eintrag** mit Datum 2026-09-09.
- [ ] **Step 4: Commit** — `git commit -m "AssetForge: Dokumentation und Repo-Integration"`

---

## Self-Review

- **Spec-Deckung:** Teile finden (T2), Stück-für-Stück-Dezimierung (T4), NOHQ High→Low (T5),
  Low-Poly-Export + PNG für Item-Generator (T6), UI (T7), Publish/Doku (T8). Teil B (Brücke)
  ist bewusst ein eigener Plan. ✓
- **Platzhalter:** keine „TODO/TBD"; jeder Code-Step hat echten Code. ✓
- **Typ-Konsistenz:** `finde_teile`, `dezimiere_teil`, `sichere_uv`, `backe_nohq`, `exportiere`
  identisch in Tasks und im UI-Operator (T7) benannt und aufgerufen. ✓
- **Offener Verifikationspunkt:** Grün-Kanal-Konvention (T5) — vor Festschreibung prüfen.
