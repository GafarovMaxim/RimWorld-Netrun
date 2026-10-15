#!/usr/bin/env python3
"""
Textures of the "Studio" deck (Braindance // Studio, docs/mockups/37f-theme-studio.png; compact variant, Maxim's pick
2026-10-16): a graphite monitor standing behind a wide sloped desk. The monitor's top strip carries the title, a
timeline (timecode and a scrolling track under the playhead), the braindance chip bay, the REC / NET / SICK / POD lamps
and signal bars; the desk is one row: the LEVEL LED meter (HEAT and SIG), square keys
with a warm backlight, the SESSION LCD, a model plate, channel faders VISUAL / AUDIO / SOMATIC / EMOTION, the NET LINK
fader and the big TUNER JOG wheel.
DeckStudio.cs lays them out (keep the numbers here and there in step).

    python tools/gen_deck_studio.py                 textures
    python tools/gen_deck_studio.py --mock OUT      textures and a mockup (docs/mockups/49-studio.png)
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

import deck_art as A
import deck_stickers as S

T = "Studio"
W, H = 1400, 900
IN_L, IN_T, IN_R, IN_B = 52, 58, 52, 166      # box to screen (DeckStudio.cs: StudioIn*)
OVER = 24                                      # the desk is wider than the monitor by this on each side
DESK = 150                                     # desk height from the box bottom
LIP = 16                                       # the desk's front lip
DY0 = H - DESK
JOG_DX, JOG_DY, JOG_R = 80, 66, 52             # jog centre from the box's right edge / desk top
GRAPHITE0, GRAPHITE1 = (54, 44, 68), (34, 27, 44)          # the monitor: muted dark violet
DESK0, DESK1 = (70, 58, 86), (50, 41, 63)
ACCENT = (180, 122, 168)                       # muted magenta
INK = (196, 190, 176)                         # warm light print on the dark desk
HEAT_SEG = [(49, 196, 106), (230, 196, 49), (255, 138, 60), (255, 74, 60), (179, 18, 42)]


def rrect(size, box, r):
    x0, y0, x1, y1 = box
    return A.mask(size, lambda d, s: d.rounded_rectangle([x0 * s, y0 * s, x1 * s - 1, y1 * s - 1], r * s, fill=255))


def poly(size, pts):
    return A.mask(size, lambda d, s: d.polygon([(x * s, y * s) for x, y in pts], fill=255))


def vgrad(w, h, c0, c1, y0=0, y1=None):
    y1 = h if y1 is None else y1
    t = np.clip((np.mgrid[0:h, 0:w][0].astype(np.float32) - y0) / max(1, y1 - y0), 0, 1)[..., None]
    return np.array(c0, np.float32) * (1 - t) + np.array(c1, np.float32) * t


def screw_on(img, x, y, n=11):
    A.over(img, A.rivet(n, A.hexc(0x6a6c6e), int(x * 7 + y)), (x - n / 2 - 2, y - n / 2 - 2))
    d = ImageDraw.Draw(img)
    a = A.rng(int(x + y * 3)).uniform(0, np.pi)
    d.line([(x - np.cos(a) * n * 0.3, y - np.sin(a) * n * 0.3), (x + np.cos(a) * n * 0.3, y + np.sin(a) * n * 0.3)], fill=(24, 24, 26, 255), width=2)


def bd_chip():
    """The braindance chip: a blue card with a label, gold contacts at its back end."""
    w, h = 78, 28
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (34, 52, 86, 255), r=4)
    b.rect((2, 2, w - 3, 5), (70, 96, 140, 255), r=2)
    for k in range(6):
        b.rect((w - 12, 6 + k * 3.2, w - 4, 7.6 + k * 3.2), (176, 150, 84, 255))
    b.text((30, 15), "BD · RAW 07", A.font, 8, (200, 212, 232, 255))
    return A.spray(b.done(), 91, 0.95)


def make_body():
    size = (W, H)
    mon = rrect(size, (OVER, 0, W - OVER, DY0 + 24), 18)
    desk = poly(size, [(12, DY0), (W - 12, DY0), (W, H - LIP), (0, H - LIP)])
    lip = rrect(size, (0, H - LIP - 2, W, H), 8)
    hole = rrect(size, (IN_L, IN_T, W - IN_R, H - IN_B), 8)
    full = np.clip(mon + desk + lip, 0, 1)

    # the monitor: graphite with a faint brushed grain, lit top-left edge, a recess around the screen
    rgb = vgrad(W, H, GRAPHITE0, GRAPHITE1, 0, DY0) + A.streaks(W, H, "x", 3, 401)[..., None] + A.grain(W, H, 402, 2)[..., None]
    rgb += (A.shade(mon, 3) * 40)[..., None]
    lipr = np.clip(rrect(size, (IN_L - 8, IN_T - 8, W - IN_R + 8, H - IN_B + 8), 12) - hole, 0, 1)
    rgb -= lipr[..., None] * 14
    rgb -= (A.shade(np.clip(1 - hole, 0, 1) * rrect(size, (IN_L - 8, IN_T - 8, W - IN_R + 8, H - IN_B + 8), 12), 2, (1, 1)) * 26)[..., None]
    # a thin seam under the top strip's corners and a darker band where the monitor sinks into the desk
    yy = np.mgrid[0:H, 0:W][0].astype(np.float32)
    rgb -= (np.clip(1 - np.abs(yy - (DY0 - 4)) / 10, 0, 1) * mon * 18)[..., None]

    # the desk: a sloped face lit along its top edge, a darker front lip
    drgb = vgrad(W, H, DESK0, DESK1, DY0, H - LIP) + A.fbm(W, H, 50, 3, 403)[..., None] * 6 + A.grain(W, H, 404, 2)[..., None]
    drgb += (np.clip(1 - (yy - DY0) / 3, 0, 1) * 34)[..., None]
    rgb = rgb * (1 - desk[..., None]) + drgb * desk[..., None]
    lrgb = vgrad(W, H, (34, 27, 44), (20, 16, 26), H - LIP, H) + A.grain(W, H, 405, 2)[..., None]
    lonly = np.clip(lip - desk, 0, 1)
    rgb = rgb * (1 - lonly[..., None]) + lrgb * lonly[..., None]
    rgb += (np.clip(1 - np.abs(yy - (H - LIP)) / 1.5, 0, 1) * desk * 20)[..., None]

    # the jog well, baked here (it sits in the right-bottom corner slice)
    jx, jy = W - JOG_DX, DY0 + JOG_DY
    well = A.mask(size, lambda d, s: d.ellipse([(jx - JOG_R - 6) * s, (jy - JOG_R - 6) * s, (jx + JOG_R + 6) * s, (jy + JOG_R + 6) * s], fill=255))
    rgb -= (well * 26)[..., None] + (A.shade(well, 2, (1, 1)) * 30)[..., None]
    img = A.to_img(rgb, full * (1 - hole))

    for x, y in ((OVER + 14, 14), (W - OVER - 14, 14)):
        screw_on(img, x, y)
    return img


METER_ROWS = 12
METER_COLS = (14, 25, 47, 58)                  # x of the LED columns: HEAT 1 / 2, SIG 1 / 2
METER_Y0, METER_STEP, METER_LED = 24, 7, (9, 5)


def meter_colour(row):
    """The LED colour of a row from the bottom: green, yellow, orange, red at the top."""
    return (64, 200, 96) if row < 7 else (228, 200, 56) if row < 9 else (255, 140, 52) if row < 11 else (236, 62, 50)


def make_meter():
    """The LEVEL meter, 82 x 122, after Maxim's reference: a dark recessed panel, MASTER and HEAT / SIG on top, LED
    segments in four columns (unlit ones baked dim), LEVEL at the bottom. Lit LEDs come from the C#."""
    w, h = 82, 122
    outer = rrect((w, h), (0, 0, w, h), 7)
    inner = rrect((w, h), (3, 3, w - 3, h - 3), 5)
    img = A.to_img(np.array((26, 27, 28), np.float32) + (A.shade(outer, 1.5) * 34)[..., None], outer)
    rgb = np.array((13, 14, 15), np.float32) + A.grain(w, h, 430, 2)[..., None] - (A.shade(inner, 2, (1, 1)) * 20)[..., None]
    img.alpha_composite(A.to_img(rgb, inner))
    b = S.Big(w, h)
    b.text((w / 2, 9), "MASTER", A.font, 6, INK + (210,))
    b.text((24, 18), "HEAT", A.font, 6, INK + (210,))
    b.text((57, 18), "SIG", A.font, 6, INK + (210,))
    b.line([(41, 13), (41, 110)], (40, 42, 44, 255), 1)
    for r in range(METER_ROWS):
        c = meter_colour(r)
        y = METER_Y0 + (METER_ROWS - 1 - r) * METER_STEP
        for x in METER_COLS:
            b.rect((x, y, x + METER_LED[0] - 1, y + METER_LED[1] - 1), tuple(int(v * 0.2 + 12) for v in c) + (255,), r=1)
    b.text((w / 2, 114), "LEVEL", A.font, 7, INK + (220,))
    img.alpha_composite(b.done())
    return img


