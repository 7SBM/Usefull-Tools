"""Tests fuer kern/backen."""
import bpy
from hilfen import neue_szene, hochpoly_kugel, pruefe
from kern import dezimierung, uv, backen


def test_nohq_bild_entsteht_und_hat_detail():
    neue_szene()
    high = hochpoly_kugel("High", unterteilungen=5)
    low = dezimierung.dezimiere_teil(high, verhaeltnis=0.1)
    uv.sichere_uv(low)
    bild = backen.backe_nohq(high, low, aufloesung=64, cage_extrusion=0.2)
    pruefe(bild is not None, "kein Bild erzeugt")
    pruefe(tuple(bild.size) == (64, 64), f"falsche Groesse: {tuple(bild.size)}")
    px = list(bild.pixels)
    verschieden = len(set(round(p, 3) for p in px[:4000]))
    pruefe(verschieden > 3, f"Normalmap wirkt leer/flach (nur {verschieden} Werte)")


def test_nohq_bild_ist_non_color():
    """Normalmaps sind Daten, keine Farben: ohne Non-Color wuerde das PNG gamma-verzerrt."""
    neue_szene()
    high = hochpoly_kugel("High", unterteilungen=4)
    low = dezimierung.dezimiere_teil(high, verhaeltnis=0.2)
    uv.sichere_uv(low)
    bild = backen.backe_nohq(high, low, aufloesung=32, cage_extrusion=0.2)
    pruefe(bild.colorspace_settings.name == "Non-Color",
           f"Farbraum muss Non-Color sein, ist {bild.colorspace_settings.name!r}")


def test_gruen_kanal_invertieren_dreht_nur_gruen():
    neue_szene()
    bild = bpy.data.images.new("Probe", width=2, height=2)
    bild.colorspace_settings.name = "Non-Color"
    bild.pixels = [0.1, 0.2, 0.3, 1.0] * 4
    backen.gruen_kanal_invertieren(bild)
    px = list(bild.pixels)
    pruefe(abs(px[0] - 0.1) < 0.01, f"Rot veraendert: {px[:4]}")
    pruefe(abs(px[1] - 0.8) < 0.01, f"Gruen nicht invertiert: {px[:4]}")
    pruefe(abs(px[2] - 0.3) < 0.01, f"Blau veraendert: {px[:4]}")
    pruefe(abs(px[3] - 1.0) < 0.01, f"Alpha veraendert: {px[:4]}")
