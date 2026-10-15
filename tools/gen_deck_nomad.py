#!/usr/bin/env python3
"""
Textures of the Nomad deck theme (docs/mockups/37g-theme-nomad.png): a dusty sand road case with a solar panel
opened behind it, a paracord handle below, a battery pouch strapped to the right side and a bone charm on the left.

    python tools/gen_deck_nomad.py                 # textures into Textures/UI/Netrun/Frame/Nomad/
    python tools/gen_deck_nomad.py --mock OUT.png  # a composite at the default terminal size, as the game lays it out

The layout numbers below are mirrored in Source/Terminal/DeckNomad.cs; change both together.
"""
import math
import sys

import numpy as np
from PIL import Image, ImageDraw

import deck_art as A

T = "Nomad"

# ---- the case: a 9-slice, screen inset and slice borders (px) ----
CASE_W, CASE_H = 1500, 1000
IN_L, IN_T, IN_R, IN_B = 24, 60, 24, 72
SLICE = 170
RADIUS = 20
# ---- where the case sits in the window: room for the panel, charm, pouch and handle ----
OUT_L, OUT_T, OUT_R, OUT_B = 42, 50, 80, 32

PAINT_TOP, PAINT_BOT = A.hexc(0x5a4f37), A.hexc(0x3b3325)
METAL = A.hexc(0x2a2924)
STEEL = A.hexc(0x3a3b35)
RUBBER = A.hexc(0x15130f)
BRASS = A.hexc(0x8a6d3c)


def paint(w, h, seed):
    """Sand paint over steel: a top-to-bottom tone, dusty mottling, fine grain."""
    rgb = A.gradient_v(w, h, PAINT_TOP, PAINT_BOT)
    tint = np.array([1.0, 0.94, 0.78], dtype=np.float32)
    rgb += (A.fbm(w, h, 220, 5, seed) * 26)[..., None] * tint
    dust = np.clip(A.fbm(w, h, 70, 3, seed + 5) * 3.2 - 0.15, 0, 1)
    rgb += (dust * 12)[..., None] * tint
    rgb += A.grain(w, h, seed + 9, 3.5)[..., None]
    return rgb


def scratches(w, h, count, seed, area=None, length=(8, 55)):
    """Thin scratch lines as a float mask, mostly along the length of the case."""
    r = A.rng(seed)

    def draw(d, s):
        for _ in range(count):
            x, y = r.integers(0, w), r.integers(0, h)
            if area is not None and area[min(h - 1, y), min(w - 1, x)] < 0.5:
                continue
            ln = r.integers(*length)
            ang = r.normal(0, 0.35) + (math.pi / 2 if r.random() < 0.12 else 0)
            x2, y2 = x + math.cos(ang) * ln, y + math.sin(ang) * ln
            d.line([x * s, y * s, x2 * s, y2 * s], fill=int(r.integers(90, 200)), width=s)
    return A.mask((w, h), draw)