def make_bay():
    """The braindance chip bay on the monitor's top strip, 150 x 34: a recess with a smoked window over the chip, an
    eject button on the right. The busy LED (at 129, 8) comes from the C#."""
    w, h = 150, 34
    outer = rrect((w, h), (0, 0, w, h), 6)
    img = A.to_img(np.array((22, 24, 26), np.float32) + (A.shade(outer, 1.5, (1, 1)) * 30)[..., None], outer)
    win = rrect((w, h), (6, 6, 116, h - 6), 3)
    img.alpha_composite(A.to_img(np.array((8, 9, 11), np.float32) + np.zeros((h, w, 3), np.float32), win))
    chip = bd_chip().resize((96, 18), Image.LANCZOS)
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    layer.alpha_composite(chip, (12, 8))
    a = np.array(layer.getchannel("A"), np.float32) / 255 * win
    rgbc = np.array(layer.convert("RGB"), np.float32) * 0.78 + np.array((10, 6, 18), np.float32) * 0.22
    img.alpha_composite(A.to_img(rgbc, a))
    glare = A.mask((w, h), lambda d, s: d.polygon([(10 * s, 6 * s), (60 * s, 6 * s), (40 * s, (h - 6) * s), (-10 * s, (h - 6) * s)], fill=255), blur=2) * win
    img.alpha_composite(A.to_img(np.full((h, w, 3), 255, np.float32), glare * 0.08))
    ej = rrect((w, h), (122, 15, 144, 27), 3)
    img.alpha_composite(A.to_img(np.array((86, 88, 90), np.float32) + (A.shade(ej, 1.5) * 50)[..., None], ej))
    d = ImageDraw.Draw(img)
    d.polygon([(129, 23), (137, 23), (133, 19)], fill=(28, 28, 30, 255))
    d.line([(129, 25), (137, 25)], fill=(28, 28, 30, 255), width=1)
    return img


def make_wave():
    """The timeline's track strip, 256 x 22, seamless across x: three lanes of braindance data (visual, audio,
    emotion) as dim bar waveforms. The C# scrolls it under a fixed playhead."""
    w, h = 256, 22
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    lanes = [((150, 110, 200), 2, 6, 0.0), ((80, 190, 200), 9, 6, 1.7), ((220, 160, 80), 16, 5, 3.1)]
    for col, y0, hh, ph in lanes:
        for x in range(0, w, 2):
            t = 2 * np.pi * x / w
            v = 0.5 + 0.25 * np.sin(t * 3 + ph) + 0.15 * np.sin(t * 7 + ph * 2) + 0.1 * np.sin(t * 13 + ph * 3)
            bh = max(1, v * hh)
            d.line([(x, y0 + hh / 2 - bh / 2), (x, y0 + hh / 2 + bh / 2)], fill=col + (170,), width=1)
    return img


def make_key(cap):
    """A square key with a warm backlit edge, 38 x 32: a dark skirt, a domed cap with a highlight."""
    w, h = 38, 32
    skirt = rrect((w, h), (1, 2, w - 1, h - 1), 7)
    m = rrect((w, h), (4, 3, w - 4, h - 6), 5)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32)
    img = A.to_img(np.zeros((h, w, 3), np.float32), np.roll(A.blur(skirt, 1.5), 2, 0) * 0.7)
    img.alpha_composite(A.to_img(np.array((20, 21, 22), np.float32) + (A.shade(skirt, 1) * 18)[..., None], skirt))
    edge = np.clip(A.blur(m, 2.2) - m, 0, 1)
    img.alpha_composite(A.to_img(np.broadcast_to(np.array((255, 170, 90), np.float32), (h, w, 3)).copy(), edge * 0.35))
    rgb = np.array(cap, np.float32) + (A.shade(m, 2.5) * 56)[..., None] + (np.clip(1 - (yy - 3) / 12, 0, 1) * 18)[..., None] + A.grain(w, h, sum(cap), 3)[..., None]
    img.alpha_composite(A.to_img(rgb, m))
    return img


