#!/usr/bin/env python3
"""
generate_mapgroupproto.py  -  Brienz Assets MapGroupProto Generator

Run from H:\\brienz_assets\\ or pass the path as argument.
Scans all DayZ building mods (DePbo configs + PBO binaries)
and generates a mapgroupproto.xml with entries for each building.

Excludes: tunnel, cave, bridge segments, modular construction pieces,
props, stairs, platforms, rails, fences, signs, ladders, antennas.
"""

import os
import re
import sys
from pathlib import Path
from collections import OrderedDict

# ---------------------------------------------------------------------------
# CONFIG  —  adjust these if buildings get wrongly included/excluded
# ---------------------------------------------------------------------------

EXCLUDE_KEYWORDS = [
    "tunnel_", "bridge", "platform", "stairs", "stair",
    "_rail", "support", "ramp", "segment", "piece",
    "ladder", "fence", "gate", "wall_", "_wall", "door",
    "pillar", "corner", "panel", "billboard", "sign_",
    "roadway", "antenna", "_kit", "placing",
    "_lab", "ceiling", "truss", "window",
    "cone", "cart", "vitrine", "parking", "protection",
    "police_band",
]

EXCLUDE_PREFIXES = [
    "MC_",
    "maBLDS_",
    "CBS_SciFi_",
    "BM_Brick",
    "BM_Tiled",
    "BM_OSB",
    "BM_Paper",
    "BM_Wood_",
    "BM_Modular",
    "BM_Concrete",
    "BM_Steel",
    "BM_Nature",
    "BM_Glass",
    "BM_Protection",
    "BM_Shopping",
    "BM_Doller",
    "BM_Leather",
    "BM_Platform",
    "BM_Stair",
    "BM_Roadway",
    "BM_RoadWay",
    "PNH_",
    "Land_sp_platform",
]

EXCLUDE_FULL_MODS = {
    "BM_ModularConstruction",
    "CBS_SciFi_Structures",
    "MA_Props",
    "CS_Modular_Tunnel",
    "HA_Foot_Bridge",
}

DEFAULT_USAGE = ["Town", "Village"]


# ---------------------------------------------------------------------------
# STRING EXTRACTION
# ---------------------------------------------------------------------------

def extract_strings(data: bytes, min_len: int = 4) -> list[str]:
    """Extract null-terminated ASCII strings from binary data."""
    strings = []
    current = []
    for b in data:
        if 0x20 <= b < 0x7F:
            current.append(chr(b))
        else:
            if len(current) >= min_len:
                strings.append("".join(current))
            current = []
    if len(current) >= min_len:
        strings.append("".join(current))
    return strings


# ---------------------------------------------------------------------------
# EXTRACTION FROM CONFIG FILES
# ---------------------------------------------------------------------------

def extract_from_config_cpp(filepath: Path) -> list[str]:
    """Parse a decompiled config.cpp for class names under CfgVehicles."""
    try:
        text = filepath.read_text(encoding="utf-8", errors="replace")
    except Exception:
        return []

    cfg_match = re.search(r"class\s+CfgVehicles\s*\{", text)
    if not cfg_match:
        return []

    section = text[cfg_match.start():]
    classes = []
    for m in re.finditer(r"class\s+(\w+)\s*:\s*(\w+)", section):
        child, parent = m.group(1), m.group(2)
        if parent in ("HouseNoDestruct", "House", "Building",
                       "HouseNoDestructBase"):
            classes.append(child)
    return classes


def extract_from_config_bin(filepath: Path) -> list[str]:
    """Extract Land_*/land_* class names from a config.bin binary."""
    try:
        data = filepath.read_bytes()
    except Exception:
        return []

    strings = extract_strings(data, min_len=6)
    classes = set()

    for s in strings:
        if re.fullmatch(r"[Ll]and_[A-Za-z0-9_]{3,60}", s):
            classes.add(s)

    return list(classes)


