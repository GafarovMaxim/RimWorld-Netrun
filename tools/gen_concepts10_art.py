"""Concept art for docs/CONCEPTS_10.md (cloud session, 2026-10-07).

Writes:
  Hacking Expansion Netrun Art/Concepts10/...   source PNGs (256 px)
  docs/ui-mockups/25-achievement-frames.png     4 frame variants for Vanilla Achievements Expanded
  docs/ui-mockups/26-concepts10-icons.png       memes, issues, runner crews, corporations
  docs/ui-mockups/27-cyberspace-geography.png   map scheme: backbone, branches, zones, home, exits

Run from the repo root:  python tools/gen_concepts10_art.py
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from netrun_art import *  # noqa: E402,F401,F403

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Hacking Expansion Netrun Art", "Concepts10")
MOCK = os.path.join(ROOT, "docs", "ui-mockups")
SAMPLE_ICON = os.path.join(ROOT, "Textures", "UI", "Netrun", "Gizmo_NetrunDive.png")

TIERS = [("common", CYAN), ("rare", GOLD), ("secret", PURPLE)]
FRAME = 256


# ---------------------------------------------------------------- achievement frames

def frame_octagon(tier):
    """A: octagon node rim, like the Gateway nodes and daemons."""
    S = FRAME * SS
    img = canvas(FRAME)
    c = S / 2
    outer = octagon(c, c, S * 0.47)
    rim = octagon(c, c, S * 0.445)
    inner = octagon(c, c, S * 0.40)
    rim_layer = canvas(FRAME)
    poly(rim_layer, outer, with_alpha(tier, 255))
    poly(img, outer, INK)
    poly(img, rim, tier)
    poly(img, octagon(c, c, S * 0.418), INK)
    m = mask_of(img.size, inner)
    img.alpha_composite(radial_fill(img.size, DARK2, DARK, m))
    grid(img, m, S // 16, with_alpha(tier, 22))
    # corner ticks on the diagonals
    d = ImageDraw.Draw(img)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        x0, y0 = c + math.cos(a) * S * 0.33, c + math.sin(a) * S * 0.33
        x1, y1 = c + math.cos(a) * S * 0.375, c + math.sin(a) * S * 0.375
        thick_line(d, [(x0, y0), (x1, y1)], with_alpha(tier, 170), S * 0.012)
    return glow(img, rim_layer, S * 0.02, 0.8)


def frame_deck(tier):
    """B: cyberdeck plate: chamfered steel frame, screws, CRT screen, tier LED."""
    S = FRAME * SS
    img = canvas(FRAME)
    m0 = S * 0.04
    outer = chamfer_rect(m0, m0, S - m0, S - m0, S * 0.09)
    poly(img, outer, INK)
    steel = chamfer_rect(m0 + S * 0.015, m0 + S * 0.015, S - m0 - S * 0.015, S - m0 - S * 0.015, S * 0.08)
    mk = mask_of(img.size, steel)
    img.alpha_composite(radial_fill(img.size, STEEL_HI, STEEL, mk))
    # screen
    s0, s1 = S * 0.15, S * 0.85
    screen = chamfer_rect(s0, s0, s1, S * 0.80, S * 0.04)
    poly(img, chamfer_rect(s0 - S * 0.012, s0 - S * 0.012, s1 + S * 0.012, S * 0.812, S * 0.05), INK)
    poly(img, chamfer_rect(s0 - S * 0.004, s0 - S * 0.004, s1 + S * 0.004, S * 0.804, S * 0.045), tier)
    ms = mask_of(img.size, screen)
    img.alpha_composite(radial_fill(img.size, (16, 34, 30, 255), (6, 12, 12, 255), ms))
    scanlines(img, ms, S // 64, with_alpha(tier, 26))
    d = ImageDraw.Draw(img)
    # screws
    for x, y in [(0.095, 0.095), (0.905, 0.095), (0.095, 0.905), (0.905, 0.905)]:
        r = S * 0.026
        d.ellipse((x * S - r - 4, y * S - r - 4, x * S + r + 4, y * S + r + 4), fill=INK)
        d.ellipse((x * S - r, y * S - r, x * S + r, y * S + r), fill=STEEL_HI)
        thick_line(d, [(x * S - r * 0.6, y * S), (x * S + r * 0.6, y * S)], INK, S * 0.008)
    # bottom label and LED
    d.rectangle((S * 0.30, S * 0.845, S * 0.70, S * 0.89), fill=INK)
    text_center(img, (S * 0.48, S * 0.846), "NR-ACH", int(S * 0.034), with_alpha(tier, 230), mono=True)
    led = canvas(FRAME)
    ImageDraw.Draw(led).ellipse((S * 0.73, S * 0.85, S * 0.77, S * 0.89), fill=tier)
    img.alpha_composite(led)
    img = glow(img, led, S * 0.015, 1.5)
    return img


def frame_chip(tier):
    """C: hex chip with pins, like the AI gizmos."""
    S = FRAME * SS
    img = canvas(FRAME)
    c = S / 2
    d = ImageDraw.Draw(img)
    # pins left and right
    for side in (-1, 1):
        for k in range(4):
            y = c + (k - 1.5) * S * 0.12
            x0 = c + side * S * 0.36
            x1 = c + side * S * 0.475
            box = (min(x0, x1), y - S * 0.022, max(x0, x1), y + S * 0.022)
            d.rectangle((box[0] - 6, box[1] - 6, box[2] + 6, box[3] + 6), fill=INK)
            d.rectangle(box, fill=with_alpha(tier, 255))
    outer = regular_poly(c, c, S * 0.43, 6, -math.pi / 2)
    rim = regular_poly(c, c, S * 0.40, 6, -math.pi / 2)
    inner = regular_poly(c, c, S * 0.355, 6, -math.pi / 2)
    rim_layer = canvas(FRAME)
    poly(rim_layer, outer, tier)
    poly(img, outer, INK)
    poly(img, rim, tier)
    poly(img, regular_poly(c, c, S * 0.375, 6, -math.pi / 2), INK)
    m = mask_of(img.size, inner)
    img.alpha_composite(radial_fill(img.size, DARK2, DARK, m))
    # circuit traces from the corners
    tr = canvas(FRAME)
    dt = ImageDraw.Draw(tr)
    for i, (x, y) in enumerate(inner):
        mx, my = x + (c - x) * 0.18, y + (c - y) * 0.18
        thick_line(dt, [(x, y), (mx, my), (mx + (S * 0.04 if i % 2 else -S * 0.04), my)], with_alpha(tier, 70), S * 0.008)
    img.alpha_composite(tr)
    return glow(img, rim_layer, S * 0.018, 0.7)


def frame_ring(tier):
    """D: round badge with a segmented ring, like the Heat ring."""
    S = FRAME * SS
    img = canvas(FRAME)
    c = S / 2
    d = ImageDraw.Draw(img)
    R = S * 0.46
    d.ellipse((c - R, c - R, c + R, c + R), fill=INK)
    ring = canvas(FRAME)
    dr = ImageDraw.Draw(ring)
    r1 = S * 0.435
    for k in range(5):
        start = -90 + k * 72 + 4
        dr.pieslice((c - r1, c - r1, c + r1, c + r1), start, start + 64, fill=tier)
    r2 = S * 0.385
    dr.ellipse((c - r2, c - r2, c + r2, c + r2), fill=(0, 0, 0, 0))
    hole = Image.new("L", img.size, 255)
    ImageDraw.Draw(hole).ellipse((c - r2, c - r2, c + r2, c + r2), fill=0)
    ring.putalpha(Image.composite(ring.getchannel("A"), Image.new("L", img.size, 0), hole))
    img.alpha_composite(ring)
    r3 = S * 0.37
    m = ellipse_mask(img.size, (c - r3, c - r3, c + r3, c + r3))
    img.alpha_composite(radial_fill(img.size, DARK2, DARK, m))
    # ticks
    for k in range(40):
        a = 2 * math.pi * k / 40
        l = 0.025 if k % 5 else 0.045
        thick_line(d, [(c + math.cos(a) * r3 * (1 - l * 2), c + math.sin(a) * r3 * (1 - l * 2)),
                       (c + math.cos(a) * r3 * 0.985, c + math.sin(a) * r3 * 0.985)], with_alpha(tier, 60), S * 0.005)
    img = glow(img, ring, S * 0.02, 0.8)
    return img


FRAMES = [
    ("A", "Восьмиугольник ноды", frame_octagon),
    ("B", "Плата кибердеки", frame_deck),
    ("C", "Чип ИИ", frame_chip),
    ("D", "Кольцо Heat", frame_ring),
]


def sample_icon(size):
    icon = Image.open(SAMPLE_ICON).convert("RGBA")
    return icon.resize((size, size), Image.LANCZOS)


def build_frames():
    os.makedirs(os.path.join(ART, "AchievementFrames"), exist_ok=True)
    cell = 192
    pad = 26
    label_w = 290
    cols = ["Обычное", "Редкое", "Секретное", "Закрыто", "128 px"]
    W = label_w + len(cols) * (cell + pad) + pad
    H = 120 + len(FRAMES) * (cell + 50) + 70
    sheet = Image.new("RGBA", (W, H), (30, 30, 32, 255))
    d = ImageDraw.Draw(sheet)
    d.text((pad, 24), "Рамки достижений Netrun (Vanilla Achievements Expanded, bgtexPath)", font=font(26, True), fill=WHITE)
    d.text((pad, 62), "Фон 256 px одинаковый для всех достижений, цвет обода = уровень. Иконка рисуется поверх; у закрытого VAE красит её в чёрный.",
           font=font(16), fill=(170, 176, 186, 255))
    for i, name in enumerate(cols):
        x = label_w + pad + i * (cell + pad)
        d.text((x + cell / 2 - d.textlength(name, font=font(18, True)) / 2, 96), name, font=font(18, True), fill=(200, 205, 212, 255))
    icon_full = sample_icon(FRAME)
    for r, (key, title, fn) in enumerate(FRAMES):
        y = 128 + r * (cell + 50)
        d.text((pad, y + cell / 2 - 30), f"{key}", font=font(40, True), fill=CYAN)
        d.text((pad + 44, y + cell / 2 - 18), title, font=font(19, True), fill=WHITE)
        for c_i, (tier_name, tier) in enumerate(TIERS):
            fr = finish(fn(tier), FRAME)
            fr.save(os.path.join(ART, "AchievementFrames", f"AchFrame_{key}_{tier_name}.png"))
            card = fr.copy()
            ic = icon_full.resize((int(FRAME * 0.56), int(FRAME * 0.56)), Image.LANCZOS)
            off = (FRAME - ic.size[0]) // 2
            card.alpha_composite(ic, (off, off))
            sheet.alpha_composite(card.resize((cell, cell), Image.LANCZOS), (label_w + pad + c_i * (cell + pad), y))
            if c_i == 0:
                locked = finish(fn(tier), FRAME)
                locked.alpha_composite(silhouette(ic), (off, off))
                sheet.alpha_composite(locked.resize((cell, cell), Image.LANCZOS), (label_w + pad + 3 * (cell + pad), y))
                small = card.resize((128, 128), Image.LANCZOS)
                sheet.alpha_composite(small, (label_w + pad + 4 * (cell + pad) + (cell - 128) // 2, y + (cell - 128) // 2))
    d.text((pad, H - 50), "Пример иконки внутри: Gizmo_NetrunDive (иконки достижений будут свои, с запасом по краям, чтобы обод был виден).",
           font=font(16), fill=(170, 176, 186, 255))
    sheet.convert("RGB").save(os.path.join(MOCK, "25-achievement-frames.png"))


# ---------------------------------------------------------------- icons: memes, issues, role, crews, corporations

ICON = 256
INKW = 0.022  # outline width, share of the canvas


def _S():
    return ICON * SS


def chip_body(img, c, half, rim, pins=4, pin_len=0.07):
    """Square chip with pins on all sides (the mod's AI gizmo look)."""
    S = _S()
    d = ImageDraw.Draw(img)
    pw = S * 0.035
    for side in range(4):
        for k in range(pins):
            t = (k - (pins - 1) / 2) * (2 * half / (pins + 0.6))
            if side == 0:
                box = (c + t - pw / 2, c - half - S * pin_len, c + t + pw / 2, c - half)
            elif side == 1:
                box = (c + t - pw / 2, c + half, c + t + pw / 2, c + half + S * pin_len)
            elif side == 2:
                box = (c - half - S * pin_len, c + t - pw / 2, c - half, c + t + pw / 2)
            else:
                box = (c + half, c + t - pw / 2, c + half + S * pin_len, c + t + pw / 2)
            e = S * 0.012
            d.rectangle((box[0] - e, box[1] - e, box[2] + e, box[3] + e), fill=INK)
            d.rectangle(box, fill=rim)
    outlined_poly(img, chamfer_rect(c - half, c - half, c + half, c + half, half * 0.18), rim, S * INKW)
    poly(img, chamfer_rect(c - half * 0.86, c - half * 0.86, c + half * 0.86, c + half * 0.86, half * 0.14), INK)
    poly(img, chamfer_rect(c - half * 0.80, c - half * 0.80, c + half * 0.80, c + half * 0.80, half * 0.12), DARK2)


def eye(img, cx, cy, w, h, iris_color):
    S = _S()
    pts = []
    for i in range(41):
        t = i / 40
        x = cx - w / 2 + w * t
        pts.append((x, cy - h / 2 * math.sin(math.pi * t)))
    for i in range(41):
        t = 1 - i / 40
        x = cx - w / 2 + w * t
        pts.append((x, cy + h / 2 * math.sin(math.pi * t)))
    outlined_poly(img, pts, WHITE, S * INKW * 0.8)
    r = h * 0.42
    lay = canvas(ICON)
    ImageDraw.Draw(lay).ellipse((cx - r, cy - r, cx + r, cy + r), fill=iris_color)
    outlined_ellipse(img, (cx - r, cy - r, cx + r, cy + r), iris_color, S * INKW * 0.6)
    r2 = r * 0.45
    ImageDraw.Draw(img).ellipse((cx - r2, cy - r2, cx + r2, cy + r2), fill=INK)
    return lay


def icon_meme_machine():
    """Meme: Machine revelation. A chip with an all-seeing eye under a halo."""
    S = _S(); c = S / 2
    img = canvas(ICON)
    # halo
    halo = canvas(ICON)
    dh = ImageDraw.Draw(halo)
    box = (c - S * 0.30, S * 0.05, c + S * 0.30, S * 0.26)
    dh.ellipse((box[0] - S * 0.02, box[1] - S * 0.02, box[2] + S * 0.02, box[3] + S * 0.02), fill=INK)
    dh.ellipse(box, fill=GOLD)
    dh.ellipse((box[0] + S * 0.04, box[1] + S * 0.035, box[2] - S * 0.04, box[3] - S * 0.035), fill=(0, 0, 0, 0))
    img.alpha_composite(halo)
    chip_body(img, c, S * 0.25, GOLD)
    img_c = canvas(ICON)
    img_c.alpha_composite(img)
    glow_layer = eye(img_c, c, c + S * 0.02, S * 0.36, S * 0.22, CYAN)
    img_c = glow(img_c, glow_layer, S * 0.03, 1.2)
    img_c = glow(img_c, halo, S * 0.025, 0.6)
    return img_c


def icon_meme_ascension():
    """Meme: Net ascension. A head dissolving upward into pixels."""
    S = _S(); c = S / 2
    img = canvas(ICON)
    # shoulders and head
    outlined_poly(img, [(S * 0.18, S * 0.95), (S * 0.24, S * 0.78), (S * 0.38, S * 0.70), (S * 0.62, S * 0.70), (S * 0.76, S * 0.78), (S * 0.82, S * 0.95)], CYAN, S * INKW)
    outlined_ellipse(img, (c - S * 0.16, S * 0.36, c + S * 0.16, S * 0.70), WHITE, S * INKW)
    # top of the head breaks into squares
    d = ImageDraw.Draw(img)
    rnd = random.Random(7)
    cover = (c - S * 0.18, S * 0.30, c + S * 0.18, S * 0.47)
    d.rectangle(cover, fill=(0, 0, 0, 0))
    pix = canvas(ICON)
    dp = ImageDraw.Draw(pix)
    cell = S * 0.055
    for row in range(7):
        y = S * 0.44 - row * cell * 1.15
        n = max(1, 6 - row)
        for k in range(n):
            if rnd.random() < 0.25 + row * 0.07:
                continue
            x = c + (k - (n - 1) / 2) * cell * 1.2 + rnd.uniform(-cell * 0.3, cell * 0.3) * row * 0.4
            col = WHITE if row < 2 else CYAN if row < 4 else GREEN
            sz = cell * (1 - row * 0.09)
            dp.rectangle((x - sz / 2 - S * 0.012, y - sz / 2 - S * 0.012, x + sz / 2 + S * 0.012, y + sz / 2 + S * 0.012), fill=INK)
            dp.rectangle((x - sz / 2, y - sz / 2, x + sz / 2, y + sz / 2), fill=col)
    img.alpha_composite(pix)
    # visor line
    outlined_line(img, [(c - S * 0.11, S * 0.56), (c + S * 0.11, S * 0.56)], CYAN, S * 0.03, S * 0.03)
    return glow(img, pix, S * 0.02, 0.9)


def icon_meme_purity():
    """Meme: Digital purity. A head with a crossed-out chip in front."""
    S = _S(); c = S / 2
    img = canvas(ICON)
    skin = (226, 200, 170, 255)
    outlined_poly(img, [(S * 0.14, S * 0.95), (S * 0.20, S * 0.76), (S * 0.36, S * 0.66), (S * 0.64, S * 0.66), (S * 0.80, S * 0.76), (S * 0.86, S * 0.95)], GREEN, S * INKW)
    outlined_ellipse(img, (c - S * 0.16, S * 0.12, c + S * 0.16, S * 0.62), skin, S * INKW)
    # no-sign with a chip
    cx, cy, r = S * 0.68, S * 0.68, S * 0.2
    outlined_ellipse(img, (cx - r, cy - r, cx + r, cy + r), WHITE, S * INKW)
    sub = canvas(ICON)
    chip_small = canvas(ICON)
    chip_body(chip_small, S / 2, S * 0.2, STEEL_HI, pins=3, pin_len=0.06)
    chip_small = chip_small.resize((int(S * 0.45), int(S * 0.45)), Image.LANCZOS)
    sub.alpha_composite(chip_small, (int(cx - S * 0.225), int(cy - S * 0.225)))
    img.alpha_composite(sub)
    d = ImageDraw.Draw(img)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=INK, width=int(S * 0.06))
    d.ellipse((cx - r + S * 0.012, cy - r + S * 0.012, cx + r - S * 0.012, cy + r - S * 0.012), outline=RED, width=int(S * 0.035))
    a = math.radians(45)
    p0 = (cx - math.cos(a) * r * 0.85, cy - math.sin(a) * r * 0.85)
    p1 = (cx + math.cos(a) * r * 0.85, cy + math.sin(a) * r * 0.85)
    outlined_line(img, [p0, p1], RED, S * 0.035, S * 0.03)
    return img


