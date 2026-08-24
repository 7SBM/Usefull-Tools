import struct, math
import numpy as np
from scipy.ndimage import gaussian_filter1d

SHP_PATH    = r"P:\DayZ_Misc\tracks\shapes\Rail_Grindelwald_interlaken.shp"
ASC_PATH    = r"P:\DayZ_Misc\tracks\highfield\heightmap_Train optimized.asc"
OUTPUT_PATH = r"P:\DayZ_Misc\tracks\rail_placement_output.txt"

# ── Stuecktypen ───────────────────────────────────────────────
# Flaches Kiesbett (niedriges Profil)
C_FLAT          = "rail_track_25"
C_FLAT_L        = "rail_track_l25_10"
C_FLAT_R        = "rail_track_r25_10"

# Hohes Kiesbett / Tracke (kann tiefer liegen ohne haesslich auszusehen)
C_TRACKE        = "rail_tracke_25"
C_TRACKE_L      = "rail_tracke_l25_10"
C_TRACKE_R      = "rail_tracke_r25_10"

# Bruecke (kann beliebig tief eingegraben werden, bis 10° neigbar)
C_BRIDGE        = "rail_bridge_15"
C_BRIDGE_CURVE  = "rail_bridge_15_curve"   # gebogene Bruecke

STEP_LONG   = 24.6   # Schritt fuer 25m Stuecke (0.4m Ueberlappung)
STEP_BRIDGE = 14.6   # Schritt fuer 15m Brueckenteile (0.4m Ueberlappung)
PIECE_LONG  = 25.0
PIECE_BRIDGE = 15.0

# Slope-Schwellen fuer Stucktypauswahl
SLOPE_TRACKE  = 0.03   # ab 3% -> hohes Kiesbett (tracke)
SLOPE_BRIDGE  = 0.08   # ab 8% -> Bruecke

PITCH_MAX     = 10.0   # Max Pitch-Winkel in Grad
CURVE_TRIGGER = 7.0    # Akkumulierte Grad ab wann Kurventueck
CURVE_ANGLE   = 10.0   # Grad pro Kurventueck
SMOOTH_SIGMA  = 2.5    # Hoehenprofil-Glaettung (62.5m)

ASC_XLLCORNER = 200000.0
ASC_CELLSIZE  = 10.0
ASC_NROWS     = 2048

# ── Shapefile ─────────────────────────────────────────────────
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
dx_s  = np.diff(pts_x); dy_s = np.diff(pts_y)
cum_dist = np.concatenate([[0.0], np.cumsum(np.sqrt(dx_s**2 + dy_s**2))])
total_len = cum_dist[-1]
print(f"  {num_points} Punkte, {total_len:.0f} m")

def pos_at(d):
    d = float(np.clip(d, 0, total_len))
    idx = min(int(np.searchsorted(cum_dist, d, side="right")) - 1, len(pts_x)-2)
    span = cum_dist[idx+1] - cum_dist[idx]
    t = (d - cum_dist[idx]) / span if span > 1e-9 else 0.0
    return (float(pts_x[idx] + t*(pts_x[idx+1]-pts_x[idx])),
            float(pts_y[idx] + t*(pts_y[idx+1]-pts_y[idx])))

def heading_at(d):
    ax, ay = pos_at(d)
    bx, by = pos_at(d + 5.0)
    return math.degrees(math.atan2(bx-ax, by-ay)) % 360

# ── Heightmap ─────────────────────────────────────────────────
print("Lade Heightmap...")
hmap = np.loadtxt(ASC_PATH, skiprows=6)
print(f"  {hmap.shape} geladen.")

def get_height(x, y):
    col = int((x - ASC_XLLCORNER) / ASC_CELLSIZE)
    row = ASC_NROWS - 1 - int(y / ASC_CELLSIZE)
    col = max(0, min(col, hmap.shape[1]-1))
    row = max(0, min(row, hmap.shape[0]-1))
    v = float(hmap[row, col])
    return v if v > -9000 else 0.0

def slope_at(d, piece_len):
    ax, ay = pos_at(d)
    bx, by = pos_at(d + piece_len)
    ha = get_height(ax, ay)
    hb = get_height(bx, by)
    return (hb - ha) / piece_len

# ── Hauptschleife: variabel Schrittweite ──────────────────────
print("Berechne Platzierungen...")
placements = []
accum_turn    = 0.0
d = 0.0

