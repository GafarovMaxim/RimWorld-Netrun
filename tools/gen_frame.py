#!/usr/bin/env python3
"""
Generates the textures of the terminal casing, template A "field deck" (docs/CONCEPT_TERMINAL_FRAME.md).

    python3 tools/gen_frame.py            # textures into Textures/UI/Netrun/Frame/
    python3 tools/gen_frame.py --mock SCREEN.png OUT.png   # a composite preview with a screenshot inside

Needs numpy and pillow. Everything is drawn procedurally (gradients, brushed noise, bevels) at 3x and shrunk, so the
files are reproducible. Names, sizes and 9-slice borders are listed in docs/CONCEPT_TERMINAL_FRAME.md.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(BASE, "Textures", "UI", "Netrun", "Frame")
SS = 3  # supersampling

# ---- the 9-slice layout of the body: borders in final pixels ----
BODY_L, BODY_R, BODY_T, BODY_B = 28, 28, 56, 64
BODY_SIZE = 256
BUMPER_W, BUMPER_TH, BUMPER_BH = 78, 56, 64
RAIL_W, RAIL_CAP = 22, 16


def hexc(h):
    return np.array([(h >> 16) & 255, (h >> 8) & 255, h & 255], dtype=np.float32)


def lerp(a, b, t):
    return a + (b - a) * t


def rng(seed):
    return np.random.default_rng(seed)


def streaks(w, h, axis, amp, seed, blur=1.2):
    """Brushed metal: noise stretched along one axis. axis='x' means streaks run horizontally."""
    r = rng(seed)
    if axis == "x":
        n = r.normal(0, 1, (h, 1)) * np.ones((1, w)) * 0.7 + r.normal(0, 1, (h, w)) * 0.3
    else:
        n = r.normal(0, 1, (1, w)) * np.ones((h, 1)) * 0.7 + r.normal(0, 1, (h, w)) * 0.3
    img = Image.fromarray(np.clip(n * 40 + 128, 0, 255).astype("uint8"))
    img = img.filter(ImageFilter.GaussianBlur(blur))
    return (np.asarray(img, dtype=np.float32) - 128.0) / 128.0 * amp


def to_img(rgb, alpha):
    a = np.clip(alpha, 0, 1)
    out = np.dstack([np.clip(rgb, 0, 255), a * 255]).astype("uint8")
    return Image.fromarray(out, "RGBA")


def shrink(img):
    return img.resize((img.width // SS, img.height // SS), Image.LANCZOS)


def mask_of(size, fn):
    m = Image.new("L", size, 0)
    fn(ImageDraw.Draw(m))
    return np.asarray(m, dtype=np.float32) / 255.0


def edge_dist(mask, px):
    """Approximate distance (in supersampled px) from the inside of a mask edge: a blurred mask."""
    m = Image.fromarray((mask * 255).astype("uint8"))
    return np.asarray(m.filter(ImageFilter.GaussianBlur(px)), dtype=np.float32) / 255.0


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name))


# ------------------------------------------------------------------ body

def make_body():
    W = H = BODY_SIZE * SS
    s = SS
    ys = np.arange(H)[:, None] / s
    xs = np.arange(W)[None, :] / s
    top = np.clip(ys / BODY_T, 0, 1)
    bot = np.clip((ys - (BODY_SIZE - BODY_B)) / BODY_B, 0, 1)
    side_l = np.clip(xs / BODY_L, 0, 1)
    side_r = np.clip((BODY_SIZE - xs) / BODY_R, 0, 1)
    c_top0, c_top1 = hexc(0x424b55), hexc(0x2c333b)
    c_bot0, c_bot1 = hexc(0x2a3138), hexc(0x1a1f24)
    c_side0, c_side1 = hexc(0x4a5560), hexc(0x262c33)
    col = np.zeros((H, W, 3), dtype=np.float32)
    in_top = (ys < BODY_T)
    in_bot = (ys >= BODY_SIZE - BODY_B)
    t_top = top[:, :, None] * np.ones((1, W, 1))
    col_top = lerp(c_top0, c_top1, t_top)
    col_bot = lerp(c_bot0, c_bot1, (bot[:, :, None] * np.ones((1, W, 1))))
    sl = (1 - side_l)[:, :, None] * np.ones((H, 1, 1))
    sr = (1 - side_r)[:, :, None] * np.ones((H, 1, 1))
    col_l = lerp(c_side0, c_side1, np.clip(1 - sl, 0, 1))
    col_r = lerp(c_side0, c_side1, np.clip(1 - sr, 0, 1))
    side_pick = np.where((xs < BODY_SIZE / 2)[:, :, None] * np.ones((H, 1, 1)) > 0, col_l, col_r)
    col = np.where(in_top[:, :, None] * np.ones((1, W, 1)) > 0, col_top, side_pick)
    col = np.where(in_bot[:, :, None] * np.ones((1, W, 1)) > 0, col_bot, col)
    # brushed metal: horizontal streaks on the strips along the top and bottom, vertical on the sides
    sh = streaks(W, H, "x", 7, 1)
    sv = streaks(W, H, "y", 7, 2)
    horiz = (in_top | in_bot)[:, :, None] * np.ones((1, W, 1)) > 0
    col = col + np.where(horiz, sh[:, :, None], sv[:, :, None])
    # alpha: rounded outer shape minus the window
    R = 22 * s
    outer = mask_of((W, H), lambda d: d.rounded_rectangle([0, 0, W - 1, H - 1], R, fill=255))
    win = (BODY_L * s, BODY_T * s, (BODY_SIZE - BODY_R) * s, (BODY_SIZE - BODY_B) * s)
    hole = mask_of((W, H), lambda d: d.rectangle([win[0], win[1], win[2] - 1, win[3] - 1], fill=255))
    alpha = outer * (1 - hole)
    # edge lighting: lit rim along the outer edge (top/left brighter), dark bevel at the window
    inner_o = edge_dist(outer, 1.6 * s)
    rim = np.clip((1 - inner_o) * 2.4, 0, 1) * outer
    lit = np.clip(1.0 - (xs + ys) / (BODY_SIZE * 1.3), 0.25, 1.0)
    col = col + (rim * lit * 46)[:, :, None]
    col = col - (np.clip((1 - edge_dist(outer, 3.0 * s)) * 1.6, 0, 1) * outer * (1 - lit) * 30)[:, :, None]
    # window bevel: dark line right at the window edge, light line on the far side (below/right)
    d = ImageDraw.Draw(Image.new("L", (1, 1)))
    shadow = mask_of((W, H), lambda dr: dr.rectangle([win[0] - 3 * s, win[1] - 3 * s, win[2] + 3 * s - 1, win[3] + 3 * s - 1], outline=255, width=3 * s))
    shadow = shadow * (1 - hole)
    col = col * (1 - 0.55 * shadow[:, :, None])
    hi = mask_of((W, H), lambda dr: dr.rectangle([win[0] - 5 * s, win[1] - 5 * s, win[2] + 5 * s - 1, win[3] + 5 * s - 1], outline=255, width=2 * s))
    hi = hi * (1 - hole) * (1 - shadow)
    col = col + 22 * hi[:, :, None]
    # panel seams: a dark line with a light one under it, across the top and bottom strips
    for y0, strength in ((40, 1.0), (BODY_SIZE - BODY_B + 24, 1.0)):
        seam = np.zeros((H, W), dtype=np.float32)
        yy = y0 * s
        seam[yy:yy + s, :] = 1
        light = np.zeros((H, W), dtype=np.float32)
        light[yy + s:yy + 2 * s, :] = 1
        seam *= horiz[:, :, 0] * (1 - hole) * (xs > 8) * (xs < BODY_SIZE - 8)
        light *= horiz[:, :, 0] * (1 - hole) * (xs > 8) * (xs < BODY_SIZE - 8)
        col = col * (1 - 0.5 * seam[:, :, None]) + 14 * light[:, :, None]
    # a few edge chips (wear) so it does not look new
    r = rng(5)
    for _ in range(26):
        x = int(r.integers(0, W))
        y = int(r.integers(0, H))
        if alpha[y, x] > 0.5 and inner_o[y, x] < 0.75:
            rr = int(r.integers(2, 5)) * s // 2
            chip = mask_of((W, H), lambda dr: dr.ellipse([x - rr, y - rr // 2, x + rr, y + rr // 2], fill=255)) * alpha
            col = col + 26 * chip[:, :, None]
    return shrink(to_img(col, alpha))


# ------------------------------------------------------------------ bumpers, rail, bracket

def make_bumper(h_px):
    """A ribbed rubber corner guard, BUMPER_W wide and h_px tall, so it stays inside the casing strip and never covers the screen."""
    s = SS
    W, H = BUMPER_W * s, h_px * s
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32) / s
    R = 26 * s
    shape = mask_of((W, H), lambda d: d.rounded_rectangle([0, 0, W - 1, H - 1], 8 * s, fill=255))
    sq = mask_of((W, H), lambda d: d.rectangle([0, 0, R, R], fill=255))
    ell = mask_of((W, H), lambda d: d.ellipse([0, 0, 2 * R, 2 * R], fill=255))
    alpha = shape * (1 - sq * (1 - ell))
    col = np.ones((H, W, 3), dtype=np.float32) * hexc(0x17191c)
    ph = ((xs + ys) / 5.2) % 1.0
    rib = np.where(ph < 0.5, 1.0, 0.0) * 0.5 + np.sin(ph * 2 * math.pi) * 0.5
    col = col + (rib * 9 - 2)[:, :, None]
    col = col + (np.clip(1 - (xs + ys) / (BUMPER_W * 1.3), 0, 1) * 18)[:, :, None]
    ed = edge_dist(alpha, 2.0 * s)
    col = col + (np.clip((1 - ed) * 2, 0, 1) * alpha * 26)[:, :, None]
    col = col * (1 - (np.clip((1 - edge_dist(alpha, 0.8 * s)) * 3, 0, 1) * alpha * 0.8)[:, :, None])
    cx, cy = 44 * s, (h_px // 2 + 2) * s
    for k, rad in enumerate((10, 8.5)):
        hexm = mask_of((W, H), lambda d: d.regular_polygon((cx, cy, rad * s), 6, rotation=15, fill=255))
        if k == 0:
            tone = np.ones((H, W, 3), dtype=np.float32) * hexc(0x0c0e10)
        else:
            tone = lerp(hexc(0x8d97a2), hexc(0x38404a), np.clip((xs + ys - 60) / 40, 0, 1)[:, :, None])
        col = col * (1 - hexm[:, :, None]) + tone * hexm[:, :, None]
    slot = mask_of((W, H), lambda d: d.line([cx - 5 * s, cy + 2 * s, cx + 5 * s, cy - 2 * s], fill=255, width=int(1.6 * s)))
    col = col * (1 - 0.7 * slot[:, :, None])
    return shrink(to_img(col, alpha))


def flip_variants(img, name):
    save(img, f"Deck_Bumper_{name}.png")


def make_rail():
    w, h = RAIL_W * SS, 96 * SS
    s = SS
    xs = np.arange(w)[None, :].astype(np.float32) / w
    prof = np.zeros((1, w, 3), dtype=np.float32)
    stops = [(0.0, 0x1a1f24), (0.12, 0x4d5862), (0.34, 0xe3e9ee), (0.5, 0xaab4be), (0.78, 0x5e6973), (1.0, 0x14181c)]
    for i in range(len(stops) - 1):
        a, ca = stops[i]
        b, cb = stops[i + 1]
        m = (xs[0] >= a) & (xs[0] <= b)
        t = (xs[0][m] - a) / (b - a)
        prof[0, m, :] = lerp(hexc(ca), hexc(cb), t[:, None])
    col = np.repeat(prof, h, axis=0)
    shape = mask_of((w, h), lambda d: d.rounded_rectangle([0, 0, w - 1, h - 1], w // 2, fill=255))
    col = col + streaks(w, h, "y", 4, 9, 0.8)[:, :, None]
    return shrink(to_img(col, shape))


def make_bracket():
    w, h = 30 * SS, 18 * SS
    s = SS
    col = np.zeros((h, w, 3), dtype=np.float32)
    ys = np.arange(h)[:, None] / h
    col[:] = lerp(hexc(0x56616b), hexc(0x242a30), ys)[:, :, None].transpose(0, 2, 1) if False else lerp(hexc(0x56616b), hexc(0x242a30), ys[:, :, None])
    shape = mask_of((w, h), lambda d: d.rounded_rectangle([0, 0, w - 1, h - 1], 3 * s, fill=255))
    for bx in (7 * s, w - 7 * s):
        b = mask_of((w, h), lambda d: d.ellipse([bx - 3 * s, h // 2 - 3 * s, bx + 3 * s, h // 2 + 3 * s], fill=255))
        col = col * (1 - b[:, :, None]) + hexc(0x9aa4ae) * b[:, :, None]
    ed = edge_dist(shape, 1.0 * s)
    col = col * (1 - (np.clip((1 - ed) * 2.5, 0, 1) * shape * 0.6)[:, :, None])
    return shrink(to_img(col, shape))


# ------------------------------------------------------------------ knob, socket, gauge, key, plate, vent, led, screen

def radial(n):
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    c = (n - 1) / 2
    dx, dy = xs - c, ys - c
    return np.hypot(dx, dy) / c, np.degrees(np.arctan2(dy, dx))


def make_knob():
    size = 128
    n = size * SS
    r, ang = radial(n)
    alpha = np.clip((0.985 - r) * 40, 0, 1)
    ribs = (np.sin(np.radians(ang) * 24) * 0.5 + 0.5)
    ring = np.clip((r - 0.62) / 0.3, 0, 1)
    base = lerp(hexc(0x30373e), hexc(0x14181c), np.clip(r, 0, 1)[:, :, None])
    col = base + ((ribs - 0.5) * 34 * ring)[:, :, None]
    # top face: concentric grooves and a lit upper-left arc
    face = (r < 0.6).astype(np.float32)
    grooves = (np.sin(r * 80) * 0.5 + 0.5)
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    lit = np.clip(1 - (xs + ys) / (2.0 * n) * 1.8, 0, 1)
    col = np.where(face[:, :, None] > 0, lerp(hexc(0x3b434b), hexc(0x242a30), np.clip(r / 0.6, 0, 1)[:, :, None]) + (grooves * 8 + lit * 14)[:, :, None], col)
    col = col - (np.clip(1 - np.abs(r - 0.6) * 30, 0, 1) * 34)[:, :, None]
    col = col * (1 - np.clip((r - 0.93) * 12, 0, 1)[:, :, None] * 0.7)
    return shrink(to_img(col, alpha))


def make_socket():
    size = 160
    n = size * SS
    r, ang = radial(n)
    alpha = np.clip((0.99 - r) * 40, 0, 1)
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    col = lerp(hexc(0x0a0c0e), hexc(0x20262c), np.clip((r - 0.7) / 0.3, 0, 1)[:, :, None])
    shade = np.clip(1 - (xs + ys) / (2.0 * n) * 1.6, 0, 1)
    col = col + (shade * (r > 0.8) * 14)[:, :, None]
    col = col + (np.clip(1 - np.abs(r - 0.97) * 40, 0, 1) * 30)[:, :, None]
    return shrink(to_img(col, alpha))


GAUGE_ARC = (-135.0, 135.0)  # degrees from straight up, clockwise


def zone_color(frac, zones):
    for hi, col in zones:
        if frac <= hi + 1e-6:
            return col
    return zones[-1][1]


HEAT_ZONES = [(0.24, hexc(0x2fc46b)), (0.49, hexc(0xe6c431)), (0.74, hexc(0xff8a3c)), (0.94, hexc(0xff4a3c)), (1.0, hexc(0xb3122a))]
NET_ZONES = [(0.15, hexc(0x7a8590)), (1.0, hexc(0x31c46a))]


def make_gauge_face(zones, name):
    size = 128
    n = size * SS
    r, ang = radial(n)
    alpha = np.clip((0.86 - r) * 40, 0, 1)
    col = lerp(hexc(0x07120d), hexc(0x050a07), np.clip(r / 0.86, 0, 1)[:, :, None])
    # angle measured from straight up, clockwise: atan2(dx, -dy)
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    c = (n - 1) / 2
    a = np.degrees(np.arctan2(xs - c, -(ys - c)))
    inside = (a >= GAUGE_ARC[0]) & (a <= GAUGE_ARC[1])
    frac = np.clip((a - GAUGE_ARC[0]) / (GAUGE_ARC[1] - GAUGE_ARC[0]), 0, 1)
    arc = inside & (r > 0.60) & (r < 0.78)
    zc = np.zeros((n, n, 3), dtype=np.float32)
    for hi, colr in reversed(zones):
        zc = np.where((frac <= hi + 1e-6)[:, :, None], colr, zc)
    col = np.where(arc[:, :, None], zc * 0.9, col)
    # ticks
    for i in range(0, 21):
        ta = GAUGE_ARC[0] + (GAUGE_ARC[1] - GAUGE_ARC[0]) * i / 20
        major = i % 5 == 0
        ra = np.radians(ta)
        r0, r1 = (0.46, 0.6) if major else (0.52, 0.6)
        x0, y0 = c + math.sin(ra) * r0 * c, c - math.cos(ra) * r0 * c
        x1, y1 = c + math.sin(ra) * r1 * c, c - math.cos(ra) * r1 * c
        t = mask_of((n, n), lambda d: d.line([x0, y0, x1, y1], fill=255, width=int((2.2 if major else 1.2) * SS)))
        col = col * (1 - t[:, :, None]) + hexc(0xbfd8c8) * 0.8 * t[:, :, None]
    col = col * (1 - np.clip((r - 0.8) * 12, 0, 1)[:, :, None] * 0.5)
    return shrink(to_img(col, alpha)), name


def make_gauge_bezel():
    size = 128
    n = size * SS
    r, ang = radial(n)
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    ring = (r <= 0.99) & (r >= 0.8)
    lit = np.clip(1 - (xs + ys) / (2.0 * n) * 1.6, 0, 1)
    metal = lerp(hexc(0x1b2025), hexc(0x9aa5b0), lit[:, :, None] * np.clip(1 - np.abs(r - 0.9) / 0.1, 0.25, 1)[:, :, None])
    a_ring = np.clip((0.99 - r) * 40, 0, 1) * np.clip((r - 0.8) * 40, 0, 1)
    # glass: a soft highlight on the upper-left of the face, a faint dark vignette
    face = np.clip((0.82 - r) * 40, 0, 1)
    hl = np.clip(1 - np.hypot(xs - n * 0.38, ys - n * 0.3) / (n * 0.34), 0, 1) ** 2
    glass_a = face * (hl * 0.22 + np.clip(r - 0.5, 0, 1) * 0.18)
    glass_c = np.ones((n, n, 3), dtype=np.float32) * 230
    glass_c = glass_c * hl[:, :, None] + np.zeros((n, n, 3)) * (1 - hl[:, :, None])
    alpha = np.maximum(a_ring, glass_a)
    col = np.where((a_ring > 0.01)[:, :, None], metal, glass_c)
    return shrink(to_img(col, alpha))


def capsule(w, h, c0, c1, edge_light, edge_dark, radius=None):
    s = SS
    W, H = w * s, h * s
    R = (radius if radius is not None else h / 2) * s
    shape = mask_of((W, H), lambda d: d.rounded_rectangle([0, 0, W - 1, H - 1], R, fill=255))
    ys = np.arange(H)[:, None].astype(np.float32) / H
    col = lerp(hexc(c0), hexc(c1), ys[:, :, None]) * np.ones((1, W, 1))
    ed = edge_dist(shape, 1.5 * s)
    top_light = np.clip(1 - ys * 6, 0, 1)
    col = col + (top_light * shape * 40)[:, :, None]
    col = col * (1 - (np.clip((1 - ed) * 2.4, 0, 1) * shape * 0.65)[:, :, None])
    col = col + (np.clip(1 - ys * 14, 0, 1) * shape * edge_light)[:, :, None]
    return shrink(to_img(col, shape))


def make_plate():
    w, h = 64, 26
    s = SS
    W, H = w * s, h * s
    shape = mask_of((W, H), lambda d: d.rounded_rectangle([0, 0, W - 1, H - 1], 3 * s, fill=255))
    ys = np.arange(H)[:, None].astype(np.float32) / H
    col = lerp(hexc(0x0e1114), hexc(0x181c20), ys[:, :, None]) * np.ones((1, W, 1))
    ed = edge_dist(shape, 1.2 * s)
    col = col + (np.clip((ys - 0.82) * 12, 0, 1) * shape * 40)[:, :, None]
    col = col * (1 - (np.clip((1 - ed) * 2.4, 0, 1) * shape * 0.5)[:, :, None])
    return shrink(to_img(col, shape))


def make_vent():
    w, h = 120, 28
    s = SS
    W, H = w * s, h * s
    shape = mask_of((W, H), lambda d: d.rounded_rectangle([0, 0, W - 1, H - 1], 4 * s, fill=255))
    col = np.ones((H, W, 3), dtype=np.float32) * hexc(0x14181c)
    xs = np.arange(W)[None, :] / s
    for i in range(10):
        x0 = 8 + i * 11
        slot = mask_of((W, H), lambda d: d.rounded_rectangle([x0 * s, 5 * s, (x0 + 5) * s, (h - 5) * s], 2 * s, fill=255))
        col = col * (1 - slot[:, :, None]) + hexc(0x030405) * slot[:, :, None]
        hi = mask_of((W, H), lambda d: d.line([(x0 + 5) * s, 6 * s, (x0 + 5) * s, (h - 6) * s], fill=255, width=s))
        col = col + 34 * (hi * (1 - slot))[:, :, None]
    col = col * (1 - (np.clip((1 - edge_dist(shape, 1.2 * s)) * 2.4, 0, 1) * shape * 0.5)[:, :, None])
    return shrink(to_img(col, shape))


def make_led(color_rgb, name):
    size = 24
    n = size * SS
    r, _ = radial(n)
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    lens = np.clip((0.55 - r) * 40, 0, 1)
    col = np.ones((n, n, 3), dtype=np.float32) * 0
    base = np.array(color_rgb, dtype=np.float32)
    shade = lerp(base * 1.1, base * 0.5, np.clip(r / 0.55, 0, 1)[:, :, None])
    hl = np.clip(1 - np.hypot(xs - n * 0.42, ys - n * 0.38) / (n * 0.16), 0, 1)
    shade = shade + hl[:, :, None] * 120
    col = np.where((lens > 0)[:, :, None], shade, col)
    ring = np.clip((0.78 - r) * 40, 0, 1) * np.clip((r - 0.55) * 40, 0, 1)
    col = np.where((ring > 0)[:, :, None], hexc(0x20262c) * (0.6 + 0.6 * (1 - ys / n))[:, :, None], col)
    return shrink(to_img(col, np.maximum(lens, ring))), name


def make_screen_frame():
    w = h = 48
    s = SS
    W, H = w * s, h * s
    shape = mask_of((W, H), lambda d: d.rounded_rectangle([0, 0, W - 1, H - 1], 8 * s, fill=255))
    hole = mask_of((W, H), lambda d: d.rectangle([14 * s, 14 * s, (w - 14) * s - 1, (h - 14) * s - 1], fill=255))
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    col = np.ones((H, W, 3), dtype=np.float32) * hexc(0x07090b)
    # outer lit edge (lower/right), inner shadow ring and an inner bright hairline: the screen sits in a recess
    ring = shape * (1 - hole)
    ed = edge_dist(ring, 2.0 * s)
    col = col + (np.clip((1 - ed) * 1.6, 0, 1) * ring * 20)[:, :, None]
    inner = mask_of((W, H), lambda d: d.rectangle([13 * s, 13 * s, (w - 13) * s - 1, (h - 13) * s - 1], outline=255, width=s))
    col = col + 70 * (inner * (1 - hole))[:, :, None]
    # corner rivets of light
    return shrink(to_img(col, ring))


def make_glass():
    w, h = 256, 256
    s = 2
    W, H = w * s, h * s
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    d = (xs / W + ys / H)
    band = np.exp(-((d - 0.42) / 0.07) ** 2) * 0.07 + np.exp(-((d - 0.58) / 0.025) ** 2) * 0.05
    vign = np.clip(np.hypot(xs / W - 0.5, ys / H - 0.5) - 0.38, 0, 1) * 1.1
    col = np.ones((H, W, 3), dtype=np.float32) * 255
    a = band
    col = col * (a / np.maximum(a + vign * 0.0, 1e-6))[:, :, None] * 0 + 255
    # two layers in one: white sheen where band, black vignette at the edges
    out = np.zeros((H, W, 4), dtype=np.float32)
    v = np.clip(vign, 0, 0.55)
    alpha = np.clip(band + v, 0, 1)
    colr = (255 * band + 0 * v) / np.maximum(band + v, 1e-6)
    out[..., 0] = out[..., 1] = out[..., 2] = colr
    out[..., 3] = alpha * 255
    return Image.fromarray(np.clip(out, 0, 255).astype("uint8"), "RGBA").resize((w, h), Image.LANCZOS)


def make_antenna():
    w, h = 24, 56
    s = SS
    W, H = w * s, h * s
    xs = np.arange(W)[None, :].astype(np.float32) / W
    col = np.zeros((H, W, 3), dtype=np.float32)
    prof = lerp(hexc(0x10151a), hexc(0x56616b), np.clip(1 - np.abs(xs - 0.38) / 0.4, 0, 1)[:, :, None])
    col[:] = prof
    base = mask_of((W, H), lambda d: d.rounded_rectangle([0, 26 * s, W - 1, H - 1], 5 * s, fill=255))
    rod = mask_of((W, H), lambda d: d.rounded_rectangle([9 * s, 0, 15 * s, H], 3 * s, fill=255))
    tip = mask_of((W, H), lambda d: d.ellipse([7 * s, 0, 17 * s, 9 * s], fill=255))
    shape = np.maximum(np.maximum(base, rod), tip)
    return shrink(to_img(col, shape))


def build():
    save(make_body(), "Deck_Body.png")
    bt, bb = make_bumper(BUMPER_TH), make_bumper(BUMPER_BH)
    save(bt, "Deck_Bumper_TL.png")
    save(bt.transpose(Image.FLIP_LEFT_RIGHT), "Deck_Bumper_TR.png")
    save(bb.transpose(Image.FLIP_TOP_BOTTOM), "Deck_Bumper_BL.png")
    save(bb.transpose(Image.ROTATE_180), "Deck_Bumper_BR.png")
    save(make_rail(), "Deck_Rail.png")
    save(make_bracket(), "Deck_Bracket.png")
    save(make_knob(), "Deck_Knob.png")
    save(make_socket(), "Deck_KnobSocket.png")
    face, _ = make_gauge_face(HEAT_ZONES, "Heat")
    save(face, "Deck_GaugeFace_Heat.png")
    face, _ = make_gauge_face(NET_ZONES, "Net")
    save(face, "Deck_GaugeFace_Net.png")
    save(make_gauge_bezel(), "Deck_GaugeBezel.png")
    save(capsule(64, 28, 0x434b53, 0x1c2024, 36, 0), "Deck_Key.png")
    save(capsule(64, 28, 0xff9a4a, 0xc85d16, 60, 0), "Deck_Key_Hot.png")
    save(make_plate(), "Deck_Plate.png")
    save(make_vent(), "Deck_Vent.png")
    for nm, c in (("Green", (47, 224, 122)), ("Orange", (255, 138, 60)), ("Red", (255, 70, 70)), ("Off", (40, 48, 56))):
        led, _ = make_led(c, nm)
        save(led, f"Deck_Led_{nm}.png")
    save(make_screen_frame(), "Deck_ScreenFrame.png")
    save(make_glass(), "Deck_Glass.png")
    save(make_antenna(), "Deck_Antenna.png")


# ------------------------------------------------------------------ composite preview

def nine_slice(src, size, l, t, r, b):
    w, h = size
    sw, sh = src.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    xs = [(0, l, 0, l), (l, w - r, l, sw - r), (w - r, w, sw - r, sw)]
    ys = [(0, t, 0, t), (t, h - b, t, sh - b), (h - b, h, sh - b, sh)]
    for dy0, dy1, sy0, sy1 in ys:
        for dx0, dx1, sx0, sx1 in xs:
            if dx1 <= dx0 or dy1 <= dy0:
                continue
            piece = src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS)
            out.alpha_composite(piece, (dx0, dy0))
    return out


def rotate_needle(img, center, angle, length, color, width=2):
    d = ImageDraw.Draw(img)
    a = math.radians(angle)
    x1, y1 = center[0] + math.sin(a) * length, center[1] - math.cos(a) * length
    x0, y0 = center[0] - math.sin(a) * 6, center[1] + math.cos(a) * 6
    d.line([x0, y0, x1, y1], fill=color, width=width)
    d.ellipse([center[0] - 4, center[1] - 4, center[0] + 4, center[1] + 4], fill=(30, 36, 42, 255), outline=(0, 0, 0, 255))


def load(name):
    return Image.open(os.path.join(OUT, name)).convert("RGBA")


def mock(screen_path, out_path):
    scr = Image.open(screen_path).convert("RGBA")  # 900x720
    SW, SH = scr.size
    rail = RAIL_W
    BW, BH = SW + 2 * BODY_L, SH + BODY_T + BODY_B
    CW, CH = BW + 2 * rail, BH + 36
    canvas = Image.new("RGBA", (CW, CH), (11, 13, 14, 255))
    ox, oy = rail, 36 - 8  # body offset; room above for the antenna
    # rails with brackets
    rl = load("Deck_Rail.png")
    for side_x in (2, CW - rail - 2):
        rr = nine_slice(rl, (rail, 520), 0, RAIL_CAP, 0, RAIL_CAP)
        canvas.alpha_composite(rr, (side_x, oy + 150))
    br = load("Deck_Bracket.png")
    for by in (oy + 170, oy + 150 + 520 - 34):
        canvas.alpha_composite(br.crop((0, 0, 30, 18)), (ox - 12, by))
        canvas.alpha_composite(br, (ox + BW - 18, by))
    # antenna
    canvas.alpha_composite(load("Deck_Antenna.png"), (ox + BW - 120, oy - 30))
    # body
    body = nine_slice(load("Deck_Body.png"), (BW, BH), BODY_L, BODY_T, BODY_R, BODY_B)
    canvas.alpha_composite(body, (ox, oy))
    # screen + frame + glass
    sx, sy = ox + BODY_L, oy + BODY_T
    canvas.alpha_composite(scr, (sx, sy))
    glass = load("Deck_Glass.png").resize((SW, SH), Image.LANCZOS)
    canvas.alpha_composite(glass, (sx, sy))
    sf = nine_slice(load("Deck_ScreenFrame.png"), (SW + 28, SH + 28), 14, 14, 14, 14)
    canvas.alpha_composite(sf, (sx - 14, sy - 14))
    # bumpers
    for nm, (bx, by) in (("TL", (ox, oy)), ("TR", (ox + BW - BUMPER_W, oy)), ("BL", (ox, oy + BH - BUMPER_BH)), ("BR", (ox + BW - BUMPER_W, oy + BH - BUMPER_BH))):
        canvas.alpha_composite(load(f"Deck_Bumper_{nm}.png"), (bx, by))
    # top: gauges, tuner, plate, leds
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 11)
    small = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 9)
    d = ImageDraw.Draw(canvas)
    gy = oy + 5
    for gx, face, bez, val in ((ox + 100, "Deck_GaugeFace_Heat.png", "Deck_GaugeBezel.png", 0.58), (ox + 158, "Deck_GaugeFace_Net.png", "Deck_GaugeBezel.png", 0.62)):
        f = load(face).resize((46, 46), Image.LANCZOS)
        b = load(bez).resize((46, 46), Image.LANCZOS)
        canvas.alpha_composite(f, (gx, gy))
        needle = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        ang = -135 + 270 * val
        rotate_needle(needle, (gx + 23, gy + 23), ang, 15, (255, 138, 60, 255), 2)
        canvas.alpha_composite(needle)
        canvas.alpha_composite(b, (gx, gy))
    d.text((ox + 100, gy + 47), "HEAT", font=small, fill=(150, 160, 170, 255))
    d.text((ox + 160, gy + 47), "NET", font=small, fill=(150, 160, 170, 255))
    # tuner
    tx, ty, tw, th = ox + 240, oy + 18, 220, 20
    d.rounded_rectangle([tx, ty, tx + tw, ty + th], 3, fill=(8, 14, 11, 255), outline=(6, 8, 10, 255))
    for i in range(0, tw, 7):
        d.line([tx + i, ty + 4, tx + i, ty + th - 4 if i % 35 else ty + th - 2], fill=(58, 143, 94, 150))
    d.rectangle([tx + 120, ty + 1, tx + 122, ty + th - 1], fill=(255, 106, 60, 255))
    # plate
    pl = nine_slice(load("Deck_Plate.png"), (250, 26), 8, 8, 8, 8)
    canvas.alpha_composite(pl, (ox + 490, oy + 15))
    d.text((ox + 504, oy + 22), "NETRUN // CYBERDECK MK-II", font=font, fill=(154, 166, 176, 255))
    # leds
    for i, nm in enumerate(("Green", "Orange", "Off")):
        canvas.alpha_composite(load(f"Deck_Led_{nm}.png").resize((16, 16), Image.LANCZOS), (ox + 770 + i * 20, oy + 20))
    # bottom: vent, keys, knobs
    by = oy + BH - BODY_B + 14
    canvas.alpha_composite(load("Deck_Vent.png"), (ox + 110, by + 4))
    for i, (nm, label) in enumerate((("Deck_Key.png", "<"), ("Deck_Key_Hot.png", "o"), ("Deck_Key.png", "?"))):
        k = load(nm)
        kx = ox + BW // 2 - 110 + i * 78
        canvas.alpha_composite(k, (kx, by + 6))
        d.text((kx + 32, by + 20), label, font=font, fill=(20, 24, 28, 255) if i == 1 else (155, 255, 190, 255), anchor="mm")
    sock = load("Deck_KnobSocket.png")
    knob = load("Deck_Knob.png")
    kc = [(ox + BW - 250, by + 20), (ox + BW - 160, by + 20)]
    labels = [(["SHADOW", "NOTICED", "TRACKED", "TARGET", "RED"], [(47, 196, 107), (230, 196, 49), (255, 138, 60), (255, 74, 60), (179, 18, 42)]), (["OFF", "ON"], [(122, 133, 144), (49, 196, 106)])]
    for (cx, cy), (names, cols), cur in zip(kc, labels, (2, 1)):
        canvas.alpha_composite(sock.resize((72, 72), Image.LANCZOS), (cx - 36, cy - 36))
        canvas.alpha_composite(knob.resize((46, 46), Image.LANCZOS), (cx - 23, cy - 23))
        n = len(names)
        angs = [-120 + 240 * i / (n - 1) for i in range(n)] if n > 2 else [-50, 40]
        for a, c in zip(angs, cols):
            ra = math.radians(a)
            d.line([cx + math.sin(ra) * 28, cy - math.cos(ra) * 28, cx + math.sin(ra) * 33, cy - math.cos(ra) * 33], fill=c + (255,), width=2)
        needle = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        rotate_needle(needle, (cx, cy), angs[cur], 19, (255, 138, 60, 255), 3)
        canvas.alpha_composite(needle)
    canvas.convert("RGB").save(out_path)


if __name__ == "__main__":
    if "--mock" in sys.argv:
        i = sys.argv.index("--mock")
        if not os.path.exists(os.path.join(OUT, "Deck_Body.png")):
            build()
        mock(sys.argv[i + 1], sys.argv[i + 2])
    else:
        build()
        print("textures written to", OUT)