def make_lcd():
    n = 32
    b = S.Big(n, n)
    b.rect((0, 0, n - 1, n - 1), (16, 17, 18, 255), r=5)
    b.rect((4, 4, n - 5, n - 5), (8, 22, 14, 255), r=2)
    img = b.done()
    g = A.mask((n, n), lambda d, s: d.rectangle([4 * s, 4 * s, (n - 4) * s, 8 * s], fill=255), blur=1.5)
    img.alpha_composite(A.to_img(np.full((n, n, 3), 255, np.float32), g * 0.05))
    return img


def make_fader_slot():
    """A fader groove with tick marks, 30 x 100."""
    w, h = 30, 100
    b = S.Big(w, h)
    for i in range(11):
        y = 8 + i * 8.4
        ln = 6 if i % 5 == 0 else 4
        b.line([(4, y), (4 + ln, y)], (150, 150, 142, 200), 1)
        b.line([(w - 4 - ln, y), (w - 4, y)], (150, 150, 142, 200), 1)
    b.rect((12, 2, 18, h - 3), (8, 8, 9, 255), r=3)
    b.rect((13, 3, 17, 6), (40, 42, 44, 255), r=1)
    return b.done()


def make_fader_cap(col):
    """A fader cap, 30 x 16: a ridged block with a light centre line."""
    w, h = 30, 16
    m = rrect((w, h), (1, 1, w - 1, h - 3), 3)
    sh = np.roll(A.blur(m, 1.4), 2, 0)
    img = A.to_img(np.zeros((h, w, 3), np.float32), sh * 0.7)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32)
    rgb = np.array(col, np.float32) + (A.shade(m, 1.5) * 60)[..., None] - (np.clip((yy - 4) / 9, 0, 1) * 30)[..., None]
    img.alpha_composite(A.to_img(rgb, m))
    d = ImageDraw.Draw(img)
    d.line([(4, 6), (w - 5, 6)], fill=(24, 24, 24, 220), width=1)
    d.line([(4, 7), (w - 5, 7)], fill=(236, 232, 224, 200), width=1)
    return img


def make_jog():
    """The TUNER JOG wheel, 112 x 112 (centre 56, 56; r 52): a ribbed rubber rim, a brushed metal face, a finger dimple
    at the top. Drawn rotated in the C#."""
    n, c, r = 112, 56, JOG_R
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    rr = np.hypot(xx - c, yy - c)
    ang = np.arctan2(yy - c, xx - c)
    disc = A.mask((n, n), lambda d, s: d.ellipse([(c - r) * s, (c - r) * s, (c + r) * s, (c + r) * s], fill=255))
    face = A.mask((n, n), lambda d, s: d.ellipse([(c - r + 9) * s, (c - r + 9) * s, (c + r - 9) * s, (c + r - 9) * s], fill=255))
    rim = np.array((30, 30, 32), np.float32) + (np.cos(ang * 60) * 6)[..., None] + (A.shade(disc, 2.5) * 40)[..., None]
    img = A.to_img(rim, disc)
    metal = np.array((128, 130, 132), np.float32) + (np.cos(ang * 2 + 0.8) * 26)[..., None] + (np.sin(rr * 1.7) * 3)[..., None] + A.grain(n, n, 420, 3)[..., None]
    metal -= (A.shade(face, 2, (1, 1)) * 34)[..., None]
    img.alpha_composite(A.to_img(metal, face))
    dimple = A.mask((n, n), lambda d, s: d.ellipse([(c - 9) * s, (c - r + 15) * s, (c + 9) * s, (c - r + 33) * s], fill=255))
    img.alpha_composite(A.to_img(np.array((60, 62, 64), np.float32) + (A.shade(dimple, 2, (1, 1)) * 60)[..., None], dimple))
    hub = A.mask((n, n), lambda d, s: d.ellipse([(c - 7) * s, (c - 7) * s, (c + 7) * s, (c + 7) * s], fill=255))
    img.alpha_composite(A.to_img(np.array((70, 72, 74), np.float32) + (A.shade(hub, 1.5) * 50)[..., None], hub))
    return img


def make_disc(n, col, ring=None):
    b = S.Big(n, n)
    if ring:
        b.ellipse((0, 0, n - 1, n - 1), ring + (255,))
    k = 2 if ring else 0
    b.ellipse((k, k, n - 1 - k, n - 1 - k), col + (255,))
    b.ellipse((n * 0.28, n * 0.2, n * 0.5, n * 0.4), (255, 255, 255, 110))
    return b.done()


def make_plate():
    """The model plate with a speaker grille, 240 x 46."""
    w, h = 240, 46
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    b = S.Big(w, h)
    txt = "B D   S T U D I O   D E C K   ·   M O D E L   4"
    b.text((w / 2, 7), txt, A.font, 7, INK + (200,))
    for i in range(46):
        x = 8 + i * 5
        b.rect((x, 18, x + 2, h - 4), (14, 15, 15, 255), r=1)
    img.alpha_composite(b.done())
    return img


def make_panel():
    """An instrument panel inset into the desk, 9-slice 40 x 40 (slice 12): a darker plate with a lit edge and a small
    screw in every corner."""
    n = 40
    m = rrect((n, n), (0, 0, n, n), 6)
    rgb = np.array((36, 30, 46), np.float32) + A.grain(n, n, 440, 2)[..., None] + (A.shade(m, 1.2) * 34)[..., None]
    img = A.to_img(rgb, m)
    for x, y in ((6, 6), (n - 6, 6), (6, n - 6), (n - 6, n - 6)):
        b = S.Big(n, n)
        b.ellipse((x - 2.5, y - 2.5, x + 2.5, y + 2.5), (120, 112, 130, 255))
        b.line([(x - 1.6, y), (x + 1.6, y)], (40, 34, 48, 255), 1)
        img.alpha_composite(b.done())
    return img