def make_case():
    W, H = CASE_W, CASE_H
    outer = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], RADIUS * s, fill=255))
    hole_box = (IN_L, IN_T, W - IN_R, H - IN_B)

    def grown(g):
        return A.mask((W, H), lambda d, s: d.rounded_rectangle(
            [(hole_box[0] - g) * s, (hole_box[1] - g) * s, (hole_box[2] + g) * s - 1, (hole_box[3] + g) * s - 1], max(1, g) * s, fill=255))
    hole = grown(0)
    gasket = grown(8)
    steel = grown(11)
    frame = outer * (1 - steel)

    rgb = paint(W, H, 11)
    # grime: darker towards the bottom bar, in the crease around the screen and in the corners
    ys = np.linspace(0, 1, H, dtype=np.float32)[:, None]
    rgb *= (1 - 0.16 * np.clip((ys - 0.82) / 0.18, 0, 1))[..., None]
    crease = np.clip(A.blur(steel, 6) - steel, 0, 1)
    rgb *= (1 - 0.55 * crease)[..., None]

    # paint worn off the outer edge and in chips: dark steel shows through
    edge = A.inner_edge(outer, 10)
    chips = np.clip((A.fbm(W, H, 9, 3, 21) + edge * 0.62 - 0.48) * 9, 0, 1) * frame
    worn_metal = METAL + A.grain(W, H, 23, 5)[..., None]
    rgb = rgb * (1 - chips[..., None]) + worn_metal * chips[..., None]
    rgb += (A.shade(chips, 0.8, light=(1, 1)) * 18)[..., None]
    # scratches: a light line with a dark one beside it
    sc = scratches(W, H, 520, 31, area=frame) * frame
    rgb += (sc * 30)[..., None]
    rgb -= (np.roll(sc, 1, 0) * (1 - sc) * 14)[..., None]

    # the outer bevel: lit rim top-left, dark bottom-right
    rgb += (A.shade(outer, 2.5) * 70)[..., None]
    rgb += (A.inner_edge(outer, 1.5) * 18)[..., None]

    # the screen surround: a steel lip, then a rubber gasket going down into the screen
    ring_steel = steel * (1 - gasket)
    steel_rgb = STEEL + A.streaks(W, H, "x", 6, 41)[..., None]
    rgb = rgb * (1 - ring_steel[..., None]) + steel_rgb * ring_steel[..., None]
    rgb += (A.shade(steel, 1.2) * 50 * ring_steel)[..., None]
    ring_rubber = gasket * (1 - hole)
    rub = RUBBER + A.grain(W, H, 43, 3)[..., None]
    rgb = rgb * (1 - ring_rubber[..., None]) + rub * ring_rubber[..., None]
    rgb += (A.shade(1 - hole, 2.0, light=(1, 1)) * 40 * ring_rubber)[..., None]

    img = A.to_img(rgb, outer * (1 - hole))

    # stamped seams on the top and bottom bars, near the corners (inside the slice corners: never stretched)
    seams = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(seams)
    for x in (150, W - 150):
        for (y0, y1) in ((8, IN_T - 14), (H - IN_B + 14, H - 8)):
            d.line([x, y0, x, y1], fill=(10, 9, 6, 110), width=1)
            d.line([x + 1, y0, x + 1, y1], fill=(255, 240, 200, 26), width=1)
    img.alpha_composite(seams)

    # steel corner guards with rivets
    for cx, cy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        guard = corner_guard()
        if cx:
            guard = guard.transpose(Image.FLIP_LEFT_RIGHT)
        if cy:
            guard = guard.transpose(Image.FLIP_TOP_BOTTOM)
        img.alpha_composite(guard, dest=(W - guard.width if cx else 0, H - guard.height if cy else 0))
    # clip the guards to the rounded case
    arr = np.asarray(img, dtype=np.float32).copy()
    arr[..., 3] = np.minimum(arr[..., 3], outer * 255 * (1 - hole) + 0)
    img = Image.fromarray(np.clip(arr, 0, 255).astype("uint8"), "RGBA")
    # small screws at the screen corners
    for sx, sy in ((IN_L - 2, IN_T - 2), (W - IN_R + 2, IN_T - 2), (IN_L - 2, H - IN_B + 2), (W - IN_R + 2, H - IN_B + 2)):
        A.over(img, A.rivet(7, A.hexc(0x55544a), seed=sx + sy), (sx - 13 - 5 if sx < W / 2 else sx + 13 - 5, sy - 13 - 5 if sy < H / 2 else sy + 13 - 5))
    return img


def corner_guard():
    """An L-shaped steel guard wrapping a corner, 64 px each way, two rivets."""
    n, t = 64, 18
    m = A.mask((n, n), lambda d, s: (d.rounded_rectangle([0, 0, n * s - 1, t * s], 5 * s, fill=255),
                                     d.rounded_rectangle([0, 0, t * s, n * s - 1], 5 * s, fill=255),
                                     d.rectangle([0, 0, t * s, t * s], fill=255)))
    rgb = A.hexc(0x30312c) + A.streaks(n, n, "x", 7, 51)[..., None]
    rgb += A.grain(n, n, 52, 4)[..., None]
    rgb += (A.shade(m, 1.4) * 70)[..., None]
    rgb -= (A.inner_edge(m, 1.0) * 10)[..., None]
    shadow = np.roll(np.roll(A.blur(m, 2.0), 2, 0), 2, 1) * 0.55
    img = A.to_img(np.zeros((n, n, 3), np.float32), shadow)
    img.alpha_composite(A.to_img(rgb, m))
    for (x, y) in ((44, 4), (4, 44)):
        A.over(img, A.rivet(9, A.hexc(0x6a685c), seed=x), (x - 2, y - 2))
    return img


