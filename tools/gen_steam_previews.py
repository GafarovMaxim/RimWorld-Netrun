"""Steam Workshop preview images, 1920 x 1080, in the spirit of the Vanilla Expanded previews but in Netrun's look:
a full-bleed scene, a dark caption plate at the bottom with a glowing title and two or three lines, the NETRUN logo in
the corner, scanlines and a neon rule along the bottom edge.

Sources: game screenshots from the Preview folder outside the repo (`--src`, default the Reserve path) and the frame and
UI mockups in docs/. Writes docs/steam/previews/<lang>_<name>.png (and a 960 x 540 copy for a quick look).

    python tools/gen_steam_previews.py [--lang ru|en] [--only terminal,cyberspace,club]
"""
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
SRC = r"G:\Modding\RimWorld\Hacking Expansion Netrun\Preview"
OUT = os.path.join(ROOT, "docs", "steam", "previews")
MOCK = os.path.join(ROOT, "docs", "mockups")
UI = os.path.join(ROOT, "docs", "ui-mockups")
FONT = os.path.join(ROOT, "tools", "fonts", "Tektur-Medium.ttf")
FONT_REG = os.path.join(ROOT, "tools", "fonts", "Tektur-Regular.ttf")
W, H = 1920, 1080
GREEN, CYAN, PINK, INK = (60, 255, 130), (70, 220, 255), (255, 70, 160), (214, 226, 220)


def font(size, regular=False):
    return ImageFont.truetype(FONT_REG if regular else FONT, size)


def cover(img, w=W, h=H):
    """Scale and crop to fill w x h."""
    k = max(w / img.width, h / img.height)
    img = img.resize((round(img.width * k), round(img.height * k)), Image.LANCZOS)
    x, y = (img.width - w) // 2, (img.height - h) // 2
    return img.crop((x, y, x + w, y + h))


def darken(img, k, blur=0):
    if blur:
        img = img.filter(ImageFilter.GaussianBlur(blur))
    return Image.blend(Image.new("RGBA", img.size, (6, 8, 10, 255)), img.convert("RGBA"), k)


def screen_rect(frame):
    """The screen opening of a frame mockup: flood the screen colour from near the centre (by similarity, so CRT tints
    work too) and take the bounding box of the largest fill."""
    rgb = frame.convert("RGB")
    best = None
    for fx, fy in ((0.5, 0.5), (0.4, 0.6), (0.6, 0.4), (0.45, 0.75), (0.3, 0.5)):
        work = rgb.copy()
        ImageDraw.floodfill(work, (int(frame.width * fx), int(frame.height * fy)), (255, 0, 255), thresh=22)
        a = np.array(work)
        box = Image.fromarray(((a[..., 0] == 255) & (a[..., 1] == 0) & (a[..., 2] == 255)).astype(np.uint8) * 255).getbbox()
        if box and (best is None or (box[2] - box[0]) * (box[3] - box[1]) > (best[2] - best[0]) * (best[3] - best[1])):
            best = box
    return best


def framed(frame_name, ui_name, width):
    """A frame mockup with a UI mockup filling its screen, scaled to `width`."""
    fr = Image.open(os.path.join(MOCK, frame_name)).convert("RGBA")
    x0, y0, x1, y1 = screen_rect(fr)
    ui = cover(Image.open(os.path.join(UI, ui_name)).convert("RGBA"), x1 - x0 - 8, y1 - y0 - 8)
    hole = Image.new("RGBA", fr.size, (0, 0, 0, 0))
    hole.paste(ui, (x0 + 4, y0 + 4))
    fr.alpha_composite(hole)
    k = width / fr.width
    return fr.resize((width, round(fr.height * k)), Image.LANCZOS)


def shadowed(base, img, xy, angle=0.0, glow=None):
    """Pastes img rotated by angle with a soft drop shadow (and an optional neon glow)."""
    if angle:
        img = img.rotate(angle, Image.BICUBIC, expand=True)
    sh = Image.new("RGBA", (img.width + 120, img.height + 120), (0, 0, 0, 0))
    sh.paste((0, 0, 0, 200), (60, 60), img.getchannel("A"))
    sh = sh.filter(ImageFilter.GaussianBlur(22))
    base.alpha_composite(sh, (xy[0] - 60 + 14, xy[1] - 60 + 22))
    if glow:
        g = Image.new("RGBA", sh.size, (0, 0, 0, 0))
        g.paste(glow + (120,), (60, 60), img.getchannel("A"))
        base.alpha_composite(g.filter(ImageFilter.GaussianBlur(26)), (xy[0] - 60, xy[1] - 60))
    base.alpha_composite(img, xy)


