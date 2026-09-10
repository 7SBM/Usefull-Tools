"""Strip absolute drive-letter paths (e.g. 'p:\\...') baked into a p3d's
per-face material/texture references, replacing them with the PBO-relative
path (everything after the drive letter + backslash).

Usage: python fix_p3d_paths.py path/to/model.p3d
"""
import sys
import re
import py3d

DRIVE_PREFIX = re.compile(r"^[a-zA-Z]:[\\/]")


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(1)

    path = sys.argv[1]
    with open(path, "rb") as f:
        p = py3d.P3D(f)

    fixed = 0
    for lod in p.lods:
        for face in lod.faces:
            if DRIVE_PREFIX.match(face.texture):
                face.texture = DRIVE_PREFIX.sub("", face.texture)
                fixed += 1
            if DRIVE_PREFIX.match(face.material):
                face.material = DRIVE_PREFIX.sub("", face.material)
                fixed += 1

    print(f"fixed {fixed} face texture/material references")

    with open(path, "wb") as f:
        p.write(f)
    print(f"written back to {path}")


if __name__ == "__main__":
    main()
