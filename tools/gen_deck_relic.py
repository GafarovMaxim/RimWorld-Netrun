#!/usr/bin/env python3
"""
Textures of the "Relic" deck (the old deck, docs/mockups/47b-old-orange.png, Maxim's pick 2026-10-15): worn cream
plastic over metal with chipped paint and rust runs, an orange side panel, a rubber gasket round the screen, rubber
keys on a dark plate, a dial gauge for TUNE, jewel lamps, a small LCD for RF, an orange LCD, round keys, Heat lamps,
a slide switch, a speaker grille, a jack strip on the left rail and stickers. DeckRelic.cs lays them out.

    python tools/gen_deck_relic.py
"""
import numpy as np
from PIL import Image, ImageDraw

import deck_art as A
import deck_stickers as S

T = "Relic"
W, H = 1400, 900
IN_L, IN_T, IN_R, IN_B = 60, 96, 64, 100
CREAM, ORANGE, INK = (210, 202, 182), (204, 104, 52), (52, 48, 42)


def make_body():
    full = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 34 * s, fill=255))
    hole = A.mask((W, H), lambda d, s: d.rounded_rectangle([IN_L * s, IN_T * s, (W - IN_R) * s - 1, (H - IN_B) * s - 1], 10 * s, fill=255))
    rim = A.mask((W, H), lambda d, s: d.rounded_rectangle([(IN_L - 14) * s, (IN_T - 14) * s, (W - IN_R + 14) * s - 1, (H - IN_B + 14) * s - 1], 18 * s, fill=255))
    inner = A.mask((W, H), lambda d, s: d.rounded_rectangle([(IN_L - 8) * s, (IN_T - 8) * s, (W - IN_R + 8) * s - 1, (H - IN_B + 8) * s - 1], 14 * s, fill=255))
    side = A.mask((W, H), lambda d, s: d.rounded_rectangle([(W - 46) * s, (IN_T - 36) * s, (W - 6) * s, (H - IN_B + 36) * s], 10 * s, fill=255))
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    rgb = np.array(CREAM, np.float32) + A.fbm(W, H, 120, 4, 101)[..., None] * np.array([14, 13, 6], np.float32) + A.grain(W, H, 102, 3)[..., None]
    acc = np.array(ORANGE, np.float32) + A.fbm(W, H, 60, 3, 103)[..., None] * 10
    rgb = rgb * (1 - side[..., None]) + acc * side[..., None]
    rgb += (A.shade(full, 3) * 40)[..., None] + (A.shade(side, 1.5) * 40)[..., None]
    seam = A.mask((W, H), lambda d, s: [d.line([(x0 * s, y0 * s), (x1 * s, y1 * s)], fill=255, width=2 * s) for x0, y0, x1, y1 in
                                        ((4, IN_T - 30, W - 50, IN_T - 30), (4, H - IN_B + 30, W - 50, H - IN_B + 30))])
    rgb -= (seam * 70)[..., None] - (np.roll(seam, 1, 0) * 18)[..., None]
    rgb += (A.shade(rim, 2.5) * 50)[..., None]
    gasket = np.array((30, 30, 30), np.float32) + A.grain(W, H, 104, 3)[..., None] - (A.shade(inner, 2) * 30)[..., None]
    rgb = rgb * (1 - inner[..., None]) + gasket * inner[..., None]
    edge = 1 - A.blur(full, 18)
    dirt = np.clip(A.fbm(W, H, 30, 4, 105) * 1.4 + edge * 1.2 + A.blur(seam, 4) * 1.5 - 0.15, 0, 1)
    rgb = rgb * (1 - dirt[..., None] * 0.35) + np.array((96, 78, 52), np.float32) * (dirt[..., None] * 0.35)
    chips = np.clip((A.fbm(W, H, 14, 4, 106) + edge * 0.7 + A.blur(seam, 3) * 0.6 - 0.41) * 9, 0, 1) * (1 - inner)
    metal = np.array((70, 66, 60), np.float32) + A.fbm(W, H, 4, 2, 107)[..., None] * 30
    rust = np.clip(A.fbm(W, H, 8, 3, 108) * 2 + 0.3, 0, 1)[..., None]
    under = metal * (1 - rust * 0.7) + np.array((118, 62, 30), np.float32) * (rust * 0.7)
    rgb = rgb * (1 - chips[..., None]) + under * chips[..., None]
    rgb -= (A.blur(chips, 1.2) - chips).clip(0, 1)[..., None] * 60
    img = A.to_img(rgb, full * (1 - hole))
    for x, y in ((26, 26), (W - 26, 26), (26, H - 26), (W - 26, H - 26)):
        A.over(img, rivet_run(x, y, y < H - 60), (x - 9, y - 9))
    return img


