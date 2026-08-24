"""
Lake Mask Offset Script v2 (kein geopandas nötig)
===================================================
Benötigt: numpy, shapely, pyshp
Blender Install: subprocess.run([sys.executable, "-m", "pip", "install", "pyshp", "shapely", "numpy"])
"""

import numpy as np
import shapefile                     # pyshp
from shapely.geometry import shape   # shapely

# ─── EINSTELLUNGEN ───────────────────────────────────────────────
# bereits -560m geshiftete Datei +15 m offset fuer den Brienzer see, und die Are 
INPUT_ASC  = r"P:\brienz\source\landbuilder\PLATTEN83M - Kopie.asc"
OUTPUT_ASC = r"P:\brienz\source\landbuilder\heightmap_riverOPTIMIZED3.asc"
SHAPEFILE  = r"P:\brienz\source\Fluesse\RiverSteps3.shp"
LAKE_OFFSET  = -57.0   # zusätzliche Absenkung in Meter (z.B. -20 bis -30)
# ─────────────────────────────────────────────────────────────────

def read_asc_header(filepath):
    header = {}
    header_lines = []
    with open(filepath, "r") as f:
        for line in f:
            key = line.strip().lower().split()[0]
            if key in ("ncols","nrows","xllcorner","xllcenter",
                       "yllcorner","yllcenter","cellsize","nodata_value"):
                header[key] = float(line.strip().split()[1])
                header_lines.append(line)
            else:
                break
    return header, header_lines, len(header_lines)

def main():
    print("[1/5] Lese ASC Header...")
    header, header_lines, skip = read_asc_header(INPUT_ASC)

    ncols    = int(header["ncols"])
    nrows    = int(header["nrows"])
    xll      = header.get("xllcorner", header.get("xllcenter"))
    yll      = header.get("yllcorner", header.get("yllcenter"))
    cellsize = header["cellsize"]
    nodata   = header.get("nodata_value", -9999)

    print(f"    Grid: {ncols} x {nrows}, Ursprung: ({xll}, {yll}), Cellsize: {cellsize}m")

    print("[2/5] Lese Shapefile...")
    sf      = shapefile.Reader(SHAPEFILE)
    polygon = shape(sf.shape(0).__geo_interface__)
    minx, miny, maxx, maxy = polygon.bounds
    print(f"    See-Bounds: X({minx:.0f}–{maxx:.0f}), Y({miny:.0f}–{maxy:.0f})")

    print("[3/5] Lese Heightmap (kann kurz dauern)...")
    data = np.loadtxt(INPUT_ASC, skiprows=skip, dtype=np.float32)
    print(f"    Array Shape: {data.shape}")

    print("[4/5] Erstelle See-Maske...")
    # Nur Sub-Region im Polygon-BoundingBox berechnen
    col_min = max(0,     int((minx - xll) / cellsize) - 1)
    col_max = min(ncols, int((maxx - xll) / cellsize) + 2)
    row_min = max(0,     int((yll + nrows * cellsize - maxy) / cellsize) - 1)
    row_max = min(nrows, int((yll + nrows * cellsize - miny) / cellsize) + 2)

    print(f"    Sub-Region: Spalten {col_min}–{col_max}, Zeilen {row_min}–{row_max}")

    # Koordinaten-Meshgrid für Sub-Region
    sub_x = xll + (np.arange(col_min, col_max) + 0.5) * cellsize
    sub_y = yll + (nrows - np.arange(row_min, row_max) - 0.5) * cellsize
    xx, yy = np.meshgrid(sub_x, sub_y)

    # Point-in-Polygon (shapely 2.x vectorized)
    try:
        import shapely as shp
        inside_flat = shp.contains_xy(polygon, xx.ravel(), yy.ravel())
    except AttributeError:
        # shapely < 2.0 fallback
        from shapely.vectorized import contains
        inside_flat = contains(polygon, xx.ravel(), yy.ravel())

    inside_2d = inside_flat.reshape(row_max - row_min, col_max - col_min)

    sub_data    = data[row_min:row_max, col_min:col_max]
    nodata_mask = sub_data == nodata
    affected    = int(np.sum(inside_2d & ~nodata_mask))
    print(f"    Betroffene Zellen: {affected:,} (~{affected * cellsize**2 / 1e6:.2f} km²)")

    # Offset anwenden
    data[row_min:row_max, col_min:col_max] = np.where(
        inside_2d & ~nodata_mask,
        sub_data + LAKE_OFFSET,
        sub_data
    )

    print("[5/5] Schreibe Output ASC...")
    with open(OUTPUT_ASC, "w") as f:
        f.writelines(header_lines)
        for row in data:
            vals = []
            for v in row:
                fv = float(v)
                if fv == nodata:
                    vals.append(str(int(nodata)))
                elif abs(fv - round(fv)) < 1e-3:
                    vals.append(str(int(round(fv))))
                else:
                    vals.append(f"{fv:.4f}")
            f.write(" ".join(vals) + "\n")

    print(f"\n✓ Fertig! Gespeichert: {OUTPUT_ASC}")
    print(f"  See abgesenkt um {LAKE_OFFSET:+.1f}m")

if __name__ == "__main__":
    main()
