#!/usr/bin/env python3
"""
Textures of the Iron Company deck theme (the Milspec theme), after Maxim's references (2026-10-15): a rugged field
tablet in olive drab paint with chipped edges, black rubber corner bumpers, tube handles on both sides, a riveted
nameplate, guarded toggle switches, rubber soft keys, jewel lamps and a lime backlit radio LCD.

    python tools/gen_deck_iron.py                 # textures into Textures/UI/Netrun/Frame/Iron/
    python tools/gen_deck_iron.py --mock OUT.png  # a composite at the default terminal size

The layout numbers below are mirrored in Source/Terminal/DeckIron.cs; change both together.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

import deck_art as A
import deck_stickers as S

T = "Iron"

BODY_W, BODY_H = 1400, 900
IN_L, IN_T, IN_R, IN_B = 46, 106, 46, 92
SLICE = 170
OUT_L, OUT_T, OUT_R, OUT_B = 34, 36, 34, 8
BEZEL = 14          # the raised frame around the screen

OD = A.hexc(0x4e5038)
OD_DARK = A.hexc(0x37392b)
METAL = A.hexc(0x8c8b80)
RUBBER = A.hexc(0x17181a)
STENCIL = (216, 210, 180)
LIME = A.hexc(0xffa22a)       # amber backlight (Maxim chose it from variant C)
LCD_INK = (48, 20, 0)
RF_COL = (255, 176, 60)
RED = (196, 34, 30)


def paint(w, h, seed, base=None):
    """Olive drab powder paint: mottled, grainy, with faint scuffs."""
    rgb = np.broadcast_to(OD if base is None else base, (h, w, 3)).copy()
    rgb += A.fbm(w, h, 60, 3, seed)[..., None] * 16
    rgb += A.fbm(w, h, 5, 2, seed + 1)[..., None] * 10
    rgb += A.grain(w, h, seed + 2, 5)[..., None]
    return rgb


def chips(w, h, seed, edge, amount=0.55):
    """Where the paint is chipped off to bare metal: along edges (edge mask 0..1) and a few random spots."""
    n = A.fbm(w, h, 9, 3, seed) + A.fbm(w, h, 3, 1, seed + 1) * 0.3
    return np.clip((n + edge * 0.32 * amount * 2 - 0.36) * 9, 0, 1)


def body_mask(W, H, s):
    def fn(d, ss):
        d.rounded_rectangle([0, 0, W * ss - 1, H * ss - 1], 26 * ss, fill=255)
    return A.mask((W, H), fn)


def make_body():
    W, H = BODY_W, BODY_H
    outer = body_mask(W, H, A.SS)
    hole = A.mask((W, H), lambda d, s: d.rounded_rectangle([IN_L * s, IN_T * s, (W - IN_R) * s - 1, (H - IN_B) * s - 1], 8 * s, fill=255))
    frame = A.mask((W, H), lambda d, s: d.rounded_rectangle([(IN_L - BEZEL) * s, (IN_T - BEZEL) * s, (W - IN_R + BEZEL) * s - 1, (H - IN_B + BEZEL) * s - 1], 16 * s, fill=255))

    rgb = paint(W, H, 801)
    # the raised bezel: darker paint, a strong bevel, a black gasket on its inside
    ring = frame * (1 - hole)
    brgb = paint(W, H, 802, OD_DARK) + (A.shade(frame, 2.0) * 70)[..., None]
    rgb = rgb * (1 - ring[..., None]) + brgb * ring[..., None]
    gasket = np.clip(A.blur(hole, 0.1) - 0, 0, 1)
    g = np.clip(A.inner_edge(1 - hole, 3.5), 0, 1) * (1 - hole)
    rgb = rgb * (1 - g[..., None]) + RUBBER * g[..., None]
    rgb -= (np.clip(A.blur(frame, 6) - frame, 0, 1) * 30)[..., None]   # the bezel's shadow on the case

    # the case edge: bevel, chipped paint along it
    edge = A.inner_edge(outer, 16)
    rgb += (A.shade(outer, 2.4) * 60)[..., None]
    ch = chips(W, H, 803, edge + A.inner_edge(ring, 4) * 0.6, 0.6) * outer
    metal = METAL + A.grain(W, H, 804, 8)[..., None]
    rgb = rgb * (1 - ch[..., None]) + metal * ch[..., None]
    rgb -= (A.blur(ch, 1.2) - ch).clip(0, 1)[..., None] * 40   # a dark rim around each chip

    img = A.to_img(rgb, outer * (1 - hole))

    # black rubber bumpers on the four corners, with grip ribs
    for cx, cy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        bw = 96
        bm = A.mask((bw, bw), lambda d, s: (d.rounded_rectangle([0, 0, bw * s - 1, 34 * s], 18 * s, fill=255),
                                            d.rounded_rectangle([0, 0, 34 * s, bw * s - 1], 18 * s, fill=255)))
        brgb = RUBBER + A.grain(bw, bw, 810 + cx * 2 + cy, 4)[..., None] + (A.shade(bm, 2.4) * 45)[..., None]
        bump = A.to_img(brgb, bm)
        d = ImageDraw.Draw(bump)
        for k in range(46, bw - 8, 9):
            d.line([k, 7, k, 27], fill=(8, 8, 9, 255), width=2)
            d.line([7, k, 27, k], fill=(8, 8, 9, 255), width=2)
        if cx:
            bump = bump.transpose(Image.FLIP_LEFT_RIGHT)
        if cy:
            bump = bump.transpose(Image.FLIP_TOP_BOTTOM)
        shadow = A.to_img(np.zeros((bw, bw, 3), np.float32), np.roll(A.blur(np.asarray(bump, np.float32)[..., 3] / 255, 3), 3, 0) * 0.6)
        x, y = (W - bw if cx else 0), (H - bw if cy else 0)
        A.over(img, shadow, (x, y))
        A.over(img, bump, (x, y))

    # hex screws in the bezel corners
    screw = make_screw()
    for x, y in ((IN_L - 7, IN_T - 7), (W - IN_R + 7, IN_T - 7), (IN_L - 7, H - IN_B + 7), (W - IN_R + 7, H - IN_B + 7)):
        A.over(img, screw, (x - screw.width // 2, y - screw.height // 2))
    return img


def make_screw(n=11):
    img = A.rivet(n, A.hexc(0x6a6a62), seed=831, dome=False)
    d = ImageDraw.Draw(img)
    c = img.width / 2
    r = n * 0.22
    pts = [(c + r * np.cos(a), c + r * np.sin(a)) for a in np.linspace(0, 2 * np.pi, 7)[:-1] + 0.3]
    d.polygon(pts, fill=(20, 20, 18, 255))
    return img


def make_handle():
    """A black powder-coated tube handle on two mounting blocks, 34 x 240, mounted to the left side."""
    W, H = 34, 240
    m = A.mask((W, H), lambda d, s: (d.rounded_rectangle([3 * s, 0, 15 * s, H * s - 1], 6 * s, fill=255),
                                     d.rounded_rectangle([3 * s, 14 * s, W * s - 1, 40 * s], 5 * s, fill=255),
                                     d.rounded_rectangle([3 * s, (H - 40) * s, W * s - 1, (H - 14) * s], 5 * s, fill=255)))
    rgb = RUBBER + 10 + A.grain(W, H, 841, 3)[..., None] + (A.shade(m, 2.5) * 70)[..., None]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    rgb += (np.clip(1 - np.abs(xx - 7) / 2.5, 0, 1) * 30 * (xx < 16))[..., None]   # a highlight along the tube
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 3), 3, 0) * 0.6)
    img.alpha_composite(A.to_img(rgb, m))
    screw = make_screw(7)
    for y in (27, H - 27):
        A.over(img, screw, (W - 14, y - screw.height // 2))
    return img


def make_plate():
    """The riveted steel nameplate: IRON COMPANY // EW-9 TACTICAL DECK."""
    W, H = 280, 46
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 4 * s, fill=255))
    rgb = A.hexc(0x6e6f68) + A.streaks(W, H, "x", 8, 851)[..., None] + (A.shade(m, 1.4) * 60)[..., None]
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 2), 2, 0) * 0.5)
    img.alpha_composite(A.to_img(rgb, m))
    for x in (8, W - 8):
        A.over(img, A.rivet(6, A.hexc(0x8a8a82), seed=852 + x), (x - 5, H / 2 - 5))
    f1, f2 = A.font(13), A.font(8, False)
    for txt, fnt, xy in (("IRON COMPANY // EW-9 TACTICAL DECK", f1, (22, 17)), ("SER. IC-884 · MIL-SPEC · NET-EW RATED", f2, (22, 34))):
        img.alpha_composite(A.text_layer((W, H), txt, fnt, (220, 220, 210, 60), (xy[0], xy[1] + 1), "lm"))
        img.alpha_composite(A.text_layer((W, H), txt, fnt, (24, 24, 22, 230), xy, "lm"))
    return img