def rivet_run(x, y, run=True, n=10):
    """A rivet with a rust run under it, as a sprite (18 x 90): the rivet sits at (9, 9)."""
    out = Image.new("RGBA", (18, 90), (0, 0, 0, 0))
    if run:
        r = A.rng(int(x * 3 + y))
        ln = r.uniform(24, 70)
        m = A.mask((18, 90), lambda d, s: d.line([(9 * s, 9 * s), ((9 + r.uniform(-2, 2)) * s, (9 + ln) * s)], fill=255, width=int(3 * s)))
        yy = np.mgrid[0:90, 0:18][0].astype(np.float32)
        m = A.blur(m, 1.3) * np.clip(1 - (yy - 9) / ln, 0, 1)
        out.alpha_composite(A.to_img(np.broadcast_to(np.array((104, 58, 30), np.float32), (90, 18, 3)).copy(), m * 0.7))
    out.alpha_composite(A.rivet(n, A.hexc(0x6a645a), int(x + y)), dest=(2, 2))
    return out


def make_badge():
    """The top-left corner: sprayed NETRUN, the serial, a big worn 02, a hazard triangle, yellow tape, a CAUTION label."""
    w, h = 340, 72
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    A.over(img, A.spray(A.text_layer((w, h), "NETRUN", A.font(22), INK + (255,), (0, 4)), 110, 0.9), (0, 0))
    ImageDraw.Draw(img).text((2, 36), "FIELD DECK  MOD. 2 // SER 4471", font=A.font(8), fill=INK + (255,), anchor="lm")
    A.over(img, A.spray(A.text_layer((w, h), "02", A.font(40), (196, 104, 58, 255), (206, 0)), 111, 0.88), (0, 0))
    tri = S.Big(30, 27)
    tri.poly([(15, 1), (29, 26), (1, 26)], (236, 196, 52, 255), INK + (255,), 1.5)
    tri.text((15, 18), "!", A.font, 14, INK + (255,))
    A.over(img, tri.done(), (294, 10))
    t = S.tape(70, 8, color=(222, 186, 60), alpha=0.85, seed=60)
    A.over(img, A.spray(t, 60, 0.85).rotate(-1, expand=True), (8, 46))
    lab = S.Big(110, 26)
    lab.rect((0, 0, 109, 25), (236, 232, 222, 255), r=2)
    lab.text((55, 9), "CAUTION: DO NOT", A.font, 8, INK + (255,))
    lab.text((55, 18), "OPEN WHILE LIVE", A.font, 8, INK + (255,))
    A.over(img, A.spray(lab.done(), 122, 0.9).rotate(-1.5, expand=True, resample=Image.BICUBIC), (100, 42))
    return img


def make_plate(w, h, r=6):
    """A dark recessed metal plate, 9-sliced (borders 10), with screws in its corners drawn by the C#."""
    m = A.mask((w, h), lambda d, s: d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], r * s, fill=255))
    rgb = A.hexc(0x34322e) + A.streaks(w, h, "x", 6, w)[..., None] + A.grain(w, h, w + 1, 3)[..., None] - (A.shade(m, 1.5) * 40)[..., None]
    return A.to_img(rgb, m)


