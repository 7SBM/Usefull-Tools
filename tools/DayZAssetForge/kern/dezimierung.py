"""Erzeugt aus einem High-Poly-Teil ein dezimiertes Low-Poly-Teil (Stueck fuer Stueck)."""
import bpy


def dezimiere_teil(obj, ziel_dreiecke=None, verhaeltnis=None, name_suffix="_low"):
    """Dupliziert obj und dezimiert die Kopie per Decimate-Modifier (COLLAPSE).

    Genau eines von ziel_dreiecke / verhaeltnis angeben. ziel_dreiecke wird anhand der
    aktuellen Dreieckszahl in ein Verhaeltnis umgerechnet. Rueckgabe: das Low-Objekt.
    """
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

    with bpy.context.temp_override(
        object=low,
        active_object=low,
        selected_objects=[low],
        selected_editable_objects=[low],
    ):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return low
