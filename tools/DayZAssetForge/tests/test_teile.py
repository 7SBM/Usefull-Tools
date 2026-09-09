"""Tests fuer kern/teile."""
import bmesh
import mathutils
from hilfen import neue_szene, hochpoly_wuerfel, pruefe
from kern import teile


def test_ohne_aufteilung_kommen_die_objekte_zurueck():
    neue_szene()
    a = hochpoly_wuerfel("A", 1)
    b = hochpoly_wuerfel("B", 1)
    ergebnis = teile.finde_teile([a, b])
    pruefe(len(ergebnis) == 2, f"erwartet 2, bekam {len(ergebnis)}")


def test_zwei_lose_inseln_werden_zwei_teile():
    neue_szene()
    obj = hochpoly_wuerfel("Doppel", 1)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.create_cube(bm, size=0.5, matrix=mathutils.Matrix.Translation((3, 0, 0)))
    bm.to_mesh(obj.data)
    bm.free()
    ergebnis = teile.finde_teile([obj], nach_losen_inseln=True)
    pruefe(len(ergebnis) == 2, f"erwartet 2 Teile, bekam {len(ergebnis)}")
