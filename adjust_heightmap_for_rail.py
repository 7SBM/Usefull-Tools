"""
adjust_heightmap_for_rail.py
============================
Passt die Heightmap entlang der Bahnstrecke an.
Berechnet ein grade-limitiertes Hoehenprofil und setzt
die ASC-Zellen im Korridor auf dieses Profil.

ALLE PARAMETER SIND OBEN EINSTELLBAR.
"""

import struct, math
import numpy as np
from scipy.ndimage import gaussian_filter1d
from scipy.spatial import KDTree

# ═══════════════════════════════════════════════════════════════
#  PARAMETER  — alles hier anpassen
# ═══════════════════════════════════════════════════════════════

SHP_PATH    = r"P:\DayZ_Misc\tracks\shapes\Rail_Grindelwald_interlaken.shp"
ASC_IN      = r"P:\DayZ_Misc\tracks\highfield\heightmap_Train optimized.asc"
ASC_OUT     = r"P:\DayZ_Misc\tracks\highfield\heightmap_rail_adjusted.asc"

# Korridor direkt unter den Schienen (wird exakt auf Profil gesetzt)
CORRIDOR_M  = 3.0   # Meter je Seite vom Gleismittelpunkt (total 6m)

# Uebergangszone ausserhalb (weiches Einblenden ins natuerliche Terrain)
TRANSITION_M = 8.0  # Meter zusaetzlich je Seite (total Einfluss: 22m)

# Profilglaettung (Gauss, in Stueck-Einheiten, 1 Stueck = 25m)
SMOOTH_SIGMA = 5.0  # 5 x 25m = 125m Glaettungsradius

# Abstand der Stuetzpunkte auf der Mittellinie (fuer Distanzberechnung)
RESAMPLE_M  = 5.0   # je 5m ein Punkt auf der Centerline

# Exakte Hoehen-Deltas aus MLOD (bitte nicht aendern)
DELTA_UP    = +1.343  # rail_track_up_25
DELTA_DOWN  = -1.165  # rail_track_down_25
PIECE_LEN   =  25.0
CURVE_THRESH =  2.0   # Grad ab wann Kurve

ASC_XLLCORNER = 200000.0
ASC_YLLCORNER = 0.0
ASC_CELLSIZE  = 10.0
ASC_NROWS     = 2048
ASC_NCOLS     = 2048
ASC_NODATA    = -9999.0

# ═══════════════════════════════════════════════════════════════
#  1. Shapefile lesen
# ═══════════════════════════════════════════════════════════════
print("Lese Shapefile...")
with open(SHP_PATH, "rb") as f:
    f.seek(108)
    f.read(4); f.read(32)
    num_parts,  = struct.unpack("<i", f.read(4))
    num_points, = struct.unpack("<i", f.read(4))
    f.read(num_parts * 4)
    raw = np.frombuffer(f.read(num_points * 16), dtype="<f8").reshape(-1, 2)

pts_x = raw[:, 0]
pts_y = raw[:, 1]

dx_s = np.diff(pts_x); dy_s = np.diff(pts_y)
seg_len  = np.sqrt(dx_s**2 + dy_s**2)
cum_dist = np.concatenate([[0.0], np.cumsum(seg_len)])
total_len = cum_dist[-1]
print(f"  {num_points} Punkte, {total_len:.0f} m")

def pos_at(d):
    d = float(np.clip(d, 0, total_len))
    idx = int(np.searchsorted(cum_dist, d, side="right")) - 1
    idx = min(idx, len(pts_x) - 2)
    span = cum_dist[idx+1] - cum_dist[idx]
    t = (d - cum_dist[idx]) / span if span > 1e-9 else 0.0
    return (float(pts_x[idx] + t*(pts_x[idx+1]-pts_x[idx])),
            float(pts_y[idx] + t*(pts_y[idx+1]-pts_y[idx])))

# ═══════════════════════════════════════════════════════════════
#  2. Heightmap laden
# ═══════════════════════════════════════════════════════════════
print("Lade Heightmap...")
hmap = np.loadtxt(ASC_IN, skiprows=6)
print(f"  {hmap.shape} Zellen geladen.")

def xy_to_rowcol(x, y):
    col = (x - ASC_XLLCORNER) / ASC_CELLSIZE
    row = ASC_NROWS - 1 - y / ASC_CELLSIZE
    return row, col

def get_height_xy(x, y):
    row, col = xy_to_rowcol(x, y)
    r = int(max(0, min(ASC_NROWS-1, round(row))))
    c = int(max(0, min(ASC_NCOLS-1, round(col))))
    v = float(hmap[r, c])
    return v if v > -9000 else 0.0

# ═══════════════════════════════════════════════════════════════
#  3. Gleispositionen (25m Schritte) + Chain-Hoehenprofil
# ═══════════════════════════════════════════════════════════════
print("Berechne Hoehenprofil...")
positions = []
d = 0.0
while d + PIECE_LEN * 0.5 < total_len:
    ax, ay = pos_at(d)
    bx, by = pos_at(d + PIECE_LEN)
    positions.append((ax, ay, bx, by, d))
    d += PIECE_LEN
n = len(positions)

raw_h    = np.array([get_height_xy(ax, ay) for ax,ay,bx,by,_ in positions])
target_h = gaussian_filter1d(raw_h, sigma=SMOOTH_SIGMA)

# Headings + Kurven-Deltas
headings = [math.degrees(math.atan2(bx-ax, by-ay)) % 360
            for ax,ay,bx,by,_ in positions]
adelta = [0.0]
for i in range(1, n):
    d = headings[i] - headings[i-1]
    if d >  180: d -= 360
    if d < -180: d += 360
    adelta.append(d)

