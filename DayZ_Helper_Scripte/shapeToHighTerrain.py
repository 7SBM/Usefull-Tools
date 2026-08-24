"""
Flatten to Height
=================
Setzt alle Terrain-Zellen innerhalb eines Shapefiles
auf eine absolute Hoehe (z.B. 127m ueber dem Meer).
"""

import numpy as np
import shapefile
from shapely.geometry import shape

# ── EINSTELLUNGEN ─────────────────────────────────────────────────────────────

# Deine Ausgangs-Heightmap
INPUT_ASC  = r"P:\brienz\source\landbuilder\heightmap_riverOPTIMIZED3.asc"
OUTPUT_ASC = r"P:\brienz\source\landbuilder\heightmap_riverOPTIMIZED3.asc"
SHAPEFILE  = r"P:\brienz\source\Rails\3erBridgeRail30m_2.shp"
# Auf welche Hoehe soll das Terrain gesetzt werden (Meter ueber dem Meer)
ZIELHOEHE    = 30
# True  = nur eingraben  (Terrain das schon tiefer liegt bleibt unveraendert)
# False = exakt auf Zielhoehe (sowohl eingraben als auch auffuellen)
NUR_EINGRABEN = False

# Sanfter Uebergang am Rand des Shapes nach aussen (in Meter)
# 0  = harte scharfe Kante (gut fuer Tunnel / kuenstliche Kanaele)
# 30 = 30 Meter weicher Uebergang ins normale Terrain
RAND_M       = 45.0

# ─────────────────────────────────────────────────────────────────────────────

def read_header(path):
    hdr, lines = {}, []
    with open(path) as f:
        for line in f:
            k = line.strip().split()[0].lower()
            if k in ("ncols","nrows","xllcorner","xllcenter",
                     "yllcorner","yllcenter","cellsize","nodata_value"):
                hdr[k] = float(line.strip().split()[1])
                lines.append(line)
            else:
                break
    return hdr, lines, len(lines)

def main():
    print("[1/5] Lese Header...")
    hdr, hdr_lines, skip = read_header(INPUT_ASC)
    ncols  = int(hdr["ncols"]); nrows = int(hdr["nrows"])
    xll    = hdr.get("xllcorner", hdr.get("xllcenter"))
    yll    = hdr.get("yllcorner",  hdr.get("yllcenter"))
    cs     = hdr["cellsize"]
    nodata = hdr.get("nodata_value", -9999)
    print(f"    {ncols}x{nrows}, {cs}m/px")

    print("[2/5] Lese Shape...")
    sf   = shapefile.Reader(SHAPEFILE)
    poly = shape(sf.shape(0).__geo_interface__)
    minx, miny, maxx, maxy = poly.bounds
    print(f"    BBox: X {minx:.0f}-{maxx:.0f}  Y {miny:.0f}-{maxy:.0f}")

    print("[3/5] Lese Heightmap...")
    data = np.loadtxt(INPUT_ASC, skiprows=skip, dtype=np.float64)

    print("[4/5] Erstelle Maske und setze Hoehe...")
    pad     = max(1, int(RAND_M / cs) + 2)
    col_min = max(0,     int((minx - xll) / cs) - pad)
    col_max = min(ncols, int((maxx - xll) / cs) + pad)
    row_min = max(0,     int((yll + nrows*cs - maxy) / cs) - pad)
    row_max = min(nrows, int((yll + nrows*cs - miny) / cs) + pad)

    sub_x = xll + (np.arange(col_min, col_max) + 0.5) * cs
    sub_y = yll + (nrows - np.arange(row_min, row_max) - 0.5) * cs
    xx, yy = np.meshgrid(sub_x, sub_y)

    try:
        import shapely as sl
        inside_flat = sl.contains_xy(poly, xx.ravel(), yy.ravel())
    except (AttributeError, ImportError):
        from shapely.vectorized import contains
        inside_flat = contains(poly, xx.ravel(), yy.ravel())
    inside = inside_flat.reshape(row_max - row_min, col_max - col_min)

    sub   = data[row_min:row_max, col_min:col_max].copy()
    valid = inside & (sub != nodata)

    if NUR_EINGRABEN:
        mask = valid & (sub > ZIELHOEHE)
    else:
        mask = valid

    affected = int(np.sum(mask))
    print(f"    Betroffene Zellen: {affected}  ({affected * cs**2 / 1e6:.4f} km2)")
    print(f"    Zielhoehe: {ZIELHOEHE}m")

    # Innen: auf Zielhoehe setzen
    result = sub.copy()
    result[mask] = ZIELHOEHE

    # Sanfter Rand
    if RAND_M > 0:
        from scipy.ndimage import distance_transform_edt
        dist_out   = distance_transform_edt(~inside) * cs
        rand_zone  = (~inside) & (dist_out <= RAND_M) & (sub != nodata)
        if np.any(rand_zone):
            t        = np.clip(dist_out[rand_zone] / RAND_M, 0.0, 1.0)
            t_smooth = (1.0 - np.cos(t * np.pi)) / 2.0
            blended  = ZIELHOEHE * (1.0 - t_smooth) + sub[rand_zone] * t_smooth
            if NUR_EINGRABEN:
                blended = np.minimum(blended, sub[rand_zone])
            result[rand_zone] = blended

    data[row_min:row_max, col_min:col_max] = result

    print("[5/5] Schreibe Output...")
    with open(OUTPUT_ASC, "w") as f:
        f.writelines(hdr_lines)
        for row in data:
            parts = []
            for v in row:
                fv = float(v)
                if fv == nodata:
                    parts.append(str(int(nodata)))
                elif abs(fv - round(fv)) < 0.001:
                    parts.append(str(int(round(fv))))
                else:
                    parts.append(f"{fv:.4f}")
            f.write(" ".join(parts) + "\n")

    print(f"\nFertig! -> {OUTPUT_ASC}")
    print(f"  {affected} Zellen auf {ZIELHOEHE}m gesetzt")

main()
