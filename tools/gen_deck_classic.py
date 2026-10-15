#!/usr/bin/env python3
"""
Mockups for the reworked classic deck frame (2026-10-15), two directions for Maxim to choose from. Mockups only for now:
the textures and DeckClassic.cs come after the pick. Same controls as the current classic frame: CLOSE, MENU, ACHIEV,
NET LOCK, the tuner, RF, NET/SICK/POD lamps on top; the LCD, BACK/CORE/HELP, the Heat scale and NET LINK at the bottom.

    B  Cyberdeck  matte black plastic with chamfered corners, carbon inserts, ridges, chiclet keys with cyan legends,
                  a cyan light line in the bezel, an amber OLED
    C  DIY        a home-made deck: grey 3D-printed case with layer lines and orange corner caps, bare green PCBs with
                  tactile switches, a pot, an OLED module, 5 mm LEDs, a rainbow ribbon cable, a 16x2 LCD, an LED
                  bargraph for the Heat, masking tape labels

    python tools/gen_deck_classic.py [--heat 58]      writes docs/mockups/46b-classic-cyberdeck.png and 46c-classic-diy.png
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

import deck_art as A
import deck_stickers as S

IN_L, IN_T, IN_R, IN_B = 60, 96, 60, 100
OUT = 8
SCREEN = (930, 666)
W = SCREEN[0] + IN_L + IN_R + 2 * OUT
H = SCREEN[1] + IN_T + IN_B + 2 * OUT
BX0, BY0, BX1, BY1 = OUT, OUT, W - OUT, H - OUT
SX0, SY0, SX1, SY1 = BX0 + IN_L, BY0 + IN_T, BX1 - IN_R, BY1 - IN_B

HEAT_SEG = [(49, 196, 106), (230, 196, 49), (255, 138, 60), (255, 74, 60), (179, 18, 42)]
CYAN, AMBER, RED = (70, 220, 230), (255, 176, 60), (230, 60, 50)


def dim(c, k=0.25, bg=(10, 10, 12)):
    """An unlit LED: its colour mixed into the dark behind it (opaque, PIL does not blend fills)."""
    return tuple(int(v * k + b * (1 - k)) for v, b in zip(c, bg)) + (255,)


def level_of(heat):
    return 0 if heat < 20 else 1 if heat < 45 else 2 if heat < 70 else 3 if heat < 90 else 4


def drop(img, a, off=2, k=0.55, r=2.0):
    """A soft drop shadow of the alpha mask a (full canvas size) under what comes next."""
    sh = np.roll(np.roll(A.blur(a, r), off, 0), off // 2, 1) * k
    img.alpha_composite(A.to_img(np.zeros(a.shape + (3,), np.float32), sh))


def screen_content(img, heat):
    d = ImageDraw.Draw(img)
    d.rectangle([SX0, SY0, SX1 - 1, SY1 - 1], fill=(12, 16, 16, 255))
    f = A.font(15)
    lv = ["SHADOW", "NOTICED", "TRACKED", "TARGET", "RED ZONE"][level_of(heat)]
    for i, (t, c) in enumerate((("NETRUN // NETWORK CORE", (90, 230, 130)), ("> HOME NODE ........ ONLINE", (90, 210, 130)),
                                ("> GATEWAY .......... 5 NODES", (90, 210, 130)), (f"> HEAT {heat} .......... {lv}", (230, 190, 70)))):
        d.text((SX0 + 30, SY0 + 28 + i * 24), t, font=f, fill=c)
    cx, cy = SX1 - 260, (SY0 + SY1) / 2
    for r in (70, 140):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(40, 120, 70))


# ================================================================== B  Cyberdeck

def cyber(heat):
    lvl = level_of(heat)
    img = Image.new("RGBA", (W, H), (26, 28, 30, 255))
    bw, bh = BX1 - BX0, BY1 - BY0
    c = 26

    def chamfer(x0, y0, x1, y1, k):
        return [(x0 + k, y0), (x1 - k, y0), (x1, y0 + k), (x1, y1 - k), (x1 - k, y1), (x0 + k, y1), (x0, y1 - k), (x0, y0 + k)]

    full = A.mask((W, H), lambda d, s: d.polygon([(x * s, y * s) for x, y in chamfer(BX0, BY0, BX1 - 1, BY1 - 1, c)], fill=255))
    hole = A.mask((W, H), lambda d, s: d.rectangle([SX0 * s, SY0 * s, SX1 * s - 1, SY1 * s - 1], fill=255))
    bezel = A.mask((W, H), lambda d, s: d.polygon([(x * s, y * s) for x, y in chamfer(SX0 - 16, SY0 - 16, SX1 + 15, SY1 + 15, 12)], fill=255))
    drop(img, full, 4, 0.7, 4)

    rgb = A.hexc(0x1c1d21) + A.grain(W, H, 51, 3)[..., None] + A.fbm(W, H, 60, 3, 52)[..., None] * 6
    rgb += (A.shade(full, 2.5) * 40)[..., None]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # carbon twill inserts on both rails
    tw = ((np.floor(xx / 4) + np.floor(yy / 4)) % 2)[..., None] * np.where((np.floor(yy / 8) % 2) == 0, 1, -1)[..., None]
    for x0 in (BX0 + 12, BX1 - 12 - 30):
        ins = A.mask((W, H), lambda d, s: d.rounded_rectangle([x0 * s, (SY0 + 40) * s, (x0 + 30) * s, (SY1 - 40) * s], 4 * s, fill=255))
        carbon = A.hexc(0x24262b) + tw * 7 + (A.shade(ins, 1.2) * -30)[..., None]
        rgb = rgb * (1 - ins[..., None]) + carbon * ins[..., None]
    # ridges along the top and bottom bands
    for y0, y1 in ((BY0 + 6, BY0 + 12), (BY1 - 12, BY1 - 6)):
        rid = A.mask((W, H), lambda d, s: [d.rectangle([x * s, y0 * s, (x + 2) * s, y1 * s], fill=255) for x in range(BX0 + 120, BX1 - 120, 6)])
        rgb += (A.shade(rid, 0.8) * 50)[..., None] - (rid * 6)[..., None]
    # the bezel, a step lower and darker, with a cyan light line round the screen
    rgb = rgb * (1 - bezel[..., None]) + (A.hexc(0x111215) + A.grain(W, H, 53, 2)[..., None]) * bezel[..., None]
    rgb -= (A.shade(bezel, 2.0) * 35)[..., None]
    img.alpha_composite(A.to_img(rgb, full * (1 - hole)))
    d = ImageDraw.Draw(img)
    line_c = CYAN if lvl < 3 else RED
    A.glow(img, (SX0 - 6, SY0 - 6, SX1 + 6, SY1 + 6), line_c, 0.25, 4)
    d.rectangle([SX0 - 7, SY0 - 7, SX1 + 6, SY1 + 6], outline=line_c + (255,), width=1)
    d.rectangle([SX0 - 6, SY0 - 6, SX1 + 5, SY1 + 5], fill=(14, 15, 18, 255))
    screen_content(img, heat)
    d = ImageDraw.Draw(img)
    # hex screws in the four chamfered corners
    for x, y in ((BX0 + 22, BY0 + 22), (BX1 - 22, BY0 + 22), (BX0 + 22, BY1 - 22), (BX1 - 22, BY1 - 22)):
        A.over(img, A.rivet(10, A.hexc(0x3a3c42), x), (x - 7, y - 7))
        d.regular_polygon((x, y, 2.6), 6, fill=(12, 12, 14))
    # side vents on the right rail, a little tag on the left
    for i in range(6):
        y = SY0 + 140 + i * 10
        d.rounded_rectangle([BX1 - 44, y, BX1 - 26, y + 4], 2, fill=(8, 8, 10))
    f7, f8, f9, f11 = A.font(7), A.font(8), A.font(9), A.font(11)

    def label(x, y, t, col=(150, 156, 166), f=f8):
        d.text((x, y), t, font=f, fill=col + (255,), anchor="mm")

    def chiclet(x, y, w, h, legend, col):
        k = S.Big(w + 4, h + 6)
        k.rect((2, 4, w + 1, h + 4), (6, 6, 8, 255), r=5)
        k.rect((2, 1, w + 1, h + 1), (40, 42, 48, 255), r=5)
        k.rect((4, 3, w - 1, h - 3), (30, 32, 37, 255), r=4)
        k.rect((6, 3, w - 3, 5), (70, 72, 80, 160), r=1)
        A.over(img, k.done(), (x - 2, y - 1))
        A.glow(img, (x + w / 2 - 6, y + h / 2 - 3, x + w / 2 + 6, y + h / 2 + 3), col, 0.3, 3)
        d.text((x + w / 2, y + h / 2 + 1), legend, font=f9, fill=col + (255,), anchor="mm")

    # ---- top
    ty = BY0 + 30
    d.text((BX0 + 52, BY0 + 34), "NR-1", font=A.font(22), fill=(200, 204, 210, 255), anchor="lm")
    d.text((BX0 + 112, BY0 + 28), "CYBERDECK", font=f9, fill=(150, 156, 166, 255), anchor="lm")
    d.text((BX0 + 112, BY0 + 41), "NETRUN SYSTEMS", font=f7, fill=(100, 106, 116, 255), anchor="lm")
    d.line([(BX0 + 52, BY0 + 56), (BX0 + 196, BY0 + 56)], fill=(54, 160, 170, 255), width=1)
    d.line([(BX0 + 196, BY0 + 56), (BX0 + 206, BY0 + 66), (BX0 + 260, BY0 + 66)], fill=(54, 160, 170, 255), width=1)
    x = BX0 + 300
    d.rounded_rectangle([x - 10, ty - 8, x + 4 * 50 - 4, ty + 38], 6, fill=(12, 13, 16, 255))
    for i, (lg, nm, col) in enumerate((("X", "CLOSE", RED), ("=", "MENU", CYAN), ("*", "ACHIEV", CYAN), ("#", "NET LOCK", AMBER))):
        chiclet(x + i * 50, ty - 2, 38, 28, lg, col)
        label(x + i * 50 + 19, ty + 48, nm)
    # tuner: a knurled thumb wheel in a slot
    tx = x + 4 * 50 + 30
    d.rounded_rectangle([tx, ty - 6, tx + 22, ty + 34], 4, fill=(8, 8, 10, 255))
    for k in range(12):
        yk = ty - 2 + k * 3
        d.line([(tx + 3, yk), (tx + 19, yk)], fill=(70, 72, 80, 255) if k % 2 else (40, 42, 46, 255), width=1)
    label(tx + 11, ty + 48, "TUNE")
    # lamps: light pipes in a strip
    lx = BX1 - 330
    for i, (nm, col, on) in enumerate((("NET", (90, 230, 120), True), ("SICK", AMBER, True), ("POD", CYAN, False))):
        cx = lx + i * 34
        d.rounded_rectangle([cx - 8, ty + 6, cx + 8, ty + 14], 2, fill=(10, 10, 12, 255))
        if on:
            A.glow(img, (cx - 6, ty + 8, cx + 6, ty + 12), col, 0.6, 4)
        d.rounded_rectangle([cx - 6, ty + 8, cx + 6, ty + 12], 1, fill=(col if on else (40, 44, 50)) + (255,))
        label(cx, ty + 48, nm)
    # alarm light pipe that lights from Target
    if lvl >= 3:
        A.glow(img, (lx - 42, ty + 8, lx - 30, ty + 12), RED, 0.8, 5)
    d.rounded_rectangle([lx - 44, ty + 6, lx - 28, ty + 14], 2, fill=(10, 10, 12, 255))
    d.rounded_rectangle([lx - 42, ty + 8, lx - 30, ty + 12], 1, fill=(RED if lvl >= 3 else (50, 30, 30)) + (255,))
    label(lx - 36, ty + 48, "ALERT")
    # RF on an OLED
    rx = BX1 - 210
    d.rounded_rectangle([rx, ty - 8, rx + 150, ty + 36], 4, fill=(6, 7, 8, 255), outline=(50, 52, 58, 255))
    d.text((rx + 10, ty + 2), "RF 2.4G", font=f8, fill=CYAN + (255,), anchor="lm")
    for i in range(12):
        h = 4 + i * 1.6
        on = i < 8 or (lvl >= 3 and i == 11)
        cc = RED if i == 11 and lvl >= 3 else CYAN
        d.rectangle([rx + 10 + i * 11, ty + 30 - h, rx + 16 + i * 11, ty + 30], fill=cc + (255,) if on else dim(cc))
    label(rx + 75, ty + 48, "LINK")

    # ---- bottom
    yb = SY1 + 30
    d.rounded_rectangle([BX0 + 52, yb - 6, BX0 + 312, yb + 52], 5, fill=(6, 6, 7, 255), outline=(50, 52, 58, 255))
    for i, t in enumerate(("CYBERDATA 07", f"HEAT {heat}", "D2 14:20  NET ON")):
        d.text((BX0 + 64, yb + 4 + i * 15), t, font=A.font(11), fill=AMBER + (255,), anchor="lt")
    kx = BX0 + 350
    for i, (lg, nm) in enumerate((("<", "BACK"), ("O", "CORE"), ("?", "HELP"))):
        chiclet(kx + i * 54, yb + 4, 40, 30, lg, CYAN)
        label(kx + i * 54 + 20, yb + 50, nm)
    # Heat: five light-pipe segments
    hx = BX0 + 560
    d.rounded_rectangle([hx - 8, yb + 6, hx + 5 * 40, yb + 32], 4, fill=(10, 10, 12, 255))
    for i in range(5):
        cc = HEAT_SEG[i]
        on = i <= lvl
        r = (hx + i * 40, yb + 12, hx + i * 40 + 32, yb + 26)
        if on:
            A.glow(img, r, cc, 0.45, 4)
        d.rounded_rectangle(r, 2, fill=cc + (255,) if on else dim(cc))
    label(hx + 96, yb + 50, "HEAT LEVEL")
    # NET LINK: a rocker
    sx = BX1 - 150
    d.rounded_rectangle([sx, yb + 2, sx + 74, yb + 36], 5, fill=(8, 8, 10, 255))
    d.rounded_rectangle([sx + 38, yb + 5, sx + 71, yb + 33], 4, fill=(40, 42, 48, 255))
    A.glow(img, (sx + 48, yb + 16, sx + 62, yb + 22), (90, 230, 120), 0.5, 3)
    d.rounded_rectangle([sx + 48, yb + 17, sx + 62, yb + 21], 1, fill=(90, 230, 120, 255))
    d.text((sx + 18, yb + 19), "OFF", font=f8, fill=(90, 94, 104, 255), anchor="mm")
    label(sx + 37, yb + 50, "NET LINK")
    return img


# ================================================================== C  DIY

PLA, PLA_DARK, ORANGE = (148, 150, 150), (110, 112, 112), (226, 112, 40)
FR4, SILK = (28, 92, 52), (232, 236, 228)


def diy(heat):
    lvl = level_of(heat)
    img = Image.new("RGBA", (W, H), (26, 28, 30, 255))
    full = A.mask((W, H), lambda d, s: d.rounded_rectangle([BX0 * s, BY0 * s, BX1 * s - 1, BY1 * s - 1], 16 * s, fill=255))
    hole = A.mask((W, H), lambda d, s: d.rounded_rectangle([SX0 * s, SY0 * s, SX1 * s - 1, SY1 * s - 1], 4 * s, fill=255))
    bez = A.mask((W, H), lambda d, s: d.rounded_rectangle([(SX0 - 12) * s, (SY0 - 12) * s, (SX1 + 12) * s - 1, (SY1 + 12) * s - 1], 8 * s, fill=255))
    drop(img, full, 4, 0.7, 4)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    rgb = np.array(PLA, np.float32) + A.grain(W, H, 61, 2)[..., None]
    rgb += ((np.sin(yy * np.pi / 1.5) * 4) + A.fbm(W, H, 3, 1, 62) * 3)[..., None]       # print layer lines
    rgb += (A.shade(full, 3) * 38)[..., None]
    rgb -= (bez * 18)[..., None] + (A.shade(bez, 2) * 30)[..., None]                        # the screen frame, a step lower
    img.alpha_composite(A.to_img(rgb, full * (1 - hole)))
    d = ImageDraw.Draw(img)
    # orange corner caps printed separately
    for cx, cy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        n = 70
        m = A.mask((n, n), lambda dr, s: (dr.rounded_rectangle([0, 0, n * s - 1, 22 * s], 10 * s, fill=255), dr.rounded_rectangle([0, 0, 22 * s, n * s - 1], 10 * s, fill=255)))
        cap = A.to_img(np.array(ORANGE, np.float32) + (np.sin(np.mgrid[0:n, 0:n][0] * np.pi / 1.5) * 5)[..., None] + (A.shade(m, 2) * 50)[..., None], m)
        if cx:
            cap = cap.transpose(Image.FLIP_LEFT_RIGHT)
        if cy:
            cap = cap.transpose(Image.FLIP_TOP_BOTTOM)
        A.over(img, cap, (BX1 - n if cx else BX0, BY1 - n if cy else BY0))
    screen_content(img, heat)
    d = ImageDraw.Draw(img)

    def screw(x, y):
        A.over(img, A.rivet(9, A.hexc(0x9c9a94), int(x)), (x - 6, y - 6))
        d.line([(x - 2.5, y), (x + 2.5, y)], fill=(50, 50, 48), width=1)
        d.line([(x, y - 2.5), (x, y + 2.5)], fill=(50, 50, 48), width=1)

    for x, y in ((BX0 + 11, BY0 + 11), (BX1 - 12, BY0 + 11), (BX0 + 11, BY1 - 12), (BX1 - 12, BY1 - 12)):
        screw(x, y)
    f7, f8, f9 = A.font(7), A.font(8), A.font(9)

    def pcb(x0, y0, x1, y1):
        m = A.mask((W, H), lambda dr, s: dr.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], fill=255))
        drop(img, m, 2, 0.5, 1.5)
        pr = np.array(FR4, np.float32) + A.grain(W, H, x0, 3)[..., None]
        img.alpha_composite(A.to_img(pr, m))
        dd = ImageDraw.Draw(img)
        dd.rectangle([x0, y0, x1, y1], outline=(18, 60, 34, 255))
        for hx, hy in ((x0 + 6, y0 + 6), (x1 - 6, y0 + 6), (x0 + 6, y1 - 6), (x1 - 6, y1 - 6)):
            dd.ellipse([hx - 4, hy - 4, hx + 4, hy + 4], fill=(200, 170, 80, 255))
            screw(hx, hy)
        # a few traces
        r = A.rng(x0)
        for _ in range(7 if x1 - x0 > 120 else 0):
            ty = r.integers(y0 + 12, y1 - 12)
            tx = r.integers(x0 + 14, x1 - 60)
            dd.line([(tx, ty), (tx + r.integers(20, 60), ty), (tx + r.integers(60, 80), ty + 6)], fill=(40, 120, 70, 255), width=1)

    def tact(x, y, cap):
        dd = ImageDraw.Draw(img)
        dd.rectangle([x - 12, y - 12, x + 12, y + 12], fill=(30, 30, 32, 255))
        for px, py in ((x - 12, y - 9), (x + 12, y - 9), (x - 12, y + 9), (x + 12, y + 9)):
            dd.rectangle([px - 2, py - 1, px + 2, py + 1], fill=(190, 190, 186, 255))
        A.over(img, A.rivet(16, np.array(cap, np.float32), x + y), (x - 10, y - 10))

    def silk(x, y, t, f=f8, col=SILK):
        ImageDraw.Draw(img).text((x, y), t, font=f, fill=col + (255,), anchor="mm")

    def led(x, y, col, on):
        dd = ImageDraw.Draw(img)
        dd.line([(x - 3, y + 7), (x - 3, y + 11)], fill=(180, 180, 176), width=1)
        dd.line([(x + 3, y + 7), (x + 3, y + 11)], fill=(180, 180, 176), width=1)
        if on:
            A.glow(img, (x - 5, y - 5, x + 5, y + 5), col, 0.7, 5)
        glass = col if on else tuple(int(v * 0.35) for v in col)
        A.over(img, A.rivet(12, np.array(glass, np.float32), x), (x - 8, y - 8))

    def tape(x, y, w, text, angle=-2):
        t = S.tape(w, 18, color=(222, 208, 160), alpha=0.95, seed=x)
        ImageDraw.Draw(t).text((w / 2 + 2, 11), text, font=S.jfont(12) if False else A.font(10, False), fill=(30, 30, 60, 255), anchor="mm")
        A.over(img, t.rotate(angle, expand=True, resample=Image.BICUBIC), (x, y))

    # ---- top: a PCB with four tactile switches, a pot, LEDs, an OLED module
    ty = BY0 + 46
    tape(BX0 + 40, BY0 + 26, 150, "NETRUN deck v0.3", -3)
    d.text((BX0 + 60, BY0 + 64), "printed 3x, works now", font=A.font(8, False), fill=(60, 60, 70, 255), anchor="lm")
    px0 = BX0 + 276
    pcb(px0, BY0 + 14, px0 + 300, BY0 + 86)
    for i, (nm, cap) in enumerate((("CLOSE", (200, 50, 40)), ("MENU", (40, 40, 44)), ("ACHIEV", (40, 40, 44)), ("NET LOCK", (220, 180, 40)))):
        tact(px0 + 36 + i * 58, ty - 4, cap)
        silk(px0 + 36 + i * 58, ty + 24, nm)
    # pot
    x = px0 + 330
    pcb(x - 26, BY0 + 14, x + 26, BY0 + 86)
    d = ImageDraw.Draw(img)
    d.rectangle([x - 14, ty - 18, x + 14, ty + 8], fill=(40, 90, 170, 255))
    A.over(img, A.rivet(18, A.hexc(0xb8b8b2), 7), (x - 11, ty - 16))
    d.line([(x, ty - 6), (x + 5, ty - 12)], fill=(40, 40, 40), width=2)
    silk(x, ty + 24, "TUNE")
    # LEDs and the OLED on the right PCB
    rx0 = BX1 - 400
    pcb(rx0, BY0 + 14, BX1 - 50, BY0 + 86)
    if lvl >= 3:
        pass
    for i, (nm, col, on) in enumerate((("ALARM", (240, 50, 40), lvl >= 3), ("NET", (60, 230, 80), True), ("SICK", (255, 160, 40), True), ("POD", (60, 140, 255), False))):
        led(rx0 + 34 + i * 40, ty - 6, col, on)
        silk(rx0 + 34 + i * 40, ty + 24, nm, f7)
    ox = rx0 + 196
    d = ImageDraw.Draw(img)
    d.rectangle([ox, ty - 26, ox + 140, ty + 20], fill=(20, 70, 160, 255))
    for k in range(4):
        d.ellipse([ox + 3 + k * 7, ty - 24, ox + 7 + k * 7, ty - 20], fill=(210, 180, 80, 255))
    d.rectangle([ox + 8, ty - 16, ox + 132, ty + 16], fill=(4, 6, 10, 255))
    d.text((ox + 14, ty - 12), "RF 2.4", font=f8, fill=(255, 230, 80, 255), anchor="lt")
    for i in range(10):
        h = 3 + i * 1.5
        on = i < 7 or lvl >= 3 and i == 9
        d.rectangle([ox + 14 + i * 11, ty + 12 - h, ox + 20 + i * 11, ty + 12], fill=(90, 220, 255, 255) if on else dim((90, 220, 255), bg=(4, 6, 10)))
    silk(ox + 70, ty + 30, "RF / LINK", f7)

    # ---- the rainbow ribbon cable down the left rail, zip-tied
    rbx = BX0 + 22
    cols = [(150, 80, 40), (220, 50, 40), (240, 140, 40), (240, 220, 60), (60, 170, 70), (50, 100, 220), (140, 70, 190), (150, 150, 150)]
    for k, cc in enumerate(cols):
        d.line([(rbx + k * 3, BY0 + 80), (rbx + k * 3, BY1 - 80)], fill=cc + (255,), width=3)
    for y in (SY0 + 120, SY0 + 380, SY1 - 100):
        d.rounded_rectangle([rbx - 4, y, rbx + 27, y + 6], 2, fill=(28, 28, 30, 255))
        d.rectangle([rbx + 27, y - 1, rbx + 33, y + 7], fill=(28, 28, 30, 255))
    # a breadboard scrap and vents on the right rail
    for i in range(7):
        d.rounded_rectangle([BX1 - 44, SY0 + 120 + i * 12, BX1 - 18, SY0 + 125 + i * 12], 2, fill=(70, 72, 72, 255))
    A.over(img, S.text_sticker("NO ICE", 13, (255, 255, 255), (220, 50, 40), seed=3), (BX1 - 56, SY1 - 120))

    # ---- bottom: a 16x2 LCD module, three big tactile buttons, an LED bargraph, a toggle switch
    yb = SY1 + 30
    lx0 = BX0 + 52
    pcb(lx0, yb - 16, lx0 + 270, yb + 60)
    d = ImageDraw.Draw(img)
    d.rectangle([lx0 + 14, yb - 4, lx0 + 256, yb + 46], fill=(20, 20, 20, 255))
    d.rectangle([lx0 + 20, yb + 2, lx0 + 250, yb + 40], fill=(160, 196, 40, 255))
    mono = S.jfont(15) if False else A.font(13, False)
    d.text((lx0 + 28, yb + 6), "DATA 07  HEAT %d" % heat, font=mono, fill=(30, 50, 10, 255))
    d.text((lx0 + 28, yb + 22), "D2 14:20  NET ON", font=mono, fill=(30, 50, 10, 255))
    kx = BX0 + 380
    pcb(kx - 28, yb - 16, kx + 3 * 56 - 28, yb + 60)
    for i, (nm, cap) in enumerate((("BACK", (220, 60, 50)), ("CORE", (240, 200, 50)), ("HELP", (60, 120, 220)))):
        tact(kx + i * 56, yb + 14, cap)
        silk(kx + i * 56, yb + 44, nm)
    hx = BX0 + 610
    pcb(hx - 20, yb - 16, hx + 230, yb + 60)
    d = ImageDraw.Draw(img)
    d.rectangle([hx, yb, hx + 210, yb + 26], fill=(26, 26, 28, 255))
    seg_cols = [HEAT_SEG[min(4, i // 2)] for i in range(10)]
    lit = int(round(heat / 10))
    for i in range(10):
        cc = seg_cols[i]
        r = (hx + 6 + i * 20, yb + 4, hx + 20 + i * 20, yb + 22)
        if i < lit:
            A.glow(img, r, cc, 0.4, 3)
        ImageDraw.Draw(img).rectangle(r, fill=cc + (255,) if i < lit else dim(cc, bg=(26, 26, 28)))
    silk(hx + 105, yb + 44, "HEAT")
    # toggle
    sx = BX1 - 120
    pcb(sx - 30, yb - 16, sx + 50, yb + 60)
    A.over(img, A.rivet(26, A.hexc(0xc8c8c0), 11), (sx - 3, yb - 3))
    d = ImageDraw.Draw(img)
    d.line([(sx + 10, yb + 12), (sx + 20, yb - 6)], fill=(220, 220, 214, 255), width=5)
    d.ellipse([sx + 16, yb - 10, sx + 24, yb - 2], fill=(235, 235, 228, 255))
    silk(sx + 10, yb + 44, "NET  ON")
    return img


if __name__ == "__main__":
    heat = int(sys.argv[sys.argv.index("--heat") + 1]) if "--heat" in sys.argv else 58
    out = os.path.join(os.path.dirname(__file__), "..", "docs", "mockups")
    cyber(heat).save(os.path.join(out, "46b-classic-cyberdeck.png"))
    diy(heat).save(os.path.join(out, "46c-classic-diy.png"))
    print("written")