def icon_issue_ai():
    """Issue: AI. A chip with two eyes."""
    S = _S(); c = S / 2
    img = canvas(ICON)
    chip_body(img, c, S * 0.30, PURPLE, pins=4)
    eyes = canvas(ICON)
    de = ImageDraw.Draw(eyes)
    for dx in (-1, 1):
        x = c + dx * S * 0.11
        de.rounded_rectangle((x - S * 0.05 - S * 0.015, c - S * 0.09 - S * 0.015, x + S * 0.05 + S * 0.015, c + S * 0.05 + S * 0.015), radius=S * 0.03, fill=INK)
        de.rounded_rectangle((x - S * 0.05, c - S * 0.09, x + S * 0.05, c + S * 0.05), radius=S * 0.025, fill=CYAN)
    img.alpha_composite(eyes)
    outlined_line(img, [(c - S * 0.10, c + S * 0.15), (c + S * 0.10, c + S * 0.15)], CYAN, S * 0.025, S * 0.025)
    return glow(img, eyes, S * 0.025, 1.0)


def icon_issue_braindance():
    """Issue: braindance. Visor goggles with a play mark, like the BD gizmos."""
    S = _S(); c = S / 2
    img = canvas(ICON)
    coral = (255, 112, 96, 255)
    d = ImageDraw.Draw(img)
    # strap
    outlined_line(img, [(S * 0.06, c - S * 0.02), (S * 0.20, c - S * 0.12), (S * 0.80, c - S * 0.12), (S * 0.94, c - S * 0.02)], STEEL, S * 0.04, S * 0.03)
    body = [(S * 0.16, c - S * 0.10), (S * 0.84, c - S * 0.10), (S * 0.90, c + S * 0.04), (S * 0.80, c + S * 0.16), (S * 0.58, c + S * 0.16), (S * 0.5, c + S * 0.09), (S * 0.42, c + S * 0.16), (S * 0.20, c + S * 0.16), (S * 0.10, c + S * 0.04)]
    outlined_poly(img, body, DARK2, S * INKW)
    lens = canvas(ICON)
    dl = ImageDraw.Draw(lens)
    dl.polygon([(S * 0.22, c - S * 0.05), (S * 0.78, c - S * 0.05), (S * 0.82, c + S * 0.04), (S * 0.75, c + S * 0.10), (S * 0.25, c + S * 0.10), (S * 0.18, c + S * 0.04)], fill=coral)
    img.alpha_composite(lens)
    tri = [(c - S * 0.035, c - S * 0.035), (c - S * 0.035, c + S * 0.075), (c + S * 0.06, c + S * 0.02)]
    outlined_poly(img, tri, WHITE, S * 0.012)
    return glow(img, lens, S * 0.03, 1.0)


