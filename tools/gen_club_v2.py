"""Mockup of the reworked Club (CONCEPTS_10, section 12b): tabs Fixers / Corporations / Crews / Top of the net.
Terminal style (manner D in docs/ART_STYLE.md). Faction logos come from the v2 icons, tinted like the game does.

Writes docs/ui-mockups/29-club-v2.png. Run from the repo root after tools/gen_icons_v2.py.
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from netrun_art import font  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICONS = os.path.join(ROOT, "Hacking Expansion Netrun Art", "Concepts10", "IconsV2")
OUT = os.path.join(ROOT, "docs", "ui-mockups", "29-club-v2.png")

BG = (24, 26, 24); PANEL = (34, 36, 34); LINE = (58, 214, 232); TEXT = (232, 236, 236); MUTED = (150, 156, 156)
GOLD = (240, 190, 60); RED = (230, 72, 80); GREEN = (80, 210, 110); BLUE = (80, 150, 240); PURPLE = (170, 110, 240)


def logo(name, color, size):
    im = Image.open(os.path.join(ICONS, name + ".png")).convert("RGBA").resize((size, size), Image.LANCZOS)
    r, g, b, a = im.split()
    r = r.point(lambda v: int(v * color[0] / 255)); g = g.point(lambda v: int(v * color[1] / 255)); b = b.point(lambda v: int(v * color[2] / 255))
    return Image.merge("RGBA", (r, g, b, a))


def tag(d, x, y, text, col):
    f = font(12)
    w = d.textlength(text, font=f) + 12
    d.rectangle((x, y, x + w, y + 18), outline=col, width=1)
    d.text((x + 6, y + 2), text, font=f, fill=col)
    return x + w + 6


def bar(d, x, y, w, frac, col, label, value):
    d.text((x, y - 2), label, font=font(13), fill=TEXT)
    bx = x + 70
    d.rectangle((bx, y, bx + w, y + 10), fill=(12, 12, 12))
    d.rectangle((bx, y, bx + int(w * frac), y + 10), fill=col)
    d.text((bx + w + 8, y - 4), str(value), font=font(14, True), fill=col)


def button(d, x, y, text, col, dim=False):
    f = font(13)
    w = d.textlength(text, font=f) + 22
    d.rectangle((x, y, x + w, y + 28), outline=(70, 74, 74) if dim else col, width=1)
    d.text((x + 11, y + 6), text, font=f, fill=MUTED if dim else col)
    return x + w + 8


def frame(img, x, y, w, h, title, active):
    d = ImageDraw.Draw(img)
    d.rectangle((x, y, x + w, y + h), fill=BG, outline=(70, 74, 74), width=2)
    d.text((x + 16, y + 12), "NETRUN // КЛУБ", font=font(20, True), fill=LINE)
    d.rectangle((x + w - 330, y + 12, x + w - 170, y + 36), fill=PANEL); d.text((x + w - 322, y + 15), "Серебро: 1240", font=font(13), fill=TEXT)
    d.rectangle((x + w - 160, y + 12, x + w - 16, y + 36), fill=PANEL); d.text((x + w - 152, y + 15), "Cyberdata: 7 / 2", font=font(13), fill=TEXT)
    d.line((x, y + 46, x + w, y + 46), fill=(70, 74, 74), width=1)
    tx = x + 16
    for name in ["Фиксеры", "Корпорации", "Команды", "Топ сети"]:
        f = font(14, name == active)
        tw = d.textlength(name, font=f)
        d.text((tx, y + 58), name, font=f, fill=LINE if name == active else MUTED)
        if name == active:
            d.line((tx, y + 80, tx + tw, y + 80), fill=LINE, width=3)
        tx += tw + 32
    d.line((x + 16, y + 84, x + w - 16, y + 84), fill=(50, 54, 54), width=1)
    d.text((x + 16, y + 96), title, font=font(14, True), fill=LINE)
    return d


def corp_card(img, d, x, y, w, logo_name, color, name, theme, status, status_col, alert, power, terr, note):
    h = 146
    d.rectangle((x, y, x + w, y + h), fill=PANEL, outline=(50, 54, 54))
    d.rectangle((x, y, x + 4, y + h), fill=color)
    d.rectangle((x + 16, y + 16, x + 116, y + 116), fill=BG, outline=color, width=2)
    img.alpha_composite(logo(logo_name, color, 88), (x + 22, y + 22))
    tx = x + 132
    d.text((tx, y + 12), name, font=font(18, True), fill=TEXT)
    nx = tx + d.textlength(name, font=font(18, True)) + 10
    nx = tag(d, nx, y + 15, theme, color)
    tag(d, nx, y + 15, status, status_col)
    bar(d, tx, y + 48, 200, alert / 100, RED, "Тревога", alert)
    bar(d, tx, y + 70, 200, power / 100, BLUE, "Сила", power)
    d.text((tx, y + 88), terr, font=font(13), fill=MUTED)
    bx = tx
    bx = button(d, bx, y + 110, "На карте", LINE)
    button(d, bx, y + 110, note, GOLD)
    return y + h + 10


def crew_card(img, d, x, y, w, logo_name, color, name, focus, rel, rel_col, good, skill, fame, last, buttons):
    h = 150
    d.rectangle((x, y, x + w, y + h), fill=PANEL, outline=(50, 54, 54))
    d.rectangle((x, y, x + 4, y + h), fill=color)
    d.rectangle((x + 16, y + 16, x + 116, y + 116), fill=BG, outline=color, width=2)
    img.alpha_composite(logo(logo_name, color, 88), (x + 22, y + 22))
    tx = x + 132
    d.text((tx, y + 12), name, font=font(18, True), fill=TEXT)
    nx = tx + d.textlength(name, font=font(18, True)) + 10
    nx = tag(d, nx, y + 15, "ФОКУС: " + focus, color)
    tag(d, nx, y + 15, rel, rel_col)
    # goodwill -100..100
    d.text((tx, y + 44), "Отношение", font=font(13), fill=TEXT)
    bx = tx + 90; bw = 200
    d.rectangle((bx, y + 46, bx + bw, y + 56), fill=(12, 12, 12))
    mid = bx + bw // 2
    end = mid + int(bw / 2 * good / 100)
    d.rectangle((min(mid, end), y + 46, max(mid, end), y + 56), fill=rel_col)
    d.line((mid, y + 42, mid, y + 60), fill=MUTED)
    d.text((bx + bw + 8, y + 42), f"{good:+d}", font=font(14, True), fill=rel_col)
    d.text((tx, y + 66), "Мастерство", font=font(13), fill=TEXT)
    for k in range(10):
        c = GOLD if k < skill else (60, 62, 62)
        d.rectangle((tx + 90 + k * 14, y + 68, tx + 90 + k * 14 + 10, y + 78), fill=c)
    d.text((tx + 250, y + 64), f"Слава {fame}", font=font(14, True), fill=GOLD)
    d.text((tx, y + 88), last, font=font(13), fill=MUTED)
    bx2 = tx
    for text, col, dim in buttons:
        bx2 = button(d, bx2, y + 112, text, col, dim)
    return y + h + 10


def top_panel(img, d, x, y, w, rows):
    d.rectangle((x, y, x + w, y + 470), fill=PANEL, outline=(50, 54, 54))
    d.text((x + 14, y + 10), "ТОП СЕТИ", font=font(15, True), fill=LINE)
    d.text((x + 14, y + 32), "по Славе, обновляется по событию", font=font(12), fill=MUTED)
    yy = y + 60
    for i, (rank, name, fame, trend, col, kind) in enumerate(rows):
        if kind == "you":
            d.rectangle((x + 6, yy - 6, x + w - 6, yy + 30), fill=(30, 52, 40))
        d.text((x + 14, yy), f"{rank}", font=font(18, True), fill=GOLD if rank == 1 else TEXT)
        d.rectangle((x + 44, yy + 4, x + 54, yy + 14), fill=col)
        d.text((x + 62, yy), name, font=font(14, kind == "you"), fill=TEXT)
        d.text((x + w - 100, yy), str(fame), font=font(14, True), fill=GOLD)
        d.text((x + w - 44, yy), trend, font=font(14, True), fill=GREEN if trend.startswith("▲") else RED if trend.startswith("▼") else MUTED)
        yy += 42
    d.line((x + 14, yy, x + w - 14, yy), fill=(60, 64, 64))
    d.text((x + 14, yy + 10), "Топ-1: фиксеры +10% к наградам,", font=font(12), fill=MUTED)
    d.text((x + 14, yy + 28), "но корпорации следят внимательнее.", font=font(12), fill=MUTED)


def build():
    W, H = 1880, 780
    img = Image.new("RGBA", (W, H), (16, 16, 18, 255))
    d = ImageDraw.Draw(img)
    # screen 1: corporations
    x0, y0, w0 = 20, 20, 900
    d = frame(img, x0, y0, w0, 740, "КОРПОРАЦИИ (3) · крах редок: не чаще раза в год", "Корпорации")
    y = y0 + 124
    y = corp_card(img, d, x0 + 16, y, w0 - 32, "Corp_Security", (90, 150, 235), "Кобальт", "ОХРАННАЯ", "ОХОТИТСЯ", RED, 72, 64,
                  "Территория: 7 узлов · крепость «Восток» · ядро закрыто", "Новости (2)")
    y = corp_card(img, d, x0 + 16, y, w0 - 32, "Corp_Media", (235, 90, 160), "Сайрен-Медиа", "МЕДИА", "НА ГРАНИ КРАХА", GOLD, 20, 12,
                  "Территория: 3 узла · Сила падает 9 дней · крепость ослаблена", "Новости (4)")
    y = corp_card(img, d, x0 + 16, y, w0 - 32, "Corp_Neurotech", (110, 210, 140), "Гелиос-Нейро", "НЕЙРОТЕХ", "РАСШИРЯЕТСЯ", GREEN, 18, 81,
                  "Территория: 9 узлов · захватила узел 21 два часа назад", "Новости (1)")
    d.rectangle((x0 + 16, y + 4, x0 + w0 - 16, y + 52), fill=PANEL, outline=(50, 54, 54))
    d.text((x0 + 28, y + 10), "Лента: «Сайрен-Медиа теряет активы: аналитики ждут краха»", font=font(13), fill=GOLD)
    d.text((x0 + 28, y + 30), "Вторжение от Кобальта: пакет на узле 14, до Дома ~6 ч", font=font(13), fill=RED)

    # screen 2: crews + top
    x1, y1, w1 = 960, 20, 900
    d = frame(img, x1, y1, w1, 740, "КОМАНДЫ РАННЕРОВ (3)", "Команды")
    cw = 560
    y = y1 + 124
    y = crew_card(img, d, x1 + 16, y, cw, "Crew_SkullCables", (230, 80, 80), "Хромовые", "КОРПОРАЦИИ", "ВРАЖДА", RED, -45, 7, 1840,
                  "Вчера: вскрыли архив-12 (выжжен на 3 дня)", [("Предложить гонку", LINE, False), ("Заказать у фиксера", GOLD, False)])
    y = crew_card(img, d, x1 + 16, y, cw, "Crew_Crow", (90, 160, 240), "Вороны", "КОНТРАКТЫ", "СОЮЗ", GREEN, 62, 5, 1210,
                  "Идут за вашей ★-нодой «Рынок-7»: у вас ~1 день", [("Позвать на помощь", GREEN, False), ("Наём раннера", LINE, False)])
    y = crew_card(img, d, x1 + 16, y, cw, "Crew_CrackedMask", (240, 190, 70), "Трещина", "ОХОТА", "НЕЙТРАЛЬНЫ", MUTED, 5, 4, 640,
                  "3 дня назад: ловушка на узле 9", [("Позвать на помощь", GREEN, True)])
    top_panel(img, d, x1 + 16 + cw + 14, y1 + 124, w1 - cw - 46, [
        (1, "Хромовые", 1840, "▲", (230, 80, 80), "crew"),
        (2, "Вы: Ли «Тихая»", 1520, "▲", (80, 210, 110), "you"),
        (3, "Вороны", 1210, "—", (90, 160, 240), "crew"),
        (4, "Вы: Марк", 760, "▼", (80, 210, 110), "you"),
        (5, "Трещина", 640, "▼", (240, 190, 70), "crew"),
    ])
    img.convert("RGB").save(OUT)


if __name__ == "__main__":
    build()
    print("done")
