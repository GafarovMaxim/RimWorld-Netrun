"""Prototype of the cyberspace map generator (docs/CYBERSPACE_V2.md, section 4) and renders of full maps.

Not game code: it checks the rules (node counts per district, links, exits, darknet, Old Net) and shows how
different layouts look. Writes docs/ui-mockups/30-...34-*.png and prints stats (hours from Home per district).

Run from the repo root:  python tools/gen_cyber_maps.py
"""
import heapq
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from netrun_art import font  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MOCK = os.path.join(ROOT, "docs", "ui-mockups")
NODE_TEX = os.path.join(ROOT, "Textures", "UI", "Netrun", "Cyberspace")

# ---------------------------------------------------------------- districts

# key: (label, colour, branch slots range, branch icon pool)
DISTRICTS = {
    "outskirts": ("Окраина", (90, 200, 120), (1, 2), ["Node_Market", "Node_Archive"]),
    "hub":       ("Транзит", (60, 210, 230), (0, 1), ["Node_Market"]),
    "corpA":     ("Корпорация A", (90, 150, 235), (2, 2), ["Node_CorpVault"]),
    "corpB":     ("Корпорация B", (235, 90, 160), (2, 2), ["Node_CorpVault"]),
    "corpC":     ("Корпорация C", (235, 175, 70), (2, 2), ["Node_CorpVault"]),
    "corpD":     ("Корпорация D", (150, 110, 240), (2, 2), ["Node_CorpVault"]),
    "market":    ("Рынок-квартал", (230, 210, 120), (2, 3), ["Node_Market", "Node_DarkExchange"]),
    "ruins":     ("Руины", (170, 150, 130), (1, 2), ["Node_AbandonedServer"]),
    "campus":    ("Кампус", (110, 200, 210), (2, 2), ["Node_Archive"]),
    "fun":       ("Квартал развлечений", (240, 120, 120), (2, 2), ["Node_Market", "Node_Archive"]),
    "dump":      ("Свалка данных", (150, 170, 90), (1, 2), ["Node_AbandonedServer", "Node_Archive"]),
    "oldnet":    ("Старая сеть", (220, 60, 70), (1, 1), ["Node_CorpCore"]),
}

SIZES = {
    # nodes per district; Old Net and the darknet hub come on top
    "small":  {"outskirts": 3, "hub": 3, "corpA": 3, "corpB": 3, "corpC": 3, "market": 3, "ruins": 2, "campus": 0, "fun": 2, "dump": 2, "oldnet": 2},
    "normal": {"outskirts": 4, "hub": 4, "corpA": 4, "corpB": 4, "corpC": 4, "market": 3, "ruins": 3, "campus": 2, "fun": 2, "dump": 2, "oldnet": 3},
    "large":  {"outskirts": 4, "hub": 5, "corpA": 4, "corpB": 4, "corpC": 4, "corpD": 4, "market": 4, "ruins": 3, "campus": 3, "fun": 3, "dump": 3, "oldnet": 3},
}

