import os
import sys
import glob

def check_deps():
    missing = []
    try:
        import ezdxf
    except ImportError:
        missing.append("ezdxf")
    try:
        import shapefile
    except ImportError:
        missing.append("pyshp")
    if missing:
        print("FEHLER: Fehlende Pakete. Bitte in CMD ausfuehren:")
        for pkg in missing:
            print(f"  pip install {pkg}")
        input("\nEnter druecken zum Beenden...")
        sys.exit(1)

check_deps()

import ezdxf
import shapefile

def dxf_to_polyline_shp(dxf_path, shp_path):
    doc = ezdxf.readfile(dxf_path)
    msp = doc.modelspace()

    w = shapefile.Writer(shp_path, shapefile.POLYLINE)
    w.field("SOURCE", "C", 80)
    w.field("LAYER", "C", 80)

    count = 0
    for entity in msp:
        points = []

        if entity.dxftype() == "LWPOLYLINE":
            points = [(p[0], p[1]) for p in entity.get_points()]
        elif entity.dxftype() == "POLYLINE":
            points = [(v.dxf.location.x, v.dxf.location.y) for v in entity.vertices]
        elif entity.dxftype() == "LINE":
            s = entity.dxf.start
            e = entity.dxf.end
            points = [(s.x, s.y), (e.x, e.y)]
        elif entity.dxftype() == "SPLINE":
            points = [(p[0], p[1]) for p in entity.control_points]

        if len(points) >= 2:
            layer = entity.dxf.layer if hasattr(entity.dxf, "layer") else ""
            w.line([points])
            w.record(os.path.basename(dxf_path), layer)
            count += 1

    w.close()
    return count

folder = os.path.dirname(os.path.abspath(__file__))
dxf_files = glob.glob(os.path.join(folder, "*.dxf"))

print("=" * 60)
print("DXF -> SHP Konverter")
print("=" * 60)

if not dxf_files:
    print("Keine .dxf Dateien im Ordner gefunden.")
else:
    print(f"{len(dxf_files)} DXF-Dateien gefunden.\n")
    ok = 0
    errors = 0
    for dxf_path in dxf_files:
        name = os.path.splitext(os.path.basename(dxf_path))[0]
        shp_path = os.path.join(folder, name + "_polyline")
        try:
            n = dxf_to_polyline_shp(dxf_path, shp_path)
            print(f"  OK  {name}.dxf  ->  {n} Polylines  ->  {name}_polyline.shp")
            ok += 1
        except Exception as e:
            print(f"  FEHLER  {name}.dxf:  {e}")
            errors += 1

    print()
    print("=" * 60)
    print(f"Fertig: {ok} erfolgreich, {errors} Fehler")
    print("=" * 60)

input("\nEnter druecken zum Beenden...")
