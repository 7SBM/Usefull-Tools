"""Zerlegt die Auswahl in einzeln zu verarbeitende Teile ("Stueck fuer Stueck")."""
import bpy


def _override(obj):
    """Kontext-Override, damit Operatoren auch in --background sauber laufen."""
    return bpy.context.temp_override(
        object=obj,
        active_object=obj,
        selected_objects=[obj],
        selected_editable_objects=[obj],
    )


def finde_teile(objekte, nach_losen_inseln=False):
    """Gibt die zu verarbeitenden Mesh-Teile zurueck.

    Ohne Aufteilung: die Mesh-Objekte selbst.
    Mit nach_losen_inseln=True: trennt lose Geometrie in eigene Objekte (separate LOOSE)
    und gibt Ausgangs- plus neue Objekte zurueck.
    """
    mesh_objekte = [o for o in objekte if o and o.type == "MESH"]
    if not nach_losen_inseln:
        return list(mesh_objekte)

    ergebnis = []
    for obj in mesh_objekte:
        vorher = set(bpy.context.scene.objects)
        with _override(obj):
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.separate(type="LOOSE")
            bpy.ops.object.mode_set(mode="OBJECT")
        neue = [o for o in bpy.context.scene.objects if o not in vorher]
        ergebnis.append(obj)
        ergebnis.extend(neue)
    return ergebnis