def make_small_knob():
    """A small trim knob, 30 x 30 (centre 15, 15) on a printed tick ring."""
    n, c = 30, 15
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    b = S.Big(n, n)
    for i in range(9):
        a = np.radians(135 + 270 * i / 8)
        b.line([(c + np.cos(a) * 11.5, c + np.sin(a) * 11.5), (c + np.cos(a) * 14, c + np.sin(a) * 14)], INK + (180,), 1)
    b.ellipse((c - 10, c - 9, c + 10, c + 11), (10, 8, 14, 150))
    b.ellipse((c - 9, c - 9, c + 9, c + 9), (44, 40, 50, 255))
    b.ellipse((c - 7, c - 7.5, c + 7, c + 6.5), (70, 66, 78, 255))
    b.ellipse((c - 4, c - 5, c + 3, c + 1), (110, 106, 118, 255))
    a = np.radians(135 + 270 * 0.6)
    b.line([(c + np.cos(a) * 2, c + np.sin(a) * 2), (c + np.cos(a) * 7, c + np.sin(a) * 7)], (236, 230, 220, 255), 2)
    img.alpha_composite(b.done())
    return img


def make_note():
    """A sticky note on the monitor, 46 x 40, slightly turned: a reminder in pen."""
    w, h = 46, 40
    b = S.Big(w, h)
    b.rect((2, 3, w - 2, h - 1), (0, 0, 0, 90), r=1)
    b.rect((0, 0, w - 3, h - 4), (226, 196, 120, 255), r=1)
    b.rect((0, 0, w - 3, 6), (210, 178, 100, 255), r=1)
    pen = lambda sz: A.font(sz, False)
    b.text((21, 15), "BD-RAW 07", pen, 7, (52, 40, 90, 235))
    b.text((21, 25), "DON'T", pen, 7, (52, 40, 90, 235))
    b.text((21, 32), "ERASE", pen, 7, (150, 40, 60, 235))
    return A.spray(b.done(), 460, 0.97).rotate(-5, expand=True, resample=Image.BICUBIC)


def make_vent():
    """Vent slits on the monitor's top strip, 84 x 18."""
    w, h = 84, 18
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    b = S.Big(w, h)
    for i in range(10):
        x = 3 + i * 8
        b.rect((x + 1, 3, x + 4, h - 2), (90, 80, 104, 140), r=1.5)
        b.rect((x, 2, x + 3, h - 3), (12, 10, 16, 255), r=1.5)
    img.alpha_composite(b.done())
    return img


def make_badge():
    """The maker's badge under the screen, 64 x 12."""
    w, h = 64, 12
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (24, 20, 30, 255), r=3)
    b.rect((1, 1, w - 2, 2), (90, 80, 104, 255), r=1)
    b.text((w / 2, 6.5), "NR · BD-4", A.font, 7, ACCENT + (255,))
    return b.done()


PINK, CYAN, VIOLET = (236, 120, 200), (110, 226, 220), (150, 110, 230)


def chroma(layer, dx=1):
    """The VHS look: the layer split into a pink and a cyan ghost a pixel apart, the white core on top."""
    a = np.array(layer.getchannel("A"), np.float32) / 255
    h, w = a.shape
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.alpha_composite(A.to_img(np.broadcast_to(np.array(PINK, np.float32), (h, w, 3)).copy(), np.roll(a, -dx, 1) * 0.8))
    out.alpha_composite(A.to_img(np.broadcast_to(np.array(CYAN, np.float32), (h, w, 3)).copy(), np.roll(a, dx, 1) * 0.8))
    out.alpha_composite(layer)
    return out


def scanlines(img, k=0.28):
    a = np.array(img, np.float32)
    a[1::2, :, :3] *= 1 - k
    return Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")


def sticker_base(w, h, r, fill):
    """A die-cut sticker: a pale border, a soft shadow."""
    b = S.Big(w + 4, h + 4)
    b.rect((3, 4, w + 2, h + 3), (0, 0, 0, 90), r=r)
    b.rect((0, 0, w - 1, h - 1), (226, 220, 236, 255), r=r)
    b.rect((2, 2, w - 3, h - 3), fill + (255,), r=max(1, r - 2))
    return b.done()


