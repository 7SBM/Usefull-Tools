"""Tests fuer kern/uv."""
from hilfen import neue_szene, hochpoly_wuerfel, pruefe
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
