"""Icons v2 in the vanilla manner (docs/ART_STYLE.md): white glyphs for factions and Ideology issues/roles,
muted illustrations for memes. Solid black outline around the whole shape, soft light from the top left,
thin inner lines, no glow.

Writes 128 and 256 px PNGs to "Hacking Expansion Netrun Art/Concepts10/IconsV2/" and the sheet
docs/ui-mockups/28-icons-v2.png. Optional reference: pass a folder with Vanilla Expanded icons as the first
argument to put them on the sheet for comparison (they are not copied into the repo).

Run from the repo root:  python tools/gen_icons_v2.py [path/to/ve/icons]
"""
import math
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from netrun_art import font  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Hacking Expansion Netrun Art", "Concepts10", "IconsV2")
MOCK = os.path.join(ROOT, "docs", "ui-mockups")

N = 512                 # working canvas (4x of 128)
OUTLINE = 19            # outer outline at 512 (~4.7 px at 128)
INNER = 9               # inner lines at 512 (~2.3 px at 128)

# Glyph palette (manner A): greys only
W_LIGHT, W_SHADE = (246, 246, 246), (212, 212, 212)
G_LIGHT, G_SHADE = (178, 178, 178), (136, 136, 136)
INK = (0, 0, 0)


# ---------------------------------------------------------------- masks

def blank():
    return Image.new("L", (N, N), 0)


def m_poly(pts):
    m = blank(); ImageDraw.Draw(m).polygon([(x * N, y * N) for x, y in pts], fill=255); return m


def m_ellipse(x0, y0, x1, y1):
    m = blank(); ImageDraw.Draw(m).ellipse((x0 * N, y0 * N, x1 * N, y1 * N), fill=255); return m


def m_rrect(x0, y0, x1, y1, r):
    m = blank(); ImageDraw.Draw(m).rounded_rectangle((x0 * N, y0 * N, x1 * N, y1 * N), radius=r * N, fill=255); return m


def m_line(pts, w):
    m = blank(); d = ImageDraw.Draw(m)
    P = [(x * N, y * N) for x, y in pts]
    d.line(P, fill=255, width=int(w * N), joint="curve")
    r = w * N / 2
    for x, y in (P[0], P[-1]):
        d.ellipse((x - r, y - r, x + r, y + r), fill=255)
    return m


def m_arc(x0, y0, x1, y1, a0, a1, w):
    m = blank(); ImageDraw.Draw(m).arc((x0 * N, y0 * N, x1 * N, y1 * N), a0, a1, fill=255, width=int(w * N)); return m


def union(*ms):
    out = blank()
    for m in ms:
        out = ImageChops.lighter(out, m)
    return out


def minus(a, b):
    return ImageChops.subtract(a, b)


def dilate(m, px):
    """Fast dilation: blur then threshold."""
    b = m.filter(ImageFilter.GaussianBlur(px / 2.0))
    return b.point(lambda v: 255 if v > 8 else 0)


def ring(m, px):
    return minus(dilate(m, px), m)


# ---------------------------------------------------------------- rendering

def gradient(light, shade):
    """Linear light from the top left to the bottom right."""
    small = Image.new("RGB", (32, 32))
    p = small.load()
    for y in range(32):
        for x in range(32):
            t = min(1.0, max(0.0, (x + y) / 62.0 * 1.15 - 0.08))
            p[x, y] = tuple(int(light[i] + (shade[i] - light[i]) * t) for i in range(3))
    return small.resize((N, N), Image.BICUBIC)


class Icon:
    def __init__(self):
        self.parts = []   # (mask, light, shade)
        self.lines = []   # inner black masks

    def part(self, mask, light=W_LIGHT, shade=W_SHADE):
        self.parts.append((mask, light, shade)); return mask

    def line(self, mask):
        self.lines.append(mask)

    def render(self, size):
        img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        allm = union(*[p[0] for p in self.parts])
        black = Image.new("RGBA", (N, N), INK + (255,))
        img.paste(black, (0, 0), dilate(allm, OUTLINE))
        for mask, light, shade in self.parts:
            # each part gets a thin edge so overlapping parts read apart
            img.paste(black, (0, 0), dilate(mask, INNER))
            img.paste(gradient(light, shade).convert("RGBA"), (0, 0), mask)
        for m in self.lines:
            img.paste(black, (0, 0), m)
        return img.resize((size, size), Image.LANCZOS)


