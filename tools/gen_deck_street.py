#!/usr/bin/env python3
"""
Textures of the Street deck theme (Maxim's reference and picks, 2026-10-15; the CRT variant is archived in Reserve): a yellow industrial panel with cut
corners in a black housing, olive side rails, a neon tube in a raised housing on top (the Heat), a brushed title plate,
keys with neon outlines, an RF meter, a holographic kitsune sticker over the right edge and 夜の走者 printed down the
right side.

    python tools/gen_deck_street.py                 # textures into Textures/UI/Netrun/Frame/Street/
    python tools/gen_deck_street.py --mock OUT.png  # a composite at the default terminal size

The layout numbers below are mirrored in Source/Terminal/DeckStreet.cs; change both together.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

import deck_art as A
import deck_stickers as S

T = "Street"

PANEL_W, PANEL_H = 1400, 900
IN_L, IN_T, IN_R, IN_B = 34, 86, 74, 66
SLICE = 150
CHAMFER = 34
HOUSING = 7
OUT_L, OUT_T, OUT_R, OUT_B = 16, 16, 30, 12

YELLOW_TOP, YELLOW_BOT = A.hexc(0xd9a92b), A.hexc(0xbd8d20)
PRIMER = A.hexc(0x34343a)


def scratches(w, h, count, seed, area=None, length=(6, 40), vertical=0.15):
    r = A.rng(seed)

    def draw(d, s):
        for _ in range(count):
            x, y = r.integers(0, w), r.integers(0, h)
            if area is not None and area[min(h - 1, y), min(w - 1, x)] < 0.5:
                continue
            ln = r.integers(*length)
            ang = r.normal(0, 0.5) + (math.pi / 2 if r.random() < vertical else 0)
            d.line([x * s, y * s, (x + math.cos(ang) * ln) * s, (y + math.sin(ang) * ln) * s], fill=int(r.integers(80, 200)), width=s)
    return A.mask((w, h), draw)


def make_lcd():
    W, H = 200, 56
    outer = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 7 * s, fill=255))
    inner = A.mask((W, H), lambda d, s: d.rounded_rectangle([6 * s, 6 * s, (W - 6) * s - 1, (H - 6) * s - 1], 3 * s, fill=255))
    rgb = A.hexc(0x121014) + A.grain(W, H, 351, 3)[..., None] + (A.shade(outer, 1.3) * 50)[..., None]
    glass = A.gradient_v(W, H, A.hexc(0x08150c), A.hexc(0x040a06)) - (A.inner_edge(inner, 3) * 16)[..., None]
    rgb = rgb * (1 - inner[..., None]) + glass * inner[..., None]
    return A.to_img(rgb, outer)


def make_led(color):
    """A small 5 mm LED, 12 px: off is a dark dome, lit has a hot core (the glow is code)."""
    n = 12
    m = A.mask((n, n), lambda d, s: d.ellipse([1 * s, 1 * s, (n - 1) * s - 1, (n - 1) * s - 1], fill=255))
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    r = np.hypot(xx - 5.5, yy - 5.5) / 5
    if color is None:
        rgb = A.hexc(0x1d1822) + ((1 - r) * 14)[..., None]
    else:
        c = A.hexc(color)
        rgb = c * (0.6 + 0.4 * np.clip(1 - r, 0, 1))[..., None] + (np.clip(1 - r * 2, 0, 1) * 110)[..., None]
    rgb += (np.clip(1 - np.hypot(xx - 4, yy - 4) / 1.8, 0, 1) * 120)[..., None]
    return A.to_img(rgb, m)


def fake_screen(img, x0, y0, x1, y1):
    d = ImageDraw.Draw(img)
    d.rectangle([x0, y0, x1 - 1, y1 - 1], fill=(6, 10, 8, 255))
    f = ImageFont_mono(13)
    lines = [("NAVIGATION // CORE", (90, 210, 130)), ("> HOME NODE ........ ONLINE", (90, 210, 130)),
             ("> GATEWAY .......... 5 NODES", (90, 210, 130)), ("> HEAT 58 .......... TRACKED", (230, 190, 70))]
    for i, (t, c) in enumerate(lines):
        d.text((x0 + 30, y0 + 28 + i * 22), t, font=f, fill=c)
    cx, cy = x1 - 200, (y0 + y1) / 2
    for r in (60, 120):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(40, 120, 70), width=1)
    for yy in range(y0, y1, 3):
        d.line([x0, yy, x1, yy], fill=(0, 0, 0, 40))


def ImageFont_mono(size):
    from PIL import ImageFont
    try:
        return ImageFont.truetype("consola.ttf", size)
    except OSError:
        return A.font(size)


def chamfer_poly(x0, y0, x1, y1, c, s=1):
    return [((x0 + c) * s, y0 * s), ((x1 - c) * s, y0 * s), (x1 * s, (y0 + c) * s), (x1 * s, (y1 - c) * s),
            ((x1 - c) * s, y1 * s), ((x0 + c) * s, y1 * s), (x0 * s, (y1 - c) * s), (x0 * s, (y0 + c) * s)]


def screw(d=9, seed=0):
    img = A.rivet(d, A.hexc(0x6b6d72), seed=seed, dome=False)
    dr = ImageDraw.Draw(img)
    c = img.width / 2
    a = A.rng(seed).uniform(0, math.pi)
    for ang in (a, a + math.pi / 2):
        dx, dy = math.cos(ang) * d * 0.32, math.sin(ang) * d * 0.32
        dr.line([c - dx, c - dy, c + dx, c + dy], fill=(30, 30, 34, 230), width=1)
    return img


def make_panel():
    W, H = PANEL_W, PANEL_H
    outer = A.mask((W, H), lambda d, s: d.polygon(chamfer_poly(0, 0, W - 1, H - 1, CHAMFER, s), fill=255))
    face = A.mask((W, H), lambda d, s: d.polygon(chamfer_poly(HOUSING, HOUSING, W - 1 - HOUSING, H - 1 - HOUSING, CHAMFER - 3, s), fill=255))
    hole_box = (IN_L, IN_T, W - IN_R, H - IN_B)

    def grown(g, r):
        return A.mask((W, H), lambda d, s: d.rounded_rectangle(
            [(hole_box[0] - g) * s, (hole_box[1] - g) * s, (hole_box[2] + g) * s - 1, (hole_box[3] + g) * s - 1], r * s, fill=255))
    hole = grown(0, 4)
    bezel = grown(14, 14)

    # the black housing around the face
    rgb = A.hexc(0x16171a) + A.grain(W, H, 501, 3)[..., None] + (A.shade(outer, 2.0) * 55)[..., None]
    # the yellow face: worn paint over dark primer, grime in the corners and at the bottom
    paint = A.gradient_v(W, H, YELLOW_TOP, YELLOW_BOT)
    paint += (A.fbm(W, H, 140, 4, 502) * 18)[..., None] * np.array([1.0, 0.9, 0.5], np.float32)
    paint += A.grain(W, H, 503, 3)[..., None]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    corner = np.clip(1 - np.minimum(np.minimum(xx, W - xx), np.minimum(yy, H - yy)) / 60, 0, 1)
    grime = np.clip(A.fbm(W, H, 40, 3, 504) * 2 + 0.15, 0, 1) * (0.25 + corner * 0.7)
    paint *= (1 - 0.32 * grime)[..., None]
    paint *= (1 - 0.12 * np.clip((yy / H - 0.86) / 0.14, 0, 1))[..., None]
    face_only = face * (1 - bezel)
    edge = A.inner_edge(face, 8)
    chips = np.clip((A.fbm(W, H, 7, 3, 505) + edge * 0.38 - 0.49) * 10, 0, 1) * face_only
    primer = PRIMER + A.grain(W, H, 506, 4)[..., None]
    paint = paint * (1 - chips[..., None]) + primer * chips[..., None]
    paint += (A.shade(chips, 0.8, light=(1, 1)) * 20)[..., None]
    sc = scratches(W, H, 520, 507, area=face_only) * face_only
    paint += (sc * 32)[..., None] * np.array([1.0, 1.0, 0.85], np.float32)
    paint -= (np.roll(sc, 1, 0) * (1 - sc) * 22)[..., None]
    paint += (A.shade(face, 2.0) * 60)[..., None]
    rgb = rgb * (1 - face[..., None]) + paint * face[..., None]

    # olive side rails, recessed, along both outer edges
    for x0, x1 in ((12, 25), (W - 25, W - 12)):
        rail = A.mask((W, H), lambda d, s, x0=x0, x1=x1: d.rounded_rectangle([x0 * s, 128 * s, x1 * s, (H - 128) * s], 5 * s, fill=255))
        rrgb = A.hexc(0x3c4430) + A.grain(W, H, 508 + x0, 3)[..., None] + A.streaks(W, H, "y", 6, 509 + x0)[..., None]
        rrgb += (A.shade(1 - rail, 1.6) * 40)[..., None]
        rgb = rgb * (1 - rail[..., None]) + rrgb * rail[..., None]
        rgb -= (np.clip(A.blur(rail, 2.5) - rail, 0, 1) * 50)[..., None] * (1 - rail[..., None])

    # the black screen bezel with a lip
    band = bezel * (1 - hole)
    brgb = A.hexc(0x111215) + A.grain(W, H, 510, 2)[..., None] + (A.shade(bezel, 1.6) * 40)[..., None]
    brgb += (A.shade(1 - hole, 2.0, light=(1, 1)) * 30)[..., None]
    rgb = rgb * (1 - band[..., None]) + brgb * band[..., None]
    rgb -= (np.clip(A.blur(bezel, 3) - bezel, 0, 1) * 60)[..., None] * (1 - bezel[..., None])

    img = A.to_img(rgb, outer * (1 - hole))
    for (x, y) in ((30, 30), (W - 30, 30), (30, H - 30), (W - 30, H - 30)):
        A.over(img, screw(10, seed=x * 7 + y), (x - 7, y - 7))
    for x in (18.5, W - 18.5):
        for y in (140, H - 140):
            A.over(img, screw(7, seed=int(x) + y), (x - 5.5, y - 5.5))
    return img


def make_neon():
    """The raised housing of the neon tube on the top edge (3-slice, borders 48); the light is drawn in code."""
    W, H = 440, 38
    m = A.mask((W, H), lambda d, s: d.polygon([(26 * s, 0), ((W - 26) * s, 0), (W * s, H * s), (0, H * s)], fill=255))
    rgb = A.hexc(0x2a2c30) + A.streaks(W, H, "x", 6, 521)[..., None] + (A.shade(m, 1.8) * 60)[..., None]
    slot = A.mask((W, H), lambda d, s: d.rounded_rectangle([44 * s, 11 * s, (W - 44) * s, 25 * s], 6 * s, fill=255))
    srgb = A.hexc(0x060708) + (A.shade(1 - slot, 1.4, light=(1, 1)) * 30)[..., None]
    rgb = rgb * (1 - slot[..., None]) + srgb * slot[..., None]
    shadow = np.roll(A.blur(m, 2), 2, 0) * 0.5
    img = A.to_img(np.zeros((H, W, 3), np.float32), shadow)
    img.alpha_composite(A.to_img(rgb, m))
    for x in (34, W - 34):
        A.over(img, screw(7, seed=x), (x - 5.5, 18 - 5.5))
    return img


def make_plate():
    W, H = 340, 28
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 4 * s, fill=255))
    rgb = A.hexc(0x9a9ca0) + A.streaks(W, H, "x", 14, 531)[..., None] + (A.shade(m, 1.4) * 70)[..., None]
    rgb += (np.linspace(18, -10, H)[:, None, None])
    img = A.to_img(rgb, m)
    f = A.font(13)
    d = ImageDraw.Draw(img)
    d.text((W / 2, H / 2 + 1), "NETRUN // NIGHT RUNNER · CUSTOM BUILD", font=f, fill=(255, 255, 255, 90), anchor="mm")
    d.text((W / 2, H / 2), "NETRUN // NIGHT RUNNER · CUSTOM BUILD", font=f, fill=(34, 34, 38, 230), anchor="mm")
    for x in (7, W - 7):
        A.over(img, screw(5, seed=x), (x - 4.5, H / 2 - 4.5))
    shadow = Image.new("RGBA", (W + 4, H + 4), (0, 0, 0, 0))
    shadow.alpha_composite(A.to_img(np.zeros((H, W, 3), np.float32), A.blur(m, 1.5) * 0.6), dest=(2, 3))
    shadow.alpha_composite(img, dest=(1, 1))
    return shadow


def make_key(outline=None):
    """A key with a black rim and a dark face (9-slice, borders 10); a neon outline when coloured."""
    W, H = 88, 32
    rim = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, (H - 2) * s - 1], 6 * s, fill=255))
    face = A.mask((W, H), lambda d, s: d.rounded_rectangle([3 * s, 3 * s, (W - 3) * s - 1, (H - 5) * s - 1], 4 * s, fill=255))
    rgb = A.hexc(0x0b0b0d) + (A.shade(rim, 1.2) * 40)[..., None]
    frgb = A.gradient_v(W, H, A.hexc(0x34363b), A.hexc(0x222428)) + A.grain(W, H, 541, 2)[..., None] + (A.shade(face, 1.0) * 40)[..., None]
    if outline is not None:
        c = A.hexc(outline)
        ring = np.clip(face - A.mask((W, H), lambda d, s: d.rounded_rectangle([5 * s, 5 * s, (W - 5) * s - 1, (H - 7) * s - 1], 3 * s, fill=255)), 0, 1)
        inner_glow = np.clip(A.blur(ring, 2.5) * 1.2, 0, 1) * face
        frgb = frgb * (1 - ring[..., None]) + c * ring[..., None]
        frgb += c * inner_glow[..., None] * 0.25
    rgb = rgb * (1 - face[..., None]) + frgb * face[..., None]
    shadow = np.roll(A.blur(rim, 1.5), 2, 0) * 0.55
    img = A.to_img(np.zeros_like(rgb), shadow)
    img.alpha_composite(A.to_img(rgb, rim))
    return img


def make_meter():
    W, H = 120, 34
    rim = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 6 * s, fill=255))
    inner = A.mask((W, H), lambda d, s: d.rounded_rectangle([4 * s, 4 * s, (W - 4) * s - 1, (H - 4) * s - 1], 3 * s, fill=255))
    rgb = A.hexc(0x0d0d10) + (A.shade(rim, 1.3) * 45)[..., None]
    glass = A.gradient_v(W, H, A.hexc(0x071214), A.hexc(0x03080a)) - (A.inner_edge(inner, 3) * 12)[..., None]
    rgb = rgb * (1 - inner[..., None]) + glass * inner[..., None]
    return A.to_img(rgb, rim)


def make_lamp_plate(W=180, H=44):
    """The lamps' plate (Maxim: the lamps on bare yellow looked plain): a chamfered black plate with screws in the
    corners and a neon hairline, 9-sliced with borders 14."""
    m = A.mask((W, H), lambda d, s: d.polygon(chamfer_poly(0, 0, W - 1, H - 1, 7, s), fill=255))
    rgb = A.hexc(0x16161a) + A.grain(W, H, 581, 3)[..., None] + (A.shade(m, 1.6) * 50)[..., None]
    rgb += scratches(W, H, 10, 582)[..., None] * 18
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 2), 2, 0) * 0.6)
    img.alpha_composite(A.to_img(rgb, m))
    d = ImageDraw.Draw(img)
    d.line([12, H - 6, W - 13, H - 6], fill=(63, 224, 230, 150), width=1)
    for i, (x, y) in enumerate(((7, 7), (W - 8, 7), (7, H - 8), (W - 8, H - 8))):
        sc = screw(7, seed=583 + i)
        img.alpha_composite(sc, (int(x - sc.width / 2), int(y - sc.height / 2)))
    return img


def make_led_ring(n=20):
    """A chrome bezel for a 12 px LED (the LED is drawn inside it)."""
    m = A.mask((n, n), lambda d, s: d.ellipse([0, 0, n * s - 1, n * s - 1], fill=255))
    hole = A.mask((n, n), lambda d, s: d.ellipse([4 * s, 4 * s, (n - 4) * s - 1, (n - 4) * s - 1], fill=255))
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    rgb = A.hexc(0x9a9ca2) + ((1 - yy / n) * 60)[..., None] + (A.shade(m, 1.2) * 60)[..., None]
    return A.to_img(rgb, m * (1 - hole) + hole * 0.0)


def make_switch_plate():
    """NET LINK: a gunmetal plate with hazard ends and a slot for the sliding knob, 128 x 40."""
    W, H = 128, 40
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([0, 0, W * s - 1, H * s - 1], 5 * s, fill=255))
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    rgb = A.hexc(0x2a2c30) + A.streaks(W, H, "x", 5, 551)[..., None]
    ends = ((xx < 22) | (xx > W - 22))
    stripes = (((xx + yy) // 7).astype(int) % 2 == 0)
    haz = np.where(stripes[..., None], np.array([226, 182, 44], np.float32), np.array([20, 20, 22], np.float32))
    rgb = np.where(ends[..., None], haz, rgb)
    rgb += (A.shade(m, 1.4) * 55)[..., None]
    slot = A.mask((W, H), lambda d, s: d.rounded_rectangle([30 * s, 22 * s, (W - 30) * s, 29 * s], 3 * s, fill=255))
    rgb = rgb * (1 - slot[..., None]) + (A.hexc(0x060607) + (A.shade(1 - slot, 1.0, light=(1, 1)) * 30)[..., None]) * slot[..., None]
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 1.6), 2, 0) * 0.55)
    img.alpha_composite(A.to_img(rgb, m))
    f = A.font(8)
    d = ImageDraw.Draw(img)
    d.text((W / 2, 11), "NET LINK", font=f, fill=(230, 230, 226, 200), anchor="mm")
    d.text((33, 35), "OFF", font=A.font(6), fill=(200, 200, 196, 150), anchor="lm")
    d.text((W - 33, 35), "ON", font=A.font(6), fill=(200, 200, 196, 150), anchor="rm")
    return img


def make_knob():
    W, H = 18, 22
    m = A.mask((W, H), lambda d, s: d.rounded_rectangle([1 * s, 1 * s, (W - 1) * s - 1, (H - 1) * s - 1], 4 * s, fill=255))
    rgb = A.gradient_v(W, H, A.hexc(0x3a3c40), A.hexc(0x16171a)) + (A.shade(m, 1.2) * 60)[..., None]
    for y in (7, 10, 13):
        rgb[y:y + 1, 4:W - 4] = A.hexc(0x0c0c0e)
        rgb[y + 1:y + 2, 4:W - 4] += 20
    img = A.to_img(np.zeros_like(rgb), np.roll(A.blur(m, 1.5), 2, 0) * 0.6)
    img.alpha_composite(A.to_img(rgb, m))
    return img


def make_kanji():
    """夜の走者 down the right side: cream letters with a pink shadow on a sprayed black band, so they read on the
    yellow (Maxim, 2026-10-15)."""
    W, H = 50, 204
    r = A.rng(561)

    def band(d, s):
        left = [(4 + r.uniform(-1.5, 1.5), y) for y in np.linspace(0, H, 14)]
        right = [(W - 4 + r.uniform(-1.5, 1.5), y) for y in np.linspace(H, 0, 14)]
        d.polygon([(x * s, y * s) for x, y in left + right], fill=255)
    m = A.mask((W, H), band)
    m *= 0.9 + 0.1 * np.clip(A.fbm(W, H, 6, 2, 562) * 2 + 0.5, 0, 1)
    over = np.clip(A.blur(m, 2.0) * 0.5 - m, 0, 1) * (A.rng(563).random((H, W)) > 0.5)
    img = A.to_img(np.broadcast_to(np.array(S.INK, np.float32), (H, W, 3)).copy(), np.clip(m + over * 0.6, 0, 1))
    d = ImageDraw.Draw(img)
    f = S.jfont(36)
    for i, ch in enumerate("夜の走者"):
        y = 27 + i * 50
        d.text((W / 2 + 1.5, y + 1.5), ch, font=f, fill=S.PINK + (230,), anchor="mm")
        d.text((W / 2, y), ch, font=f, fill=(246, 238, 216, 245), anchor="mm")
    return img


def build():
    A.save(make_panel(), T, "Panel")
    A.save(make_neon(), T, "Neon")
    A.save(make_plate(), T, "Plate")
    A.save(make_key(), T, "Key")
    A.save(make_key(0xff4f9e), T, "Key_Pink")
    A.save(make_key(0x3fe0e6), T, "Key_Cyan")
    A.save(make_key(0xe8384f), T, "Key_Red")
    A.save(make_meter(), T, "Meter")
    for name, col in (("Off", None), ("Green", 0x3fd07a), ("Pink", 0xff4f9e), ("Cyan", 0x3fe0e6), ("Orange", 0xf08a2e)):
        A.save(make_led(col), T, "Led_" + name)
    A.save(make_lcd(), T, "Lcd")
    A.save(make_switch_plate(), T, "SwitchPlate")
    A.save(make_lamp_plate(), T, "LampPlate")
    A.save(make_led_ring(), T, "LedRing")
    A.save(S.bolt(22, seed=585), T, "St_Bolt")
    A.save(S.tape(46, 13, seed=586).rotate(24, resample=Image.BICUBIC, expand=True), T, "Tape")
    A.save(make_knob(), T, "Knob")
    A.save(make_kanji(), T, "Kanji")
    A.save(S.slashes(35, 56, seed=564), T, "Slashes")
    A.save(S.kitsune(58, holo=True, seed=571).rotate(-14, resample=Image.BICUBIC, expand=True), T, "St_Kitsune")
    A.save(S.pixel_skull(22, seed=572), T, "St_Skull")
    A.save(S.graffiti("GH0ST", 22, (24, 20, 30), S.PINK, seed=575, drips=6), T, "Graffiti_Top")
    A.save(S.graffiti("NR", 34, S.PINK, S.INK, seed=576, skew=0.12, angle=5, bubble=True, drips=4), T, "Graffiti_Bottom")
    A.save(mock(None).resize((300, 240), Image.LANCZOS), T, "Preview")


# ------------------------------------------------------------------ the layout (mirrored in DeckStreet.cs)

PLATE_X, PLATE_Y = 33, 4
KEY_Y, KEY_H, KEY_GAP = 37, 32, 10
KEY_W = (82, 82, 92, 104)
METER_Y, METER_W, METER_H = 36, 230, 34
NEON_Y, NEON_H, NEON_FROM = -14, 38, 0.52      # the housing starts at 52% of the width, ends IN_R + 14 from the right
TUBE_INSET, TUBE_Y0, TUBE_Y1 = 46, 14, 22
BTN_W, BTN_H, BTN_STEP, BTN_Y = 46, 34, 54, 20
SWITCH_Y, LCD_Y, LCD_H, LCD_MAX = 13, 5, 54, 520


def mock(out_path=None, screen=(930, 666), heat=58, online=True):
    W = screen[0] + OUT_L + OUT_R + IN_L + IN_R
    H = screen[1] + OUT_T + OUT_B + IN_T + IN_B

    def load(n):
        return Image.open(os.path.join(A.FRAME, T, n + ".png")).convert("RGBA")
    img = Image.new("RGBA", (W, H), (14, 13, 16, 255))
    bx0, by0, bx1, by1 = OUT_L, OUT_T, W - OUT_R, H - OUT_B
    bw, bh = bx1 - bx0, by1 - by0
    d = ImageDraw.Draw(img)
    # the neon housing stands behind the top edge and over it
    nx0, nx1 = bx0 + round(bw * NEON_FROM), bx1 - IN_R - 14
    neon = A.nine_slice(load("Neon"), (int(nx1 - nx0), NEON_H), 48, 0, 48, 0)
    A.over(img, A.nine_slice(load("Panel"), (bw, bh), SLICE, SLICE, SLICE, SLICE), (bx0, by0))
    A.over(img, neon, (nx0, by0 + NEON_Y))
    # the tube: lit to the Heat, cyan going to pink and red as it rises
    t0, t1 = nx0 + TUBE_INSET, nx1 - TUBE_INSET
    ty0, ty1 = by0 + NEON_Y + TUBE_Y0, by0 + NEON_Y + TUBE_Y1
    lit = t0 + (t1 - t0) * heat / 100
    col = (63, 224, 230) if heat < 50 else (255, 79, 158) if heat < 75 else (232, 56, 79)
    d.rounded_rectangle([t0, ty0, t1, ty1], 4, fill=(26, 22, 30, 255))
    A.glow(img, (t0, ty0, lit, ty1), col, 0.7, 6)
    d.rounded_rectangle([t0, ty0, lit, ty1], 4, fill=col + (255,))
    d.rounded_rectangle([t0 + 3, ty0 + 2, lit - 3, ty0 + 4], 1, fill=(255, 255, 255, 170))

    sx0, sy0, sx1, sy1 = bx0 + IN_L, by0 + IN_T, bx1 - IN_R, by1 - IN_B
    fake_screen(img, sx0, sy0, sx1, sy1)
    plate = load("Plate")
    A.over(img, plate, (bx0 + PLATE_X, by0 + PLATE_Y))
    # graffiti on the top bar between the plate and the neon housing
    g = load("Graffiti_Top")
    gx0, gx1 = bx0 + PLATE_X + plate.width + 10, nx0 - 6
    if gx1 - gx0 >= g.width:
        A.over(img, g, (gx0 + (gx1 - gx0 - g.width) / 2, by0 - 2))
    # keys
    x = bx0 + 34
    f = A.font(12, bold=False)
    keys = (("CLOSE", "Key_Pink", (255, 120, 180)), ("MENU", "Key", (220, 220, 220)), ("ACHIEV", "Key_Cyan", (120, 235, 240)),
            ("NET-LOCK", "Key", (220, 220, 220)))
    for (label, tex, ink), kw in zip(keys, KEY_W):
        A.over(img, A.nine_slice(load(tex), (kw, KEY_H), 10, 10, 10, 10), (x, by0 + KEY_Y))
        d.text((x + kw / 2, by0 + KEY_Y + 15), label, font=f, fill=ink, anchor="mm")
        x += kw + KEY_GAP
    # RF meter
    mx = x + 12
    my = by0 + METER_Y
    A.over(img, A.nine_slice(load("Meter"), (METER_W, METER_H), 10, 10, 10, 10), (mx, my))
    d.text((mx + 10, my + 21), "RF signal", font=A.font(9, bold=False), fill=(90, 220, 230), anchor="lm")
    for i in range(18):
        x2 = mx + 66 + i * 8.5
        c = (63, 224, 230) if i < 10 else (90, 230, 130)
        d.rectangle([x2, my + 7, x2 + 5, my + 27], fill=c + ((255,) if i < 13 else (40,)))
    for i in range(4):
        h = 3 + i * 2.5
        d.rectangle([mx + 12 + i * 5, my + 13 - h, mx + 15 + i * 5, my + 13], fill=(90, 220, 230))
    # LEDs on a black plate in chrome rings, a strip of tape over its corner, a bolt sticker after it
    lx = mx + METER_W + 20
    plate = (lx + 4, my - 5, 180, 44)
    A.over(img, A.nine_slice(load("LampPlate"), plate[2:], 14, 14, 14, 14), plate[:2])
    for i, (lab, led, c) in enumerate((("NET", "Led_Cyan", (63, 224, 230)), ("SICK", "Led_Off", None), ("POD", "Led_Green", (63, 208, 122)))):
        cx = plate[0] + 32 + i * 58
        A.over(img, load("LedRing"), (cx - 10, plate[1] + 4))
        if c:
            A.glow(img, (cx - 3, plate[1] + 11, cx + 3, plate[1] + 17), c, 0.6, 4)
        A.over(img, load(led), (cx - 6, plate[1] + 8))
        d = ImageDraw.Draw(img)
        d.text((cx, plate[1] + 31), lab, font=A.font(10, bold=False), fill=(63, 224, 230) if i != 1 else (255, 79, 158), anchor="mm")
    A.over(img, load("Tape"), (plate[0] - 12, plate[1] - 10))
    A.over(img, load("St_Bolt"), (plate[0] + plate[2] + 8, plate[1] - 2))
    # the right column: the tilted kitsune over the edge, the kanji band, three slashes below the middle, a skull
    A.over(img, load("Kanji"), (bx1 - 62, by0 + 104))
    A.over(img, load("Slashes"), (bx1 - 60, by0 + round(bh * 0.6)))
    A.over(img, load("St_Kitsune"), (bx1 - 84, by0 - 2))
    A.over(img, load("St_Skull"), (bx1 - 58, by1 - 62))
    # the bottom bar
    yb = by1 - IN_B
    A.over(img, load("SwitchPlate"), (bx0 + 34, yb + SWITCH_Y))
    kx = bx0 + 34 + (89 if online else 39) - 9
    A.over(img, load("Knob"), (kx, yb + SWITCH_Y + 14))
    if online:
        A.glow(img, (bx0 + 34 + 104, yb + SWITCH_Y + 30, bx0 + 34 + 110, yb + SWITCH_Y + 34), (63, 208, 122), 0.5, 4)
    btn_x = bx1 - IN_R - 3 * BTN_STEP + 6
    lcd_x = bx0 + 34 + 128 + 20
    lcd_w = min(LCD_MAX, btn_x - 24 - lcd_x)
    A.over(img, A.nine_slice(load("Lcd"), (lcd_w, LCD_H), 12, 12, 12, 12), (lcd_x, yb + LCD_Y))
    fm = ImageFont_mono(12)
    for i, t in enumerate(("CYBERDATA 491", "HEAT 2 - SHADOW", "D8 08:27 NET ON")):
        d.text((lcd_x + 12, yb + LCD_Y + 7 + i * 14), t, font=fm, fill=(110, 230, 140))
    import random
    rnd = random.Random(7)
    cols = int((lcd_w - 12 - 210 - 60) / 21)
    for i, sk in enumerate((" .-. ", "(x.x)", " |=| ")):
        hexrow = " ".join("%02X" % rnd.randrange(256) for _ in range(max(0, cols)))
        d.text((lcd_x + 6 + 210 + 4, yb + LCD_Y + 7 + i * 14), hexrow, font=fm, fill=(110, 230, 140, 90))
        d.text((lcd_x + lcd_w - 6 - 50, yb + LCD_Y + 7 + i * 14), sk, font=fm, fill=(110, 230, 140))
    for i, gl in enumerate(("<", "O", "?")):
        A.over(img, A.nine_slice(load("Key"), (BTN_W, BTN_H), 10, 10, 10, 10), (btn_x + i * BTN_STEP, yb + BTN_Y))
        d.text((btn_x + i * BTN_STEP + 23, yb + BTN_Y + 16), gl, font=A.font(14), fill=(225, 225, 225), anchor="mm")
    # graffiti on the bottom bar between the LCD and the buttons
    g = load("Graffiti_Bottom")
    gx0, gx1 = lcd_x + lcd_w + 8, btn_x - 8
    if gx1 - gx0 >= g.width:
        A.over(img, g, (gx0 + (gx1 - gx0 - g.width) / 2, by1 - 58))
    if out_path:
        img.convert("RGB").save(out_path)
    return img


if __name__ == "__main__":
    if len(sys.argv) > 2 and sys.argv[1] == "--mock":
        mock(sys.argv[2])
    else:
        build()
        print("Street textures written")
