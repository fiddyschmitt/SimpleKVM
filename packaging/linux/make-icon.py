#!/usr/bin/env python3
"""Draws the Linux launcher icon (simplekvm.png, 256x256) from the app's 32x32 Windows icon.

The .ico is the only artwork there is, and launchers show icons at 48 to 256 pixels. Plain
upscaling blurs it, so the two-tone drawing is enlarged far past the target, its soft edges
are snapped back to black or white with a steep tone curve, and the result is scaled down
again, which leaves smooth, sharp outlines. It is then set on a white tile with rounded
corners, the shape desktop icons usually have.

    python packaging/linux/make-icon.py        (needs Pillow)
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

here = Path(__file__).resolve().parent
source = here.parent.parent / "SimpleKVM" / "iconfinder_Communication_pc_computer_sharing_6588768_white_bg.ico"

SIZE, WORK = 256, 2048
TILE_INSET, TILE_RADIUS = 10, 46        # the rounded white tile, in final pixels
GLYPH = 196                             # the drawing's size on the tile

art = Image.open(source).convert("RGBA")
flat = Image.new("RGBA", art.size, (255, 255, 255, 255))
flat.alpha_composite(art)

big = flat.convert("L").resize((WORK, WORK), Image.BICUBIC).filter(ImageFilter.GaussianBlur(10))
crisp = big.point(lambda v: 0 if v < 118 else 255 if v > 138 else int((v - 118) * 255 / 20))
glyph = crisp.resize((GLYPH, GLYPH), Image.LANCZOS)

scale = 4                               # the tile is drawn oversized too, for smooth corners
tile = Image.new("L", (SIZE * scale, SIZE * scale), 0)
ImageDraw.Draw(tile).rounded_rectangle(
    [TILE_INSET * scale, TILE_INSET * scale, (SIZE - TILE_INSET) * scale - 1, (SIZE - TILE_INSET) * scale - 1],
    radius=TILE_RADIUS * scale, fill=255)
tile = tile.resize((SIZE, SIZE), Image.LANCZOS)

icon = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
white = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 255))
offset = (SIZE - GLYPH) // 2
white.paste(Image.merge("RGBA", (glyph, glyph, glyph, Image.new("L", glyph.size, 255))), (offset, offset))
icon.paste(white, (0, 0), tile)

icon.save(here / "simplekvm.png", optimize=True)
print("wrote", here / "simplekvm.png")
