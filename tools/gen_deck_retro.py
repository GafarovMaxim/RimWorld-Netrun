#!/usr/bin/env python3
"""
Textures of the retro deck theme, three variants for Maxim to choose from (2026-10-15, his references in
G:/Modding/RimWorld/Hacking Expansion Netrun/Retro): 80s consumer electronics in yellowed plastic, sunset stripes,
stylised logo badges, a green-grey dot-matrix LCD, an analog VU meter for the Heat and a slide switch for NET LINK.

    A  Sunset   cream handheld plastic, rainbow sunset stripes, a NETRUN badge with katakana
    B  Studio   off-white hi-fi panel with orange and yellow colour blocks, a red sun badge
    C  Datawave brown and cream two-tone, vertical stripes on the left rail, a sunburst badge

    python tools/gen_deck_retro.py --variant A --mock OUT.png
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

import deck_art as A
import deck_stickers as S

BODY_W, BODY_H = 1400, 900
IN_L, IN_T, IN_R, IN_B = 64, 96, 64, 104
SLICE = 170
OUT = 8
BEZEL = 22

SUNSET = [(246, 196, 64), (240, 140, 48), (226, 82, 46), (178, 40, 52)]
INK = (40, 34, 30)
LCD = A.hexc(0x9aa776)
LCD_INK = (34, 48, 26)

V = {
    "A": dict(T="Retro_A", base=(222, 214, 192), dark=(64, 62, 70), stripes=SUNSET, accent=(150, 36, 70), key=(70, 68, 76)),
    "B": dict(T="Retro_B", base=(232, 228, 216), dark=(54, 54, 56), stripes=[(246, 186, 40), (232, 96, 36)], accent=(214, 50, 40), key=(54, 54, 56)),
    "D": dict(T="Retro_D", base=(222, 214, 192), dark=(64, 62, 70), stripes=[(250, 212, 70)] + SUNSET + [(120, 36, 70)], accent=(150, 36, 70), key=(196, 190, 174)),
    "C": dict(T="Retro_C", base=(92, 60, 42), dark=(40, 30, 24), stripes=[(88, 160, 200), (110, 180, 90), (246, 196, 64), (240, 120, 40), (214, 50, 40)],
              accent=(232, 214, 176), key=(70, 46, 32), cream=(232, 216, 180)),
}
P = V["A"]


def plastic(w, h, seed, base):
    rgb = np.broadcast_to(np.array(base, np.float32), (h, w, 3)).copy()
    rgb += A.fbm(w, h, 120, 3, seed)[..., None] * np.array([10, 9, 2], np.float32)   # yellowing, uneven
    rgb += A.fbm(w, h, 4, 1, seed + 1)[..., None] * 4                                  # fine texture of moulded plastic
    rgb += A.grain(w, h, seed + 2, 2)[..., None]
    return rgb


def make_body(v):
    W, H = BODY_W, BODY_H
    outer = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 30 * s, fill=255))
    hole = A.mask((W, H), lambda d, s: d.rounded_rectangle([IN_L * s, IN_T * s, (W - IN_R) * s - 1, (H - IN_B) * s - 1], 6 * s, fill=255))
    frame = A.mask((W, H), lambda d, s: d.rounded_rectangle([(IN_L - BEZEL) * s, (IN_T - BEZEL) * s, (W - IN_R + BEZEL) * s - 1, (H - IN_B + BEZEL) * s - 1],
                                                            (14 if v != "A" else 12) * s, fill=255))
    p = V[v]
    rgb = plastic(W, H, 1001, p["base"])
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    if v == "C":   # the cream top band of the two-tone case
        band = np.clip((IN_T - BEZEL - 14 - yy) * 0.8, 0, 1)
        rgb = rgb * (1 - band[..., None]) + plastic(W, H, 1002, p["cream"]) * band[..., None]
    if v == "B":   # orange and yellow blocks across the top bar
        blk = ((yy > 10) & (yy < IN_T - BEZEL - 10)).astype(np.float32)
        orange = ((xx > W * 0.45) & (xx < W - 40)).astype(np.float32) * blk
        yellow = ((xx > W * 0.30) & (xx <= W * 0.45)).astype(np.float32) * blk
        for m, c in ((A.blur(orange, 0.6), p["stripes"][1]), (A.blur(yellow, 0.6), p["stripes"][0])):
            rgb = rgb * (1 - m[..., None]) + plastic(W, H, 1003, c) * m[..., None]
    # the dark screen surround, a soft step down into it
    ring = frame * (1 - hole)
    rgb = rgb * (1 - ring[..., None]) + (plastic(W, H, 1004, p["dark"]) + (A.shade(frame, 1.6) * 40)[..., None]) * ring[..., None]
    rgb -= (np.clip(A.blur(frame, 4) - frame, 0, 1) * 16)[..., None]
    rgb += (A.shade(outer, 3.0) * 50)[..., None]
    rgb -= (A.inner_edge(outer, 1.2) * 60)[..., None]
    img = A.to_img(rgb, outer * (1 - hole))

    s = A.SS
    big = Image.new("RGBA", (W * s, H * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    st = p["stripes"]
    if v == "A":   # sunset stripes: along the top bar, sweeping down the bottom-left corner at 45 degrees
        for i, c in enumerate(st):
            y = 10 + i * 6
            d.line([((40) * s, y * s), ((W - 40) * s, y * s)], fill=c + (255,), width=4 * s)
        for i, c in enumerate(st):
            o = i * 9
            d.line([((0) * s, (H - 150 + o) * s), ((150 - o) * s, (H) * s)], fill=c + (255,), width=7 * s)
    if v == "C":   # vertical stripes down the left rail
        for i, c in enumerate(st):
            x = 10 + i * 6
            d.line([(x * s, (IN_T - 20) * s), (x * s, (H - 40) * s)], fill=c + (255,), width=4 * s)
    # the screen surround's lettering, like a handheld's: a power lamp and a small caption
    cap = {"D": "DOT MATRIX  WITH  NET LINK", "A": "DOT MATRIX  WITH  NET LINK", "B": "STUDIO MONITOR  \u00b7  NR-80", "C": "DATAWAVE  \u00b7  COLOUR TERMINAL"}[v]
    d.text(((IN_L + 12) * s, (IN_T - 11) * s), cap, font=A.font(8 * s, False), fill=(200, 200, 205, 200), anchor="lm")
    if v in ("A", "D"):
        d.line([((IN_L) * s, (IN_T - 11) * s), ((IN_L + 8) * s, (IN_T - 11) * s)], fill=(150, 36, 70, 255), width=2 * s)
    printed = big.resize((W, H), Image.LANCZOS)
    img.alpha_composite(A.to_img(np.zeros((H, W, 3), np.float32), np.asarray(printed, np.float32)[..., 3] / 255 * 0) )
    img.alpha_composite(printed)
    # a speaker grille in the bottom-right corner: diagonal slots
    g = ImageDraw.Draw(img)
    for i in range(6):
        x0 = W - 150 + i * 16
        g.line([x0, H - 22, x0 + 26, H - 62], fill=(50, 44, 40, 255), width=5)
        g.line([x0 + 1, H - 21, x0 + 27, H - 61], fill=(255, 255, 255, 40), width=1)
    return img


def make_badge(v):
    """The logo badge on the top bar."""
    p = V[v]
    if v == "A":
        W, H = 190, 46
        b = S.Big(W, H)
        b.rect((0, 0, W - 1, H - 1), (246, 238, 214, 255), r=8, outline=INK + (255,), width=2)
        b.text((14, 19), "NETRUN", A.font, 22, INK + (255,), anchor="lm")
        for i, c in enumerate(SUNSET):
            b.line([(112, 10 + i * 6), (180, 10 + i * 6)], c + (255,), 4)
        b.text((14, 37), "\u30cd\u30c3\u30c8\u30e9\u30f3 \u00b7 PORTABLE NET DECK", lambda sz: S.jfont(sz), 8, INK + (220,), anchor="lm")
        return b.done()
    if v in ("B", "D"):
        W, H = 168, 52
        b = S.Big(W, H)
        b.ellipse((2, 2, 48, 48), (246, 236, 214, 255), outline=INK + (255,), width=2)
        b.ellipse((14, 8, 36, 30), (214, 50, 40, 255))
        b.poly([(8, 42), (25, 22), (42, 42)], INK + (255,))
        for i in range(3):
            b.line([(14 + i * 2, 36 + i * 3), (36 - i * 2, 36 + i * 3)], (246, 236, 214, 255), 1.2)
        b.text((58, 20), "NR-80", A.font, 22, INK + (255,), anchor="lm")
        b.text((58, 40), "STUDIO NET SYSTEM", A.font, 8, INK + (220,), anchor="lm")
        return b.done()
    W, H = 64, 64
    b = S.Big(W, H)
    b.rect((0, 0, W - 1, H - 1), (60, 40, 30, 255), r=8)
    b.rect((4, 4, W - 5, H - 5), p["cream"] + (255,), r=6)
    import math
    for k in range(9):
        a0 = math.radians(180 + k * 20)
        a1 = a0 + math.radians(10)
        b.poly([(32, 40), (32 + 40 * math.cos(a0), 40 + 40 * math.sin(a0)), (32 + 40 * math.cos(a1), 40 + 40 * math.sin(a1))], (226, 100, 50, 255))
    b.ellipse((20, 28, 44, 52), p["cream"] + (255,))
    b.poly([(6, 58), (32, 30), (58, 58)], (60, 40, 30, 255))
    b.poly([(25, 37), (32, 30), (39, 37), (35, 35), (32, 38), (29, 35)], (246, 240, 226, 255))
    b.rect((4, 50, W - 5, H - 5), (60, 40, 30, 255), r=0)
    b.text((32, 54), "DATAWAVE", A.font, 8, p["cream"] + (255,))
    return b.done()


def make_stripes(colors, w=30, gap=8, h=IN_T - BEZEL):
    """Vertical sunset bars for the top bar, between the badge and the keys (Maxim, 2026-10-15)."""
    n = len(colors)
    b = S.Big(n * w + (n - 1) * gap, h)
    for i, c in enumerate(colors):
        b.rect((i * (w + gap), 0, i * (w + gap) + w - 1, h - 1), c + (255,), r=1)
    return b.done()


def make_wheel(w=14, h=56):
    """A ribbed thumb wheel peeking out of the case edge (volume on the left, contrast on the right)."""
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (58, 56, 62, 255), r=5)
    for y in range(4, h - 3, 4):
        b.line([(2, y), (w - 3, y)], (30, 28, 32, 255), 1.5)
        b.line([(2, y + 1.5), (w - 3, y + 1.5)], (110, 108, 116, 255), 0.8)
    return b.done()


def make_port(w=44, h=16):
    """A link port: a dark slot in a moulded surround with gold pins."""
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (60, 58, 64, 255), r=4)
    b.rect((4, 4, w - 5, h - 5), (16, 16, 18, 255), r=2)
    for x in range(8, w - 7, 4):
        b.rect((x, 6, x + 1.6, h - 7), (200, 160, 70, 255))
    return b.done()


def make_keyplate(w=120, h=56, base=(58, 56, 64)):
    """A recessed dark plate under the top keys and lamps, 9-sliced (borders 12), with a thin sunset line at the foot."""
    m = A.mask((w, h), lambda d, s: d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], 8 * s, fill=255))
    rgb = plastic(w, h, 1031, base) - (A.shade(m, 2.0) * 40)[..., None]      # sunk in: lit from below-right
    rgb -= (A.inner_edge(m, 3) * 25)[..., None]
    img = A.to_img(rgb, m)
    d = ImageDraw.Draw(img)
    for i, c in enumerate(SUNSET):
        d.line([10, h - 9 + i * 1.5, w - 11, h - 9 + i * 1.5], fill=c + (230,), width=1)
    return img


def make_screw(n=10):
    b = S.Big(n, n)
    b.ellipse((0, 0, n - 1, n - 1), (170, 164, 150, 255), outline=(90, 84, 74, 255), width=1)
    b.line([(2.5, n / 2), (n - 3.5, n / 2)], (80, 74, 66, 255), 1.4)
    return b.done()


def make_jack(n=16):
    b = S.Big(n, n)
    b.ellipse((0, 0, n - 1, n - 1), (180, 176, 168, 255), outline=(60, 56, 50, 255), width=1)
    b.ellipse((4, 4, n - 5, n - 5), (12, 12, 12, 255))
    return b.done()


def make_round_key(color, n=38):
    m = A.mask((n, n), lambda d, s: d.ellipse([1 * s, 1 * s, (n - 1) * s - 1, (n - 1) * s - 1], fill=255))
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    rgb = np.array(color, np.float32) + (np.clip(1 - np.hypot(xx - n * 0.4, yy - n * 0.35) / (n * 0.45), 0, 1) * 40)[..., None]
    rgb += (A.shade(m, 2.0) * 40)[..., None]
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 2), 2, 0) * 0.6)
    img.alpha_composite(A.to_img(rgb, m))
    return img


def make_pill(color, w=50, h=22):
    """A straight rubber key with a domed cap (Maxim, 2026-10-15: the slanted pills looked unnatural)."""
    m = A.mask((w, h), lambda d, s: d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], 6 * s, fill=255))
    cap = A.mask((w, h), lambda d, s: d.rounded_rectangle([2 * s, 1.5 * s, (w - 2) * s - 1, (h - 4) * s - 1], 5 * s, fill=255))
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    rgb = np.array(color, np.float32) - 14 + (A.shade(m, 1.5) * 30)[..., None] + A.grain(w, h, 1011, 2)[..., None]
    face = np.array(color, np.float32) + 10 + (A.shade(cap, 2.0) * 50)[..., None] + A.grain(w, h, 1012, 2)[..., None]
    face += (np.clip(1 - np.hypot(xx - w * 0.4, yy - h * 0.3) / (w * 0.5), 0, 1) * 18)[..., None]
    rgb = rgb * (1 - cap[..., None]) + face * cap[..., None]
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 1.5), 2, 0) * 0.6)
    img.alpha_composite(A.to_img(rgb, m))
    return img


def make_lcd():
    W, H = 120, 60
    outer = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 4 * s, fill=255))
    inner = A.mask((W, H), lambda d, s: d.rounded_rectangle([4 * s, 4 * s, (W - 4) * s - 1, (H - 4) * s - 1], 2 * s, fill=255))
    rgb = A.hexc(0x3a3a40) + (A.shade(outer, 1.2) * 30)[..., None]
    glass = LCD + A.grain(W, H, 1021, 2)[..., None] - (A.inner_edge(inner, 5) * 30)[..., None]
    rgb = rgb * (1 - inner[..., None]) + glass * inner[..., None]
    return A.to_img(rgb, outer)


def make_vu(w=150, h=64):
    """An analog VU meter face for the Heat: cream dial, black scale, a red zone; the needle comes from code."""
    import math
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (40, 38, 36, 255), r=5)
    b.rect((4, 4, w - 5, h - 5), (238, 226, 190, 255), r=3)
    cx, cy, r = w / 2, h + 26, 74
    for k in range(21):
        a = math.radians(-50 + k * 5)
        r0 = r - (8 if k % 5 == 0 else 5)
        col = (200, 40, 36, 255) if k >= 15 else INK + (255,)
        b.line([(cx + r0 * math.sin(a), cy - r0 * math.cos(a)), (cx + r * math.sin(a), cy - r * math.cos(a))], col, 1.4 if k % 5 == 0 else 1)
    b.text((12, 13), "-", A.font, 10, INK + (255,))
    b.text((w - 14, 13), "+", A.font, 10, (200, 40, 36, 255))
    b.text((w / 2, h - 11), "HEAT  VU", A.font, 8, INK + (255,))
    return b.done()


def make_slide(w=86, h=26):
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (40, 38, 36, 255), r=5)
    b.rect((6, 10, w - 7, 16), (14, 14, 14, 255), r=3)
    return b.done()


def make_grip():
    """The slide switch's knob: a ribbed grip."""
    w, h = 22, 20
    b = S.Big(w, h)
    b.rect((0, 0, w - 1, h - 1), (210, 204, 190, 255), r=3, outline=(60, 56, 50, 255), width=1)
    for x in range(5, w - 4, 3):
        b.line([(x, 4), (x, h - 5)], (120, 114, 104, 255), 1)
    return b.done()