def make_toggle(up, guard=None, guard_open=False):
    """A toggle switch on a square base, 40 x 56; guard: a flip cover (closed over the lever, or flipped up)."""
    W, H = 40, 56
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    base = A.mask((W, H), lambda d, s: d.rounded_rectangle([4 * s, 12 * s, 36 * s, 44 * s], 3 * s, fill=255))
    rgb = A.hexc(0x2a2c28) + A.grain(W, H, 861, 3)[..., None] + (A.shade(base, 1.4) * 50)[..., None]
    img.alpha_composite(A.to_img(np.zeros_like(rgb), np.roll(A.blur(base, 2), 2, 0) * 0.6))
    img.alpha_composite(A.to_img(rgb, base))
    b = S.Big(W, H)
    b.ellipse((13, 21, 27, 35), (150, 150, 144, 255), outline=(40, 40, 38, 255), width=1)   # the nut
    b.ellipse((16, 24, 24, 32), (60, 60, 56, 255))
    if guard and guard_open:
        b.rect((7, 0, 33, 12), guard + (255,), r=2, outline=(30, 10, 8, 255), width=1)       # the cover flipped up
    # the bat lever, chrome, pointing up or down
    y0, y1 = (28, 6) if up else (28, 50)
    b.line([(20, y0), (20, y1)], (200, 200, 196, 255), 4.5)
    b.ellipse((16.5, y1 - 3.5, 23.5, y1 + 3.5), (225, 225, 220, 255))
    if guard and not guard_open:
        b.rect((7, 9, 33, 47), guard + (235,), r=3, outline=(40, 10, 8, 255), width=1.2)    # the cover, closed
        b.line([(10, 13), (30, 13)], (255, 160, 150, 120), 1)
    img.alpha_composite(b.done())
    return img


