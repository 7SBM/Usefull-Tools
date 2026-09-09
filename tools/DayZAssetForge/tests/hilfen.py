"""Hilfen fuer die headless-Tests (Blender --background)."""
import bpy
import bmesh
import mathutils


class TestFehler(Exception):
    pass


def pruefe(bedingung, meldung):
    if not bedingung:
        raise TestFehler(meldung)


def neue_szene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def hochpoly_wuerfel(name="High", unterteilungen=4):
    """Erzeugt einen unterteilten Wuerfel als High-Poly-Testobjekt."""
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for _ in range(unterteilungen):
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
    bm.to_mesh(mesh)
    bm.free()
    return obj


def zaehle_dreiecke(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)
