#!/usr/bin/env python3
"""
Textures of the "Relic" deck, v2 (docs/mockups/48c-relic-column-v2.png, Maxim's pick 2026-10-15, after his references):
a battered field unit in cream plastic worn through to rusty metal, the screen in a thick dark bezel with screws, and an
instrument column on the right as an inset module: VOL/SQL knobs, a faded mustard TUNE gauge with a cracked glass,
a SCAN toggle with the ICE lamp, a TRACE pointer knob that follows the Heat level, JACK and PURGE buttons, a vent.
Rubber keys and jewel lamps on a dark top plate; at the bottom an orange LCD, stickers, grey square keys in the style
of the reference handheld, Heat lamps and a slide switch. DeckRelic.cs lays them out.

    python tools/gen_deck_relic.py
"""
import numpy as np
from PIL import Image, ImageDraw

import deck_art as A
import deck_stickers as S

T = "Relic"
W, H = 1400, 900
IN_L, IN_T, IN_R, IN_B = 60, 96, 168, 100
SX0, SY0, SX1, SY1 = IN_L, IN_T, W - IN_R, H - IN_B
CX0, CX1 = W - 134, W - 12                     # the instrument column (DeckRelic.cs: box.xMax - 134 .. - 12)
CY0, CY1 = IN_T - 30, H - IN_B + 30
CREAM, ORANGE, RED, YELLOW = (214, 206, 186), (226, 112, 44), (196, 54, 40), (236, 190, 48)
INK = (50, 46, 40)
FACE = (204, 182, 118)                          # the gauge face: an old, faded mustard


def rrect(size, box, r):
    x0, y0, x1, y1 = box
    return A.mask(size, lambda d, s: d.rounded_rectangle([x0 * s, y0 * s, x1 * s - 1, y1 * s - 1], r * s, fill=255))


def lines_mask(size, segs, width=2):
    return A.mask(size, lambda d, s: [d.line([(a * s, b * s), (c * s, e * s)], fill=255, width=int(width * s)) for a, b, c, e in segs])


def screw_on(img, x, y, n=12, base=0x8a8478):
    A.over(img, A.rivet(n, A.hexc(base), int(x * 7 + y)), (x - n / 2 - 2, y - n / 2 - 2))
    a = A.rng(int(x + y * 3)).uniform(0, np.pi)
    d = ImageDraw.Draw(img)
    for b in (a, a + np.pi / 2):
        d.line([(x - np.cos(b) * n * 0.32, y - np.sin(b) * n * 0.32), (x + np.cos(b) * n * 0.32, y + np.sin(b) * n * 0.32)], fill=(40, 38, 34, 255), width=2)


def rust_run(img, x, y, ln, seed):
    w, h = img.size
    r = A.rng(seed)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32)
    m = A.mask((w, h), lambda d, s: d.line([(x * s, y * s), ((x + r.uniform(-4, 4)) * s, (y + ln) * s)], fill=255, width=int(4 * s)))
    m = A.blur(m, 1.8) * np.clip(1 - (yy - y) / ln, 0, 1)
    img.alpha_composite(A.to_img(np.broadcast_to(np.array((112, 56, 26), np.float32), (h, w, 3)).copy(), m * 0.75))


