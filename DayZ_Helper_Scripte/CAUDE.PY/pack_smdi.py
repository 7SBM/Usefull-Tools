"""
Pack separate Metallic/Roughness maps into one SMDI texture (Arma3/DayZ
metallic-roughness workflow, per the SubstanceToArma preset convention):

    R = User1 (white / 255)
    G = Metallic
    B = Roughness
    A = unused (255)

Source: https://github.com/MoonieFR/SubstanceToArma (Arma 3 "Super" preset docs).
Note from that doc: invert roughness before export if the result looks too
bright in-game -- that's a per-asset visual call, not a fixed rule, so it's
exposed here as an option rather than baked in.

Usage:
    python pack_smdi.py metallic.png roughness.png output_smdi.png [--invert-roughness]

Prerequisite: source maps must already be plain images (PNG/TGA) -- Pillow
cannot read/write .paa directly. Convert the packed output PNG to .paa
afterwards with ImageToPAA (DayZ Tools\\Bin\\ImageToPAA\\imageToPaaGUI.exe).
"""

import sys
from PIL import Image


def load_gray(path):
    return Image.open(path).convert("L")


def main():
    args = sys.argv[1:]
    invert_roughness = "--invert-roughness" in args
    args = [a for a in args if a != "--invert-roughness"]

    if len(args) != 3:
        print(__doc__)
        sys.exit(1)

    metallic_path, roughness_path, out_path = args
    metallic_img = load_gray(metallic_path)
    roughness_img = load_gray(roughness_path)

    if metallic_img.size != roughness_img.size:
        roughness_img = roughness_img.resize(metallic_img.size)

    if invert_roughness:
        roughness_img = Image.eval(roughness_img, lambda v: 255 - v)

    white = Image.new("L", metallic_img.size, color=255)

    packed = Image.merge("RGBA", (white, metallic_img, roughness_img, white))
    packed.save(out_path)
    print(f"wrote {out_path} ({packed.size[0]}x{packed.size[1]}) R=white G=metallic B=roughness A=white")


if __name__ == "__main__":
    main()
