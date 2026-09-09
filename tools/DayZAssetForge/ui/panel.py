"""N-Panel 'DayZ AssetForge' im 3D-Viewport (Kategorie AssetForge)."""
import bpy


class ASSETFORGE_PT_panel(bpy.types.Panel):
    bl_label = "DayZ AssetForge"
    bl_idname = "ASSETFORGE_PT_panel"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "AssetForge"

    def draw(self, context):
        layout = self.layout
        layout.label(text="High-Poly auswaehlen, dann:")
        layout.operator("assetforge.optimieren", icon="MOD_DECIM")
        layout.separator()
        layout.label(text="Ergebnis: Low-Poly + _no.png")
        layout.label(text="-> rvmat_paa_generator.py (Item-Generator)")


def register():
    bpy.utils.register_class(ASSETFORGE_PT_panel)


def unregister():
    bpy.utils.unregister_class(ASSETFORGE_PT_panel)
