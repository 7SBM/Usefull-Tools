"""Lade-/Integrationstest: Addon registrieren und den Operator einmal durchlaufen."""
import os
import sys
import tempfile
import bpy
from hilfen import neue_szene, hochpoly_kugel, pruefe

# Elternordner der Addon-Wurzel auf den Pfad, damit "import DayZAssetForge" als Paket geht.
_WURZEL = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # tools/DayZAssetForge
_TOOLS = os.path.dirname(_WURZEL)                                      # tools
if _TOOLS not in sys.path:
    sys.path.insert(0, _TOOLS)


def test_addon_registriert_und_operator_laeuft():
    import DayZAssetForge as addon
    addon.register()
    try:
        pruefe(hasattr(bpy.types, "ASSETFORGE_OT_optimieren"), "Operator nicht registriert")
        pruefe(hasattr(bpy.types, "ASSETFORGE_PT_panel"), "Panel nicht registriert")

        neue_szene()
        obj = hochpoly_kugel("ProbeItem", unterteilungen=4)
        ziel = tempfile.mkdtemp()
        # Echter Kontext wie beim Klick in der UI (kein temp_override, sonst kollidieren
        # die verschachtelten Mode-Wechsel der Pipeline mit dem Override).
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        ergebnis = bpy.ops.assetforge.optimieren(
            zielordner=ziel, basisname="ProbeItem",
            verhaeltnis=0.2, aufloesung=64, gruen_invertieren=True,
        )
        pruefe(ergebnis == {"FINISHED"}, f"Operator nicht FINISHED: {ergebnis}")
        pruefe(os.path.isfile(os.path.join(ziel, "ProbeItem.obj")), "keine OBJ-Datei")
        pruefe(os.path.isfile(os.path.join(ziel, "ProbeItem_no.png")), "kein Normal-PNG")
    finally:
        addon.unregister()
