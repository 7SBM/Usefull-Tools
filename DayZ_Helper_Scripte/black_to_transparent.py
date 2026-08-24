from pathlib import Path
from PIL import Image
import numpy as np

Image.MAX_IMAGE_PIXELS = None
THRESHOLD = 10

script_dir = Path(__file__).parent
out_dir = script_dir / "output"
out_dir.mkdir(exist_ok=True)

for png in sorted(script_dir.glob("*.png")):
    print(f"Verarbeite: {png.name}")
    img = Image.open(png).convert("RGBA")
    arr = np.array(img, dtype=np.uint8)
    schwarz = (arr[:,:,0] <= THRESHOLD) & (arr[:,:,1] <= THRESHOLD) & (arr[:,:,2] <= THRESHOLD)
    arr[schwarz, 3] = 0
    result = Image.fromarray(arr, "RGBA")
    out = out_dir / (png.stem + "_TRANSPARENT.png")
    result.save(out, "PNG")
    print(f"  -> {out}")

print("Fertig.")