def window(ui_name, crop, width, accent):
    """A UI mockup as a floating window with a neon rim."""
    img = Image.open(os.path.join(UI, ui_name)).convert("RGBA")
    if crop:
        img = img.crop(crop)
    img = img.resize((width, round(img.height * width / img.width)), Image.LANCZOS)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, img.width - 1, img.height - 1), outline=accent + (255,), width=2)
    return img


def glow_text(img, xy, text, f, color, blur=8, anchor="la", strength=2):
    lay = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(lay).text(xy, text, font=f, fill=color + (255,), anchor=anchor)
    halo = lay.filter(ImageFilter.GaussianBlur(blur))
    for _ in range(strength):
        img.alpha_composite(halo)
    img.alpha_composite(lay)


def rich(d, xy, text, f, base=INK):
    """Text with *highlighted* words in green, one line."""
    x, y = xy
    for i, part in enumerate(text.split("*")):
        if not part:
            continue
        d.text((x, y), part, font=f, fill=(GREEN if i % 2 else base) + (255,))
        x += d.textlength(part, font=f)


def overlay(img, title, lines, accent=GREEN):
    """Caption plate, logo, scanlines, vignette and the bottom rule."""
    # vignette
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    v = np.clip(((xx - W / 2) / (W * 0.62)) ** 2 + ((yy - H / 2) / (H * 0.66)) ** 2 - 0.35, 0, 1) * 170
    img.alpha_composite(Image.fromarray(np.dstack([np.zeros((H, W, 3)), v]).astype(np.uint8), "RGBA"))
    # bottom plate: a gradient into near black
    g = np.clip((yy - 700) / 220, 0, 1) ** 1.2 * 235
    img.alpha_composite(Image.fromarray(np.dstack([np.full((H, W, 3), 6), g]).astype(np.uint8), "RGBA"))
    # scanlines
    sl = np.zeros((H, W), np.float32)
    sl[::3, :] = 22
    img.alpha_composite(Image.fromarray(np.dstack([np.zeros((H, W, 3)), sl]).astype(np.uint8), "RGBA"))
    d = ImageDraw.Draw(img)
    # title with a neon tick, then the lines; the block ends above the bottom rule
    ty = H - 62 - 42 * len(lines) - 100
    d.rectangle((70, ty + 12, 78, ty + 78), fill=PINK + (255,))
    glow_text(img, (100, ty), title.upper(), font(76), accent, blur=10)
    d = ImageDraw.Draw(img)
    for i, line in enumerate(lines):
        rich(d, (102, ty + 100 + i * 42), line, font(29, True))
    # logo bottom right, clear of the text (lines stay under ~1250 px)
    glow_text(img, (W - 66, H - 48), "NETRUN", font(104), GREEN, blur=14, anchor="rs")
    glow_text(img, (W - 70, H - 150), "HACKING EXPANSION", font(30), (210, 236, 220), blur=5, anchor="rs", strength=1)
    d = ImageDraw.Draw(img)
    d.rectangle((W - 430, H - 140, W - 330, H - 137), fill=PINK + (230,))
    d.rectangle((W - 320, H - 140, W - 290, H - 137), fill=CYAN + (230,))
    # neon rule along the bottom
    rule = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(rule).rectangle((40, H - 22, W - 40, H - 20), fill=CYAN + (255,))
    img.alpha_composite(rule.filter(ImageFilter.GaussianBlur(4)))
    img.alpha_composite(rule)
    return img