def make_solar():
    """The flexible solar panel opened behind the case: a 3-slice along its width (borders 90)."""
    W, H = 1400, 66
    frame = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 7 * s, fill=255))
    cells_box = (8, 7, W - 8, H - 12)
    cells = A.mask((W, H), lambda d, s: d.rectangle([cells_box[0] * s, cells_box[1] * s, cells_box[2] * s - 1, cells_box[3] * s - 1], fill=255))
    rgb = paint(W, H, 61) * 0.92
    rgb += (A.shade(frame, 1.6) * 55)[..., None]
    # cells: dark blue, darker at the top (tilted back), a grid of cells with silver bus lines
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    t = np.clip((yy - cells_box[1]) / (cells_box[3] - cells_box[1]), 0, 1)
    cell_rgb = A.hexc(0x0b1018) + (A.hexc(0x16233a) - A.hexc(0x0b1018)) * t[..., None]
    cell_rgb += A.fbm(W, H, 40, 3, 62)[..., None] * 8
    gx = ((xx - cells_box[0]) % 46) < 1.6
    gy = ((yy - cells_box[1]) % 24) < 1.4
    bus = (((xx - cells_box[0]) % 46) > 14) & (((xx - cells_box[0]) % 46) < 15) | (((xx - cells_box[0]) % 46) > 31) & (((xx - cells_box[0]) % 46) < 32)
    cell_rgb = np.where((gx | gy)[..., None], A.hexc(0x05080d), cell_rgb)
    cell_rgb = np.where((bus & ~gy)[..., None], cell_rgb + 22, cell_rgb)
    # a soft sheen
    sheen = np.clip(1 - np.abs((xx - yy * 3.0) - W * 0.32) / 120, 0, 1) * 18
    cell_rgb += sheen[..., None] * np.array([0.7, 0.8, 1.0], np.float32)
    dust = np.clip(A.fbm(W, H, 30, 3, 64) * 3 + (t - 0.7) * 1.4, 0, 1) * 16
    cell_rgb += dust[..., None] * np.array([1.0, 0.92, 0.75], np.float32)
    rgb = rgb * (1 - cells[..., None]) + cell_rgb * cells[..., None]
    rgb -= (A.inner_edge(cells, 2) * 20)[..., None]
    img = A.to_img(rgb, frame)
    # labels and a clan sticker in the slice ends (never stretched)
    A.over(img, A.text_layer((90, 12), "PV 40W  FLEX", A.font(8, False), (110, 130, 155, 150)), (W - 84, H - 11))
    A.over(img, clan_sticker(30), (18, 16))
    return img


def clan_sticker(d):
    """A faded paper sticker: a wheel with a skull hub (the Blacktop clan)."""
    img = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    m = A.mask((d, d), lambda dr, s: dr.ellipse([0, 0, d * s - 1, d * s - 1], fill=255))
    rgb = A.hexc(0x8a7a55) + A.fbm(d, d, 8, 2, 71)[..., None] * 24
    img.alpha_composite(A.to_img(rgb, m * 0.8))
    dr = ImageDraw.Draw(img)
    c = d / 2
    dr.ellipse([c - d * 0.36, c - d * 0.36, c + d * 0.36, c + d * 0.36], outline=(34, 28, 20, 200), width=2)
    for k in range(6):
        a = k * math.pi / 3
        dr.line([c, c, c + math.cos(a) * d * 0.36, c + math.sin(a) * d * 0.36], fill=(34, 28, 20, 170), width=1)
    dr.ellipse([c - 4, c - 4, c + 4, c + 4], fill=(34, 28, 20, 220))
    return A.spray(img, 72, 0.9)


def make_hinge():
    W, H = 46, 18
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 2 * s, W * s - 1, (H - 2) * s], 6 * s, fill=255))
    yy = np.mgrid[0:H, 0:W][0].astype(np.float32)
    cyl = 1 - np.abs(yy - H / 2) / (H / 2)
    rgb = A.hexc(0x2e2f2a) + (cyl ** 0.6 * 60)[..., None] + A.grain(W, H, 81, 4)[..., None]
    for x in (15, 31):
        rgb[:, x:x + 1] *= 0.35
    img = A.to_img(rgb, m)
    return img


def make_handle():
    """Paracord handle: two ochre rubber grips bolted under the case, a braided cord between."""
    W, H = 340, 50
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    s = A.SS
    big = Image.new("RGBA", (W * s, H * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    p0, p1, p2 = (36, 22), (170, 62), (304, 22)
    pts = []
    for i in range(161):
        t = i / 160
        x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0]
        y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]
        pts.append((x, y))
    for i, (x, y) in enumerate(pts):
        r = 6.2
        col = (68, 76, 58, 255) if (i // 3) % 2 == 0 else (52, 59, 45, 255)
        d.ellipse([(x - r) * s, (y - r) * s, (x + r) * s, (y + r) * s], fill=col)
    for i in range(0, len(pts) - 2, 3):
        (x, y), (x2, y2) = pts[i], pts[i + 2]
        nx, ny = -(y2 - y), x2 - x
        ln = math.hypot(nx, ny) or 1
        nx, ny = nx / ln * 5, ny / ln * 5
        d.line([(x - nx) * s, (y - ny) * s, (x2 + nx * 0.2) * s, (y2 + ny * 0.2) * s], fill=(30, 34, 26, 200), width=s)
    cord = big.resize((W, H), Image.LANCZOS)
    a = np.asarray(cord, dtype=np.float32)
    sh = A.shade(a[..., 3] / 255, 1.5) * 45
    rgb = a[..., :3] + sh[..., None]
    shadow = np.roll(A.blur(a[..., 3] / 255, 2.5), 3, 0) * 0.5
    img.alpha_composite(A.to_img(np.zeros_like(rgb), shadow))
    img.alpha_composite(A.to_img(rgb, a[..., 3] / 255))
    for gx in (14, W - 58):
        img.alpha_composite(grip(), dest=(gx, 0))
    return img


def grip():
    W, H = 44, 32
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 8 * s, fill=255))
    rgb = A.hexc(0x7a6a30) + A.fbm(W, H, 10, 2, 91)[..., None] * 18 + A.grain(W, H, 92, 4)[..., None]
    yy = np.mgrid[0:H, 0:W][0]
    for y in (9, 15, 21):
        rgb[y:y + 1, 6:W - 6] *= 0.62
        rgb[y + 1:y + 2, 6:W - 6] += 14
    rgb += (A.shade(m, 1.6) * 60)[..., None]
    img = A.to_img(rgb, m)
    A.over(img, A.rivet(8, A.hexc(0x5a584e), seed=93), (W / 2 - 6, 2))
    return img


