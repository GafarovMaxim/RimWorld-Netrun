"""Achievements for Vanilla Achievements Expanded (docs/TECH_DETAILS.md, section 3; docs/CONCEPTS_10.md, section 15).

One table makes everything (the defs load only with VAE through MayRequire; NetrunMod loads Optional/NetrunVAE.dll):
- Textures/UI/Netrun/Achievements/Frame_<tier>.png: the chip frame (variant C) as the card background (bgtexPath);
- Textures/UI/Netrun/Achievements/<Key>.png: the mod's own icon with a margin, so the chip rim stays visible;
- Defs/Achievements_Netrun.xml (English) and the Russian DefInjected file;
- Defs/NetrunAchievements.xml: the same list as the mod's own defs (Netrun.NetrunAchievementDef), always loaded, for
  the terminal's achievement screen and the frame themes they open (works without VAE).

Run: python tools/gen_achievements.py             (everything)
     python tools/gen_achievements.py --defs-only (XML only, no images; the art folder is not needed)
"""
import os
from xml.sax.saxutils import escape

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = os.path.join(ROOT, "Hacking Expansion Netrun Art", "Concepts10", "AchievementFrames")
TEX = os.path.join(ROOT, "Textures")
OUT = ROOT
OUT_TEX = os.path.join(OUT, "Textures", "UI", "Netrun", "Achievements")
SIZE = 128
ICON = 74  # the inner part of the chip frame