def make_rubber_key(w=46, h=34):
    m = A.mask((w, h), lambda d, s: d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], 7 * s, fill=255))
    rgb = RUBBER + 8 + A.grain(w, h, 871, 3)[..., None] + (A.shade(m, 3.0) * 55)[..., None]
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32)
    rgb += (np.clip(1 - yy / (h * 0.5), 0, 1) * 10)[..., None]
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 2), 2, 0) * 0.7)
    img.alpha_composite(A.to_img(rgb, m))
    return img


def make_knob(n=40):
    m = A.mask((n, n), lambda d, s: d.ellipse([1 * s, 1 * s, (n - 1) * s - 1, (n - 1) * s - 1], fill=255))
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    ang = np.arctan2(yy - n / 2, xx - n / 2)
    r = np.hypot(xx - n / 2, yy - n / 2) / (n / 2)
    rgb = A.hexc(0x2a2a2a) + (np.sin(ang * 30) * 10 * (r > 0.78))[..., None]
    rgb += (np.clip(1 - np.hypot(xx - n * 0.38, yy - n * 0.34) / (n * 0.4), 0, 1) * 50)[..., None]
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 2), 2, 0) * 0.6)
    img.alpha_composite(A.to_img(rgb, m))
    ImageDraw.Draw(img).line([n / 2, n / 2 - 2, n / 2, 5], fill=(230, 230, 220, 255), width=2)
    return img


def make_lamp(color):
    """A jewel lamp in a chrome bezel, 18 px; color None for an unlit one."""
    n = 18
    b = S.Big(n, n)
    b.ellipse((0, 0, n - 1, n - 1), (150, 150, 146, 255), outline=(40, 40, 38, 255), width=1)
    glass = color if color else (60, 20, 18)
    b.ellipse((3, 3, n - 4, n - 4), glass + (255,))
    b.ellipse((5, 4.5, 9, 8), (255, 255, 255, 170 if color else 60))
    return b.done()