def make_body():
    size = (W, H)
    full = rrect(size, (0, 0, W, H), 30)
    bez = rrect(size, (SX0 - 22, SY0 - 22, SX1 + 22, SY1 + 22), 16)
    hole = rrect(size, (SX0, SY0, SX1, SY1), 8)
    col = rrect(size, (CX0, CY0, CX1, CY1), 10)
    ring = np.clip(rrect(size, (CX0 - 3, CY0 - 3, CX1 + 3, CY1 + 3), 12) - col, 0, 1)
    rgb = np.array(CREAM, np.float32) + A.fbm(W, H, 120, 4, 301)[..., None] * np.array([16, 14, 8], np.float32) + A.grain(W, H, 302, 3)[..., None]
    rgb += (A.shade(full, 3) * 42)[..., None]
    rgb -= ring[..., None] * 110
    rgb += (A.shade(col, 2, (1, 1)) * 34)[..., None] - col[..., None] * 8
    seam = lines_mask(size, [(4, SY0 - 34, CX0 - 6, SY0 - 34), (4, SY1 + 34, CX0 - 6, SY1 + 34)])
    rgb -= (seam * 70)[..., None] - (np.roll(seam, 1, 0) * 22)[..., None]
    edge = 1 - A.blur(full, 16)
    near = A.blur(seam, 5) + A.blur(ring, 5) * 1.2
    dirt = np.clip(A.fbm(W, H, 26, 4, 305) * 1.5 + edge * 1.3 + near * 1.4 - 0.1, 0, 1)
    rgb = rgb * (1 - dirt[..., None] * 0.4) + np.array((92, 74, 50), np.float32) * (dirt[..., None] * 0.4)
    chips_n = A.fbm(W, H, 46, 5, 306) * 0.8 + A.fbm(W, H, 9, 3, 314) * 0.35 + edge * 1.2 + near * 0.7 + A.blur(bez, 10) * 0.2
    chips = np.clip((chips_n - 0.55) * 10, 0, 1)
    metal = np.array((74, 68, 60), np.float32) + A.fbm(W, H, 4, 2, 307)[..., None] * 34
    rustm = np.clip(A.fbm(W, H, 10, 3, 308) * 2.2 + 0.45, 0, 1)[..., None]
    under = metal * (1 - rustm * 0.8) + np.array((132, 64, 28), np.float32) * (rustm * 0.8)
    rgb = rgb * (1 - chips[..., None]) + under * chips[..., None]
    rgb -= (A.blur(chips, 1.3) - chips).clip(0, 1)[..., None] * 70
    halo = np.clip(A.blur(chips, 5) - chips, 0, 1) * rustm[..., 0]
    rgb = rgb * (1 - halo[..., None] * 0.5) + np.array((150, 84, 40), np.float32) * (halo[..., None] * 0.5)
    r = A.rng(315)
    scr = []
    for _ in range(110):
        x, y = r.uniform(10, W - 10), r.uniform(10, H - 10)
        a, ln = r.uniform(-0.5, 0.5) + (np.pi / 2 if r.random() < 0.3 else 0), r.uniform(8, 60)
        scr.append((x, y, x + np.cos(a) * ln, y + np.sin(a) * ln))
    sc = A.blur(lines_mask(size, scr, 1), 0.4) * (1 - hole)
    rgb += sc[..., None] * np.array([22, 20, 16], np.float32) - (np.roll(sc, 1, 0) * 18)[..., None]
    stain = np.clip((A.fbm(W, H, 60, 3, 316) - 0.6) * 4, 0, 1) * 0.3
    rgb = rgb * (1 - stain[..., None]) + np.array((150, 120, 70), np.float32) * stain[..., None]
    brgb = np.array((44, 45, 44), np.float32) + A.fbm(W, H, 40, 3, 309)[..., None] * 8 + A.grain(W, H, 310, 3)[..., None]
    brgb += (A.shade(bez, 2.5) * 46)[..., None] - (A.shade(hole, 2, (1, 1)) * 30)[..., None]
    brgb += (np.clip((A.fbm(W, H, 6, 3, 311) - 0.62) * 6, 0, 1) * 20)[..., None] + sc[..., None] * 30
    rgb = rgb * (1 - bez[..., None]) + brgb * bez[..., None]
    img = A.to_img(rgb, full * (1 - hole))
    # screws (all inside the 170 px corner slices, so the 9-slice never stretches them) with rust runs
    pts = [(SX0 - 11, SY0 - 11, 13, 40), (SX1 + 11, SY0 - 11, 13, 40), (SX0 - 11, SY1 + 11, 13, 40), (SX1 + 11, SY1 + 11, 13, 40),
           (22, 22, 12, 70), (W - 22, 22, 12, 70), (22, H - 22, 12, 0), (W - 22, H - 22, 12, 0),
           (CX0 + 12, CY0 + 12, 10, 30), (CX1 - 12, CY0 + 12, 10, 30), (CX0 + 12, CY1 - 12, 10, 0), (CX1 - 12, CY1 - 12, 10, 0)]
    for x, y, n, ln in pts:
        if ln:
            rust_run(img, x, y + 4, ln, int(x * 5 + y))
    for x, y, n, ln in pts:
        screw_on(img, x, y, n)
    # the barcode under the column, bottom right
    A.over(img, A.spray(S.barcode(70, 22, "NR-02", 321), 321, 0.93).rotate(7, expand=True, resample=Image.BICUBIC), (CX0 + 22, CY1 + 4))
    return img


def make_badge():
    w, h = 300, 60
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    A.over(img, A.spray(A.text_layer((w, h), "NETRUN", A.font(24), (58, 54, 48, 255), (0, 0)), 312, 0.8), (0, 0))
    ImageDraw.Draw(img).text((2, 36), "FIELD UNIT  NR-02 // OLD NET CERT.", font=A.font(8), fill=INK + (255,), anchor="lm")
    A.over(img, A.spray(A.text_layer((w, h), "02", A.font(38), (190, 96, 50, 255), (222, 0)), 313, 0.78), (0, 0))
    return img