def make_pouch():
    """The battery pouch strapped to the right side; the straps' left ends run under the case edge."""
    W, H = 104, 292
    body_box = (26, 4, 100, 286)
    body = A.mask((W, H), lambda d, s: d.rounded_rectangle([body_box[0] * s, body_box[1] * s, body_box[2] * s - 1, body_box[3] * s - 1], 12 * s, fill=255))
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    weave = (np.sin(xx * 2.1) * np.sin(yy * 2.1)) * 4
    rgb = A.hexc(0x3d4030) + weave[..., None] + A.fbm(W, H, 26, 3, 101)[..., None] * 16 + A.grain(W, H, 102, 3)[..., None]
    flap = A.mask((W, H), lambda d, s: d.rounded_rectangle([body_box[0] * s, body_box[1] * s, body_box[2] * s - 1, 78 * s], 12 * s, fill=255))
    rgb += (flap * 7)[..., None]
    under_flap = np.clip(A.blur(flap, 3) - flap, 0, 1)
    rgb *= (1 - 0.6 * under_flap)[..., None]
    rgb += (A.shade(body, 2.0) * 50)[..., None]
    rgb += (A.shade(flap, 1.2) * 30 * flap)[..., None]
    shadow = np.roll(np.roll(A.blur(body, 5), 6, 0), -3, 1) * 0.7
    img = A.to_img(np.zeros((H, W, 3), np.float32), shadow)
    img.alpha_composite(A.to_img(rgb, body))
    d = ImageDraw.Draw(img)
    # stitching
    for x in range(body_box[0] + 10, body_box[2] - 8, 7):
        d.line([x, body_box[3] - 6, x + 3, body_box[3] - 6], fill=(112, 112, 84, 200))
        d.line([x, 72, x + 3, 72], fill=(112, 112, 84, 200))
    for y in range(body_box[1] + 12, body_box[3] - 10, 7):
        d.line([body_box[2] - 6, y, body_box[2] - 6, y + 3], fill=(112, 112, 84, 180))
    # snap button and the label
    A.over(img, A.rivet(11, BRASS, seed=104), (56, 54))
    lab = Image.new("RGBA", (40, 16), (150, 140, 110, 210))
    ImageDraw.Draw(lab).text((20, 8), "CELL 2", font=A.font(8), fill=(40, 34, 24, 230), anchor="mm")
    A.over(img, lab, (44, 150))
    # straps of black webbing with steel buckles
    for y0 in (96, 214):
        strap = A.mask((W, H), lambda d, s, y0=y0: d.rectangle([0, y0 * s, W * s - 1, (y0 + 22) * s - 1], fill=255))
        srgb = A.hexc(0x151513) + (((xx.astype(int) % 3) == 0) * 6)[..., None] + A.grain(W, H, 105, 3)[..., None]
        srgb += (A.shade(strap, 1.2) * 40)[..., None]
        img.alpha_composite(A.to_img(srgb, strap))
        bk = Image.new("RGBA", (26, 32), (0, 0, 0, 0))
        bm = A.mask((26, 32), lambda d, s: d.rounded_rectangle([0, 0, 26 * s - 1, 32 * s - 1], 4 * s, outline=255, width=4 * s))
        brgb = A.hexc(0x6f6a58) + (A.shade(bm, 1.0) * 70)[..., None]
        bk.alpha_composite(A.to_img(brgb, bm))
        A.over(img, bk, (54, y0 - 5))
    # the power cable from the pouch into the case
    s = A.SS
    big = Image.new("RGBA", (W * s, H * s), (0, 0, 0, 0))
    bd = ImageDraw.Draw(big)
    pts = [(40, 270), (30, 282), (16, 276), (14, 262), (24, 256), (10, 250), (0, 252)]
    bd.line([(x * s, y * s) for x, y in pts], fill=(16, 16, 14, 255), width=6 * s, joint="curve")
    bd.line([(x * s, (y - 1.5) * s) for x, y in pts], fill=(60, 60, 54, 255), width=1 * s, joint="curve")
    img.alpha_composite(big.resize((W, H), Image.LANCZOS))
    return img


