#!/usr/bin/env python3
"""
Relic deck, second pass after Maxim's references (2026-10-15): a battered field unit in cream plastic with paint worn
through to rusty metal, the screen in a thick dark bezel with screws, a yellow dial gauge, knurled knobs, orange and red
round buttons, a yellow inventory sticker and a paper label with kanji. Mockups only.

    A  instrument column  the right side widens into a column: knobs, the yellow TUNE gauge, a toggle, orange/red buttons
    B  same layout        the current Relic layout (thin right rail), restyled after the references

    python tools/mock_relic2.py [--heat 58]   writes docs/mockups/48a-relic-column.png and 48b-relic-classic.png
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

import deck_art as A
import deck_stickers as S

SCREEN = (930, 666)
OUT = 8
HEAT_SEG = [(49, 196, 106), (230, 196, 49), (255, 138, 60), (255, 74, 60), (179, 18, 42)]
INK = (50, 46, 40, 255)
CREAM = (214, 206, 186)
ORANGE, RED, YELLOW = (226, 112, 44), (196, 54, 40), (236, 190, 48)


def level_of(heat):
    return 0 if heat < 20 else 1 if heat < 45 else 2 if heat < 70 else 3 if heat < 90 else 4


def dim(c, k=0.3, bg=(40, 38, 34)):
    return tuple(int(v * k + b * (1 - k)) for v, b in zip(c, bg)) + (255,)


def relic(heat, column):
    lvl = level_of(heat)
    il, it, ir, ib = 60, 96, (168 if column else 64), 100
    W, H = SCREEN[0] + il + ir + 2 * OUT, SCREEN[1] + it + ib + 2 * OUT
    bx0, by0, bx1, by1 = OUT, OUT, W - OUT, H - OUT
    sx0, sy0, sx1, sy1 = bx0 + il, by0 + it, bx1 - ir, by1 - ib
    img = Image.new("RGBA", (W, H), (22, 23, 22, 255))
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)

    full = A.mask((W, H), lambda d, s: d.rounded_rectangle([bx0 * s, by0 * s, bx1 * s - 1, by1 * s - 1], 30 * s, fill=255))
    bez = A.mask((W, H), lambda d, s: d.rounded_rectangle([(sx0 - 22) * s, (sy0 - 22) * s, (sx1 + 22) * s - 1, (sy1 + 22) * s - 1], 16 * s, fill=255))
    hole = A.mask((W, H), lambda d, s: d.rounded_rectangle([sx0 * s, sy0 * s, sx1 * s - 1, sy1 * s - 1], 8 * s, fill=255))
    sh = np.roll(np.roll(A.blur(full, 5), 5, 0), 2, 1) * 0.75
    img.alpha_composite(A.to_img(np.zeros((H, W, 3), np.float32), sh))

    # ---- the cream shell: mottled, raised edges, grooved seams
    rgb = np.array(CREAM, np.float32) + A.fbm(W, H, 120, 4, 201)[..., None] * np.array([16, 14, 8], np.float32) + A.grain(W, H, 202, 3)[..., None]
    rgb += (A.shade(full, 3) * 42)[..., None]
    cx_col = sx1 + 22 + 6
    seams = [(bx0 + 4, sy0 - 34, bx1 - 4, sy0 - 34), (bx0 + 4, sy1 + 34, bx1 - 4, sy1 + 34)]
    if column:
        seams.append((cx_col, by0 + 4, cx_col, by1 - 4))
    seam = A.mask((W, H), lambda d, s: [d.line([(a * s, b * s), (c * s, e * s)], fill=255, width=2 * s) for a, b, c, e in seams])
    rgb -= (seam * 70)[..., None] - (np.roll(seam, 1, 0) * 20)[..., None]

    # ---- wear: grime, paint chipped through to metal, rust patches and runs
    edge = 1 - A.blur(full, 16)
    dirt = np.clip(A.fbm(W, H, 26, 4, 205) * 1.5 + edge * 1.3 + A.blur(seam, 4) * 1.6 - 0.12, 0, 1)
    rgb = rgb * (1 - dirt[..., None] * 0.4) + np.array((92, 74, 50), np.float32) * (dirt[..., None] * 0.4)
    # big worn patches near the edges and seams, a few in the open, ragged at the rim
    chips_n = A.fbm(W, H, 46, 5, 206) * 0.8 + A.fbm(W, H, 9, 3, 214) * 0.35 + edge * 1.2 + A.blur(seam, 5) * 0.8 + A.blur(bez, 10) * 0.2
    chips = np.clip((chips_n - 0.54) * 10, 0, 1)
    metal = np.array((74, 68, 60), np.float32) + A.fbm(W, H, 4, 2, 207)[..., None] * 34
    rustm = np.clip(A.fbm(W, H, 10, 3, 208) * 2.2 + 0.45, 0, 1)[..., None]
    under = metal * (1 - rustm * 0.8) + np.array((132, 64, 28), np.float32) * (rustm * 0.8)
    rgb = rgb * (1 - chips[..., None]) + under * chips[..., None]
    rgb -= (A.blur(chips, 1.3) - chips).clip(0, 1)[..., None] * 70
    # rust bleeding out round the chips, a faint orange halo
    halo = np.clip(A.blur(chips, 5) - chips, 0, 1) * rustm[..., 0]
    rgb = rgb * (1 - halo[..., None] * 0.5) + np.array((150, 84, 40), np.float32) * (halo[..., None] * 0.5)

    # ---- the dark bezel: rubbery charcoal, a raised lip, scuffs
    brgb = np.array((44, 45, 44), np.float32) + A.fbm(W, H, 40, 3, 209)[..., None] * 8 + A.grain(W, H, 210, 3)[..., None]
    brgb += (A.shade(bez, 2.5) * 46)[..., None] - (A.shade(hole, 2, (1, 1)) * 30)[..., None]
    scuff = np.clip((A.fbm(W, H, 6, 3, 211) - 0.62) * 6, 0, 1) * 0.5
    brgb += scuff[..., None] * 40
    rgb = rgb * (1 - bez[..., None]) + brgb * bez[..., None]
    img.alpha_composite(A.to_img(rgb, full * (1 - hole)))

    d = ImageDraw.Draw(img)
    d.rectangle([sx0, sy0, sx1 - 1, sy1 - 1], fill=(12, 16, 16, 255))
    f = A.font(15)
    lv = ["SHADOW", "NOTICED", "TRACKED", "TARGET", "RED ZONE"][lvl]
    for i, (t, c) in enumerate((("NETRUN // NETWORK CORE", (90, 230, 130)), ("> HOME NODE ........ ONLINE", (90, 210, 130)),
                                ("> GATEWAY .......... 5 NODES", (90, 210, 130)), (f"> HEAT {heat} .......... {lv}", (230, 190, 70)))):
        d.text((sx0 + 30, sy0 + 28 + i * 24), t, font=f, fill=c)

    def screw(x, y, n=12):
        A.over(img, A.rivet(n, A.hexc(0x8a8478), int(x * 7 + y)), (x - n / 2 - 2, y - n / 2 - 2))
        r = A.rng(int(x + y * 3)).uniform(0, np.pi)
        dd = ImageDraw.Draw(img)
        for a in (r, r + np.pi / 2):
            dd.line([(x - np.cos(a) * n * 0.32, y - np.sin(a) * n * 0.32), (x + np.cos(a) * n * 0.32, y + np.sin(a) * n * 0.32)], fill=(40, 38, 34, 255), width=2)

    def run(x, y, ln=None):
        r = A.rng(int(x * 5 + y))
        ln = ln or r.uniform(24, 80)
        m = A.mask((W, H), lambda dr, s: dr.line([(x * s, y * s), ((x + r.uniform(-4, 4)) * s, (y + ln) * s)], fill=255, width=int(4 * s)))
        m = A.blur(m, 1.8) * np.clip(1 - (yy - y) / ln, 0, 1)
        img.alpha_composite(A.to_img(np.broadcast_to(np.array((112, 56, 26), np.float32), (H, W, 3)).copy(), m * 0.75))

    for x, y in ((sx0 - 11, sy0 - 11), (sx1 + 11, sy0 - 11), (sx0 - 11, sy1 + 11), (sx1 + 11, sy1 + 11)):
        run(x, y + 4, 40)
        screw(x, y, 13)
    for x, y in ((bx0 + 22, by0 + 22), (bx1 - 22, by0 + 22), (bx0 + 22, by1 - 22), (bx1 - 22, by1 - 22), (bx0 + 18, (sy0 + sy1) / 2)):
        if y < by1 - 40:
            run(x, y + 4)
        screw(x, y, 12)

    # ---- top left: worn maker's name, model line, yellow tape
    lay = A.text_layer((W, H), "NETRUN", A.font(24), (58, 54, 48, 255), (bx0 + 48, by0 + 16))
    A.over(img, A.spray(lay, 212, 0.8), (0, 0))
    d = ImageDraw.Draw(img)
    d.text((bx0 + 50, by0 + 52), "FIELD UNIT  NR-02 // OLD NET CERT.", font=A.font(8), fill=INK, anchor="lm")
    num = A.text_layer((W, H), "02", A.font(38), (190, 96, 50, 255), (bx0 + 270, by0 + 14))
    A.over(img, A.spray(num, 213, 0.78), (0, 0))

    def rubber(x, y, cap, w=34, h=24):
        k = S.Big(w + 4, h + 6)
        k.rect((2, 4, w + 1, h + 4), (34, 32, 30, 255), r=6)
        k.rect((2, 1, w + 1, h + 1), cap + (255,), r=6)
        k.rect((5, 3, w - 2, 8), tuple(min(255, int(v * 1.2)) for v in cap) + (255,), r=4)
        A.over(img, k.done(), (x - w / 2 - 2, y - h / 2 - 1))

    def knob(x, y, r, cap=(36, 34, 32), mark=(220, 214, 196)):
        k = S.Big(2 * r + 6, 2 * r + 8)
        k.ellipse((3, 5, 2 * r + 3, 2 * r + 5), (22, 20, 18, 255))
        k.ellipse((3, 2, 2 * r + 3, 2 * r + 2), cap + (255,))
        for i in range(24):
            a = i * np.pi / 12
            k.line([(r + 3 + np.cos(a) * (r - 2), r + 2 + np.sin(a) * (r - 2)), (r + 3 + np.cos(a) * r, r + 2 + np.sin(a) * r)], (70, 66, 60, 255), 1)
        k.ellipse((r * 0.45 + 3, r * 0.45 + 2, r * 1.55 + 3, r * 1.55 + 2), tuple(int(v * 1.35) for v in cap) + (255,))
        k.line([(r + 3, r + 2), (r + 3 + r * 0.7 * np.cos(-2.2), r + 2 + r * 0.7 * np.sin(-2.2))], mark + (255,), 2)
        A.over(img, k.done(), (x - r - 3, y - r - 2))

    def gauge(x, y, r, v):
        g = S.Big(2 * r + 8, 2 * r + 8)
        c = r + 4
        g.ellipse((0, 0, 2 * r + 7, 2 * r + 7), (40, 38, 34, 255))
        g.ellipse((3, 3, 2 * r + 4, 2 * r + 4), (150, 146, 136, 255))
        g.ellipse((7, 7, 2 * r, 2 * r), (232, 196, 60, 255))
        for i in range(21):
            a = np.radians(150 + i * 12)
            ln = 0.2 if i % 5 == 0 else 0.11
            g.line([(c + np.cos(a) * r * (0.82 - ln), c + np.sin(a) * r * (0.82 - ln)), (c + np.cos(a) * r * 0.82, c + np.sin(a) * r * 0.82)], (40, 34, 20, 255), 2 if i % 5 == 0 else 1)
        a0 = np.radians(150 + 200)
        red = [np.radians(a) for a in np.linspace(345, 390, 10)]
        g.line([(c + np.cos(a) * r * 0.86, c + np.sin(a) * r * 0.86) for a in red], (200, 40, 30, 255), 4)
        g.text((c, c + r * 0.38), "TUNE", A.font, max(8, int(r * 0.2)), (40, 34, 20, 255))
        a = np.radians(150 + 240 * v)
        g.line([(c, c), (c + np.cos(a) * r * 0.76, c + np.sin(a) * r * 0.76)], (30, 28, 26, 255), 2)
        g.ellipse((c - 4, c - 4, c + 4, c + 4), (30, 28, 26, 255))
        A.over(img, g.done(), (x - c, y - c))
        A.glow(img, (x - r * 0.6, y - r * 0.9, x + r * 0.1, y - r * 0.5), (255, 255, 255), 0.12, 6)

    def jewel(x, y, col, on, n=12):
        A.over(img, A.rivet(n + 6, A.hexc(0x5a5650), int(x)), (x - (n + 6) / 2 - 2, y - (n + 6) / 2 - 2))
        if on:
            A.glow(img, (x - 5, y - 5, x + 5, y + 5), col, 0.7, 6)
        A.over(img, A.rivet(n, np.array(col if on else tuple(int(v * 0.3) for v in col), np.float32), int(x) + 1), (x - n / 2 - 2, y - n / 2 - 2))

    def round_btn(x, y, r, col):
        k = S.Big(2 * r + 8, 2 * r + 10)
        k.ellipse((0, 2, 2 * r + 7, 2 * r + 9), (50, 48, 44, 255))
        k.ellipse((4, 6, 2 * r + 4, 2 * r + 6), (20, 18, 16, 255))
        k.ellipse((4, 3, 2 * r + 4, 2 * r + 3), col + (255,))
        k.ellipse((r * 0.6 + 4, r * 0.35 + 3, r * 1.3 + 4, r * 0.8 + 3), tuple(min(255, int(v * 1.25)) for v in col) + (255,))
        A.over(img, k.done(), (x - r - 4, y - r - 4))

    cy, ly = by0 + 40, by0 + 70
    keys = (("CLOSE", RED), ("MENU", (126, 126, 120)), ("ACHIEV", (126, 126, 120)), ("NET LOCK", ORANGE))
    kx = bx0 + 440
    for i, (nm, cap) in enumerate(keys):
        x = kx + i * 52
        rubber(x, cy, cap)
        ImageDraw.Draw(img).text((x, ly), nm, font=A.font(8), fill=INK, anchor="mm")
    if not column:
        gauge(kx + 4 * 52 + 28, cy - 3, 26, 0.62)
    # RF on a small green-grey LCD in a dark surround
    ox = (sx1 - 240) if column else (bx1 - 310)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([ox - 6, cy - 22, ox + 100, cy + 20], 5, fill=(40, 40, 38, 255))
    d.rectangle([ox, cy - 16, ox + 94, cy + 14], fill=(150, 158, 112, 255))
    d.text((ox + 4, cy - 14), "RF", font=A.font(9), fill=(40, 46, 30, 255), anchor="lt")
    for i in range(7):
        h = 4 + i * 3
        on = i < 5 or (lvl >= 3 and i == 6)
        d.rectangle([ox + 26 + i * 9, cy + 10 - h, ox + 31 + i * 9, cy + 10], fill=(40, 46, 30, 255) if on else (136, 144, 102, 255))
    d.text((ox + 47, ly), "LINK", font=A.font(8), fill=INK, anchor="mm")
    lx = ox + 136
    for i, (nm, col, on) in enumerate((("ALARM", (230, 50, 36), lvl >= 3), ("NET", (90, 210, 90), True), ("SICK", (240, 150, 40), True), ("POD", (90, 160, 230), False))):
        x = lx + i * 34
        if column or x < bx1 - 40:
            jewel(x, cy, col, on)
            ImageDraw.Draw(img).text((x, ly), nm, font=A.font(8), fill=INK, anchor="mm")

    # ---- the instrument column (A) or the thin right rail (B)
    if column:
        mx = (cx_col + bx1) / 2
        # two knurled knobs with scale marks
        for j, (nm, x) in enumerate((("VOL", mx - 34), ("SQL", mx + 34))):
            y = sy0 + 30
            for i in range(9):
                a = np.radians(135 + i * 33.75)
                ImageDraw.Draw(img).line([(x + np.cos(a) * 24, y + np.sin(a) * 24), (x + np.cos(a) * 28, y + np.sin(a) * 28)], fill=INK, width=1)
            knob(x, y, 19)
            ImageDraw.Draw(img).text((x, y + 36), nm, font=A.font(8), fill=INK, anchor="mm")
        gauge(mx, sy0 + 150, 56, 0.62)
        # a plate with a toggle and a small jewel
        d = ImageDraw.Draw(img)
        py = sy0 + 240
        d.rounded_rectangle([mx - 58, py, mx + 58, py + 64], 6, fill=(60, 58, 54, 255))
        d.rounded_rectangle([mx - 54, py + 4, mx + 54, py + 60], 4, fill=(84, 82, 76, 255))
        A.over(img, A.rivet(20, A.hexc(0x9a968c), 7), (mx - 34, py + 20))
        d = ImageDraw.Draw(img)
        d.line([(mx - 22, py + 32), (mx - 14, py + 12)], fill=(200, 196, 186, 255), width=5)
        d.ellipse([mx - 18, py + 6, mx - 8, py + 16], fill=(214, 210, 200, 255))
        jewel(mx + 26, py + 32, (230, 60, 40), lvl >= 3, 14)
        d = ImageDraw.Draw(img)
        d.text((mx - 22, py + 54), "SCAN", font=A.font(7), fill=(220, 214, 196, 255), anchor="mm")
        d.text((mx + 26, py + 54), "ICE", font=A.font(7), fill=(220, 214, 196, 255), anchor="mm")
        # a big black knob in a cream collar, then orange and red buttons
        ky = sy0 + 400
        A.over(img, A.rivet(70, A.hexc(0xb8ae96), 9), (mx - 37, ky - 37))
        knob(mx, ky, 26, (30, 28, 26))
        ImageDraw.Draw(img).text((mx, ky + 44), "TRACE", font=A.font(8), fill=INK, anchor="mm")
        by = sy0 + 520
        round_btn(mx - 30, by, 20, ORANGE)
        round_btn(mx + 30, by, 20, RED)
        d = ImageDraw.Draw(img)
        d.text((mx - 30, by + 34), "JACK", font=A.font(8), fill=INK, anchor="mm")
        d.text((mx + 30, by + 34), "PURGE", font=A.font(8), fill=INK, anchor="mm")
        bc = S.barcode(70, 22, "NR-02", 221)
        A.over(img, bc, (mx - 36, sy1 - 24))
    else:
        ks = S.kanji_strip("旧網機", 26, 74, paper=(226, 220, 204), ink=RED, seed=222)
        A.over(img, A.spray(ks, 222, 0.9), (bx1 - 45, sy0 + 150))
        knob(bx1 - 32, sy0 + 330, 17)
        bc = S.barcode(36, 18, "NR-02", 221)
        A.over(img, bc, (bx1 - 50, sy1 - 150))

    # ---- bottom: the orange LCD, a yellow inventory sticker, a paper label with kanji, round keys, Heat lamps, NET LINK
    cb, lb = sy1 + 58, sy1 + 86
    d = ImageDraw.Draw(img)
    vx = bx0 + 54
    d.rounded_rectangle([vx - 7, cb - 27, vx + 247, cb + 27], 6, fill=(40, 40, 38, 255))
    d.rectangle([vx, cb - 20, vx + 240, cb + 20], fill=(238, 124, 40, 255))
    A.glow(img, (vx, cb - 20, vx + 240, cb + 20), (255, 140, 40), 0.15, 6)
    for i, t in enumerate(("DATA 07   HEAT %d" % heat, "D2 14:20  NET ON")):
        ImageDraw.Draw(img).text((vx + 10, cb - 9 + i * 17), t, font=A.font(13, False), fill=(60, 24, 6, 255), anchor="lm")
    st = S.Big(92, 46)
    st.rect((0, 0, 91, 45), YELLOW + (255,), r=2)
    st.text((46, 15), "V-NR 02", A.font, 15, (30, 28, 24, 255))
    st.text((46, 33), "5X65  OLD", A.font, 11, (30, 28, 24, 255))
    A.over(img, A.spray(st.done(), 223, 0.82).rotate(2, expand=True), (vx + 262, cb - 26))
    pp = S.Big(96, 50)
    pp.rect((0, 0, 95, 49), (226, 220, 204, 255), r=2)
    pp.text((32, 25), "遺物", S.jfont, 22, (178, 50, 40, 255))
    for i, t in enumerate(("S/N 4471", "TYPE II", "EXPORT")):
        pp.text((76, 12 + i * 12), t, A.font, 7, (60, 56, 50, 255))
    A.over(img, A.spray(pp.done(), 224, 0.86).rotate(-1.5, expand=True), (vx + 360, cb - 25))
    kx = vx + 500
    for i, nm in enumerate(("BACK", "CORE", "HELP")):
        x = kx + i * 50
        A.over(img, A.rivet(34, A.hexc(0xb8b09a), x), (x - 19, cb - 19))
        A.over(img, A.rivet(26, A.hexc(0x2e2c2a), x + 1), (x - 15, cb - 15))
        ImageDraw.Draw(img).text((x, lb), nm, font=A.font(8), fill=INK, anchor="mm")
    hx = kx + 170
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([hx - 12, cb - 18, hx + 5 * 30 + 2, cb + 18], 4, fill=(40, 40, 38, 255))
    for i in range(5):
        x = hx + 8 + i * 30
        cc = HEAT_SEG[i]
        on = i <= lvl
        if on:
            A.glow(img, (x - 9, cb - 8, x + 9, cb + 8), cc, 0.5, 4)
        ImageDraw.Draw(img).rectangle([x - 10, cb - 9, x + 10, cb + 9], fill=cc + (255,) if on else dim(cc))
    ImageDraw.Draw(img).text((hx + 70, lb), "HEAT", font=A.font(8), fill=INK, anchor="mm")
    sx = bx1 - (150 if not column else 140)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([sx, cb - 10, sx + 80, cb + 10], 5, fill=(40, 38, 34, 255))
    d.rounded_rectangle([sx + 46, cb - 8, sx + 76, cb + 8], 3, fill=ORANGE + (255,))
    d.text((sx - 4, cb), "OFF", font=A.font(8), fill=INK, anchor="rm")
    d.text((sx + 84, cb), "ON", font=A.font(8), fill=INK, anchor="lm")
    d.text((sx + 40, lb), "NET LINK", font=A.font(8), fill=INK, anchor="mm")
    return img


if __name__ == "__main__":
    heat = int(sys.argv[sys.argv.index("--heat") + 1]) if "--heat" in sys.argv else 58
    out = os.path.join(os.path.dirname(__file__), "..", "docs", "mockups")
    relic(heat, True).save(os.path.join(out, "48a-relic-column.png"))
    relic(heat, False).save(os.path.join(out, "48b-relic-classic.png"))
    print("written")