def lw(px128):
    """Inner line width given in px at 128."""
    return px128 / 128.0


# ---------------------------------------------------------------- shared shapes

def skull(ic, cx, cy, s, light=W_LIGHT, shade=W_SHADE):
    head = m_ellipse(cx - 0.20 * s, cy - 0.24 * s, cx + 0.20 * s, cy + 0.12 * s)
    jaw = m_rrect(cx - 0.12 * s, cy + 0.02 * s, cx + 0.12 * s, cy + 0.22 * s, 0.03 * s)
    ic.part(union(head, jaw), light, shade)
    for dx in (-1, 1):
        ic.line(m_ellipse(cx + dx * 0.085 * s - 0.06 * s, cy - 0.08 * s, cx + dx * 0.085 * s + 0.06 * s, cy + 0.04 * s))
    ic.line(m_poly([(cx, cy + 0.04 * s), (cx - 0.03 * s, cy + 0.10 * s), (cx + 0.03 * s, cy + 0.10 * s)]))
    for k in range(-1, 2):
        ic.line(m_line([(cx + k * 0.05 * s, cy + 0.15 * s), (cx + k * 0.05 * s, cy + 0.22 * s)], lw(2) * s))


def plug(ic, x0, y0, x1, y1, w, light=W_LIGHT, shade=W_SHADE):
    """A cable from (x0,y0) to a jack at (x1,y1)."""
    ic.part(m_line([(x0, y0), (x1, y1)], w), G_LIGHT, G_SHADE)
    ang = math.atan2(y1 - y0, x1 - x0)
    hx, hy = math.cos(ang), math.sin(ang)
    L, Wd = 0.11, 0.065
    pts = [(x1 - hx * 0.02 + -hy * Wd / 2, y1 - hy * 0.02 + hx * Wd / 2), (x1 + hx * L + -hy * Wd / 2, y1 + hy * L + hx * Wd / 2),
           (x1 + hx * L + hy * Wd / 2, y1 + hy * L - hx * Wd / 2), (x1 - hx * 0.02 + hy * Wd / 2, y1 - hy * 0.02 - hx * Wd / 2)]
    ic.part(m_poly(pts), light, shade)
    for k in (-1, 1):
        px, py = x1 + hx * L + -hy * k * Wd * 0.25, y1 + hy * L + hx * k * Wd * 0.25
        ic.part(m_line([(px, py), (px + hx * 0.05, py + hy * 0.05)], 0.018), G_LIGHT, G_SHADE)


def chip(ic, cx, cy, h, light=W_LIGHT, shade=W_SHADE, pins=3):
    for side in range(4):
        for k in range(pins):
            t = (k - (pins - 1) / 2) * (2 * h / (pins + 0.5))
            if side == 0: ic.part(m_rrect(cx + t - 0.02, cy - h - 0.07, cx + t + 0.02, cy - h + 0.01, 0.01), G_LIGHT, G_SHADE)
            if side == 1: ic.part(m_rrect(cx + t - 0.02, cy + h - 0.01, cx + t + 0.02, cy + h + 0.07, 0.01), G_LIGHT, G_SHADE)
            if side == 2: ic.part(m_rrect(cx - h - 0.07, cy + t - 0.02, cx - h + 0.01, cy + t + 0.02, 0.01), G_LIGHT, G_SHADE)
            if side == 3: ic.part(m_rrect(cx + h - 0.01, cy + t - 0.02, cx + h + 0.07, cy + t + 0.02, 0.01), G_LIGHT, G_SHADE)
    ic.part(m_rrect(cx - h, cy - h, cx + h, cy + h, h * 0.18), light, shade)


# ---------------------------------------------------------------- runner crews (manner A)

