"""Steam Workshop description (docs/steam/): a banner and section headers in the terminal style, plus the BBCode text
in English and Russian. Two variants per language:
- description_<lang>.txt: text headers ([h1]), paste-ready as is;
- description_<lang>_images.txt: header images. By default they link to the raw files on GitHub under the release
  tag (v<modVersion> from About.xml), so push the tag before publishing. Other hosting: put the links into
  docs/steam/urls.json ({"en_banner": "https://...", ...}) and run the script again.
Steam allows 8000 characters; the script warns above that. Run: python tools/gen_steam.py
"""
import json
import os
import re

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "docs", "steam")
IMG = os.path.join(OUT, "img")
TEX = os.path.join(ROOT, "Textures", "UI", "Netrun")
FONT = os.path.join(ROOT, "tools", "fonts", "Tektur-Medium.ttf")
FONT_REG = os.path.join(ROOT, "tools", "fonts", "Tektur-Regular.ttf")

W = 1270
BG = (9, 13, 14)
PANEL = (14, 20, 21)
GREEN = (78, 226, 104)
CYAN = (70, 220, 230)
GREY = (150, 162, 158)
DIM = (34, 48, 46)

HE = "https://steamcommunity.com/sharedfiles/filedetails/?id=3573344880"
HARMONY = "https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077"
VEF = "https://steamcommunity.com/sharedfiles/filedetails/?id=2023507013"
GITHUB = "https://github.com/GafarovMaxim/RimWorld-Netrun"
VERSION = re.search(r"<modVersion>([^<]+)</modVersion>", open(os.path.join(ROOT, "About", "About.xml"), encoding="utf-8").read()).group(1)
RAW = f"https://raw.githubusercontent.com/GafarovMaxim/RimWorld-Netrun/v{VERSION}/docs/steam/img/"

# id, icon, EN title, EN tagline, RU title, RU tagline
SECTIONS = [
    ("heat", "Heat/Heat_RedZone", "DIGITAL HEAT", "the net remembers your trace", "ЦИФРОВОЙ СЛЕД", "сеть помнит каждый взлом"),
    ("terminal", "Gizmo_NetTerminal", "NETWORK TERMINAL", "your colony's window into the net", "СЕТЕВОЙ ТЕРМИНАЛ", "окно колонии в сеть"),
    ("cyberspace", "Gizmo_NetrunDive", "CYBERSPACE", "a living map, dive by dive", "КИБЕРПРОСТРАНСТВО", "живая карта, нырок за нырком"),
    ("fixers", "Tabs/Tab_Fixers", "FIXERS & CONTRACTS", "everyone has a price", "ФИКСЕРЫ И КОНТРАКТЫ", "у каждого своя цена"),
    ("corps", "Factions/Corp_Security", "CORPORATIONS & CREWS", "the powers of the net", "КОРПОРАЦИИ И КОМАНДЫ", "силы, которые правят сетью"),
    ("ais", "Tabs/Tab_AI", "NETWORK AIs", "thirteen minds in your servers", "СЕТЕВЫЕ ИИ", "тринадцать разумов в ваших серверах"),
    ("braindance", "Tabs/Tab_Braindance", "BRAINDANCE", "someone else's memory, as your own", "БРЕЙНДАНС", "чужая память как своя"),
    ("defense", "Cyberspace/Program_Shield", "HUB DEFENSE", "when the net comes knocking", "ЗАЩИТА УЗЛА", "когда сеть стучится в ответ"),
    ("transition", "Gizmo_MindUpload", "THE TRANSITION", "an ending beyond the flesh", "ПЕРЕХОД", "финал за пределами плоти"),
    ("colonists", "Ideo/Role_NetArchon", "COLONISTS, DLC & STARTS", "the netrunner pays in body and mind", "КОЛОНИСТЫ, DLC И СТАРТЫ", "нетраннер платит телом и разумом"),
    ("requirements", "Icon_Guide", "REQUIREMENTS", "load order and compatibility", "ТРЕБОВАНИЯ", "порядок загрузки и совместимость"),
    ("credits", "Fixers/Handshake", "CREDITS", "thank you", "БЛАГОДАРНОСТИ", "спасибо"),
]


def font(size, regular=False):
    return ImageFont.truetype(FONT_REG if regular else FONT, size)


