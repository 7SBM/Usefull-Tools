"""Exportiert Low-Mesh + gebackene Normalmap und eine Object-Builder-To-Do-Liste.

Exportformat: OBJ (in Blender eingebaut, kein Zusatz-Addon noetig). Object Builder /
Blender-Toolbox importiert OBJ; die eigentliche P3D-Erzeugung bleibt bewusst dort.
"""
import os
import bpy

TODO_TEXT = """Object-Builder To-Do fuer {name}
================================
[ ] Low-Poly aus {mesh} importieren (Object Builder / Blender-Toolbox)
[ ] LODs setzen (1.0 Resolution, ViewGeometry, Geometry, Memory)
[ ] Named Selections / Proxys / Memory-Points anlegen
[ ] Material zuweisen; NOHQ = {normal_png} (per rvmat_paa_generator.py nach _nohq.paa)
[ ] config.cpp / model.cfg gegen P3D pruefen (Item-Generator, Pruefmodus)
"""


def _freier_pfad(pfad):
    """Nie ueberschreiben: existiert die Datei, wird '.neu' angehaengt."""
    return pfad if not os.path.exists(pfad) else pfad + ".neu"


def exportiere(low_objekte, bild, zielordner, basisname):
    """Schreibt <basisname>.obj, <basisname>_no.png und object_builder_todo.txt.

    Rueckgabe: dict mit den tatsaechlich geschriebenen Pfaden (mesh, normal_png, todo).
    """
    os.makedirs(zielordner, exist_ok=True)
    mesh_pfad = _freier_pfad(os.path.join(zielordner, basisname + ".obj"))
    png_pfad = _freier_pfad(os.path.join(zielordner, basisname + "_no.png"))
    todo_pfad = _freier_pfad(os.path.join(zielordner, "object_builder_todo.txt"))

    aktiv = low_objekte[0]
    with bpy.context.temp_override(
        object=aktiv,
        active_object=aktiv,
        selected_objects=list(low_objekte),
        selected_editable_objects=list(low_objekte),
    ):
        bpy.ops.wm.obj_export(filepath=mesh_pfad, export_selected_objects=True)

    bild.filepath_raw = png_pfad
    bild.file_format = "PNG"
    bild.save()

    with open(todo_pfad, "w", encoding="utf-8") as f:
        f.write(TODO_TEXT.format(
            name=basisname,
            mesh=os.path.basename(mesh_pfad),
            normal_png=os.path.basename(png_pfad),
        ))
    return {"mesh": mesh_pfad, "normal_png": png_pfad, "todo": todo_pfad}