def make_rail_text():
    """Worn engraved text up the left rail, 16 x 700 (the C# crops it to the screen height)."""
    lay = Image.new("RGBA", (700, 16), (0, 0, 0, 0))
    ImageDraw.Draw(lay).text((0, 8), "NR-02 FIELD UNIT  ·  OLD NET CERT. 7741  ·  KEEP DRY  ·  DO NOT OPEN WHILE LIVE  ·  MADE IN NIGHT CITY",
                             font=A.font(8), fill=(70, 64, 54, 200), anchor="lm")
    return A.spray(lay, 330, 0.7).rotate(90, expand=True)


def make_rail_parts():
    """Parts for the left rail, 30 x 300: PHONES and DATA jacks in a dark strip, a battery hatch latch, a strap lug,
    a dymo label and a rusty serial plate; DeckRelic.cs puts them at the top of the rail."""
    w, h = 30, 300
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    st = S.Big(w, 110)
    st.rect((3, 3, w - 2, 108), (14, 12, 10, 140), r=6)
    st.rect((2, 0, w - 4, 104), (40, 40, 38, 255), r=6)
    st.rect((4, 2, w - 6, 102), (52, 52, 50, 255), r=5)
    for y, col in ((20, (196, 194, 188)), (56, (226, 112, 44))):
        st.ellipse((w / 2 - 9, y - 9, w / 2 + 7, y + 7), (130, 128, 122, 255))
        st.ellipse((w / 2 - 7, y - 7, w / 2 + 5, y + 5), col + (255,))
        st.ellipse((w / 2 - 4, y - 4, w / 2 + 2, y + 2), (10, 10, 10, 255))
    st.rect((w / 2 - 7, 80, w / 2 + 5, 94), (12, 12, 12, 255), r=1)
    st.rect((w / 2 - 5, 82, w / 2 + 3, 86), (150, 140, 90, 255))
    A.over(img, st.done(), (0, 0))
    d = ImageDraw.Draw(img)
    for y, t in ((35, "PH"), (71, "IN"), (99, "DATA")):
        d.text((w / 2 - 1, y), t, font=A.font(6), fill=(214, 208, 192, 255), anchor="mm")
    for y in (7, 106):
        screw_on(img, w / 2 - 1, y if y < 50 else 104, 5, 0x7a766e)
    # the battery hatch latch: a slot with a ridged slider
    lt = S.Big(w, 60)
    lt.rect((5, 2, w - 7, 56), (60, 54, 46, 255), r=4)
    lt.rect((7, 4, w - 9, 54), (172, 164, 146, 255), r=3)
    lt.rect((9, 14, w - 11, 34), (110, 104, 92, 255), r=2)
    for k in range(5):
        lt.line([(10, 16 + k * 4), (w - 12, 16 + k * 4)], (70, 64, 56, 255), 1)
    lt.text((w / 2 - 1, 45), "BAT", A.font, 6, INK + (255,))
    lt.line([(w / 2 - 5, 8), (w / 2 - 1, 5), (w / 2 + 3, 8)], INK + (255,), 1)
    A.over(img, A.spray(lt.done(), 341, 0.9), (0, 126))
    # a strap lug with a worn ring
    lg = S.Big(w, 40)
    lg.rect((6, 4, w - 8, 36), (16, 14, 12, 130), r=4)
    lg.rect((5, 2, w - 9, 33), (136, 132, 124, 255), r=4)
    lg.rect((10, 8, w - 14, 27), (36, 34, 32, 255), r=3)
    lg.ellipse((9, 12, w - 13, 24), (180, 176, 168, 255))
    lg.ellipse((12, 15, w - 16, 21), (36, 34, 32, 255))
    A.over(img, lg.done(), (0, 196))
    # a rusty serial plate
    sp = S.Big(w, 56)
    sp.rect((4, 0, w - 6, 54), (126, 116, 96, 255), r=2)
    sp.rect((6, 2, w - 8, 52), (164, 152, 126, 255), r=1)
    for k, t in enumerate(("S/N", "7741", "C-02")):
        sp.text((w / 2 - 1, 12 + k * 14), t, A.font, 6, (52, 44, 34, 255))
    plate = A.spray(sp.done(), 342, 0.85)
    m = np.clip((A.fbm(w, 56, 6, 3, 343) - 0.45) * 4, 0, 1) * (np.array(plate)[..., 3] / 255.0)
    plate.alpha_composite(A.to_img(np.broadcast_to(np.array((132, 64, 28), np.float32), (56, w, 3)).copy(), m * 0.7))
    A.over(img, plate, (0, 242))
    return img