# Grade-limitiertes Chain-Profil
chain_h = float(target_h[0])
chain_heights = []
for i in range(n):
    chain_heights.append(chain_h)
    diff = float(target_h[i]) - chain_h
    if abs(adelta[i]) >= CURVE_THRESH:
        delta = 0.0
    else:
        options = [("G", 0.0), ("U", DELTA_UP), ("D", DELTA_DOWN)]
        valid   = [(t, dv) for t, dv in options
                   if not (t=="U" and diff < 0) and not (t=="D" and diff > 0)]
        if not valid: valid = options
        _, delta = min(valid, key=lambda x: abs(diff - x[1]))
    chain_h += delta

chain_heights = np.array(chain_heights)

# ═══════════════════════════════════════════════════════════════
#  4. Centerline dicht abtasten fuer Distanzberechnung (KDTree)
# ═══════════════════════════════════════════════════════════════
print("Erstelle Centerline KDTree...")
sample_ds = np.arange(0, total_len, RESAMPLE_M)
center_pts = np.array([pos_at(d) for d in sample_ds])   # (M, 2)

# Fuer jeden Centerline-Punkt: chain_h interpolieren
piece_idx   = np.searchsorted(
    np.array([p[4] for p in positions]),
    sample_ds, side="right") - 1
piece_idx   = np.clip(piece_idx, 0, n-1)
center_h    = chain_heights[piece_idx]

tree = KDTree(center_pts)
print(f"  {len(center_pts)} Centerline-Punkte im KDTree.")

# ═══════════════════════════════════════════════════════════════
#  5. Bounding Box der betroffenen Zellen
# ═══════════════════════════════════════════════════════════════
influence = CORRIDOR_M + TRANSITION_M + ASC_CELLSIZE

x_min = center_pts[:, 0].min() - influence
x_max = center_pts[:, 0].max() + influence
y_min = center_pts[:, 1].min() - influence
y_max = center_pts[:, 1].max() + influence

col_min = int(max(0,        math.floor((x_min - ASC_XLLCORNER) / ASC_CELLSIZE)))
col_max = int(min(ASC_NCOLS-1, math.ceil((x_max - ASC_XLLCORNER) / ASC_CELLSIZE)))
row_min = int(max(0,        math.floor(ASC_NROWS - 1 - y_max / ASC_CELLSIZE)))
row_max = int(min(ASC_NROWS-1, math.ceil(ASC_NROWS - 1 - y_min / ASC_CELLSIZE)))

print(f"  Betroffene Zellen: Zeilen {row_min}-{row_max}, Spalten {col_min}-{col_max}")

# ═══════════════════════════════════════════════════════════════
#  6. Heightmap-Zellen anpassen
# ═══════════════════════════════════════════════════════════════
print("Passe Heightmap an...")
hmap_out = hmap.copy()

# Alle Zellen im Bbox als (x, y) Array
rows = np.arange(row_min, row_max + 1)
cols = np.arange(col_min, col_max + 1)
col_grid, row_grid = np.meshgrid(cols, rows)   # (H, W)

cell_x = ASC_XLLCORNER + col_grid * ASC_CELLSIZE
cell_y = (ASC_NROWS - 1 - row_grid) * ASC_CELLSIZE

pts_flat = np.column_stack([cell_x.ravel(), cell_y.ravel()])

# Naechster Centerline-Punkt + Distanz
dists, idxs = tree.query(pts_flat)
dists = dists.reshape(row_grid.shape)
idxs  = idxs.reshape(row_grid.shape)

target_cell = center_h[idxs]   # Ziel-Hoehe an naechstem Centerline-Punkt
orig_cell   = hmap[row_grid, col_grid].copy()

# Blend-Faktor: 1.0 im Korridor, 0.0 ausserhalb der Transition
blend = np.clip(1.0 - (dists - CORRIDOR_M) / TRANSITION_M, 0.0, 1.0)

# Neue Hoehe: blend * target + (1-blend) * original
new_h = blend * target_cell + (1.0 - blend) * orig_cell

# Nur wo blend > 0 anwenden, NODATA unveraendert lassen
mask = (blend > 0) & (orig_cell > -9000)
hmap_out[row_grid[mask], col_grid[mask]] = new_h[mask]

changed = int(mask.sum())
print(f"  {changed} Zellen angepasst.")

# ═══════════════════════════════════════════════════════════════
#  7. Neue ASC schreiben
# ═══════════════════════════════════════════════════════════════
print(f"Schreibe {ASC_OUT} ...")
header = (
    f"ncols {ASC_NCOLS}\n"
    f"nrows {ASC_NROWS}\n"
    f"xllcorner {ASC_XLLCORNER:.6f}\n"
    f"yllcorner {ASC_YLLCORNER:.6f}\n"
    f"cellsize {ASC_CELLSIZE:.6f}\n"
    f"NODATA_value {ASC_NODATA:.6f}\n"
)
with open(ASC_OUT, "w") as f:
    f.write(header)
    for row in range(ASC_NROWS):
        f.write(" ".join(f"{v:.6f}" for v in hmap_out[row]))
        f.write("\n")
        if row % 200 == 0:
            print(f"  Zeile {row}/{ASC_NROWS}...", end="\r")

print(f"\n\n=== Fertig ===")
print(f"  Input:    {ASC_IN}")
print(f"  Output:   {ASC_OUT}")
print(f"  Korridor: {CORRIDOR_M*2:.0f}m (exakt auf Profil)")
print(f"  Uebergang:{TRANSITION_M:.0f}m (sanfter Blend)")
print(f"  Zellen angepasst: {changed}")
print(f"\nIn TerrainBuilder: Terrain > Import Heightmap > {ASC_OUT}")
print(f"Danach Placement-Script nochmal laufen lassen -> perfekter Sitz!")
