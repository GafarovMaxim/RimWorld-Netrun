#!/usr/bin/env python3
"""
Icons for the net rituals and the Net Archon role in the style of the vanilla Ideology icons (2026-10-15):
white stick figures with a thick black outline and light grey accents on a transparent background, 128 px.
Every piece gets its own outline, so later pieces overlap earlier ones with a clean black edge.

    python tools/gen_ritual_icons.py
"""
import os
from collections import deque

import numpy as np

from PIL import Image, ImageDraw, ImageFilter

N = 128
K = 4                       # supersampling
O = 4.5                     # outline width at 128 px
WHITE, GREY, BLACK = (255, 255, 255, 255), (196, 196, 196, 255), (0, 0, 0, 255)
OUT = os.path.join(os.path.dirname(__file__), "..", "Textures", "UI", "Netrun", "Ideo")


class Icon:
    def __init__(self):
        self.img = Image.new("RGBA", (N * K, N * K), (0, 0, 0, 0))

    def piece(self, draw_fn, fill=WHITE, outline=O):
        """draw_fn(d, s) paints the piece in white on an L mask, coordinates scaled by s."""
        m = Image.new("L", self.img.size, 0)
        draw_fn(ImageDraw.Draw(m), K)
        r = int(round(outline * K))
        dil = m
        while r > 0:                                # MaxFilter in steps: big kernels are slow
            step = min(r, 6)
            dil = dil.filter(ImageFilter.MaxFilter(2 * step + 1))
            r -= step
        self.img.paste(Image.new("RGBA", self.img.size, BLACK), (0, 0), dil)
        self.img.paste(Image.new("RGBA", self.img.size, fill), (0, 0), m)

    def lines(self, pts, w, fill=WHITE):
        def f(d, s):
            P = [(x * s, y * s) for x, y in pts]
            d.line(P, fill=255, width=int(w * s), joint="curve")
            for x, y in P:
                d.ellipse([x - w * s / 2, y - w * s / 2, x + w * s / 2, y + w * s / 2], fill=255)
        self.piece(f, fill)

    def detail(self, draw_fn):
        """Black detail lines on top, no outline."""
        draw_fn(ImageDraw.Draw(self.img), K)

    def save(self, name, top=None):
        img = self.img.resize((N, N), Image.LANCZOS)
        if top is not None:
            img.alpha_composite(top)
        img.save(os.path.join(OUT, name + ".png"))


REFS = os.path.join(os.path.dirname(__file__), "ritual_refs")


def ref_figures(name):
    """The figures from a vanilla ritual icon (Maxim's references in tools/ritual_refs): only the white pieces that are
    bodies (reaching the bottom) or heads (about 125 px each) stay with their outlines, the gear and the flags go;
    returns a 128 px RGBA layer."""
    im = np.array(Image.open(os.path.join(REFS, name)).convert("RGBA")).astype(np.int32)
    h, w = im.shape[:2]
    white = (im[..., 3] > 100) & (im[..., :3].mean(-1) > 140)
    label = np.zeros((h, w), np.int32)
    keep = np.zeros((h, w), bool)
    drop = np.zeros((h, w), bool)
    n = 0
    for y0 in range(h):
        for x0 in range(w):
            if not white[y0, x0] or label[y0, x0]:
                continue
            n += 1
            q, pts = deque([(y0, x0)]), []
            label[y0, x0] = n
            while q:
                y, x = q.popleft()
                pts.append((y, x))
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < h and 0 <= xx < w and white[yy, xx] and not label[yy, xx]:
                        label[yy, xx] = n
                        q.append((yy, xx))
            if max(p[0] for p in pts) > 80 or 100 < len(pts) < 150:
                for y, x in pts:
                    keep[y, x] = True
            else:
                for y, x in pts:
                    drop[y, x] = True
    def grow(m, k):
        return np.array(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(k))) > 0
    # the outline of a kept piece, minus the edge of a dropped one where they touch
    near = grow(keep, 11) & ~(grow(drop, 5) & ~keep)
    out = im.copy()
    out[..., 3] = np.where(near, im[..., 3], 0)
    layer = Image.fromarray(out.astype(np.uint8), "RGBA")
    canvas = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    canvas.alpha_composite(layer, (4, 8))
    return canvas


def figure(ic, x, y, hands, fill=WHITE):
    """A vanilla-like stick figure: head at (x, y), torso and legs under it, arms from the shoulders to the hands."""
    sh = y + 13
    def body(d, s):
        for hx, hy, side in hands:
            d.line([((x + side * 7) * s, (sh + 3) * s), (hx * s, hy * s)], fill=255, width=int(8 * s))
            d.ellipse([(hx - 4.5) * s, (hy - 4.5) * s, (hx + 4.5) * s, (hy + 4.5) * s], fill=255)
        d.rounded_rectangle([(x - 9) * s, (sh - 1) * s, (x + 9) * s, (sh + 22) * s], 7 * s, fill=255)
        for sx in (-4.5, 4.5):
            d.rounded_rectangle([(x + sx - 4.5) * s, (sh + 14) * s, (x + sx + 4.5) * s, (sh + 36) * s], 4 * s, fill=255)
    ic.piece(body, fill)
    ic.detail(lambda d, s: d.line([(x * s, (sh + 23) * s), (x * s, (sh + 36) * s)], fill=BLACK, width=int(3 * s)))
    ic.piece(lambda d, s: d.ellipse([(x - 9) * s, (y - 9) * s, (x + 9) * s, (y + 9) * s], fill=255), fill)