def make_lcd():
    """The lime backlit radio LCD, 9-sliced (borders 10)."""
    W, H = 120, 60
    outer = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 5 * s, fill=255))
    inner = A.mask((W, H), lambda d, s: d.rounded_rectangle([6 * s, 6 * s, (W - 6) * s - 1, (H - 6) * s - 1], 2 * s, fill=255))
    rgb = A.hexc(0x1e201b) + (A.shade(outer, 1.4) * 40)[..., None] + A.grain(W, H, 881, 3)[..., None]
    glass = LIME + (A.gradient_v(W, H, A.hexc(0x0c0c00), A.hexc(0x000000)) - 6) - (A.inner_edge(inner, 4) * 40)[..., None]
    rgb = rgb * (1 - inner[..., None]) + glass * inner[..., None]
    return A.to_img(rgb, outer)


def make_window():
    """A dark recessed window for the RF readout and the Heat bargraph, 9-sliced (borders 8)."""
    W, H = 80, 40
    outer = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 4 * s, fill=255))
    inner = A.mask((W, H), lambda d, s: d.rounded_rectangle([3 * s, 3 * s, (W - 3) * s - 1, (H - 3) * s - 1], 2 * s, fill=255))
    rgb = A.hexc(0x2a2d26) + (A.shade(outer, 1.2) * 40)[..., None]
    rgb = rgb * (1 - inner[..., None]) + (A.hexc(0x070806) - (A.inner_edge(inner, 4) * 0)[..., None]) * inner[..., None]
    return A.to_img(rgb, outer)


def make_hazard(w=12, h=46):
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (226, 180, 30, 255))
    for y in range(-w, h + w, 12):
        b.poly([(0, y + w), (0, y + w + 6), (w, y + 6), (w, y)], (20, 20, 18, 255))
    img = b.done()
    return A.spray(img, 891, 0.9)


def make_panel():
    """A recessed sub-panel (9-slice, borders 14) with a screw in each corner: the switch clusters sit on these."""
    W, H = 64, 64
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 6 * s, fill=255))
    rgb = paint(W, H, 901, OD_DARK * 0.92) - (A.inner_edge(m, 5) * 26)[..., None] + (A.shade(1 - m, 1.4, light=(1, 1)) * 30)[..., None]
    img = A.to_img(rgb, m)
    sc = make_screw(7)
    for x, y in ((7, 7), (W - 7, 7), (7, H - 7), (W - 7, H - 7)):
        A.over(img, sc, (x - sc.width // 2, y - sc.height // 2))
    return img


def make_antenna():
    """A rubber whip antenna stub on a threaded base, 22 x 62 (base at the bottom, on the top edge of the case; it has to
    fit in the 36 px the window keeps above the case)."""
    W, H = 22, 62
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    b = S.Big(W, H)
    b.rect((3, H - 22, W - 4, H - 1), (150, 150, 144, 255), r=2, outline=(40, 40, 38, 255), width=1)   # the nut
    for y in range(H - 20, H - 2, 3):
        b.line([(4, y), (W - 5, y)], (90, 90, 86, 255), 0.8)
    b.rect((5, H - 40, W - 6, H - 21), (28, 28, 28, 255), r=3)                                          # the boot
    for y in range(H - 38, H - 22, 4):
        b.line([(5, y), (W - 6, y)], (60, 60, 60, 255), 1)
    b.rect((8.5, 2, W - 9.5, H - 38), (22, 22, 22, 255), r=2)                                           # the whip
    b.line([(10, 6), (10, H - 42)], (80, 80, 80, 255), 0.8)
    b.ellipse((7, 0, W - 8, 6), (36, 36, 36, 255))
    img.alpha_composite(b.done())
    return img


def make_connector(cap=True):
    """A round military connector, 30 x 30: knurled coupling ring, a dust cap on a short chain or open pins."""
    n = 30
    m = A.mask((n, n), lambda d, s: d.ellipse([1 * s, 1 * s, (n - 1) * s - 1, (n - 1) * s - 1], fill=255))
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    ang = np.arctan2(yy - n / 2, xx - n / 2)
    r = np.hypot(xx - n / 2, yy - n / 2) / (n / 2)
    rgb = A.hexc(0x5c5e58) + (np.sin(ang * 36) * 14 * (r > 0.75))[..., None] + (A.shade(m, 1.4) * 60)[..., None]
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 2), 2, 0) * 0.6)
    img.alpha_composite(A.to_img(rgb, m))
    b = S.Big(n, n)
    if cap:
        b.ellipse((6, 6, n - 7, n - 7), (40, 42, 36, 255), outline=(20, 20, 18, 255), width=1)
        b.ellipse((10, 9, 15, 13), (255, 255, 255, 50))
    else:
        b.ellipse((6, 6, n - 7, n - 7), (14, 14, 12, 255))
        for a in np.linspace(0, 2 * np.pi, 7)[:-1]:
            x, y = n / 2 + 5.5 * np.cos(a), n / 2 + 5.5 * np.sin(a)
            b.ellipse((x - 1.2, y - 1.2, x + 1.2, y + 1.2), (200, 170, 90, 255))
        b.ellipse((n / 2 - 1.2, n / 2 - 1.2, n / 2 + 1.2, n / 2 + 1.2), (200, 170, 90, 255))
    img.alpha_composite(b.done())
    return img


