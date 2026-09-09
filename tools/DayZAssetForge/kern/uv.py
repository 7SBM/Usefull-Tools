"""Stellt sicher, dass ein Objekt ein UV-Layout hat (fuers Backen noetig)."""
import bpy


def sichere_uv(obj):
    """Erzeugt per Smart-UV-Project ein UV-Layout, falls keines vorhanden ist.

    Rueckgabe: True, wenn ein UV neu erzeugt wurde; False, wenn schon eines vorhanden war.
    """
    if obj.data.uv_layers:
        return False

    # Reales aktives Objekt + Auswahl setzen (Edit-Mode-Poll braucht das echte active object,
    # ein blosser temp_override genuegt in --background nicht).
    if bpy.context.view_layer.objects.active and bpy.context.view_layer.objects.active.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    # Alle Flaechen schon im Object-Mode waehlen, damit kein mesh.select_all im Edit-Mode
    # noetig ist (dessen Poll scheitert in --background zuverlaessig).
    for polygon in obj.data.polygons:
        polygon.select = True

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")

    # Sicherheitsnetz: falls Smart-UV in verschachteltem Kontext nichts erzeugt hat,
    # trotzdem eine (aktive) UV-Ebene garantieren, damit der Bake nie an fehlender UV scheitert.
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    if obj.data.uv_layers.active is None:
        obj.data.uv_layers.active = obj.data.uv_layers[0]
    return True
