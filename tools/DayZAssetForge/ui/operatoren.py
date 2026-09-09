"""Operator, der die gesamte AssetForge-Pipeline auf der Auswahl ausfuehrt."""
import bpy
from bpy.props import StringProperty, IntProperty, FloatProperty, BoolProperty

from ..kern import teile, dezimierung, uv, backen, export


class ASSETFORGE_OT_optimieren(bpy.types.Operator):
    """High-Poly-Auswahl teilweise dezimieren und die NOHQ vom High auf das Low backen."""

    bl_idname = "assetforge.optimieren"
    bl_label = "High -> Low mit NOHQ backen"
    bl_options = {"REGISTER", "UNDO"}

    zielordner: StringProperty(name="Zielordner", subtype="DIR_PATH")
    basisname: StringProperty(name="Basisname", default="Asset")
    verhaeltnis: FloatProperty(name="Dezimier-Verhaeltnis", default=0.25, min=0.01, max=1.0)
    aufloesung: IntProperty(name="NOHQ-Aufloesung", default=2048, min=64, max=8192)
    nach_losen_inseln: BoolProperty(name="Nach losen Inseln teilen", default=False)
    gruen_invertieren: BoolProperty(name="Gruen-Kanal invertieren (DirectX)", default=True)

    def execute(self, context):
        auswahl = [o for o in context.selected_objects if o.type == "MESH"]
        if not auswahl:
            self.report({"ERROR"}, "Kein Mesh ausgewaehlt.")
            return {"CANCELLED"}
        if not self.zielordner:
            self.report({"ERROR"}, "Kein Zielordner gesetzt.")
            return {"CANCELLED"}

        ordner = bpy.path.abspath(self.zielordner)
        teile_liste = teile.finde_teile(auswahl, nach_losen_inseln=self.nach_losen_inseln)
        if not teile_liste:
            self.report({"ERROR"}, "Keine verwertbaren Teile gefunden.")
            return {"CANCELLED"}

        mehrteilig = len(teile_liste) > 1
        letzte = None
        for teil in teile_liste:
            low = dezimierung.dezimiere_teil(teil, verhaeltnis=self.verhaeltnis)
            uv.sichere_uv(low)
            bild = backen.backe_nohq(teil, low, aufloesung=self.aufloesung,
                                     gruen_invertieren=self.gruen_invertieren)
            name = f"{self.basisname}_{teil.name}" if mehrteilig else self.basisname
            letzte = export.exportiere([low], bild, ordner, name)

        self.report({"INFO"}, f"{len(teile_liste)} Teil(e) verarbeitet. Zuletzt: {letzte['mesh']}")
        return {"FINISHED"}


def register():
    bpy.utils.register_class(ASSETFORGE_OT_optimieren)


def unregister():
    bpy.utils.unregister_class(ASSETFORGE_OT_optimieren)