def make_vent(w=16, h=110):
    """Vent slots cut into the rail."""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y in range(4, h - 6, 8):
        sm = A.mask((w, 7), lambda d, s: d.rounded_rectangle([1 * s, 1 * s, (w - 1) * s - 1, 5 * s], 2.5 * s, fill=255))
        rgb = np.full((7, w, 3), 8, np.float32) + (A.shade(1 - sm, 1.0, light=(1, 1)) * 40)[..., None]
        img.alpha_composite(A.to_img(rgb, sm), dest=(0, y))
    return img


def make_dataplate():
    """A small riveted aluminium data plate, vertical, 24 x 120, with tiny engraved lines."""
    W, H = 24, 120
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 3 * s, fill=255))
    rgb = A.hexc(0xa3a49c) + A.streaks(W, H, "y", 8, 911)[..., None] + (A.shade(m, 1.2) * 50)[..., None]
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 1.5), 2, 0) * 0.5)
    img.alpha_composite(A.to_img(rgb, m))
    t = Image.new("RGBA", (H - 24, W), (0, 0, 0, 0))
    td = ImageDraw.Draw(t)
    td.text((2, 6), "NSN 5895-01-884-0007", font=A.font(7, False), fill=(30, 30, 28, 220), anchor="lm")
    td.text((2, 16), "LOT 07 · IRON CO.", font=A.font(7, False), fill=(30, 30, 28, 220), anchor="lm")
    img.alpha_composite(t.rotate(90, expand=True), dest=(0, 12))
    for y in (6, H - 6):
        A.over(img, A.rivet(5, A.hexc(0xc0c0b8), seed=912 + y), (W / 2 - 4.5, y - 4.5))
    return img


def make_sticker():
    """A worn yellow QC sticker with a barcode, slightly crooked."""
    W, H = 70, 30
    b = S.Big(W, H)
    b.rect((0, 0, W - 1, H - 1), (232, 196, 52, 255), r=2)
    b.text((5, 8), "INSPECTED", A.font, 7, (30, 26, 10, 255), anchor="lm")
    b.text((5, 16), "QC 07 · 0419", A.font, 6, (30, 26, 10, 255), anchor="lm")
    r = A.rng(921)
    x = 5
    while x < W - 6:
        bw = r.choice([0.8, 1.2, 2.0])
        b.rect((x, 20, x + bw, 27), (30, 26, 10, 255))
        x += bw + r.choice([0.8, 1.4])
    img = b.done()
    arr = np.asarray(img, np.float32).copy()
    arr[..., 3] *= 1 - chips(W, H, 922, A.inner_edge(arr[..., 3] / 255, 4), 0.9) * 0.9
    img = Image.fromarray(np.clip(arr, 0, 255).astype("uint8"), "RGBA").rotate(-4, expand=True, resample=Image.BICUBIC)
    return img


def make_warning():
    """A stencilled warning: a triangle and HIGH VOLTAGE, sprayed."""
    W, H = 96, 16
    b = S.Big(W, H)
    b.poly([(2, 14), (9, 2), (16, 14)], None, outline=STENCIL + (220,), width=1.4)
    b.text((9, 10.5), "!", A.font, 8, STENCIL + (220,))
    b.text((22, 8.5), "HIGH VOLTAGE", A.font, 8, STENCIL + (220,), anchor="lm")
    return A.spray(b.done(), 931, 0.85)


