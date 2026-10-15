"""Ritual effect textures for the net rituals (litany of data, trace cleansing): white art, tinted green in the defs.

Writes Textures/Things/Mote/Netrun/Ritual/:
  Digit_0, Digit_1, Digit_Hex  - single glyphs that float up and fade (flecks, 64 x 64)
  Column_A/B/C                 - a falling column of code, bright head at the bottom, fading tail (fleck, 32 x 256)
  Circuit_A/B/C                - circuit traces on the floor with pads and vias (motes, 256 x 256, RitualOutline shader)
`--mock OUT` renders a frame of the effect over a dark floor into OUT.
"""
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
import deck_art as A  # noqa: E402

ROOT = os.path.join(os.path.dirname(__file__), "..")
OUT = os.path.join(ROOT, "Textures", "Things", "Mote", "Netrun", "Ritual")
GREEN = (60, 255, 130)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"))


def glow_glyph(text, n=64, size=40):
    """A white glyph with a soft halo, centred on a transparent n x n square."""
    lay = Image.new("L", (n, n), 0)
    ImageDraw.Draw(lay).text((n / 2, n / 2), text, font=A.font(size), fill=255, anchor="mm")
    halo = lay.filter(ImageFilter.GaussianBlur(5)).point(lambda v: int(v * 0.8))
    a = np.maximum(np.array(lay, np.float32), np.array(halo, np.float32))
    img = Image.new("RGBA", (n, n), (255, 255, 255, 0))
    img.putalpha(Image.fromarray(a.clip(0, 255).astype(np.uint8)))
    return img


def make_column(seed=7):
    """A column of code: 12 glyphs, the bottom one (the head) brightest, the rest fading upwards."""
    w, h, k = 32, 256, 12
    rnd = random.Random(seed)
    lay = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(lay)
    for i in range(k):
        ch = rnd.choice("01" * 4 + "ABCDEF3789")
        v = int(255 * (0.18 + 0.82 * (i / (k - 1)) ** 1.6))
        d.text((w / 2, 12 + i * (h - 24) / (k - 1)), ch, font=A.font(17), fill=v, anchor="mm")
    halo = lay.filter(ImageFilter.GaussianBlur(3)).point(lambda v: int(v * 0.7))
    a = np.maximum(np.array(lay, np.float32), np.array(halo, np.float32))
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    img.putalpha(Image.fromarray(a.clip(0, 255).astype(np.uint8)))
    return img


def make_circuit(seed):
    """PCB traces on a 16 px grid: 45-degree bends, round pads at the ends, vias along the way, fading at the edges."""
    n, g = 256, 16
    rnd = random.Random(seed)
    ss = 3
    lay = Image.new("L", (n * ss, n * ss), 0)
    d = ImageDraw.Draw(lay)
    for _ in range(9):
        x, y = rnd.randrange(2, 14) * g, rnd.randrange(2, 14) * g
        pts = [(x, y)]
        dx, dy = rnd.choice([(1, 0), (-1, 0), (0, 1), (0, -1)])
        for _ in range(rnd.randrange(3, 6)):
            steps = rnd.randrange(2, 5)
            x, y = x + dx * g * steps, y + dy * g * steps
            x, y = min(max(x, g), n - g), min(max(y, g), n - g)
            pts.append((x, y))
            if rnd.random() < 0.6:   # a 45-degree jog
                ddx = dx if dx else rnd.choice((-1, 1))
                ddy = dy if dy else rnd.choice((-1, 1))
                x, y = min(max(x + ddx * g, g), n - g), min(max(y + ddy * g, g), n - g)
                pts.append((x, y))
            dx, dy = rnd.choice([(dy, dx), (-dy, -dx)])
        d.line([(px * ss, py * ss) for px, py in pts], fill=255, width=3 * ss, joint="curve")
        for px, py in (pts[0], pts[-1]):
            d.ellipse(((px - 6) * ss, (py - 6) * ss, (px + 6) * ss, (py + 6) * ss), fill=255)
            d.ellipse(((px - 3) * ss, (py - 3) * ss, (px + 3) * ss, (py + 3) * ss), fill=0)
        for px, py in pts[1:-1]:
            if rnd.random() < 0.4:
                d.ellipse(((px - 4) * ss, (py - 4) * ss, (px + 4) * ss, (py + 4) * ss), fill=255)
    lay = lay.resize((n, n), Image.LANCZOS)
    a = np.array(lay, np.float32) / 255
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    r = np.hypot(xx - n / 2, yy - n / 2) / (n / 2)
    a *= np.clip((1 - r) * 2.2, 0, 1)
    img = Image.new("RGBA", (n, n), (255, 255, 255, 0))
    img.putalpha(Image.fromarray((a * 255).clip(0, 255).astype(np.uint8)))
    return img


