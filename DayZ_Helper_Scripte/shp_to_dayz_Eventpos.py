"""
SHP -> DayZ XML Converter
Liest Polyline-Shapefiles, konvertiert UTM -> DayZ-Koordinaten,
und traegt die Positionen in cfgeventspawns.xml oder cfgplayerspawnpoints.xml ein.
Einfach doppelklicken und Anweisungen folgen.
"""
import struct, math, os, sys, re

DEFAULT_SHP     = r"P:\brienz\source\shapefiles\EventPos_boot.shp"
DEFAULT_EVENT   = r"P:\7SBM_ce\cfgeventspawns.xml"
DEFAULT_SPAWN   = r"P:\7SBM_ce\cfgplayerspawnpoints.xml"
DEFAULT_ORIGINX = 200000.0
DEFAULT_ORIGINY = 0.0


def read_shp_polylines(shp_path):
    with open(shp_path, "rb") as f:
        data = f.read()
    offset = 100
    rec_num = 0
    lines = []
    while offset < len(data):
        rec_num += 1
        content_len = struct.unpack(">i", data[offset+4:offset+8])[0] * 2
        rec_start = offset + 8
        shp_type = struct.unpack("<i", data[rec_start:rec_start+4])[0]
        if shp_type == 3:
            num_parts = struct.unpack("<i", data[rec_start+36:rec_start+40])[0]
            num_points = struct.unpack("<i", data[rec_start+40:rec_start+44])[0]
            points_start = rec_start + 44 + num_parts * 4
            pts = []
            for i in range(num_points):
                px = struct.unpack("<d", data[points_start+i*16:points_start+i*16+8])[0]
                py = struct.unpack("<d", data[points_start+i*16+8:points_start+i*16+16])[0]
                pts.append((px, py))
            lines.append((rec_num, pts))
        offset = rec_start + content_len
    return lines


def heading(x1, z1, x2, z2):
    dx = x2 - x1
    dz = z2 - z1
    return math.degrees(math.atan2(dx, dz)) % 360


def convert_points(lines, origin_x, origin_y):
    results = []
    for _, pts in lines:
        for i, (ux, uy) in enumerate(pts):
            x = ux - origin_x
            z = uy - origin_y
            if i < len(pts) - 1:
                nx = pts[i+1][0] - origin_x
                nz = pts[i+1][1] - origin_y
                a = heading(x, z, nx, nz)
            elif i > 0:
                px = pts[i-1][0] - origin_x
                pz = pts[i-1][1] - origin_y
                a = heading(px, pz, x, z)
            else:
                a = 0.0
            results.append((x, z, a))
    return results


# --- cfgeventspawns.xml ---

def inject_into_eventspawns(cfg_path, event_name, points, mode):
    with open(cfg_path, "r", encoding="utf-8-sig") as f:
        raw = f.read()
    raw = raw.replace("\r\n", "\n").replace("\r", "\n")
    cfg = [line for line in raw.split("\n") if line.strip()]

    pos_lines = [f'\t\t<pos x="{x:.6f}" z="{z:.6f}" a="{a:.6f}" />' for x, z, a in points]

    event_pat = re.compile(r'^\s*<event\s+name="' + re.escape(event_name) + r'"', re.IGNORECASE)
    event_start = -1
    event_end = -1

    for i, line in enumerate(cfg):
        if event_pat.search(line):
            event_start = i
            if line.rstrip().endswith("/>"):
                event_end = i
                break
            for j in range(i + 1, len(cfg)):
                if re.search(r'^\s*</event>', cfg[j]):
                    event_end = j
                    break
            break

    if event_start == -1:
        close_tag = -1
        for i in range(len(cfg) - 1, -1, -1):
            if "</eventposdef>" in cfg[i]:
                close_tag = i
                break
        if close_tag == -1:
            print("FEHLER: </eventposdef> nicht gefunden")
            return "FEHLER"
        new_block = [f'\t<event name="{event_name}">'] + pos_lines + ["\t</event>"]
        cfg = cfg[:close_tag] + new_block + cfg[close_tag:]
        action = "NEU angelegt"

    elif event_end == event_start:
        new_block = [f'\t<event name="{event_name}">'] + pos_lines + ["\t</event>"]
        cfg = cfg[:event_start] + new_block + cfg[event_start+1:]
        action = "war leer -> eingefuegt"

    else:
        if mode == "replace":
            keep = [cfg[event_start]]
            for i in range(event_start + 1, event_end):
                if re.search(r'^\s*<zone\s', cfg[i]):
                    keep.append(cfg[i])
            keep += pos_lines
            keep.append(cfg[event_end])
            cfg = cfg[:event_start] + keep + cfg[event_end+1:]
            action = "ERSETZT"
        else:
            cfg = cfg[:event_end] + pos_lines + cfg[event_end:]
            action = "ANGEHAENGT"

    with open(cfg_path, "w", encoding="utf-8") as f:
        f.write("\n".join(cfg) + "\n")

    return action


