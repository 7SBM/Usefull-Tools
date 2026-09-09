"""Test-Runner fuer AssetForge, ausgefuehrt in Blender:

    blender --background --python tools/DayZAssetForge/tests/run_tests.py

Sammelt alle test_*-Funktionen aus den Testmodulen, fuehrt sie aus und beendet
Blender mit Exit-Code 1, sobald ein Test fehlschlaegt.
"""
import sys
import os
import importlib
import traceback

_HIER = os.path.dirname(os.path.abspath(__file__))
_WURZEL = os.path.dirname(_HIER)  # Addon-Wurzel: macht "kern" importierbar
for _p in (_WURZEL, _HIER):
    if _p not in sys.path:
        sys.path.insert(0, _p)

TESTMODULE = ["test_teile", "test_uv", "test_dezimierung", "test_backen", "test_export", "test_laden"]


def main():
    fehler = 0
    gesamt = 0
    for modname in TESTMODULE:
        try:
            mod = importlib.import_module(modname)
        except ModuleNotFoundError:
            continue  # Modul in dieser Ausbaustufe noch nicht angelegt
        for name in sorted(dir(mod)):
            if name.startswith("test_"):
                gesamt += 1
                try:
                    getattr(mod, name)()
                    print(f"  OK   {modname}.{name}")
                except Exception as e:  # noqa: BLE001 - Testrahmen faengt bewusst alles
                    fehler += 1
                    print(f"  FEHL {modname}.{name}: {e}")
                    traceback.print_exc()
    print(f"\n{gesamt - fehler}/{gesamt} Tests gruen")
    sys.exit(1 if fehler else 0)


if __name__ == "__main__":
    main()