def crew_skull_cables():
    ic = Icon()
    plug(ic, 0.30, 0.78, 0.13, 0.20, 0.06)
    plug(ic, 0.70, 0.78, 0.87, 0.20, 0.06)
    skull(ic, 0.5, 0.50, 1.25)
    ic.part(m_rrect(0.60, 0.33, 0.70, 0.40, 0.015), G_LIGHT, G_SHADE)   # temple port
    return ic


def crew_hood_visor():
    ic = Icon()
    hood = m_poly([(0.5, 0.12), (0.72, 0.26), (0.78, 0.55), (0.88, 0.88), (0.12, 0.88), (0.22, 0.55), (0.28, 0.26)])
    ic.part(hood)
    face = m_poly([(0.5, 0.26), (0.64, 0.36), (0.65, 0.58), (0.5, 0.68), (0.35, 0.58), (0.36, 0.36)])
    ic.part(face, G_LIGHT, G_SHADE)
    ic.line(m_poly([(0.37, 0.43), (0.63, 0.43), (0.61, 0.50), (0.39, 0.50)]))
    ic.line(m_line([(0.5, 0.70), (0.5, 0.86)], lw(2.5)))
    return ic


def crew_crow():
    ic = Icon()
    # raven head in profile with ragged neck feathers and a cyber eye
    neck = m_poly([(0.30, 0.50), (0.60, 0.56), (0.66, 0.70), (0.60, 0.74), (0.66, 0.84), (0.56, 0.82), (0.58, 0.92),
                   (0.46, 0.84), (0.40, 0.93), (0.36, 0.82), (0.26, 0.90), (0.26, 0.76), (0.16, 0.80), (0.22, 0.64)])
    head = m_ellipse(0.20, 0.18, 0.62, 0.62)
    ic.part(union(head, neck))
    beak = m_poly([(0.56, 0.30), (0.94, 0.44), (0.58, 0.52)])
    ic.part(beak, G_LIGHT, G_SHADE)
    ic.line(m_line([(0.58, 0.42), (0.90, 0.44)], lw(2)))
    ic.part(m_ellipse(0.38, 0.28, 0.52, 0.42), G_LIGHT, G_SHADE)
    ic.line(m_ellipse(0.42, 0.32, 0.48, 0.38))
    ic.line(m_line([(0.30, 0.44), (0.40, 0.44), (0.44, 0.40)], lw(2)))   # circuit under the eye
    ic.line(m_line([(0.28, 0.56), (0.42, 0.62)], lw(2)))
    return ic


def crew_fist_cable():
    ic = Icon()
    plug(ic, 0.18, 0.86, 0.62, 0.12, 0.06)
    fist = m_rrect(0.30, 0.38, 0.70, 0.74, 0.06)
    thumb = m_rrect(0.24, 0.46, 0.40, 0.66, 0.05)
    wrist = m_rrect(0.36, 0.70, 0.64, 0.90, 0.03)
    ic.part(union(fist, wrist))
    ic.part(thumb)
    for k in range(1, 4):
        x = 0.30 + k * 0.10
        ic.line(m_line([(x, 0.38), (x, 0.52)], lw(2)))
    ic.line(m_line([(0.36, 0.78), (0.64, 0.78)], lw(2)))
    return ic


def crew_cracked_mask():
    ic = Icon()
    mask = m_poly([(0.5, 0.10), (0.76, 0.20), (0.80, 0.50), (0.66, 0.82), (0.5, 0.90), (0.34, 0.82), (0.20, 0.50), (0.24, 0.20)])
    ic.part(mask)
    for dx in (-1, 1):
        ic.line(m_poly([(0.5 + dx * 0.05, 0.42), (0.5 + dx * 0.22, 0.37), (0.5 + dx * 0.20, 0.47), (0.5 + dx * 0.07, 0.49)]))
    ic.line(m_line([(0.42, 0.66), (0.58, 0.66)], lw(2.5)))
    ic.line(m_line([(0.56, 0.10), (0.52, 0.24), (0.60, 0.32), (0.54, 0.40)], lw(2.2)))   # crack
    ic.part(m_rrect(0.70, 0.28, 0.80, 0.34, 0.01), G_LIGHT, G_SHADE)   # implant
    return ic


