"""PROTO-X, the fixer's secret prototype deck: three looks to choose from (mockups only for now).
A  Carbon & Gold: black carbon weave, a brass trim, three breadboards under glass on the right, a ribbon cable, gold pins.
B  Open Board: the case is a bare prototype PCB, gold traces, chips, kapton tape, ENG SAMPLE marks, SMD LED Heat.
C  Black Glass: a smoked glass slab, a window into the guts along the top, capacitive ring keys, glass bead Heat.
Usage: python gen_deck_protox.py --variant A --mock OUT.png"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
import deck_art as A
import deck_stickers as S

OUT = 8
GOLD = (196, 160, 92)
GOLD_DIM = (120, 96, 56)
INK_G = (90, 230, 130)


def carbon(w, h, seed=0, base=(22, 22, 24)):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    cell = 6
    u, v = (xx // cell) % 2, (yy // cell) % 2
    fx, fy = (xx % cell) / cell, (yy % cell) / cell
    weave = np.where(u == v, np.sin(fx * math.pi), np.sin(fy * math.pi))
    rgb = np.array(base, np.float32) + (weave * 9)[..., None] + A.grain(w, h, seed, 2)[..., None]
    rgb += ((1 - yy / h) * 8)[..., None]
    return rgb


def pcb(w, h, seed=0):
    rgb = np.broadcast_to(np.array((14, 40, 28), np.float32), (h, w, 3)).copy()
    rgb += A.fbm(w, h, 60, 3, seed)[..., None] * np.array([6, 14, 8], np.float32)
    rgb += A.grain(w, h, seed + 1, 2)[..., None]
    return rgb


def glass(w, h, seed=0):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    rgb = np.broadcast_to(np.array((12, 13, 16), np.float32), (h, w, 3)).copy()
    rgb += (np.clip(1 - np.abs((xx - yy * 1.6) - w * 0.25) / (w * 0.12), 0, 1) * 10)[..., None]   # a soft diagonal sheen
    rgb += A.grain(w, h, seed, 1)[..., None]
    return rgb


def traces(d, x0, y0, x1, y1, rng, n, col, width=2):
    """Gold PCB traces: horizontal runs with 45-degree jogs and a via at each end."""
    for _ in range(n):
        y = rng.uniform(y0, y1)
        x = rng.uniform(x0, x1 - 120)
        pts = [(x, y)]
        for _ in range(rng.integers(2, 4)):
            x += rng.uniform(30, 120)
            pts.append((x, y))
            dy = rng.choice([-1, 1]) * rng.uniform(8, 22)
            if y0 < y + dy < y1:
                x += abs(dy)
                y += dy
                pts.append((x, y))
        if pts[-1][0] > x1:
            continue
        d.line(pts, fill=col, width=width)
        for p in (pts[0], pts[-1]):
            d.ellipse([p[0] - 3, p[1] - 3, p[0] + 3, p[1] + 3], outline=col, width=1, fill=(10, 10, 10))


def chip(d, x, y, w, h, label, font, pins=True):
    d.rectangle([x, y, x + w, y + h], fill=(18, 18, 20), outline=(60, 60, 64))
    if pins:
        for px in range(int(x) + 4, int(x + w) - 2, 6):
            d.rectangle([px, y - 3, px + 2, y], fill=(190, 190, 196))
            d.rectangle([px, y + h, px + 2, y + h + 3], fill=(190, 190, 196))
    d.text((x + w / 2, y + h / 2), label, font=font, fill=(150, 150, 156), anchor="mm")
    d.ellipse([x + 3, y + 3, x + 6, y + 6], fill=(70, 70, 74))


def breadboard(img, x, y, w, h, rng, font):
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([x, y, x + w, y + h], 6, fill=(30, 28, 24), outline=GOLD_DIM, width=2)
    d.rounded_rectangle([x + 6, y + 6, x + w - 6, y + h - 6], 3, fill=(214, 208, 192))
    for yy in range(int(y) + 14, int(y + h) - 10, 7):
        for xx in range(int(x) + 12, int(x + w) - 10, 7):
            d.rectangle([xx, yy, xx + 2, yy + 2], fill=(120, 116, 104))
    for _ in range(2):
        cx = rng.uniform(x + 20, x + w - 60)
        chip(d, cx, rng.uniform(y + 20, y + h - 30), 40, 14, "ICE-K", font)
    cols = [(220, 60, 60), (60, 140, 230), (240, 190, 40), (60, 190, 90)]
    for i in range(3):
        a = (rng.uniform(x + 14, x + w - 14), rng.uniform(y + 14, y + h - 14))
        b = (rng.uniform(x + 14, x + w - 14), rng.uniform(y + 14, y + h - 14))
        mid = ((a[0] + b[0]) / 2, min(a[1], b[1]) - rng.uniform(10, 26))
        pts = S.bez(a, mid, mid, b, 14)
        d.line(pts, fill=cols[i % 4], width=3)
    # glass over it
    sheen = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(sheen)
    sd.polygon([(x + w * 0.15, y + 6), (x + w * 0.45, y + 6), (x + w * 0.2, y + h - 6), (x + 6, y + h - 6), (x + 6, y + h * 0.6)], fill=(255, 255, 255, 22))
    img.alpha_composite(sheen)


def mock(v, out_path, screen=(930, 666), heat=58, online=True):
    rng = np.random.default_rng(7)
    side = 170 if v == "A" else 0
    IN_L, IN_T, IN_R, IN_B = (44, 92, 44 + side, 100) if v != "C" else (40, 112, 40, 96)
    W, H = screen[0] + IN_L + IN_R + 2 * OUT, screen[1] + IN_T + IN_B + 2 * OUT
    img = Image.new("RGBA", (W, H), (14, 14, 16, 255))
    bx0, by0, bx1, by1 = OUT, OUT, W - OUT, H - OUT
    bw, bh = bx1 - bx0, by1 - by0
    m = A.mask((bw, bh), lambda d, s: d.rounded_rectangle([0, 0, bw * s - 1, bh * s - 1], 18 * s, fill=255))
    if v == "A":
        rgb = carbon(bw, bh, 11)
    elif v == "B":
        rgb = pcb(bw, bh, 21)
    else:
        rgb = glass(bw, bh, 31)
    rgb += (A.shade(m, 2.5) * 40)[..., None]
    rgb -= (A.inner_edge(m, 1.5) * 30)[..., None]
    body = A.to_img(rgb, m)
    img.alpha_composite(body, (bx0, by0))
    d = ImageDraw.Draw(img)
    f8, f9, f11, f13 = A.font(8, False), A.font(9, False), A.font(11), A.font(13)
    mono = A.font(12, False)
    sx0, sy0, sx1, sy1 = bx0 + IN_L, by0 + IN_T, bx1 - IN_R, by1 - IN_B

    if v == "B":   # traces and parts all over the board, the screen on a black carrier
        traces(d, bx0 + 10, by0 + 10, bx1 - 10, sy0 - 12, rng, 10, (176, 146, 70))
        traces(d, bx0 + 10, sy1 + 12, bx1 - 10, by1 - 10, rng, 8, (176, 146, 70))
        for x in (bx0 + 14, bx1 - 30):
            for k in range(14):
                y = sy0 + 20 + k * 44
                if y > sy1 - 20:
                    break
                d.rectangle([x, y, x + 16, y + 8], fill=(160, 120, 60) if k % 2 else (40, 40, 44))
        kap = S.tape(110, 22, color=(214, 140, 40), alpha=0.75, seed=3)
        img.alpha_composite(kap, (bx0 + 300, by0 + 6))
        d = ImageDraw.Draw(img)
        d.text((bx0 + 355, by0 + 17), "ENG SAMPLE", font=f9, fill=(40, 20, 0), anchor="mm")
        for i, t in enumerate(("PROTO-X REV C", "NOT FOR SALE", "U1  U2  U3")):
            d.text((bx1 - 220, by1 - 70 + i * 14), t, font=f8, fill=(220, 220, 210))
    if v == "C":   # a window into the guts along the top: boards, a fan, gold
        wx0, wy0, wx1, wy1 = bx0 + 300, by0 + 14, bx1 - 300, by0 + 62
        guts = pcb(wx1 - wx0, wy1 - wy0, 41)
        gi = A.to_img(guts, np.ones((wy1 - wy0, wx1 - wx0), np.float32))
        gd = ImageDraw.Draw(gi)
        traces(gd, 0, 4, wx1 - wx0, wy1 - wy0 - 4, rng, 6, (176, 146, 70))
        for k in range(4):
            chip(gd, 30 + k * 90, 16, 50, 16, "X%d" % k, f8)
        fx = (wx1 - wx0) - 40
        gd.ellipse([fx - 18, 6, fx + 18, 42], fill=(20, 20, 22), outline=(80, 80, 84))
        for k in range(7):
            a = k * math.tau / 7
            gd.line([fx, 24, fx + 15 * math.cos(a), 24 + 15 * math.sin(a)], fill=(70, 70, 76), width=3)
        img.alpha_composite(gi, (wx0, wy0))
        smoke = Image.new("RGBA", img.size, (0, 0, 0, 0))
        ImageDraw.Draw(smoke).rounded_rectangle([wx0, wy0, wx1, wy1], 8, fill=(10, 10, 14, 120), outline=(90, 90, 100, 255), width=2)
        img.alpha_composite(smoke)
        d = ImageDraw.Draw(img)

    # screen with a gold (A, B) or chrome (C) edge
    trim = GOLD if v != "C" else (150, 150, 160)
    d.rounded_rectangle([sx0 - 6, sy0 - 6, sx1 + 6, sy1 + 6], 10, fill=(8, 8, 10), outline=trim, width=2)
    d.rounded_rectangle([sx0, sy0, sx1, sy1], 6, fill=(10, 16, 12))
    for i, (t, c) in enumerate((("NETRUN // NETWORK CORE", INK_G), ("> HOME NODE ........ ONLINE", INK_G),
                                ("> GATEWAY .......... 5 NODES", INK_G), ("> HEAT 58 .......... TRACKED", (230, 190, 70)))):
        d.text((sx0 + 30, sy0 + 28 + i * 22), t, font=mono, fill=c)
    for r in (60, 120):
        d.ellipse([sx1 - 230 - r, (sy0 + sy1) / 2 - r, sx1 - 230 + r, (sy0 + sy1) / 2 + r], outline=(40, 120, 70))
    d.rounded_rectangle([bx0 + 4, by0 + 4, bx1 - 4, by1 - 4], 15, outline=trim + (180,), width=1)

    if v == "A":   # the breadboard column and a ribbon cable out of the side, two gold pins on top
        cx0 = sx1 + 22
        ch = (sy1 - sy0 - 20) / 3
        for k in range(3):
            breadboard(img, cx0, sy0 + k * (ch + 10), bx1 - 16 - cx0, ch, rng, f8)
        d = ImageDraw.Draw(img)
        for k in range(7):
            col = tuple(int(c) for c in np.ravel(S.hsv2rgb(np.float32(k / 7), np.float32(0.45), np.float32(0.8)))[:3])
            d.line([(bx1 - 2, sy0 + 120 + k * 4), (W - 1, sy0 + 140 + k * 4)], fill=col, width=4)

    # top: badge, keys, lamps
    ty = by0 + 22
    if v == "A":
        d.rounded_rectangle([sx0 + 300, ty - 6, sx0 + 560, ty + 40], 4, fill=(10, 10, 10), outline=GOLD, width=1)
        d.text((sx0 + 430, ty + 10), "PROTO-X  //  NOT FOR SALE", font=f13, fill=GOLD, anchor="mm")
        d.text((sx0 + 430, ty + 28), "UNIT 001  ·  WARRANTY VOID", font=f8, fill=GOLD_DIM, anchor="mm")
    elif v == "B":
        d.text((bx0 + 40, ty + 14), "PROTO-X", font=A.font(26), fill=(230, 220, 190), anchor="lm")
        d.text((bx0 + 40, ty + 38), "FIXER BUILD  ·  REV C", font=f8, fill=(176, 146, 70), anchor="lm")
    else:
        d.text((bx0 + 40, by0 + 38), "PROTO‑X", font=A.font(24, False), fill=(220, 220, 228), anchor="lm")
        holo = S.holo_rgb(90, 20, 5)
        hi = A.to_img(holo, np.ones((20, 90), np.float32) * 0.55)
        img.alpha_composite(hi, (bx0 + 150, by0 + 28))
        d = ImageDraw.Draw(img)
        d.text((bx0 + 195, by0 + 38), "SER 0001", font=f8, fill=(20, 20, 24), anchor="mm")
    keys = ("CLOSE", "MENU", "ACHIEV", "LOCK")
    kx0 = sx0 if v == "A" else (bx1 - 560 if v == "B" else bx1 - 520)
    ky = ty if v != "C" else by0 + 70
    for i, k in enumerate(keys):
        x = kx0 + i * 58
        if v == "A":
            d.ellipse([x, ky - 4, x + 40, ky + 36], fill=(16, 16, 18), outline=GOLD, width=2)
        elif v == "B":
            d.rectangle([x, ky, x + 40, ky + 28], fill=(30, 30, 32), outline=(150, 150, 156))
            d.ellipse([x + 15, ky + 9, x + 25, ky + 19], fill=(60, 60, 64))
        else:
            d.ellipse([x + 4, ky - 2, x + 36, ky + 30], outline=(120, 220, 230), width=2)
        d.text((x + 20, ky + 46 if v != "C" else ky + 42), k, font=f8, fill=GOLD if v != "C" else (170, 170, 180), anchor="mm")
    lx = kx0 + 4 * 58 + 20
    for i, (c, name) in enumerate((((70, 220, 120), "NET"), ((240, 150, 50), "SICK"), ((60, 60, 60), "POD"))):
        cx, cy = lx + i * 34, ky + 14
        if i < 2:
            A.glow(img, (cx - 3, cy - 3, cx + 3, cy + 3), c, 0.6, 5)
        d = ImageDraw.Draw(img)
        d.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], fill=c)
        d.text((cx, ky + 46 if v != "C" else ky + 42), name, font=f8, fill=GOLD if v != "C" else (170, 170, 180), anchor="mm")
    if v != "C":
        for k in range(2):
            px = bx1 - 160 + k * 28
            d.rectangle([px, 0, px + 7, by0 + 4], fill=GOLD)
            d.rectangle([px + 1, 0, px + 3, by0 + 4], fill=(240, 214, 150))

    # bottom: LCD, keys, Heat, NET LINK touch slider
    yb = by1 - IN_B
    lcd = (bx0 + 70, yb + 22, 230, 58)
    d.rounded_rectangle([lcd[0], lcd[1], lcd[0] + lcd[2], lcd[1] + lcd[3]], 4, fill=(6, 14, 8), outline=trim, width=1)
    for i, t in enumerate(("CYBERDATA 491", "HEAT 58 ▲ TRACKED", "D8 08:27  NET ON")):
        d.text((lcd[0] + 12, lcd[1] + 8 + i * 15), t, font=mono, fill=INK_G)
    bx = lcd[0] + lcd[2] + 40
    for i, (g, name) in enumerate((("◁", "BACK"), ("○", "CORE"), ("?", "HELP"))):
        x = bx + i * 58
        if v == "C":
            d.ellipse([x, yb + 26, x + 40, yb + 66], outline=(120, 220, 230), width=2)
        else:
            d.ellipse([x, yb + 26, x + 40, yb + 66], fill=(16, 16, 18), outline=GOLD, width=2)
        d.text((x + 20, yb + 46), g, font=f13, fill=GOLD if v != "C" else (150, 230, 240), anchor="mm")
        d.text((x + 20, yb + 80), name, font=f8, fill=GOLD if v != "C" else (170, 170, 180), anchor="mm")
    hx = bx + 3 * 58 + 30
    lit = round(heat / 10)
    for i in range(10):
        c = [(70, 220, 120), (70, 220, 120), (210, 200, 70), (210, 200, 70), (240, 150, 50), (240, 150, 50), (230, 70, 50), (230, 70, 50), (200, 30, 60), (200, 30, 60)][i]
        on = i < lit
        if v == "B":   # SMD LEDs on the board
            d.rectangle([hx + i * 22, yb + 40, hx + i * 22 + 12, yb + 48], fill=c if on else (40, 44, 40))
            if on:
                A.glow(img, (hx + i * 22, yb + 40, hx + i * 22 + 12, yb + 48), c, 0.4, 4)
                d = ImageDraw.Draw(img)
        else:          # glass beads
            cx, cy = hx + i * 22 + 8, yb + 44
            if on:
                A.glow(img, (cx - 4, cy - 4, cx + 4, cy + 4), c, 0.35, 5)
                d = ImageDraw.Draw(img)
            d.ellipse([cx - 8, cy - 8, cx + 8, cy + 8], fill=c if on else (34, 34, 38), outline=(10, 10, 10))
            d.ellipse([cx - 5, cy - 6, cx - 1, cy - 2], fill=(255, 255, 255, 140))
    d.text((hx + 110, yb + 80), "HEAT LEVEL", font=f8, fill=GOLD if v != "C" else (170, 170, 180), anchor="mm")
    sx = bx1 - IN_R - 200 if v != "A" else sx1 - 200
    d.rounded_rectangle([sx, yb + 30, sx + 150, yb + 62], 16, fill=(10, 10, 12), outline=trim, width=1)
    if online:
        d.rounded_rectangle([sx + 80, yb + 33, sx + 147, yb + 59], 13, fill=GOLD if v != "C" else (120, 220, 230))
    d.text((sx + 75, yb + 80), "NET LINK · TOUCH", font=f8, fill=GOLD if v != "C" else (170, 170, 180), anchor="mm")
    if v == "A":   # a gold circuit flourish in the bottom-left corner
        d.line([(bx0 + 16, yb + 30), (bx0 + 40, yb + 30), (bx0 + 52, yb + 42), (lcd[0] - 10, yb + 42)], fill=GOLD_DIM, width=1)
        d.line([(bx0 + 16, yb + 56), (bx0 + 30, yb + 56), (bx0 + 42, yb + 68), (lcd[0] - 10, yb + 68)], fill=GOLD_DIM, width=1)
    img.convert("RGB").save(out_path)


if __name__ == "__main__":
    v = sys.argv[sys.argv.index("--variant") + 1] if "--variant" in sys.argv else "A"
    mock(v, sys.argv[sys.argv.index("--mock") + 1])