def make_floppy():
    """A vertical floppy drive slot for the bottom of the left rail, 30 x 166: a beige bezel, the slot with its dark
    flap, an eject button and a dymo label; the activity LED (at 15, 14) comes from the C#."""
    w, h = 30, 166
    b = S.Big(w, h)
    b.rect((4, 3, w - 2, 149), (14, 12, 10, 140), r=5)
    b.rect((2, 0, w - 4, 146), (96, 90, 78, 255), r=5)
    b.rect((3, 1, w - 5, 144), (186, 178, 156, 255), r=4)
    b.rect((8, 6, w - 10, 22), (40, 38, 34, 255), r=2)          # LED window
    b.rect((9, 28, w - 11, 118), (24, 22, 20, 255), r=2)       # the slot recess
    b.rect((11, 30, w - 13, 116), (58, 56, 52, 255), r=1)      # the flap
    b.line([(13, 32), (13, 114)], (88, 86, 80, 255), 1)
    b.rect((10, 124, w - 12, 140), (60, 56, 50, 255), r=2)     # eject
    b.rect((11, 125, w - 13, 137), (150, 144, 128, 255), r=2)
    img = b.done()
    put(img, dymo("FD", 7, -2, 351), w / 2 - 1, 158)
    d = ImageDraw.Draw(img)
    d.polygon([(12, 133), (w / 2 - 1, 128), (w - 14, 133)], fill=(60, 56, 50, 255))
    noise = np.clip((A.fbm(w, h, 8, 3, 352) - 0.5) * 3, 0, 1) * (np.array(img)[..., 3] / 255.0) * 0.35
    img.alpha_composite(A.to_img(np.broadcast_to(np.array((92, 74, 50), np.float32), (h, w, 3)).copy(), noise))
    return img


def make_plate(w, h, r=6):
    m = A.mask((w, h), lambda d, s: d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], r * s, fill=255))
    rgb = A.hexc(0x34322e) + A.streaks(w, h, "x", 6, w)[..., None] + A.grain(w, h, w + 1, 3)[..., None] - (A.shade(m, 1.5) * 40)[..., None]
    return A.to_img(rgb, m)


def make_key(cap):
    """A rubber key, 40x34, for the top plate."""
    w, h = 40, 34
    m = A.mask((w, h), lambda d, s: d.rounded_rectangle([4 * s, 3 * s, (w - 4) * s, (h - 7) * s], 6 * s, fill=255))
    skirt = A.mask((w, h), lambda d, s: d.rounded_rectangle([1 * s, 2 * s, (w - 1) * s, (h - 2) * s], 8 * s, fill=255))
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32)
    rgb = np.array(cap, np.float32) + (A.shade(m, 3) * 70)[..., None] + (np.clip(1 - (yy - 4) / 12, 0, 1) * 26)[..., None] + A.grain(w, h, sum(cap), 3)[..., None]
    img = A.to_img(np.zeros((h, w, 3), np.float32), np.roll(A.blur(skirt, 1.5), 2, 0) * 0.7)
    img.alpha_composite(A.to_img(A.hexc(0x24221f) + (A.shade(skirt, 1) * 20)[..., None], skirt))
    img.alpha_composite(A.to_img(rgb, m))
    return img


def make_square_key():
    """A grey key in the style of the reference handheld, 42x34: a squarish cap with soft corners in a recess."""
    w, h = 42, 34
    well = A.mask((w, h), lambda d, s: d.rounded_rectangle([1 * s, 1 * s, (w - 1) * s, (h - 1) * s], 7 * s, fill=255))
    cap = A.mask((w, h), lambda d, s: d.rounded_rectangle([4 * s, 3 * s, (w - 4) * s, (h - 6) * s], 5 * s, fill=255))
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32)
    img = A.to_img(np.array((150, 142, 124), np.float32) - (A.shade(well, 1.5, (1, 1)) * 40)[..., None], well)
    img.alpha_composite(A.to_img(np.zeros((h, w, 3), np.float32), np.roll(A.blur(cap, 1.4), 2, 0) * 0.6))
    rgb = np.array((132, 132, 128), np.float32) + (A.shade(cap, 2.5) * 60)[..., None] + (np.clip(1 - (yy - 3) / 14, 0, 1) * 14)[..., None]
    rgb += A.grain(w, h, 77, 4)[..., None] + A.fbm(w, h, 6, 2, 78)[..., None] * 8
    img.alpha_composite(A.to_img(rgb, cap))
    return img


def make_lcd(col, frame=(28, 28, 26)):
    n = 32
    b = S.Big(n, n)
    b.rect((0, 0, n - 1, n - 1), frame + (255,), r=5)
    b.rect((5, 5, n - 6, n - 6), col + (255,), r=1)
    img = b.done()
    g = A.mask((n, n), lambda d, s: d.rectangle([5 * s, 5 * s, (n - 5) * s, 9 * s], fill=255), blur=1.5)
    img.alpha_composite(A.to_img(np.zeros((n, n, 3), np.float32), g * 0.25))
    return img