def crew_broken_link():
    ic = Icon()
    a = minus(m_rrect(0.12, 0.30, 0.50, 0.62, 0.16), m_rrect(0.20, 0.38, 0.42, 0.54, 0.08))
    b = minus(m_rrect(0.50, 0.38, 0.88, 0.70, 0.16), m_rrect(0.58, 0.46, 0.80, 0.62, 0.08))
    ic.part(a); ic.part(b)
    for k, (x, y) in enumerate([(0.50, 0.24), (0.58, 0.30), (0.42, 0.76), (0.50, 0.82)]):
        ic.part(m_poly([(x, y - 0.05), (x + 0.03, y), (x, y + 0.05), (x - 0.03, y)]), G_LIGHT, G_SHADE)
    return ic


# ---------------------------------------------------------------- corporations (manner A), theme pool

def corp_security():
    ic = Icon()
    ic.part(m_poly([(0.5, 0.12), (0.82, 0.24), (0.78, 0.58), (0.5, 0.88), (0.22, 0.58), (0.18, 0.24)]))
    ic.line(m_ellipse(0.43, 0.36, 0.57, 0.50))
    ic.line(m_poly([(0.47, 0.46), (0.53, 0.46), (0.56, 0.66), (0.44, 0.66)]))
    return ic


def corp_media():
    ic = Icon()
    for r in (0.40, 0.30):
        for a0 in (-55, 125):
            ic.part(m_arc(0.5 - r, 0.5 - r, 0.5 + r, 0.5 + r, a0, a0 + 110, 0.055), G_LIGHT, G_SHADE)
    eye = m_poly([(0.24, 0.5)] + [(0.24 + 0.52 * t / 20, 0.5 - 0.17 * math.sin(math.pi * t / 20)) for t in range(21)] +
                 [(0.24 + 0.52 * (20 - t) / 20, 0.5 + 0.17 * math.sin(math.pi * (20 - t) / 20)) for t in range(21)])
    ic.part(eye)
    ic.part(m_ellipse(0.42, 0.42, 0.58, 0.58), G_LIGHT, G_SHADE)
    ic.line(m_ellipse(0.47, 0.47, 0.53, 0.53))
    return ic


def corp_biotech():
    ic = Icon()
    p1 = [(0.5 + math.sin(t / 30 * math.pi * 2.2) * 0.16, 0.12 + t / 30 * 0.76) for t in range(31)]
    p2 = [(0.5 - math.sin(t / 30 * math.pi * 2.2) * 0.16, 0.12 + t / 30 * 0.76) for t in range(31)]
    for i in range(3, 30, 5):
        ic.part(m_line([p1[i], p2[i]], 0.035), G_LIGHT, G_SHADE)
    ic.part(m_line(p1, 0.075))
    ic.part(m_line(p2, 0.075))
    return ic


def corp_neurotech():
    ic = Icon()
    brain = union(m_ellipse(0.20, 0.22, 0.56, 0.66), m_ellipse(0.42, 0.20, 0.80, 0.64), m_ellipse(0.30, 0.44, 0.72, 0.76))
    ic.part(brain, W_LIGHT, W_SHADE)
    ic.line(m_line([(0.5, 0.24), (0.48, 0.40), (0.52, 0.56), (0.50, 0.72)], lw(2.2)))
    ic.line(m_arc(0.26, 0.34, 0.44, 0.52, 200, 340, lw(2)))
    ic.line(m_arc(0.56, 0.32, 0.74, 0.50, 200, 340, lw(2)))
    plug(ic, 0.5, 0.74, 0.50, 0.86, 0.05)
    return ic


def corp_finance():
    ic = Icon()
    gem = m_poly([(0.5, 0.88), (0.12, 0.40), (0.26, 0.20), (0.74, 0.20), (0.88, 0.40)])
    ic.part(gem)
    ic.part(m_poly([(0.26, 0.20), (0.38, 0.40), (0.62, 0.40), (0.74, 0.20)]), G_LIGHT, G_SHADE)
    ic.line(m_line([(0.12, 0.40), (0.88, 0.40)], lw(2)))
    ic.line(m_line([(0.38, 0.40), (0.5, 0.88), (0.62, 0.40)], lw(2)))
    return ic


