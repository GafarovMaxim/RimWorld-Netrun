"""Monte Carlo for the v3 dive plan (docs/BALANCE_PLAN.md, sections 3 and 20). Not game code.

Simulates a "competent player" policy on every planned node layout, solo and in a group of 2-3 runners, and
prints success rate, turns, noise, loot, alarm, hit and catcher numbers.

Model (v3 plan, numbers in TUNE below):
- hack chance = base + per_int * Int - per_str * strength + bonus (bonus capped), clamped 5..95%;
  gate strength = node defense + 0..2 (as now in DiveSession).
- 2 actions per turn (competent hacking speed), then the node turn.
- noise: failed gate +1, failed white ICE +2, failed grey/black +1, door +1, seen by a patrol +2.
  suspicion at 2, alarm at 4; a group of 3 is a bigger signature (alarm 2, suspicion 1).
- turns: a formula from the graph (turn_budget), the same for every layout.
- patrol: a token that moves 1 step per node turn on its route (cycle, ping-pong or a rotating gaze).
  quiet: seeing a runner on its step = +2 noise; suspicion: also the next step of its route; alarm: a runner it
  sees is hit (50%).
- ICE: grey and black strike only the runner who worked on them this turn (hacked or held).
  grey 50%: burns a program (the shield first). black 30% in quiet / 60% otherwise; a black hit ends the dive in
  40% (lockout 30% + coma 10%, as in HE), otherwise nausea and the dive goes on.
- alarm: the core files melt by 1 per node turn until read; a safe needs the Cipher minigame.
- a finishing hit: solo leaves with a decoy (75% loot) or drops the haul (50%); a group runner flatlines and a
  teammate within 3 steps pulls him out with the Pull out action (70%), else coma.
- group: shared deck channel (best deck + 2 MU per extra runner), one copy of each program (one shared shield),
  +1 grey ICE per extra runner, double-lock consoles must be opened in the same turn.

Policy assumptions (competent player): Int 10 (group 10/9/8), demon Reaper (+30%), programs Shield + Ghost solo
(Shield + Ghost + Decoy in a group), scans once on entering the ring (reveals the backdoor), waits one action
instead of stepping into the patrol's next sight, goes for the core first, takes files/safe only if the turns left
cover the detour plus the way out with 2 turns to spare, never touches the black ICE vault solo without a shield,
uses the minigames as a skilled player (Cipher 85%, Hold 70%, Pull out 70%) or Auto (hack chance - 15%).

Run:  python tools/sim_dive.py            (table, 20000 dives per row)
      python tools/sim_dive.py -n 5000     (faster)
"""
import random
import sys
from collections import deque

TUNE = dict(
    base=0.30, per_int=0.035, per_str=0.065, bonus=0.30, gate_extra=(0, 2),
    actions=2, suspicion=2, alarm=4,
    seen_noise=2, gate_fail_noise=1, white_fail_noise=2, ice_fail_noise=1,
    patrol_hit=0.5, grey_hit=0.5, black_hit_quiet=0.3, black_hit=0.6, black_end=0.4,
    cipher=0.85, solo_lock_cipher=0.6, hold=0.7, big_group_threshold=2, group_from=2, large_grey=1,
    turns_small=8, turns_medium=11, turns_large=12, turns_core=14,
    auto_turns=True, slack={"small": 3, "medium": 2, "large": 2, "core": 1}, pull_out=0.7, catcher_turn=3, catcher_every=2,
)

DEBUG = False
OBST = {"gate", "white", "grey", "black", "door", "lock"}


class G:
    """A node graph: kind and strength per step, links, patrol route, targets."""

    def __init__(self, name, defense, turns, core_loot, file_loot):
        self.name, self.defense, self.turns = name, defense, turns
        self.core_loot, self.file_loot = core_loot, file_loot
        self.kind, self.str, self.adj = {}, {}, {}
        self.patrols = []          # (mode, route)
        self.entry = None
        self.exits = []
        self.hidden_exits = []
        self.locks = []
        self.switches = []
        self.slack_mod = 0

    def add(self, i, kind, strength=0):
        self.kind[i], self.str[i] = kind, strength
        self.adj.setdefault(i, set())

    def link(self, a, b):
        self.adj[a].add(b)
        self.adj[b].add(a)

    def chain(self, ids):
        for a, b in zip(ids, ids[1:]):
            self.link(a, b)


