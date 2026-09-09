"""Tests fuer kern/export."""
import os
import tempfile
from hilfen import neue_szene, hochpoly_kugel, pruefe
from kern import dezimierung, uv, backen, export


def test_export_schreibt_erwartete_dateien():
    neue_szene()
    high = hochpoly_kugel("Kiste", 4)
    low = dezimierung.dezimiere_teil(high, verhaeltnis=0.2)
    uv.sichere_uv(low)
    bild = backen.backe_nohq(high, low, aufloesung=64, cage_extrusion=0.2)
    ziel = tempfile.mkdtemp()
    ergebnis = export.exportiere([low], bild, ziel, "Kiste")
    pruefe(os.path.isfile(ergebnis["mesh"]), "keine Mesh-Datei")
    pruefe(os.path.isfile(ergebnis["normal_png"]), "kein Normal-PNG")
    pruefe(os.path.isfile(ergebnis["todo"]) and os.path.getsize(ergebnis["todo"]) > 0,
           "To-Do leer/fehlt")