def corp_logistics():
    ic = Icon()
    top = m_poly([(0.5, 0.14), (0.84, 0.30), (0.5, 0.46), (0.16, 0.30)])
    left = m_poly([(0.16, 0.30), (0.5, 0.46), (0.5, 0.86), (0.16, 0.70)])
    right = m_poly([(0.84, 0.30), (0.5, 0.46), (0.5, 0.86), (0.84, 0.70)])
    ic.part(top); ic.part(left, W_LIGHT, W_SHADE); ic.part(right, G_LIGHT, G_SHADE)
    ic.line(m_line([(0.33, 0.22), (0.67, 0.38)], lw(2)))
    ic.line(m_line([(0.60, 0.62), (0.76, 0.54)], lw(3)))
    ic.line(m_poly([(0.78, 0.53), (0.71, 0.50), (0.74, 0.60)]))
    return ic


def corp_arms():
    ic = Icon()
    for k in (-1, 0, 1):
        x = 0.5 + k * 0.17
        ic.part(union(m_rrect(x - 0.06, 0.34, x + 0.06, 0.82, 0.02), m_ellipse(x - 0.06, 0.18, x + 0.06, 0.42)))
        ic.part(m_rrect(x - 0.07, 0.70, x + 0.07, 0.84, 0.015), G_LIGHT, G_SHADE)
    return ic


def corp_energy():
    ic = Icon()
    ring_m = minus(m_ellipse(0.12, 0.12, 0.88, 0.88), m_ellipse(0.24, 0.24, 0.76, 0.76))
    for k in range(8):
        a = k * math.pi / 4
        x, y = 0.5 + math.cos(a) * 0.40, 0.5 + math.sin(a) * 0.40
        ring_m = union(ring_m, m_ellipse(x - 0.07, y - 0.07, x + 0.07, y + 0.07))
    ic.part(ring_m, G_LIGHT, G_SHADE)
    ic.part(m_poly([(0.56, 0.20), (0.34, 0.54), (0.48, 0.54), (0.42, 0.82), (0.66, 0.44), (0.52, 0.44)]))
    return ic


def corp_pharma():
    ic = Icon()
    a = m_rrect(0.18, 0.36, 0.82, 0.64, 0.14)
    ic.part(a)
    ic.part(m_rrect(0.50, 0.36, 0.82, 0.64, 0.14), G_LIGHT, G_SHADE)
    ic.part(m_rrect(0.50, 0.36, 0.60, 0.64, 0.0), G_LIGHT, G_SHADE)
    ic.line(m_line([(0.5, 0.36), (0.5, 0.64)], lw(2)))
    ic.part(union(m_rrect(0.30, 0.10, 0.40, 0.34, 0.01), m_rrect(0.23, 0.17, 0.47, 0.27, 0.01)))
    return ic


def corp_databroker():
    ic = Icon()
    nodes = [(0.5, 0.18), (0.20, 0.42), (0.80, 0.42), (0.32, 0.80), (0.68, 0.80), (0.5, 0.52)]
    for a, b in [(0, 1), (0, 2), (1, 3), (2, 4), (3, 4), (5, 0), (5, 3), (5, 4), (5, 1), (5, 2)]:
        ic.part(m_line([nodes[a], nodes[b]], 0.035), G_LIGHT, G_SHADE)
    for i, (x, y) in enumerate(nodes):
        r = 0.09 if i == 5 else 0.07
        ic.part(m_ellipse(x - r, y - r, x + r, y + r))
    ic.line(m_ellipse(0.47, 0.49, 0.53, 0.55))
    return ic


def corp_orbital():
    ic = Icon()
    ic.part(m_rrect(0.06, 0.40, 0.34, 0.60, 0.02), G_LIGHT, G_SHADE)
    ic.part(m_rrect(0.66, 0.40, 0.94, 0.60, 0.02), G_LIGHT, G_SHADE)
    for x in (0.13, 0.20, 0.27, 0.73, 0.80, 0.87):
        ic.line(m_line([(x, 0.41), (x, 0.59)], lw(1.5)))
    ic.part(m_rrect(0.36, 0.34, 0.64, 0.66, 0.05))
    ic.part(m_arc(0.38, 0.06, 0.72, 0.40, 200, 330, 0.045), G_LIGHT, G_SHADE)
    ic.part(m_line([(0.5, 0.34), (0.56, 0.22)], 0.03), G_LIGHT, G_SHADE)
    return ic