def make_key(cap):
    """A rubber key, 40x34: a dark skirt and a domed cap, lit from the top."""
    w, h = 40, 34
    m = A.mask((w, h), lambda d, s: d.rounded_rectangle([4 * s, 3 * s, (w - 4) * s, (h - 7) * s], 6 * s, fill=255))
    skirt = A.mask((w, h), lambda d, s: d.rounded_rectangle([1 * s, 2 * s, (w - 1) * s, (h - 2) * s], 8 * s, fill=255))
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32)
    rgb = np.array(cap, np.float32) + (A.shade(m, 3) * 70)[..., None] + (np.clip(1 - (yy - 4) / 12, 0, 1) * 26)[..., None] + A.grain(w, h, sum(cap), 3)[..., None]
    img = A.to_img(np.zeros((h, w, 3), np.float32), np.roll(A.blur(skirt, 1.5), 2, 0) * 0.7)
    img.alpha_composite(A.to_img(A.hexc(0x24221f) + (A.shade(skirt, 1) * 20)[..., None], skirt))
    img.alpha_composite(A.to_img(rgb, m))
    return img


def make_gauge():
    """The TUNE dial, 60x60: a chrome bezel, a cream face with ticks and a red end arc, a glass glint (the C# adds the needle)."""
    n = 60
    b = S.Big(n, n)
    b.ellipse((0, 0, n - 1, n - 1), (60, 58, 54, 255))
    b.ellipse((2, 2, n - 3, n - 3), (176, 174, 166, 255))
    b.ellipse((5, 5, n - 6, n - 6), (40, 38, 34, 255))
    b.ellipse((7, 7, n - 8, n - 8), (228, 222, 202, 255))
    c = n / 2
    b.d.arc([(c - 19) * b.s, (c - 19) * b.s, (c + 19) * b.s, (c + 19) * b.s], 330, 390, fill=(200, 50, 36), width=int(3 * b.s))
    for k in range(13):
        a = np.radians(150 + k * 20)
        r0 = 15 if k % 3 else 13
        b.line([(c + np.cos(a) * r0, c + np.sin(a) * r0), (c + np.cos(a) * 20, c + np.sin(a) * 20)], (40, 38, 34, 255), 1.2 if k % 3 else 1.8)
    b.text((c, c + 10), "MHz", A.font, 6, (80, 76, 68, 255))
    img = b.done()
    gl = A.mask((n, n), lambda d, s: d.ellipse([12 * s, 9 * s, 34 * s, 22 * s], fill=255), blur=2)
    img.alpha_composite(A.to_img(np.full((n, n, 3), 255, np.float32), gl * 0.35))
    return img


def make_disc(n, col, ring=None):
    b = S.Big(n, n)
    if ring:
        b.ellipse((0, 0, n - 1, n - 1), ring + (255,))
    k = 2 if ring else 0
    b.ellipse((k, k, n - 1 - k, n - 1 - k), col + (255,))
    b.ellipse((n * 0.28, n * 0.2, n * 0.5, n * 0.4), (255, 255, 255, 120))
    return b.done()


def make_lcd(col, frame=(46, 44, 40)):
    """An LCD window, 9-sliced (borders 8): a dark frame round the glass."""
    n = 32
    b = S.Big(n, n)
    b.rect((0, 0, n - 1, n - 1), frame + (255,), r=5)
    b.rect((5, 5, n - 6, n - 6), col + (255,), r=1)
    img = b.done()
    g = A.mask((n, n), lambda d, s: d.rectangle([5 * s, 5 * s, (n - 5) * s, 9 * s], fill=255), blur=1.5)
    img.alpha_composite(A.to_img(np.zeros((n, n, 3), np.float32), g * 0.25))
    return img


def make_round_key():
    n = 38
    img = A.rivet(34, A.hexc(0xb8b09a), 5)
    img.alpha_composite(A.rivet(26, A.hexc(0x2e2c2a), 6), dest=(4, 4))
    return img