def make_charm():
    """A bone charm on a leather cord, tied to an eyelet on the case's left edge."""
    W, H = 58, 196
    s = A.SS
    big = Image.new("RGBA", (W * s, H * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    cord = [(52, 8), (46, 22), (36, 40), (30, 60), (29, 76)]
    d.line([(x * s, y * s) for x, y in cord], fill=(88, 66, 42, 255), width=3 * s, joint="curve")
    d.ellipse([24 * s, 72 * s, 35 * s, 82 * s], fill=(72, 54, 34, 255))
    d.ellipse([23 * s, 84 * s, 35 * s, 96 * s], fill=(47, 98, 92, 255))
    d.ellipse([24 * s, 98 * s, 34 * s, 108 * s], fill=(110, 46, 36, 255))
    fang = [(18, 112), (40, 112), (39, 126), (35, 146), (30, 168), (27, 186), (24, 168), (20, 146), (17, 126)]
    d.polygon([(x * s, y * s) for x, y in fang], fill=(185, 172, 140, 255))
    img = big.resize((W, H), Image.LANCZOS)
    a = np.asarray(img, dtype=np.float32)
    alpha = a[..., 3] / 255
    rgb = a[..., :3] + (A.shade(alpha, 1.3) * 60)[..., None] + A.grain(W, H, 111, 4)[..., None]
    yy = np.mgrid[0:H, 0:W][0]
    rgb *= (1 - 0.25 * np.clip((yy - 140) / 50, 0, 1) * (yy > 112))[..., None]
    shadow = np.roll(np.roll(A.blur(alpha, 2.0), 3, 0), 3, 1) * 0.55
    out = A.to_img(np.zeros_like(rgb), shadow)
    out.alpha_composite(A.to_img(rgb, alpha))
    dd = ImageDraw.Draw(out)
    for y in (128, 138, 148):
        dd.line([24, y, 33, y - 2], fill=(70, 58, 40, 200), width=1)
    A.over(out, A.rivet(10, A.hexc(0x5e5c50), seed=112, dome=False), (45, 1))
    return out


def make_key(base, seed):
    """A worn rubber key, 44 x 32: a face and a darker side under it; the middle rubbed lighter."""
    W, H = 44, 32
    side = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 3 * s, W * s - 1, H * s - 1], 7 * s, fill=255))
    face = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, (H - 4) * s], 7 * s, fill=255))
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    rgb = np.broadcast_to(base * 0.55, (H, W, 3)).copy()
    frgb = base + ((1 - yy / H) * 14)[..., None] + A.fbm(W, H, 8, 2, seed)[..., None] * 12 + A.grain(W, H, seed + 1, 4)[..., None]
    rub = np.clip(1 - np.hypot((xx - W / 2) / (W * 0.36), (yy - H * 0.42) / (H * 0.34)), 0, 1)
    frgb += (rub * 16)[..., None]
    frgb += (A.shade(face, 1.4) * 55)[..., None]
    rgb = rgb * (1 - face[..., None]) + frgb * face[..., None]
    alpha = np.maximum(side, face)
    shadow = np.roll(A.blur(alpha, 1.5), 2, 0) * 0.55
    img = A.to_img(np.zeros_like(rgb), shadow)
    img.alpha_composite(A.to_img(rgb, alpha))
    return img


def make_led(color):
    """A lamp in a brass bezel, 22 px: off is a dark lens, on is lit with a hot core (the glow is drawn in code)."""
    n = 22
    img = A.rivet(18, BRASS, seed=121, dome=False)
    img = img.resize((n, n), Image.LANCZOS)
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    lens = A.mask((n, n), lambda d, s: d.ellipse([5 * s, 5 * s, 17 * s - 1, 17 * s - 1], fill=255))
    r = np.hypot(xx - 10.5, yy - 10.5) / 6
    if color is None:
        rgb = A.hexc(0x16130e) + ((1 - r) * 10)[..., None]
    else:
        c = A.hexc(color)
        rgb = c * (0.55 + 0.45 * np.clip(1 - r, 0, 1))[..., None] + (np.clip(1 - r * 1.8, 0, 1) * 90)[..., None]
    hl = np.clip(1 - np.hypot(xx - 8.5, yy - 8.0) / 2.2, 0, 1) * 120
    rgb += hl[..., None]
    img.alpha_composite(A.to_img(rgb, lens))
    return img


def make_window(w, h, rim, inner_top, inner_bot, rim_w, radius, seed):
    """A small window with a rim (brass or rubber) around dark glass, as a 9-slice."""
    outer = A.mask((w, h), lambda d, s: d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], radius * s, fill=255))
    inner = A.mask((w, h), lambda d, s: d.rounded_rectangle([rim_w * s, rim_w * s, (w - rim_w) * s - 1, (h - rim_w) * s - 1], max(1, radius - rim_w) * s, fill=255))
    rgb = rim + A.grain(w, h, seed, 4)[..., None] + (A.shade(outer, 1.2) * 60)[..., None]
    glass = A.gradient_v(w, h, inner_top, inner_bot) + A.grain(w, h, seed + 1, 2)[..., None]
    glass -= (A.inner_edge(inner, 3) * 18)[..., None]
    glass[:, :, :] += 0
    rgb = rgb * (1 - inner[..., None]) + glass * inner[..., None]
    yy = np.mgrid[0:h, 0:w][0]
    refl = inner * ((yy > rim_w + 1) & (yy < rim_w + 3)) * 10
    rgb += refl[..., None]
    return A.to_img(rgb, outer)