def icon_role_archon():
    """Precept role: Net archon. Hooded figure with a visor and a data halo."""
    S = _S(); c = S / 2
    img = canvas(ICON)
    halo = canvas(ICON)
    dh = ImageDraw.Draw(halo)
    for k in range(12):
        a = math.pi + k * math.pi / 11
        x, y = c + math.cos(a) * S * 0.36, S * 0.42 + math.sin(a) * S * 0.36
        r = S * 0.03
        dh.rectangle((x - r - S * 0.01, y - r - S * 0.01, x + r + S * 0.01, y + r + S * 0.01), fill=INK)
        dh.rectangle((x - r, y - r, x + r, y + r), fill=CYAN if k % 3 else GOLD)
    img.alpha_composite(halo)
    hood = [(c, S * 0.18), (c + S * 0.20, S * 0.34), (c + S * 0.24, S * 0.62), (c + S * 0.36, S * 0.95), (c - S * 0.36, S * 0.95), (c - S * 0.24, S * 0.62), (c - S * 0.20, S * 0.34)]
    outlined_poly(img, hood, (70, 54, 120, 255), S * INKW)
    face = [(c, S * 0.30), (c + S * 0.12, S * 0.40), (c + S * 0.13, S * 0.58), (c, S * 0.66), (c - S * 0.13, S * 0.58), (c - S * 0.12, S * 0.40)]
    poly(img, face, INK)
    visor = canvas(ICON)
    ImageDraw.Draw(visor).polygon([(c - S * 0.11, S * 0.43), (c + S * 0.11, S * 0.43), (c + S * 0.09, S * 0.50), (c - S * 0.09, S * 0.50)], fill=CYAN)
    img.alpha_composite(visor)
    outlined_line(img, [(c, S * 0.72), (c, S * 0.92)], GOLD, S * 0.03, S * 0.025)
    img = glow(img, visor, S * 0.025, 1.3)
    return glow(img, halo, S * 0.02, 0.7)