def make_slide(on):
    w, h = 84, 22
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (40, 38, 34, 255), r=6)
    b.rect((3, 9, w - 4, 12), (20, 18, 16, 255), r=1)
    x0 = 46 if on else 4
    b.rect((x0, 2, x0 + 33, h - 3), ORANGE + (255,), r=4)
    for k in range(4):
        b.line([(x0 + 10 + k * 4, 6), (x0 + 10 + k * 4, h - 7)], (150, 70, 34, 255), 1)
    return b.done()


def make_grille():
    """A round speaker grille, 46x46, punched holes in the cream plastic."""
    n = 46
    b = S.Big(n, n)
    b.ellipse((0, 0, n - 1, n - 1), (170, 162, 142, 255))
    b.ellipse((2, 2, n - 3, n - 3), (196, 188, 168, 255))
    for y in range(7, n - 5, 5):
        for x in range(7, n - 5, 5):
            if (x - n / 2) ** 2 + (y - n / 2) ** 2 < (n / 2 - 7) ** 2:
                b.ellipse((x - 1.4, y - 1.4, x + 1.4, y + 1.4), (30, 28, 26, 255))
    return b.done()


def make_jacks():
    """The left rail strip: two jack ports, a vent and a printed label, 34x230."""
    w, h = 34, 230
    img = make_plate(w, h, 6)
    d = ImageDraw.Draw(img)
    for i, lab in enumerate(("IN", "OUT")):
        y = 26 + i * 52
        img.alpha_composite(A.rivet(18, A.hexc(0xa8a49a), y), dest=(6, y - 11))
        d = ImageDraw.Draw(img)
        d.ellipse([12, y - 5, 22, y + 5], fill=(10, 10, 10, 255))
        d.text((17, y + 18), lab, font=A.font(7), fill=(200, 194, 176, 255), anchor="mm")
    for k in range(9):
        d.rounded_rectangle([8, 124 + k * 9, w - 8, 128 + k * 9], 2, fill=(14, 13, 12, 255))
    return img


def make_danger():
    dg = S.Big(64, 36)
    dg.rect((0, 0, 63, 35), (236, 232, 222, 255), r=2)
    dg.rect((2, 2, 61, 33), (196, 40, 36, 255), r=1)
    dg.rect((5, 5, 58, 30), (236, 232, 222, 255), r=1)
    img = dg.done()
    ImageDraw.Draw(img).text((32, 18), "DANGER", font=A.font(11), fill=(196, 40, 36, 255), anchor="mm")
    return A.spray(img.rotate(-90, expand=True), 120, 0.95)


def build():
    A.save(make_body(), T, "Body")
    A.save(make_badge(), T, "Badge")
    A.save(make_plate(40, 40), T, "Plate")
    for name, cap in (("Red", (176, 60, 48)), ("Grey", (124, 124, 118)), ("Orange", (214, 110, 50))):
        A.save(make_key(cap), T, "Key_" + name)
    A.save(make_gauge(), T, "Gauge")
    A.save(make_disc(20, (90, 88, 82), (40, 38, 34)), T, "LampBezel")
    A.save(make_disc(12, (255, 255, 255)), T, "Glass")
    A.save(make_lcd((150, 156, 112)), T, "Lcd")
    A.save(make_lcd((236, 120, 40)), T, "LcdOrange")
    A.save(make_round_key(), T, "Round")
    A.save(make_slide(False), T, "Slide_0")
    A.save(make_slide(True), T, "Slide_1")
    A.save(make_grille(), T, "Grille")
    A.save(make_jacks(), T, "Jacks")
    A.save(make_danger(), T, "Danger")
    A.save(rivet_run(0, 0), T, "Rivet")
    A.save(S.barcode(34, 18, "NR-02", 121), T, "Barcode")
    import gen_deck_classic as C
    A.save(C.old(30, ORANGE).resize((300, 240), Image.LANCZOS), T, "Preview")


if __name__ == "__main__":
    build()
    print("Relic textures written")