def build():
    A.save(make_body(), T, "Body")
    A.save(make_handle(), T, "Handle")
    A.save(make_plate(), T, "Plate")
    A.save(make_screw(), T, "Screw")
    for up in (0, 1):
        A.save(make_toggle(up), T, f"Toggle_{up}")
        A.save(make_toggle(up, RED, False), T, f"Guard_Closed_{up}")
        A.save(make_toggle(up, RED, True), T, f"Guard_Open_{up}")
    A.save(make_rubber_key(), T, "Key")
    A.save(make_knob(), T, "Knob")
    for name, col in (("Off", None), ("Green", (90, 230, 90)), ("Orange", (255, 150, 40)), ("Red", (240, 50, 40))):
        A.save(make_lamp(col), T, "Lamp_" + name)
    A.save(make_lcd(), T, "Lcd")
    A.save(make_window(), T, "Window")
    A.save(make_hazard(), T, "Hazard")
    A.save(make_panel(), T, "Panel")
    A.save(make_antenna(), T, "Antenna")
    A.save(make_connector(True), T, "Connector_Cap")
    A.save(make_connector(False), T, "Connector_Open")
    A.save(make_vent(), T, "Vent")
    A.save(make_dataplate(), T, "DataPlate")
    A.save(make_sticker(), T, "Sticker")
    A.save(make_warning(), T, "Warning")
    A.save(mock(None).resize((300, 240), Image.LANCZOS), T, "Preview")


# ------------------------------------------------------------------ a composite, the way DeckIron.cs lays it out

HEAT_SEG = [(49, 196, 106), (230, 196, 49), (255, 138, 60), (255, 74, 60), (179, 18, 42)]


def mono(size, bold=False):
    try:
        return ImageFont.truetype("consolab.ttf" if bold else "consola.ttf", size)
    except OSError:
        return A.font(size)


def stencil(d, xy, text, size=9, anchor="mm", fill=None):
    d.text(xy, text, font=A.font(size), fill=(fill or STENCIL) + (230,), anchor=anchor)