# Crew emblems: white fill so the game can tint them with the faction color.
def icon_crew_claws():
    S = _S(); c = S / 2
    img = canvas(ICON)
    outlined_poly(img, octagon(c, c, S * 0.44), DARK2, S * INKW)
    for k in range(3):
        x = c + (k - 1) * S * 0.15
        pts = [(x + S * 0.10, S * 0.18), (x + S * 0.04, S * 0.20), (x - S * 0.10, S * 0.80), (x - S * 0.04, S * 0.78), (x - S * 0.02, S * 0.70)]
        outlined_poly(img, [(x + S * 0.11, S * 0.17), (x + S * 0.02, S * 0.24), (x - S * 0.11, S * 0.83), (x - S * 0.03, S * 0.74)], WHITE, S * INKW * 0.8)
    return img


def icon_crew_mask():
    S = _S(); c = S / 2
    img = canvas(ICON)
    outlined_poly(img, octagon(c, c, S * 0.44), DARK2, S * INKW)
    mask = [(c, S * 0.16), (c + S * 0.26, S * 0.26), (c + S * 0.28, S * 0.52), (c + S * 0.14, S * 0.80), (c, S * 0.86), (c - S * 0.14, S * 0.80), (c - S * 0.28, S * 0.52), (c - S * 0.26, S * 0.26)]
    outlined_poly(img, mask, WHITE, S * INKW)
    d = ImageDraw.Draw(img)
    for dx in (-1, 1):
        eye_pts = [(c + dx * S * 0.04, S * 0.44), (c + dx * S * 0.22, S * 0.40), (c + dx * S * 0.20, S * 0.49), (c + dx * S * 0.06, S * 0.51)]
        d.polygon(eye_pts, fill=INK)
    d.line([(c, S * 0.58), (c, S * 0.78)], fill=INK, width=int(S * 0.02))
    for k in range(3):
        y = S * 0.63 + k * S * 0.05
        d.line([(c - S * 0.07, y), (c + S * 0.07, y)], fill=INK, width=int(S * 0.012))
    return img


