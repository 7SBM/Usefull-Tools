"""
=============================================================
  DayZ SP Import Script v2
  
  Scannt den SP Export Ordner nach CO, AS, NOHQ, SMDI Texturen,
  erstellt pro Teil ein Material, verkabelt alles richtig
  (inkl. SMDI Channel-Splitting), und weist die Materialien
  den Mesh-Teilen zu.
  
  ANLEITUNG:
    1. Modell importieren (alle Teile)
    2. SP_EXPORT_FOLDER anpassen
    3. Scripting > Play
    4. Z > Material Preview > checken
=============================================================
"""

import bpy
import os
import glob

# =============================================================
#  EINSTELLUNGEN
# =============================================================

# Ordner wo die SP-Exports liegen
SP_EXPORT_FOLDER = r"C:\Users\henry\Documents\Adobe\Adobe Substance 3D Painter\export"

# Dateinamen-Suffixe (wie SP sie benennt)
SUFFIX_CO   = "_CO"
SUFFIX_AS   = "_AS"
SUFFIX_NOHQ = "_NOHQ"
SUFFIX_SMDI = "_SMDI"

ALL_SUFFIXES = [SUFFIX_CO, SUFFIX_AS, SUFFIX_NOHQ, SUFFIX_SMDI]

# UV Map Name (wie deine UV in Blender heisst)
UV_NAME = "st"

# =============================================================
#  START
# =============================================================
print("\n" + "=" * 60)
print("  SP TEXTURE IMPORTER v2")
print("  Texturen: CO, AS, NOHQ, SMDI")
print("=" * 60)

if bpy.context.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')


# =============================================================
#  1. TEXTUREN SCANNEN
# =============================================================
print(f"\n[1] Scanne Ordner: {SP_EXPORT_FOLDER}")

# Alle PNGs im Ordner und Unterordnern finden
all_pngs = []
for root, dirs, files in os.walk(SP_EXPORT_FOLDER):
    for f in files:
        if f.lower().endswith(".png"):
            all_pngs.append(os.path.join(root, f))

print(f"    {len(all_pngs)} PNG Dateien gefunden")

# Nach Typ sortieren
textures = {}  # {part_name: {suffix: pfad}}

for filepath in all_pngs:
    basename = os.path.basename(filepath).replace(".png", "").replace(".PNG", "")
    
    # Welcher Suffix?
    found_suffix = None
    part_name = None
    for suffix in ALL_SUFFIXES:
        if basename.upper().endswith(suffix):
            found_suffix = suffix
            # Teil-Name: alles VOR dem Suffix
            part_name = basename[:-(len(suffix))]
            break
    
    if not found_suffix or not part_name:
        continue
    
    if part_name not in textures:
        textures[part_name] = {}
    textures[part_name][found_suffix] = filepath

print(f"    {len(textures)} Teile gefunden:\n")
for part, maps in sorted(textures.items()):
    map_list = [s.replace("_", "") for s in maps.keys()]
    print(f"      {part}: {', '.join(map_list)}")


# =============================================================
#  2. MESH-TEILE FINDEN UND ZUORDNEN
# =============================================================
print(f"\n[2] Mesh-Teile zuordnen")

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print(f"    {len(meshes)} Mesh-Objekte in Szene")

def find_mesh_for_part(part_name, meshes):
    """
    Findet das passende Mesh per Name-Matching.
    Laengster Match gewinnt.
    """
    best_match = None
    best_len = 0
    
    for m in meshes:
        # Exakter Match
        if part_name == m.name and len(m.name) > best_len:
            best_match = m
            best_len = len(m.name)
        
        # Teil-Name beginnt mit Mesh-Name + Trennzeichen
        for sep in ["_", ".", " ", "-"]:
            prefix = m.name + sep
            if part_name.startswith(prefix) and len(prefix) > best_len:
                best_match = m
                best_len = len(prefix)
        
        # Mesh-Name beginnt mit Teil-Name
        for sep in ["_", ".", " ", "-"]:
            prefix = part_name + sep
            if m.name.startswith(prefix) and len(prefix) > best_len:
                best_match = m
                best_len = len(prefix)
        
        # Mesh-Name ist in Teil-Name enthalten
        if m.name in part_name and len(m.name) > best_len:
            best_match = m
            best_len = len(m.name)
    
    return best_match

assignments = {}
unmatched_parts = []

for part in sorted(textures.keys()):
    mesh = find_mesh_for_part(part, meshes)
    if mesh:
        assignments[part] = mesh
        print(f"    {part} -> {mesh.name}")
    else:
        unmatched_parts.append(part)
        print(f"    {part} -> KEIN MESH GEFUNDEN!")

if unmatched_parts:
    print(f"\n    WARNUNG: {len(unmatched_parts)} Teile ohne Mesh!")


# =============================================================
#  3. MATERIALIEN ERSTELLEN UND VERKABELN
# =============================================================
print(f"\n[3] Materialien erstellen und verkabeln")

created_count = 0

