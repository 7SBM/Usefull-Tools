"""Tests fuer kern/dezimierung."""
from hilfen import neue_szene, hochpoly_wuerfel, zaehle_dreiecke, pruefe
from kern import dezimierung


def _diag(obj):
    xs = [v[0] for v in obj.bound_box]
    ys = [v[1] for v in obj.bound_box]
    zs = [v[2] for v in obj.bound_box]
    return (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))


def test_low_hat_weniger_dreiecke_bbox_bleibt():
    neue_szene()
    high = hochpoly_wuerfel("High", unterteilungen=4)
    tris_high = zaehle_dreiecke(high)
    low = dezimierung.dezimiere_teil(high, verhaeltnis=0.25)
    tris_low = zaehle_dreiecke(low)
    pruefe(tris_low < tris_high, f"Low ({tris_low}) muss < High ({tris_high}) sein")
    dh, dl = _diag(high), _diag(low)
    for a, b in zip(dh, dl):
        pruefe(abs(a - b) < 0.1, f"Bounding-Box weicht zu stark ab: {dh} vs {dl}")


def test_ziel_dreiecke_greift():
    neue_szene()
    high = hochpoly_wuerfel("High", unterteilungen=4)
    low = dezimierung.dezimiere_teil(high, ziel_dreiecke=50)
    pruefe(zaehle_dreiecke(low) < zaehle_dreiecke(high), "Zieldreieckszahl hat nicht dezimiert")