# district centres per layout, in a 0..1 x 0..0.66 field; outskirts always at the left edge, Old Net behind the wall on the right
LAYOUTS = {
    "ring": {
        "title": "Кольцо: транзит в центре, районы вокруг",
        "centers": {"outskirts": (0.08, 0.36), "hub": (0.46, 0.34), "corpA": (0.42, 0.08), "corpB": (0.72, 0.16), "corpC": (0.70, 0.54),
                    "corpD": (0.30, 0.47), "market": (0.22, 0.16), "ruins": (0.18, 0.60), "campus": (0.62, 0.34), "fun": (0.46, 0.60),
                    "dump": (0.80, 0.36), "oldnet": (0.95, 0.30)},
        "cross": 2, "express": "hub_star",
    },
    "spine": {
        "title": "Хребет: транзит цепью через всю карту, районы по сторонам",
        "centers": {"outskirts": (0.07, 0.34), "hub": (0.50, 0.34), "corpA": (0.32, 0.10), "corpB": (0.58, 0.10), "corpC": (0.66, 0.58),
                    "corpD": (0.84, 0.10), "market": (0.20, 0.56), "ruins": (0.42, 0.58), "campus": (0.18, 0.12), "fun": (0.86, 0.58),
                    "dump": (0.82, 0.34), "oldnet": (0.96, 0.34)},
        "cross": 1, "express": "hub_chain",
    },
    "archipelago": {
        "title": "Архипелаг: острова-районы, связи только через транзит",
        "centers": {"outskirts": (0.08, 0.34), "hub": (0.46, 0.33), "corpA": (0.30, 0.08), "corpB": (0.64, 0.08), "corpC": (0.68, 0.58),
                    "corpD": (0.32, 0.60), "market": (0.22, 0.34), "ruins": (0.50, 0.62), "campus": (0.48, 0.06), "fun": (0.70, 0.33),
                    "dump": (0.84, 0.18), "oldnet": (0.96, 0.46)},
        "cross": 0, "express": "hub_star",
    },
    "metro": {
        "title": "Мегаполис: плотная сеть кварталов, много обходных путей",
        "centers": {"outskirts": (0.08, 0.34), "hub": (0.50, 0.34), "corpA": (0.34, 0.12), "corpB": (0.66, 0.12), "corpC": (0.66, 0.56),
                    "corpD": (0.34, 0.56), "market": (0.22, 0.20), "ruins": (0.22, 0.50), "campus": (0.50, 0.08), "fun": (0.50, 0.60),
                    "dump": (0.80, 0.34), "oldnet": (0.95, 0.34)},
        "cross": 3, "express": "hub_ring",
    },
}

HOUR_NORMAL, HOUR_EXPRESS = 1.0, 0.5