def make_patch():
    """The maker's plate: a stitched leather patch (9-slice, borders 16)."""
    W, H = 240, 48
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 6 * s, fill=255))
    rgb = A.hexc(0x4a3020) + A.fbm(W, H, 12, 3, 131)[..., None] * 22 + A.grain(W, H, 132, 5)[..., None]
    rgb -= (A.inner_edge(m, 5) * 24)[..., None]
    rgb += (A.shade(m, 1.4) * 40)[..., None]
    img = A.to_img(rgb, m)
    d = ImageDraw.Draw(img)
    for x in range(10, W - 10, 7):
        d.line([x, 5, x + 3, 5], fill=(170, 140, 96, 210))
        d.line([x, H - 6, x + 3, H - 6], fill=(170, 140, 96, 210))
    for y in range(9, H - 8, 7):
        d.line([5, y, 5, y + 3], fill=(170, 140, 96, 210))
        d.line([W - 6, y, W - 6, y + 3], fill=(170, 140, 96, 210))
    return img


def make_dpad():
    """A compact rubber cross pad, 72 x 48 (fits the bottom bar): left arm BACK, right arm HELP, a hole in the middle
    for the brass CORE button."""
    W, H = 72, 48
    m = A.mask((W, H), lambda d, s: (d.rounded_rectangle([0, 15 * s, W * s - 1, 33 * s - 1], 5 * s, fill=255),
                                     d.rounded_rectangle([27 * s, 0, 45 * s - 1, H * s - 1], 5 * s, fill=255)))
    hole = A.mask((W, H), lambda d, s: d.ellipse([26 * s, 14 * s, 46 * s - 1, 34 * s - 1], fill=255))
    rgb = A.hexc(0x2c271d) + A.fbm(W, H, 10, 2, 141)[..., None] * 10 + A.grain(W, H, 142, 4)[..., None]
    rgb += (A.shade(m, 1.6) * 55)[..., None]
    rgb = rgb * (1 - hole[..., None]) + (A.hexc(0x0d0c09) + (A.shade(1 - hole, 1.4, light=(1, 1)) * 30)[..., None]) * hole[..., None]
    shadow = np.roll(A.blur(m, 2), 2, 0) * 0.55
    img = A.to_img(np.zeros_like(rgb), shadow)
    img.alpha_composite(A.to_img(rgb, m))
    d = ImageDraw.Draw(img)
    # embossed marks on the top and bottom arms (decoration)
    d.polygon([(36, 4), (32, 9), (40, 9)], fill=(20, 18, 13, 200))
    d.polygon([(36, 44), (32, 39), (40, 39)], fill=(20, 18, 13, 200))
    return img


def make_strap():
    """The Heat strap: a leather band with stitching and steel tips (3-slice, borders 20); rivets come from code."""
    W, H = 340, 38
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 9 * s, fill=255))
    rgb = A.gradient_v(W, H, A.hexc(0x4a2f1c), A.hexc(0x33200f)) + A.fbm(W, H, 14, 3, 151)[..., None] * 20 + A.grain(W, H, 152, 5)[..., None]
    rgb -= (A.inner_edge(m, 3) * 20)[..., None]
    rgb += (A.shade(m, 1.6) * 45)[..., None]
    img = A.to_img(rgb, m)
    d = ImageDraw.Draw(img)
    for x in range(22, W - 22, 7):
        d.line([x, 5, x + 3, 5], fill=(150, 118, 78, 200))
        d.line([x, H - 6, x + 3, H - 6], fill=(150, 118, 78, 200))
    for side in (0, 1):
        tip = A.mask((14, H), lambda dd, s: dd.rounded_rectangle([0, 0, 14 * s - 1, H * s - 1], 6 * s, fill=255))
        trgb = A.hexc(0x50504a) + (A.shade(tip, 1.0) * 70)[..., None] + A.grain(14, H, 153 + side, 4)[..., None]
        t = A.to_img(trgb, tip)
        A.over(img, t, (W - 14 if side else 0, 0))
    return img


def make_gem():
    """A glass bead set in the strap, white so code can tint it by the Heat level."""
    n = 16
    m = A.mask((n, n), lambda d, s: d.ellipse([1 * s, 1 * s, (n - 1) * s - 1, (n - 1) * s - 1], fill=255))
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    r = np.hypot(xx - 7.5, yy - 7.5) / 7
    rgb = np.full((n, n, 3), 200, np.float32) * (0.65 + 0.35 * np.clip(1 - r, 0, 1))[..., None]
    rgb += (np.clip(1 - np.hypot(xx - 5.5, yy - 5) / 2.4, 0, 1) * 55)[..., None]
    rgb -= (np.clip(r - 0.75, 0, 1) * 160)[..., None]
    return A.to_img(rgb, m)