def gate_str(g, rng, extra=0):
    lo, hi = TUNE["gate_extra"]
    return g.defense + rng.randint(lo, hi) + extra


# ---------- layouts (docs/BALANCE_PLAN.md, 3.1 and 3.10) ----------

def line(rng, defense=2):
    g = G("Line (small, def 2)", defense, 8, 2, 1)
    g.add(0, "entry"); g.add(1, "gate", gate_str(g, rng)); g.add(2, "files"); g.add(3, "gate", gate_str(g, rng))
    g.add(4, "core"); g.add(5, "exit")
    g.chain([0, 1, 2, 3, 4]); g.link(4, 5)
    g.entry, g.exits = 0, [0, 5]
    return g


def ring(rng, defense=3, backdoor=False, extra_grey=0):
    name = "Ring + backdoor (large, def 4)" if backdoor else "Ring (medium, def 3)"
    g = G(name, defense, TUNE["turns_large"] if backdoor else TUNE["turns_medium"], 6 if backdoor else 4, 2)
    if backdoor:
        extra_grey += TUNE["large_grey"]
        g.slack_mod = -1      # the backdoor already shortens the way out
    g.add(0, "entry"); g.add(1, "gate", gate_str(g, rng))
    ring_ids = list(range(10, 16))
    for r in ring_ids:
        g.add(r, "ring")
    g.chain(ring_ids); g.link(ring_ids[-1], ring_ids[0])
    g.link(0, 1); g.link(1, 10)
    g.add(20, "white", gate_str(g, rng)); g.add(21, "core"); g.link(13, 20); g.link(20, 21)
    g.add(30, "files"); g.link(11, 30)
    g.add(31, "safe"); g.link(15, 31)
    g.add(32, "black", defense + 2); g.add(33, "vault"); g.link(12, 32); g.link(32, 33)
    for k in range(extra_grey):
        gid = 40 + k
        a, b = ring_ids[(2 * k + 1) % 6], ring_ids[(2 * k + 2) % 6]
        g.adj[a].discard(b); g.adj[b].discard(a)
        g.add(gid, "grey", defense + 1); g.link(a, gid); g.link(gid, b)
    g.patrols.append(("cycle", [r for r in _cycle_order(g, 10)]))
    g.entry, g.exits = 0, [0]
    if backdoor:
        g.add(50, "exit"); g.link(14, 50); g.hidden_exits = [50]
    return g


def _cycle_order(g, start):
    order, prev, cur = [start], None, start
    while True:
        nxt = [n for n in g.adj[cur] if n != prev and g.kind[n] in ("ring", "grey")]
        if not nxt or nxt[0] == start or len(order) > 20:
            break
        prev, cur = cur, sorted(nxt)[0]
        if cur == start:
            break
        order.append(cur)
    return order


def double_ring(rng, defense=5, extra_grey=0):
    g = G("Double ring (corp core, def 5)", defense, TUNE["turns_core"], 12, 3)
    g.add(0, "entry"); g.add(1, "gate", gate_str(g, rng)); g.link(0, 1)
    outer = list(range(10, 18))
    for r in outer:
        g.add(r, "ring")
    g.chain(outer); g.link(outer[-1], outer[0]); g.link(1, 10)
    inner = list(range(60, 64))
    for r in inner:
        g.add(r, "ring")
    g.chain(inner); g.link(inner[-1], inner[0])
    g.add(70, "lock", defense + 1); g.add(71, "lock", defense + 1)
    g.link(12, 70); g.link(70, 60); g.link(16, 71); g.link(71, 62)
    g.locks = [70, 71]
    g.add(80, "black", 8); g.add(81, "core"); g.link(61, 80); g.link(80, 81)
    g.add(30, "files"); g.link(11, 30)
    g.add(31, "safe"); g.link(15, 31)
    g.add(90, "grey", defense + 1)
    g.adj[13].discard(14); g.adj[14].discard(13); g.link(13, 90); g.link(90, 14)
    for k in range(extra_grey):
        gid = 91 + k
        a, b = [(10, 11), (16, 17)][k % 2]
        g.adj[a].discard(b); g.adj[b].discard(a)
        g.add(gid, "grey", defense + 1); g.link(a, gid); g.link(gid, b)
    g.patrols.append(("cycle", _cycle_order(g, 10)))
    g.patrols.append(("cycle", inner))
    g.entry, g.exits = 0, [0]
    g.add(50, "exit"); g.link(17, 50); g.hidden_exits = [50]
    return g