class Map:
    def __init__(self, layout, size, seed):
        self.layout, self.size, self.rng = layout, size, random.Random(seed)
        self.nodes = []        # dicts: x, y, district, slots, exit, home, dark
        self.edges = {}        # (a, b) -> kind: normal | express | dark | wall
        self.generate()

    # ------------------------------------------------ generation
    def generate(self):
        L = LAYOUTS[self.layout]
        counts = SIZES[self.size]
        for d, n in counts.items():
            if n == 0:
                continue
            if d == "hub" and L["express"] == "hub_chain":
                for k in range(n):   # the spine: transit nodes spread along the axis
                    x = 0.22 + k * (0.62 / max(1, n - 1)) + self.rng.uniform(-0.02, 0.02)
                    self.nodes.append({"x": x, "y": L["centers"]["hub"][1] + self.rng.uniform(-0.04, 0.04), "d": d, "slots": self.rng.randint(0, 1),
                                       "exit": False, "home": False, "dark": False})
                continue
            cx, cy = L["centers"][d]
            r = 0.06 + 0.012 * n
            placed = 0; tries = 0
            while placed < n and tries < 4000:
                tries += 1
                a = self.rng.uniform(0, 2 * math.pi); rr = r * math.sqrt(self.rng.random())
                x, y = cx + math.cos(a) * rr, cy + math.sin(a) * rr * 0.9
                if not (0.03 < x < 0.985 and 0.03 < y < 0.64):
                    continue
                if any(math.hypot(x - o["x"], y - o["y"]) < 0.055 for o in self.nodes):
                    continue
                lo, hi = DISTRICTS[d][2]
                self.nodes.append({"x": x, "y": y, "d": d, "slots": self.rng.randint(lo, hi), "exit": False, "home": False, "dark": False})
                placed += 1
        idx = {d: [i for i, n in enumerate(self.nodes) if n["d"] == d] for d in counts}
        main = [i for i, n in enumerate(self.nodes) if n["d"] != "oldnet"]

        # Gabriel graph inside the main net
        def gabriel_ok(a, b):
            ax, ay, bx, by = self.nodes[a]["x"], self.nodes[a]["y"], self.nodes[b]["x"], self.nodes[b]["y"]
            mx, my, r2 = (ax + bx) / 2, (ay + by) / 2, ((ax - bx) ** 2 + (ay - by) ** 2) / 4
            return all((self.nodes[c]["x"] - mx) ** 2 + (self.nodes[c]["y"] - my) ** 2 >= r2 for c in main if c not in (a, b))

        cross_pairs = {}
        cand = []
        for i in main:
            for j in main:
                if i < j and gabriel_ok(i, j):
                    cand.append((self.dist(i, j), i, j))
        cand.sort()
        for dd, i, j in cand:
            same = self.nodes[i]["d"] == self.nodes[j]["d"]
            if same and dd < 0.22:
                self.add(i, j, "normal")
            elif not same:
                key = tuple(sorted((self.nodes[i]["d"], self.nodes[j]["d"])))
                if "hub" in key:
                    continue          # the hub joins by express links only
                if cross_pairs.get(key, 0) < L["cross"] and dd < 0.20:
                    cross_pairs[key] = cross_pairs.get(key, 0) + 1
                    self.add(i, j, "normal")

        # express links: the transit hub
        hub = idx["hub"]
        if L["express"] == "hub_chain":
            hub.sort(key=lambda i: self.nodes[i]["x"])
        else:
            c = (sum(self.nodes[i]["x"] for i in hub) / len(hub), sum(self.nodes[i]["y"] for i in hub) / len(hub))
            hub.sort(key=lambda i: math.atan2(self.nodes[i]["y"] - c[1], self.nodes[i]["x"] - c[0]))
        for a, b in zip(hub, hub[1:]):
            self.add(a, b, "express")
        if L["express"] in ("hub_star", "hub_ring") and len(hub) > 2:
            self.add(hub[-1], hub[0], "express")
        for d, ids in idx.items():
            if d in ("hub", "oldnet") or not ids:
                continue
            best = min(((self.dist(i, h), i, h) for i in ids for h in hub))
            self.add(best[1], best[2], "express")
            if L["express"] == "hub_ring" and len(ids) > 2:
                second = sorted(((self.dist(i, h), i, h) for i in ids for h in hub if (min(i, h), max(i, h)) not in self.edges))[0]
                self.add(second[1], second[2], "express")

        # connectivity inside each district (minimum spanning tree as a safety net)
        for d, ids in idx.items():
            if len(ids) < 2:
                continue
            inside = set([ids[0]])
            while len(inside) < len(ids):
                best = min(((self.dist(a, b), a, b) for a in inside for b in ids if b not in inside))
                self.add(best[1], best[2], "normal"); inside.add(best[2])

        # degree cap 4 for normal links (express never removed)
        for i in main:
            ns = sorted(((self.dist(i, j), j) for j in self.neighbors(i) if self.edges.get(self.key(i, j)) == "normal"), reverse=True)
            for dd, j in ns:
                if len(self.neighbors(i)) <= 4:
                    break
                k = self.key(i, j); kind = self.edges.pop(k)
                if not self.connected(main):
                    self.edges[k] = kind

        # home on the left edge, linked to the two nearest outskirts nodes
        hx, hy = 0.02, L["centers"]["outskirts"][1]
        self.nodes.append({"x": hx, "y": hy, "d": "home", "slots": 0, "exit": True, "home": True, "dark": False})
        home = len(self.nodes) - 1
        for dd, j in sorted((self.dist(home, j), j) for j in idx["outskirts"])[:2]:
            self.add(home, j, "normal")
        self.home = home

        # Old Net behind the wall: chain, one gate link from the nearest main node
        old = idx.get("oldnet", [])
        for a, b in zip(sorted(old, key=lambda i: self.nodes[i]["y"]), sorted(old, key=lambda i: self.nodes[i]["y"])[1:]):
            self.add(a, b, "normal")
        if old:
            gate = min(((self.dist(i, j), i, j) for i in old for j in main if self.nodes[j]["d"] not in ("hub",)))
            self.add(gate[1], gate[2], "wall")
        self.wall_x = min((self.nodes[i]["x"] for i in old), default=1.0) - 0.035

        # darknet: one hidden hub node and dark links to ruins, dump, market (+1 shortcut)
        cands = [(self.rng.uniform(0.30, 0.80), self.rng.uniform(0.10, 0.58)) for _ in range(200)]
        dx, dy = max(cands, key=lambda p: min(math.hypot(p[0] - n["x"], p[1] - n["y"]) for n in self.nodes))
        self.nodes.append({"x": dx, "y": dy, "d": "dark", "slots": 2, "exit": False, "home": False, "dark": True})
        dark = len(self.nodes) - 1
        for d in ("ruins", "dump", "market"):
            if idx.get(d):
                j = min(idx[d], key=lambda j: self.dist(dark, j)); self.add(dark, j, "dark")
        far = sorted(main, key=lambda i: -self.dist(dark, i))[:6]
        a, b = self.rng.sample(far, 2)
        self.add(a, b, "dark")

        # one fortress per corporation: the corp node farthest from home
        self.fortress = {}
        for d in ("corpA", "corpB", "corpC", "corpD"):
            if idx.get(d):
                self.fortress[d] = max(idx[d], key=lambda i: self.dist(home, i))

        # exits: ~20% of the main net, at least 2 hops apart, not in corp districts' cores
        order = main[:]; self.rng.shuffle(order)
        want = max(3, round(len(main) * 0.2))
        for i in order:
            if len([n for n in self.nodes if n["exit"] and not n["home"]]) >= want:
                break
            if i in self.fortress.values():
                continue
            if self.nodes[i]["d"] == "hub" or all(self.hops(i, j) >= 2 for j, n in enumerate(self.nodes) if n["exit"]):
                self.nodes[i]["exit"] = True

    # ------------------------------------------------ helpers
    def key(self, a, b):
        return (min(a, b), max(a, b))

    def add(self, a, b, kind):
        if a != b:
            k = self.key(a, b)
            if k not in self.edges or kind == "express":
                self.edges[k] = kind

    def dist(self, a, b):
        return math.hypot(self.nodes[a]["x"] - self.nodes[b]["x"], self.nodes[a]["y"] - self.nodes[b]["y"])

    def neighbors(self, i, kinds=None):
        out = []
        for (a, b), k in self.edges.items():
            if kinds and k not in kinds:
                continue
            if a == i: out.append(b)
            elif b == i: out.append(a)
        return out

    def connected(self, ids):
        ids = set(ids); start = next(iter(ids)); seen = {start}; stack = [start]
        while stack:
            u = stack.pop()
            for v in self.neighbors(u, ("normal", "express")):
                if v in ids and v not in seen:
                    seen.add(v); stack.append(v)
        return seen >= ids

    def hops(self, a, b):
        seen = {a: 0}; q = [a]
        while q:
            u = q.pop(0)
            if u == b:
                return seen[u]
            for v in self.neighbors(u, ("normal", "express")):
                if v not in seen:
                    seen[v] = seen[u] + 1; q.append(v)
        return 99

    def hours_from_home(self):
        """Dijkstra in game hours over known links (dark and wall links excluded)."""
        dist = {self.home: 0.0}; pq = [(0.0, self.home)]
        while pq:
            d, u = heapq.heappop(pq)
            if d > dist.get(u, 1e9):
                continue
            for (a, b), k in self.edges.items():
                if k not in ("normal", "express") or u not in (a, b):
                    continue
                v = b if a == u else a
                nd = d + (HOUR_EXPRESS if k == "express" else HOUR_NORMAL)
                if nd < dist.get(v, 1e9):
                    dist[v] = nd; heapq.heappush(pq, (nd, v))
        return dist

    def stats(self):
        h = self.hours_from_home()
        per = {}
        for i, n in enumerate(self.nodes):
            if n["d"] in ("home", "dark", "oldnet"):
                continue
            per.setdefault(n["d"], []).append(h.get(i, 99))
        main = [n for n in self.nodes if n["d"] not in ("home", "dark", "oldnet")]
        return {
            "nodes": len(main), "oldnet": sum(1 for n in self.nodes if n["d"] == "oldnet"),
            "slots": sum(n["slots"] for n in main), "exits": sum(1 for n in main if n["exit"]),
            "links": sum(1 for k in self.edges.values() if k == "normal"), "express": sum(1 for k in self.edges.values() if k == "express"),
            "dark": sum(1 for k in self.edges.values() if k == "dark"),
            "hours": {d: (min(v), sum(v) / len(v), max(v)) for d, v in per.items()},
            "max": max(v for vs in per.values() for v in vs),
        }


