"""Ideology style "Cyberpunk": sample sprites in two directions for docs/IDEOLOGY_STYLE.md (south views, 128 px).
Manner C of docs/ART_STYLE.md: muted body, black outline, one glowing accent.

Writes "Hacking Expansion Netrun Art/Concepts10/StyleSamples/" and docs/ui-mockups/35-ideology-style-directions.png.
Run from the repo root:  python tools/gen_style_samples.py
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
import gen_icons_v2 as G  # noqa: E402
from gen_icons_v2 import Icon, m_ellipse, m_line, m_poly, m_rrect, union, lw  # noqa: E402
from netrun_art import font  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Hacking Expansion Netrun Art", "Concepts10", "StyleSamples")
MOCK = os.path.join(ROOT, "docs", "ui-mockups", "35-ideology-style-directions.png")

DIRS = {
    "A": dict(name="A: Корп-хром", body=((236, 238, 242), (176, 182, 192)), body2=((150, 158, 170), (96, 104, 116)),
              trim=((210, 214, 220), (140, 146, 156)), glow=(70, 220, 240), glow2=(70, 220, 240), sticker=None),
    "B": dict(name="B: Уличный неон", body=((96, 100, 110), (52, 55, 62)), body2=((70, 74, 82), (38, 40, 46)),
              trim=((140, 120, 96), (90, 74, 58)), glow=(255, 60, 160), glow2=(60, 220, 240), sticker=((240, 200, 60), (200, 150, 30))),
}


class GlowIcon(Icon):
    def __init__(self):
        super().__init__()
        self.glows = []   # (mask, rgb)

    def glow(self, mask, rgb):
        self.parts.append((mask, tuple(min(255, c + 40) for c in rgb), rgb))
        self.glows.append((mask, rgb))

    def render(self, size):
        base = super().render(G.N)
        halo = Image.new("RGBA", (G.N, G.N), (0, 0, 0, 0))
        for mask, rgb in self.glows:
            layer = Image.new("RGBA", (G.N, G.N), rgb + (0,))
            layer.putalpha(mask.filter(ImageFilter.GaussianBlur(18)).point(lambda v: int(v * 0.55)))
            halo.alpha_composite(layer)
        out = Image.new("RGBA", (G.N, G.N), (0, 0, 0, 0))
        out.alpha_composite(halo)
        out.alpha_composite(base)
        return out.resize((size, size), Image.LANCZOS)


def lamp(D):
    ic = GlowIcon()
    ic.part(m_ellipse(0.30, 0.80, 0.70, 0.92), *D["body2"])                     # base
    ic.part(m_rrect(0.46, 0.34, 0.54, 0.86, 0.02), *D["trim"])                  # pole
    head = m_poly([(0.30, 0.16), (0.70, 0.16), (0.76, 0.34), (0.24, 0.34)])
    ic.part(head, *D["body"])
    ic.glow(m_rrect(0.27, 0.30, 0.73, 0.36, 0.02), D["glow"])                   # light strip
    if D["sticker"]:
        ic.part(m_rrect(0.50, 0.50, 0.58, 0.58, 0.01), *D["sticker"])
        ic.glow(m_rrect(0.535, 0.40, 0.555, 0.80, 0.01), D["glow2"])           # neon tube on the pole
    return ic


def chair(D):
    ic = GlowIcon()
    ic.part(m_rrect(0.26, 0.10, 0.74, 0.50, 0.06), *D["body2"])                 # backrest (behind)
    ic.glow(m_rrect(0.30, 0.16, 0.70, 0.21, 0.02), D["glow"])
    for x in (0.28, 0.66):
        ic.part(m_rrect(x, 0.66, x + 0.06, 0.92, 0.015), *D["trim"])            # legs
    ic.part(m_rrect(0.20, 0.46, 0.80, 0.70, 0.06), *D["body"])                  # seat
    ic.line(m_line([(0.24, 0.62), (0.76, 0.62)], lw(1.6)))
    if D["sticker"]:
        ic.part(m_rrect(0.60, 0.26, 0.68, 0.34, 0.01), *D["sticker"])
    return ic


def end_table(D):
    ic = GlowIcon()
    ic.part(m_rrect(0.16, 0.30, 0.84, 0.54, 0.04), *D["body"])                  # top
    ic.part(m_rrect(0.20, 0.50, 0.80, 0.84, 0.03), *D["body2"])                 # front
    ic.glow(m_rrect(0.24, 0.58, 0.76, 0.62, 0.01), D["glow"])                   # drawer light line
    ic.part(m_rrect(0.44, 0.68, 0.56, 0.74, 0.01), *D["trim"])                  # handle
    ic.part(m_rrect(0.40, 0.18, 0.60, 0.34, 0.02), *D["trim"])                  # a small deck on top
    ic.glow(m_rrect(0.43, 0.21, 0.57, 0.27, 0.01), D["glow2"])
    if D["sticker"]:
        ic.part(m_rrect(0.66, 0.70, 0.76, 0.78, 0.01), *D["sticker"])
    return ic


def jacket(D):
    ic = GlowIcon()
    body = m_poly([(0.32, 0.14), (0.42, 0.10), (0.50, 0.20), (0.58, 0.10), (0.68, 0.14), (0.88, 0.30), (0.82, 0.62),
                   (0.74, 0.58), (0.74, 0.90), (0.26, 0.90), (0.26, 0.58), (0.18, 0.62), (0.12, 0.30)])
    ic.part(body, *D["body"])
    ic.part(m_poly([(0.42, 0.10), (0.50, 0.20), (0.46, 0.46), (0.38, 0.24)]), *D["body2"])     # collar
    ic.part(m_poly([(0.58, 0.10), (0.50, 0.20), (0.54, 0.46), (0.62, 0.24)]), *D["body2"])
    ic.line(m_line([(0.50, 0.22), (0.50, 0.88)], lw(1.8)))
    ic.glow(m_poly([(0.13, 0.33), (0.18, 0.31), (0.23, 0.58), (0.18, 0.60)]), D["glow"])      # sleeve strip
    ic.glow(m_poly([(0.87, 0.33), (0.82, 0.31), (0.77, 0.58), (0.82, 0.60)]), D["glow2"])
    if D["sticker"]:
        ic.part(m_rrect(0.58, 0.60, 0.70, 0.70, 0.01), *D["sticker"])
    return ic


def neon_sign(D):
    """A unique designator: a wall neon sign."""
    ic = GlowIcon()
    ic.part(m_rrect(0.10, 0.24, 0.90, 0.76, 0.05), *D["body2"])
    ic.glow(G.minus(m_ellipse(0.20, 0.32, 0.46, 0.68), m_ellipse(0.25, 0.37, 0.41, 0.63)), D["glow"])
    ic.glow(m_poly([(0.56, 0.32), (0.80, 0.32), (0.80, 0.38), (0.62, 0.38), (0.62, 0.47), (0.76, 0.47), (0.76, 0.53),
                    (0.62, 0.53), (0.62, 0.68), (0.56, 0.68)]), D["glow2"])
    return ic


def style_icon(D):
    """The style category icon: a chip-eye glyph in the direction's colours."""
    ic = GlowIcon()
    ic.part(m_rrect(0.16, 0.16, 0.84, 0.84, 0.14), *D["body2"])
    eye = m_poly([(0.24, 0.5)] + [(0.24 + 0.52 * t / 20, 0.5 - 0.16 * __import__("math").sin(3.14159 * t / 20)) for t in range(21)] +
                 [(0.24 + 0.52 * (20 - t) / 20, 0.5 + 0.16 * __import__("math").sin(3.14159 * (20 - t) / 20)) for t in range(21)])
    ic.part(eye, *D["body"])
    ic.glow(m_ellipse(0.42, 0.42, 0.58, 0.58), D["glow"])
    return ic


