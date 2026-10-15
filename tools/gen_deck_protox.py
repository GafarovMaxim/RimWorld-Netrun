"""PROTO-X, the fixer's secret prototype deck: two looks to choose from (mockups only for now; Maxim turned down the
carbon/board/glass set on 2026-10-15).
BOX   A black box from a security service: matte composite, laser-engraved clearance marks and REDACTED bars, hidden keys
      that light up under the mouse, red tamper strips; the Heat is a red line creeping round the screen.
HIVE  A nano-swarm: the casing is built of hexagonal cells; as the Heat rises cells come loose, drift and glow red.
Usage: python gen_deck_protox.py --variant BOX --mock OUT.png   (renders a low and a high Heat side by side)"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
import deck_art as A

OUT = 8
SCREEN = (930, 666)
INK_G = (90, 230, 130)
RED = (230, 40, 46)


def screen_content(d, sx0, sy0, sx1, sy1, mono, heat):
    d.rounded_rectangle([sx0, sy0, sx1, sy1], 6, fill=(10, 14, 12))
    for i, (t, c) in enumerate((("NETRUN // NETWORK CORE", INK_G), ("> HOME NODE ........ ONLINE", INK_G),
                                ("> GATEWAY .......... 5 NODES", INK_G), ("> HEAT .............. %d" % heat, (230, 190, 70)))):
        d.text((sx0 + 30, sy0 + 28 + i * 22), t, font=mono, fill=c)
    for r in (60, 120):
        d.ellipse([sx1 - 230 - r, (sy0 + sy1) / 2 - r, sx1 - 230 + r, (sy0 + sy1) / 2 + r], outline=(40, 120, 70))


def perimeter_point(x0, y0, x1, y1, t):
    """A point at fraction t of the way clockwise round a rectangle from its top-left corner."""
    w, h = x1 - x0, y1 - y0
    p = t * 2 * (w + h)
    if p <= w:
        return x0 + p, y0
    p -= w
    if p <= h:
        return x1, y0 + p
    p -= h
    if p <= w:
        return x1 - p, y1
    return x0, y1 - (p - w)


def mock_box(heat, hover=1):
    IN_L, IN_T, IN_R, IN_B = 40, 84, 40, 92
    W, H = SCREEN[0] + IN_L + IN_R + 2 * OUT, SCREEN[1] + IN_T + IN_B + 2 * OUT
    img = Image.new("RGBA", (W, H), (30, 32, 30, 255))
    bx0, by0, bx1, by1 = OUT, OUT, W - OUT, H - OUT
    bw, bh = bx1 - bx0, by1 - by0
    m = A.mask((bw, bh), lambda d, s: d.rounded_rectangle([0, 0, bw * s - 1, bh * s - 1], 10 * s, fill=255))
    yy, xx = np.mgrid[0:bh, 0:bw].astype(np.float32)
    rgb = np.array((20, 20, 22), np.float32) + A.grain(bw, bh, 61, 2)[..., None] + A.fbm(bw, bh, 40, 2, 62)[..., None] * 6
    rgb += (((xx + yy) % 6 < 1) * 2.5)[..., None]                      # a faint diagonal micro-texture
    rgb += (A.shade(m, 2.0) * 30)[..., None]
    img.alpha_composite(A.to_img(rgb, m), (bx0, by0))
    d = ImageDraw.Draw(img)
    eng = (58, 58, 62)            # laser engraving: barely lighter than the body
    eng_hi = (84, 84, 90)
    f7, f8 = A.font(7, False), A.font(8, False)
    mono = A.font(12, False)
    sx0, sy0, sx1, sy1 = bx0 + IN_L, by0 + IN_T, bx1 - IN_R, by1 - IN_B

    # engraved markings: clearance, REDACTED bars, a data matrix, a serial
    d.text((bx0 + 40, by0 + 22), "PROTO-X", font=A.font(18), fill=eng_hi)
    d.text((bx0 + 40, by0 + 48), "CLEARANCE  LEVEL 5  //  EYES ONLY", font=f8, fill=eng)
    d.rectangle([bx0 + 230, by0 + 26, bx0 + 330, by0 + 36], fill=(8, 8, 9))
    d.rectangle([bx0 + 230, by0 + 42, bx0 + 300, by0 + 52], fill=(8, 8, 9))
    d.text((bx0 + 340, by0 + 26), "REDACTED", font=f8, fill=eng)
    rng = np.random.default_rng(5)
    qx, qy = bx1 - 70, by0 + 18
    for i in range(12):
        for j in range(12):
            if rng.random() < 0.5 or (i in (0, 11) and j % 2 == 0) or j == 0:
                d.rectangle([qx + i * 4, qy + j * 4, qx + i * 4 + 3, qy + j * 4 + 3], fill=eng)
    d.text((bx1 - 40, by1 - 22), "UNIT 0-01  ·  NO SERVICEABLE PARTS  ·  TAMPER = WIPE", font=f7, fill=eng, anchor="ra")

    # the tamper strip round the screen: a dark red hairline, the Heat creeps along it in bright red
    tx0, ty0, tx1, ty1 = sx0 - 14, sy0 - 14, sx1 + 14, sy1 + 14
    d.rounded_rectangle([tx0, ty0, tx1, ty1], 8, outline=(60, 14, 16), width=2)
    t = heat / 100
    pts = [perimeter_point(tx0, ty0, tx1, ty1, k / 400 * t) for k in range(401)]
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).line(pts, fill=RED + (140,), width=7)
    ga = A.blur(np.asarray(glow, np.float32)[..., 3] / 255, 4) * 0.8
    img.alpha_composite(A.to_img(np.broadcast_to(np.array(RED, np.float32), ga.shape + (3,)).copy(), ga))
    d = ImageDraw.Draw(img)
    d.line(pts, fill=(255, 70, 70), width=2)
    hx, hy = pts[-1]
    d.ellipse([hx - 4, hy - 4, hx + 4, hy + 4], fill=(255, 200, 200))
    d.text((tx1, ty1 + 4), "INTEGRITY  %d%%" % (100 - heat), font=f8, fill=(190, 50, 54) if heat > 50 else eng_hi, anchor="ra")

    screen_content(d, sx0, sy0, sx1, sy1, mono, heat)

    # hidden keys: engraved outlines only, the hovered one lit
    kx = bx1 - 440
    for i, n in enumerate(("CLOSE", "MENU", "ACHIEV", "LOCK")):
        x = kx + i * 64
        lit = i == hover
        if lit:
            A.glow(img, (x, by0 + 24, x + 46, by0 + 50), (220, 230, 240), 0.25, 8)
            d = ImageDraw.Draw(img)
        d.rounded_rectangle([x, by0 + 24, x + 46, by0 + 50], 3, outline=(200, 210, 220) if lit else eng, width=1)
        d.text((x + 23, by0 + 37), n, font=f8, fill=(230, 236, 240) if lit else eng, anchor="mm")
    for i, (c, n) in enumerate((((70, 220, 120), "NET"), ((240, 150, 50), "SICK"), ((50, 50, 52), "POD"))):
        cx = kx + 4 * 64 + 10 + i * 26
        if i < 2:
            A.glow(img, (cx - 1, by0 + 30, cx + 1, by0 + 32), c, 0.5, 3)
            d = ImageDraw.Draw(img)
        d.ellipse([cx - 2, by0 + 29, cx + 2, by0 + 33], fill=c)
        d.text((cx, by0 + 44), n, font=f7, fill=eng_hi, anchor="mm")

    # bottom: a white dot-matrix strip, hidden BACK CORE HELP, an engraved NET LINK slider
    yb = by1 - IN_B
    d.rectangle([bx0 + 60, yb + 30, bx0 + 300, yb + 64], fill=(8, 8, 9), outline=eng)
    for i, t2 in enumerate(("CYBERDATA 491", "D8 08:27  NET ON")):
        d.text((bx0 + 70, yb + 34 + i * 14), t2, font=A.font(11, False), fill=(214, 220, 226))
    for i, n in enumerate(("BACK", "CORE", "HELP")):
        x = bx0 + 340 + i * 70
        d.rounded_rectangle([x, yb + 30, x + 52, yb + 64], 3, outline=eng, width=1)
        d.text((x + 26, yb + 47), n, font=f8, fill=eng_hi, anchor="mm")
    sx = bx1 - 220
    d.line([sx, yb + 47, sx + 150, yb + 47], fill=eng, width=1)
    d.line([sx + 80, yb + 47, sx + 150, yb + 47], fill=(70, 220, 120), width=2)
    d.rectangle([sx + 146, yb + 40, sx + 152, yb + 54], fill=(200, 210, 220))
    d.text((sx + 75, yb + 64), "NET LINK", font=f8, fill=eng_hi, anchor="mm")
    return img


def hex_poly(cx, cy, r, rot=30):
    return [(cx + r * math.cos(math.radians(60 * k + rot)), cy + r * math.sin(math.radians(60 * k + rot))) for k in range(6)]


def mock_hive(heat):
    IN_L, IN_T, IN_R, IN_B = 46, 92, 46, 96
    W, H = SCREEN[0] + IN_L + IN_R + 2 * OUT, SCREEN[1] + IN_T + IN_B + 2 * OUT
    img = Image.new("RGBA", (W, H), (30, 32, 30, 255))
    bx0, by0, bx1, by1 = OUT + 4, OUT + 4, W - OUT - 4, H - OUT - 4
    sx0, sy0, sx1, sy1 = OUT + IN_L, OUT + IN_T, W - OUT - IN_R, H - OUT - IN_B
    rng = np.random.default_rng(9)
    r = 13.0
    dx, dy = r * math.sqrt(3), r * 1.5
    t = heat / 100
    big = Image.new("RGBA", (W * 2, H * 2), (0, 0, 0, 0))
    bd = ImageDraw.Draw(big)
    loose = []
    row = 0
    y = by0 + r
    while y < by1 - r * 0.5:
        x = bx0 + r + (dx / 2 if row % 2 else 0)
        while x < bx1 - r * 0.6:
            if not (sx0 - 10 < x < sx1 + 10 and sy0 - 10 < y < sy1 + 10):
                # the outer cells come loose first
                edge = min(x - bx0, bx1 - x, y - by0, by1 - y)
                chance = max(0.0, t - 0.25) * 1.3 * (1.0 if edge < 20 else 0.35)
                if rng.random() < chance:
                    loose.append((x, y))
                else:
                    shade = 30 + int(rng.integers(-4, 5))
                    hot = rng.random() < t * 0.25
                    fill = (shade, shade + 2, shade + 6) if not hot else (90 + int(80 * t), 30, 26)
                    bd.polygon([(px * 2, py * 2) for px, py in hex_poly(x, y, r - 1.2)], fill=fill + (255,),
                               outline=(40, 170, 190, 150) if not hot else (255, 90, 60, 220))
            x += dx
        y += dy
        row += 1
    img.alpha_composite(big.resize((W, H), Image.LANCZOS))
    # loose cells drift outwards, tilted, glowing
    for x, y in loose:
        ox = (x - (bx0 + bx1) / 2) * 0.03 * t + rng.normal(0, 3)
        oy = (y - (by0 + by1) / 2) * 0.03 * t + rng.normal(0, 3)
        rr = r * rng.uniform(0.55, 0.9)
        A.glow(img, (x + ox - 3, y + oy - 3, x + ox + 3, y + oy + 3), (255, 80, 50), 0.25 + 0.4 * t, 6)
        ImageDraw.Draw(img).polygon(hex_poly(x + ox, y + oy, rr, rng.uniform(0, 60)), fill=(60, 20, 18), outline=(255, 110, 70))
    d = ImageDraw.Draw(img)
    mono = A.font(12, False)
    d.rounded_rectangle([sx0 - 4, sy0 - 4, sx1 + 4, sy1 + 4], 6, fill=(6, 8, 10), outline=(40, 170, 190), width=1)
    screen_content(d, sx0, sy0, sx1, sy1, mono, heat)
    f8 = A.font(8, False)

    def key(cx, cy, label, glyph, col=(60, 210, 230)):
        A.glow(img, (cx - 8, cy - 8, cx + 8, cy + 8), col, 0.3, 8)
        dd = ImageDraw.Draw(img)
        dd.polygon(hex_poly(cx, cy, 17), fill=(12, 30, 36), outline=col)
        dd.text((cx, cy), glyph, font=A.font(12), fill=col, anchor="mm")
        dd.text((cx, cy + 26), label, font=f8, fill=(150, 210, 220), anchor="mm")
    for i, (lab, g) in enumerate((("CLOSE", "X"), ("MENU", "="), ("ACHIEV", "*"), ("LOCK", "#"))):
        key(sx0 + 30 + i * 52, by0 + 34, lab, g, (240, 80, 90) if i == 0 else (60, 210, 230))
    d = ImageDraw.Draw(img)
    d.text((sx0 + 260, by0 + 34), "PROTO-X  //  SWARM BUILD", font=A.font(14), fill=(180, 230, 240), anchor="lm")
    for i, (c, n) in enumerate((((70, 220, 120), "NET"), ((240, 150, 50), "SICK"), ((50, 60, 60), "POD"))):
        cx = sx1 - 120 + i * 40
        if i < 2:
            A.glow(img, (cx - 3, by0 + 31, cx + 3, by0 + 37), c, 0.5, 5)
        d = ImageDraw.Draw(img)
        d.polygon(hex_poly(cx, by0 + 34, 7), fill=c)
        d.text((cx, by0 + 54), n, font=f8, fill=(150, 210, 220), anchor="mm")
    yb = sy1 + 4
    d.rectangle([sx0 + 20, yb + 22, sx0 + 250, yb + 72], fill=(6, 12, 14), outline=(40, 170, 190))
    for i, t2 in enumerate(("CYBERDATA 491", "HEAT %d  ·  SWARM %d%%" % (heat, 100 - int(heat * 0.6)), "D8 08:27  NET ON")):
        d.text((sx0 + 30, yb + 27 + i * 14), t2, font=A.font(11, False), fill=(120, 230, 240))
    for i, (lab, g) in enumerate((("BACK", "<"), ("CORE", "O"), ("HELP", "?"))):
        key(sx0 + 300 + i * 52, yb + 44, lab, g, (255, 150, 60) if i == 1 else (60, 210, 230))
    d = ImageDraw.Draw(img)
    for i in range(6):   # NET LINK: a row of cells, filled while online
        cx = sx1 - 200 + i * 26
        d.polygon(hex_poly(cx, yb + 44, 11), fill=(40, 170, 120) if i >= 2 else (20, 30, 30), outline=(60, 210, 230))
    d.text((sx1 - 135, yb + 70), "NET LINK", font=f8, fill=(150, 210, 220), anchor="mm")
    return img


def pair(fn, out_path):
    a, b = fn(15), fn(85)
    s = 0.6
    a = a.resize((int(a.width * s), int(a.height * s)), Image.LANCZOS)
    b = b.resize((int(b.width * s), int(b.height * s)), Image.LANCZOS)
    out = Image.new("RGB", (a.width * 2 + 30, a.height + 40), (24, 24, 26))
    d = ImageDraw.Draw(out)
    d.text((10, 10), "HEAT 15", font=A.font(16), fill=(240, 200, 70))
    d.text((a.width + 20, 10), "HEAT 85", font=A.font(16), fill=(240, 200, 70))
    out.paste(a.convert("RGB"), (10, 34))
    out.paste(b.convert("RGB"), (a.width + 20, 34))
    out.save(out_path)


if __name__ == "__main__":
    v = sys.argv[sys.argv.index("--variant") + 1] if "--variant" in sys.argv else "BOX"
    pair(mock_box if v == "BOX" else mock_hive, sys.argv[sys.argv.index("--mock") + 1])