def extract_from_pbo(filepath: Path) -> list[str]:
    """Extract building class names from a PBO binary.

    Uses two methods:
    1. Land_*/land_* pattern matching on extracted strings
    2. For mods without Land_ prefix: look for class names
       immediately before 'HouseNoDestruct' in the string list
    """
    try:
        data = filepath.read_bytes()
    except Exception:
        return []

    strings = extract_strings(data, min_len=4)
    classes = set()

    config_noise = {
        "HouseNoDestruct", "HouseNoDestructBase",
        "CfgPatches", "CfgVehicles", "CfgMods", "CfgSounds",
        "DZ_Data", "DZ_Scripts", "DZ_Structures", "DZ_Structures_Signs",
        "scope", "model", "displayName", "descriptionShort",
        "hiddenSelections", "hiddenSelectionsTextures",
        "hiddenSelectionsMaterials",
        "DamageSystem", "GlobalHealth", "GlobalArmor",
        "Projectile", "Health", "Blood", "Shock", "Melee",
        "FragGrenade", "DamageZones",
        "Doors", "Door1", "Door2", "Doors1", "Doors2",
        "soundOpen", "soundClose", "soundLocked", "soundOpenABit",
        "animPeriod", "initPhase", "initOpened", "component", "soundPos",
        "requiredAddons", "requiredVersion",
        "Game", "World", "Mission",
        "gameScriptModule", "worldScriptModule", "missionScriptModule",
        "dependencies", "author", "authorID", "version", "credits",
        "value", "files", "name", "type", "extra", "picture", "action",
        "hideName", "hidePicture", "units", "weapons",
        "hitpoints", "damage", "transferToGlobalCoef",
        "fatalInjuryCoef", "componentNames",
        "ArmorType", "true", "false",
    }

    # Method 1 — Land_*/land_* strings
    for s in strings:
        if re.fullmatch(r"[Ll]and_[A-Za-z0-9_]{3,60}", s):
            classes.add(s)

    # Method 2 — class names that appear right before HouseNoDestruct
    for i, s in enumerate(strings):
        if s == "HouseNoDestruct" and i > 0:
            prev = strings[i - 1]
            if (re.fullmatch(r"[A-Za-z][A-Za-z0-9_]{4,60}", prev)
                    and prev not in config_noise
                    and not prev.endswith(("_co", "_nohq", "_smdi",
                                           "_ca", ".p3d", ".paa"))):
                classes.add(prev)

        if s == "HouseNoDestruct" and i + 1 < len(strings):
            nxt = strings[i + 1]
            if (re.fullmatch(r"[A-Za-z][A-Za-z0-9_]{4,60}", nxt)
                    and nxt not in config_noise
                    and not nxt.endswith(("_co", "_nohq", "_smdi",
                                          "_ca", ".p3d", ".paa"))):
                classes.add(nxt)

    return list(classes)


# ---------------------------------------------------------------------------
# GROUPPROTO LOADING
# ---------------------------------------------------------------------------

def load_groupproto_txt(filepath: Path) -> dict:
    """Parse a groupproto .txt file (single <group> element)."""
    try:
        text = filepath.read_text(encoding="utf-8", errors="replace")
    except Exception:
        return {}

    results = {}
    for gm in re.finditer(
        r'<group\s+name="([^"]+)"[^>]*>(.*?)</group>',
        text, re.DOTALL
    ):
        name = gm.group(1)
        body = gm.group(2)

        usages = re.findall(r'<usage\s+name="([^"]+)"', body)

        containers = []
        for cm in re.finditer(
            r'<container\s+name="([^"]+)"[^>]*>(.*?)</container>',
            body, re.DOTALL
        ):
            cname = cm.group(1)
            lootmax_m = re.search(r'lootmax="(\d+)"', cm.group(0))
            clootmax = lootmax_m.group(1) if lootmax_m else "4"
            points = []
            for pm in re.finditer(
                r'<point\s+pos="([^"]+)"\s+range="([^"]+)"\s+'
                r'height="([^"]+)"',
                cm.group(2)
            ):
                points.append({
                    "pos": pm.group(1),
                    "range": pm.group(2),
                    "height": pm.group(3),
                })
            containers.append({
                "name": cname,
                "lootmax": clootmax,
                "points": points,
            })

        lootmax_g = re.search(
            r'lootmax="(\d+)"', gm.group(0).split(">")[0]
        )
        results[name] = {
            "lootmax": lootmax_g.group(1) if lootmax_g else "0",
            "usages": usages,
            "containers": containers,
        }

    return results


# ---------------------------------------------------------------------------
# FILTER
# ---------------------------------------------------------------------------

def should_exclude(classname: str) -> bool:
    """Return True if this class is a modular piece / prop / tunnel etc."""
    low = classname.lower()

    for pfx in EXCLUDE_PREFIXES:
        if classname.startswith(pfx) or low.startswith(pfx.lower()):
            return True

    for kw in EXCLUDE_KEYWORDS:
        if kw in low:
            return True

    return False


def is_from_excluded_mod(mod_name: str) -> bool:
    """Return True if the entire mod should be skipped."""
    return mod_name in EXCLUDE_FULL_MODS


# ---------------------------------------------------------------------------
# XML GENERATION
# ---------------------------------------------------------------------------

def generate_xml(buildings: dict[str, dict],
                  custom_names: set[str]) -> str:
    """Generate mapgroupproto.xml content."""
    lines = [
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
        "<mapgroupproto>",
        "",
    ]

    custom = {k: v for k, v in buildings.items() if k in custom_names}
    auto = {k: v for k, v in buildings.items() if k not in custom_names}

    def write_group(name, info):
        lines.append(
            f'    <group name="{name}" lootmax="{info["lootmax"]}">'
        )
        for u in info.get("usages", DEFAULT_USAGE):
            lines.append(f'        <usage name="{u}" />')
        for cont in info.get("containers", []):
            lines.append(
                f'        <container name="{cont["name"]}" '
                f'lootmax="{cont["lootmax"]}">'
            )
            for pt in cont["points"]:
                lines.append(
                    f'            <point pos="{pt["pos"]}" '
                    f'range="{pt["range"]}" '
                    f'height="{pt["height"]}" />'
                )
            lines.append("        </container>")
        lines.append("    </group>")
        lines.append("")

    if custom:
        lines.append(
            "    <!-- ===== Buildings with custom loot data ===== -->"
        )
        lines.append("")
        for name in sorted(custom):
            write_group(name, custom[name])

    if auto:
        lines.append(
            "    <!-- ===== Auto-generated center spawns "
            "(adjust points per building) ===== -->"
        )
        lines.append("")
        for name in sorted(auto):
            write_group(name, auto[name])

    lines.append("</mapgroupproto>")
    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------------------