ITEMS = [("Lamp", "Торшер", lamp), ("Chair", "Стул", chair), ("EndTable", "Тумба", end_table),
         ("Jacket", "Куртка (одежда)", jacket), ("NeonSign", "Вывеска (своя)", neon_sign), ("StyleIcon", "Иконка стиля", style_icon)]


def build():
    os.makedirs(OUT, exist_ok=True)
    cell, pad = 128, 26
    W = 240 + len(ITEMS) * (cell + pad)
    H = 120 + len(DIRS) * (cell + 70) + 40
    sheet = Image.new("RGBA", (W, H), (43, 43, 43, 255))
    floor = Image.new("RGBA", (cell, cell), (88, 80, 66, 255))       # wooden floor tone to judge contrast
    fd = ImageDraw.Draw(floor)
    for y in range(0, cell, 16):
        fd.line([(0, y), (cell, y)], fill=(74, 66, 54, 255), width=1)
    d = ImageDraw.Draw(sheet)
    d.text((20, 18), "Стиль Ideology «Киберпанк»: два направления (вид с юга, 128 px, на деревянном полу)", font=font(22, True), fill=(235, 235, 235))
    for i, (_, label, _) in enumerate(ITEMS):
        x = 240 + i * (cell + pad)
        d.text((x, 70), label, font=font(13), fill=(190, 190, 190))
    for r, (key, D) in enumerate(DIRS.items()):
        y = 100 + r * (cell + 70)
        d.text((20, y + cell / 2 - 12), D["name"], font=font(17, True), fill=(235, 235, 235))
        for i, (fname, label, fn) in enumerate(ITEMS):
            im = fn(D).render(cell)
            fn(D).render(256).save(os.path.join(OUT, f"{key}_{fname}.png"))
            x = 240 + i * (cell + pad)
            tile = floor.copy() if fname not in ("StyleIcon", "Jacket") else Image.new("RGBA", (cell, cell), (30, 30, 32, 255))
            tile.alpha_composite(im)
            sheet.alpha_composite(tile, (x, y))
    d.text((20, H - 34), "Одежда показана иконкой предмета; на пешке рисуются отдельные текстуры (юг, север, восток).", font=font(13), fill=(170, 170, 170))
    sheet.convert("RGB").save(MOCK)


if __name__ == "__main__":
    build()
    print("done")