def build(v):
    T = V[v]["T"]
    A.save(make_body(v), T, "Body")
    A.save(make_badge(v), T, "Badge")
    A.save(make_round_key(V[v]["accent"]), T, "Round")
    A.save(make_pill(V[v]["key"]), T, "Pill")
    A.save(make_lcd(), T, "Lcd")
    A.save(make_vu(), T, "VU")
    A.save(make_slide(), T, "Slide")
    A.save(make_grip(), T, "Grip")
    if v == "D":
        A.save(make_stripes(V[v]["stripes"]), T, "Stripes")
        A.save(make_wheel(), T, "Wheel")
        A.save(make_port(), T, "Port")
        A.save(make_jack(), T, "Jack")
        A.save(make_keyplate(), T, "KeyPlate")
        A.save(make_screw(), T, "Screw")


def mono(size, bold=True):
    try:
        return ImageFont.truetype("consolab.ttf" if bold else "consola.ttf", size)
    except OSError:
        return A.font(size)


SEG = {"0": "abcdef", "1": "bc", "2": "abged", "3": "abgcd", "4": "fgbc", "5": "afgcd", "6": "afgedc", "7": "abc", "8": "abcdefg", "9": "abcdfg"}


def seg_digit(d, x, y, w, h, ch, on, off):
    """A 7-segment digit, segments drawn as bars (the game draws them the same way)."""
    t = max(2, w // 5)
    bars = {"a": (x + t, y, x + w - t, y + t), "g": (x + t, y + h / 2 - t / 2, x + w - t, y + h / 2 + t / 2), "d": (x + t, y + h - t, x + w - t, y + h),
            "f": (x, y + t, x + t, y + h / 2 - t / 2), "b": (x + w - t, y + t, x + w, y + h / 2 - t / 2),
            "e": (x, y + h / 2 + t / 2, x + t, y + h - t), "c": (x + w - t, y + h / 2 + t / 2, x + w, y + h - t)}
    for k, r in bars.items():
        d.rectangle(r, fill=on if k in SEG[ch] else off)


def draw_heat(img, style, x, y, heat, ink):
    """Three takes on the Heat readout for the retro deck (Maxim, 2026-10-15: the VU needle felt like a car dashboard)."""
    d = ImageDraw.Draw(img)
    lab = A.font(9, False)
    level = min(4, int(heat // 20))
    if style == "pixel":
        # On a second green LCD: five pixel flames, lit up to the Heat level, like the lives in a handheld game.
        A.over(img, A.nine_slice(make_lcd(), (200, 60), 8, 8, 8, 8), (x, y))
        flame = ["...#....", "..##....", "..###.#.", ".#####..", ".######.", "##.####.", "##..###.", ".######.", "..####.."]
        for i in range(5):
            on = i <= level
            for r, row in enumerate(flame):
                for c, chv in enumerate(row):
                    if chv == "#":
                        px, py = x + 12 + i * 37 + c * 4, y + 12 + r * 4
                        d.rectangle([px, py, px + 3, py + 3], fill=LCD_INK if on else (128, 142, 100))
        d.text((x + 100, y + 72), "HEAT", font=lab, fill=ink, anchor="mm")
    elif style == "seg":
        # A red LED counter, the Heat in three digits and the level as a word, like an old clock radio.
        d.rounded_rectangle([x, y, x + 170, y + 60], 5, fill=(40, 38, 36))
        d.rounded_rectangle([x + 5, y + 5, x + 165, y + 55], 3, fill=(36, 6, 8))
        on, off = (255, 60, 40), (64, 14, 14)
        for i, ch in enumerate(f"{int(heat):03d}"):
            seg_digit(d, x + 14 + i * 30, y + 12, 22, 36, ch, on, off)
        A.glow(img, (x + 14, y + 12, x + 96, y + 48), on, 0.25, 6)
        d = ImageDraw.Draw(img)
        for i, t in enumerate(("SHADOW", "NOTICED", "TRACKED", "TARGET", "RED")):
            d.text((x + 108, y + 9 + i * 9.5), t, font=A.font(7, False), fill=on if i == level else (110, 40, 36))
        d.text((x + 85, y + 72), "HEAT", font=lab, fill=ink, anchor="mm")
    else:
        # A tape counter: three white-on-black drums behind a little window, rolling with the Heat.
        d.rounded_rectangle([x + 20, y + 6, x + 150, y + 54], 6, fill=(180, 176, 166), outline=(60, 56, 50))
        d.rounded_rectangle([x + 30, y + 14, x + 112, y + 46], 3, fill=(20, 20, 20))
        for i, ch in enumerate(f"{int(heat):03d}"):
            cx = x + 32 + i * 27
            d.rectangle([cx, y + 15, cx + 23, y + 45], fill=(30, 30, 30) if i < 2 else (34, 30, 30))
            d.text((cx + 12, y + 30), ch, font=A.font(22), fill=(240, 236, 226), anchor="mm")
            d.line([cx, y + 16, cx + 23, y + 16], fill=(70, 70, 70))
            d.line([cx, y + 44, cx + 23, y + 44], fill=(10, 10, 10))
        d.ellipse([x + 122, y + 22, x + 138, y + 38], fill=(60, 56, 50), outline=(30, 28, 26))
        d.text((x + 130, y + 30), "R", font=A.font(9), fill=(220, 214, 200), anchor="mm")
        d.text((x + 85, y + 72), "HEAT COUNTER", font=lab, fill=ink, anchor="mm")


def mock(v, out_path, screen=(930, 666), heat=58, online=True, heat_style="pixel"):
    import math
    T = V[v]["T"]
    p = V[v]
    W = screen[0] + 2 * OUT + IN_L + IN_R
    H = screen[1] + 2 * OUT + IN_T + IN_B

    def load(n):
        return Image.open(os.path.join(A.FRAME, T, n + ".png")).convert("RGBA")
    img = Image.new("RGBA", (W, H), (34, 34, 36, 255))
    d = ImageDraw.Draw(img)
    bx0, by0, bx1, by1 = OUT, OUT, W - OUT, H - OUT
    A.over(img, A.nine_slice(load("Body"), (bx1 - bx0, by1 - by0), SLICE, SLICE, SLICE, SLICE), (bx0, by0))
    sx0, sy0, sx1, sy1 = bx0 + IN_L, by0 + IN_T, bx1 - IN_R, by1 - IN_B
    d.rounded_rectangle([sx0, sy0, sx1 - 1, sy1 - 1], 6, fill=(22, 26, 22, 255))
    f = mono(13, False)
    for i, (t, c) in enumerate((("NETRUN // NETWORK CORE", (90, 230, 130)), ("> HOME NODE ........ ONLINE", (90, 210, 130)),
                                ("> GATEWAY .......... 5 NODES", (90, 210, 130)), ("> HEAT 58 .......... TRACKED", (230, 190, 70)))):
        d.text((sx0 + 30, sy0 + 28 + i * 22), t, font=f, fill=c)
    for r in (60, 120):
        d.ellipse([sx1 - 230 - r, (sy0 + sy1) / 2 - r, sx1 - 230 + r, (sy0 + sy1) / 2 + r], outline=(40, 120, 70))
    ink = (246, 236, 214) if v == "C" else INK
    lab = A.font(9, False)

    # top: the badge, pill keys CLOSE MENU ACHIEV NET-LOCK, lamps
    badge = load("Badge")
    top_y = by0 + 26 if v == "A" else by0 + 18
    A.over(img, badge, (bx0 + 60, by0 + (30 if v == "A" else 14) if v != "C" else by0 + 8))
    top_y = by0 + 18 if v == "D" else top_y
    kx = bx1 - IN_R - 420
    if v == "D":
        st = load("Stripes")                    # thick bars from the case's top edge down to the screen surround
        A.over(img, st, (kx - 40 - st.width, by0))
        rail = IN_L - BEZEL                     # the visible side rail, 42 px
        wheel = load("Wheel")
        y0 = by0 + IN_T + 40
        A.over(img, wheel, (bx0 - 5, y0))
        A.over(img, wheel, (bx1 - wheel.width + 5, y0))
        lab7 = A.font(7, False)
        d.text((bx0 + rail / 2 + 4, y0 + 66), "VOL", font=lab7, fill=INK, anchor="mm")
        d.text((bx1 - rail / 2 - 4, y0 + 66), "CONT", font=lab7, fill=INK, anchor="mm")
        jack = load("Jack")
        A.over(img, jack, (int(bx0 + rail / 2 - 4), y0 + 100))
        d.text((bx0 + rail / 2 + 4, y0 + 126), "PHONES", font=lab7, fill=INK, anchor="mm")
        port_v = load("Port").rotate(90, expand=True)
        A.over(img, port_v, (int(bx1 - rail / 2 - 12), y0 + 92))
        d.text((bx1 - rail / 2 - 4, y0 + 146), "EXT", font=lab7, fill=INK, anchor="mm")
        screw = load("Screw")
        for sxx in (bx0 + rail / 2 + 4 - 5, bx1 - rail / 2 - 4 - 5):
            for syy in (by0 + IN_T - 8, by1 - IN_B + 4):
                A.over(img, screw, (int(sxx), int(syy)))
        A.glow(img, (bx0 + rail / 2 + 1, y0 + 170, bx0 + rail / 2 + 7, y0 + 176), (240, 60, 50), 0.6, 5)
        d.ellipse([bx0 + rail / 2, y0 + 169, bx0 + rail / 2 + 8, y0 + 177], fill=(240, 60, 50), outline=(60, 20, 20))
        d.text((bx0 + rail / 2 + 4, y0 + 188), "POWER", font=lab7, fill=INK, anchor="mm")
        cap_img = Image.new("RGBA", (220, 14), (0, 0, 0, 0))
        ImageDraw.Draw(cap_img).text((110, 7), "NR-80  \u00b7  STEREO NET SOUND", font=lab7, fill=INK + (200,), anchor="mm")
        A.over(img, cap_img.rotate(-90, expand=True), (int(bx1 - rail / 2 - 11), y0 + 190))
        for k in range(14):                       # grip ridges low on both rails
            yy = by1 - IN_B - 60 - k * 9
            for xa in (bx0 + 10, bx1 - rail + 2):
                d.line([xa, yy, xa + rail - 12, yy], fill=(190, 182, 160), width=2)
                d.line([xa, yy + 2, xa + rail - 12, yy + 2], fill=(242, 236, 220), width=1)
    top_ink = ink
    if v == "D":   # the keys and lamps sit on a sunk dark plate (Maxim: they looked bare)
        plate_r = (kx - 18, top_y - 6, bx1 - IN_R - 112 + 2 * 36 + 22 - (kx - 18), 56)
        A.over(img, A.nine_slice(load("KeyPlate"), plate_r[2:], 12, 12, 12, 12), plate_r[:2])
        top_ink = (226, 222, 210)
    for i, name in enumerate(("CLOSE", "MENU", "ACHIEV", "NET-LOCK")):
        x = kx + i * 72
        pill = load("Pill")
        A.over(img, pill, (x, top_y + 4))
        d.text((x + 25, top_y + 36), name, font=lab, fill=top_ink, anchor="mm")
    lx = bx1 - IN_R - 112
    for i, (c, name) in enumerate((((240, 60, 50), "NET"), ((250, 160, 40), "SICK"), ((60, 50, 50), "POD"))):
        cx, cy = lx + i * 36, top_y + 15
        if i < 2:
            A.glow(img, (cx - 3, cy - 3, cx + 3, cy + 3), c, 0.6, 5)
        d.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], fill=c, outline=(30, 26, 24))
        d.text((cx, top_y + 36), name, font=lab, fill=top_ink, anchor="mm")

    # bottom: LCD, round keys BACK CORE HELP (A/B style), the VU meter, the NET LINK slide switch
    yb = by1 - IN_B
    lcd = (bx0 + 70, yb + 26, 240, 60)
    A.over(img, A.nine_slice(load("Lcd"), lcd[2:], 8, 8, 8, 8), lcd[:2])
    for i, t in enumerate(("CYBERDATA 491", "HEAT 58 \u25b2 TRACKED", "D8 08:27  NET ON")):
        d.text((lcd[0] + 12, lcd[1] + 9 + i * 14), t, font=mono(11), fill=LCD_INK)
    rx = lcd[0] + lcd[2] + 40
    for i, (g, name) in enumerate((("<", "BACK"), ("O", "CORE"), ("?", "HELP"))):
        x, y = rx + i * 52, yb + 40 - (i % 2) * 10   # staggered like a handheld's A/B
        A.over(img, load("Round"), (x, y))
        d.text((x + 19, y + 19), g, font=A.font(14), fill=(246, 236, 214) if v != "C" else (60, 40, 30), anchor="mm")
        d.text((x + 19, y + 48), name, font=lab, fill=ink, anchor="mm")
    vx = rx + 3 * 52 + 30
    draw_heat(img, heat_style, vx, yb + 26, heat, ink)
    sx = bx1 - IN_R - 230
    A.over(img, load("Slide"), (sx, yb + 44))
    A.over(img, load("Grip"), (sx + (58 if online else 6), yb + 47))
    d.text((sx, yb + 34), "OFF", font=lab, fill=ink, anchor="lm")
    d.text((sx + 86, yb + 34), "ON", font=lab, fill=ink, anchor="rm")
    d.text((sx + 43, yb + 80), "NET LINK", font=lab, fill=ink, anchor="mm")
    img.convert("RGB").save(out_path)


if __name__ == "__main__":
    v = sys.argv[sys.argv.index("--variant") + 1] if "--variant" in sys.argv else "A"
    build(v)
    if "--mock" in sys.argv:
        style = sys.argv[sys.argv.index("--heat") + 1] if "--heat" in sys.argv else "pixel"
        mock(v, sys.argv[sys.argv.index("--mock") + 1], heat_style=style)
