"""Tests fuer kern/backen."""
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
