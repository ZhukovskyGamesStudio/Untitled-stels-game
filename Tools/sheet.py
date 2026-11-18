"""Contact sheets of screenshots: python Tools/sheet.py <dir> [cols rows] -> <dir>/_sheets/sheet_NN.png (labels = file names)."""
import sys, os, glob
from PIL import Image, ImageDraw
d = sys.argv[1]; cols = int(sys.argv[2]) if len(sys.argv) > 2 else 2; rows = int(sys.argv[3]) if len(sys.argv) > 3 else 2
files = sorted(glob.glob(os.path.join(d, "*.png")))
out = os.path.join(d, "_sheets"); os.makedirs(out, exist_ok=True)
W, H = 1920 // cols, 1080 // rows
for i in range(0, len(files), cols * rows):
    sheet = Image.new("RGB", (W * cols, H * rows))
    for k, f in enumerate(files[i:i + cols * rows]):
        im = Image.open(f).convert("RGB").resize((W, H), Image.LANCZOS)
        ImageDraw.Draw(im).text((6, H - 16), os.path.basename(f)[:-4], fill=(255, 255, 0))
        sheet.paste(im, ((k % cols) * W, (k // cols) * H))
    sheet.save(os.path.join(out, "sheet_%02d.png" % (i // (cols * rows))))
print(len(files), "files")