# ---------------------------------------------------------------- rendering

ICON_CACHE = {}


def tex(name, size):
    k = (name, size)
    if k not in ICON_CACHE:
        ICON_CACHE[k] = Image.open(os.path.join(NODE_TEX, name + ".png")).convert("RGBA").resize((size, size), Image.LANCZOS)
    return ICON_CACHE[k]


def render(m, path, W=1500, H=900, legend=True, title=None):
    K = 2
    MW = 1120 if legend else W
    img = Image.new("RGBA", (W * K, H * K), (10, 14, 18, 255))
    d = ImageDraw.Draw(img)
    top = 60
    sx = lambda x: (20 + x * (MW - 40)) * K
    sy = lambda y: (top + 10 + y / 0.66 * (H - top - 30)) * K
    for gx in range(0, MW * K, 40 * K):
        d.line([(gx, top * K), (gx, H * K)], fill=(18, 40, 32, 255), width=K)
    for gy in range(top * K, H * K, 40 * K):
        d.line([(0, gy), (MW * K, gy)], fill=(18, 40, 32, 255), width=K)

    # district areas
    terr = Image.new("RGBA", img.size, (0, 0, 0, 0))
    dt = ImageDraw.Draw(terr)
    for n in m.nodes:
        if n["d"] in DISTRICTS and n["d"] != "hub":
            r = 70 * K
            dt.ellipse((sx(n["x"]) - r, sy(n["y"]) - r, sx(n["x"]) + r, sy(n["y"]) + r), fill=DISTRICTS[n["d"]][1] + (46,))
    img.alpha_composite(terr.filter(ImageFilter.GaussianBlur(26 * K)))
    d = ImageDraw.Draw(img)

    # the wall in front of the Old Net
    wx = sx(m.wall_x)
    for yy in range(top * K, H * K, 26 * K):
        d.line([(wx, yy), (wx, yy + 16 * K)], fill=(220, 60, 70, 255), width=4 * K)

    # links
    for (a, b), k in m.edges.items():
        A, B = m.nodes[a], m.nodes[b]
        p, q = (sx(A["x"]), sy(A["y"])), (sx(B["x"]), sy(B["y"]))
        if k == "express":
            d.line([p, q], fill=(5, 8, 10, 255), width=12 * K); d.line([p, q], fill=(60, 210, 230, 255), width=6 * K)
        elif k == "normal":
            d.line([p, q], fill=(5, 8, 10, 255), width=7 * K); d.line([p, q], fill=(70, 190, 140, 255), width=3 * K)
        else:
            col = (180, 100, 255, 255) if k == "dark" else (220, 60, 70, 255)
            L = math.hypot(q[0] - p[0], q[1] - p[1]); n = max(2, int(L / (14 * K)))
            for i in range(0, n, 2):
                t0, t1 = i / n, min(1, (i + 1) / n)
                d.line([(p[0] + (q[0] - p[0]) * t0, p[1] + (q[1] - p[1]) * t0), (p[0] + (q[0] - p[0]) * t1, p[1] + (q[1] - p[1]) * t1)], fill=col, width=4 * K)

    # branch slots as small node icons around each node
    for i, n in enumerate(m.nodes):
        if n["d"] not in DISTRICTS or n["slots"] == 0:
            continue
        pool = DISTRICTS[n["d"]][3]
        rnd = random.Random(i * 7 + 3)
        for s in range(n["slots"]):
            ang = -math.pi / 2 + (s - (n["slots"] - 1) / 2) * 0.9 + (math.pi if (i % 2) else 0)
            bx, by = sx(n["x"]) + math.cos(ang) * 30 * K, sy(n["y"]) + math.sin(ang) * 30 * K
            d.line([(sx(n["x"]), sy(n["y"])), (bx, by)], fill=(120, 140, 160, 200), width=K)
            ic = tex(rnd.choice(pool), 22 * K)
            img.alpha_composite(ic, (int(bx - 11 * K), int(by - 11 * K)))
    d = ImageDraw.Draw(img)

    # fortresses
    for dname, i in m.fortress.items():
        n = m.nodes[i]; X, Y = sx(n["x"]), sy(n["y"])
        col = DISTRICTS[dname][1]
        pts = [(X + math.cos(math.pi / 8 + k * math.pi / 4) * 40 * K, Y + math.sin(math.pi / 8 + k * math.pi / 4) * 40 * K) for k in range(8)]
        for k in range(8):
            p, q = pts[k], pts[(k + 1) % 8]
            d.line([p, ((p[0] + q[0]) / 2, (p[1] + q[1]) / 2)], fill=col + (255,), width=3 * K)
        img.alpha_composite(tex("Node_CorpCore", 26 * K), (int(X + 18 * K), int(Y - 46 * K)))
    d = ImageDraw.Draw(img)

    # nodes
    for i, n in enumerate(m.nodes):
        X, Y = sx(n["x"]), sy(n["y"])
        if n["home"]:
            ic = Image.open(os.path.join(ROOT, "Textures", "UI", "Netrun", "Gizmo_NetTerminal.png")).convert("RGBA").resize((56 * K, 56 * K), Image.LANCZOS)
            img.alpha_composite(ic, (int(X - 10 * K), int(Y - 28 * K)))
            d.text((X - 4 * K, Y + 34 * K), "ДОМ", font=font(15 * K, True), fill=(90, 220, 120))
            continue
        if n["dark"]:
            col = (180, 100, 255)
        elif n["d"] == "hub":
            col = (60, 210, 230)
        else:
            col = DISTRICTS[n["d"]][1]
        r = 12 * K if n["d"] == "hub" else 10 * K
        if n["exit"]:
            d.rectangle((X - r - 3 * K, Y - r - 3 * K, X + r + 3 * K, Y + r + 3 * K), fill=(0, 0, 0))
            d.rectangle((X - r, Y - r, X + r, Y + r), fill=(20, 30, 40), outline=(60, 210, 230), width=3 * K)
            d.polygon([(X + 6 * K, Y), (X - 2 * K, Y - 5 * K), (X - 2 * K, Y + 5 * K)], fill=(60, 210, 230))
        else:
            d.ellipse((X - r - 3 * K, Y - r - 3 * K, X + r + 3 * K, Y + r + 3 * K), fill=(0, 0, 0))
            d.ellipse((X - r, Y - r, X + r, Y + r), fill=(20, 30, 40), outline=col, width=3 * K)
            if n["d"] == "hub":
                d.ellipse((X - 4 * K, Y - 4 * K, X + 4 * K, Y + 4 * K), fill=col)

    # district labels at their centres
    for dname, (cx, cy) in LAYOUTS[m.layout]["centers"].items():
        if SIZES[m.size].get(dname, 0) == 0:
            continue
        label = DISTRICTS[dname][0]
        f = font(14 * K, True)
        w = d.textlength(label, font=f)
        X, Y = sx(cx), sy(cy) + 26 * K
        if dname == "outskirts":
            X += 40 * K
        d.rectangle((X - w / 2 - 5 * K, Y - 2 * K, X + w / 2 + 5 * K, Y + 18 * K), fill=(10, 14, 18, 210))
        d.text((X - w / 2, Y), label, font=f, fill=DISTRICTS[dname][1])
    dx_ = [n for n in m.nodes if n["dark"]][0]
    d.text((sx(dx_["x"]) + 14 * K, sy(dx_["y"]) - 8 * K), "Тёмная сеть", font=font(13 * K, True), fill=(180, 100, 255))

    # title bar
    st = m.stats()
    d.rectangle((0, 0, W * K, top * K), fill=(16, 18, 22))
    d.text((20 * K, 16 * K), title or LAYOUTS[m.layout]["title"], font=font(21 * K, True), fill=(236, 240, 244))

    if legend:
        lx = (MW + 16) * K
        d.rectangle((MW * K, top * K, W * K, H * K), fill=(18, 20, 24))
        y = top + 16
        def line(t, col=(236, 240, 244), size=15, bold=True):
            nonlocal y
            d.text((lx, y * K), t, font=font(size * K, bold), fill=col); y += size + 9
        line(f"Размер: {m.size}", (60, 210, 230), 16)
        line(f"Узлов: {st['nodes']} + Старая сеть {st['oldnet']} + тёмный хаб 1", size=14, bold=False)
        line(f"Слотов ответвлений: {st['slots']}", size=14, bold=False)
        line(f"Точек выхода: {st['exits']}", size=14, bold=False)
        line(f"Связей: {st['links']} обычных, {st['express']} скоростных, {st['dark']} тёмных", size=14, bold=False)
        y += 8
        line("Часы от Дома (мин / сред / макс):", (60, 210, 230), 15)
        for dname in ["outskirts", "market", "hub", "campus", "fun", "ruins", "dump", "corpA", "corpB", "corpC", "corpD"]:
            if dname in st["hours"]:
                a, b, c = st["hours"][dname]
                line(f"{DISTRICTS[dname][0]}: {a:.1f} / {b:.1f} / {c:.1f}", DISTRICTS[dname][1], 13, False)
        y += 10
        line("Обозначения", (60, 210, 230), 15)
        for t, col in [("━ скоростная магистраль (0.5 ч)", (60, 210, 230)), ("— обычная связь (1 ч)", (70, 190, 140)),
                       ("┅ тёмная связь (скрыта)", (180, 100, 255)), ("┇ Стена и ворота в Старую сеть", (220, 60, 70)),
                       ("□ точка выхода", (60, 210, 230)), ("◯ узел района, иконки рядом: ответвления", (200, 205, 212)),
                       ("⬡ пунктир: крепость корпорации", (200, 205, 212))]:
            line(t, col, 13, False)
    img.resize((W, H), Image.LANCZOS).convert("RGB").save(path)
    return st