# ---------------------------------------------------------------- Ideology issues and role (manner A)

def issue_ai():
    ic = Icon()
    chip(ic, 0.5, 0.5, 0.25)
    for dx in (-1, 1):
        ic.line(m_rrect(0.5 + dx * 0.10 - 0.04, 0.40, 0.5 + dx * 0.10 + 0.04, 0.54, 0.02))
    ic.line(m_line([(0.40, 0.62), (0.60, 0.62)], lw(2.5)))
    return ic


def issue_braindance():
    ic = Icon()
    ic.part(m_line([(0.06, 0.48), (0.18, 0.36), (0.82, 0.36), (0.94, 0.48)], 0.05), G_LIGHT, G_SHADE)
    body = m_poly([(0.14, 0.38), (0.86, 0.38), (0.92, 0.52), (0.82, 0.66), (0.58, 0.66), (0.5, 0.58), (0.42, 0.66), (0.18, 0.66), (0.08, 0.52)])
    ic.part(body)
    ic.part(m_poly([(0.22, 0.44), (0.78, 0.44), (0.82, 0.52), (0.76, 0.59), (0.24, 0.59), (0.18, 0.52)]), G_LIGHT, G_SHADE)
    ic.part(m_poly([(0.46, 0.46), (0.46, 0.57), (0.56, 0.515)]))
    return ic


def role_archon():
    ic = Icon()
    body = m_poly([(0.16, 0.92), (0.22, 0.70), (0.36, 0.62), (0.64, 0.62), (0.78, 0.70), (0.84, 0.92)])
    ic.part(body, G_LIGHT, G_SHADE)
    head = m_ellipse(0.34, 0.20, 0.66, 0.60)
    ic.part(head)
    ic.part(m_rrect(0.32, 0.34, 0.68, 0.44, 0.03), G_LIGHT, G_SHADE)    # visor
    ic.line(m_line([(0.36, 0.39), (0.64, 0.39)], lw(1.5)))
    plug(ic, 0.70, 0.36, 0.86, 0.14, 0.04)                              # cable from the temple
    ic.line(m_line([(0.5, 0.66), (0.5, 0.90)], lw(2.2)))
    ic.part(m_ellipse(0.46, 0.72, 0.54, 0.80))                         # pendant
    return ic


def faction_mercs():
    ic = Icon()
    for (a, b) in [((0.16, 0.84), (0.84, 0.24)), ((0.84, 0.84), (0.16, 0.24))]:
        ic.part(m_line([a, b], 0.07), G_LIGHT, G_SHADE)
    skull(ic, 0.5, 0.50, 1.2)
    ic.part(m_rrect(0.33, 0.37, 0.67, 0.47, 0.02), G_LIGHT, G_SHADE)    # visor strip
    return ic


# ---------------------------------------------------------------- memes (manner B, muted colors)

def meme_machine():
    ic = Icon()
    # rays
    for k in range(9):
        a = math.pi + (k + 0.5) * math.pi / 9
        x, y = 0.5 + math.cos(a) * 0.44, 0.38 + math.sin(a) * 0.32
        ic.part(m_line([(0.5 + math.cos(a) * 0.26, 0.38 + math.sin(a) * 0.20), (x, y)], 0.035), (250, 236, 170), (214, 186, 100))
    ic.part(m_rrect(0.24, 0.66, 0.76, 0.76, 0.02), (196, 190, 176), (140, 134, 122))   # pedestal
    ic.part(m_rrect(0.32, 0.74, 0.68, 0.90, 0.02), (176, 170, 156), (120, 114, 102))
    ic.part(m_ellipse(0.32, 0.22, 0.68, 0.62), (150, 214, 210), (66, 132, 134))        # AI core
    ic.part(m_poly([(0.38, 0.42), (0.50, 0.34), (0.62, 0.42), (0.50, 0.50)]), (236, 248, 246), (180, 220, 218))
    ic.line(m_ellipse(0.47, 0.39, 0.53, 0.45))
    return ic