TEXT = {
    "ru": {
        "terminal": ("Терминал сети", [
            "Ядро, шлюз, клуб, студия и справка в одном окне.",
            "*Восемь корпусов* терминала: от корпоративного Kurenai до студии брейнданса.",
            "Новые рамки открываются достижениями."]),
        "cyberspace": ("Нырки в сеть", [
            "Нетраннер ложится в *капсулу* и уходит в киберпространство.",
            "Узлы, *ICE*, файлы и ловушки: пошаговый забег, где жадность стоит дорого."]),
        "club": ("Фиксеры, корпы, банды", [
            "*Фиксеры* дают контракты и чистят ваш след.",
            "*Корпорации* делят сеть и охотятся за вами, *команды раннеров* спорят за славу.",
            "Отношения, репутация и топ сети живут своей жизнью."]),
    },
    "en": {
        "terminal": ("The net terminal", [
            "Core, gateway, club, studio and help in one window.",
            "*Eight terminal decks*, from the corporate Kurenai to a braindance studio.",
            "New frames unlock with achievements."]),
        "cyberspace": ("Dive into the net", [
            "A netrunner lies down in a *pod* and jacks into cyberspace.",
            "Nodes, *ICE*, files and traps: a turn-based run where greed costs you."]),
        "club": ("Fixers, corps, crews", [
            "*Fixers* hand out contracts and scrub your trace.",
            "*Corporations* carve up the net and hunt you; *runner crews* fight for fame.",
            "Relations, reputation and the net's top list live on their own."]),
    },
}


def p_terminal(lang, src):
    img = darken(cover(Image.open(os.path.join(src, "киберпространство и нырки.jpg")).convert("RGBA")), 0.35, blur=6)
    decks = [("48d-relic-column-v3.png", "6-contracts-board.png", 600, -4, (300, 30)),
             ("49-studio.png", "4-club-contacts.png", 600, 4, (1010, 30)),
             ("42d-retro.png", "10-heat-v2.png", 620, -7, (30, 220)),
             ("38-nomad.png", "3-dive.png", 620, 6, (1250, 210)),
             ("39-kurenai.png", "1-core-map.png", 820, 0, (550, 90))]
    for frame, ui, width, ang, xy in decks:
        shadowed(img, framed(frame, ui, width), xy, ang, glow=GREEN if frame.startswith("39") else None)
    return overlay(img, *TEXT[lang]["terminal"])


def p_cyberspace(lang, src):
    img = cover(Image.open(os.path.join(src, "киберпространство и нырки.jpg")).convert("RGBA"))
    img = darken(img, 0.9)
    f = framed("46b-classic-cyberdeck.png", "3-dive.png", 620)
    shadowed(img, f, (1250, 70), 3, glow=CYAN)
    # a thin link from the pod to the screen
    d = ImageDraw.Draw(img)
    d.line([(1160, 680), (1250, 600)], fill=CYAN + (200,), width=3)
    d.ellipse((1150, 670, 1170, 690), outline=CYAN + (255,), width=3)
    return overlay(img, *TEXT[lang]["cyberspace"])


def p_club(lang, src):
    img = darken(cover(Image.open(os.path.join(src, "квесты.jpg")).convert("RGBA")), 0.35, blur=8)
    corps = window("29-club-v2.png", (20, 20, 920, 760), 760, PINK)
    crews = window("29-club-v2.png", (960, 20, 1860, 760), 760, CYAN)
    shadowed(img, corps, (50, 110), 4, glow=PINK)
    shadowed(img, crews, (1110, 100), -4, glow=CYAN)
    f = framed("38a-street-crt.png", "4-club-contacts.png", 720)
    shadowed(img, f, (600, 40), 0, glow=GREEN)
    return overlay(img, *TEXT[lang]["club"])


PREVIEWS = {"terminal": p_terminal, "cyberspace": p_cyberspace, "club": p_club}


def main():
    lang = sys.argv[sys.argv.index("--lang") + 1] if "--lang" in sys.argv else "ru"
    src = sys.argv[sys.argv.index("--src") + 1] if "--src" in sys.argv else SRC
    only = sys.argv[sys.argv.index("--only") + 1].split(",") if "--only" in sys.argv else list(PREVIEWS)
    os.makedirs(OUT, exist_ok=True)
    for name in only:
        img = PREVIEWS[name](lang, src).convert("RGB")
        img.save(os.path.join(OUT, f"{lang}_{name}.png"))
        img.resize((960, 540), Image.LANCZOS).save(os.path.join(OUT, f"{lang}_{name}_small.jpg"), quality=88)
        print("wrote", name)


if __name__ == "__main__":
    main()
