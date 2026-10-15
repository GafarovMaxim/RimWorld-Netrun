#!/usr/bin/env python3
"""
Relic deck, third pass (2026-10-15): Maxim picked the instrument column (48a) and asked for richer detail.
The column is now its own inset module with screws, engraved section lines, dymo labels and a vent grille; the gauge
has a chrome ring, numbers and a glass highlight; knobs sit on printed scales; buttons have chrome guard rings;
the shell gets scratches, stains and worn engraved text. Mockup only.

    python tools/mock_relic3.py [--heat 58]   writes docs/mockups/48c-relic-column-v2.png
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

import deck_art as A
import deck_stickers as S

SCREEN = (930, 666)
OUT = 8
IL, IT, IR, IB = 60, 96, 168, 100
W, H = SCREEN[0] + IL + IR + 2 * OUT, SCREEN[1] + IT + IB + 2 * OUT
BX0, BY0, BX1, BY1 = OUT, OUT, W - OUT, H - OUT
SX0, SY0, SX1, SY1 = BX0 + IL, BY0 + IT, BX1 - IR, BY1 - IB
CX0, CX1 = SX1 + 34, BX1 - 12              # the instrument column module
CY0, CY1 = SY0 - 30, SY1 + 30
MX = (CX0 + CX1) / 2

HEAT_SEG = [(49, 196, 106), (230, 196, 49), (255, 138, 60), (255, 74, 60), (179, 18, 42)]
INK = (50, 46, 40, 255)
CREAM = (214, 206, 186)
ORANGE, RED, YELLOW = (226, 112, 44), (196, 54, 40), (236, 190, 48)


def level_of(heat):
    return 0 if heat < 20 else 1 if heat < 45 else 2 if heat < 70 else 3 if heat < 90 else 4


def dim(c, k=0.3, bg=(40, 38, 34)):
    return tuple(int(v * k + b * (1 - k)) for v, b in zip(c, bg)) + (255,)


def rrect(box, r):
    x0, y0, x1, y1 = box
    return A.mask((W, H), lambda d, s: d.rounded_rectangle([x0 * s, y0 * s, x1 * s - 1, y1 * s - 1], r * s, fill=255))


def lines_mask(segs, width=2):
    return A.mask((W, H), lambda d, s: [d.line([(a * s, b * s), (c * s, e * s)], fill=255, width=int(width * s)) for a, b, c, e in segs])


def shell(heat):
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    img = Image.new("RGBA", (W, H), (22, 23, 22, 255))
    full = rrect((BX0, BY0, BX1, BY1), 30)
    bez = rrect((SX0 - 22, SY0 - 22, SX1 + 22, SY1 + 22), 16)
    hole = rrect((SX0, SY0, SX1, SY1), 8)
    col = rrect((CX0, CY0, CX1, CY1), 10)
    plate = rrect((BX0 + 418, BY0 + 10, CX0 - 16, BY0 + 68), 8)
    lcdf = rrect((BX0 + 40, SY1 + 30, BX0 + 316, BY1 - 10), 8)
    img.alpha_composite(A.to_img(np.zeros((H, W, 3), np.float32), np.roll(np.roll(A.blur(full, 5), 5, 0), 2, 1) * 0.75))

    rgb = np.array(CREAM, np.float32) + A.fbm(W, H, 120, 4, 301)[..., None] * np.array([16, 14, 8], np.float32) + A.grain(W, H, 302, 3)[..., None]
    rgb += (A.shade(full, 3) * 42)[..., None]
    # the column sits a step lower: a dark gap round it, its face lit from the top-left
    gap = np.clip(A.blur(col, 1.5) - A.blur(1 - A.blur(col, 0.1), 0.1) * 0, 0, 1)
    ring = np.clip(rrect((CX0 - 3, CY0 - 3, CX1 + 3, CY1 + 3), 12) - col, 0, 1)
    rgb -= ring[..., None] * 110
    rgb += (A.shade(col, 2, (1, 1)) * 34)[..., None]
    rgb -= col[..., None] * 8
    # engraved section lines in the column and the shell seams
    segs = [(BX0 + 4, SY0 - 34, CX0 - 6, SY0 - 34), (BX0 + 4, SY1 + 34, CX0 - 6, SY1 + 34)]
    segs += [(CX0 + 6, y, CX1 - 6, y) for y in (CY0 + 96, CY0 + 300, CY0 + 400, CY0 + 520, CY0 + 630)]
    seam = lines_mask(segs)
    rgb -= (seam * 70)[..., None] - (np.roll(seam, 1, 0) * 22)[..., None]
    # the dark top plate for the keys and lamps, the raised cream frame round the LCD
    rgb -= (rrect((BX0 + 416, BY0 + 8, CX0 - 14, BY0 + 70), 9) - plate).clip(0, 1)[..., None] * 90
    prgb = np.array((52, 52, 50), np.float32) + A.grain(W, H, 303, 3)[..., None] + (A.shade(plate, 2, (1, 1)) * 30)[..., None]
    rgb = rgb * (1 - plate[..., None]) + prgb * plate[..., None]
    rgb += (A.shade(lcdf, 2.5) * 40)[..., None]

    # wear: grime, big chips to rusty metal at edges, seams and corners, rust halos, scratches, stains
    edge = 1 - A.blur(full, 16)
    near = A.blur(seam, 5) + A.blur(ring, 5) * 1.2
    dirt = np.clip(A.fbm(W, H, 26, 4, 305) * 1.5 + edge * 1.3 + near * 1.4 - 0.1, 0, 1)
    rgb = rgb * (1 - dirt[..., None] * 0.4) + np.array((92, 74, 50), np.float32) * (dirt[..., None] * 0.4)
    chips_n = A.fbm(W, H, 46, 5, 306) * 0.8 + A.fbm(W, H, 9, 3, 314) * 0.35 + edge * 1.2 + near * 0.7 + A.blur(bez, 10) * 0.2
    chips = np.clip((chips_n - 0.55) * 10, 0, 1) * (1 - plate)
    metal = np.array((74, 68, 60), np.float32) + A.fbm(W, H, 4, 2, 307)[..., None] * 34
    rustm = np.clip(A.fbm(W, H, 10, 3, 308) * 2.2 + 0.45, 0, 1)[..., None]
    under = metal * (1 - rustm * 0.8) + np.array((132, 64, 28), np.float32) * (rustm * 0.8)
    rgb = rgb * (1 - chips[..., None]) + under * chips[..., None]
    rgb -= (A.blur(chips, 1.3) - chips).clip(0, 1)[..., None] * 70
    halo = np.clip(A.blur(chips, 5) - chips, 0, 1) * rustm[..., 0]
    rgb = rgb * (1 - halo[..., None] * 0.5) + np.array((150, 84, 40), np.float32) * (halo[..., None] * 0.5)
    r = A.rng(315)
    scr = []
    for _ in range(90):
        x, y = r.uniform(BX0 + 10, BX1 - 10), r.uniform(BY0 + 10, BY1 - 10)
        a, ln = r.uniform(-0.5, 0.5) + (np.pi / 2 if r.random() < 0.3 else 0), r.uniform(8, 60)
        scr.append((x, y, x + np.cos(a) * ln, y + np.sin(a) * ln))
    sc = A.blur(lines_mask(scr, 1), 0.4) * (1 - hole)
    rgb += sc[..., None] * np.array([22, 20, 16], np.float32) - (np.roll(sc, 1, 0) * 18)[..., None]
    stain = np.clip((A.fbm(W, H, 60, 3, 316) - 0.6) * 4, 0, 1) * 0.3
    rgb = rgb * (1 - stain[..., None]) + np.array((150, 120, 70), np.float32) * stain[..., None]

    # the dark bezel
    brgb = np.array((44, 45, 44), np.float32) + A.fbm(W, H, 40, 3, 309)[..., None] * 8 + A.grain(W, H, 310, 3)[..., None]
    brgb += (A.shade(bez, 2.5) * 46)[..., None] - (A.shade(hole, 2, (1, 1)) * 30)[..., None]
    brgb += (np.clip((A.fbm(W, H, 6, 3, 311) - 0.62) * 6, 0, 1) * 20)[..., None] + sc[..., None] * 30
    rgb = rgb * (1 - bez[..., None]) + brgb * bez[..., None]
    img.alpha_composite(A.to_img(rgb, full * (1 - hole)))
    return img, yy


def relic(heat):
    lvl = level_of(heat)
    img, yy = shell(heat)
    d = ImageDraw.Draw(img)
    d.rectangle([SX0, SY0, SX1 - 1, SY1 - 1], fill=(12, 16, 16, 255))
    lv = ["SHADOW", "NOTICED", "TRACKED", "TARGET", "RED ZONE"][lvl]
    for i, (t, c) in enumerate((("NETRUN // NETWORK CORE", (90, 230, 130)), ("> HOME NODE ........ ONLINE", (90, 210, 130)),
                                ("> GATEWAY .......... 5 NODES", (90, 210, 130)), (f"> HEAT {heat} .......... {lv}", (230, 190, 70)))):
        d.text((SX0 + 30, SY0 + 28 + i * 24), t, font=A.font(15), fill=c)

    def screw(x, y, n=12, base=0x8a8478):
        A.over(img, A.rivet(n, A.hexc(base), int(x * 7 + y)), (x - n / 2 - 2, y - n / 2 - 2))
        a = A.rng(int(x + y * 3)).uniform(0, np.pi)
        dd = ImageDraw.Draw(img)
        for b in (a, a + np.pi / 2):
            dd.line([(x - np.cos(b) * n * 0.32, y - np.sin(b) * n * 0.32), (x + np.cos(b) * n * 0.32, y + np.sin(b) * n * 0.32)], fill=(40, 38, 34, 255), width=2)

    def run(x, y, ln=None):
        r = A.rng(int(x * 5 + y))
        ln = ln or r.uniform(24, 80)
        m = A.mask((W, H), lambda dr, s: dr.line([(x * s, y * s), ((x + r.uniform(-4, 4)) * s, (y + ln) * s)], fill=255, width=int(4 * s)))
        m = A.blur(m, 1.8) * np.clip(1 - (yy - y) / ln, 0, 1)
        img.alpha_composite(A.to_img(np.broadcast_to(np.array((112, 56, 26), np.float32), (H, W, 3)).copy(), m * 0.75))

    def dymo(x, y, text, size=8, angle=0.0, col=(26, 26, 28)):
        """An embossed label-maker strip: dark tape, raised white letters, slightly crooked."""
        f = A.font(size)
        tw = int(ImageDraw.Draw(img).textlength(text, font=f)) + 12
        k = S.Big(tw + 4, size + 10)
        k.rect((2, 3, tw + 1, size + 8), (10, 10, 10, 120), r=2)
        k.rect((1, 1, tw, size + 6), col + (255,), r=2)
        k.text(((tw + 1) / 2, (size + 7) / 2 + 0.5), text, A.font, size, (238, 236, 228, 255))
        lab = A.spray(k.done(), int(x + y), 0.97).rotate(angle, expand=True, resample=Image.BICUBIC)
        A.over(img, lab, (x - lab.width / 2, y - lab.height / 2))

    for x, y in ((SX0 - 11, SY0 - 11), (SX1 + 11, SY0 - 11), (SX0 - 11, SY1 + 11), (SX1 + 11, SY1 + 11)):
        run(x, y + 4, 40)
        screw(x, y, 13)
    for x, y in ((BX0 + 22, BY0 + 22), (BX1 - 22, BY0 + 22), (BX0 + 22, BY1 - 22), (BX1 - 22, BY1 - 22), (BX0 + 18, (SY0 + SY1) / 2)):
        if y < BY1 - 40:
            run(x, y + 4)
        screw(x, y, 12)
    for x, y in ((CX0 + 12, CY0 + 12), (CX1 - 12, CY0 + 12), (CX0 + 12, CY1 - 12), (CX1 - 12, CY1 - 12)):
        run(x, y + 4, 30)
        screw(x, y, 10)

    # worn engraved text up the left rail
    lay = Image.new("RGBA", (SY1 - SY0, 16), (0, 0, 0, 0))
    ImageDraw.Draw(lay).text((0, 8), "NR-02 FIELD UNIT  ·  OLD NET CERT. 7741  ·  KEEP DRY  ·  DO NOT OPEN WHILE LIVE  ·  MADE IN NIGHT CITY",
                             font=A.font(8), fill=(70, 64, 54, 200), anchor="lm")
    A.over(img, A.spray(lay, 330, 0.7).rotate(90, expand=True), (BX0 + 8, SY0 + 10))

    # ---- top left: worn maker's name, model line, number
    A.over(img, A.spray(A.text_layer((W, H), "NETRUN", A.font(24), (58, 54, 48, 255), (BX0 + 48, BY0 + 16)), 312, 0.8), (0, 0))
    d = ImageDraw.Draw(img)
    d.text((BX0 + 50, BY0 + 52), "FIELD UNIT  NR-02 // OLD NET CERT.", font=A.font(8), fill=INK, anchor="lm")
    A.over(img, A.spray(A.text_layer((W, H), "02", A.font(38), (190, 96, 50, 255), (BX0 + 270, BY0 + 14)), 313, 0.78), (0, 0))

    # ---- top plate: rubber keys, RF LCD, jewel lamps
    def rubber(x, y, cap, w=34, h=24):
        k = S.Big(w + 4, h + 6)
        k.rect((2, 4, w + 1, h + 4), (20, 20, 18, 255), r=6)
        k.rect((2, 1, w + 1, h + 1), cap + (255,), r=6)
        k.rect((5, 3, w - 2, 8), tuple(min(255, int(v * 1.2)) for v in cap) + (255,), r=4)
        A.over(img, k.done(), (x - w / 2 - 2, y - h / 2 - 1))

    def jewel(x, y, col, on, n=12):
        A.over(img, A.rivet(n + 7, A.hexc(0xa8a498), int(x)), (x - (n + 7) / 2 - 2, y - (n + 7) / 2 - 2))
        if on:
            A.glow(img, (x - 5, y - 5, x + 5, y + 5), col, 0.7, 6)
        A.over(img, A.rivet(n, np.array(col if on else tuple(int(v * 0.3) for v in col), np.float32), int(x) + 1), (x - n / 2 - 2, y - n / 2 - 2))

    light = (214, 208, 192, 255)
    cy, ly = BY0 + 32, BY0 + 58
    kx = BX0 + 452
    for i, (nm, cap) in enumerate((("CLOSE", RED), ("MENU", (126, 126, 120)), ("ACHIEV", (126, 126, 120)), ("NET LOCK", ORANGE))):
        x = kx + i * 50
        rubber(x, cy, cap)
        ImageDraw.Draw(img).text((x, ly), nm, font=A.font(8), fill=light, anchor="mm")
    for x in (BX0 + 428, CX0 - 26):
        screw(x, BY0 + 20, 9, 0x6a665e)
        screw(x, BY0 + 58, 9, 0x6a665e)
    ox = kx + 212
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([ox - 5, cy - 20, ox + 96, cy + 18], 4, fill=(24, 24, 22, 255))
    d.rectangle([ox, cy - 15, ox + 91, cy + 13], fill=(150, 158, 112, 255))
    d.text((ox + 4, cy - 13), "RF", font=A.font(9), fill=(40, 46, 30, 255), anchor="lt")
    for i in range(7):
        h = 4 + i * 3
        on = i < 5 or (lvl >= 3 and i == 6)
        d.rectangle([ox + 24 + i * 9, cy + 9 - h, ox + 29 + i * 9, cy + 9], fill=(40, 46, 30, 255) if on else (136, 144, 102, 255))
    A.glow(img, (ox, cy - 15, ox + 91, cy + 13), (190, 200, 140), 0.08, 5)
    ImageDraw.Draw(img).text((ox + 45, ly), "LINK", font=A.font(8), fill=light, anchor="mm")
    lx = ox + 130
    for i, (nm, col, on) in enumerate((("ALARM", (230, 50, 36), lvl >= 3), ("NET", (90, 210, 90), True), ("SICK", (240, 150, 40), True), ("POD", (90, 160, 230), False))):
        x = lx + i * 34
        jewel(x, cy, col, on)
        ImageDraw.Draw(img).text((x, ly), nm, font=A.font(8), fill=light, anchor="mm")

    # ---- the instrument column
    def scale_ring(x, y, r0, r1, a0, a1, n, nums=None, col=INK):
        dd = ImageDraw.Draw(img)
        for i in range(n):
            a = np.radians(a0 + (a1 - a0) * i / (n - 1))
            dd.line([(x + np.cos(a) * r0, y + np.sin(a) * r0), (x + np.cos(a) * r1, y + np.sin(a) * r1)], fill=col, width=1)
            if nums and i % (n // (len(nums) - 1)) == 0:
                dd.text((x + np.cos(a) * (r1 + 7), y + np.sin(a) * (r1 + 7)), nums[i // (n // (len(nums) - 1))], font=A.font(7), fill=col, anchor="mm")

    def knob(x, y, r, cap=(34, 32, 30), pointer=-2.2):
        k = S.Big(2 * r + 10, 2 * r + 12)
        c = r + 5
        k.ellipse((c - r - 3, c - r, c + r + 3, c + r + 5), (18, 16, 14, 150))
        k.ellipse((c - r, c - r - 1, c + r, c + r - 1), (150, 148, 142, 255))            # chrome skirt
        k.ellipse((c - r + 2, c - r + 1, c + r - 2, c + r - 3), (196, 194, 188, 255))
        k.ellipse((c - r * 0.72, c - r * 0.75, c + r * 0.72, c + r * 0.69), cap + (255,))
        for i in range(20):
            a = i * np.pi / 10
            k.line([(c + np.cos(a) * r * 0.6, c - 3 + np.sin(a) * r * 0.6), (c + np.cos(a) * r * 0.7, c - 3 + np.sin(a) * r * 0.7)], (64, 60, 56, 255), 1)
        k.ellipse((c - r * 0.42, c - r * 0.48, c + r * 0.42, c + r * 0.36), tuple(int(v * 1.5) for v in cap) + (255,))
        k.line([(c + np.cos(pointer) * r * 0.2, c - 3 + np.sin(pointer) * r * 0.2), (c + np.cos(pointer) * r * 0.66, c - 3 + np.sin(pointer) * r * 0.66)], (236, 232, 220, 255), 2)
        A.over(img, k.done(), (x - c, y - c + 2))

    def gauge(x, y, r, v):
        g = S.Big(2 * r + 12, 2 * r + 12)
        c = r + 6
        g.ellipse((c - r - 5, c - r - 3, c + r + 5, c + r + 7), (14, 12, 10, 140))
        for i, col in enumerate(((96, 94, 90), (210, 208, 202), (150, 148, 142), (120, 118, 112))):   # chrome ring
            g.ellipse((c - r - 4 + i * 1.5, c - r - 4 + i * 1.5, c + r + 4 - i * 1.5, c + r + 4 - i * 1.5), col + (255,))
        g.ellipse((c - r + 3, c - r + 3, c + r - 3, c + r - 3), (240, 204, 66, 255))
        g.ellipse((c - r + 5, c - r + 5, c + r - 5, c + r - 5), (236, 198, 60, 255))
        for i in range(41):
            a = np.radians(150 + i * 6)
            big = i % 4 == 0
            r0 = r * (0.66 if big else 0.72)
            g.line([(c + np.cos(a) * r0, c + np.sin(a) * r0), (c + np.cos(a) * r * 0.8, c + np.sin(a) * r * 0.8)], (40, 32, 18, 255), 2 if big else 1)
            if big:
                g.text((c + np.cos(a) * r * 0.53, c + np.sin(a) * r * 0.53), str(i // 4), A.font, 8, (40, 32, 18, 255))
        red = [np.radians(a) for a in np.linspace(342, 390, 12)]
        g.line([(c + np.cos(a) * r * 0.84, c + np.sin(a) * r * 0.84) for a in red], (196, 40, 30, 255), 4)
        g.text((c, c + r * 0.3), "TUNE", A.font, 10, (40, 32, 18, 255))
        g.text((c, c + r * 0.48), "MHz x10", A.font, 6, (70, 60, 36, 255))
        g.text((c, c - r * 0.3), "NR", A.font, 7, (120, 96, 30, 255))
        a = np.radians(150 + 240 * v)
        g.line([(c - np.cos(a) * r * 0.16, c - np.sin(a) * r * 0.16), (c + np.cos(a) * r * 0.78, c + np.sin(a) * r * 0.78)], (176, 30, 24, 255), 2)
        g.ellipse((c - 5, c - 5, c + 5, c + 5), (30, 28, 26, 255))
        g.ellipse((c - 2, c - 3, c + 1, c), (110, 106, 100, 255))
        A.over(img, g.done(), (x - c, y - c))
        # glass: a soft crescent highlight and a little dust
        gl = np.zeros((H, W), np.float32)
        hl = A.mask((W, H), lambda dr, s: dr.ellipse([(x - r * 0.8) * s, (y - r * 0.86) * s, (x + r * 0.5) * s, (y + r * 0.1) * s], fill=255))
        cut = A.mask((W, H), lambda dr, s: dr.ellipse([(x - r * 0.7) * s, (y - r * 0.62) * s, (x + r * 0.7) * s, (y + r * 0.4) * s], fill=255))
        gl = A.blur(np.clip(hl - cut, 0, 1), 2) * 0.35
        img.alpha_composite(A.to_img(np.full((H, W, 3), 255, np.float32), gl))
        for i, (sx, sy) in enumerate(((0.3, 0.2), (-0.4, 0.35), (0.1, -0.5))):
            screw(x + np.cos(np.radians(45 + i * 120)) * (r + 1), y + np.sin(np.radians(45 + i * 120)) * (r + 1), 6, 0x9a968c)

    # 1. two knobs on printed scales, VOL and SQL
    for nm, x in (("VOL", MX - 36), ("SQL", MX + 36)):
        y = CY0 + 50
        scale_ring(x, y, 22, 26, 135, 405, 11)
        knob(x, y, 17, pointer=np.radians(135 + 270 * (0.55 if nm == "VOL" else 0.3)))
        dymo(x, y + 36, nm, 7, A.rng(int(x)).uniform(-2, 2))
    # 2. the yellow TUNE gauge
    gauge(MX, CY0 + 196, 58, 0.62)
    dymo(MX, CY0 + 280, "TUNE / RX", 8, -1.2)
    # 3. a black plate with a chrome toggle and the ICE lamp
    py = CY0 + 318
    pl = S.Big(122, 70)
    pl.rect((1, 3, 121, 69), (14, 14, 12, 160), r=6)
    pl.rect((0, 0, 119, 66), (36, 36, 34, 255), r=6)
    pl.rect((3, 3, 116, 63), (48, 48, 46, 255), r=5)
    A.over(img, pl.done(), (MX - 60, py))
    for sx, sy in ((MX - 52, py + 8), (MX + 52, py + 8), (MX - 52, py + 58), (MX + 52, py + 58)):
        screw(sx, sy, 6, 0x7a766e)
    hx, hy = MX - 24, py + 34
    nut = S.Big(26, 26)
    nut.poly([(13 + np.cos(a) * 11, 13 + np.sin(a) * 11) for a in np.radians(np.arange(0, 360, 60) + 30)], (176, 174, 168, 255), (90, 88, 84, 255), 1)
    nut.ellipse((7, 7, 19, 19), (120, 118, 112, 255))
    A.over(img, nut.done(), (hx - 13, hy - 13))
    tg = S.Big(30, 40)
    tg.line([(15, 30), (21, 6)], (90, 88, 84, 255), 6)
    tg.line([(15, 30), (21, 6)], (214, 212, 206, 255), 3.5)
    tg.ellipse((16, 1, 26, 11), (226, 224, 218, 255), (110, 108, 104, 255), 1)
    A.over(img, tg.done(), (hx - 15, hy - 30))
    jewel(MX + 28, py + 32, (230, 60, 40), lvl >= 3, 15)
    d = ImageDraw.Draw(img)
    d.text((hx, py + 56), "SCAN", font=A.font(7), fill=light, anchor="mm")
    d.text((MX + 28, py + 56), "ICE", font=A.font(7), fill=light, anchor="mm")
    # 4. a chicken-head pointer knob on an arc scale, TRACE
    ky = CY0 + 460
    scale_ring(MX, ky, 34, 40, 200, 340, 5, nums=["1", "2", "3", "4", "5"])
    kk = S.Big(80, 80)
    kk.ellipse((14, 18, 66, 70), (14, 12, 10, 140))
    kk.ellipse((16, 14, 64, 62), (150, 148, 142, 255))
    kk.ellipse((18, 16, 62, 60), (196, 194, 188, 255))
    pts = [(40, 6), (47, 30), (48, 52), (40, 58), (32, 52), (33, 30)]
    kk.poly([(x, y + 2) for x, y in pts], (10, 10, 10, 160))
    kk.poly(pts, (30, 28, 26, 255))
    kk.poly([(40, 9), (44, 30), (40, 34), (36, 30)], (58, 56, 52, 255))
    kk.line([(40, 10), (40, 24)], (236, 232, 220, 255), 2)
    k_img = kk.done().rotate(-25, resample=Image.BICUBIC)
    A.over(img, k_img, (MX - 40, ky - 40))
    dymo(MX, ky + 46, "TRACE", 8, 1.5)
    # 5. orange and red buttons in chrome guard rings
    by = CY0 + 576
    for x, col, nm in ((MX - 32, ORANGE, "JACK"), (MX + 32, RED, "PURGE")):
        k = S.Big(54, 56)
        k.ellipse((3, 6, 51, 54), (14, 12, 10, 140))
        k.ellipse((2, 2, 50, 50), (110, 108, 104, 255))
        k.ellipse((4, 4, 48, 48), (200, 198, 192, 255))
        k.ellipse((8, 8, 44, 44), (24, 22, 20, 255))
        k.ellipse((10, 9, 42, 41), col + (255,))
        k.ellipse((15, 12, 33, 24), tuple(min(255, int(v * 1.3)) for v in col) + (255,))
        A.over(img, k.done(), (x - 26, by - 26))
        dymo(x, by + 36, nm, 7, A.rng(int(x)).uniform(-2.5, 2.5))
    # 6. a vent grille and the barcode
    vy = CY0 + 652
    d = ImageDraw.Draw(img)
    for i in range(7):
        y = vy + i * 7
        d.rounded_rectangle([MX - 46, y, MX + 46, y + 3], 1, fill=(28, 26, 24, 255))
        d.line([(MX - 45, y + 4), (MX + 45, y + 4)], fill=(236, 230, 214, 140), width=1)
    bc = S.barcode(70, 22, "NR-02", 321)
    A.over(img, A.spray(bc, 321, 0.93), (MX - 36, CY1 - 34))

    # ---- bottom: the orange LCD in its raised frame, stickers, round keys, Heat lamps, NET LINK
    cb, lb = SY1 + 58, SY1 + 86
    vx = BX0 + 56
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([vx - 6, cb - 25, vx + 246, cb + 25], 5, fill=(28, 28, 26, 255))
    d.rectangle([vx, cb - 19, vx + 240, cb + 19], fill=(238, 124, 40, 255))
    A.glow(img, (vx, cb - 19, vx + 240, cb + 19), (255, 140, 40), 0.15, 6)
    for i, t in enumerate(("DATA 07   HEAT %d" % heat, "D2 14:20  NET ON")):
        ImageDraw.Draw(img).text((vx + 10, cb - 9 + i * 17), t, font=A.font(13, False), fill=(60, 24, 6, 255), anchor="lm")
    for x in (vx - 18, vx + 258):
        screw(x, cb, 9)
    st = S.Big(92, 46)
    st.rect((0, 0, 91, 45), YELLOW + (255,), r=2)
    st.rect((3, 3, 88, 13), (30, 28, 24, 255), r=1)
    st.text((46, 8), "PROPERTY OF", A.font, 7, YELLOW + (255,))
    st.text((46, 24), "V-NR 02", A.font, 13, (30, 28, 24, 255))
    st.text((46, 37), "5X65  OLD", A.font, 9, (30, 28, 24, 255))
    A.over(img, A.spray(st.done(), 323, 0.8).rotate(2, expand=True), (vx + 280, cb - 26))
    pp = S.Big(96, 50)
    pp.rect((0, 0, 95, 49), (226, 220, 204, 255), r=2)
    pp.text((30, 25), "遺物", S.jfont, 22, (178, 50, 40, 255))
    for i, t in enumerate(("S/N 4471", "TYPE II", "EXPORT")):
        pp.text((76, 12 + i * 12), t, A.font, 7, (60, 56, 50, 255))
    A.over(img, A.spray(pp.done(), 324, 0.86).rotate(-1.5, expand=True), (vx + 382, cb - 25))
    kx = vx + 512
    for i, nm in enumerate(("BACK", "CORE", "HELP")):
        x = kx + i * 46
        A.over(img, A.rivet(34, A.hexc(0xb8b09a), x), (x - 19, cb - 19))
        A.over(img, A.rivet(26, A.hexc(0x2e2c2a), x + 1), (x - 15, cb - 15))
        ImageDraw.Draw(img).text((x, lb), nm, font=A.font(8), fill=INK, anchor="mm")
    hx = kx + 150
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([hx - 12, cb - 18, hx + 5 * 30 + 2, cb + 18], 4, fill=(28, 28, 26, 255))
    for i in range(5):
        x = hx + 8 + i * 30
        cc = HEAT_SEG[i]
        on = i <= lvl
        if on:
            A.glow(img, (x - 9, cb - 8, x + 9, cb + 8), cc, 0.5, 4)
        ImageDraw.Draw(img).rectangle([x - 10, cb - 9, x + 10, cb + 9], fill=cc + (255,) if on else dim(cc))
    ImageDraw.Draw(img).text((hx + 70, lb), "HEAT", font=A.font(8), fill=INK, anchor="mm")
    sx = CX0 - 104
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([sx, cb - 10, sx + 80, cb + 10], 5, fill=(28, 28, 26, 255))
    d.rounded_rectangle([sx + 46, cb - 8, sx + 76, cb + 8], 3, fill=ORANGE + (255,))
    for i in range(4):
        d.line([(sx + 52 + i * 6, cb - 5), (sx + 52 + i * 6, cb + 5)], fill=(170, 80, 30, 255), width=1)
    d.text((sx - 4, cb), "OFF", font=A.font(8), fill=INK, anchor="rm")
    d.text((sx + 84, cb), "ON", font=A.font(8), fill=INK, anchor="lm")
    d.text((sx + 40, lb), "NET LINK", font=A.font(8), fill=INK, anchor="mm")
    return img


if __name__ == "__main__":
    heat = int(sys.argv[sys.argv.index("--heat") + 1]) if "--heat" in sys.argv else 58
    out = os.path.join(os.path.dirname(__file__), "..", "docs", "mockups")
    relic(heat).save(os.path.join(out, "48c-relic-column-v2.png"))
    print("written")
