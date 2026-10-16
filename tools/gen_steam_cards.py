"""Steam description infographics (the Vanilla Expanded way: the description is a chain of images). One card per
section, 1270 px wide like the section headers from gen_steam.py: the header on top, then a picture (an in-game terminal
shot or a scene) and a bullet list beside it, sides alternating. Writes docs/steam/img/<lang>_card_<id>.png.

    python tools/gen_steam_cards.py [--only heat,terminal]
Headers are drawn with gen_steam.header (same look), numbered in card order.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
import gen_steam as G  # noqa: E402
import gen_steam_previews as P  # noqa: E402

ROOT = P.ROOT
IMG = os.path.join(ROOT, "docs", "steam", "img")
W = 1270
BG, PANEL, DIM = (9, 13, 14), (14, 20, 21), (34, 48, 46)
GREEN, CYAN, PINK, INK = (78, 226, 104), (70, 220, 230), (255, 70, 160), (206, 216, 212)
PIC_W = 640


def wrap(d, text, f, width):
    """Splits a *marked* line into rows that fit `width`; markers survive the split."""
    words, rows, row = text.split(" "), [], ""
    for w in words:
        test = (row + " " + w).strip()
        if d.textlength(test.replace("*", ""), font=f) > width and row:
            rows.append(row)
            row = w
        else:
            row = test
    rows.append(row)
    # a highlight cut by the wrap: close it on one row and reopen on the next
    out, open_ = [], False
    for r in rows:
        r2 = ("*" if open_ else "") + r
        if r2.count("*") % 2:
            r2 += "*"
            open_ = True
        else:
            open_ = False
        out.append(r2)
    return out


def picture(spec, w):
    kind, name = spec[0], spec[1]
    if kind == "shot":
        return P.shot(name, w)
    img = Image.open(os.path.join(P.SRC, name)).convert("RGBA")
    if len(spec) > 2:
        img = img.crop(spec[2])
    h = round(w * 0.62)
    img = P.cover(img, w, h)
    ImageDraw.Draw(img).rectangle((0, 0, w - 1, h - 1), outline=CYAN + (255,), width=2)
    return img


# Sections that gen_steam.py has no header for: id -> (icon, title, tagline)
EXTRA = {
    "dives": ("Cyberspace/Dive_BLACKICE", "NETRUN DIVES", "turn-based runs through the ICE"),
    "newcomer": ("Icon_Guide", "NEW PLAYER FRIENDLY", "the net explains itself"),
}


def head_img(cid, index, lang):
    """The section header from gen_steam.header, rendered into a scratch folder so the text variant's own headers
    (numbered in their order) stay as they are."""
    tmp = os.path.join(IMG, "_cardhead")
    os.makedirs(tmp, exist_ok=True)
    G.IMG = tmp
    sec = next((x for x in G.SECTIONS if x[0] == cid), None)
    ico, title, tag = (sec[1], sec[2], sec[3]) if sec else EXTRA[cid]
    G.header(cid, ico, title, tag, index, lang)
    return Image.open(os.path.join(tmp, f"{lang}_{cid}.png")).convert("RGBA")


def card(cid, pic, bullets, flip, index, lang="en"):
    head = head_img(cid, index, lang)
    f = P.font(29, True)
    probe = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    text_w = W - PIC_W - 120
    rows = [wrap(probe, b, f, text_w - 34) for b in bullets]
    text_h = sum(len(r) * 40 + 18 for r in rows)
    pic_img = picture(pic, PIC_W)
    body_h = max(pic_img.height + 60, text_h + 60)
    img = Image.new("RGBA", (W, head.height + body_h), BG + (255,))
    img.alpha_composite(head)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((12, head.height - 4, W - 12, img.height - 12), radius=10, fill=PANEL + (255,), outline=DIM + (255,), width=2)
    px = W - 40 - PIC_W if flip else 40
    py = head.height + (body_h - pic_img.height) // 2 - 6
    P.shadowed(img, pic_img, (px, py), 0)
    tx = 40 if flip else 40 + PIC_W + 40
    y = head.height + (body_h - text_h) // 2 - 4
    d = ImageDraw.Draw(img)
    for r in rows:
        d.rectangle((tx, y + 13, tx + 10, y + 23), fill=PINK + (255,))
        for line in r:
            P.rich(d, (tx + 30, y), line, f, base=INK)
            y += 40
        y += 18
    return img


CARDS = [
    ("heat", ("shot", "nomad_heat"), [
        "Every hack raises the colony's *Heat*: five levels from *Shadow* to the *Red Zone*.",
        "The hotter the trace, the harsher the answer: neural noise, data theft, sabotage, power surges, ransomware.",
        "At the top, raids by the hidden *Black Ledger* mercenaries.",
        "Lay low, scrub the trace through a fixer, or burn bright and pay for it."]),
    ("terminal", ("shot", "street_core"), [
        "The comms console becomes a *network console*: a neon terminal for the whole net.",
        "*Core*, *Club*, *Board*, *Archive*, *Server room*, *Studio* and *Gateway*.",
        "*Eight deck frames* that react to your Heat; new ones unlock as you play.",
        "Five sound packs, scaling and accent colours."]),
    ("cyberspace", ("shot", "nr80_gateway"), [
        "One *living map* per game: about 45 hubs in districts you chart as you go.",
        "Corporate towers, markets, ruins, the hidden darknet and the *Old Net* behind a wall.",
        "Lenses, search, routes; outages, storms and rival crews change it day by day.",
        "Up to three runners in cyberpods walk its links in game time."]),
    ("dives", ("shot", "street_dive"), [
        "*Turn-based dives*: code gates, hidden caches, files, memory units, easy and hard forks.",
        "*Black ICE* strikes back with Hacking Expansion's nausea, lockout and coma.",
        "*Programs* and five *daemons*; HE's ICE breakers fit right in.",
        "Corporate *boss cores* hold the best loot in the net."]),
    ("fixers", ("shot", "retro_board"), [
        "A rotating roster of *fixers*: brokers, cleaners, fences, imperial clients.",
        "Each hides a trait: reliable, greedy, paranoid... or a *rat* who sells you out.",
        "Build reputation, pin the ones you trust.",
        "*8 kinds of contracts*: deliveries, system taps, quiet runs, braindance labs."]),
    ("corps", ("shot", "studio_corps"), [
        "*3 to 4 corporations* with Alert and Strength: they patrol, intrude and take territory.",
        "Push one hard enough and it *collapses*.",
        "*Rival runner crews* race you, help you and can be hired.",
        "The *top-100 of the net*: fame pays, and gets you watched."]),
    ("ais", ("scene", "сетевые атаки, ии и его ивенты.jpg", (280, 248, 1760, 1080)), [
        "Salvage *AI fragments* and wake them: *13 personalities*, each with a service, hunger and trust.",
        "Keep them on an *AI server*, in a pod *coprocessor* or in a colonist's head.",
        "*75 AI events* with choices and *personal quests*.",
        "A colonist with a cyberlink who dies may leave an *Echo*..."]),
    ("braindance", ("scene", "брейндансы.jpg", (60, 0, 1740, 945)), [
        "*Braindance goggles* capture your colonists' strongest moments.",
        "Edit raw recordings into chips, normal to masterwork, or forbidden *black chips*.",
        "Sell them, watch them, get hooked.",
        "Hunt down *8 unique recordings* hidden across the net."]),
    ("defense", ("scene", "квесты.jpg", (200, 225, 1720, 1080)), [
        "Wall-mounted *firewalls* and your own *ICE tiers* up to black ICE.",
        "A *honeypot* that traces attackers back; netrunners on watch duty.",
        "Full network isolation, or intercept an incoming intrusion with a dive.",
        "*Distress signals* lead to abandoned servers worth the risk."]),
    ("colonists", ("scene", "связь с DLC.jpg", (220, 60, 1700, 892)), [
        "*Netsick*: pod addiction, cravings and reckless emotional dives.",
        "Dive traits, *9 backstories*, *4 scenarios*, *26 achievements* (optional).",
        "*Ideology*: net memes, rituals, the Net Archon. *Royalty*: imperial fixers.",
        "*Biotech*: a gene and braindance for children. *Odyssey*: orbital hubs."]),
    ("newcomer", ("shot", "nr1_cyberpedia"), [
        "The *Cyberpedia* explains every system with live examples from your game.",
        "*Guide mode* outlines each part of the screen.",
        "First steps tick off as you play; a free *training dive* teaches the dive screen.",
        "Threats wait until your colony touches the net."]),
]


def description(lang="en"):
    """The card variant of the description: banner, the intro, the cards one after another, then requirements and
    credits as text under their headers (links must stay clickable). Image links follow gen_steam: GitHub raw files
    under the release tag, or docs/steam/urls.json."""
    urls_path = os.path.join(ROOT, "docs", "steam", "urls.json")
    urls = G.json.load(open(urls_path, encoding="utf-8")) if os.path.exists(urls_path) else {}
    text = G.EN if lang == "en" else G.RU
    url = lambda name: urls.get(name, G.RAW + name + ".png")
    parts = [f"[img]{url(lang + '_banner')}[/img]", text["intro"]]
    parts.append("\n".join(f"[img]{url(f'{lang}_card_{cid}')}[/img]" for cid, _p, _b in CARDS))
    for sid in ("requirements", "credits"):
        parts.append(f"[img]{url(lang + '_' + sid)}[/img]\n{text[sid]}")
    body = "\n\n".join(parts) + "\n"
    path = os.path.join(ROOT, "docs", "steam", f"description_{lang}_cards.txt")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    print(f"{os.path.basename(path)}: {len(body)} characters")


def main():
    only = sys.argv[sys.argv.index("--only") + 1].split(",") if "--only" in sys.argv else None
    for i, (cid, pic, bullets) in enumerate(CARDS):
        if only and cid not in only:
            continue
        card(cid, pic, bullets, flip=i % 2 == 1, index=i + 1).convert("RGB").save(os.path.join(IMG, f"en_card_{cid}.png"), optimize=True)
        print("card", cid)
    description("en")


if __name__ == "__main__":
    main()
