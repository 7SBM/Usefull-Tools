import numpy as np
import shapefile
from shapely.geometry import shape
from scipy.ndimage import distance_transform_edt

# ─── EINSTELLUNGEN ───────────────────────────────────────────────
INPUT_ASC    = r"P:\brienz\source\gtt_export_20k\gtt_heightmap_Rivers_1_2_Offset.asc"   # bereits -560m geshiftete Datei +15 m offset fuer den Brienzer see, und die Are 
OUTPUT_ASC   = r"P:\brienz\source\gtt_export_20k\gtt_heightmap_Rivers_1_3_Offset.asc"
SHAPEFILE    = r"P:\brienz\source\shapefiles\Rivers_3.shp"
LAKE_OFFSET  = -22.0   # zusätzliche Absenkung in Meter (z.B. -20 bis -30)
# ─────────────────────────────────────────────────────────────────

FEATHER_M    = 24.0   # Übergangszone AUSSERHALB des Sees in Metern

def read_asc_header(filepath):
    header = {}
    header_lines = []
    with open(filepath, "r") as f:
        for line in f:
            key = line.strip().lower().split()[0]
            if key in ("ncols","nrows","xllcorner","xllcenter","yllcorner","yllcenter","cellsize","nodata_value"):
                header[key] = float(line.strip().split()[1])
                header_lines.append(line)
            else:
                break
    return header, header_lines, len(header_lines)

def main():
    print("[1/6] Lese ASC Header...")
    header, header_lines, skip = read_asc_header(INPUT_ASC)
    ncols    = int(header["ncols"])
    nrows    = int(header["nrows"])
    xll      = header.get("xllcorner", header.get("xllcenter"))
    yll      = header.get("yllcorner", header.get("yllcenter"))
    cellsize = header["cellsize"]
    nodata   = header.get("nodata_value", -9999)
    print(f"    Grid: {ncols} x {nrows}, Cellsize: {cellsize}m")

    print("[2/6] Lese Shapefile...")
    sf      = shapefile.Reader(SHAPEFILE)
    polygon = shape(sf.shape(0).__geo_interface__)
    minx, miny, maxx, maxy = polygon.bounds

    print("[3/6] Lese Heightmap...")
    data = np.loadtxt(INPUT_ASC, skiprows=skip, dtype=np.float32)

    print("[4/6] Erstelle Maske + Feather-Zone (aussen)...")
    pad = int(FEATHER_M / cellsize) + 2
    col_min = max(0,     int((minx - xll) / cellsize) - pad)
    col_max = min(ncols, int((maxx - xll) / cellsize) + pad)
    row_min = max(0,     int((yll + nrows * cellsize - maxy) / cellsize) - pad)
    row_max = min(nrows, int((yll + nrows * cellsize - miny) / cellsize) + pad)

    sub_x = xll + (np.arange(col_min, col_max) + 0.5) * cellsize
    sub_y = yll + (nrows - np.arange(row_min, row_max) - 0.5) * cellsize
    xx, yy = np.meshgrid(sub_x, sub_y)

    try:
        import shapely as shp
        inside_flat = shp.contains_xy(polygon, xx.ravel(), yy.ravel())
    except AttributeError:
        from shapely.vectorized import contains
        inside_flat = contains(polygon, xx.ravel(), yy.ravel())

    inside_2d = inside_flat.reshape(row_max - row_min, col_max - col_min)

    # Distanz der AUSSEN-Zellen zum Polygon-Rand (in Zellen)
    dist_outside = distance_transform_edt(~inside_2d) * cellsize  # Meter

    # Gewicht:
    # - Innerhalb See:          1.0 (voller Offset)
    # - Ausserhalb < FEATHER_M: sanfter Uebergang von 1 → 0
    # - Ausserhalb > FEATHER_M: 0.0 (kein Offset)
    feather_weight = np.where(
        inside_2d,
        1.0,
        np.clip(1.0 - dist_outside / FEATHER_M, 0.0, 1.0)
    )
    # Cosinus-Kurve fuer ausserhalb
    outside_zone = ~inside_2d & (dist_outside < FEATHER_M)
    feather_weight[outside_zone] = (1.0 + np.cos(
        dist_outside[outside_zone] / FEATHER_M * np.pi
    )) / 2.0

    print(f"    See innen:    {int(np.sum(inside_2d)):,} Zellen (voller Offset)")
    print(f"    Ufer-Feather: {int(np.sum(outside_zone)):,} Zellen ({FEATHER_M}m Zone)")

    print("[5/6] Wende Offset an...")
    sub_data    = data[row_min:row_max, col_min:col_max].copy()
    nodata_mask = sub_data == nodata

    data[row_min:row_max, col_min:col_max] = np.where(
        ~nodata_mask,
        sub_data + feather_weight * LAKE_OFFSET,
        sub_data
    )

    print("[6/6] Schreibe Output ASC...")
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

    print(f"\n Fertig! Gespeichert: {OUTPUT_ASC}")
    print(f"  See: {LAKE_OFFSET:+.1f}m  |  Ufer-Übergang: {FEATHER_M}m")

main()