"""7SBM DayZ AssetForge — Blender-Addon zur Optimierung von High-Poly-DayZ-Assets.

Teil A: High-Poly -> Low-Poly mit gebackener NOHQ-Normalmap, Teil fuer Teil.
Der Kern (kern/) ist reine Logik auf bpy-Daten und headless testbar; ui/ ruft nur den Kern.
"""


def register():
    from .ui import operatoren, panel
    operatoren.register()
    panel.register()


def unregister():
    from .ui import operatoren, panel
    panel.unregister()
    operatoren.unregister()