def main():
    out = []
    for i, (lay, seed) in enumerate([("ring", 11), ("spine", 23), ("archipelago", 5), ("metro", 42)]):
        m = Map(lay, "normal", seed)
        st = render(m, os.path.join(MOCK, f"3{i}-map-{lay}.png"))
        out.append((lay, st))
    # sizes on one sheet (same layout, three sizes)
    thumbs = []
    for size, seed in [("small", 3), ("normal", 11), ("large", 8)]:
        m = Map("ring", size, seed)
        p = os.path.join(MOCK, f"_tmp_{size}.png")
        st = render(m, p, W=1120, H=720, legend=False, title=f"Кольцо, размер {size}: {st_line(m.stats())}")
        thumbs.append(Image.open(p)); os.remove(p)
        out.append(("ring-" + size, m.stats()))
    sheet = Image.new("RGB", (1120, 720 * 3 + 20), (16, 16, 18))
    for k, t in enumerate(thumbs):
        sheet.paste(t, (0, k * (720 + 10)))
    sheet.save(os.path.join(MOCK, "34-map-sizes.png"))
    for name, st in out:
        print(f"{name:18} nodes {st['nodes']:2} slots {st['slots']:2} exits {st['exits']} links {st['links']}+{st['express']} dark {st['dark']} max {st['max']:.1f}h  "
              + "  ".join(f"{k}:{v[1]:.1f}" for k, v in sorted(st['hours'].items())))


def st_line(st):
    return f"{st['nodes']} узлов, {st['slots']} слотов, {st['exits']} выходов, до {st['max']:.1f} ч от Дома"


if __name__ == "__main__":
    main()
