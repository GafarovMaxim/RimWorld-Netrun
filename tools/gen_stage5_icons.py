"""Stage 5 icons: the Biotech gene "neural resilience".

Gene icons in vanilla are a light glyph with soft shading over the gene card; learning icons are small colored
pictograms. Run: python tools/gen_stage5_icons.py
"""
import math
from PIL import Image, ImageDraw
from netrun_art import SS, canvas, finish, INK, CYAN, WHITE, DARK2, STEEL_HI, GOLD, with_alpha

import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Textures", "UI", "Netrun") + "/"


def neuron(d, cx, cy, r, fill, outline):
    # dendrites
    for i in range(6):
        a = math.pi * 2 * i / 6 + 0.3
        x1, y1 = cx + math.cos(a) * r * 2.3, cy + math.sin(a) * r * 2.3
        d.line([(cx, cy), (x1, y1)], fill=outline, width=int(r * 0.55))
        d.line([(cx, cy), (x1, y1)], fill=fill, width=int(r * 0.3))
        for s in (-0.5, 0.5):
            x2, y2 = x1 + math.cos(a + s) * r * 0.9, y1 + math.sin(a + s) * r * 0.9
            d.line([(x1, y1), (x2, y2)], fill=outline, width=int(r * 0.4))
            d.line([(x1, y1), (x2, y2)], fill=fill, width=int(r * 0.2))
    d.ellipse([cx - r * 1.1, cy - r * 1.1, cx + r * 1.1, cy + r * 1.1], fill=outline)
    d.ellipse([cx - r * 0.85, cy - r * 0.85, cx + r * 0.85, cy + r * 0.85], fill=fill)


def gene():
    size = 128
    img = canvas(size)
    d = ImageDraw.Draw(img)
    s = SS
    # shield
    pts = [(64 * s, 10 * s), (110 * s, 26 * s), (104 * s, 76 * s), (64 * s, 118 * s), (24 * s, 76 * s), (18 * s, 26 * s)]
    d.polygon(pts, fill=INK)
    inner = [(64 * s, 18 * s), (102 * s, 31 * s), (97 * s, 73 * s), (64 * s, 108 * s), (31 * s, 73 * s), (26 * s, 31 * s)]
    d.polygon(inner, fill=with_alpha(STEEL_HI, 255))
    inner2 = [(64 * s, 24 * s), (96 * s, 35 * s), (92 * s, 70 * s), (64 * s, 100 * s), (36 * s, 70 * s), (32 * s, 35 * s)]
    d.polygon(inner2, fill=(196, 206, 214, 255))
    neuron(d, 64 * s, 60 * s, 9 * s, WHITE, INK)
    d.ellipse([60 * s, 56 * s, 68 * s, 64 * s], fill=CYAN)
    finish(img, size).save(OUT + "Genes/NR_NeuralResilience.png")


if __name__ == "__main__":
    gene()
    print("ok")