def meme_ascension():
    ic = Icon()
    skin_l, skin_s = (232, 196, 160), (176, 128, 98)
    shoulders = m_poly([(0.18, 0.94), (0.24, 0.76), (0.40, 0.68), (0.62, 0.68), (0.76, 0.76), (0.82, 0.94)])
    ic.part(shoulders, (120, 140, 160), (70, 84, 104))
    head = minus(m_ellipse(0.34, 0.28, 0.68, 0.72), m_rrect(0.30, 0.20, 0.72, 0.42, 0.0))
    ic.part(head, skin_l, skin_s)
    ic.part(m_rrect(0.36, 0.48, 0.66, 0.54, 0.02), (150, 216, 224), (80, 150, 168))     # visor
    import random
    rnd = random.Random(3)
    for row in range(6):
        y = 0.40 - row * 0.065
        n = 5 - row // 2
        for k in range(n):
            if rnd.random() < 0.2 + row * 0.08:
                continue
            x = 0.51 + (k - (n - 1) / 2) * 0.075 + rnd.uniform(-0.02, 0.02) * row
            s = 0.026 - row * 0.002
            col = (skin_l, skin_s) if row == 0 else ((190, 226, 232), (110, 170, 186))
            ic.part(m_rrect(x - s, y - s, x + s, y + s, 0.003), *col)
    return ic


def meme_purity():
    ic = Icon()
    chip_l, chip_s = (120, 124, 112), (70, 74, 64)
    a = m_poly([(0.20, 0.50), (0.50, 0.50), (0.44, 0.62), (0.52, 0.72), (0.46, 0.86), (0.20, 0.86)])
    b = m_poly([(0.54, 0.50), (0.80, 0.50), (0.80, 0.86), (0.50, 0.86), (0.56, 0.72), (0.48, 0.62)])
    ic.part(a, chip_l, chip_s); ic.part(b, chip_l, chip_s)
    for x in (0.28, 0.38, 0.62, 0.72):
        ic.part(m_rrect(x - 0.02, 0.86, x + 0.02, 0.94, 0.005), (176, 176, 160), (120, 120, 104))
    ic.line(m_line([(0.26, 0.58), (0.36, 0.58), (0.40, 0.66)], lw(1.6)))
    ic.line(m_line([(0.74, 0.78), (0.64, 0.78)], lw(1.6)))
    stem = m_line([(0.50, 0.66), (0.50, 0.40), (0.47, 0.24)], 0.045)
    ic.part(stem, (126, 186, 92), (64, 120, 50))
    ic.part(m_poly([(0.49, 0.34), (0.30, 0.22), (0.20, 0.28), (0.34, 0.38)]), (150, 206, 104), (80, 140, 60))
    ic.part(m_poly([(0.49, 0.28), (0.66, 0.12), (0.80, 0.16), (0.64, 0.30)]), (150, 206, 104), (80, 140, 60))
    return ic


GROUPS = [
    ("Команды раннеров: иконки фракций (белые, игра красит в цвет фракции)", [
        ("Crew_SkullCables", "Череп и кабели", crew_skull_cables), ("Crew_HoodVisor", "Капюшон и визор", crew_hood_visor),
        ("Crew_Crow", "Ворон", crew_crow), ("Crew_FistCable", "Кулак с кабелем", crew_fist_cable),
        ("Crew_CrackedMask", "Треснувшая маска", crew_cracked_mask), ("Crew_BrokenLink", "Разорванное звено", crew_broken_link),
    ]),
    ("Корпорации: иконки фракций по тематикам (пул)", [
        ("Corp_Security", "Охранная", corp_security), ("Corp_Media", "Медиа", corp_media), ("Corp_Biotech", "Биотех", corp_biotech),
        ("Corp_Neurotech", "Нейротех", corp_neurotech), ("Corp_Finance", "Финансы", corp_finance), ("Corp_Logistics", "Логистика", corp_logistics),
        ("Corp_Arms", "Оружейная", corp_arms), ("Corp_Energy", "Энергетика", corp_energy), ("Corp_Pharma", "Фарма", corp_pharma),
        ("Corp_DataBroker", "Дата-брокер", corp_databroker), ("Corp_Orbital", "Орбитальная", corp_orbital),
    ]),
    ("Ideology: вопросы, роль; наёмники (перерисовка в белом)", [
        ("Issue_AI", "Вопрос «ИИ»", issue_ai), ("Issue_Braindance", "Вопрос «Брейнданс»", issue_braindance),
        ("Role_NetArchon", "Роль «Архонт сети»", role_archon), ("Faction_Mercenaries_v2", "Наёмники", faction_mercs),
    ]),
    ("Мемы (иллюстрации, приглушённые цвета)", [
        ("Meme_MachineRevelation", "Машинное откровение", meme_machine), ("Meme_NetAscension", "Сетевое восхождение", meme_ascension),
        ("Meme_DigitalPurity", "Цифровая чистота", meme_purity),
    ]),
]