def make_disc(n, col, ring=None):
    b = S.Big(n, n)
    if ring:
        b.ellipse((0, 0, n - 1, n - 1), ring + (255,))
    k = 2 if ring else 0
    b.ellipse((k, k, n - 1 - k, n - 1 - k), col + (255,))
    b.ellipse((n * 0.28, n * 0.2, n * 0.5, n * 0.4), (255, 255, 255, 120))
    return b.done()


def make_slide(on):
    w, h = 84, 22
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (28, 28, 26, 255), r=6)
    b.rect((3, 9, w - 4, 12), (14, 14, 12, 255), r=1)
    x0 = 46 if on else 4
    b.rect((x0, 2, x0 + 33, h - 3), ORANGE + (255,), r=4)
    for k in range(4):
        b.line([(x0 + 10 + k * 4, 6), (x0 + 10 + k * 4, h - 7)], (170, 80, 30, 255), 1)
    return b.done()


def dymo(text, size=8, angle=0.0, seed=0):
    """An embossed label-maker strip: dark tape, raised white letters, slightly crooked."""
    f = A.font(size)
    tw = int(ImageDraw.Draw(Image.new("RGBA", (1, 1))).textlength(text, font=f)) + 12
    k = S.Big(tw + 4, size + 10)
    k.rect((2, 3, tw + 1, size + 8), (10, 10, 10, 120), r=2)
    k.rect((1, 1, tw, size + 6), (26, 26, 28, 255), r=2)
    k.text(((tw + 1) / 2, (size + 7) / 2 + 0.5), text, A.font, size, (238, 236, 228, 255))
    return A.spray(k.done(), seed, 0.97).rotate(angle, expand=True, resample=Image.BICUBIC)


def put(img, sprite, cx, cy):
    A.over(img, sprite, (round(cx - sprite.width / 2), round(cy - sprite.height / 2)))


def scale_ring(img, x, y, r0, r1, a0, a1, n, nums=None, col=INK + (255,)):
    d = ImageDraw.Draw(img)
    for i in range(n):
        a = np.radians(a0 + (a1 - a0) * i / (n - 1))
        d.line([(x + np.cos(a) * r0, y + np.sin(a) * r0), (x + np.cos(a) * r1, y + np.sin(a) * r1)], fill=col, width=1)
        if nums:
            d.text((x + np.cos(a) * (r1 + 7), y + np.sin(a) * (r1 + 7)), nums[i], font=A.font(7), fill=col, anchor="mm")


def knob(r, cap=(34, 32, 30), pointer=-2.2):
    k = S.Big(2 * r + 10, 2 * r + 12)
    c = r + 5
    k.ellipse((c - r - 3, c - r, c + r + 3, c + r + 5), (18, 16, 14, 150))
    k.ellipse((c - r, c - r - 1, c + r, c + r - 1), (150, 148, 142, 255))
    k.ellipse((c - r + 2, c - r + 1, c + r - 2, c + r - 3), (196, 194, 188, 255))
    k.ellipse((c - r * 0.72, c - r * 0.75, c + r * 0.72, c + r * 0.69), cap + (255,))
    for i in range(20):
        a = i * np.pi / 10
        k.line([(c + np.cos(a) * r * 0.6, c - 3 + np.sin(a) * r * 0.6), (c + np.cos(a) * r * 0.7, c - 3 + np.sin(a) * r * 0.7)], (64, 60, 56, 255), 1)
    k.ellipse((c - r * 0.42, c - r * 0.48, c + r * 0.42, c + r * 0.36), tuple(int(v * 1.5) for v in cap) + (255,))
    k.line([(c + np.cos(pointer) * r * 0.2, c - 3 + np.sin(pointer) * r * 0.2), (c + np.cos(pointer) * r * 0.66, c - 3 + np.sin(pointer) * r * 0.66)], (236, 232, 220, 255), 2)
    return k.done()


def make_col_knobs():
    """VOL and SQL knobs on printed scales with dymo labels, 122 x 100 (centres at x 25 / 97, y 44)."""
    img = Image.new("RGBA", (122, 100), (0, 0, 0, 0))
    for nm, x, v in (("VOL", 25, 0.55), ("SQL", 97, 0.3)):
        scale_ring(img, x, 44, 22, 26, 135, 405, 11)
        put(img, knob(17, pointer=np.radians(135 + 270 * v)), x, 46)
        put(img, dymo(nm, 7, A.rng(x).uniform(-2, 2), x), x, 80)
    return img