def icon_crew_bolt():
    S = _S(); c = S / 2
    img = canvas(ICON)
    d = ImageDraw.Draw(img)
    R = S * 0.42
    d.ellipse((c - R - S * 0.02, c - R - S * 0.02, c + R + S * 0.02, c + R + S * 0.02), fill=INK)
    d.ellipse((c - R, c - R, c + R, c + R), fill=DARK2)
    # broken ring
    for start in (20, 140, 260):
        d.arc((c - R * 0.86, c - R * 0.86, c + R * 0.86, c + R * 0.86), start, start + 95, fill=INK, width=int(S * 0.07))
        d.arc((c - R * 0.86 + S * 0.012, c - R * 0.86 + S * 0.012, c + R * 0.86 - S * 0.012, c + R * 0.86 - S * 0.012), start + 3, start + 92, fill=WHITE, width=int(S * 0.04))
    bolt = [(c + S * 0.06, S * 0.14), (c - S * 0.16, S * 0.54), (c - S * 0.01, S * 0.54), (c - S * 0.08, S * 0.86), (c + S * 0.17, S * 0.42), (c + S * 0.02, S * 0.42)]
    outlined_poly(img, bolt, WHITE, S * INKW)
    return img


def icon_corp_security():
    """Corporation logo, security profile: isometric cube with a shield face."""
    S = _S(); c = S / 2
    img = canvas(ICON)
    blue, blue_d, blue_l = (70, 140, 230, 255), (40, 82, 150, 255), (140, 196, 255, 255)
    top = [(c, S * 0.12), (c + S * 0.34, S * 0.30), (c, S * 0.48), (c - S * 0.34, S * 0.30)]
    left = [(c - S * 0.34, S * 0.30), (c, S * 0.48), (c, S * 0.90), (c - S * 0.34, S * 0.70)]
    right = [(c + S * 0.34, S * 0.30), (c, S * 0.48), (c, S * 0.90), (c + S * 0.34, S * 0.70)]
    whole = [top[0], top[1], right[3], left[2], left[3], top[3]]
    d = ImageDraw.Draw(img)
    d.polygon(whole, fill=INK)
    d.line(whole + [whole[0]], fill=INK, width=int(S * INKW * 2), joint="curve")
    poly(img, top, blue_l)
    poly(img, left, blue)
    poly(img, right, blue_d)
    sh = [(c + S * 0.08, S * 0.52), (c + S * 0.26, S * 0.43), (c + S * 0.26, S * 0.60), (c + S * 0.17, S * 0.74), (c + S * 0.08, S * 0.68)]
    outlined_poly(img, sh, WHITE, S * 0.012)
    return img


def icon_corp_media():
    """Corporation logo, media profile: eye with broadcast arcs."""
    S = _S(); c = S / 2
    img = canvas(ICON)
    arcs = canvas(ICON)
    da = ImageDraw.Draw(arcs)
    for k, r in enumerate((0.30, 0.40)):
        for a0 in (-50, 130):
            box = (c - S * r, c - S * r, c + S * r, c + S * r)
            da.arc(box, a0, a0 + 100, fill=INK, width=int(S * 0.06))
            da.arc((box[0] + S * 0.01, box[1] + S * 0.01, box[2] - S * 0.01, box[3] - S * 0.01), a0 + 3, a0 + 97, fill=MAGENTA, width=int(S * 0.035))
    img.alpha_composite(arcs)
    lay = eye(img, c, c, S * 0.44, S * 0.26, MAGENTA)
    return glow(img, lay, S * 0.03, 1.0)


def icon_corp_biotech():
    """Corporation logo, biotech profile: double helix in a hexagon."""
    S = _S(); c = S / 2
    img = canvas(ICON)
    green_d = (30, 120, 70, 255)
    outlined_poly(img, regular_poly(c, c, S * 0.44, 6, -math.pi / 2), green_d, S * INKW)
    poly(img, regular_poly(c, c, S * 0.37, 6, -math.pi / 2), DARK2)
    hel = canvas(ICON)
    dh = ImageDraw.Draw(hel)
    pts1, pts2 = [], []
    for i in range(61):
        t = i / 60
        y = S * 0.22 + t * S * 0.56
        x1 = c + math.sin(t * math.pi * 2.2) * S * 0.12
        x2 = c - math.sin(t * math.pi * 2.2) * S * 0.12
        pts1.append((x1, y)); pts2.append((x2, y))
        if i % 6 == 3:
            dh.line([(x1, y), (x2, y)], fill=with_alpha(GREEN, 200), width=int(S * 0.018))
    for pts in (pts1, pts2):
        thick_line(dh, pts, INK, S * 0.055)
    thick_line(dh, pts1, GREEN, S * 0.032)
    thick_line(dh, pts2, (150, 255, 190, 255), S * 0.032)
    img.alpha_composite(hel)
    return glow(img, hel, S * 0.02, 0.8)


