"""Backt die NOHQ-Normalmap vom High-Poly auf das Low-Poly-Teil (Cycles)."""
import bpy


def _material_mit_bildnode(low, bild):
    """Sichert ein Material mit aktiver Bild-Node als Bake-Ziel auf dem Low-Objekt."""
    if not low.data.materials:
        mat = bpy.data.materials.new(low.name + "_mat")
        mat.use_nodes = True
        low.data.materials.append(mat)
    mat = low.data.materials[0]
    mat.use_nodes = True
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = bild
    mat.node_tree.nodes.active = node
    node.select = True
    return node


def backe_nohq(high, low, aufloesung=2048, cage_extrusion=0.1, gruen_invertieren=True):
    """Backt eine Tangent-Space-Normalmap vom High auf das Low (selected_to_active).

    gruen_invertieren=True dreht den Gruen-Kanal auf DirectX-Konvention.
    Rueckgabe: das erzeugte bpy.types.Image.
    """
    szene = bpy.context.scene
    vorherige_engine = szene.render.engine
    szene.render.engine = "CYCLES"

    bild = bpy.data.images.new(low.name + "_no", width=aufloesung, height=aufloesung)
    _material_mit_bildnode(low, bild)

    szene.render.bake.use_selected_to_active = True
    szene.render.bake.cage_extrusion = cage_extrusion

    with bpy.context.temp_override(
        object=low,
        active_object=low,
        selected_objects=[high, low],
        selected_editable_objects=[high, low],
    ):
        bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT")

    if gruen_invertieren:
        px = list(bild.pixels)
        for i in range(1, len(px), 4):  # Gruen-Kanal
            px[i] = 1.0 - px[i]
        bild.pixels = px

    szene.render.engine = vorherige_engine
    return bild