# MAIN
# ---------------------------------------------------------------------------

def main():
    if len(sys.argv) > 1:
        base = Path(sys.argv[1])
    else:
        base = Path.cwd()

    depbo_dir = base / "DePbo_From @MODS"
    pbo_dir = base / "Pbo_From_@MODS"
    groupproto_dir = base / "!!! Groupprotos !!!"

    if not depbo_dir.exists() and not pbo_dir.exists():
        print(f"ERROR: Neither DePbo nor PBO folder found in {base}")
        sys.exit(1)

    all_classes: OrderedDict[str, str] = OrderedDict()
    excluded: list[tuple[str, str]] = []

    # --- Step 1a: DePbo config.cpp files ---
    if depbo_dir.exists():
        for cpp in sorted(depbo_dir.rglob("config.cpp")):
            mod = cpp.parts[len(depbo_dir.parts)]
            if is_from_excluded_mod(mod):
                continue
            for cls in extract_from_config_cpp(cpp):
                if should_exclude(cls):
                    excluded.append((cls, mod))
                else:
                    all_classes[cls] = mod

    # --- Step 1b: DePbo config.bin files ---
    if depbo_dir.exists():
        for binf in sorted(depbo_dir.rglob("config.bin")):
            mod = binf.parts[len(depbo_dir.parts)]
            if is_from_excluded_mod(mod):
                continue
            for cls in extract_from_config_bin(binf):
                if should_exclude(cls):
                    excluded.append((cls, mod))
                elif cls not in all_classes:
                    all_classes[cls] = mod

    # --- Step 1c: PBOs without DePbo ---
    depbo_mods = set()
    if depbo_dir.exists():
        depbo_mods = {d.name for d in depbo_dir.iterdir() if d.is_dir()}

    if pbo_dir.exists():
        for pbo in sorted(pbo_dir.glob("*.pbo")):
            stem = pbo.stem
            if stem in depbo_mods:
                continue
            if is_from_excluded_mod(stem):
                continue
            print(f"  Scanning PBO: {pbo.name} ...", flush=True)
            for cls in extract_from_pbo(pbo):
                if should_exclude(cls):
                    excluded.append((cls, stem))
                elif cls not in all_classes:
                    all_classes[cls] = stem

    print()
    print(f"Found {len(all_classes)} building classes "
          f"({len(excluded)} excluded)")
    print()

    # --- Step 2: Load existing groupproto data ---
    groupprotos: dict[str, dict] = {}
    if groupproto_dir.exists():
        for f in sorted(groupproto_dir.iterdir()):
            if f.suffix in (".txt",):
                groupprotos.update(load_groupproto_txt(f))

    if groupprotos:
        print(f"Loaded loot data for: "
              f"{', '.join(sorted(groupprotos))}")
        print()

    # --- Step 3: Build output ---
    default_points = [
        {"pos": "0.0 0.5 0.0",   "range": "2", "height": "1"},
        {"pos": "1.5 0.5 1.5",   "range": "2", "height": "1"},
        {"pos": "-1.5 0.5 -1.5", "range": "2", "height": "1"},
    ]

    buildings: dict[str, dict] = {}
    for cls in all_classes:
        if cls in groupprotos:
            buildings[cls] = groupprotos[cls]
        else:
            buildings[cls] = {
                "lootmax": "3",
                "usages": list(DEFAULT_USAGE),
                "containers": [{
                    "name": "lootFloor",
                    "lootmax": "3",
                    "points": list(default_points),
                }],
            }

    # --- Step 4: Write XML ---
    custom_names = set(groupprotos.keys())
    xml = generate_xml(buildings, custom_names)
    out_path = base / "mapgroupproto.xml"
    out_path.write_text(xml, encoding="utf-8")

    n_custom = sum(1 for k in buildings if k in custom_names)
    n_auto = len(buildings) - n_custom

    print(f"Generated: {out_path}")
    print(f"  {len(buildings)} building entries total")
    print(f"  {n_custom} with custom loot data")
    print(f"  {n_auto} with auto-generated center spawns")
    print()

    print("=== INCLUDED BUILDINGS ===")
    for cls in sorted(buildings):
        src = all_classes.get(cls, "?")
        loot = "LOOT" if buildings[cls].get("containers") else "skeleton"
        print(f"  {cls:<50s}  [{src}]  ({loot})")

    print()
    print(f"=== EXCLUDED ({len(set(excluded))} entries) ===")
    for cls, src in sorted(set(excluded)):
        print(f"  {cls:<50s}  [{src}]")


if __name__ == "__main__":
    main()