def make_lever_plate():
    W, H = 160, 54
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 6 * s, fill=255))
    rgb = A.hexc(0x34352f) + A.streaks(W, H, "x", 7, 161)[..., None] + A.grain(W, H, 162, 3)[..., None]
    rgb += (A.shade(m, 1.6) * 60)[..., None]
    slot = A.mask((W, H), lambda d, s: d.rounded_rectangle([24 * s, 26 * s, 136 * s - 1, 34 * s - 1], 4 * s, fill=255))
    rgb = rgb * (1 - slot[..., None]) + (A.hexc(0x0a0a08) + (A.shade(1 - slot, 1.0, light=(1, 1)) * 30)[..., None]) * slot[..., None]
    shadow = np.roll(A.blur(m, 2), 2, 0) * 0.5
    img = A.to_img(np.zeros_like(rgb), shadow)
    img.alpha_composite(A.to_img(rgb, m))
    for x, y in ((7, 7), (W - 7, 7), (7, H - 7), (W - 7, H - 7)):
        A.over(img, A.rivet(6, A.hexc(0x5a5a50), seed=x * 3 + y), (x - 5, y - 5))
    f = A.font(8)
    for text, xy in (("NET LINK", (80, 45)), ("OFF", (26, 45)), ("ON", (134, 45))):
        A.over(img, A.text_layer((W, H), text, f, (240, 225, 190, 40), (xy[0], xy[1] + 1), "mm"), (0, 0))
        A.over(img, A.text_layer((W, H), text, f, (12, 11, 8, 190), xy, "mm"), (0, 0))
    return img