# key, count, tier, points, icon, EN label, EN description, RU label, RU description
ACH = [
    ("Dives", 1, "common", 5, "UI/Netrun/Gizmo_NetrunDive",
     "Jacked in", "Dive into cyberspace for the first time.",
     "Подключение", "Впервые нырнуть в киберпространство."),
    ("CleanStreak", 10, "common", 15, "UI/Netrun/Cyberspace/Program_Stealth",
     "On the wave", "Finish 10 dives in a row with a clean exit.",
     "На волне", "10 нырков подряд с чистым выходом."),
    ("BlackICEPassed", 1, "common", 15, "UI/Netrun/Cyberspace/Dive_BLACKICE",
     "Through the black", "Get through a node guarded by Black ICE.",
     "Сквозь чёрный лёд", "Пройти узел под защитой Black ICE."),
    ("CorpCores", 1, "rare", 50, "UI/Netrun/Cyberspace/Node_CorpCore",
     "Core breach", "Take a corporate core.",
     "Пробой ядра", "Взять корпоративное ядро."),
    ("EmotionalJackpot", 1, "common", 15, "UI/Netrun/Tabs/Tab_Braindance",
     "Dive on feelings", "A dive taken during a net craving break ends with a jackpot.",
     "Нырок на эмоциях", "Нырок во время срыва «тяга к сети» принёс джекпот."),
    ("RedZoneHours", 72, "common", 20, "UI/Netrun/Heat/Heat_RedZone",
     "Living in the red", "Stay in the Red Zone for 3 days in a row and survive.",
     "Жизнь в красном", "Провести в Красной зоне 3 дня подряд и выжить."),
    ("RedToShadow", 1, "common", 20, "UI/Netrun/Heat/Heat_Reduction",
     "Slipped out", "Go from the Red Zone to the Shadow within a day.",
     "Выскользнул", "Уйти из Красной зоны в Тень за сутки."),
    ("ShadowSeason", 1, "common", 20, "UI/Netrun/Heat/Heat_Shadow",
     "A season in the shadow", "Spend a season (15 days) in the Shadow with 20 or more hacks.",
     "Сезон в тени", "Провести сезон (15 дней) в Тени, сделав 20 и более взломов."),
    ("AIsAwakened", 1, "common", 10, "UI/Netrun/Gizmo_AI_Awaken",
     "It's alive", "Awaken your first AI.",
     "Оно живое", "Пробудить первый ИИ."),
    ("Personalities", 13, "rare", 50, "UI/Netrun/Tabs/Tab_AI",
     "The whole zoo", "Meet all 13 AI personalities in one game.",
     "Весь зверинец", "Встретить все 13 личностей ИИ за одну игру."),
    ("SeedGrown", 1, "common", 15, "UI/Netrun/AI/AI_Seed",
     "The seed has grown", "A Seed AI grows into a personality.",
     "Зерно проросло", "ИИ-Зерно выросло в личность."),
    ("Trust100", 1, "common", 20, "UI/Netrun/AI/AI_Warden",
     "Absolute trust", "An AI trusts the colony completely (trust 100).",
     "Абсолютное доверие", "ИИ полностью доверяет колонии (доверие 100)."),
    ("BetrayalSurvived", 1, "common", 15, "UI/Netrun/AI/AI_Trickster",
     "Knife in the back", "Survive an AI's betrayal.",
     "Нож в спину", "Пережить предательство ИИ."),
    ("PinnedFixers", 3, "common", 10, "UI/Netrun/Icon_Pinned",
     "Own people", "Have 3 pinned fixers at once.",
     "Свои люди", "Держать трёх закреплённых фиксеров одновременно."),
    ("ContractsDone", 25, "common", 20, "UI/Netrun/Tabs/Tab_Contracts",
     "Professional", "Complete 25 contracts.",
     "Профессионал", "Выполнить 25 контрактов."),
    ("RatCut", 1, "common", 15, "UI/Netrun/Factions/Crew_Rat",
     "Smelled a rat", "Cut ties with a rat fixer before it sells you out.",
     "Почуял крысу", "Разорвать связь с фиксером-крысой до того, как он сольёт колонию."),
    ("UniqueRecords", 8, "rare", 40, "UI/Netrun/Cyberspace/Reward_UniqueBraindance",
     "Collector", "Find all 8 unique braindance recordings.",
     "Коллекционер", "Найти все 8 уникальных записей брейнданса."),
    ("MasterChip", 1, "common", 15, "Things/Item/Drug/NR_BraindanceChip",
     "Director's cut", "Edit a braindance chip of master quality.",
     "Режиссёрская версия", "Смонтировать чип брейнданса мастерского качества."),
    ("IntrusionsRepelled", 10, "common", 20, "UI/Netrun/Cyberspace/Program_Shield",
     "Firewall", "Repel 10 net intrusions.",
     "Брандмауэр", "Отбить 10 сетевых вторжений."),
    ("Intercepts", 1, "common", 15, "UI/Netrun/Cyberspace/Daemon_Hound",
     "Interception", "Repel an intrusion with a runner's dive.",
     "Перехват", "Отбить вторжение нырком раннера."),
    ("CorpCollapsed", 1, "rare", 40, "UI/Netrun/Cyberspace/Event_CorpHunt",
     "Market crash", "A corporation collapses.",
     "Обвал рынка", "Корпорация рухнула."),
    ("NetNumberOne", 1, "rare", 50, "UI/Netrun/Fixers/Star",
     "Number one", "A colony runner tops the top-100 of the net.",
     "Номер один", "Раннер колонии возглавил топ-100 сети."),
    # Secret: riddles instead of conditions (TECH_DETAILS 3.6, option 1).
    ("EchoBorn", 1, "secret", 50, "UI/Netrun/AI/AI_Echo",
     "Digital immortality", "Someone in the net remembers how you died.",
     "Цифровое бессмертие", "Кто-то в сети помнит, как ты умер."),
    ("EchoMeetsKiller", 1, "secret", 25, "UI/Netrun/Cyberspace/Program_Killer",
     "Face to face", "The dead keep a list. Sometimes the list walks in.",
     "Лицом к лицу", "У мёртвых есть список. Иногда список приходит сам."),
    ("RookieRedZone", 1, "secret", 15, "UI/Netrun/Cyberspace/Dive_Lightning_Red",
     "Baptism of fire", "Some learn to swim in the shallows. Not you.",
     "Крещение огнём", "Кто-то учится плавать на мелководье. Но не ты."),
    ("NoAIYear2", 1, "secret", 15, "UI/Netrun/Cyberspace/Icon_Lock",
     "Luddite with a pod", "The pod hums all year, and the servers stay silent.",
     "Луддит с капсулой", "Капсула гудит весь год, а серверы молчат."),
    ("OldNetEntered", 1, "common", 15, "UI/Netrun/Cyberspace/Node_OldNet",
     "Ghosts of the Old Net", "Dive into a system of the Old Net, behind the Wall.",
     "Призраки Старой сети", "Нырнуть в систему Старой сети, за Стеной."),
]


def frame(tier):
    im = Image.open(os.path.join(FRAMES, f"AchFrame_C_{tier}.png")).convert("RGBA")
    return im.resize((SIZE, SIZE), Image.LANCZOS)