while d + PIECE_BRIDGE * 0.5 < total_len:
    ax, ay = pos_at(d)
    hdg    = heading_at(d)

    # Steigung bestimmen (erstmal mit langer Laenge schaetzen)
    sl_long   = slope_at(d, PIECE_LONG)
    sl_bridge = slope_at(d, PIECE_BRIDGE)
    abs_slope = abs(sl_long)

    # Stuecklaenge und Kategorie festlegen
    if abs_slope >= SLOPE_BRIDGE:
        category  = "bridge"
        step      = STEP_BRIDGE
        piece_len = PIECE_BRIDGE
        slope_val = sl_bridge
    elif abs_slope >= SLOPE_TRACKE:
        category  = "tracke"
        step      = STEP_LONG
        piece_len = PIECE_LONG
        slope_val = sl_long
    else:
        category  = "flat"
        step      = STEP_LONG
        piece_len = PIECE_LONG
        slope_val = sl_long

    # Pitch = Align to terrain, geclipt auf PITCH_MAX
    pitch_deg = max(-PITCH_MAX, min(PITCH_MAX,
                    math.degrees(math.atan(slope_val))))

    # Kurven-Akkumulation: Heading-Delta zum naechsten Punkt
    next_hdg = heading_at(d + step)
    turn = next_hdg - hdg
    if turn >  180: turn -= 360
    if turn < -180: turn += 360
    accum_turn += turn

    # Stuecktyp bestimmen
    if accum_turn >= CURVE_TRIGGER:
        if category == "bridge":
            cls = C_BRIDGE_CURVE
        elif category == "tracke":
            cls = C_TRACKE_R
        else:
            cls = C_FLAT_R
        accum_turn -= CURVE_ANGLE
        hdg = (hdg + CURVE_ANGLE) % 360
    elif accum_turn <= -CURVE_TRIGGER:
        if category == "bridge":
            cls = C_BRIDGE_CURVE  # bridge curve wird je nach Seite gedreht
        elif category == "tracke":
            cls = C_TRACKE_L
        else:
            cls = C_FLAT_L
        accum_turn += CURVE_ANGLE
        hdg = (hdg - CURVE_ANGLE) % 360
    else:
        if category == "bridge":
            cls = C_BRIDGE
        elif category == "tracke":
            cls = C_TRACKE
        else:
            cls = C_FLAT

    placements.append({
        "x":       ax,
        "y":       ay,
        "heading": hdg,
        "pitch":   pitch_deg,
        "cls":     cls,
        "cat":     category,
    })

    d += step

# ── Output ────────────────────────────────────────────────────
print(f"Schreibe {OUTPUT_PATH} ...")
lines = []
for p in placements:
    lines.append(
        f'"{p["cls"]}";{p["x"]:.6f};{p["y"]:.6f};{p["heading"]:.6f};'
        f'{p["pitch"]:.6f};0.000000;1.000000;0.000000;'
    )

with open(OUTPUT_PATH, "w") as f:
    f.write("\n".join(lines))

# Zusammenfassung
n_flat   = sum(1 for p in placements if p["cat"] == "flat")
n_tracke = sum(1 for p in placements if p["cat"] == "tracke")
n_bridge = sum(1 for p in placements if p["cat"] == "bridge")
pitches  = [p["pitch"] for p in placements]

print(f"\n=== Fertig ===")
print(f"  Gesamt:           {len(placements)}")
print(f"  Flach (track):    {n_flat}   x 25m")
print(f"  Hoch  (tracke):   {n_tracke}   x 25m")
print(f"  Bruecke:          {n_bridge}   x 15m")
print(f"  Max Pitch:        {max(pitches):.1f}°  Min: {min(pitches):.1f}°")
print(f"\nBeispiel: {lines[0]}")
print(f"\nImport: Relative to terrain")
print(f"\nParameter anpassen (oben im Script):")
print(f"  SLOPE_TRACKE = {SLOPE_TRACKE*100:.0f}%  (ab wann tracke statt flat)")
print(f"  SLOPE_BRIDGE = {SLOPE_BRIDGE*100:.0f}%  (ab wann Bruecke statt tracke)")
print(f"  PITCH_MAX    = {PITCH_MAX}°    (max Neigung)")
print(f"  SMOOTH_SIGMA = {SMOOTH_SIGMA}    (Glaettung)")