def make_lever(angle):
    """A short brass lever standing in the slot, on a 60 x 60 canvas with the pivot (in the slot) at (30, 48), tilted
    towards where it travels. Maxim, 2026-10-15: it slides along the slot instead of sticking out over the plate."""
    n = 60
    s = A.SS
    big = Image.new("RGBA", (n * s, n * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    d.rounded_rectangle([28 * s, 30 * s, 32 * s, 48 * s], 2 * s, fill=(150, 116, 60, 255))
    d.rounded_rectangle([29 * s, 30 * s, 30 * s, 48 * s], 1 * s, fill=(205, 168, 98, 255))
    d.ellipse([23 * s, 20 * s, 37 * s, 34 * s], fill=(160, 124, 64, 255))
    d.ellipse([26 * s, 22 * s, 31 * s, 27 * s], fill=(230, 200, 140, 255))
    d.ellipse([25 * s, 44 * s, 35 * s, 52 * s], fill=(40, 38, 32, 255))
    img = big.resize((n, n), Image.LANCZOS).rotate(angle, resample=Image.BICUBIC, center=(30, 48))
    a = np.asarray(img, dtype=np.float32)
    shadow = np.roll(np.roll(A.blur(a[..., 3] / 255, 2), 3, 0), 3, 1) * 0.5
    out = A.to_img(np.zeros((n, n, 3), np.float32), shadow)
    out.alpha_composite(img)
    return out


def make_tally():
    """Scratched tally marks: dives counted on the case."""
    W, H = 74, 24
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    x = 2
    r = A.rng(171)
    for g in range(3):
        for k in range(4):
            j = r.normal(0, 0.8)
            d.line([x + k * 4 + j, 4, x + k * 4 - j, 20], fill=(10, 9, 6, 120), width=1)
            d.line([x + k * 4 + 1 + j, 4, x + k * 4 + 1 - j, 20], fill=(225, 205, 160, 70), width=1)
        d.line([x - 2, 16, x + 16, 7], fill=(225, 205, 160, 70), width=1)
        x += 25
    return img


def make_stencil():
    f = A.font(13)
    img = A.text_layer((190, 22), "BLACKTOP CLAN", f, (205, 186, 140, 120), (2, 3))
    return A.spray(img, 181, 0.8)


def build():
    A.save(make_case(), T, "Case")
    A.save(make_solar(), T, "Solar")
    A.save(make_hinge(), T, "Hinge")
    A.save(make_handle(), T, "Handle")
    A.save(make_pouch(), T, "Pouch")
    A.save(make_charm(), T, "Charm")
    A.save(make_key(A.hexc(0x3d3629), 191), T, "Key")
    A.save(make_key(A.hexc(0x6a2a20), 192), T, "Key_Red")
    A.save(make_led(None), T, "Led_Off")
    A.save(make_led(0x3fae66), T, "Led_Green")
    A.save(make_led(0xd0782e), T, "Led_Orange")
    A.save(make_window(170, 32, BRASS * 0.85, A.hexc(0x100e0a), A.hexc(0x060504), 3, 6, 201), T, "Window")
    A.save(make_window(212, 56, RUBBER * 1.2, A.hexc(0x08150c), A.hexc(0x040b06), 7, 8, 202), T, "Lcd")
    A.save(make_patch(), T, "Patch")
    A.save(make_dpad(), T, "Dpad")
    A.save(A.rivet(16, BRASS * 1.05, seed=211), T, "Core")
    A.save(make_strap(), T, "Strap")
    A.save(A.rivet(14, BRASS * 0.9, seed=212), T, "Rivet")
    A.save(make_gem(), T, "Gem")
    A.save(make_lever_plate(), T, "LeverPlate")
    # seven frames from OFF (tilted left) to ON (tilted right): the lever glides along the slot
    for i in range(7):
        A.save(make_lever(30 - 10 * i), T, "Lever_%d" % i)
    A.save(make_tally(), T, "Tally")
    A.save(make_stencil(), T, "Stencil")
    # the settings card shows the whole deck small
    A.save(mock(None).resize((300, 240), Image.LANCZOS), T, "Preview")


# ------------------------------------------------------------------ a composite, the way DeckNomad.cs lays it out

def mock(out_path=None, W=1100, H=880):
    import os
    def load(n):
        return Image.open(os.path.join(A.FRAME, T, n + ".png")).convert("RGBA")
    img = Image.new("RGBA", (W, H), (12, 12, 14, 255))
    cx0, cy0, cx1, cy1 = OUT_L, OUT_T, W - OUT_R, H - OUT_B
    cw, ch = cx1 - cx0, cy1 - cy0
    solar = A.nine_slice(load("Solar"), (cw - 80, 66), 90, 0, 90, 0)
    A.over(img, solar, (cx0 + 40, 0))
    for hx in (cx0 + 110, cx1 - 156):
        A.over(img, load("Hinge"), (hx, cy0 - 8))
    A.over(img, load("Handle"), (cx0 + cw / 2 - 170, cy1 - 18))
    case = A.nine_slice(load("Case"), (cw, ch), SLICE, SLICE, SLICE, SLICE)
    A.over(img, case, (cx0, cy0))
    for hx in (cx0 + 110, cx1 - 156):
        A.over(img, load("Hinge"), (hx, cy0 - 8))
    sx0, sy0, sx1, sy1 = cx0 + IN_L, cy0 + IN_T, cx1 - IN_R, cy1 - IN_B
    ImageDraw.Draw(img).rectangle([sx0, sy0, sx1 - 1, sy1 - 1], fill=(6, 10, 8, 255))
    A.over(img, load("Charm"), (cx0 - 40, cy0 + 30))
    A.over(img, load("Pouch"), (cx1 - 26, cy0 + ch / 2 - 146))
    # top bar
    for i in range(4):
        A.over(img, load("Key_Red" if i == 0 else "Key"), (cx0 + 34 + i * 54, cy0 + 10))
    A.over(img, A.nine_slice(load("Window"), (170, 32), 10, 10, 10, 10), (cx0 + 262, cy0 + 10))
    A.over(img, A.nine_slice(load("Window"), (64, 32), 10, 10, 10, 10), (cx1 - 98, cy0 + 10))
    for i, led in enumerate(("Led_Green", "Led_Orange", "Led_Green")):
        A.over(img, load(led), (cx1 - 98 - 20 - 102 + 6 + i * 34, cy0 + 15))
    A.over(img, A.nine_slice(load("Patch"), (240, 48), 16, 16, 16, 16), (cx0 + 262 + 170 + ((cx1 - 220 - 20) - (cx0 + 432)) / 2 - 120, cy0 + 6))
    # bottom bar
    by = cy1 - IN_B
    A.over(img, A.nine_slice(load("Lcd"), (212, 56), 12, 12, 12, 12), (cx0 + 34, by + 8))
    A.over(img, load("Dpad"), (cx0 + 270, by + 12))
    A.over(img, load("Core"), (cx0 + 270 + 26, by + 12 + 14))
    strap = A.nine_slice(load("Strap"), (300, 38), 20, 0, 20, 0)
    A.over(img, strap, (cx0 + 430, by + 12))
    for i in range(10):
        A.over(img, load("Rivet"), (cx0 + 430 + 22 + i * 26.4, by + 21 - 2))
    A.over(img, load("Tally"), (cx0 + 760, by + 22))
    A.over(img, load("LeverPlate"), (cx1 - 34 - 160, by + 9))
    A.over(img, load("Lever_6"), (cx1 - 34 - 160 + 128 - 30, by + 9 + 30 - 48))
    if out_path:
        img.convert("RGB").save(out_path)
    return img


if __name__ == "__main__":
    if len(sys.argv) > 2 and sys.argv[1] == "--mock":
        mock(sys.argv[2])
    else:
        build()
        print("Nomad textures written")