# --- cfgplayerspawnpoints.xml ---

def inject_into_spawnpoints(cfg_path, points):
    with open(cfg_path, "r", encoding="utf-8-sig") as f:
        raw = f.read()
    raw = raw.replace("\r\n", "\n").replace("\r", "\n")
    cfg = [line for line in raw.split("\n") if line.strip()]

    pos_lines = [f'                <pos x="{x:.6f}" z="{z:.6f}" />' for x, z, _ in points]

    sections = ["fresh", "hop", "travel"]
    for section in sections:
        bubble_start = -1
        bubble_end = -1
        in_section = False

        for i, line in enumerate(cfg):
            if re.search(r'<' + section + r'>', line):
                in_section = True
            if in_section and re.search(r'<generator_posbubbles>', line):
                bubble_start = i
            if in_section and bubble_start != -1 and re.search(r'</generator_posbubbles>', line):
                bubble_end = i
                break
            if re.search(r'</' + section + r'>', line):
                in_section = False

        if bubble_start == -1 or bubble_end == -1:
            continue

        new_block = [
            cfg[bubble_start],
            '            <group name="Generator">',
        ] + pos_lines + [
            '            </group>',
            cfg[bubble_end],
        ]
        cfg = cfg[:bubble_start] + new_block + cfg[bubble_end+1:]

    with open(cfg_path, "w", encoding="utf-8") as f:
        f.write("\n".join(cfg) + "\n")

    return f"{len(points)} Positionen in {len(sections)} Sektionen (fresh/hop/travel) ERSETZT"


def main():
    print("=" * 55)
    print("  SHP -> DayZ XML Converter")
    print("=" * 55)

    # Zielformat
    print(f"\n[1] Ziel-XML")
    print(f"    1 = cfgeventspawns.xml     (Vehicle/Event Spawns)")
    print(f"    2 = cfgplayerspawnpoints.xml (Player Spawns)")
    target_in = input("    Wahl: ").strip()
    if target_in not in ("1", "2"):
        print("\n  FEHLER: 1 oder 2 waehlen!")
        input("\nEnter zum Beenden..."); return
    target = "event" if target_in == "1" else "spawn"

    # Shapefile
    print(f"\n[2] Shapefile (.shp)")
    print(f"    Standard: {DEFAULT_SHP}")
    shp = input("    Pfad (Enter = Standard): ").strip().strip('"')
    if not shp:
        shp = DEFAULT_SHP
    if not os.path.exists(shp):
        print(f"\n  FEHLER: {shp} nicht gefunden!")
        input("\nEnter zum Beenden..."); return

    lines = read_shp_polylines(shp)
    total_pts = sum(len(pts) for _, pts in lines)
    print(f"    -> {len(lines)} Polylines, {total_pts} Punkte")

    points = convert_points(lines, DEFAULT_ORIGINX, DEFAULT_ORIGINY)

    if target == "event":
        # Event Name
        print(f"\n[3] Event-Name")
        print(f"    z.B.: StaticHeliCrash, VehicleCivilianSedan, StaticBonfire")
        event = input("    Event: ").strip()
        if not event:
            print("\n  FEHLER: Event-Name darf nicht leer sein!")
            input("\nEnter zum Beenden..."); return

        # Modus
        print(f"\n[4] Modus")
        print(f"    1 = append  (an bestehenden Event anhaengen)")
        print(f"    2 = replace (bestehende Positionen ersetzen)")
        mode_in = input("    Wahl (Enter = 1): ").strip()
        mode = "replace" if mode_in == "2" else "append"

        # Ziel-Datei
        print(f"\n[5] cfgeventspawns.xml")
        print(f"    Standard: {DEFAULT_EVENT}")
        cfg = input("    Pfad (Enter = Standard): ").strip().strip('"')
        if not cfg:
            cfg = DEFAULT_EVENT

    else:
        # Ziel-Datei
        print(f"\n[3] cfgplayerspawnpoints.xml")
        print(f"    Standard: {DEFAULT_SPAWN}")
        cfg = input("    Pfad (Enter = Standard): ").strip().strip('"')
        if not cfg:
            cfg = DEFAULT_SPAWN

    if not os.path.exists(cfg):
        print(f"\n  FEHLER: {cfg} nicht gefunden!")
        input("\nEnter zum Beenden..."); return

    # Ausfuehren
    print("\n" + "-" * 55)

    if target == "event":
        action = inject_into_eventspawns(cfg, event, points, mode)
        print(f"\n  Event '{event}': {total_pts} Positionen {action}")
    else:
        action = inject_into_spawnpoints(cfg, points)
        print(f"\n  Player Spawns: {action}")

    print(f"  Quelle:  {os.path.basename(shp)}")
    print(f"  Ziel:    {cfg}")
    print("\n" + "=" * 55)
    input("\nEnter zum Beenden...")


if __name__ == "__main__":
    main()
