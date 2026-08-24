"""
River Steps - Eine Stufe pro Shape

Du baust die Treppe selbst aus mehreren einzelnen Shapes (ein Shape = eine
Stufe). Dieses Script macht pro Lauf genau EINE flache Ebene: findet den
tiefsten Punkt im Shape, setzt die komplette Flaeche darin auf
(tiefster Punkt + OFFSET), mit weichem Rand-Uebergang zum Original-Terrain.

DEPTH ist immer eine Tiefe nach unten -- Vorzeichen spielt keine Rolle,
31 und -31 machen exakt dasselbe (31m unter Terrain).
"""

import numpy as np
import shapefile
from shapely.geometry import shape
from scipy.ndimage import distance_transform_edt

# ── EINSTELLUNGEN ────────────────────────────────────────────────────────

INPUT_ASC  = r"P:\brienz\source\landbuilder\heightmap_riverOPTIMIZED3.asc"
OUTPUT_ASC = r"P:\brienz\source\landbuilder\heightmap_riverOPTIMIZED3.asc"
SHAPEFILE  = r"P:\brienz\source\Fluesse\RiverSteps3.shp"

DEPTH         = 31    # m unter dem tiefsten Punkt im Shape. Vorzeichen egal, gilt immer als "runter".
EDGE_BLEND_M  = 35    # m -- Breite des weichen Uebergangs zum Original-Terrain am Rand


# ── ASC HEADER ───────────────────────────────────────────────────────────

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


# ── MAIN ─────────────────────────────────────────────────────────────────

def main():
    print("[1] Lese Header...")
    hdr, hdr_lines, skip = read_header(INPUT_ASC)
    ncols  = int(hdr["ncols"]); nrows = int(hdr["nrows"])
    xll    = hdr.get("xllcorner", hdr.get("xllcenter"))
    yll    = hdr.get("yllcorner",  hdr.get("yllcenter"))
    cs     = hdr["cellsize"]
    nodata = hdr.get("nodata_value", -9999)
    print(f"    {ncols}x{nrows}, {cs}m/px")

    print("[2] Lese Shape...")
    sf   = shapefile.Reader(SHAPEFILE)
    poly = shape(sf.shape(0).__geo_interface__)
    minx, miny, maxx, maxy = poly.bounds

    print("[3] Lese Heightmap...")
    data = np.loadtxt(INPUT_ASC, skiprows=skip, dtype=np.float64)
    orig = data.copy()

    blend_cells = max(1, int(EDGE_BLEND_M / cs))
    pad = blend_cells + 2
    col_min = max(0,     int((minx - xll) / cs) - pad)
    col_max = min(ncols, int((maxx - xll) / cs) + pad + 1)
    row_min = max(0,     int((yll + nrows*cs - maxy) / cs) - pad)
    row_max = min(nrows, int((yll + nrows*cs - miny) / cs) + pad + 1)
    sub_h   = row_max - row_min
    sub_w   = col_max - col_min

    sub_x = xll + (np.arange(col_min, col_max) + 0.5) * cs
    sub_y = yll + (nrows - np.arange(row_min, row_max) - 0.5) * cs
    xx, yy = np.meshgrid(sub_x, sub_y)

    print("[4] Erstelle Maske...")
    try:
        import shapely as sl
        inside_flat = sl.contains_xy(poly, xx.ravel(), yy.ravel())
    except (AttributeError, ImportError):
        from shapely.vectorized import contains
        inside_flat = contains(poly, xx.ravel(), yy.ravel())
    inside = inside_flat.reshape(sub_h, sub_w)

    print("[5] Finde tiefsten Punkt im Shape...")
    orig_sub = orig[row_min:row_max, col_min:col_max]
    vals_in  = orig_sub[inside]
    vals_in  = vals_in[vals_in != nodata]
    if len(vals_in) == 0:
        raise RuntimeError("Shape liegt komplett ausserhalb der Heightmap oder nur nodata-Zellen.")
    lowest = float(np.percentile(vals_in, 2))   # 2-Perzentil statt hartem Min -- robust gegen einzelne Ausreisser-Pixel
    bed    = lowest - abs(DEPTH)
    print(f"    Tiefster Punkt (robust) = {lowest:.1f}m  ->  Stufenboden = {bed:.1f}m")

    print("[6] Grabe flache Stufe...")
    carved      = orig_sub.copy()
    carved_mask = inside.copy()
    carved[carved_mask] = bed

    print("[7] Weicher Rand-Uebergang zum Original-Terrain...")
    dist, (src_r, src_c) = distance_transform_edt(~carved_mask, return_indices=True)
    dist_m = dist * cs
    alpha  = np.clip(dist_m / EDGE_BLEND_M, 0.0, 1.0)   # 0 = direkt am Rand (voll carved), 1 = weit weg (voll original)
    extended_carved = carved[src_r, src_c]
    blended = alpha * orig_sub + (1.0 - alpha) * extended_carved
    final_sub = np.where(carved_mask, carved, blended)

    print("[8] Schreibe Output...")
    data[row_min:row_max, col_min:col_max] = final_sub

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

    print(f"\nFertig: {OUTPUT_ASC}")
    print(f"  Stufenboden={bed:.1f}m  (Tiefster Punkt={lowest:.1f}m, DEPTH={DEPTH}m)")

main()