def grid(img, top, bottom, alpha=40):
    d = ImageDraw.Draw(img, "RGBA")
    for x in range(0, img.width, 40):
        d.line([(x, top), (x, bottom)], fill=(*GREEN, alpha // 3))
    for y in range(top, bottom, 40):
        d.line([(0, y), (img.width, y)], fill=(*GREEN, alpha // 3))


def glow_text(img, xy, text, f, color, blur=6, anchor="la"):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).text(xy, text, font=f, fill=(*color, 255), anchor=anchor)
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur)))
    img.alpha_composite(layer)


def icon(path, size):
    im = Image.open(os.path.join(TEX, path + ".png")).convert("RGBA")
    box = im.getbbox() or (0, 0, im.width, im.height)
    im = im.crop(box)
    k = size / max(im.width, im.height)
    return im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)


def header(sid, ico, title, tagline, index, lang):
    h = 150
    img = Image.new("RGBA", (W, h), (*BG, 255))
    grid(img, 0, h, 50)
    d = ImageDraw.Draw(img, "RGBA")
    d.rounded_rectangle([12, 12, W - 12, h - 12], radius=10, fill=(*PANEL, 235), outline=(*DIM, 255), width=2)
    # icon in a neon frame
    d.rounded_rectangle([30, 25, 130, 125], radius=12, fill=(6, 9, 10, 255), outline=(*GREEN, 255), width=3)
    ic = icon(ico, 76)
    img.alpha_composite(ic, (80 - ic.width // 2, 75 - ic.height // 2))
    glow_text(img, (160, 34), title, font(54), GREEN, blur=8)
    d = ImageDraw.Draw(img, "RGBA")
    d.text((163, 92), "// " + tagline, font=font(24, True), fill=(*GREY, 255))
    # index tag and the neon rule
    d.text((W - 40, 34), f"[ {index:02d} ]", font=font(26, True), fill=(*CYAN, 200), anchor="ra")
    d.line([(160, h - 26), (W - 40, h - 26)], fill=(*DIM, 255), width=2)
    d.line([(160, h - 26), (420, h - 26)], fill=(*CYAN, 255), width=3)
    img.convert("RGB").save(os.path.join(IMG, f"{lang}_{sid}.png"), optimize=True)


def banner(lang):
    h = 540
    img = Image.new("RGBA", (W, h), (*BG, 255))
    horizon = 360
    grid(img, 0, horizon, 24)
    # perspective floor grid under the horizon
    d = ImageDraw.Draw(img, "RGBA")
    for i in range(-24, 25):
        d.line([(W / 2 + i * 26, horizon), (W / 2 + i * 150, h)], fill=(*GREEN, 45), width=2)
    y, step = horizon, 5.0
    while y < h:
        d.line([(0, y), (W, y)], fill=(*GREEN, 50), width=2)
        y += step
        step *= 1.3
    d.line([(0, horizon), (W, horizon)], fill=(*GREEN, 170), width=2)

    glow_text(img, (W // 2, 50), "HACKING EXPANSION", font(50), (210, 236, 220), blur=5, anchor="ma")
    glow_text(img, (W // 2, 104), "NETRUN", font(150), GREEN, blur=14, anchor="ma")
    tag = "The net remembers your trace" if lang == "en" else "Сеть помнит ваш след"
    d = ImageDraw.Draw(img, "RGBA")
    tw = d.textlength(tag.upper(), font=font(30, True))
    d.rounded_rectangle([W // 2 - tw / 2 - 24, 282, W // 2 + tw / 2 + 24, 330], radius=8, fill=(*BG, 255), outline=(*CYAN, 160), width=2)
    d.text((W // 2, 306), tag.upper(), font=font(30, True), fill=(*CYAN, 255), anchor="mm")

    # a row of AI portraits standing on the grid
    names = ["Ghost", "DataHunter", "Muse", "Predator", "Warden", "Oracle", "Echo"]
    size, gap = 104, 28
    x0 = (W - (len(names) * size + (len(names) - 1) * gap)) // 2
    top = 400
    for i, n in enumerate(names):
        x = x0 + i * (size + gap)
        d.rounded_rectangle([x, top, x + size, top + size], radius=12, fill=(6, 9, 10, 245), outline=(*GREEN, 255), width=3)
        ic = icon("AI/AI_" + n, size - 22)
        img.alpha_composite(ic, (x + (size - ic.width) // 2, top + (size - ic.height) // 2))
    # corner badge
    d.polygon([(0, 0), (150, 0), (0, 150)], fill=(60, 52, 40, 255))
    d.text((40, 30), "1.6", font=font(28), fill=(230, 220, 200, 255), anchor="mm")
    d.text((40, 62), "1.7", font=font(28), fill=(230, 220, 200, 255), anchor="mm")
    img.convert("RGB").save(os.path.join(IMG, f"{lang}_banner.png"), optimize=True)


# ---------------------------------------------------------------- text

EN = {
    "intro": f"""[quote][i]"The net remembers your trace. Data is currency, but the netrunner pays in body and mind."[/i][/quote]

[b]Hacking Expansion: Netrun[/b] is a cyberpunk layer for [url={HE}]Ushanka's Hacking Expansion[/url]. Every hack leaves a trace. Corporations notice. Fixers call. Dormant AIs wake up in your servers. Deep in a cyberpod your netrunner walks a living map of cyberspace, and the net always answers.""",
    "heat": """Every intrusion raises the colony's [b]Heat[/b]: five levels from [b]Shadow[/b] to the [b]Red Zone[/b]. The hotter the trace, the harsher the answer: neural noise, data theft, machine sabotage, power surges, ransomware and raids by the hidden [b]Black Ledger[/b] mercenaries. Lay low, scrub the trace through a fixer, or burn bright and pay for it.""",
    "terminal": """The comms console becomes a [b]network console[/b]: a neon terminal with its own navigation screen of sections.
[list]
[*][b]Core[/b]: the Heat ring, trends, alerts, resources
[*][b]Club[/b]: fixers, corporations, crews, the top-100
[*][b]Board[/b]: contracts
[*][b]Archive[/b]: the trace journal
[*][b]Server room[/b]: your AIs
[*][b]Studio[/b]: braindance
[*][b]Gateway[/b]: the cyberspace map and dives
[/list]
A big zoomable [b]cyberspace map[/b] with lenses (loot, danger, corporations, jobs, time), search, routes and a watchlist. The [b]Cyberpedia[/b] explains everything with live examples, and a Guide mode outlines every part of the screen. Five sound packs, scaling and accent colours.""",
    "cyberspace": """One map per game: about 45 hubs in districts that you chart as you go. Corporate towers, markets, ruins, the campus, the hidden darknet and the [b]Old Net[/b] behind a wall. Runners in cyberpods walk its links in game time, up to three at once. Outages, jams, storms and new tenants: the net lives day by day. A free [b]training dive[/b] walks a newcomer through the dive screen.
[list]
[*][b]Turn-based dives[/b]: code gates, hidden caches, files, memory units, forks of easy and hard paths
[*][b]Black ICE[/b] strikes back with Hacking Expansion's nausea, lockout and coma
[*][b]Programs[/b] (Sword, Shield, Decoy, Stealth, Killer) and five [b]daemons[/b]; HE's ICE breakers and exec data fit right in
[*][b]Node alerts and ICE patrols[/b]: fail too often and the node wakes up
[*][b]Protected corporate hubs[/b]: find the key in a neighbouring hub first, then take the richer loot
[*][b]Corporate cores[/b]: boss nodes with the best loot in the net
[/list]""",
    "fixers": """A rotating roster of underworld [b]fixers[/b]: brokers, cleaners, fences and imperial clients (Royalty). Each hides a trait: reliable, greedy, paranoid, a collector... or a [b]rat[/b] who will sell you out. Build reputation, pin the ones you trust and take [b]8 kinds of contracts[/b]: data deliveries, system taps, quiet runs, surveillance, clean braindance labs and more.""",
    "corps": """[list]
[*][b]3 to 4 corporations[/b] own the corporate districts. Each has [b]Alert[/b] and [b]Strength[/b]: they harden their nodes, send patrols, launch intrusions along the map, capture territory and can [b]collapse[/b].
[*][b]Rival runner crews[/b] race you, help you, hunt contract targets and can be hired.
[*][b]The top-100 of the net[/b]: fame for every dive. The best get paid more, and watched more closely.
[/list]""",
    "ais": """Salvage AI fragments from the net and wake them up. [b]13 personalities[/b], from Ghost, Data Hunter and Muse to Predator, Oracle, Trickster, Seed and Echo, each with its own service, hunger, trust and traits.
[list]
[*]Keep them on an [b]AI server[/b] with racks, in a cyberpod [b]coprocessor[/b] or in a colonist's head through an [b]AI socket[/b]
[*][b]75 AI events[/b] with choices
[*][b]Personal quests[/b] in the vanilla Quests tab; AI levels unlock stronger and second services
[*]A colonist with a cyberlink who dies may leave an [b]Echo[/b]...
[/list]""",
    "braindance": """[b]Braindance goggles[/b] capture your colonists' strongest moments. Edit raw recordings into chips at the console, from normal to masterwork, or forbidden [b]black chips[/b]. Edit a whole pile in one click, if you dare the glitch. Sell them, watch them, get hooked. Hunt down [b]8 unique recordings[/b] hidden across the net.""",
    "defense": """Wall-mounted [b]firewalls[/b], your own [b]ICE tiers[/b] up to black ICE, a [b]honeypot[/b] that traces attackers back, netrunners on watch duty, full network isolation, and intercepting an incoming intrusion with a dive.""",
    "transition": """A late-game ending for colonies that live in the net. It starts with a rumour in the news feed and a stranger in the Club who used to work in a corporate black lab. What they built there, nobody outside knows. Raid the labs, piece the prototypes together, and decide who is ready to leave the flesh behind.""",
    "colonists": """[list]
[*][b]Netsick[/b]: pod addiction, cravings and reckless emotional dives (sometimes legendary); the pod tells how long a stay is still safe
[*]Dive traits, [b]9 backstories[/b], dive stories and tales
[*][b]Royalty[/b]: imperial fixers, a trace-scrub permit
[*][b]Ideology[/b]: net memes, rituals, the Net Archon role
[*][b]Biotech[/b]: a gene, braindance learning for children
[*][b]Odyssey[/b]: orbital hubs while your gravship is in space
[*][b]4 scenarios[/b]: The Last Node, Corporate Runaways, Pirate Studio, Flying Node (Odyssey)
[*][b]26 achievements[/b] with Vanilla Achievements Expanded (optional)
[/list]""",
    "requirements": f"""[list]
[*][url={HARMONY}]Harmony[/url]
[*][url={VEF}]Vanilla Expanded Framework[/url]
[*][url={HE}]Ushanka's Hacking Expansion[/url]
[/list]
Load [b]after[/b] Hacking Expansion. Can be added to an existing save; removing it mid-game is not recommended. DLC content loads only with its DLC.
Game versions: [b]1.6[/b] and [b]1.7[/b].
Languages: [b]English[/b], [b]Русский[/b].""",
    "credits": f"""Author: [b]Raul Moreau[/b]
Built on [b]Hacking Expansion[/b] by BlueEagle421 and the Ushanka team. Thank you!
Font: [b]Tektur[/b] (SIL Open Font License 1.1)
[url={GITHUB}]Source code on GitHub[/url] (MIT)

[i]Found a bug? Leave a comment with your Player.log, it helps a lot.[/i]""",
}

RU = {
    "intro": f"""[quote][i]«Сеть помнит ваш след. Данные — валюта, но нетраннер платит телом и разумом.»[/i][/quote]

[b]Hacking Expansion: Netrun[/b] — киберпанк-надстройка над [url={HE}]Ushanka's Hacking Expansion[/url]. Каждый взлом оставляет след. Корпорации замечают. Фиксеры звонят. В ваших серверах просыпаются спящие ИИ. Глубоко в кибер-капсуле ваш нетраннер идёт по живой карте киберпространства, и сеть всегда отвечает.""",
    "heat": """Каждый взлом поднимает [b]Heat[/b] колонии: пять уровней от [b]Тени[/b] до [b]Красной зоны[/b]. Чем горячее след, тем жёстче ответ: нейрошум, кража данных, саботаж станков, скачки напряжения, вымогатели и рейды скрытых наёмников [b]«Чёрной книги»[/b]. Заляг на дно, сотри след у фиксера или гори ярко и плати за это.""",
    "terminal": """Консоль связи становится [b]сетевой консолью[/b]: неоновый терминал со своим экраном навигации из разделов.
[list]
[*][b]Ядро[/b]: кольцо Heat, тренды, тревоги, ресурсы
[*][b]Клуб[/b]: фиксеры, корпорации, команды, топ-100
[*][b]Доска[/b]: контракты
[*][b]Архив[/b]: журнал следа
[*][b]Серверная[/b]: ваши ИИ
[*][b]Студия[/b]: брейнданс
[*][b]Шлюз[/b]: карта киберпространства и нырки
[/list]
Большая [b]карта киберпространства[/b] с линзами (добыча, опасность, корпорации, работа, время), поиском, маршрутами и списком наблюдения. [b]Киберпедия[/b] объясняет всё на живых примерах, а режим подсказок обводит каждую часть экрана. Пять звуковых пакетов, масштаб и цвет акцента.""",
    "cyberspace": """Одна карта на игру: около 45 узлов по районам, которые вы открываете по мере походов. Корпоративные башни, рынки, руины, кампус, скрытый даркнет и [b]Старая сеть[/b] за стеной. Раннеры в капсулах идут по связям в игровом времени, до трёх одновременно. Отключения, заторы, штормы, новые жильцы: сеть живёт день за днём. Бесплатный [b]тренировочный нырок[/b] знакомит новичка с экраном нырка.
[list]
[*][b]Пошаговые нырки[/b]: кодовые врата, тайники, файлы, блоки памяти, развилки лёгкого и трудного пути
[*][b]Чёрный лёд[/b] бьёт в ответ тошнотой, блокировкой и комой из Hacking Expansion
[*][b]Программы[/b] (Меч, Щит, Приманка, Стелс, Убийца) и пять [b]демонов[/b]; ледорубы и exec-данные HE встают в слот
[*][b]Тревога ноды и ледовые патрули[/b]: ошибёшься слишком часто — нода проснётся
[*][b]Защищённые узлы корпораций[/b]: сначала найдите ключ в соседнем узле, потом берите добычу богаче
[*][b]Корп-ядра[/b]: босс-ноды с лучшей добычей в сети
[/list]""",
    "fixers": """Сменяющийся круг [b]фиксеров[/b] теневого мира: брокеры, чистильщики, скупщики и имперские клиенты (Royalty). У каждого скрытая черта: надёжный, жадный, параноик, коллекционер... или [b]крыса[/b], которая сдаст вас. Копите репутацию, закрепляйте тех, кому верите, и берите [b]8 видов контрактов[/b]: доставки данных, врезки в системы, тихие заходы, слежку, чистые лаборатории брейнданса и другое.""",
    "corps": """[list]
[*][b]3–4 корпорации[/b] владеют корпоративными районами. У каждой [b]Тревога[/b] и [b]Сила[/b]: они укрепляют ноды, шлют патрули, ведут вторжения по карте, захватывают территорию и могут [b]рухнуть[/b].
[*][b]Команды раннеров[/b] соревнуются с вами, помогают, охотятся за целями контрактов и сдаются в наём.
[*][b]Топ-100 сети[/b]: слава за каждый нырок. Лучшим платят больше, и следят за ними пристальнее.
[/list]""",
    "ais": """Добывайте фрагменты ИИ в сети и будите их. [b]13 личностей[/b], от Призрака, Охотника за данными и Музы до Хищника, Оракула, Трикстера, Зерна и Эха, у каждой своя услуга, голод, доверие и черты.
[list]
[*]Держите их на [b]ИИ-сервере[/b] со стойками, в [b]сопроцессоре[/b] капсулы или в голове колониста через [b]ИИ-разъём[/b]
[*][b]75 событий ИИ[/b] с выбором
[*][b]Личные квесты[/b] во вкладке «Квесты»; уровни ИИ открывают усиленную и вторую услугу
[*]Погибший колонист с кибер-линком может оставить [b]Эхо[/b]...
[/list]""",
    "braindance": """[b]Очки брейнданса[/b] записывают самые сильные моменты ваших колонистов. Монтируйте сырые записи в чипы на консоли, от обычных до мастерских, или в запретные [b]чёрные чипы[/b]. Смонтируйте всю стопку одной кнопкой, если не боитесь сбоя. Продавайте, смотрите, подсаживайтесь. Ищите [b]8 уникальных записей[/b], спрятанных по всей сети.""",
    "defense": """[b]Брандмауэры[/b] на стене, свои [b]уровни льда[/b] вплоть до чёрного, [b]ловушка[/b], которая отслеживает атакующих, нетраннеры на дежурстве, полная изоляция сети и перехват вторжения нырком.""",
    "transition": """Поздний финал для колоний, которые живут в сети. Всё начинается со слуха в ленте новостей и незнакомца в Клубе, который раньше работал в чёрной лаборатории корпорации. Что там создали, снаружи никто не знает. Взламывайте лаборатории, собирайте прототипы и решайте, кто готов оставить плоть позади.""",
    "colonists": """[list]
[*][b]Netsick[/b]: зависимость от капсулы, ломка и безрассудные нырки на эмоциях (иногда легендарные); капсула подскажет, сколько ещё безопасно лежать
[*]Черты нырков, [b]9 предысторий[/b], рассказы о нырках и легенды
[*][b]Royalty[/b]: имперские фиксеры, разрешение на зачистку следа
[*][b]Ideology[/b]: сетевые мемы, ритуалы, роль Архонта сети
[*][b]Biotech[/b]: ген, обучение детей через брейнданс
[*][b]Odyssey[/b]: орбитальные узлы, пока гравикорабль в космосе
[*][b]4 сценария[/b]: «Последний узел», «Корпоративные беглецы», «Пиратская студия», «Летающий узел» (Odyssey)
[*][b]26 достижений[/b] с Vanilla Achievements Expanded (необязательно)
[/list]""",
    "requirements": f"""[list]
[*][url={HARMONY}]Harmony[/url]
[*][url={VEF}]Vanilla Expanded Framework[/url]
[*][url={HE}]Ushanka's Hacking Expansion[/url]
[/list]
Загружать [b]после[/b] Hacking Expansion. Можно добавить в начатую игру; убирать посреди игры не рекомендуется. Контент DLC грузится только с соответствующим DLC.
Версии игры: [b]1.6[/b] и [b]1.7[/b].
Языки: [b]English[/b], [b]Русский[/b].""",
    "credits": f"""Автор: [b]Raul Moreau[/b]
Мод построен на [b]Hacking Expansion[/b] от BlueEagle421 и команды Ushanka. Спасибо!
Шрифт: [b]Tektur[/b] (SIL Open Font License 1.1)
[url={GITHUB}]Исходный код на GitHub[/url] (MIT)

[i]Нашли ошибку? Оставьте комментарий с файлом Player.log, это очень помогает.[/i]""",
}


def build(lang, text, urls, images):
    parts = []
    if images:
        parts.append(f"[img]{urls.get(lang + '_banner', RAW + lang + '_banner.png')}[/img]")
    else:
        parts.append("[h1]HACKING EXPANSION: NETRUN[/h1]")
    parts.append(text["intro"])
    for sid, _ico, en_t, _en_s, ru_t, _ru_s in SECTIONS:
        title = en_t if lang == "en" else ru_t
        if images:
            parts.append(f"[img]{urls.get(lang + '_' + sid, RAW + lang + '_' + sid + '.png')}[/img]")
        else:
            parts.append(f"[hr][/hr]\n[h1]▸ {title}[/h1]")
        parts.append(text[sid])
    return "\n\n".join(parts) + "\n"


def main():
    os.makedirs(IMG, exist_ok=True)
    for i, (sid, ico, en_t, en_s, ru_t, ru_s) in enumerate(SECTIONS, start=1):
        header(sid, ico, en_t, en_s, i, "en")
        header(sid, ico, ru_t, ru_s, i, "ru")
    banner("en")
    banner("ru")
    urls_path = os.path.join(OUT, "urls.json")
    urls = json.load(open(urls_path, encoding="utf-8")) if os.path.exists(urls_path) else {}
    for lang, text in (("en", EN), ("ru", RU)):
        for images, suffix in ((False, ""), (True, "_images")):
            body = build(lang, text, urls, images)
            path = os.path.join(OUT, f"description_{lang}{suffix}.txt")
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(body)
            note = "  (over the Steam limit of 8000!)" if len(body) > 8000 else ""
            print(f"{os.path.basename(path)}: {len(body)} characters{note}")


if __name__ == "__main__":
    main()