def make_packet():
    """A data packet running along the floor, 48 x 12: a bright head on the right, a fading tail to the left."""
    w, h = 48, 12
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    tail = np.clip(xx / (w - 8), 0, 1) ** 2 * np.clip(1 - np.abs(yy - h / 2 + 0.5) / 2.2, 0, 1)
    head = np.clip(1 - np.hypot(xx - (w - 7), yy - h / 2 + 0.5) / 5, 0, 1) ** 1.4
    a = np.maximum(tail * 0.8, head)
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    img.putalpha(Image.fromarray((a * 255).clip(0, 255).astype(np.uint8)))
    return img


def build():
    save(make_packet(), "Packet")
    save(glow_glyph("0"), "Digit_0")
    save(glow_glyph("1"), "Digit_1")
    save(glow_glyph("3F", size=30), "Digit_Hex")
    for i, s in enumerate((7, 19, 42)):
        save(make_column(s), "Column_" + "ABC"[i])
    for i, s in enumerate((11, 23, 37)):
        save(make_circuit(s), "Circuit_" + "ABC"[i])


def tint(img, color, alpha=1.0):
    a = np.array(img.getchannel("A"), np.float32) * alpha
    out = Image.new("RGBA", img.size, color + (0,))
    out.putalpha(Image.fromarray(a.clip(0, 255).astype(np.uint8)))
    return out


def mock(path):
    """A 15 x 12 cell room at 48 px per cell: circuits on the floor, a green glow, falling columns and floating digits."""
    cell, w, h = 48, 15, 12
    img = Image.new("RGBA", (w * cell, h * cell), (34, 36, 40, 255))
    d = ImageDraw.Draw(img)
    for x in range(w):
        for y in range(h):
            if (x + y) % 2:
                d.rectangle((x * cell, y * cell, x * cell + cell - 1, y * cell + cell - 1), fill=(40, 42, 46, 255))
    rnd = random.Random(3)
    load = lambda n: Image.open(os.path.join(OUT, n + ".png")).convert("RGBA")
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((img.width * 0.15, img.height * 0.1, img.width * 0.85, img.height * 0.9), fill=GREEN + (60,))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(70)))
    for i in range(10):
        c = load("Circuit_" + "ABC"[i % 3]).resize((cell * 3, cell * 3))
        img.alpha_composite(tint(c, GREEN, 0.55), (rnd.randrange(0, img.width - cell * 3), rnd.randrange(0, img.height - cell * 3)))
    for _ in range(7):
        col = load("Column_" + rnd.choice("ABC")).resize((cell // 2, cell * 4))
        img.alpha_composite(tint(col, (120, 255, 160), rnd.uniform(0.5, 0.9)), (rnd.randrange(0, img.width - cell), rnd.randrange(-cell, img.height - cell * 4)))
    for _ in range(26):
        g = load(rnd.choice(["Digit_0", "Digit_1", "Digit_Hex"])).resize((cell // 2 + 6, cell // 2 + 6))
        img.alpha_composite(tint(g, rnd.choice([GREEN, (150, 255, 180), (40, 200, 90)]), rnd.uniform(0.4, 1.0)), (rnd.randrange(0, img.width - cell), rnd.randrange(0, img.height - cell)))
    img.save(path)


if __name__ == "__main__":
    build()
    if "--mock" in sys.argv:
        mock(sys.argv[sys.argv.index("--mock") + 1])
    print("Ritual effect textures written")
