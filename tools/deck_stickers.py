"""
Die-cut vinyl stickers for the street deck themes (tools/gen_deck_street*.py): flat bright art in a white border with
a soft shadow, a little grime and a gloss. Everything is drawn from simple shapes, seeded, so a rerun gives the same
files. Each function returns an RGBA PIL image at its game size.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

import deck_art as A

PINK = (255, 61, 154)
CYAN = (45, 226, 230)
YELLOW = (255, 210, 63)
PURPLE = (138, 77, 255)
RED = (226, 52, 72)
TEAL = (31, 191, 166)
INK = (18, 12, 20)
BONE = (236, 226, 200)
WHITE = (245, 242, 235)

KANJI_FONT = "C:/Windows/Fonts/YuGothB.ttc"


def jfont(size):
    try:
        return ImageFont.truetype(KANJI_FONT, size)
    except OSError:
        return ImageFont.truetype("msgothic.ttc", size)


def impact(size):
    try:
        return ImageFont.truetype("impact.ttf", size)
    except OSError:
        return A.font(size)


class Big:
    """A canvas drawn at SS times the size and shrunk on finish (antialiased flat art)."""

    def __init__(self, w, h):
        self.w, self.h, self.s = w, h, A.SS
        self.img = Image.new("RGBA", (w * self.s, h * self.s), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def P(self, pts):
        return [(x * self.s, y * self.s) for x, y in pts]

    def poly(self, pts, fill, outline=None, width=0):
        self.d.polygon(self.P(pts), fill=fill)
        if outline:
            self.d.line(self.P(pts + pts[:1]), fill=outline, width=int(width * self.s), joint="curve")

    def ellipse(self, box, fill, outline=None, width=0):
        x0, y0, x1, y1 = box
        self.d.ellipse([x0 * self.s, y0 * self.s, x1 * self.s, y1 * self.s], fill=fill, outline=outline, width=int(width * self.s))

    def rect(self, box, fill, r=0, outline=None, width=0):
        x0, y0, x1, y1 = box
        self.d.rounded_rectangle([x0 * self.s, y0 * self.s, x1 * self.s, y1 * self.s], r * self.s, fill=fill, outline=outline, width=int(width * self.s))

    def line(self, pts, fill, width):
        self.d.line(self.P(pts), fill=fill, width=max(1, int(width * self.s)), joint="curve")

    def text(self, xy, txt, fnt_fn, size, fill, anchor="mm", stroke=0, stroke_fill=None):
        self.d.text((xy[0] * self.s, xy[1] * self.s), txt, font=fnt_fn(size * self.s), fill=fill, anchor=anchor,
                    stroke_width=int(stroke * self.s), stroke_fill=stroke_fill)

    def done(self):
        return self.img.resize((self.w, self.h), Image.LANCZOS)


def hsv2rgb(h, s, v):
    i = np.floor(h * 6).astype(int) % 6
    f = h * 6 - np.floor(h * 6)
    p, q, t = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    r = np.choose(i, [v, q, p, p, t, v])
    g = np.choose(i, [t, v, v, q, p, p])
    b = np.choose(i, [p, p, t, v, v, q])
    return np.dstack([r, g, b]) * 255


def holo_rgb(w, h, seed, sat=0.42, val=0.97):
    """A holographic foil: hue swirls with the angle, the distance and some noise."""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    cx, cy = w / 2, h / 2
    ang = np.arctan2(yy - cy, xx - cx) / (2 * math.pi)
    rad = np.hypot(xx - cx, yy - cy) / max(w, h)
    hue = (ang * 1.0 + rad * 1.6 + A.fbm(w, h, max(6, w / 5), 3, seed) * 0.9 + (xx + yy) / (w + h) * 0.7) % 1.0
    rgb = hsv2rgb(hue, np.full_like(hue, sat), np.full_like(hue, val))
    rgb += (np.sin((xx * 0.9 + yy * 0.6) * 0.5) * 6)[..., None]
    return rgb


def die_cut(art, border=3, shadow=0.55, grime=0.5, seed=0, gloss=True):
    """Puts flat art into a white die-cut border with a soft shadow; pads the canvas so nothing is clipped."""
    pad = border + 4
    W, H = art.width + pad * 2, art.height + pad * 2
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    canvas.alpha_composite(art, dest=(pad, pad))
    a = canvas.getchannel("A")
    cut = a.point(lambda v: 255 if v > 40 else 0).filter(ImageFilter.MaxFilter(border * 2 + 1)).filter(ImageFilter.GaussianBlur(0.7))
    m = np.asarray(cut, dtype=np.float32) / 255.0
    rgb = np.broadcast_to(np.array(WHITE, np.float32), (H, W, 3)).copy()
    rgb += A.grain(W, H, seed + 1, 3)[..., None]
    rgb += (A.shade(m, 1.0) * 30)[..., None]
    if grime:
        dirt = np.clip(A.fbm(W, H, 10, 3, seed + 2) * 2.5 + 0.1, 0, 1)
        rgb -= (dirt * 40 * grime)[..., None] * np.array([1.0, 1.0, 1.1], np.float32)
    sh = np.roll(np.roll(A.blur(m, 1.8), 2, 0), 1, 1) * shadow
    out = A.to_img(np.zeros((H, W, 3), np.float32), sh)
    out.alpha_composite(A.to_img(rgb, m))
    out.alpha_composite(canvas)
    if gloss:
        yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
        band = np.clip(1 - np.abs((xx + yy * 0.8) - (W + H) * 0.35) / (W * 0.18 + 2), 0, 1) * 0.12 * m
        out.alpha_composite(A.to_img(np.full((H, W, 3), 255, np.float32), band))
    if grime:
        # scuffs through the print: a few pale specks
        arr = np.asarray(out, dtype=np.float32).copy()
        n = A.rng(seed + 3).random((H, W))
        scuff = (n > 0.994) * m * 0.35
        arr[..., :3] = arr[..., :3] * (1 - scuff[..., None]) + np.array(WHITE, np.float32) * scuff[..., None]
        out = Image.fromarray(np.clip(arr, 0, 255).astype("uint8"), "RGBA")
    return out


# ------------------------------------------------------------------ the art

def bez(p0, c0, c1, p1, n=18):
    """Points along a cubic Bezier curve (p0 excluded, p1 included)."""
    out = []
    for i in range(1, n + 1):
        t = i / n
        u = 1 - t
        out.append((u ** 3 * p0[0] + 3 * u * u * t * c0[0] + 3 * u * t * t * c1[0] + t ** 3 * p1[0],
                    u ** 3 * p0[1] + 3 * u * u * t * c0[1] + 3 * u * t * t * c1[1] + t ** 3 * p1[1]))
    return out


def path(start, *segs):
    """A closed outline from a start point and Bezier segments (c0, c1, p1); straight lines as (p1,)."""
    pts, cur = [start], start
    for seg in segs:
        if len(seg) == 1:
            pts.append(seg[0])
        elif len(seg) == 2:
            pts += bez(cur, seg[0], seg[0], seg[1])
        else:
            pts += bez(cur, seg[0], seg[1], seg[2])
        cur = pts[-1]
    return pts


def mirror(pts, cx=50):
    return [(2 * cx - x, y) for x, y in pts]


def sym(half):
    """A symmetric outline from its right half drawn top-centre to bottom-centre."""
    return half + mirror(half[::-1])[1:-1]


def kitsune(w, holo=False, seed=0):
    """A kitsune (fox) mask in flat cel style: tall ears, cheek tufts, slanted eyes with red liner, red and gold
    markings. Drawn in a 100 x 110 unit box, the right half mirrored."""
    h = int(w * 1.1)
    k = w / 100.0
    b = Big(w, h)
    U = lambda pts: [(x * k, y * k) for x, y in pts]
    lw = max(1.3, w / 44)
    face_c, shade_c = ((244, 239, 230), (196, 190, 214)) if not holo else ((255, 255, 255), (205, 200, 235))
    mark = RED

    half = path((50, 27), ((56, 25), (61, 23), (64, 20)), ((72, 12), (80, 5), (86, 1)), ((89, 12), (91, 24), (90, 36)),
                ((92, 44), (94, 52), (99, 63)), ((92, 64), (88, 66), (85, 68)), ((89, 72), (91, 75), (93, 79)),
                ((84, 80), (78, 81), (73, 83)), ((66, 92), (59, 101), (50, 106)))
    face = sym(half)
    b.poly(U(face), face_c, INK, lw * 1.1)
    # cel shading down the outer cheeks and along the muzzle
    for s in (1, -1):
        X = (lambda x: x) if s == 1 else (lambda x: 100 - x)
        M = lambda pts: U([(X(x), y) for x, y in pts])
        b.poly(M(path((90, 36), ((92, 44), (94, 52), (99, 63)), ((92, 64),), ((88, 54), (86, 44), (84, 36)))), shade_c)
        b.poly(M(path((73, 83), ((66, 92), (59, 101), (50, 106)), ((50, 100),), ((58, 96), (64, 90), (70, 82)))), shade_c)
        # inner ear
        b.poly(M(path((67, 21), ((74, 14), (80, 9), (84, 6)), ((86, 14), (87, 22), (86, 30)), ((78, 26), (72, 23), (67, 21)))), mark)
        b.poly(M([(72, 21), (83, 11), (84, 26)]), (255, 120, 150))
        # eye: a black slit in a red sweep that flicks up to the ear
        b.poly(M(path((56, 53), ((64, 47), (74, 43), (84, 36)), ((80, 46), (70, 54), (58, 56)))), mark)
        b.poly(M(path((58, 53), ((65, 49), (72, 47), (78, 44)), ((72, 51), (64, 54), (58, 53)))), INK)
        b.poly(M([(66, 50), (69, 49), (68, 51)]), YELLOW)
        # cheek markings: three red flames
        for i, (x0, y0) in enumerate(((66, 64), (68, 70), (66, 76))):
            b.poly(M(path((x0, y0), ((x0 + 6, y0 - 4), (x0 + 12, y0 - 4), (x0 + 17, y0 - 2 - i)), ((x0 + 11, y0 + 1), (x0 + 5, y0 + 2), (x0, y0)))), mark)
        # brow swirl
        b.poly(M(path((54, 42), ((57, 35), (63, 31), (70, 31)), ((65, 34), (60, 38), (57, 44)))), mark)
    # forehead: a gold flame jewel
    b.poly(U(path((50, 31), ((54, 36), (55, 40), (50, 45)), ((45, 40), (46, 36), (50, 31)))), YELLOW, INK, lw * 0.6)
    b.poly(U([(50, 35), (52, 40), (50, 43), (48, 40)]), (255, 240, 170))
    # nose, mouth
    b.poly(U(path((45, 97), ((48, 95), (52, 95), (55, 97)), ((54, 101), (51, 103), (50, 103)), ((49, 103), (46, 101), (45, 97)))), INK)
    b.line(U([(50, 103), (50, 106)]), INK, lw * 0.6)
    b.line(U(bez((42, 94), (45, 100), (48, 101), (50, 100), 8)), mark, lw * 0.7)
    b.line(U(bez((58, 94), (55, 100), (52, 101), (50, 100), 8)), mark, lw * 0.7)

    art = b.done()
    if holo:
        fa = np.asarray(art, dtype=np.float32).copy()
        hrgb = holo_rgb(w, h, seed + 11, sat=0.4)
        r, g, bl = fa[..., 0], fa[..., 1], fa[..., 2]
        pale = np.clip((np.minimum(np.minimum(r, g), bl) - 170) / 50, 0, 1)
        lum = (r + g + bl) / (3 * 255)
        fa[..., :3] = fa[..., :3] * (1 - pale[..., None]) + hrgb * (0.6 + 0.4 * lum)[..., None] * pale[..., None]
        art = Image.fromarray(np.clip(fa, 0, 255).astype("uint8"), "RGBA")
    return die_cut(art, border=max(2, w // 24), seed=seed)


def pixel_skull(n, seed=0, eyes=PINK):
    """A pixel skull, white on a black rounded square."""
    grid = [".XXXXXXX.",
            "XXXXXXXXX",
            "XX..X..XX",
            "XX..X..XX",
            "XXXXXXXXX",
            ".XXX.XXX.",
            "..XXXXX..",
            "..X.X.X.."]
    b = Big(n, n)
    b.rect((0, 0, n - 1, n - 1), INK, r=n * 0.18)
    cell = (n - 8) / 9
    ox, oy = 4, 4 + (n - 8 - cell * 8) / 2
    for y, row in enumerate(grid):
        for x, ch in enumerate(row):
            if ch == "X":
                b.rect((ox + x * cell, oy + y * cell, ox + (x + 1) * cell - 0.3, oy + (y + 1) * cell - 0.3), (240, 240, 240))
    for x in (2, 5):
        b.rect((ox + x * cell + cell * 0.3, oy + 2 * cell + cell * 0.6, ox + x * cell + cell * 1.7, oy + 4 * cell - 0.3 - cell * 0.6), eyes)
    return die_cut(b.done(), border=2, seed=seed)


def vr_skull(w, seed=0):
    """A red skull in a blue VR visor over a teal triangle."""
    h = int(w * 1.05)
    b = Big(w, h)
    b.poly([(0.0, 0.2 * h), (w, 0.2 * h), (w * 0.5, h)], TEAL)
    # the skull: cranium, cheekbones, eye sockets below the visor, a grinning jaw
    b.ellipse((w * 0.2, h * 0.1, w * 0.8, h * 0.66), RED, INK, 1.4)
    b.rect((w * 0.3, h * 0.56, w * 0.7, h * 0.84), RED, r=w * 0.06, outline=INK, width=1.4)
    b.ellipse((w * 0.3, h * 0.44, w * 0.45, h * 0.56), INK)
    b.ellipse((w * 0.55, h * 0.44, w * 0.7, h * 0.56), INK)
    b.poly([(w * 0.46, h * 0.64), (w * 0.5, h * 0.57), (w * 0.54, h * 0.64)], INK)
    b.rect((w * 0.34, h * 0.68, w * 0.66, h * 0.78), (250, 240, 220), r=1)
    for x in (0.4, 0.46, 0.52, 0.58):
        b.line([(w * x, h * 0.68), (w * x, h * 0.78)], INK, 1)
    # the VR visor across the forehead, with a strap and a lens strip
    b.rect((w * 0.14, h * 0.2, w * 0.86, h * 0.38), (64, 110, 220), r=w * 0.04, outline=INK, width=1.4)
    b.rect((w * 0.2, h * 0.24, w * 0.8, h * 0.28), (160, 200, 255), r=w * 0.02)
    for x in (0.26, 0.33, 0.4):
        b.ellipse((w * x, h * 0.31, w * x + w * 0.04, h * 0.35), INK)
    b.rect((w * 0.3, h * 0.06, w * 0.7, h * 0.2), (50, 90, 190), r=w * 0.03, outline=INK, width=1.2)
    for k, x in enumerate((0.36, 0.45, 0.55, 0.64)):
        b.line([(w * x, h * 0.84), (w * (x + (k - 1.5) * 0.03), h * 0.95)], RED, 2)
    return die_cut(b.done(), border=3, seed=seed)


def holo_badge(d, text="HIGH TECH \u00b7 LOW LIFE \u00b7 NIGHT RUNNER \u00b7 ", seed=0):
    """A round holographic sticker: foil, lettering around the rim, an eye in the middle."""
    s = A.SS
    big = Image.new("RGBA", (d * s, d * s), (0, 0, 0, 0))
    m = A.mask((d, d), lambda dr, ss: dr.ellipse([0, 0, d * ss - 1, d * ss - 1], fill=255))
    foil = A.to_img(holo_rgb(d, d, seed + 5), m).resize((d * s, d * s), Image.LANCZOS)
    big.alpha_composite(foil)
    dr = ImageDraw.Draw(big)
    c = d * s / 2
    ring = (c * 0.78, c * 0.98)
    dr.ellipse([c - ring[1] + s, c - ring[1] + s, c + ring[1] - s, c + ring[1] - s], outline=(90, 40, 110, 200), width=s)
    dr.ellipse([c - ring[0], c - ring[0], c + ring[0], c + ring[0]], outline=(90, 40, 110, 200), width=s)
    # lettering around the rim
    f = A.font(max(6, int(d * 0.11)) * s)
    R = (ring[0] + ring[1]) / 2
    step = 2 * math.pi / len(text)
    for i, ch in enumerate(text):
        th = -math.pi / 2 + i * step
        g = Image.new("RGBA", (f.size * 2, f.size * 2), (0, 0, 0, 0))
        ImageDraw.Draw(g).text((f.size, f.size), ch, font=f, fill=(80, 30, 100, 235), anchor="mm")
        g = g.rotate(-math.degrees(th) - 90, resample=Image.BICUBIC)
        big.alpha_composite(g, dest=(int(c + R * math.cos(th) - f.size), int(c + R * math.sin(th) - f.size)))
    # the eye
    ew, eh = ring[0] * 1.15, ring[0] * 0.62
    dr.polygon([(c - ew, c), (c - ew * 0.5, c - eh), (c + ew * 0.5, c - eh), (c + ew, c), (c + ew * 0.5, c + eh), (c - ew * 0.5, c + eh)],
               fill=(20, 14, 30, 255))
    dr.ellipse([c - eh * 0.85, c - eh * 0.85, c + eh * 0.85, c + eh * 0.85], fill=CYAN)
    dr.ellipse([c - eh * 0.4, c - eh * 0.4, c + eh * 0.4, c + eh * 0.4], fill=INK)
    dr.ellipse([c - eh * 0.55, c - eh * 0.6, c - eh * 0.25, c - eh * 0.3], fill=(255, 255, 255, 230))
    dr.line([(c - ew * 0.5, c - eh), (c - ew * 0.65, c - eh * 1.35)], fill=PINK, width=2 * s)
    dr.line([(c, c - eh), (c, c - eh * 1.4)], fill=PINK, width=2 * s)
    dr.line([(c + ew * 0.5, c - eh), (c + ew * 0.65, c - eh * 1.35)], fill=PINK, width=2 * s)
    return die_cut(big.resize((d, d), Image.LANCZOS), border=2, seed=seed, grime=0.35)


def kanji_strip(text, w, h, paper=PINK, ink=INK, vertical=True, seed=0):
    """A paper label with kanji, slightly crooked edges, no die-cut border (it is printed paper)."""
    b = Big(w, h)
    r = A.rng(seed)
    j = lambda: r.uniform(-0.8, 0.8)
    b.poly([(0 + j(), 0 + j()), (w - 1 + j(), 0 + j()), (w - 1 + j(), h - 1 + j()), (0 + j(), h - 1 + j())], paper)
    if vertical:
        step = (h - 8) / len(text)
        for i, ch in enumerate(text):
            b.text((w / 2, 4 + step * (i + 0.5)), ch, jfont, min(w - 6, step - 1), ink)
    else:
        b.text((w / 2, h / 2), text, jfont, h - 7, ink)
    img = b.done()
    a = np.asarray(img, dtype=np.float32).copy()
    a[..., :3] += A.grain(w, h, seed + 1, 5)[..., None]
    a[..., :3] -= (np.clip(A.fbm(w, h, 8, 2, seed + 2) * 2 + 0.2, 0, 1) * 30)[..., None]
    img = Image.fromarray(np.clip(a, 0, 255).astype("uint8"), "RGBA")
    out = Image.new("RGBA", (w + 6, h + 6), (0, 0, 0, 0))
    sh = np.roll(np.roll(A.blur(np.pad(a[..., 3] / 255, 3), 1.5), 2, 0), 1, 1) * 0.5
    out.alpha_composite(A.to_img(np.zeros((h + 6, w + 6, 3), np.float32), sh))
    out.alpha_composite(img, dest=(3, 3))
    return out


def text_sticker(text, size, fill, offset_col, seed=0, fnt=None, pad=4):
    """Bold lettering with a coloured drop offset, die-cut (a street tag)."""
    fnt = fnt or impact
    f = fnt(size)
    bbox = f.getbbox(text)
    w, h = bbox[2] - bbox[0] + pad * 2 + 3, bbox[3] - bbox[1] + pad * 2 + 3
    b = Big(w, h)
    x, y = pad - bbox[0], pad - bbox[1]
    b.text((x + 2.5, y + 2.5), text, fnt, size, offset_col, anchor="la", stroke=1, stroke_fill=INK)
    b.text((x, y), text, fnt, size, fill, anchor="la", stroke=1, stroke_fill=INK)
    return die_cut(b.done(), border=3, seed=seed)


def smiley(d, color, mood, seed=0):
    """A round badge face: 0 happy wink, 1 grumpy, 2 shocked."""
    b = Big(d, d)
    b.ellipse((0, 0, d - 1, d - 1), color, INK, 1.2)
    lw = max(1.2, d / 14)
    if mood == 0:
        b.line([(d * 0.3, d * 0.38), (d * 0.42, d * 0.42)], INK, lw)
        b.ellipse((d * 0.6, d * 0.33, d * 0.7, d * 0.45), INK)
        b.line([(d * 0.28, d * 0.6), (d * 0.5, d * 0.74), (d * 0.72, d * 0.6)], INK, lw)
    elif mood == 1:
        b.line([(d * 0.28, d * 0.34), (d * 0.42, d * 0.42)], INK, lw)
        b.line([(d * 0.72, d * 0.34), (d * 0.58, d * 0.42)], INK, lw)
        b.line([(d * 0.32, d * 0.7), (d * 0.5, d * 0.62), (d * 0.68, d * 0.7)], INK, lw)
    else:
        b.ellipse((d * 0.28, d * 0.32, d * 0.4, d * 0.46), INK)
        b.ellipse((d * 0.6, d * 0.32, d * 0.72, d * 0.46), INK)
        b.ellipse((d * 0.42, d * 0.58, d * 0.58, d * 0.76), INK)
    return b.done()


def smiley_column(d, seed=0):
    col = Image.new("RGBA", (d, d * 3 + 4), (0, 0, 0, 0))
    for i, (c, mood) in enumerate(((PINK, 2), ((70, 120, 235), 1), (CYAN, 0))):
        col.alpha_composite(smiley(d, c, mood, seed + i), dest=(0, i * (d + 2)))
    return die_cut(col, border=2, seed=seed)


def bolt(w, seed=0):
    h = int(w * 1.5)
    b = Big(w, h)
    pts = [(0.62, 0.0), (0.1, 0.56), (0.44, 0.56), (0.3, 1.0), (0.92, 0.38), (0.56, 0.38), (0.78, 0.0)]
    b.poly([(x * w + 2, y * h * 0.96 + 2) for x, y in pts], PINK)
    b.poly([(x * w - 1, y * h * 0.96) for x, y in pts], YELLOW, INK, 1.2)
    return die_cut(b.done(), border=2, seed=seed)


def pill(w, seed=0):
    h = int(w * 0.5)
    b = Big(w, h)
    b.rect((1, 1, w - 2, h - 2), (70, 110, 230), r=h / 2 - 1, outline=INK, width=1.2)
    b.d.rectangle([w / 2 * b.s, 2 * b.s, (w - 2) * b.s, (h - 2) * b.s], fill=(0, 0, 0, 0))
    b.rect((w / 2 - h / 2, 1, w - 2, h - 2), RED, r=h / 2 - 1, outline=INK, width=1.2)
    b.rect((1, 1, w / 2 + 1, h - 2), (70, 110, 230), r=h / 2 - 1, outline=INK, width=1.2)
    b.rect((w * 0.12, h * 0.25, w * 0.4, h * 0.38), (200, 220, 255), r=2)
    return die_cut(b.done(), border=2, seed=seed)


def barcode(w, h, label="NR-0451", seed=0):
    b = Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (240, 236, 226), r=2)
    r = A.rng(seed)
    x = 4
    while x < w - 5:
        bw = r.choice([0.8, 1.2, 2.0])
        b.d.rectangle([x * b.s, 3 * b.s, (x + bw) * b.s, (h - 9) * b.s], fill=INK)
        x += bw + r.choice([0.8, 1.2, 1.8])
    b.text((w / 2, h - 4.5), label, A.font, 6, INK)
    img = b.done()
    out = Image.new("RGBA", (w + 4, h + 4), (0, 0, 0, 0))
    sh = np.roll(A.blur(np.pad(np.asarray(img)[..., 3] / 255.0, 2), 1.2), 1, 0) * 0.5
    out.alpha_composite(A.to_img(np.zeros((h + 4, w + 4, 3), np.float32), sh))
    out.alpha_composite(img, dest=(2, 2))
    return out


def tape(w, h, color=(150, 150, 146), alpha=0.82, seed=0):
    """A strip of duct tape with torn ends, weave and a sheen."""
    r = A.rng(seed)
    top = [(0, 0)] + [(x, r.uniform(0, 1.5)) for x in np.linspace(3, w - 3, 8)] + [(w, 0)]

    def draw(d, s):
        left = [(r.uniform(0, 3.5), y) for y in np.linspace(0, h, 7)]
        right = [(w - r.uniform(0, 3.5), y) for y in np.linspace(h, 0, 7)]
        d.polygon([(x * s, y * s) for x, y in left + right], fill=255)
    m = A.mask((w, h), draw)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    rgb = np.broadcast_to(np.array(color, np.float32), (h, w, 3)).copy()
    rgb += ((np.sin(xx * 2.4) * np.sin(yy * 2.4)) * 5)[..., None]
    rgb += A.fbm(w, h, 12, 2, seed + 1)[..., None] * 20
    rgb += (np.clip(1 - np.abs(yy - h * 0.3) / (h * 0.25), 0, 1) * 18)[..., None]
    rgb -= (np.clip(A.fbm(w, h, 6, 2, seed + 2) * 3, 0, 1) * 30)[..., None]
    out = Image.new("RGBA", (w + 4, h + 4), (0, 0, 0, 0))
    sh = np.roll(A.blur(np.pad(m, 2), 1.2), 1, 0) * 0.45
    out.alpha_composite(A.to_img(np.zeros((h + 4, w + 4, 3), np.float32), sh))
    out.alpha_composite(A.to_img(rgb, m * alpha), dest=(2, 2))
    return out


# ------------------------------------------------------------------ spray paint

def graffiti(text, size, fill, outline, seed=0, skew=0.22, angle=-4, bubble=False, drips=5):
    """A spray-painted tag: skewed letters, an outline, soft overspray, speckles and paint drips running down."""
    r = A.rng(seed)
    f = impact(size * A.SS)
    stroke = max(2, size // (5 if bubble else 9)) * A.SS
    l, t, rr, b = f.getbbox(text, stroke_width=stroke)
    pad = 10 * A.SS
    W, H = rr - l + pad * 2 + int(size * skew * A.SS), b - t + pad * 2 + 18 * A.SS

    def layer(st):
        m = Image.new("L", (W, H), 0)
        ImageDraw.Draw(m).text((pad - l + int(size * skew * A.SS), pad - t), text, font=f, fill=255, stroke_width=st, stroke_fill=255)
        m = m.transform((W, H), Image.AFFINE, (1, skew, -skew * H * 0.5, 0, 1, 0), resample=Image.BICUBIC)
        return m.rotate(angle, resample=Image.BICUBIC)
    w, h = W // A.SS, H // A.SS
    m_fill = np.asarray(layer(0).resize((w, h), Image.LANCZOS), dtype=np.float32) / 255
    m_out = np.asarray(layer(stroke).resize((w, h), Image.LANCZOS), dtype=np.float32) / 255
    # drips from the bottom of the outline
    drip = np.zeros((h, w), np.float32)
    dimg = Image.new("L", (w * A.SS, h * A.SS), 0)
    dd = ImageDraw.Draw(dimg)
    cols = np.where(m_out.max(axis=0) > 0.5)[0]
    for _ in range(drips if len(cols) else 0):
        x = int(r.choice(cols))
        ys = np.where(m_out[:, x] > 0.5)[0]
        y0, ln, wd = ys.max(), r.uniform(3, 13), r.uniform(1.0, 2.0)
        dd.line([x * A.SS, y0 * A.SS, x * A.SS, (y0 + ln) * A.SS], fill=255, width=int(wd * A.SS))
        dd.ellipse([(x - wd * 0.9) * A.SS, (y0 + ln - wd * 0.9) * A.SS, (x + wd * 0.9) * A.SS, (y0 + ln + wd * 0.9) * A.SS], fill=255)
    drip = np.asarray(dimg.resize((w, h), Image.LANCZOS), dtype=np.float32) / 255
    out_m = np.clip(m_out + drip, 0, 1)
    speck = A.rng(seed + 1).random((h, w))
    over_m = np.clip(A.blur(out_m, 2.2) * 0.6 - out_m, 0, 1) * (speck > 0.55)
    rough = 0.82 + 0.18 * np.clip(A.fbm(w, h, 6, 2, seed + 2) * 2 + 0.5, 0, 1)
    img = A.to_img(np.broadcast_to(np.array(outline, np.float32), (h, w, 3)).copy(), over_m * 0.5)
    img.alpha_composite(A.to_img(np.broadcast_to(np.array(outline, np.float32), (h, w, 3)).copy(), out_m * rough))
    frgb = np.broadcast_to(np.array(fill, np.float32), (h, w, 3)).copy()
    if bubble:
        # a highlight streak along the top of the letters
        hl = np.clip(m_fill - np.roll(m_fill, 3, 0), 0, 1)
        frgb += hl[..., None] * 90
    img.alpha_composite(A.to_img(frgb, m_fill * rough))
    return img


def slashes(w=40, h=56, color=INK, seed=0):
    """Three slanted painted stripes, slightly worn."""
    def draw(d, s):
        for i in range(3):
            y = 2 + i * (h - 4) / 3
            d.polygon([(0, (y + 10) * s), (w * s, y * s), (w * s, (y + 10) * s), (0, (y + 20) * s)], fill=255)
    m = A.mask((w, h + 12), draw)
    m *= 0.9 + 0.1 * np.clip(A.fbm(w, h + 12, 5, 2, seed) * 2 + 0.5, 0, 1)
    return A.to_img(np.broadcast_to(np.array(color, np.float32), (h + 12, w, 3)).copy(), m)