def tree(rng, defense=3):
    """Tree vault: a trunk with a ping-pong guard, three branches, the core at the end of the longest one."""
    g = G("Tree vault (medium, def 3)", defense, TUNE["turns_medium"], 4, 2)
    g.add(0, "entry"); g.add(1, "gate", gate_str(g, rng)); g.add(2, "hall"); g.add(3, "hall"); g.add(4, "hall")
    g.chain([0, 1, 2, 3, 4])
    g.add(10, "files"); g.link(2, 10)
    g.add(11, "white", gate_str(g, rng)); g.add(12, "safe"); g.link(3, 11); g.link(11, 12)
    g.add(13, "white", gate_str(g, rng)); g.add(14, "hall"); g.add(15, "core"); g.chain([4, 13, 14, 15])
    g.add(16, "black", defense + 2); g.add(17, "vault"); g.link(14, 16); g.link(16, 17)
    g.patrols.append(("pingpong", [2, 3, 4, 14]))
    g.slack_mod = -1          # no loops: the way back is the way in, easy to plan
    g.entry, g.exits = 0, [0]
    return g


def maze(rng, defense=4, extra_grey=0):
    """Maze: a 3x4 grid of rooms, some links are doors (hack), the core in the far corner, two exits."""
    g = G("Maze (large, def 4)", defense, TUNE["turns_large"], 6, 2)
    extra_grey += TUNE["large_grey"]
    W, H = 4, 3
    rid = lambda x, y: 100 + y * W + x
    for y in range(H):
        for x in range(W):
            g.add(rid(x, y), "room")
    door_n = 0
    for y in range(H):
        for x in range(W):
            for dx, dy in ((1, 0), (0, 1)):
                nx, ny = x + dx, y + dy
                if nx >= W or ny >= H:
                    continue
                if rng.random() < 0.35:
                    d = 200 + door_n
                    door_n += 1
                    g.add(d, "door", gate_str(g, rng))
                    g.link(rid(x, y), d); g.link(d, rid(nx, ny))
                else:
                    g.link(rid(x, y), rid(nx, ny))
    g.add(0, "entry"); g.add(1, "gate", gate_str(g, rng)); g.link(0, 1); g.link(1, rid(0, 0))
    g.kind[rid(3, 2)] = "core"
    for nb in list(g.adj[rid(3, 2)]):
        g.adj[nb].discard(rid(3, 2))
    g.adj[rid(3, 2)] = set()
    g.add(300, "black", defense + 2); g.link(rid(2, 2), 300); g.link(300, rid(3, 2))
    g.kind[rid(3, 0)] = "files"
    g.kind[rid(0, 2)] = "safe"
    g.add(50, "exit"); g.link(rid(2, 2), 50); g.hidden_exits = [50]
    for k in range(extra_grey):
        g.kind[rid(1 + k, 1)] = "grey"; g.str[rid(1 + k, 1)] = defense + 1
    loop = [rid(1, 0), rid(2, 0), rid(2, 1), rid(1, 1)]
    g.patrols.append(("cycle", loop))
    for a, b in zip(loop, loop[1:] + loop[:1]):
        if b not in g.adj[a]:
            g.link(a, b)        # the patrol's corridor is always open
    g.entry, g.exits = 0, [0]
    return g


def star(rng, defense=4, extra_grey=0):
    """Hub and spokes: a central hall with a sentry whose gaze rotates over four spokes; the core spoke opens
    when two switch spokes are thrown."""
    g = G("Star (large, def 4)", defense, TUNE["turns_large"], 6, 2)
    extra_grey += TUNE["large_grey"]
    g.add(0, "entry"); g.add(1, "gate", gate_str(g, rng)); g.add(2, "hall"); g.chain([0, 1, 2])
    spokes = []
    for s, end in enumerate(["files", "switch", "switch", "safe"]):
        a, b = 10 + s * 10, 11 + s * 10
        g.add(a, "hall"); g.add(b, end); g.link(2, a); g.link(a, b)
        spokes.append(a)
    g.add(60, "lock", defense + 1); g.add(62, "black", defense + 2); g.add(61, "core")
    g.link(2, 60); g.link(60, 62); g.link(62, 61)
    g.locks = [60]
    g.switches = [21, 31]
    for k in range(extra_grey):
        g.kind[spokes[1 + k]] = "grey"; g.str[spokes[1 + k]] = defense + 1
    g.patrols.append(("gaze", spokes))
    g.entry, g.exits = 0, [0]
    return g