TINTS = [(0.85, 0.25, 0.30), (0.30, 0.65, 0.90), (0.95, 0.75, 0.25)]


def tinted(img, rgb):
    r, g, b, a = img.split()
    r = r.point(lambda v: int(v * rgb[0])); g = g.point(lambda v: int(v * rgb[1])); b = b.point(lambda v: int(v * rgb[2]))
    return Image.merge("RGBA", (r, g, b, a))


def build(ref_dir=None):
    os.makedirs(OUT, exist_ok=True)
    cell, pad = 128, 22
    cols = 6
    rows_total = sum((len(items) + cols - 1) // cols for _, items in GROUPS)
    W = pad + cols * (cell + pad) + 260
    H = 80 + len(GROUPS) * 60 + rows_total * (cell + 46) + (cell + 110 if ref_dir else 0)
    sheet = Image.new("RGBA", (W, H), (43, 43, 43, 255))    # RimWorld window grey
    d = ImageDraw.Draw(sheet)
    d.text((pad, 20), "Иконки v2 в ванильной манере (docs/ART_STYLE.md), 128 px", font=font(24, True), fill=(235, 235, 235))
    y = 70
    for gtitle, items in GROUPS:
        d.text((pad, y), gtitle, font=font(17, True), fill=(200, 200, 200)); y += 32
        for i, (fname, label, fn) in enumerate(items):
            ic = fn()
            img128 = ic.render(128); img256 = ic.render(256)
            img128.save(os.path.join(OUT, fname + "_128.png")); img256.save(os.path.join(OUT, fname + ".png"))
            x = pad + (i % cols) * (cell + pad)
            yy = y + (i // cols) * (cell + 46)
            sheet.alpha_composite(img128, (x, yy))
            d.text((x + cell / 2 - d.textlength(label, font=font(13)) / 2, yy + cell + 4), label, font=font(13), fill=(190, 190, 190))
        if gtitle.startswith("Команды"):
            # tint preview: how the game shows faction icons
            tx = pad + cols * (cell + pad) + 10
            d.text((tx, y - 30), "Как в игре (цвет фракции):", font=font(13, True), fill=(200, 200, 200))
            sample = GROUPS[0][1][0][2]().render(64)
            for k, t in enumerate(TINTS):
                sheet.alpha_composite(tinted(sample, t), (tx + k * 74, y))
        y += ((len(items) + cols - 1) // cols) * (cell + 46) + 28
    if ref_dir and os.path.isdir(ref_dir):
        d.text((pad, y), "Эталон для сравнения: Vanilla Expanded (в репозиторий не кладётся)", font=font(17, True), fill=(200, 200, 200)); y += 32
        refs = sorted(f for f in os.listdir(ref_dir) if f.endswith(".png"))[:7]
        for i, f in enumerate(refs):
            im = Image.open(os.path.join(ref_dir, f)).convert("RGBA").resize((cell, cell), Image.LANCZOS)
            sheet.alpha_composite(im, (pad + i * (cell + pad), y))
    sheet.convert("RGB").save(os.path.join(MOCK, "28-icons-v2.png"))


if __name__ == "__main__":
    build(sys.argv[1] if len(sys.argv) > 1 else None)
    print("done")
