#!/usr/bin/env python3
"""
Textures of the Kurenai Systems deck theme, the light corporate one (Maxim, 2026-10-15: light and textured, after his
references: printed off-white stock, red blocks, black type, diagonal hatching, registration marks, microtype, a big
worn kanji on red). A corporate terminal that looks like a printed spec sheet.

    python tools/gen_deck_kurenai.py                 # textures into Textures/UI/Netrun/Frame/Kurenai/
    python tools/gen_deck_kurenai.py --mock OUT.png  # a composite at the default terminal size

The layout numbers below are mirrored in Source/Terminal/DeckKurenai.cs; change both together.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFont

import deck_art as A
import deck_stickers as S

T = "Kurenai"

BODY_W, BODY_H = 1400, 900
IN_L, IN_T, IN_R, IN_B = 72, 80, 50, 96
SLICE = 160
CUT = 40
OUT_L, OUT_T, OUT_R, OUT_B = 8, 8, 8, 8
BAND_X0, BAND_X1 = 16, 60   # the red band down the left side
LINE_Y = IN_T - 12          # the red hairline under the top bar

PAPER = (201, 200, 196)
INK = (26, 26, 28)
RED = (212, 36, 44)
GRID = (196, 195, 191)
DIM = (120, 120, 118)


def poly(x0, y0, x1, y1, c, s=1):
    """The body outline: top left and bottom right corners cut."""
    return [((x0 + c) * s, y0 * s), (x1 * s, y0 * s), (x1 * s, (y1 - c) * s), ((x1 - c) * s, y1 * s), (x0 * s, y1 * s), (x0 * s, (y0 + c) * s)]


def stock(w, h, seed, base=PAPER, dirt=1.0):
    """Printed off-white stock: soft mottling, grain, dust specks, fine scuffs."""
    r = A.rng(seed)
    rgb = np.broadcast_to(np.array(base, np.float32), (h, w, 3)).copy()
    rgb += (A.fbm(w, h, 90, 3, seed) * 6 * dirt)[..., None]
    rgb += (A.fbm(w, h, 14, 2, seed + 1) * 3)[..., None]
    rgb += A.grain(w, h, seed + 2, 2.6)[..., None]
    n = int(w * h / 900 * dirt)
    ys, xs = r.integers(0, h, n), r.integers(0, w, n)
    rgb[ys, xs] -= r.uniform(20, 70, (n, 1))
    n2 = n // 2
    ys, xs = r.integers(0, h, n2), r.integers(0, w, n2)
    rgb[ys, xs] += 14
    sc = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(sc)
    for _ in range(int(w * h / 26000 * dirt)):
        x, y = r.uniform(0, w), r.uniform(0, h)
        L, a = r.uniform(6, 30), r.uniform(-0.5, 0.5)
        d.line([x, y, x + L * np.cos(a), y + L * np.sin(a)], fill=int(r.uniform(60, 140)), width=1)
    rgb += (np.asarray(sc, np.float32) / 255 * 10)[..., None]
    return rgb


def distress(w, h, seed, amount=0.5):
    """0..1 wear mask: worn-through chips where ink or red paint is gone."""
    n = A.fbm(w, h, 22, 3, seed) + A.fbm(w, h, 5, 1, seed + 1) * 0.35
    return np.clip((n - (1 - amount) * 0.9) * 5, 0, 1)


def make_body():
    W, H = BODY_W, BODY_H
    s = A.SS
    outer = A.mask((W, H), lambda d, ss: d.polygon(poly(0, 0, W - 1, H - 1, CUT, ss), fill=255))
    hole = A.mask((W, H), lambda d, ss: d.rounded_rectangle([IN_L * ss, IN_T * ss, (W - IN_R) * ss - 1, (H - IN_B) * ss - 1], 6 * ss, fill=255))
    bezel = A.mask((W, H), lambda d, ss: d.rounded_rectangle([(IN_L - 8) * ss, (IN_T - 8) * ss, (W - IN_R + 8) * ss - 1, (H - IN_B + 8) * ss - 1], 12 * ss, fill=255))

    rgb = stock(W, H, 701, dirt=1.7)
    rgb -= (A.inner_edge(outer, 22) * 14)[..., None]
    rgb -= (np.clip(A.blur(bezel, 10) - bezel, 0, 1) * 18)[..., None]

    # the red band down the left side, worn, with the stock showing through the chips
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    band = ((xx >= BAND_X0) & (xx < BAND_X1) & (yy >= CUT + 30) & (yy < H - 70)).astype(np.float32)
    band = A.blur(band, 0.6)
    red = np.array(RED, np.float32) + (A.fbm(W, H, 30, 3, 702) * 16)[..., None] + A.grain(W, H, 703, 3)[..., None]
    wear = distress(W, H, 704, 0.42)
    k = band * (1 - wear)
    rgb = rgb * (1 - k[..., None]) + red * k[..., None]

    # the printed layer
    big = Image.new("RGBA", (W * s, H * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)

    def L(pts, col, w=1.0):
        d.line([(x * s, y * s) for x, y in pts], fill=col, width=max(1, int(w * s)), joint="curve")

    # the red hairline under the top bar, jogging up at 45 degrees inside the top-left corner
    L([(BAND_X1 + 8, LINE_Y), (118, LINE_Y), (130, LINE_Y - 12), (W - 26, LINE_Y - 12)], RED + (255,), 1.4)
    # a grey guide frame a little inside the edge, broken at the corners
    for seg in ([(CUT + 14, 9), (W - 70, 9)], [(W - 9, 60), (W - 9, H - CUT - 20)], [(9, CUT + 20), (9, H - 60)], [(70, H - 9), (W - CUT - 20, H - 9)]):
        L(seg, GRID + (255,), 1)
    # registration crosshairs in the top-right and bottom-left corners
    for cx, cy in ((W - 34, 34), (34, H - 40)):
        L([(cx - 12, cy), (cx + 12, cy)], INK + (220,), 1)
        L([(cx, cy - 12), (cx, cy + 12)], INK + (220,), 1)
        d.ellipse([(cx - 6) * s, (cy - 6) * s, (cx + 6) * s, (cy + 6) * s], outline=INK + (220,), width=s)
    # a red bracket at the bottom-right corner of the screen
    L([(W - IN_R + 16, H - IN_B - 30), (W - IN_R + 16, H - IN_B + 16), (W - IN_R - 30, H - IN_B + 16)], RED + (255,), 1.6)
    # micro labels in the corners
    f6 = A.font(6 * s, False)
    d.text(((W - 56) * s, 54 * s), "0321", font=f6, fill=INK + (200,))
    d.text(((W - 56) * s, 12 * s), "0419", font=f6, fill=INK + (200,))
    d.text((40 * s, (H - 62) * s), "KS-0007", font=f6, fill=INK + (200,), anchor="mm")
    # black slanted bars by the bottom-right cut (the Kurenai mark, large)
    for i in range(3):
        x = W - 150 + i * 18
        d.polygon([(x * s, (H - 14) * s), ((x + 10) * s, (H - 14) * s), ((x + 26) * s, (H - 34) * s), ((x + 16) * s, (H - 34) * s)], fill=INK + (235,))
    # grey diagonal hatching under the top-left cut
    hb = (BAND_X1 + 8, 14, 118, LINE_Y - 8)
    hm = Image.new("L", (W * s, H * s), 0)
    hd = ImageDraw.Draw(hm)
    for k2 in range(-80, 140, 6):
        hd.line([((hb[0] + k2) * s, hb[3] * s), ((hb[0] + k2 + 40) * s, hb[1] * s)], fill=255, width=int(1.6 * s))
    clip = Image.new("L", (W * s, H * s), 0)
    ImageDraw.Draw(clip).rectangle([hb[0] * s, hb[1] * s, hb[2] * s, hb[3] * s], fill=255)
    cut = Image.new("L", (W * s, H * s), 0)
    ImageDraw.Draw(cut).polygon(poly(0, 0, W - 1, H - 1, CUT + 8, s), fill=255)
    hm = ImageChops.multiply(ImageChops.multiply(hm, clip), cut)
    big.paste(Image.new("RGBA", big.size, (150, 150, 148, 255)), (0, 0), hm)
    printed = big.resize((W, H), Image.LANCZOS)
    pa = np.asarray(printed, np.float32)
    pw = 1 - distress(W, H, 705, 0.18) * 0.7
    a = pa[..., 3] / 255 * pw
    rgb = rgb * (1 - a[..., None]) + pa[..., :3] * a[..., None]

    # the screen bezel: black satin plastic like a badge holder, a soft gloss along its top
    ring = bezel * (1 - hole)
    brgb = np.full((H, W, 3), 22, np.float32) + (A.shade(bezel, 1.6) * 60)[..., None] + A.grain(W, H, 706, 1.5)[..., None]
    brgb += (np.clip(1 - np.abs(yy - (IN_T - 6)) / 3, 0, 1) * 20)[..., None]
    rgb = rgb * (1 - ring[..., None]) + brgb * ring[..., None]

    # the body edge: a crisp bevel and a dark outline
    rgb += (A.shade(outer, 1.3) * 45)[..., None]
    rgb -= (A.inner_edge(outer, 1.2) * 90)[..., None]
    return A.to_img(rgb, outer * (1 - hole))


def make_kanji():
    """紅 big in black on the red band, worn like the reference poster; クレナイ small under it."""
    w, h = BAND_X1 - BAND_X0, 150
    s = A.SS
    big = Image.new("L", (w * s, h * s), 0)
    d = ImageDraw.Draw(big)
    d.text((w / 2 * s, 24 * s), "紅", font=S.jfont(40 * s), fill=255, anchor="mm")
    for i, c in enumerate("クレナイ"):
        d.text((w / 2 * s, (64 + i * 16) * s), c, font=S.jfont(13 * s), fill=255, anchor="mm")
    m = np.asarray(big.resize((w, h), Image.LANCZOS), np.float32) / 255
    m *= 1 - distress(w, h, 711, 0.45) * 0.85
    rgb = np.full((h, w, 3), INK, np.float32) + A.grain(w, h, 712, 3)[..., None]
    return A.to_img(rgb, m)


def make_mark(size=26):
    """The Kurenai mark: two slanted bars in a red square."""
    b = S.Big(size, size)
    b.rect((0, 0, size - 1, size - 1), RED + (255,), r=1)
    k = size / 26
    for x in (5, 13):
        b.poly([(x * k, 21 * k), ((x + 5) * k, 21 * k), ((x + 12) * k, 5 * k), ((x + 7) * k, 5 * k)], PAPER + (255,))
    return b.done()


def make_logo():
    mark = make_mark(30)
    f = ImageFont.truetype("bahnschrift.ttf", 22)
    try:
        f.set_variation_by_axes([125, 700])
    except Exception:
        pass
    word = "KURENAI"
    tw = int(sum(f.getlength(c) for c in word) + 2 * (len(word) - 1))
    sub = A.font(7, False)
    W, H = 30 + 10 + max(tw, 150) + 4, 34
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    img.alpha_composite(mark, dest=(0, 1))
    d = ImageDraw.Draw(img)
    x = 40
    for c in word:
        d.text((x, 13), c, font=f, fill=INK + (255,), anchor="lm")
        x += f.getlength(c) + 2
    d.text((41, 29), "SYSTEMS  —  EXEC TERMINAL  //  KS-0007", font=sub, fill=DIM + (255,), anchor="lm")
    return img


def textured(img, seed):
    arr = np.asarray(img, np.float32).copy()
    w, h = img.size
    m = arr[..., 3:] / 255
    arr[..., :3] += (A.grain(w, h, seed, 2.2)[..., None] + (A.fbm(w, h, 8, 2, seed + 1) * 4)[..., None]) * m
    return Image.fromarray(np.clip(arr, 0, 255).astype("uint8"), "RGBA")


def make_pad(face, edge):
    """A key: a printed rectangle with one cut corner, a thin dark outline, a hard offset shadow."""
    w, h = 72, 30
    b = S.Big(w + 2, h + 2)
    b.poly([(6, 2), (w + 1, 2), (w + 1, h + 1), (2, h + 1), (2, 6)], (0, 0, 0, 90))
    b.poly([(5, 0), (w - 1, 0), (w - 1, h - 1), (0, h - 1), (0, 5)], face + (255,), outline=edge + (255,), width=1.1)
    return textured(b.done(), 721)


def make_ring(face, edge):
    n = 40
    b = S.Big(n + 2, n + 2)
    b.ellipse((2, 2, n + 1, n + 1), (0, 0, 0, 90))
    b.ellipse((0, 0, n - 1, n - 1), face + (255,), outline=edge + (255,), width=1.2)
    b.ellipse((5, 5, n - 6, n - 6), None, outline=(255, 255, 255, 40), width=0.8)
    return textured(b.done(), 723)


def make_led(color):
    n = 9
    b = S.Big(n, n)
    b.rect((0, 0, n - 1, n - 1), INK + (255,), r=0.5)
    if color is not None:
        b.rect((1.5, 1.5, n - 2.5, n - 2.5), color + (255,), r=0.5)
    return b.done()


def make_lcd():
    W, H = 120, 56
    b = S.Big(W, H)
    b.rect((0, 0, W - 1, H - 1), INK + (255,), r=3)
    arr = np.asarray(b.done(), np.float32).copy()
    m = A.mask((W, H), lambda d, ss: d.rounded_rectangle([4 * ss, 4 * ss, (W - 4) * ss - 1, (H - 4) * ss - 1], 2 * ss, fill=255))
    glass = A.gradient_v(W, H, A.hexc(0x10170f), A.hexc(0x060906))
    arr[..., :3] = arr[..., :3] * (1 - m[..., None]) + glass * m[..., None]
    return Image.fromarray(np.clip(arr, 0, 255).astype("uint8"), "RGBA")


def make_seal():
    """A square red hanko: 紅 cut out of the red, worn."""
    n = 46
    b = S.Big(n, n)
    b.rect((1, 1, n - 2, n - 2), RED + (255,), r=4)
    b.text((n / 2, n / 2 + 1), "紅", S.jfont, 30, PAPER + (255,))
    arr = np.asarray(b.done(), np.float32).copy()
    arr[..., 3] *= 1 - distress(n, n, 731, 0.3) * 0.6
    return Image.fromarray(np.clip(arr, 0, 255).astype("uint8"), "RGBA")


def make_knob():
    w, h = 12, 22
    b = S.Big(w + 2, h + 2)
    b.rect((2, 2, w + 1, h + 1), (0, 0, 0, 90), r=1)
    b.rect((0, 0, w - 1, h - 1), RED + (255,), r=1, outline=INK + (255,), width=1)
    b.line([(w / 2 - 0.5, 5), (w / 2 - 0.5, h - 6)], PAPER + (200,), 1)
    return b.done()


def make_hatch(w=22, h=120):
    """A column of black diagonal hatching for the right side."""
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), None, outline=INK + (255,), width=1)
    img = b.done()
    s = A.SS
    big = Image.new("L", (w * s, h * s), 0)
    d = ImageDraw.Draw(big)
    for k in range(-w, h + w, 7):
        d.line([(0, (k + w) * s), (w * s, k * s)], fill=255, width=int(2.2 * s))
    m = np.asarray(big.resize((w, h), Image.LANCZOS), np.float32) / 255
    m *= 1 - distress(w, h, 741, 0.25) * 0.7
    img.alpha_composite(A.to_img(np.full((h, w, 3), INK, np.float32), m))
    return img


def make_xmarks():
    b = S.Big(14, 50)
    for i in range(3):
        y = 2 + i * 17
        b.line([(2, y), (12, y + 10)], RED + (255,), 1.3)
        b.line([(12, y), (2, y + 10)], RED + (255,), 1.3)
    return b.done()


def make_checker(w=96, h=8):
    b = S.Big(w, h)
    for i in range(w // 4):
        for j in range(2):
            if (i + j) % 2 == 0:
                b.rect((i * 4, j * 4, i * 4 + 4, j * 4 + 4), INK + (255,))
    return b.done()


def tracked(text, size, fill, spacing):
    f = A.font(size, False)
    w = int(sum(f.getlength(c) for c in text) + spacing * (len(text) - 1)) + 4
    img = Image.new("RGBA", (w, size + 6), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    x = 2
    for c in text:
        d.text((x, (size + 6) / 2), c, font=f, fill=fill, anchor="lm")
        x += f.getlength(c) + spacing
    return img


def build():
    A.save(make_body(), T, "Body")
    A.save(make_kanji(), T, "Kanji")
    A.save(make_logo(), T, "Logo")
    A.save(make_pad((190, 189, 186), INK), T, "Pad")
    A.save(make_pad(RED, INK), T, "Pad_Red")
    A.save(make_pad(INK, INK), T, "Pad_Black")
    A.save(make_ring(INK, INK), T, "Ring")
    A.save(make_ring(RED, INK), T, "Ring_Red")
    for name, col in (("Off", None), ("Green", (52, 190, 100)), ("Orange", (240, 138, 46)), ("Red", RED)):
        A.save(make_led(col), T, "Led_" + name)
    A.save(make_lcd(), T, "Lcd")
    A.save(make_seal(), T, "Seal")
    A.save(make_knob(), T, "Knob")
    A.save(make_hatch(), T, "Hatch")
    A.save(make_xmarks(), T, "XMarks")
    A.save(make_checker(), T, "Checker")
    A.save(S.barcode(70, 26, "KS-0007-EX", seed=751).rotate(90, expand=True), T, "Barcode")
    A.save(tracked("PROPERTY OF KURENAI SYSTEMS  ·  UNAUTHORISED USE IS AN OFFENCE", 7, DIM + (255,), 2.0), T, "Micro_Bottom")
    A.save(tracked("KURENAI SYSTEMS  ·  EXEC SERIES  KS-0007", 7, DIM + (255,), 2.4).rotate(-90, expand=True), T, "Micro_Side")
    A.save(mock(None).resize((300, 240), Image.LANCZOS), T, "Preview")


# ------------------------------------------------------------------ a composite, the way DeckKurenai.cs lays it out

HEAT_SEG = [(49, 170, 96), (214, 170, 30), (236, 120, 40), (212, 36, 44), (120, 10, 24)]


def mono(size):
    try:
        return ImageFont.truetype("consola.ttf", size)
    except OSError:
        return A.font(size)


def mock(out_path=None, screen=(930, 666), heat=58, online=True):
    W = screen[0] + OUT_L + OUT_R + IN_L + IN_R
    H = screen[1] + OUT_T + OUT_B + IN_T + IN_B

    def load(n):
        return Image.open(os.path.join(A.FRAME, T, n + ".png")).convert("RGBA")
    img = Image.new("RGBA", (W, H), (40, 44, 40, 255))
    d = ImageDraw.Draw(img)
    bx0, by0, bx1, by1 = OUT_L, OUT_T, W - OUT_R, H - OUT_B
    A.over(img, A.nine_slice(load("Body"), (bx1 - bx0, by1 - by0), SLICE, SLICE, SLICE, SLICE), (bx0, by0))
    sx0, sy0, sx1, sy1 = bx0 + IN_L, by0 + IN_T, bx1 - IN_R, by1 - IN_B
    d.rounded_rectangle([sx0, sy0, sx1 - 1, sy1 - 1], 6, fill=(26, 30, 26, 255))
    f = mono(13)
    for i, (t, c) in enumerate((("NETRUN // NETWORK CORE", (90, 230, 130)), ("> HOME NODE ........ ONLINE", (90, 210, 130)),
                                ("> GATEWAY .......... 5 NODES", (90, 210, 130)), ("> HEAT 58 .......... TRACKED", (230, 190, 70)))):
        d.text((sx0 + 30, sy0 + 28 + i * 22), t, font=f, fill=c)
    for r in (60, 120):
        d.ellipse([sx1 - 230 - r, (sy0 + sy1) / 2 - r, sx1 - 230 + r, (sy0 + sy1) / 2 + r], outline=(40, 120, 70))

    A.over(img, load("Kanji"), (bx0 + BAND_X0, by0 + 110))

    # top bar: logo, keys, lamps
    A.over(img, load("Logo"), (bx0 + 140, by0 + 20))
    keys = (("CLOSE", "Pad_Red", PAPER), ("MENU", "Pad", INK), ("ACHIEV", "Pad", INK), ("NET-LOCK", "Pad", INK))
    kx = bx1 - IN_R - 4 * 80 - 156
    fk = A.font(10)
    for i, (lab, tex, ink) in enumerate(keys):
        A.over(img, load(tex), (kx + i * 80, by0 + 22))
        d.text((kx + i * 80 + 36, by0 + 37), lab, font=fk, fill=ink, anchor="mm")
    lx = bx1 - IN_R - 96
    for i, (led, lab) in enumerate((("Led_Green", "NET"), ("Led_Orange", "SICK"), ("Led_Off", "POD"))):
        A.over(img, load(led), (lx + i * 30, by0 + 24))
        d.text((lx + i * 30 + 4, by0 + 44), lab, font=A.font(7, False), fill=DIM, anchor="mm")
    lx -= 44
    for i in range(5):
        hh = 3 + i * 2.4
        d.rectangle([lx + 2 + i * 5, by0 + 34 - hh, lx + 4 + i * 5, by0 + 34], fill=INK if i < 4 else (190, 190, 186))
    d.text((lx + 4, by0 + 44), "RF", font=A.font(7, False), fill=DIM, anchor="lm")

    # right column: hatching, X marks, vertical microtext, barcode
    A.over(img, load("Hatch"), (bx1 - IN_R + 14, by0 + IN_T + 20))
    A.over(img, load("XMarks"), (bx1 - IN_R + 18, by0 + IN_T + 158))
    A.over(img, load("Micro_Side"), (bx1 - IN_R + 20, by0 + IN_T + 230))
    bc = load("Barcode")
    A.over(img, bc, (bx1 - IN_R + 10, by1 - IN_B - bc.height - 30))

    # bottom bar
    yb = by1 - IN_B
    A.over(img, load("Seal"), (bx0 + IN_L - 2, yb + 22))
    lcd = (bx0 + IN_L + 58, yb + 18, 240, 56)
    A.over(img, A.nine_slice(load("Lcd"), lcd[2:], 8, 8, 8, 8), lcd[:2])
    fm = mono(11)
    for i, t in enumerate(("CYBERDATA 491", "HEAT 58 ▲ TRACKED", "D8 08:27  NET ON")):
        d.text((lcd[0] + 10, lcd[1] + 7 + i * 14), t, font=fm, fill=(110, 230, 140))
    rx = lcd[0] + lcd[2] + 34
    for i, (lab, g) in enumerate((("BACK", "<"), ("CORE", "O"), ("HELP", "?"))):
        A.over(img, load("Ring_Red" if i == 1 else "Ring"), (rx + i * 54, yb + 20))
        d.text((rx + i * 54 + 20, yb + 40), g, font=A.font(15), fill=PAPER, anchor="mm")
        d.text((rx + i * 54 + 20, yb + 70), lab, font=A.font(7, False), fill=DIM, anchor="mm")
    tx = rx + 3 * 54 + 30
    d.text((tx, yb + 24), "THREAT INDEX", font=A.font(8), fill=INK, anchor="lm")
    lit = int(heat / 20 + 0.999)
    for i in range(5):
        c = HEAT_SEG[i] if i < lit else (176, 175, 171)
        d.rectangle([tx + i * 26, yb + 34, tx + i * 26 + 21, yb + 46], fill=c, outline=INK)
    d.text((tx, yb + 56), "58 / TRACKED", font=A.font(7, False), fill=DIM, anchor="lm")
    A.over(img, load("Checker"), (tx, yb + 66))
    sx = tx + 5 * 26 + 34
    d.text((sx, yb + 24), "NET LINK", font=A.font(8), fill=INK, anchor="lm")
    d.rectangle([sx, yb + 39, sx + 110, yb + 41], fill=INK)
    for i in range(0, 111, 22):
        d.line([sx + i, yb + 44, sx + i, yb + 48], fill=INK)
    d.text((sx, yb + 56), "OFF", font=A.font(7, False), fill=DIM, anchor="lm")
    d.text((sx + 110, yb + 56), "ON", font=A.font(7, False), fill=DIM, anchor="rm")
    A.over(img, load("Knob"), (sx + (104 if online else 0), yb + 29))
    A.over(img, load("Micro_Bottom"), (bx0 + IN_L + 58, by1 - 20))
    if out_path:
        img.convert("RGB").save(out_path)
    return img


if __name__ == "__main__":
    if len(sys.argv) > 2 and sys.argv[1] == "--mock":
        mock(sys.argv[2])
    else:
        build()
        print("Kurenai textures written")