# ---------- simulation ----------

def hack_p(intel, strength):
    p = TUNE["base"] + TUNE["per_int"] * intel - TUNE["per_str"] * strength + TUNE["bonus"]
    return min(0.95, max(0.05, p))


class Patrol:
    def __init__(self, mode, route):
        self.mode, self.route, self.i, self.d = mode, route, 0, 1

    def pos(self):
        return self.route[self.i]

    def step(self):
        if self.mode == "pingpong":
            if not 0 <= self.i + self.d < len(self.route):
                self.d = -self.d
            self.i += self.d
        else:
            self.i = (self.i + 1) % len(self.route)

    def next_pos(self):
        if self.mode == "pingpong":
            d = self.d if 0 <= self.i + self.d < len(self.route) else -self.d
            return self.route[self.i + d]
        return self.route[(self.i + 1) % len(self.route)]

    def sees(self, g, state):
        p = self.pos()
        seen = {p}
        if self.mode == "gaze" and self.i % 2 == 0:
            seen.add(2)          # every other turn the gaze sweeps across the central hall
        if state >= 1:
            seen.add(2 if self.mode == "gaze" else self.next_pos())   # suspicion: also the next step of its route
        return seen


class Runner:
    def __init__(self, idx, intel):
        self.idx, self.intel = idx, intel
        self.pos, self.out, self.flat, self.coma = None, False, -1, False
        self.task = None


def bfs(g, start, goal, cleared):
    """Shortest path; obstacles are passable here (the walker hacks them before stepping in)."""
    prev = {start: None}
    q = deque([start])
    while q:
        c = q.popleft()
        if c == goal:
            break
        for n in g.adj[c]:
            if n not in prev:
                if g.kind[n] == "lock" and n not in cleared and n != goal:
                    continue
                if g.kind[n] == "core" and g.locks and not all(l in cleared for l in g.locks):
                    continue
                prev[n] = c
                q.append(n)
    if goal not in prev:
        return None
    path, c = [], goal
    while c is not None:
        path.append(c)
        c = prev[c]
    return path[::-1]


def dist(g, a, b, cleared):
    p = bfs(g, a, b, cleared)
    return len(p) - 1 if p else 99


def size_of(g):
    return "small" if g.defense <= 2 else "core" if g.defense >= 5 else "large" if "large" in g.name else "medium"