for part, maps in sorted(textures.items()):
    mat_name = f"SP_{part}"
    
    # Altes Material loeschen
    if mat_name in bpy.data.materials:
        bpy.data.materials.remove(bpy.data.materials[mat_name])
    
    mat = bpy.data.materials.new(name=mat_name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    
    for n in list(nodes):
        nodes.remove(n)
    
    # Output + Principled BSDF
    output = nodes.new('ShaderNodeOutputMaterial')
    output.location = (600, 300)
    
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.location = (200, 300)
    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    
    x_tex = -400
    x_uv = x_tex - 250
    y = 600
    
    # --- CO (Base Color) ---
    if SUFFIX_CO in maps:
        uv = nodes.new('ShaderNodeUVMap')
        uv.uv_map = UV_NAME
        uv.location = (x_uv, y)
        uv.label = "UV (CO)"
        
        tex = nodes.new('ShaderNodeTexImage')
        tex.location = (x_tex, y)
        tex.label = "Base Color (_CO)"
        tex.image = bpy.data.images.load(maps[SUFFIX_CO])
        tex.image.colorspace_settings.name = 'sRGB'
        links.new(uv.outputs['UV'], tex.inputs['Vector'])
        links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
        y -= 300
    
    # --- NOHQ (Normal Map) ---
    if SUFFIX_NOHQ in maps:
        uv = nodes.new('ShaderNodeUVMap')
        uv.uv_map = UV_NAME
        uv.location = (x_uv, y)
        uv.label = "UV (NOHQ)"
        
        tex = nodes.new('ShaderNodeTexImage')
        tex.location = (x_tex, y)
        tex.label = "Normal Map (_NOHQ)"
        tex.image = bpy.data.images.load(maps[SUFFIX_NOHQ])
        tex.image.colorspace_settings.name = 'Non-Color'
        links.new(uv.outputs['UV'], tex.inputs['Vector'])
        
        nmap = nodes.new('ShaderNodeNormalMap')
        nmap.location = (x_tex + 300, y)
        links.new(tex.outputs['Color'], nmap.inputs['Color'])
        links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
        y -= 300
    
    # --- SMDI (R=1, G=Metallic, B=Glossiness, A=AO) ---
    if SUFFIX_SMDI in maps:
        uv = nodes.new('ShaderNodeUVMap')
        uv.uv_map = UV_NAME
        uv.location = (x_uv, y)
        uv.label = "UV (SMDI)"
        
        tex = nodes.new('ShaderNodeTexImage')
        tex.location = (x_tex, y)
        tex.label = "SMDI (_SMDI)"
        tex.image = bpy.data.images.load(maps[SUFFIX_SMDI])
        tex.image.colorspace_settings.name = 'Non-Color'
        links.new(uv.outputs['UV'], tex.inputs['Vector'])
        
        # Separate Color: Channels aufsplitten
        sep = nodes.new('ShaderNodeSeparateColor')
        sep.location = (x_tex + 300, y)
        sep.label = "SMDI Channels"
        links.new(tex.outputs['Color'], sep.inputs['Color'])
        
        # G = Metallic -> direkt auf Metallic
        links.new(sep.outputs['Green'], bsdf.inputs['Metallic'])
        
        # B = Glossiness -> INVERTIEREN -> Roughness
        invert = nodes.new('ShaderNodeInvert')
        invert.location = (x_tex + 300, y - 120)
        invert.label = "Gloss -> Rough"
        links.new(sep.outputs['Blue'], invert.inputs['Color'])
        links.new(invert.outputs['Color'], bsdf.inputs['Roughness'])
        
        y -= 400
    
    # --- AS (Ambient Shadow) ---
    if SUFFIX_AS in maps:
        uv = nodes.new('ShaderNodeUVMap')
        uv.uv_map = UV_NAME
        uv.location = (x_uv, y)
        uv.label = "UV (AS)"
        
        tex = nodes.new('ShaderNodeTexImage')
        tex.location = (x_tex, y)
        tex.label = "Ambient Shadow (_AS)"
        tex.image = bpy.data.images.load(maps[SUFFIX_AS])
        tex.image.colorspace_settings.name = 'Non-Color'
        links.new(uv.outputs['UV'], tex.inputs['Vector'])
        # AS wird nicht direkt am BSDF angeschlossen
        # aber ist im Material fuer spaeteres Baking verfuegbar
        y -= 300
    
    # Material dem Mesh zuweisen
    if part in assignments:
        mesh_obj = assignments[part]
        # Nicht alle Materialien loeschen, sondern ersetzen/hinzufuegen
        if len(mesh_obj.data.materials) == 0:
            mesh_obj.data.materials.append(mat)
        else:
            mesh_obj.data.materials[0] = mat
        print(f"    {mat_name} -> {mesh_obj.name}")
    else:
        print(f"    {mat_name} erstellt (kein Mesh)")
    
    created_count += 1


# =============================================================
#  4. ZUSAMMENFASSUNG
# =============================================================
print(f"\n{'=' * 60}")
print(f"  FERTIG!")
print(f"{'=' * 60}")
print(f"\n  {created_count} Materialien erstellt")
print(f"  {len(assignments)} an Meshes zugewiesen")
print(f"  {len(unmatched_parts)} ohne Mesh-Zuordnung")

print(f"\n  Shader Verkabelung pro Material:")
print(f"    CO   -> Base Color")
print(f"    NOHQ -> Normal Map Node -> Normal")
print(f"    SMDI -> Separate Color:")
print(f"             G -> Metallic")
print(f"             B -> Invert -> Roughness")
print(f"    AS   -> Geladen (nicht verbunden)")

print(f"\n  -> Z-Taste > Material Preview zum Checken!")
print(f"  -> Falls Zuordnung falsch: Mesh-Namen oder")
print(f"     SP Texture Set Namen anpassen")
print(f"{'=' * 60}\n")