ICONS = [
    ("Мемы Ideology", [
        ("Meme_MachineRevelation", "Машинное откровение", icon_meme_machine),
        ("Meme_NetAscension", "Сетевое восхождение", icon_meme_ascension),
        ("Meme_DigitalPurity", "Цифровая чистота", icon_meme_purity),
    ]),
    ("Вопросы и роль", [
        ("Issue_AI", "Вопрос «ИИ»", icon_issue_ai),
        ("Issue_Braindance", "Вопрос «Брейнданс»", icon_issue_braindance),
        ("Role_NetArchon", "Роль «Архонт сети»", icon_role_archon),
    ]),
    ("Команды раннеров (под цвет фракции)", [
        ("Crew_Claws", "Когти", icon_crew_claws),
        ("Crew_Mask", "Маски", icon_crew_mask),
        ("Crew_Bolt", "Разряд", icon_crew_bolt),
    ]),
    ("Корпорации (по профилю)", [
        ("Corp_Security", "Охранная", icon_corp_security),
        ("Corp_Media", "Медиа", icon_corp_media),
        ("Corp_Biotech", "Биотех", icon_corp_biotech),
    ]),
]


def build_icons():
    os.makedirs(os.path.join(ART, "Icons"), exist_ok=True)
    cell, pad, label_h = 160, 30, 40
    groups = len(ICONS)
    W = pad + 3 * (cell + pad) + 40
    W = max(W, 2 * (3 * (cell + pad)) + pad * 2)
    cols = 2
    gw = 3 * (cell + pad)
    H = 90 + ((groups + 1) // 2) * (cell + label_h + 80)
    sheet = Image.new("RGBA", (pad + cols * (gw + pad), H), (30, 30, 32, 255))
    d = ImageDraw.Draw(sheet)
    d.text((pad, 22), "Иконки к CONCEPTS_10 (256 px, стиль мода и HE)", font=font(26, True), fill=WHITE)
    for g, (gtitle, items) in enumerate(ICONS):
        gx = pad + (g % 2) * (gw + pad)
        gy = 80 + (g // 2) * (cell + label_h + 80)
        d.text((gx, gy), gtitle, font=font(18, True), fill=CYAN)
        for i, (fname, label, fn) in enumerate(items):
            icon = finish(fn(), ICON)
            icon.save(os.path.join(ART, "Icons", fname + ".png"))
            x = gx + i * (cell + pad)
            y = gy + 34
            tile = Image.new("RGBA", (cell, cell), (22, 22, 24, 255))
            tile.alpha_composite(icon.resize((cell - 16, cell - 16), Image.LANCZOS), (8, 8))
            sheet.alpha_composite(tile, (x, y))
            d.text((x + cell / 2 - d.textlength(label, font=font(15)) / 2, y + cell + 8), label, font=font(15), fill=(200, 205, 212, 255))
    sheet.convert("RGB").save(os.path.join(MOCK, "26-concepts10-icons.png"))


# ---------------------------------------------------------------- cyberspace geography scheme

NODE_TEX = os.path.join(ROOT, "Textures", "UI", "Netrun", "Cyberspace")


def tex(name, size):
    im = Image.open(os.path.join(NODE_TEX, name + ".png")).convert("RGBA")
    return im.resize((size, size), Image.LANCZOS)


def build_map():
    K = 2  # supersampling for the scheme
    W, H = 1500, 940
    MAPW = 1100
    img = Image.new("RGBA", (W * K, H * K), (10, 14, 18, 255))
    d = ImageDraw.Draw(img)
    # grid
    for x in range(0, MAPW * K, 40 * K):
        d.line([(x, 70 * K), (x, H * K)], fill=(20, 48, 36, 255) if (x // (40 * K)) % 5 else (28, 70, 50, 255), width=K)
    for y in range(70 * K, H * K, 40 * K):
        d.line([(0, y), (MAPW * K, y)], fill=(20, 48, 36, 255) if (y // (40 * K)) % 5 else (28, 70, 50, 255), width=K)

    # backbone layout: fixed points (map is static per game)
    P = {
        "home": (90, 520),
        "b1": (230, 470), "b2": (230, 650), "b3": (370, 380), "b4": (390, 560), "b5": (380, 760),
        "b6": (530, 260), "b7": (560, 470), "b8": (560, 660), "b9": (700, 170), "b10": (720, 380),
        "b11": (730, 560), "b12": (720, 800), "b13": (870, 260), "b14": (890, 470), "b15": (890, 680),
        "b16": (1010, 160), "b17": (1030, 380), "b18": (1030, 600), "b19": (1030, 820),
    }
    E = [("home", "b1"), ("home", "b2"), ("b1", "b3"), ("b1", "b4"), ("b2", "b4"), ("b2", "b5"), ("b3", "b6"), ("b3", "b7"),
         ("b4", "b7"), ("b4", "b8"), ("b5", "b8"), ("b6", "b9"), ("b6", "b10"), ("b7", "b10"), ("b7", "b11"), ("b8", "b11"),
         ("b8", "b12"), ("b9", "b13"), ("b10", "b13"), ("b10", "b14"), ("b11", "b14"), ("b11", "b15"), ("b12", "b15"),
         ("b13", "b16"), ("b13", "b17"), ("b14", "b17"), ("b14", "b18"), ("b15", "b18"), ("b15", "b19"), ("b16", "b17"), ("b18", "b19")]
    owner = {"b6": 0, "b9": 0, "b13": 0, "b16": 0, "b10": 0,
             "b11": 1, "b14": 1, "b15": 1, "b18": 1,
             "b12": 2, "b19": 2, "b5": 2}
    corp_col = [(70, 140, 230, 255), (255, 61, 139, 255), (60, 200, 110, 255)]
    exits = {"b4", "b10", "b15", "b17"}
    S = lambda p: (p[0] * K, p[1] * K)

    # territories: soft blobs
    terr = Image.new("RGBA", img.size, (0, 0, 0, 0))
    dt = ImageDraw.Draw(terr)
    for n, o in owner.items():
        x, y = S(P[n])
        r = 95 * K
        dt.ellipse((x - r, y - r, x + r, y + r), fill=with_alpha(corp_col[o], 60))
    terr = terr.filter(ImageFilter.GaussianBlur(30 * K))
    img.alpha_composite(terr)
    d = ImageDraw.Draw(img)

    # backbone edges
    for a, b in E:
        d.line([S(P[a]), S(P[b])], fill=(5, 8, 10, 255), width=9 * K)
        d.line([S(P[a]), S(P[b])], fill=(70, 200, 150, 255), width=4 * K)

    # intrusion path (red dashed) from b14 to home via b11, b7, b4, b1
    path = ["b14", "b11", "b7", "b4", "b1", "home"]
    for a, b in zip(path, path[1:]):
        (x0, y0), (x1, y1) = S(P[a]), S(P[b])
        L = math.hypot(x1 - x0, y1 - y0)
        n = int(L / (18 * K))
        for i in range(0, n, 2):
            t0, t1 = i / n, min(1, (i + 1) / n)
            d.line([(x0 + (x1 - x0) * t0, y0 + (y1 - y0) * t0), (x0 + (x1 - x0) * t1, y0 + (y1 - y0) * t1)], fill=RED, width=5 * K)

    # branches: (backbone node, icon, offset, burnt)
    branches = [
        ("b1", "Node_Market", (-20, -80), False), ("b2", "Node_Archive", (0, 75), False), ("b3", "Node_AbandonedServer", (-90, -40), False),
        ("b4", "Node_Market", (-70, 50), True), ("b7", "Node_Archive", (40, -70), False), ("b8", "Node_DarkExchange", (-10, 80), False),
        ("b9", "Node_CorpVault", (-80, -40), False), ("b12", "Node_CorpVault", (-80, 40), False), ("b11", "Node_DarkExchange", (60, 60), False),
        ("b17", "Node_Market", (60, -10), False), ("b16", "Node_CorpVault", (70, 10), False),
    ]
    for n, icon, (dx, dy), burnt in branches:
        x0, y0 = P[n]
        x1, y1 = x0 + dx, y0 + dy
        d.line([S((x0, y0)), S((x1, y1))], fill=(5, 8, 10, 255), width=6 * K)
        d.line([S((x0, y0)), S((x1, y1))], fill=(120, 140, 160, 255), width=2 * K)
        ic = tex(icon, 44 * K)
        if burnt:
            g = ic.convert("LA").convert("RGBA")
            g.putalpha(ic.getchannel("A").point(lambda v: v * 0.55))
            ic = g
        img.alpha_composite(ic, (int((x1 - 22) * K), int((y1 - 22) * K)))
        if burnt:
            d.line([S((x1 - 14, y1 - 14)), S((x1 + 14, y1 + 14))], fill=RED, width=4 * K)
            d.line([S((x1 - 14, y1 + 14)), S((x1 + 14, y1 - 14))], fill=RED, width=4 * K)

    # mini-zones: corp fortresses (dashed octagon) and a market quarter
    labels = []

    def zone(cx, cy, r, col, icons, label, below=False, dx=0):
        pts = octagon(cx * K, cy * K, r * K)
        pts = pts + [pts[0]]
        for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
            L = math.hypot(xb - xa, yb - ya)
            n = max(2, int(L / (14 * K)))
            for i in range(0, n, 2):
                t0, t1 = i / n, (i + 1) / n
                d.line([(xa + (xb - xa) * t0, ya + (yb - ya) * t0), (xa + (xb - xa) * t1, ya + (yb - ya) * t1)], fill=col, width=3 * K)
        for icon, (ox, oy), sz in icons:
            img.alpha_composite(tex(icon, sz * K), (int((cx + ox - sz / 2) * K), int((cy + oy - sz / 2) * K)))
        labels.append((cx + dx, cy + r + 6 if below else cy - r - 26, label, col))

    zone(830, 135, 58, corp_col[0], [("Node_CorpVault", (-26, 8), 38), ("Node_CorpVault", (26, 8), 38), ("Node_CorpCore", (0, -26), 40)], "Крепость Кобальта", below=True)
    zone(990, 520, 62, corp_col[1], [("Node_CorpVault", (-26, 14), 38), ("Node_CorpCore", (20, -18), 40)], "Крепость Сайрен-Медиа", below=True, dx=-80)
    zone(560, 860, 58, corp_col[2], [("Node_CorpVault", (-24, 0), 38), ("Node_CorpVault", (24, 0), 38)], "Крепость Гелиос-Био")
    zone(450, 150, 56, (230, 210, 120, 255), [("Node_Market", (-24, 10), 34), ("Node_Market", (24, 10), 34), ("Node_Market", (0, -22), 34)], "Рынок-квартал")
    # zone gates link to backbone
    for (zx, zy), n in [((830, 193), "b13"), ((990, 582), "b18"), ((560, 802), "b12"), ((470, 206), "b6")]:
        d.line([S((zx, zy)), S(P[n])], fill=(200, 210, 220, 255), width=2 * K)

    # backbone nodes
    for n, (x, y) in P.items():
        if n == "home":
            continue
        X, Y = S((x, y))
        r = 13 * K
        col = corp_col[owner[n]] if n in owner else (150, 165, 180, 255)
        if n in exits:
            d.rectangle((X - r - 3 * K, Y - r - 3 * K, X + r + 3 * K, Y + r + 3 * K), fill=INK)
            d.rectangle((X - r, Y - r, X + r, Y + r), fill=(20, 30, 40, 255), outline=CYAN, width=3 * K)
            d.line([(X - 5 * K, Y), (X + 6 * K, Y)], fill=CYAN, width=3 * K)
            d.polygon([(X + 7 * K, Y), (X + 1 * K, Y - 5 * K), (X + 1 * K, Y + 5 * K)], fill=CYAN)
        else:
            d.ellipse((X - r - 3 * K, Y - r - 3 * K, X + r + 3 * K, Y + r + 3 * K), fill=INK)
            d.ellipse((X - r, Y - r, X + r, Y + r), fill=(20, 30, 40, 255), outline=col, width=3 * K)

    # home node
    hx, hy = S(P["home"])
    img.alpha_composite(Image.open(os.path.join(ROOT, "Textures", "UI", "Netrun", "Gizmo_NetTerminal.png")).convert("RGBA").resize((70 * K, 70 * K), Image.LANCZOS), (hx - 35 * K, hy - 35 * K))
    d.text((hx - 22 * K, hy + 38 * K), "ДОМ", font=font(16 * K, True), fill=GREEN)

    # runner at an exit point
    rx, ry = S(P["b10"])
    d.polygon([(rx, ry - 18 * K), (rx - 12 * K, ry - 42 * K), (rx + 12 * K, ry - 42 * K)], fill=INK)
    d.polygon([(rx, ry - 22 * K), (rx - 8 * K, ry - 39 * K), (rx + 8 * K, ry - 39 * K)], fill=GREEN)
    d.text((rx + 14 * K, ry - 46 * K), "раннер", font=font(14 * K, True), fill=GREEN)

    # intrusion packet
    ix, iy = S(P["b11"])
    d.polygon([(ix, iy - 15 * K), (ix + 15 * K, iy), (ix, iy + 15 * K), (ix - 15 * K, iy)], fill=INK)
    d.polygon([(ix, iy - 11 * K), (ix + 11 * K, iy), (ix, iy + 11 * K), (ix - 11 * K, iy)], fill=RED)

    # node events on the backbone
    for n, icon in [("b8", "Event_Rival"), ("b14", "Event_HunterICE"), ("b3", "Event_DataStorm")]:
        x, y = P[n]
        img.alpha_composite(tex(icon, 30 * K), (int((x + 6) * K), int((y - 36) * K)))

    for lx_, ty, label, col in labels:
        f = font(15 * K, True)
        w = d.textlength(label, font=f)
        d.rectangle(((lx_ * K - w / 2 - 6 * K), ty * K, (lx_ * K + w / 2 + 6 * K), (ty + 20) * K), fill=(10, 14, 18, 235))
        d.text((lx_ * K - w / 2, ty * K), label, font=f, fill=col)

    # title
    d.rectangle((0, 0, W * K, 66 * K), fill=(16, 18, 22, 255))
    d.text((24 * K, 18 * K), "География киберпространства: магистраль, ответвления, мини-зоны (схема к CONCEPTS_10, раздел 10)", font=font(22 * K, True), fill=WHITE)

    # legend
    lx = (MAPW + 20) * K
    d.rectangle((MAPW * K, 66 * K, W * K, H * K), fill=(18, 20, 24, 255))
    y = 90
    def leg(draw_fn, text, sub=None):
        nonlocal y
        draw_fn(lx + 16 * K, (y + 12) * K)
        d.text((lx + 44 * K, y * K), text, font=font(16 * K, True), fill=WHITE)
        if sub:
            d.text((lx + 44 * K, (y + 22) * K), sub, font=font(13 * K), fill=(160, 168, 180, 255))
            y += 20
        y += 40

    def dot(col):
        return lambda x, yy: (d.ellipse((x - 12 * K, yy - 12 * K, x + 12 * K, yy + 12 * K), fill=INK), d.ellipse((x - 9 * K, yy - 9 * K, x + 9 * K, yy + 9 * K), fill=(20, 30, 40, 255), outline=col, width=3 * K))

    def line(col, dashed=False):
        def f(x, yy):
            if dashed:
                for i in range(0, 4, 2):
                    d.line([(x - 14 * K + i * 7 * K, yy), (x - 7 * K + i * 7 * K, yy)], fill=col, width=5 * K)
            else:
                d.line([(x - 14 * K, yy), (x + 14 * K, yy)], fill=col, width=4 * K)
        return f

    def square_exit(x, yy):
        d.rectangle((x - 11 * K, yy - 11 * K, x + 11 * K, yy + 11 * K), fill=(20, 30, 40, 255), outline=CYAN, width=3 * K)
        d.polygon([(x + 6 * K, yy), (x - 1 * K, yy - 5 * K), (x - 1 * K, yy + 5 * K)], fill=CYAN)

    def icon_at(name, size=28):
        return lambda x, yy: img.alpha_composite(tex(name, size * K), (int(x - size / 2 * K), int(yy - size / 2 * K)))

    leg(line((70, 200, 150, 255)), "Магистраль", "статична всю игру")
    leg(dot((150, 165, 180, 255)), "Узел магистрали", "события при проходе")
    leg(square_exit, "Точка выхода", "чистый выход, отсюда можно зайти")
    leg(icon_at("Node_Archive"), "Ответвление", "цель нырка; не исчезает,")
    y -= 18
    d.text((lx + 44 * K, y * K), "меняет состояние и тип", font=font(13 * K), fill=(160, 168, 180, 255))
    y += 30
    leg(icon_at("Node_Market"), "Выжженное ответвление", "2–4 дня, потом новое содержимое")
    leg(icon_at("Node_CorpCore"), "Мини-зона", "крепость: длинный нырок, ядро")
    leg(dot(corp_col[0]), "Территория корпорации", "цвет = владелец, Тревога 0–100")
    leg(line(RED, dashed=True), "Путь вторжения", "пакет идёт к Дому, 2 ч на переход")
    leg(icon_at("Event_Rival"), "Событие узла", "соперник, охотник ICE, шторм")
    leg(lambda x, yy: d.polygon([(x, yy + 10 * K), (x - 9 * K, yy - 10 * K), (x + 9 * K, yy - 10 * K)], fill=GREEN), "Позиция раннера", "запоминается между нырками")
    d.text((lx, (H - 60) * K), "Названия корпораций генерируются,", font=font(13 * K), fill=(140, 148, 160, 255))
    d.text((lx, (H - 40) * K), "здесь для примера.", font=font(13 * K), fill=(140, 148, 160, 255))

    img.resize((W, H), Image.LANCZOS).convert("RGB").save(os.path.join(MOCK, "27-cyberspace-geography.png"))


if __name__ == "__main__":
    os.makedirs(ART, exist_ok=True)
    build_frames()
    build_icons()
    build_map()
    print("done")