def make_sun():
    """A VHS sunset sticker, 62 x 62: a round sun in pink and cyan cut by dark stripes, scanlines."""
    n = 62
    img = Image.new("RGBA", (n + 4, n + 4), (0, 0, 0, 0))
    b = S.Big(n + 4, n + 4)
    b.ellipse((3, 4, n + 2, n + 3), (0, 0, 0, 90))
    b.ellipse((0, 0, n - 1, n - 1), (226, 220, 236, 255))
    b.ellipse((2, 2, n - 3, n - 3), (24, 14, 40, 255))
    img.alpha_composite(b.done())
    yy, xx = np.mgrid[0:n + 4, 0:n + 4].astype(np.float32)
    c = (n - 1) / 2
    disc = (np.hypot(xx - c, yy - c - 4) < 20).astype(np.float32)
    t = np.clip((yy - 10) / 40, 0, 1)[..., None]
    rgb = np.array(CYAN, np.float32) * (1 - t) + np.array(PINK, np.float32) * t
    stripes = np.ones_like(disc)
    for k, y0 in enumerate(range(36, 56, 4)):
        stripes[y0:y0 + 1 + k // 2, :] = 0
    img.alpha_composite(A.to_img(rgb, disc * stripes))
    d = ImageDraw.Draw(img)
    d.text((c, 14), "VHS", font=A.font(8), fill=(236, 230, 250, 255), anchor="mm")
    return scanlines(img, 0.18)


def make_phones():
    """Pixel-art headphones sticker for the headphone jack, about 30 x 28 (22 x 20 pixel art, a die-cut edge)."""
    w, h = 22, 20
    px = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(px)
    L, M, D, O = (196, 186, 240, 255), (150, 130, 220, 255), (96, 76, 170, 255), (30, 20, 50, 255)
    d.arc((3, 1, 19, 17), 180, 360, fill=O, width=4)
    d.arc((4, 2, 18, 16), 180, 360, fill=M, width=2)
    d.point([(6, 4), (8, 3), (10, 2)], fill=L)
    for x0 in (1, 14):
        d.rounded_rectangle((x0, 9, x0 + 7, 18), 3, fill=O)
        d.rounded_rectangle((x0 + 1, 10, x0 + 6, 17), 2, fill=M)
        d.rectangle((x0 + 2, 11, x0 + 3, 13), fill=L)
        d.rectangle((x0 + 4, 15, x0 + 5, 16), fill=D)
    big = px
    out = Image.new("RGBA", (w + 6, h + 6), (0, 0, 0, 0))
    a = np.array(big.getchannel("A"), np.float32) / 255
    a = np.pad(a, 3)
    edge = A.blur((A.blur(a, 1.5) > 0.05).astype(np.float32), 0.4)
    out.alpha_composite(A.to_img(np.full(a.shape + (3,), 226, np.float32), edge))
    out.alpha_composite(big, (3, 3))
    return out.rotate(6, expand=True, resample=Image.BICUBIC)


def make_mic():
    """Pixel-art microphone sticker for the mic jack, about 18 x 26: a pink capsule in a U holder on a stand."""
    w, h = 12, 20
    px = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(px)
    L, M, D, O = (250, 196, 230, 255), (236, 120, 200, 255), (170, 60, 140, 255), (40, 16, 40, 255)
    d.rounded_rectangle((3, 0, 8, 10), 2, fill=O)
    d.rounded_rectangle((4, 1, 7, 9), 1, fill=M)
    d.point([(4, 2), (4, 3), (4, 4)], fill=L)
    d.line([(5, 6), (6, 6)], fill=D)
    d.line([(5, 8), (6, 8)], fill=D)
    d.arc((0, 3, 11, 14), 0, 180, fill=O, width=2)
    d.rectangle((5, 14, 6, 17), fill=O)
    d.rectangle((2, 17, 9, 19), fill=O)
    d.rectangle((3, 18, 8, 18), fill=D)
    out = Image.new("RGBA", (w + 6, h + 6), (0, 0, 0, 0))
    a = np.pad(np.array(px.getchannel("A"), np.float32) / 255, 3)
    edge = A.blur((A.blur(a, 1.5) > 0.05).astype(np.float32), 0.4)
    out.alpha_composite(A.to_img(np.full(a.shape + (3,), 226, np.float32), edge))
    out.alpha_composite(px, (3, 3))
    return out.rotate(-5, expand=True, resample=Image.BICUBIC)


def make_jack(ring):
    """A 3.5 mm AUX socket, 22 x 22: a hex nut, a coloured collar (green for headphones, pink for the mic, the old PC
    colours) and a dark hole."""
    n, c = 22, 11
    b = S.Big(n, n)
    b.ellipse((c - 10, c - 9, c + 10, c + 11), (8, 6, 12, 140))
    hexa = [(c + 9.5 * np.cos(np.radians(30 + 60 * i)), c + 9.5 * np.sin(np.radians(30 + 60 * i))) for i in range(6)]
    b.poly(hexa, (128, 122, 136, 255))
    b.poly([(x * 0.86 + c * 0.14, y * 0.86 + c * 0.14 + 0.6) for x, y in hexa], (92, 86, 102, 255))
    b.ellipse((c - 6.5, c - 6.5, c + 6.5, c + 6.5), ring + (255,))
    b.ellipse((c - 5, c - 5.5, c + 5, c + 4.5), tuple(min(255, v + 50) for v in ring) + (255,))
    b.ellipse((c - 4.2, c - 4.2, c + 4.2, c + 4.2), (40, 36, 46, 255))
    b.ellipse((c - 2.6, c - 2.6, c + 2.6, c + 2.6), (6, 4, 8, 255))
    b.ellipse((c - 3.6, c - 4, c - 1.4, c - 2.4), (170, 166, 178, 200))
    return b.done()


def make_reel():
    """A round cassette-reel sticker, 32 x 32: a white hub with six teeth on violet, a pink ring."""
    n = 30
    b = S.Big(n + 4, n + 4)
    c = (n - 1) / 2
    b.ellipse((2, 3, n + 1, n + 2), (0, 0, 0, 90))
    b.ellipse((0, 0, n - 1, n - 1), (226, 220, 236, 255))
    b.ellipse((2, 2, n - 3, n - 3), PINK + (255,))
    b.ellipse((4, 4, n - 5, n - 5), (40, 22, 62, 255))
    b.ellipse((c - 8, c - 8, c + 8, c + 8), (226, 220, 236, 255))
    b.ellipse((c - 4.5, c - 4.5, c + 4.5, c + 4.5), (40, 22, 62, 255))
    for i in range(6):
        a = np.radians(60 * i)
        b.line([(c + np.cos(a) * 3, c + np.sin(a) * 3), (c + np.cos(a) * 5.5, c + np.sin(a) * 5.5)], (226, 220, 236, 255), 2)
    for i in range(3):
        a = np.radians(120 * i + 30)
        b.ellipse((c + np.cos(a) * 10.5 - 1.5, c + np.sin(a) * 10.5 - 1.5, c + np.cos(a) * 10.5 + 1.5, c + np.sin(a) * 10.5 + 1.5), CYAN + (255,))
    return scanlines(b.done(), 0.15).rotate(10, expand=True, resample=Image.BICUBIC)


def make_heart():
    """A round sticker, 30 x 30: a pink pixel heart on cyan, a pale die-cut edge."""
    n = 28
    img = Image.new("RGBA", (n + 4, n + 4), (0, 0, 0, 0))
    b = S.Big(n + 4, n + 4)
    b.ellipse((2, 3, n + 1, n + 2), (0, 0, 0, 90))
    b.ellipse((0, 0, n - 1, n - 1), (226, 220, 236, 255))
    b.ellipse((2, 2, n - 3, n - 3), (60, 170, 176, 255))
    img.alpha_composite(b.done())
    heart = ["0110110", "1111111", "1111111", "0111110", "0011100", "0001000"]
    px = Image.new("RGBA", (7, 6), (0, 0, 0, 0))
    for y, row in enumerate(heart):
        for x, v in enumerate(row):
            if v == "1":
                px.putpixel((x, y), PINK + (255,))
    px.putpixel((1, 1), (250, 220, 240, 255))
    big = px.resize((14, 12), Image.NEAREST)
    img.alpha_composite(big, (int((n - 14) / 2), int((n - 12) / 2) + 1))
    return img.rotate(-8, expand=True, resample=Image.BICUBIC)


SCOPE_W, SCOPE_H, SCOPE_BARS = 196, 84, 16


def make_scope():
    """The spectrum screen on the desk, 196 x 84: dark violet glass, a faint grid, a vignette (the bars are drawn in
    code on top), and its glass overlay: a sheen and scanlines."""
    w, h = SCOPE_W, SCOPE_H
    m = rrect((w, h), (0, 0, w, h), 6)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    v = np.clip(1 - (((xx - w / 2) / (w * 0.62)) ** 2 + ((yy - h / 2) / (h * 0.7)) ** 2), 0, 1)
    rgb = np.array((10, 6, 20), np.float32) + v[..., None] * np.array((22, 12, 36), np.float32)
    grid = np.zeros((h, w), np.float32)
    grid[::12, :] = 1
    grid[:, ::14] = 1
    rgb += grid[..., None] * np.array((34, 22, 58), np.float32) * (0.4 + 0.6 * v[..., None])
    rgb += A.grain(w, h, 470, 2)[..., None]
    screen = A.to_img(rgb, m)
    # glass: a soft diagonal sheen at the top-left, darker scanlines, an inner rim
    sheen = np.clip(1 - (xx + yy * 1.8) / (w * 0.55), 0, 1) ** 2 * 0.16
    rgb2 = np.full((h, w, 3), 255, np.float32)
    glass = A.to_img(rgb2, sheen * m)
    lines = np.zeros((h, w), np.float32)
    lines[1::2, :] = 0.22
    glass.alpha_composite(A.to_img(np.zeros((h, w, 3), np.float32), lines * m))
    glass.alpha_composite(A.to_img(np.zeros((h, w, 3), np.float32), A.inner_edge(m, 2) * 0.6))
    return screen, glass


def make_bdlogo():
    """A glitched "BD VIDEO" logo sticker, 76 x 40: our own mark in the style of the old disc logos, violet with
    scanlines and a chromatic split."""
    w, h = 76, 40
    base = sticker_base(w, h, 4, (10, 6, 16))
    lay = Image.new("RGBA", (w + 4, h + 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    d.text((w / 2, 13), "BD", font=A.font(17), fill=(180, 140, 255, 255), anchor="mm")
    d.ellipse((12, 23, w - 12, 30), fill=(180, 140, 255, 255))
    d.ellipse((w / 2 - 5, 25, w / 2 + 5, 28), fill=(10, 6, 16, 255))
    d.text((w / 2, 35), "V I D E O", font=A.font(6), fill=(180, 140, 255, 255), anchor="mm")
    base.alpha_composite(chroma(lay))
    return scanlines(base, 0.35).rotate(-6, expand=True, resample=Image.BICUBIC)


def make_play():
    """A VHS on-screen-display label, 132 x 22: a play triangle, PLAY, SLP and STEREO in pink on a dark strip."""
    w, h = 132, 22
    base = sticker_base(w, h, 2, (16, 10, 26))
    lay = Image.new("RGBA", (w + 4, h + 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    d.polygon([(8, 6), (8, 16), (16, 11)], fill=PINK + (255,))
    d.text((20, 11), "PLAY", font=A.font(9), fill=PINK + (255,), anchor="lm")
    d.text((64, 11), "SLP", font=A.font(9), fill=CYAN + (255,), anchor="lm")
    d.text((92, 11), "STEREO", font=A.font(8), fill=(220, 210, 240, 255), anchor="lm")
    base.alpha_composite(chroma(lay))
    return scanlines(base, 0.3)


def make_jp():
    """A vertical Japanese sticker for the monitor's side, 18 x 92: 夢を記録 ("recording dreams") in pink on violet."""
    w, h = 18, 92
    base = sticker_base(w, h, 2, (40, 22, 62))
    d = ImageDraw.Draw(base)
    for i, ch in enumerate("夢を記録"):
        d.text((w / 2 + 1, 13 + i * 19), ch, font=S.jfont(12), fill=PINK + (255,), anchor="mm")
    d.rectangle((5, h - 12, w - 6, h - 9), fill=CYAN + (255,))
    return scanlines(base, 0.2)


def make_rating():
    """A round rating sticker, 36 x 36: BD 18+ in white on pink, a cyan ring."""
    n = 34
    b = S.Big(n + 4, n + 4)
    b.ellipse((2, 3, n + 1, n + 2), (0, 0, 0, 90))
    b.ellipse((0, 0, n - 1, n - 1), (226, 220, 236, 255))
    b.ellipse((2, 2, n - 3, n - 3), CYAN + (255,))
    b.ellipse((5, 5, n - 6, n - 6), (176, 60, 140, 255))
    b.text((n / 2, 12.5), "BD", A.font, 9, (250, 240, 250, 255))
    b.text((n / 2, 22.5), "18+", A.font, 9, (250, 240, 250, 255))
    return b.done().rotate(-12, expand=True, resample=Image.BICUBIC)


def make_side():
    """A cassette-style label for the bezel under the screen, 112 x 12: TAPE 07 / SIDE A in pen on cream."""
    w, h = 112, 12
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (222, 214, 196, 255), r=1)
    b.rect((0, 0, 5, h - 1), PINK + (255,))
    b.rect((w - 6, 0, w - 1, h - 1), CYAN + (255,))
    b.text((w / 2, 6.5), "TAPE 07  ·  SIDE A", lambda sz: A.font(sz, False), 8, (52, 40, 90, 235))
    return b.done()


def make_warn():
    """A vertical warning sticker for the monitor's right side, 18 x 74: NO MAGNETS in black on yellow."""
    w, h = 18, 74
    base = sticker_base(w, h, 2, (230, 196, 60))
    lay = Image.new("RGBA", (h, w), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    d.text((h / 2, w / 2), "NO MAGNETS", font=A.font(8), fill=(30, 24, 20, 255), anchor="mm")
    base.alpha_composite(lay.rotate(90, expand=True), (2, 2))
    d = ImageDraw.Draw(base)
    for y in (4, h - 6):
        for k in range(4):
            d.line([(3 + k * 4, y + 3), (6 + k * 4, y)], fill=(30, 24, 20, 255), width=2)
    return base


def build():
    A.save(make_body(), T, "Body")
    A.save(make_meter(), T, "Meter")
    A.save(make_bay(), T, "Bay")
    A.save(make_wave(), T, "Wave")
    for name, cap in (("Grey", (122, 116, 104)), ("Red", (150, 60, 52)), ("Amber", (176, 136, 64))):
        A.save(make_key(cap), T, "Key_" + name)
    A.save(make_lcd(), T, "Lcd")
    A.save(make_fader_slot(), T, "FaderSlot")
    A.save(make_fader_cap((150, 150, 146)), T, "FaderCap")
    A.save(make_fader_cap((176, 120, 162)), T, "FaderCapPink")
    A.save(make_jog(), T, "Jog")
    A.save(make_disc(14, (52, 52, 54), (14, 14, 15)), T, "LedBezel")
    A.save(make_disc(10, (255, 255, 255)), T, "Glass")
    A.save(make_plate(), T, "Plate")
    A.save(make_panel(), T, "Panel")
    A.save(make_small_knob(), T, "KnobSmall")
    A.save(make_note(), T, "Note")
    A.save(make_vent(), T, "Vent")
    A.save(make_badge(), T, "Badge")
    A.save(make_sun(), T, "Sticker_Sun")
    A.save(make_phones(), T, "Sticker_Phones")
    A.save(make_mic(), T, "Sticker_Mic")
    A.save(make_jack((70, 170, 80)), T, "Jack_Green")
    A.save(make_jack((200, 90, 160)), T, "Jack_Pink")
    A.save(make_reel(), T, "Sticker_Reel")
    A.save(make_heart(), T, "Sticker_Heart")
    scope, glass = make_scope()
    A.save(scope, T, "Scope")
    A.save(glass, T, "ScopeGlass")
    A.save(make_bdlogo(), T, "Sticker_BdLogo")
    A.save(make_play(), T, "Sticker_Play")
    A.save(make_jp(), T, "Sticker_Jp")
    A.save(make_rating(), T, "Sticker_Rating")
    A.save(make_side(), T, "Sticker_Side")
    A.save(make_warn(), T, "Sticker_Warn")


# ------------------------------------------------------------------ mockup (the same layout as DeckStudio.cs)

def load(name):
    return Image.open(os.path.join(A.FRAME, T, name + ".png")).convert("RGBA")


def mock(heat=58, screen=(1000, 666), online=True, tuner=0.35, rf=0.7):
    out = 4
    bw, bh = screen[0] + IN_L + IN_R, screen[1] + IN_T + IN_B
    img = Image.new("RGBA", (bw + 2 * out, bh + 2 * out), (14, 12, 16, 255))
    bx, by = out, out
    xm, ym = bx + bw, by + bh
    body = A.nine_slice(load("Body"), (bw, bh), 170, 120, 170, 200)
    img.alpha_composite(body, (bx, by))
    sx, sy = bx + IN_L, by + IN_T
    d = ImageDraw.Draw(img)
    d.rectangle([sx, sy, sx + screen[0] - 1, sy + screen[1] - 1], fill=(6, 12, 9, 255))
    for i, ln in enumerate(["NAVIGATION // CORE", "> HOME NODE ........ ONLINE", "> GATEWAY ........... 5 NODES", "> HEAT 58 ........... TRACKED"]):
        d.text((sx + 30, sy + 30 + i * 22), ln, font=A.font(13, False), fill=(93, 255, 138, 255) if i == 0 else (200, 200, 120, 255))
    level = 0 if heat < 20 else 1 if heat < 45 else 2 if heat < 70 else 3 if heat < 90 else 4

    def put(name, x, y, rot=0.0, pivot=None):
        s = load(name)
        if rot:
            s = s.rotate(-rot, resample=Image.BICUBIC, center=pivot)
        img.alpha_composite(s, (int(round(x)), int(round(y))))

    def cap(text, cx, y, col=INK + (230,), size=9, anchor="mt"):
        d.text((cx, y), text, font=A.font(size), fill=col, anchor=anchor)
        d.text((cx + 1, y), text, font=A.font(size), fill=col, anchor=anchor)

    # top strip: title, scrub bar, lamps, signal bars
    ty = by + 26
    cap("BRAINDANCE // STUDIO", bx + 52, ty - 6, ACCENT + (255,), 10, "lt")
    tl = (bx + 262, ty - 15, bx + 502, ty + 15)
    img.alpha_composite(A.nine_slice(load("Lcd"), (tl[2] - tl[0], tl[3] - tl[1]), 8, 8, 8, 8), tl[:2])
    d.text((tl[0] + 10, ty), "00:12:47:09", font=A.font(10, False), fill=(93, 255, 138, 255), anchor="lm")
    wv = load("Wave")
    ww = tl[2] - tl[0] - 96
    off = 70
    strip = Image.new("RGBA", (ww, 22))
    strip.alpha_composite(wv.crop((off, 0, min(256, off + ww), 22)), (0, 0))
    if off + ww > 256:
        strip.alpha_composite(wv.crop((0, 0, off + ww - 256, 22)), (256 - off, 0))
    img.alpha_composite(strip, (tl[0] + 88, ty - 11))
    ph = tl[0] + 88 + ww // 2
    d.line([(ph, ty - 13), (ph, ty + 13)], fill=(236, 120, 200, 255), width=1)
    d.polygon([(ph - 3, ty - 13), (ph + 3, ty - 13), (ph, ty - 9)], fill=(236, 120, 200, 255))
    put("Bay", tl[2] + 24, ty - 17)
    d.rectangle([tl[2] + 24 + 129, ty - 17 + 8, tl[2] + 24 + 133, ty - 17 + 12], fill=(93, 255, 138, 255))
    put("Note", bx + 200, by + 6)
    vent_x = tl[2] + 24 + 150 + 16 + 106 + 16
    if vent_x + 84 <= xm - 52 - 40 - 4 * 58 - 24:
        put("Vent", vent_x, ty - 9)
    put("Badge", (bx + xm) / 2 - 32, ym - IN_B + 2)
    play_x = vent_x + 84 + 20
    if play_x + 136 <= xm - 52 - 40 - 4 * 58 - 16:
        put("Sticker_Play", play_x, ty - 13)
    put("Sticker_Jp", bx + OVER + 4, by + IN_T + 40)
    put("Sticker_Warn", xm - OVER - 24, ym - IN_B - 110)
    put("Sticker_Side", bx + 120, ym - IN_B + 2)
    ph_x = tl[2] + 24 + 150 + 16
    if ph_x + 106 <= xm - 52 - 40 - 4 * 58 - 12:
        put("Sticker_Phones", ph_x, ty - 15)
        put("Jack_Green", ph_x + 32, ty - 11)
        put("Sticker_Mic", ph_x + 64, ty - 14)
        put("Jack_Pink", ph_x + 84, ty - 11)
    lamps = [("REC", (220, 60, 50), level >= 3), ("NET", (49, 196, 106), online), ("SICK", (255, 154, 74), True), ("POD", (49, 196, 106), False)]
    lx = xm - 52 - 40 - 4 * 58
    for i, (nm, c, on) in enumerate(lamps):
        x = lx + i * 58
        put("LedBezel", x - 7, ty - 7)
        g = load("Glass")
        tint = Image.new("RGBA", g.size, c + (255,) if on else tuple(int(v * 0.3) for v in c) + (255,))
        tint.putalpha(g.getchannel("A"))
        img.alpha_composite(tint, (x - 5, ty - 5))
        cap(nm, x + 12, ty - 5, INK + (210,), 9, "lt")
    bxs = xm - 52 - 30
    for i in range(5):
        hgt = 4 + i * 3
        d.rectangle([bxs + i * 6, ty + 6 - hgt, bxs + i * 6 + 3, ty + 6], fill=(49, 196, 106, 255) if i < round(rf * 5) else (30, 50, 36, 255))

    # the desk row
    yd = ym - DESK
    # Left to right: the LEVEL meter, the key panel, the SESSION LCD panel, GAIN / MIX; right to left: the jog, the
    # fader panel with tape (NET LINK and four channels); the model plate in the gap when it is wide enough.
    jx, jy = xm - JOG_DX, yd + JOG_DY

    def panel(x0, y0, x1, y1):
        img.alpha_composite(A.nine_slice(load("Panel"), (int(x1 - x0), int(y1 - y0)), 12, 12, 12, 12), (int(x0), int(y0)))

    FL = 196                                     # NET LINK fader from the box's right edge
    right = xm - FL - 10 - 4 * 46 - 30
    mx, my = bx + 66, yd + 8
    put("Meter", mx, my)
    put("Sticker_Rating", bx + 16, yd + 84)
    put("Sticker_Heart", bx + 27, yd + 10)
    put("Sticker_Reel", bx + 9, yd + 47)
    hl = round(heat / 100 * 12)
    for ci, cx0 in enumerate((14, 25, 47, 58)):
        n = hl if ci < 2 else round(rf * 12) - (ci - 2)
        for rr in range(n):
            c = (64, 200, 96) if rr < 7 else (228, 200, 56) if rr < 9 else (255, 140, 52) if rr < 11 else (236, 62, 50)
            y = my + 24 + (11 - rr) * 7
            d.rounded_rectangle([mx + cx0, y, mx + cx0 + 8, y + 4], 1, fill=c + (255,))
    rows = [[("CLOSE", "Key_Red", "x"), ("MENU", "Key_Grey", "="), ("ACHIEV", "Key_Grey", "*"), ("LOCK", "Key_Grey", "L")],
            [("BACK", "Key_Grey", "<"), ("CORE", "Key_Amber", "o"), ("HELP", "Key_Grey", "?")]]
    k0 = mx + 82 + 46
    panel(k0 - 30, yd + 10, k0 + 3 * 42 + 30, yd + 130)
    for r, row in enumerate(rows):
        for i, (nm, tex, g) in enumerate(row):
            kx, ky = k0 + i * 42, yd + 34 + r * 52
            put(tex, kx - 19, ky - 16)
            d.text((kx, ky - 3), g, font=A.font(12), fill=(40, 30, 22, 255) if tex != "Key_Red" else (240, 220, 200, 255), anchor="mm")
            cap(nm, kx, ky + 15)
    x = k0 + 3 * 42 + 44
    lcd = (x + 10, yd + 22, x + 210, yd + 84)
    if lcd[2] + 10 <= right:
        panel(x, yd + 10, x + 220, yd + 130)
        img.alpha_composite(A.nine_slice(load("Lcd"), (200, 62), 8, 8, 8, 8), lcd[:2])
        for i, ln in enumerate(["CYBERDATA 493", "HEAT 58 ^ TRACKED", "D21 06:15 NET ON"]):
            d.text((lcd[0] + 10, lcd[1] + 9 + i * 15), ln, font=A.font(10, False), fill=(93, 255, 138, 255))
        cap("SESSION", lcd[0] + 30, lcd[3] + 10)
        for k, nm in enumerate(("TONE", "PAN")):
            put("KnobSmall", lcd[2] - 105 + k * 52 - 15, yd + 92)
            cap(nm, lcd[2] - 105 + k * 52, yd + 118)
        x += 220
    put("Jog", jx - 56, jy - 56, 360 * tuner, (56, 56))
    cap("TUNER JOG", jx, yd + 122)
    panel(right + 8, yd + 6, xm - FL + 26, yd + 136)
    names = ["NET LINK", "EMOTION", "SOMATIC", "AUDIO", "VISUAL"]
    vals = [1.0 if online else 0.0, 0.3, 0.55, 0.7, 0.45]
    for i, nm in enumerate(names):
        fx = xm - FL - (0 if i == 0 else 10 + i * 46)
        put("FaderSlot", fx - 15, yd + 18)
        put("FaderCapPink" if i == 0 else "FaderCap", fx - 15, yd + 18 + 86 - 80 * vals[i] - 4)
        cap(nm, fx, yd + 120, ACCENT + (240,) if i == 0 else INK + (230,))
    gap = right - x
    if gap >= 150 + SCOPE_W + 36:
        sx = x + 150 + (gap - 150 - SCOPE_W - 20) / 2
        panel(sx - 10, yd + 10, sx + SCOPE_W + 10, yd + 130)
        put("Scope", sx, yd + 20)
        bw = SCOPE_W / SCOPE_BARS
        for i in range(SCOPE_BARS):
            lvl = 0.25 + 0.7 * abs(np.sin(i * 1.7 + 0.6)) * (1 - i / SCOPE_BARS * 0.5)
            n = int(lvl * 12)
            for r in range(n):
                t = r / 11
                c = tuple(int(CYAN[k] * (1 - t) + PINK[k] * t) for k in range(3))
                y = yd + 20 + SCOPE_H - 6 - (r + 1) * 6
                d.rectangle([sx + 4 + i * bw, y, sx + 4 + i * bw + bw - 4, y + 3], fill=c + (255,))
            d.rectangle([sx + 4 + i * bw, yd + 20 + SCOPE_H - 6 - (n + 2) * 6, sx + 4 + i * bw + bw - 4, yd + 20 + SCOPE_H - 6 - (n + 2) * 6 + 1], fill=(240, 230, 250, 255))
        put("ScopeGlass", sx, yd + 20)
        cap("SPECTRUM", sx + 34, yd + 112)
        cap("L  R", sx + SCOPE_W - 24, yd + 112, PINK + (230,))
    if gap >= 150:
        put("Sticker_Sun", x + 12, yd + 18)
        put("Sticker_BdLogo", x + 66, yd + 66)
    elif gap >= 76:
        put("Sticker_Sun", x + (gap - 66) / 2, yd + 30)
    return img


if __name__ == "__main__":
    build()
    if "--mock" in sys.argv:
        out = sys.argv[sys.argv.index("--mock") + 1]
        heat = int(sys.argv[sys.argv.index("--heat") + 1]) if "--heat" in sys.argv else 58
        m = mock(heat)
        m.save(out)
        wide = mock(heat, (1300, 700))
        wide.save(out.replace(".png", "-wide.png"))
        A.save(m.resize((300, int(300 * m.height / m.width)), Image.LANCZOS), T, "Preview")
    print("Studio textures written")