def make_gauge():
    """The TUNE gauge face, 132 x 132 (centre 66, 66; r 58): a chrome ring on three screws, a faded mustard face with
    a darker rim, numbers 0..10, a red end zone. The needle comes from the C#, the glass on top of it from GaugeGlass."""
    r, n = 58, 132
    c = n / 2
    g = S.Big(n, n)
    g.ellipse((c - r - 5, c - r - 3, c + r + 5, c + r + 7), (14, 12, 10, 140))
    for i, col in enumerate(((96, 94, 90), (210, 208, 202), (150, 148, 142), (120, 118, 112))):
        g.ellipse((c - r - 4 + i * 1.5, c - r - 4 + i * 1.5, c + r + 4 - i * 1.5, c + r + 4 - i * 1.5), col + (255,))
    g.ellipse((c - r + 3, c - r + 3, c + r - 3, c + r - 3), tuple(int(v * 0.8) for v in FACE) + (255,))
    g.ellipse((c - r + 6, c - r + 6, c + r - 6, c + r - 6), FACE + (255,))
    ink = (52, 40, 22, 255)
    for i in range(41):
        a = np.radians(150 + i * 6)
        big = i % 4 == 0
        r0 = r * (0.66 if big else 0.72)
        g.line([(c + np.cos(a) * r0, c + np.sin(a) * r0), (c + np.cos(a) * r * 0.8, c + np.sin(a) * r * 0.8)], ink, 2 if big else 1)
        if big:
            g.text((c + np.cos(a) * r * 0.53, c + np.sin(a) * r * 0.53), str(i // 4), A.font, 8, ink)
    red = [np.radians(a) for a in np.linspace(342, 390, 12)]
    g.line([(c + np.cos(a) * r * 0.84, c + np.sin(a) * r * 0.84) for a in red], (170, 46, 32, 255), 4)
    g.text((c, c + r * 0.3), "TUNE", A.font, 10, ink)
    g.text((c, c + r * 0.48), "MHz x10", A.font, 6, (88, 72, 44, 255))
    g.text((c, c - r * 0.3), "NR", A.font, 7, (128, 100, 44, 255))
    img = g.done()
    # age: a brown vignette and foxing spots on the face
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    rr = np.hypot(xx - c, yy - c) / r
    face = (rr < 0.9).astype(np.float32)
    age = (np.clip((rr - 0.5) * 1.6, 0, 1) * 0.35 + np.clip((A.fbm(n, n, 10, 3, 401) - 0.55) * 3, 0, 1) * 0.25) * face
    img.alpha_composite(A.to_img(np.broadcast_to(np.array((96, 70, 30), np.float32), (n, n, 3)).copy(), age))
    for i in range(3):
        a = np.radians(45 + i * 120)
        screw_on(img, c + np.cos(a) * (r + 1), c + np.sin(a) * (r + 1), 6, 0x9a968c)
    return img


def make_gauge_glass():
    """The glass over the gauge, 132 x 132: a crescent glint, dust and a crack spreading from a chip near the top right."""
    n, r = 132, 58
    c = n / 2
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    hl = A.mask((n, n), lambda d, s: d.ellipse([(c - r * 0.8) * s, (c - r * 0.86) * s, (c + r * 0.5) * s, (c + r * 0.1) * s], fill=255))
    cut = A.mask((n, n), lambda d, s: d.ellipse([(c - r * 0.7) * s, (c - r * 0.62) * s, (c + r * 0.7) * s, (c + r * 0.4) * s], fill=255))
    img.alpha_composite(A.to_img(np.full((n, n, 3), 255, np.float32), A.blur(np.clip(hl - cut, 0, 1), 2) * 0.32))
    rnd = A.rng(402)
    origin = (c + r * 0.42, c - r * 0.38)
    segs = []

    def crack(x, y, ang, ln, depth):
        px, py = x, y
        steps = int(ln / 6)
        for _ in range(steps):
            ang += rnd.uniform(-0.35, 0.35)
            nx, ny = px + np.cos(ang) * 6, py + np.sin(ang) * 6
            if np.hypot(nx - c, ny - c) > r * 0.88:
                break
            segs.append((px, py, nx, ny))
            if depth < 2 and rnd.random() < 0.18:
                crack(nx, ny, ang + rnd.choice([-1, 1]) * rnd.uniform(0.5, 1.1), ln * 0.45, depth + 1)
            px, py = nx, ny

    for ang in (np.radians(160), np.radians(205), np.radians(110), np.radians(250), np.radians(35)):
        crack(origin[0], origin[1], ang, rnd.uniform(30, 80), 0)
    m = A.mask((n, n), lambda d, s: [d.line([(a * s, b * s), (e * s, f * s)], fill=255, width=int(1.1 * s)) for a, b, e, f in segs])
    img.alpha_composite(A.to_img(np.zeros((n, n, 3), np.float32), np.roll(m, 1, 1) * 0.35))
    img.alpha_composite(A.to_img(np.full((n, n, 3), 245, np.float32), m * 0.75))
    chip = A.mask((n, n), lambda d, s: d.ellipse([(origin[0] - 3) * s, (origin[1] - 3) * s, (origin[0] + 3) * s, (origin[1] + 3) * s], fill=255), blur=0.8)
    img.alpha_composite(A.to_img(np.full((n, n, 3), 235, np.float32), chip * 0.6))
    dust = (rnd.random((n, n)) > 0.996).astype(np.float32) * (np.hypot(*np.mgrid[0:n, 0:n].astype(np.float32) - c) < r * 0.85)
    img.alpha_composite(A.to_img(np.full((n, n, 3), 230, np.float32), A.blur(dust, 0.6) * 0.8))
    return img


def make_col_scan():
    """A black plate with the SCAN toggle on a hex nut, 122 x 72; the ICE lamp (at 88, 32) comes from the C#."""
    w, h = 122, 72
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    pl = S.Big(w, h)
    pl.rect((2, 4, w - 1, h - 1), (14, 14, 12, 160), r=6)
    pl.rect((1, 1, w - 3, h - 5), (36, 36, 34, 255), r=6)
    pl.rect((4, 4, w - 6, h - 8), (48, 48, 46, 255), r=5)
    A.over(img, pl.done(), (0, 0))
    for x, y in ((9, 9), (w - 12, 9), (9, h - 13), (w - 12, h - 13)):
        screw_on(img, x, y, 6, 0x7a766e)
    hx, hy = 36, 34
    nut = S.Big(26, 26)
    nut.poly([(13 + np.cos(a) * 11, 13 + np.sin(a) * 11) for a in np.radians(np.arange(0, 360, 60) + 30)], (176, 174, 168, 255), (90, 88, 84, 255), 1)
    nut.ellipse((7, 7, 19, 19), (120, 118, 112, 255))
    put(img, nut.done(), hx, hy)
    tg = S.Big(30, 40)
    tg.line([(15, 30), (21, 6)], (90, 88, 84, 255), 6)
    tg.line([(15, 30), (21, 6)], (214, 212, 206, 255), 3.5)
    tg.ellipse((16, 1, 26, 11), (226, 224, 218, 255), (110, 108, 104, 255), 1)
    A.over(img, tg.done(), (hx - 15, hy - 30))
    d = ImageDraw.Draw(img)
    light = (214, 208, 192, 255)
    d.text((hx, 57), "SCAN", font=A.font(7), fill=light, anchor="mm")
    d.text((88, 57), "ICE", font=A.font(7), fill=light, anchor="mm")
    return img


def make_trace_scale():
    """The printed TRACE scale (1..5 over 200..340 degrees) and its dymo label, 122 x 104, knob centre at (61, 52)."""
    img = Image.new("RGBA", (122, 104), (0, 0, 0, 0))
    scale_ring(img, 61, 52, 34, 40, 200, 340, 5, nums=["1", "2", "3", "4", "5"])
    put(img, dymo("TRACE", 8, 1.5, 61), 61, 96)
    return img


def make_trace_knob():
    """A chicken-head pointer knob, 80 x 80, pointing straight up; the C# turns it to the Heat level."""
    kk = S.Big(80, 80)
    kk.ellipse((14, 18, 66, 70), (14, 12, 10, 140))
    kk.ellipse((16, 14, 64, 62), (150, 148, 142, 255))
    kk.ellipse((18, 16, 62, 60), (196, 194, 188, 255))
    pts = [(40, 6), (47, 30), (48, 52), (40, 58), (32, 52), (33, 30)]
    kk.poly([(x, y + 2) for x, y in pts], (10, 10, 10, 160))
    kk.poly(pts, (30, 28, 26, 255))
    kk.poly([(40, 9), (44, 30), (40, 34), (36, 30)], (58, 56, 52, 255))
    kk.line([(40, 10), (40, 24)], (236, 232, 220, 255), 2)
    img = kk.done()
    # recentre on the shaft (the cap's centre is at 40, 38)
    out = Image.new("RGBA", (80, 80), (0, 0, 0, 0))
    out.alpha_composite(img, dest=(0, 2))
    return out


def make_col_buttons():
    """JACK (orange) and PURGE (red) in chrome guard rings with dymo labels, 122 x 76 (centres 29 / 93, 26)."""
    img = Image.new("RGBA", (122, 76), (0, 0, 0, 0))
    for x, col, nm in ((29, ORANGE, "JACK"), (93, RED, "PURGE")):
        k = S.Big(54, 56)
        k.ellipse((3, 6, 51, 54), (14, 12, 10, 140))
        k.ellipse((2, 2, 50, 50), (110, 108, 104, 255))
        k.ellipse((4, 4, 48, 48), (200, 198, 192, 255))
        k.ellipse((8, 8, 44, 44), (24, 22, 20, 255))
        k.ellipse((10, 9, 42, 41), col + (255,))
        k.ellipse((15, 12, 33, 24), tuple(min(255, int(v * 1.3)) for v in col) + (255,))
        A.over(img, k.done(), (x - 26, 0))
        put(img, dymo(nm, 7, A.rng(x).uniform(-2.5, 2.5), x), x, 64)
    return img


def make_col_line():
    img = Image.new("RGBA", (110, 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line([(0, 1), (109, 1)], fill=(60, 52, 40, 170), width=2)
    d.line([(0, 3), (109, 3)], fill=(240, 234, 216, 90), width=1)
    return img


def make_vent():
    img = Image.new("RGBA", (96, 50), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for i in range(7):
        y = 1 + i * 7
        d.rounded_rectangle([2, y, 93, y + 3], 1, fill=(28, 26, 24, 255))
        d.line([(3, y + 4), (92, y + 4)], fill=(236, 230, 214, 140), width=1)
    return img


def make_stickers():
    """The yellow PROPERTY OF sticker and the paper label with kanji, side by side, 206 x 56."""
    img = Image.new("RGBA", (206, 56), (0, 0, 0, 0))
    st = S.Big(92, 46)
    st.rect((0, 0, 91, 45), YELLOW + (255,), r=2)
    st.rect((3, 3, 88, 13), (30, 28, 24, 255), r=1)
    st.text((46, 8), "PROPERTY OF", A.font, 7, YELLOW + (255,))
    st.text((46, 24), "V-NR 02", A.font, 13, (30, 28, 24, 255))
    st.text((46, 37), "5X65  OLD", A.font, 9, (30, 28, 24, 255))
    A.over(img, A.spray(st.done(), 323, 0.8).rotate(2, expand=True, resample=Image.BICUBIC), (0, 2))
    pp = S.Big(96, 50)
    pp.rect((0, 0, 95, 49), (226, 220, 204, 255), r=2)
    pp.text((30, 25), "遺物", S.jfont, 22, (178, 50, 40, 255))
    for i, t in enumerate(("S/N 4471", "TYPE II", "EXPORT")):
        pp.text((76, 12 + i * 12), t, A.font, 7, (60, 56, 50, 255))
    A.over(img, A.spray(pp.done(), 324, 0.86).rotate(-1.5, expand=True, resample=Image.BICUBIC), (104, 2))
    return img


def make_screw():
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    screw_on(img, 8, 8, 10)
    return img


def build():
    A.save(make_body(), T, "Body")
    A.save(make_badge(), T, "Badge")
    A.save(make_rail_text(), T, "RailText")
    A.save(make_rail_parts(), T, "RailParts")
    A.save(make_floppy(), T, "Floppy")
    A.save(make_plate(40, 40), T, "Plate")
    for name, cap in (("Red", RED), ("Grey", (126, 126, 120)), ("Orange", ORANGE)):
        A.save(make_key(cap), T, "Key_" + name)
    A.save(make_square_key(), T, "Key_Square")
    A.save(make_gauge(), T, "Gauge")
    A.save(make_gauge_glass(), T, "GaugeGlass")
    A.save(make_col_knobs(), T, "Col_Knobs")
    A.save(make_col_scan(), T, "Col_Scan")
    A.save(make_trace_scale(), T, "Col_TraceScale")
    A.save(make_trace_knob(), T, "Col_TraceKnob")
    A.save(make_col_buttons(), T, "Col_Buttons")
    A.save(make_col_line(), T, "Col_Line")
    A.save(make_vent(), T, "Col_Vent")
    A.save(dymo("TUNE / RX", 8, -1.2, 5), T, "Dymo_Tune")
    A.save(make_disc(20, (168, 164, 152), (60, 58, 54)), T, "LampBezel")
    A.save(make_disc(12, (255, 255, 255)), T, "Glass")
    A.save(make_lcd((150, 158, 112)), T, "Lcd")
    A.save(make_lcd((238, 124, 40)), T, "LcdOrange")
    A.save(make_slide(False), T, "Slide_0")
    A.save(make_slide(True), T, "Slide_1")
    A.save(make_stickers(), T, "Stickers")
    A.save(make_screw(), T, "Screw")
    import mock_relic3 as M
    A.save(M.relic(30).resize((300, 225), Image.LANCZOS), T, "Preview")


if __name__ == "__main__":
    build()
    print("Relic textures written")