def icon(path):
    src = Image.open(os.path.join(TEX, path + ".png")).convert("RGBA")
    box = src.getbbox() or (0, 0, src.width, src.height)
    src = src.crop(box)
    k = ICON / max(src.width, src.height)
    src = src.resize((max(1, round(src.width * k)), max(1, round(src.height * k))), Image.LANCZOS)
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    out.paste(src, ((SIZE - src.width) // 2, (SIZE - src.height) // 2), src)
    return out


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def main():
    import sys
    if "--defs-only" not in sys.argv:
        os.makedirs(OUT_TEX, exist_ok=True)
        for tier in ("common", "rare", "secret"):
            frame(tier).save(os.path.join(OUT_TEX, f"Frame_{tier}.png"))
        for key, *_rest in ACH:
            icon(_rest[3]).save(os.path.join(OUT_TEX, f"{key}.png"))

    defs = ['<?xml version="1.0" encoding="utf-8"?>',
            "<!-- Generated by tools/gen_achievements.py. Loaded only with Vanilla Achievements Expanded (MayRequire). -->",
            "<Defs>", "",
            '  <AchievementsExpanded.AchievementTabDef MayRequire="vanillaexpanded.achievements">',
            "    <defName>NR_AchTab_Netrun</defName>",
            "    <label>Netrun</label>",
            "    <order>50</order>",
            "  </AchievementsExpanded.AchievementTabDef>", ""]
    ru = ['<?xml version="1.0" encoding="utf-8"?>', "<!-- Generated by tools/gen_achievements.py. -->", "<LanguageData>", ""]
    for order, (key, count, tier, points, _icon, en_l, en_d, ru_l, ru_d) in enumerate(ACH):
        dn = f"NR_Ach_{key}"
        defs += [
            '  <AchievementsExpanded.AchievementDef MayRequire="vanillaexpanded.achievements">',
            f"    <defName>{dn}</defName>",
            f"    <label>{escape(en_l)}</label>",
            f"    <description>{escape(en_d)}</description>",
            "    <tab>NR_AchTab_Netrun</tab>",
            f"    <order>{order}</order>",
            f"    <points>{points}</points>",
            f"    <texPath>UI/Netrun/Achievements/{key}</texPath>",
            f"    <bgtexPath>UI/Netrun/Achievements/Frame_{tier}</bgtexPath>",
            '    <tracker Class="NetrunVAE.NetrunCounterTracker">',
            f"      <key>{key}</key>",
            f"      <count>{count}</count>",
            "    </tracker>",
            "  </AchievementsExpanded.AchievementDef>", ""]
        ru += [f"  <{dn}.label>{escape(ru_l)}</{dn}.label>",
               f"  <{dn}.description>{escape(ru_d)}</{dn}.description>", ""]
    defs.append("</Defs>")
    ru.append("</LanguageData>")
    write(os.path.join(OUT, "Defs", "Achievements_Netrun.xml"), "\n".join(defs) + "\n")
    write(os.path.join(OUT, "Languages", "Russian (Русский)", "DefInjected", "AchievementsExpanded.AchievementDef",
                       "Achievements_Netrun.xml"), "\n".join(ru) + "\n")
    write(os.path.join(OUT, "Languages", "Russian (Русский)", "DefInjected", "AchievementsExpanded.AchievementTabDef",
                       "Achievements_Netrun.xml"),
          '<?xml version="1.0" encoding="utf-8"?>\n<LanguageData>\n  <NR_AchTab_Netrun.label>Netrun</NR_AchTab_Netrun.label>\n</LanguageData>\n')
    own = ['<?xml version="1.0" encoding="utf-8"?>',
           "<!-- Generated by tools/gen_achievements.py: the mod's own copy of the achievements (no VAE needed). -->",
           "<Defs>", ""]
    own_ru = ['<?xml version="1.0" encoding="utf-8"?>', "<!-- Generated by tools/gen_achievements.py. -->", "<LanguageData>", ""]
    for order, (key, count, tier, points, _icon, en_l, en_d, ru_l, ru_d) in enumerate(ACH):
        dn = f"NR_NAch_{key}"
        own += ["  <Netrun.NetrunAchievementDef>",
                f"    <defName>{dn}</defName>",
                f"    <label>{escape(en_l)}</label>",
                f"    <description>{escape(en_d)}</description>",
                f"    <key>{key}</key>",
                f"    <count>{count}</count>",
                f"    <tier>{tier}</tier>",
                f"    <points>{points}</points>",
                f"    <order>{order}</order>",
                "  </Netrun.NetrunAchievementDef>", ""]
        own_ru += [f"  <{dn}.label>{escape(ru_l)}</{dn}.label>",
                   f"  <{dn}.description>{escape(ru_d)}</{dn}.description>", ""]
    own.append("</Defs>")
    own_ru.append("</LanguageData>")
    write(os.path.join(OUT, "Defs", "NetrunAchievements.xml"), "\n".join(own) + "\n")
    write(os.path.join(OUT, "Languages", "Russian (Русский)", "DefInjected", "Netrun.NetrunAchievementDef",
                       "NetrunAchievements.xml"), "\n".join(own_ru) + "\n")
    print(f"{len(ACH)} achievements")


if __name__ == "__main__":
    main()