def chip(ic, cx, cy, r):
    def pins(d, s):
        for k in range(-2, 3):
            o = k * r * 0.36
            for a, b, c, e in ((cx + o, cy - r - 9, cx + o, cy + r + 9), (cx - r - 9, cy + o, cx + r + 9, cy + o)):
                d.line([(a * s, b * s), (c * s, e * s)], fill=255, width=int(4 * s))
    ic.piece(pins)
    ic.piece(lambda d, s: d.rounded_rectangle([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], 4 * s, fill=255))
    ic.piece(lambda d, s: d.rectangle([(cx - r * 0.42) * s, (cy - r * 0.42) * s, (cx + r * 0.42) * s, (cy + r * 0.42) * s], fill=255), GREY, 3)


def litany():
    """Litany of data: the two figures of the vanilla gear ritual lift the AI core, a chip, over their heads."""
    ic = Icon()
    chip(ic, 64, 32, 20)
    ic.save("Ritual_Litany", ref_figures("gear.png"))


def trace_cleansing():
    """Trace cleansing: the figures of the vanilla flag ritual hold up a terminal whose screen shows a crossed-out eye."""
    ic = Icon()
    x0, y0, x1, y1 = 36, 8, 92, 56
    ic.piece(lambda d, s: d.rounded_rectangle([x0 * s, y0 * s, x1 * s, y1 * s], 6 * s, fill=255))
    ic.piece(lambda d, s: d.rounded_rectangle([(x0 + 7) * s, (y0 + 7) * s, (x1 - 7) * s, (y1 - 9) * s], 3 * s, fill=255), GREY, 3)
    ex, ey = (x0 + x1) / 2, (y0 + y1) / 2 - 1
    def eye(d, s):
        d.ellipse([(ex - 13) * s, (ey - 7) * s, (ex + 13) * s, (ey + 7) * s], outline=BLACK, width=int(3 * s))
        d.ellipse([(ex - 4) * s, (ey - 4) * s, (ex + 4) * s, (ey + 4) * s], fill=BLACK)
        d.line([((ex - 12) * s, (ey + 10) * s), ((ex + 12) * s, (ey - 10) * s)], fill=BLACK, width=int(4 * s))
    ic.detail(eye)
    ic.save("Ritual_TraceCleansing", ref_figures("flags.png"))


def net_archon():
    """The Net Archon: a bust with a visor and a halo of data nodes."""
    ic = Icon()
    hx, hy = 62, 60
    # the halo: an arc of nodes over the head, joined by a thin grey line
    import math
    pts = [(hx + math.cos(math.radians(a)) * 40, hy - 8 + math.sin(math.radians(a)) * 36) for a in range(198, 346, 24)]
    ic.piece(lambda d, s: d.line([(x * s, y * s) for x, y in pts], fill=255, width=int(4 * s), joint="curve"), GREY, 3.5)
    for i, (x, y) in enumerate(pts):
        r = 6 if i % 2 == 0 else 4.5
        ic.piece(lambda d, s, x=x, y=y, r=r: d.rectangle([(x - r) * s, (y - r) * s, (x + r) * s, (y + r) * s], fill=255), WHITE if i % 2 == 0 else GREY, 3.5)
    def bust(d, s):
        d.rounded_rectangle([(hx - 38) * s, (hy + 30) * s, (hx + 38) * s, (hy + 100) * s], 24 * s, fill=255)
        d.rectangle([(hx - 7) * s, (hy + 14) * s, (hx + 7) * s, (hy + 34) * s], fill=255)
    ic.piece(bust)
    ic.piece(lambda d, s: d.polygon([((hx - 38) * s, (hy + 56) * s), ((hx - 14) * s, (hy + 32) * s), ((hx + 14) * s, (hy + 32) * s),
                                     ((hx + 38) * s, (hy + 56) * s), ((hx + 38) * s, 130 * s), ((hx - 38) * s, 130 * s)], fill=255), GREY)
    ic.detail(lambda d, s: d.line([(hx * s, (hy + 32) * s), (hx * s, 128 * s)], fill=BLACK, width=int(3 * s)))
    ic.piece(lambda d, s: d.ellipse([(hx - 18) * s, (hy - 20) * s, (hx + 18) * s, (hy + 20) * s], fill=255))
    ic.piece(lambda d, s: d.rounded_rectangle([(hx - 20) * s, (hy - 6) * s, (hx + 20) * s, (hy + 4) * s], 3 * s, fill=255), GREY, 3)
    ic.save("Role_NetArchon")


if __name__ == "__main__":
    litany()
    trace_cleansing()
    net_archon()
    print("icons written")