def turn_budget(g):
    """Turns = the shortest way to the core and out, gates counted as 1.5 actions, plus slack by size.
    The same rule the game would use, so every layout gets a fair budget."""
    every = set(g.kind)
    core = [i for i, k in g.kind.items() if k == "core"][0]
    path = bfs(g, g.entry, core, every)
    out = min(dist(g, core, e, every) for e in g.exits + g.hidden_exits)
    obst = sum(1 for i in path if g.kind[i] in OBST)
    hub = path[2] if len(path) > 2 else g.entry
    detour = sum(2 * dist(g, hub, sw, every) + 1 for sw in g.switches)
    actions = (len(path) - 1) + out + 1.5 * obst + 1 + (2 if g.locks else 0) + detour
    return int(-(-actions // TUNE["actions"])) + TUNE["slack"][size_of(g)] + g.slack_mod


def run(builder, n_runners, rng, catcher=False, auto=False):
    extra = n_runners - 1
    g = builder(rng, extra)
    if TUNE["auto_turns"]:
        g.turns = turn_budget(g)
    intels = [10, 9, 8][:n_runners]
    runners = [Runner(i, intels[i]) for i in range(n_runners)]
    for r in runners:
        r.pos = g.entry
    patrols = [Patrol(m, rt) for m, rt in g.patrols]
    for p in patrols:
        p.i = rng.randrange(len(p.route))
    cleared, looted = set(), set()
    noise, max_state, shield = 0, 0, 1
    bag, items, core_bonus = 0.0, 0, 0
    core_read = False
    seen_backdoor = False
    finishing = False
    imprint = False
    catcher_pos = None
    exits_used = []
    turn = 0
    cipher = TUNE["cipher"] if not auto else None
    locks_open = not g.locks
    engaged = {}

    pen = TUNE["big_group_threshold"] * max(0, n_runners - TUNE["group_from"])   # every extra runner is a bigger signature

    def state():
        return 2 if noise >= TUNE["alarm"] - pen else (1 if noise >= TUNE["suspicion"] - pen // 2 else 0)

    def exits():
        return g.exits + (g.hidden_exits if seen_backdoor else [])

    while turn < g.turns:
        turn += 1
        active = [r for r in runners if not r.out and not r.coma and r.flat < 0]
        if not active:
            break
        # lock plan: in a group two runners take the two consoles; solo opens one console with the Cipher
        pending_locks = [l for l in g.locks if l not in cleared]
        lock_try = {}
        engaged.clear()
        busy = set()
        for r in active:
            ap = 0 if r.idx in busy else TUNE["actions"]
            while ap > 0 and not r.out:
                st = state()
                left = g.turns - turn
                # flatlined teammate nearby: pull him out
                down = [o for o in runners if o.flat >= 0 and not o.coma and o is not r]
                if down and dist(g, r.pos, down[0].pos, cleared) <= 1:
                    if ap >= 2 or r.pos == down[0].pos:
                        ap = 0
                        if rng.random() < TUNE["pull_out"]:
                            down[0].flat = -1; down[0].out = True
                        else:
                            down[0].coma = True
                        continue
                # choose a goal
                if not seen_backdoor and g.hidden_exits and g.kind.get(r.pos) in ("ring", "room", "hall"):
                    seen_backdoor = True
                    ap -= 1
                    continue
                goal = None
                todo_sw = [sw for sw in g.switches if sw not in looted]
                if todo_sw and not locks_open:
                    mine = todo_sw[active.index(r) % len(todo_sw)] if len(active) > 1 else \
                        min(todo_sw, key=lambda sw: dist(g, r.pos, sw, cleared))
                    goal = mine
                    if r.pos == goal:
                        looted.add(goal)
                        ap -= 1
                        continue
                elif pending_locks and not locks_open:
                    if len(active) >= 2 and len(g.locks) == 2:
                        goal = g.locks[active.index(r) % 2] if active.index(r) < 2 else None
                    else:
                        goal = g.locks[0]
                core = [i for i, k in g.kind.items() if k == "core"][0]
                live = [o for o in active if not o.out and o.flat < 0]
                if goal is None and not core_read:
                    if len(live) == 1 or not locks_open and not g.locks:
                        goal = core
                    elif locks_open:
                        # the runner nearest to the core takes it, the rest follow him
                        lead = min(live, key=lambda o: (dist(g, o.pos, core, cleared), o.idx))
                        if lead is r:
                            goal = core
                        elif dist(g, r.pos, lead.pos, cleared) <= 1:
                            ap -= 1        # "Follow": next to the leader, ready to hold ICE or pull him out
                            continue
                        else:
                            goal = lead.pos
                    else:
                        # no console for this one: follow the first console runner
                        lead = live[0]
                        if dist(g, r.pos, lead.pos, cleared) <= 1:
                            ap -= 1
                            continue
                        goal = lead.pos
                if goal is None and len(live) == 1 and not core_read:
                    goal = core
                if goal is None:
                    ex = min(exits(), key=lambda e: dist(g, r.pos, e, cleared))
                    d_ex = dist(g, r.pos, ex, cleared)
                    for kind in ("files", "safe"):
                        tgt = [i for i, k in g.kind.items() if k == kind and i not in looted]
                        if not tgt:
                            continue
                        d = dist(g, r.pos, tgt[0], cleared) + dist(g, tgt[0], ex, cleared)
                        if left * TUNE["actions"] >= d + 4 and (core_read or len(active) > 1):
                            goal = tgt[0]
                            break
                    if goal is None:
                        goal = ex
                # at the goal
                if r.pos == goal:
                    k = g.kind[goal]
                    if k == "core":
                        bag += max(1, g.core_loot - core_bonus)
                        core_read = True
                        r.task = None
                        ap -= 1
                    elif k in ("files", "safe"):
                        looted.add(goal)
                        if k == "files":
                            bag += g.file_loot
                            ap -= 1
                        else:
                            if st == 2:
                                ok = rng.random() < (cipher if cipher else hack_p(r.intel, g.defense) - 0.15)
                                ap -= 2
                                if ok:
                                    items += 1
                            else:
                                items += 1
                                ap -= 1
                    elif goal in exits():
                        r.out = True
                        exits_used.append(goal)
                    else:
                        ap -= 1   # waiting on a console
                    continue
                path = bfs(g, r.pos, goal, cleared)
                if path is None or len(path) < 2:
                    # the lock blocks the way: try the console from here
                    ap -= 1
                    continue
                nxt = path[1]
                k = g.kind[nxt]
                if k in OBST and nxt not in cleared and k != "lock":
                    if k in ("black", "grey") and len(active) > 1:
                        helper = [o for o in active if o is not r and o.idx not in busy
                                  and dist(g, o.pos, nxt, cleared) <= 2]
                        if helper:
                            h = helper[0]
                            busy.add(h.idx)
                            ap -= 1
                            if rng.random() < TUNE["hold"]:
                                cleared.add(nxt)      # held: the runner slips past, the ICE is out for the dive
                            else:
                                engaged.setdefault(nxt, set()).add(h.idx)
                            continue
                    p = hack_p(r.intel, g.str[nxt])
                    engaged.setdefault(nxt, set()).add(r.idx)
                    if rng.random() < p:
                        cleared.add(nxt)
                    else:
                        noise += {"gate": TUNE["gate_fail_noise"], "door": TUNE["gate_fail_noise"],
                                  "white": TUNE["white_fail_noise"]}.get(k, TUNE["ice_fail_noise"])
                    ap -= 1
                    continue
                # patrol avoidance: wait one action instead of stepping into the next sight
                nxt_seen = set()
                for pt in patrols:
                    nxt_seen.add(pt.next_pos())
                    if pt.mode == "gaze" and (pt.i + 1) % 2 == 0:
                        nxt_seen.add(2)
                if nxt in nxt_seen and left * TUNE["actions"] > dist(g, r.pos, goal, cleared) + 3:
                    ap -= 1
                    continue
                r.pos = nxt
                ap -= 1
            # console attempt at the end of the runner's actions
            if r.pos in g.locks and r.pos not in cleared:
                lock_try[r.pos] = r
        # locks
        if g.locks and not locks_open:
            if len(g.locks) == 2 and len(active) >= 2:
                if all(l in lock_try for l in g.locks):
                    oks = [1 - (1 - hack_p(lock_try[l].intel, g.str[l])) ** 2 > rng.random() for l in g.locks]
                    if all(oks):
                        cleared.update(g.locks); locks_open = True
                    else:
                        noise += 1
            elif lock_try and not [sw for sw in g.switches if sw not in looted]:
                rr = list(lock_try.values())[0]
                c = TUNE["solo_lock_cipher"] if len(g.locks) == 2 else cipher   # one console, Cipher with 4 tries
                ok = rng.random() < (c if cipher else hack_p(rr.intel, g.str[g.locks[0]]) - 0.15)
                if ok:
                    cleared.update(g.locks); locks_open = True
                else:
                    noise += 2
        if DEBUG:
            print(turn, [(r.idx, r.pos, r.task, r.out, r.flat, r.coma) for r in runners], "noise", noise,
                  "pat", [pt.pos() for pt in patrols], "locks", locks_open, "core", core_read, "shield", shield)
        # node turn
        for pt in patrols:
            pt.step()
        st = state()
        for r in runners:
            if r.out or r.coma or r.flat >= 0:
                continue
            seen = any(r.pos in pt.sees(g, st) for pt in patrols)
            if seen:
                if st < 2:
                    noise += TUNE["seen_noise"]
                elif rng.random() < TUNE["patrol_hit"]:
                    if shield > 0:
                        shield -= 1
                    else:
                        finishing = True
                        r.flat = turn
            for n in list(g.adj[r.pos]) + [r.pos]:
                k = g.kind[n]
                if n in cleared or r.idx not in engaged.get(n, ()):
                    continue   # ICE strikes the runner who is working on it this turn
                if k == "grey" and rng.random() < TUNE["grey_hit"]:
                    if shield > 0:
                        shield -= 1      # grey burns the shield program first
                elif k == "black":
                    ch = TUNE["black_hit_quiet"] if st == 0 else TUNE["black_hit"]
                    if rng.random() < ch:
                        if shield > 0:
                            shield -= 1
                        elif rng.random() < TUNE["black_end"]:
                            finishing = True
                            r.flat = turn
        max_state = max(max_state, state())
        if state() == 2 and not core_read:
            core_bonus += 1
        if catcher:
            target = min((r for r in runners if not r.out and not r.coma), key=lambda r: r.idx, default=None)
            if turn == TUNE["catcher_turn"] and target is not None:
                far = [i for i, k in g.kind.items() if k in ("ring", "room", "hall", "entry")
                       and dist(g, i, target.pos, set(g.kind)) >= 3]
                catcher_pos = rng.choice(far) if far else g.entry
            elif catcher_pos is not None and target is not None and turn % TUNE["catcher_every"] == 0:
                p = bfs(g, catcher_pos, target.pos, cleared | {i for i, k in g.kind.items() if k not in OBST})
                if p and len(p) > 1 and (g.kind[p[1]] not in OBST or p[1] in cleared):
                    catcher_pos = p[1]
                if catcher_pos == target.pos:
                    if shield > 0:
                        shield -= 1
                        catcher_pos = g.entry
                    else:
                        imprint = True
                        catcher_pos = None
        # solo finishing hit: decoy or drop and leave
        if n_runners == 1 and runners[0].flat >= 0:
            bag *= 0.75 if bag >= 4 else 0.5
            runners[0].out = True
            break
        for r in runners:
            if r.flat >= 0 and turn - r.flat >= 2 and not r.out:
                r.coma = True
        if all(r.out or r.coma for r in runners):
            break
    stuck = any(not r.out and not r.coma for r in runners)
    left_ok = any(r.out for r in runners)
    timeout = stuck and not left_ok
    if stuck:
        bag *= 0.5 if not left_ok else 0.9   # an ejected teammate drops his part
    success = core_read and left_ok
    clean = success and not finishing
    return dict(success=success, clean=clean, turns=turn, noise=noise, loot=bag + 3 * items, alarm=max_state == 2,
                hit=finishing, ghost=max_state == 0 and success, imprint=imprint, coma=any(r.coma for r in runners))


ROWS = [
    ("solo", line, 1), ("solo", ring, 1), ("solo", tree, 1),
    ("solo", lambda rng, e: ring(rng, 4, True, e), 1), ("solo", maze, 1), ("solo", star, 1),
    ("solo", double_ring, 1),
    ("group", ring, 2), ("group", maze, 2), ("group", star, 2),
    ("group", double_ring, 2), ("group", double_ring, 3),
]


def main():
    n = 20000
    if "-n" in sys.argv:
        n = int(sys.argv[sys.argv.index("-n") + 1])
    rng = random.Random(42)
    print(f"{'layout':34} {'who':6} {'succ':>5} {'clean':>5} {'turns':>5} {'noise':>5} {'loot':>5} "
          f"{'alarm':>5} {'hit':>5} {'ghost':>5} {'coma':>5} {'catch':>5}")
    for who, b, k in ROWS:
        res = [run(lambda r, e: b(r, extra_grey=e) if b in (ring, double_ring, maze, star) else b(r, e) if b is not line and b is not tree else b(r), k, rng) for _ in range(n)]
        cat = [run(lambda r, e: b(r, extra_grey=e) if b in (ring, double_ring, maze, star) else b(r, e) if b is not line and b is not tree else b(r), k, rng, catcher=True) for _ in range(n // 4)]
        g = (b(random.Random(0), extra_grey=k - 1) if b in (ring, double_ring, maze, star)
             else b(random.Random(0), k - 1) if b not in (line, tree) else b(random.Random(0)))
        avg = lambda key: sum(r[key] for r in res) / n
        pct = lambda key, rs=res: 100 * sum(1 for r in rs if r[key]) / len(rs)
        print(f"{g.name:34} {who + str(k):6} {pct('success'):5.0f} {pct('clean'):5.0f} {avg('turns'):5.1f} "
              f"{avg('noise'):5.1f} {avg('loot'):5.1f} {pct('alarm'):5.0f} {pct('hit'):5.0f} {pct('ghost'):5.0f} "
              f"{pct('coma'):5.0f} {pct('imprint', cat):5.0f}")


if __name__ == "__main__":
    main()
