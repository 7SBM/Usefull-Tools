# 7SBM DayZ AssetForge (Blender-Addon)

Bringt ein High-Poly-Modell **Stück für Stück** auf eine Low-Poly-Version und backt die
**NOHQ-Normalmap** vom High auf das Low — die sichtbare Detailqualität bleibt erhalten,
obwohl die Geometrie stark reduziert wird. Deckt damit die **Mesh-Seite** der
DayZ-Item-Erstellung ab, die der [7SBM Item-Generator](../../README.md) bewusst offen lässt.

> **Sprache:** Deutsch (UI, Meldungen, Code). **Blender:** ab 5.0 (Extension-Format).

## Was es tut (Teil A)

Auswahl an High-Poly-Objekten → für **jedes auffindbare Teil**:

1. **Teilen** — jedes Mesh-Objekt ist ein Teil; optional zusätzlich nach losen Inseln trennen.
2. **UV sichern** — fehlt ein UV, wird per Smart-UV-Project eines erzeugt (vorhandenes bleibt).
3. **Dezimieren** — eine Low-Kopie per Decimate-Modifier auf ein Verhältnis / eine Zieldreieckszahl.
4. **NOHQ backen** — Tangent-Space-Normal vom High auf das Low (Cycles), Auflösung frei
   (Standard 2048²). Grün-Kanal umschaltbar (DirectX ↔ OpenGL).
5. **Exportieren** — `<name>.obj` + `<name>_no.png` + `object_builder_todo.txt` in den Zielordner.

## Nahtstelle zum Item-Generator

AssetForge liefert **Mesh + Normal-PNG**. Die weitere DayZ-Kette macht das bestehende Werkzeug:
`<name>_no.png` → per **`rvmat_paa_generator.py`** (Item-Generator) nach `_nohq.paa` (DXT5) +
RVMAT. AssetForge erzeugt **kein** PAA und **keine** RVMAT — keine doppelte Kette.

**Bewusste Grenze:** FBX/OBJ → **P3D** bleibt Object Builder / Blender-Toolbox. AssetForge legt
dafür die `object_builder_todo.txt` an (LODs, Named Selections, Memory-Points, Material).

## Installation

Blender 5.0 → **Bearbeiten → Einstellungen → Erweiterungen → ▾ → Von Datei installieren…** →
den Addon-Ordner als ZIP wählen (bzw. Auslieferung als Extension-ZIP über GitHub Releases).

## Bedienung

3D-Viewport → N-Panel (Taste **N**) → Reiter **AssetForge**. High-Poly auswählen, Zielordner
und Basisname setzen, **„High → Low mit NOHQ backen"**.

## Tests (headless)

```
"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe" --background --factory-startup ^
  --python tools\DayZAssetForge\tests\run_tests.py
```

Prüft die Kern-Funktionen und einen kompletten Operator-Durchlauf an prozeduraler Geometrie.

## Offener Verifikationspunkt

Ob DayZ die **DirectX**- oder **OpenGL**-Grün-Konvention der Normalmap erwartet, ist an einer
echten 7SBM-`_nohq` gegenzuprüfen; der Schalter „Grün-Kanal invertieren" macht den Wechsel zur
Einzeile. Standard derzeit: invertiert (DirectX).