def mock(out_path=None, screen=(930, 666), heat=58, online=True):
    W = screen[0] + OUT_L + OUT_R + IN_L + IN_R
    H = screen[1] + OUT_T + OUT_B + IN_T + IN_B

    def load(n):
        return Image.open(os.path.join(A.FRAME, T, n + ".png")).convert("RGBA")

    def panel(x0, y0, x1, y1):
        A.over(img, A.nine_slice(load("Panel"), (int(x1 - x0), int(y1 - y0)), 14, 14, 14, 14), (x0, y0))
    img = Image.new("RGBA", (W, H), (34, 38, 30, 255))
    d = ImageDraw.Draw(img)
    bx0, by0, bx1, by1 = OUT_L, OUT_T, W - OUT_R, H - OUT_B
    # behind the case: the handles, the antenna, the connectors on the top edge
    hd = load("Handle")
    hy = (by0 + by1) / 2 - hd.height / 2
    A.over(img, hd, (bx0 - 30, hy))
    A.over(img, hd.transpose(Image.FLIP_LEFT_RIGHT), (bx1 + 30 - hd.width, hy))
    A.over(img, A.nine_slice(load("Body"), (bx1 - bx0, by1 - by0), SLICE, SLICE, SLICE, SLICE), (bx0, by0))
    ant = load("Antenna")   # screwed into the top-right bumper
    A.over(img, ant, (bx1 - 40, by0 + 26 - ant.height))
    sx0, sy0, sx1, sy1 = bx0 + IN_L, by0 + IN_T, bx1 - IN_R, by1 - IN_B
    # two round connectors at the top of the left rail
    for i, (cap, lab) in enumerate(((True, "DATA"), (False, "AUX"))):
        cy = sy0 + 8 + i * 46
        A.over(img, load("Connector_Cap" if cap else "Connector_Open"), (bx0 + 2, cy))
        stencil(d, (bx0 + 17, cy + 36), lab, 7)
    d.rounded_rectangle([sx0, sy0, sx1 - 1, sy1 - 1], 8, fill=(22, 26, 22, 255))
    f = mono(13)
    for i, (t, c) in enumerate((("NETRUN // NETWORK CORE", (90, 230, 130)), ("> HOME NODE ........ ONLINE", (90, 210, 130)),
                                ("> GATEWAY .......... 5 NODES", (90, 210, 130)), ("> HEAT 58 .......... TRACKED", (230, 190, 70)))):
        d.text((sx0 + 30, sy0 + 28 + i * 22), t, font=f, fill=c)
    for r in (60, 120):
        d.ellipse([sx1 - 230 - r, (sy0 + sy1) / 2 - r, sx1 - 230 + r, (sy0 + sy1) / 2 + r], outline=(40, 120, 70))

    # the side rails: screws down both, vents, a data plate on the right
    vent = load("Vent")
    lv = (bx0 + 9, (sy0 + sy1) / 2 - vent.height / 2 + 43)
    dp = (bx1 - 29, sy0 + 84)
    rv = (bx1 - 25, sy1 - 190)
    A.over(img, vent, lv)
    A.over(img, load("DataPlate"), dp)
    A.over(img, vent, rv)
    # screws down both rails, skipping whatever already sits there
    busy = {0: [(sy0, sy0 + 104), (lv[1], lv[1] + vent.height)], 1: [(dp[1], dp[1] + 120), (rv[1], rv[1] + vent.height)]}
    sc = load("Screw")
    for y in range(int(sy0 + 60), int(sy1 - 50), 86):
        for side, x in ((0, bx0 + 17), (1, bx1 - 17)):
            if all(y + 10 < a or y - 10 > b for a, b in busy[side]):
                A.over(img, sc, (x - sc.width // 2, y - sc.height // 2))

    # top: plate with its QC sticker, a panel with the switches and the knob, lamps, the MIL-RF window
    A.over(img, load("Plate"), (bx0 + 56, by0 + 38))
    A.over(img, load("Sticker"), (bx0 + 56 + 214, by0 + 66))
    rx = bx1 - 240          # the MIL-RF window; its panel ends well clear of the top-right bumper
    x = rx - 112 - 16 - 280
    panel(x - 12, by0 + 14, x + 52 + 112 + 64 + 52, by0 + 94)
    A.over(img, load("Guard_Closed_1"), (x, by0 + 22))
    stencil(d, (x + 20, by0 + 84), "CLOSE")
    x += 52
    for lab, g in (("MENU", "\u2261"), ("ACHIEV", "\u2605")):
        A.over(img, load("Key"), (x, by0 + 32))
        d.text((x + 23, by0 + 48), g, font=S.jfont(16), fill=(216, 210, 180), anchor="mm")
        stencil(d, (x + 23, by0 + 84), lab)
        x += 56
    A.over(img, load("Guard_Closed_0"), (x, by0 + 22))
    stencil(d, (x + 20, by0 + 84), "NET-LOCK")
    x += 60
    A.over(img, load("Knob"), (x, by0 + 30))
    for k in range(7):   # the dial scale around the knob
        a = np.radians(-135 + k * 45)
        cx, cy = x + 20, by0 + 50
        d.line([cx + 22 * np.sin(a), cy - 22 * np.cos(a), cx + 26 * np.sin(a), cy - 26 * np.cos(a)], fill=STENCIL + (200,), width=1)
    stencil(d, (x + 20, by0 + 84), "TUNE")
    panel(rx - 112, by0 + 14, rx + 132, by0 + 94)
    win = A.nine_slice(load("Window"), (120, 44), 8, 8, 8, 8)
    A.over(img, win, (rx, by0 + 30))
    d.text((rx + 9, by0 + 38), "MIL-RF", font=mono(9, True), fill=RF_COL)
    d.text((rx + 111, by0 + 38), "2.4 GHz", font=mono(9), fill=RF_COL, anchor="ra")
    for i in range(10):
        d.rectangle([rx + 9 + i * 10, by0 + 54, rx + 15 + i * 10, by0 + 64], fill=RF_COL if i < 7 else tuple(v // 4 for v in RF_COL))
    stencil(d, (rx + 60, by0 + 84), "LINK // LOCKED")
    lx = rx - 98
    for i, (lamp, lab) in enumerate((("Lamp_Green", "NET"), ("Lamp_Orange", "SICK"), ("Lamp_Off", "POD"))):
        if lamp != "Lamp_Off":
            A.glow(img, (lx + i * 30 + 4, by0 + 42, lx + i * 30 + 14, by0 + 52), (90, 230, 90) if i == 0 else (255, 150, 40), 0.45, 5)
        A.over(img, load(lamp), (lx + i * 30, by0 + 38))
        stencil(d, (lx + i * 30 + 9, by0 + 84), lab)

    # bottom: amber LCD, a panel with the soft keys and the Heat bargraph, NET LINK between hazard stripes
    yb = by1 - IN_B
    lcd = (bx0 + 100, yb + 10, 250, 56)
    A.over(img, A.nine_slice(load("Lcd"), lcd[2:], 10, 10, 10, 10), lcd[:2])
    for i, t in enumerate(("CYBERDATA 491", "HEAT 58 \u25b2 TRACKED", "D8 08:27  NET ON")):
        d.text((lcd[0] + 12, lcd[1] + 9 + i * 14), t, font=mono(11, True), fill=LCD_INK)
    kx = lcd[0] + lcd[2] + 34
    hw = 250
    panel(kx - 12, yb + 12, kx + 3 * 56 + 24 + hw + 12, yb + 86)
    for i, (g, lab) in enumerate((("\u25c1", "BACK"), ("\u25cf", "CORE"), ("?", "HELP"))):
        A.over(img, load("Key"), (kx + i * 56, yb + 22))
        d.text((kx + i * 56 + 23, yb + 38), g, font=S.jfont(15), fill=(240, 90, 70) if i == 1 else (216, 210, 180), anchor="mm")
        stencil(d, (kx + i * 56 + 23, yb + 72), lab)
    hx = kx + 3 * 56 + 24
    A.over(img, A.nine_slice(load("Window"), (hw, 34), 8, 8, 8, 8), (hx, yb + 24))
    lit = int(heat / 10 + 0.5)
    for i in range(10):
        c = HEAT_SEG[min(4, i // 2)]
        d.rectangle([hx + 8 + i * 23.4, yb + 31, hx + 8 + i * 23.4 + 18, yb + 50], fill=c if i < lit else tuple(v // 6 for v in c))
    stencil(d, (hx + hw / 2, yb + 72), "THREAT LEVEL")
    tx = bx1 - IN_R - 100
    A.over(img, load("Hazard"), (tx - 16, yb + 18))
    A.over(img, load("Hazard"), (tx + 44, yb + 18))
    A.over(img, load("Toggle_1" if online else "Toggle_0"), (tx, yb + 10))
    stencil(d, (tx + 62, yb + 22), "ON", 8, "lm")
    stencil(d, (tx + 62, yb + 54), "OFF", 8, "lm")
    stencil(d, (tx + 20, yb + 74), "NET LINK")
    A.over(img, load("Warning"), (lcd[0] + 4, yb + 70))
    if out_path:
        img.convert("RGB").save(out_path)
    return img


# Alternatives for Maxim to compare (2026-10-15): B desert tan like the field tablet, C gunmetal like the EW-9 reference.
VARIANTS = {
    "B": dict(T="Iron_B", OD=A.hexc(0x8f7f5c), OD_DARK=A.hexc(0x5e543e), RUBBER=A.hexc(0x2a2a28), STENCIL=(40, 36, 26),
              LIME=A.hexc(0xb9c6a2), LCD_INK=(30, 36, 24), RF_COL=(230, 200, 120), RED=(200, 40, 34)),
    "C": dict(T="Iron_C", OD=A.hexc(0x46484a), OD_DARK=A.hexc(0x2e3032), RUBBER=A.hexc(0x141414), STENCIL=(214, 214, 206),
              LIME=A.hexc(0xffa22a), LCD_INK=(48, 20, 0), RF_COL=(255, 176, 60), METAL=A.hexc(0xa6a8a6)),
}

if __name__ == "__main__":
    if len(sys.argv) > 2 and sys.argv[1] == "--variant":
        globals().update(VARIANTS[sys.argv[2]])
        del sys.argv[1:3]
    if len(sys.argv) > 2 and sys.argv[1] == "--mock":
        mock(sys.argv[2])
    else:
        build()
        print("Iron textures written")
