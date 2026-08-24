from PIL import Image
import os

FOLDER = os.path.dirname(os.path.abspath(__file__))
OUTPUT = os.path.join(FOLDER, "output")
SIZE = (128, 128)

os.makedirs(OUTPUT, exist_ok=True)

for fname in os.listdir(FOLDER):
    if not fname.lower().endswith(".png"):
        continue

    img = Image.open(os.path.join(FOLDER, fname)).convert("RGBA")

    img.thumbnail(SIZE, Image.LANCZOS)

    canvas = Image.new("RGBA", SIZE, (0, 0, 0, 0))
    x = (SIZE[0] - img.width) // 2
    y = (SIZE[1] - img.height) // 2
    canvas.paste(img, (x, y))
    canvas.save(os.path.join(OUTPUT, fname), "PNG")

    print("Resized: %s (%dx%d)" % (fname, img.width, img.height))

print("Fertig.")
