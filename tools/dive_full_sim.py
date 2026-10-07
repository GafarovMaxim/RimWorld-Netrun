"""Full dive simulator for docs/DIVE_BALANCE.md (not game code).

Mirrors DiveSession: node size by defense (linear up to defense 2, one fork at 3-4, two at 5), gate strength =
defense + 0..2 (hard branch +1, easy -1), Black ICE on the hard branch, 1 trace per action, ICE strikes 60% when it
is the next element, a strike ends the dive in 40% (lockout 30%, coma 10%), turns = 8 + 3 per fork.

Compares the current rules with the proposal: "node alert" (each failed hack +1; at the threshold an ICE patrol
starts striking every node turn) and retuned hack numbers.

Run:  python tools/dive_full_sim.py            (prints both tables)
      python tools/dive_full_sim.py --search   (grid search for the proposal numbers)
"""
import random
import sys

CURRENT = dict(base=0.35, per_skill=0.03, per_str=0.06, alert_at=None, patrol=0.0, actions=2, turns=8, fork_turns=3)
PROPOSAL = dict(base=0.30, per_skill=0.035, per_str=0.065, alert_at=3, patrol=0.15, alert_free=1, actions=2, turns=8, fork_turns=3)

ICE_HIT = 0.6
ICE_END = 0.4          # lockout 30% + coma 10%


def build(defense, branch, rng):
    """Elements on the chosen path: ('gate'|'ice'|'loot'|'exit', strength, value)."""
    els = []
    if defense <= 2:
        for _ in range(1 + defense // 2):
            els.append(("gate", defense + rng.randint(0, 2), 0))
        els.append(("loot", 0, 1.0))
        return els + [("exit", 0, 0)], 0
    forks = 1 if defense <= 4 else 2
    els.append(("gate", defense + rng.randint(0, 2), 0))
    for f in range(forks):
        hard_here = branch == "hard" or (branch == "mixed" and f == 0)
        if hard_here:
            els.append(("gate", defense + 1 + rng.randint(0, 2), 0))
            els.append(("loot", 0, 0.3))                       # cache
            els.append(("ice", max(1, defense + 1 - (1 if f else 0)), 0))
        else:
            els.append(("gate", max(1, defense - 1 + rng.randint(0, 2)), 0))
        els.append(("loot", 0, 0.3 if f < forks - 1 else 1.0))  # junction cache / main files
    mult = {"hard": 1.6, "easy": 0.8, "mixed": 1.2}[branch]
    els = [(k, s, v * (mult if k == "loot" else 1)) for k, s, v in els]
    return els + [("exit", 0, 0)], forks


def dive(intel, defense, branch, P, rng, bonus=0.0, shield=False):
    els, forks = build(defense, branch, rng)
    max_turns = P["turns"] + forks * P["fork_turns"]
    turn, actions, trace, alert, loot, hits = 1, P["actions"], 0, 0, 0.0, 0
    shield_left = [1 if shield else 0]

    def strike_ends():
        if shield_left[0]:
            shield_left[0] = 0
            return False
        return rng.random() < ICE_END
    i = 0

    def chance(s):
        return max(0.05, min(0.95, P["base"] + P["per_skill"] * intel - P["per_str"] * s + bonus))

    def spend():
        nonlocal actions, turn, trace
        trace += 1
        actions -= 1
        if actions > 0:
            return None
        # node turn
        nxt = els[i] if i < len(els) else None
        if nxt and nxt[0] == "ice" and rng.random() < ICE_HIT:
            if strike_ends():
                return "ice"
        if P["alert_at"] and defense > P.get("alert_free", 0) and alert >= P["alert_at"] and rng.random() < P["patrol"]:
            if shield_left[0]:
                shield_left[0] = 0          # Shield / Aegis absorbs the first patrol strike
            else:
                return "patrol"             # a patrol strike throws the runner out (half the loot, no coma)
        turn += 1
        actions = P["actions"]
        if turn > max_turns:
            return "timeout"
        return None

    while i < len(els):
        kind, s, v = els[i]
        if kind in ("gate", "ice"):
            if rng.random() < chance(s):
                els[i] = ("cleared", 0, 0)
            elif kind == "gate":
                alert += 1     # only gates raise the node alert: Black ICE is its own danger
            r = spend()
            if r:
                return r, trace, loot * 0.5
            continue
        if kind == "loot":
            loot += v
            r = spend()
            if r:
                return r, trace, loot * 0.5
        if kind == "exit":
            return "ok", trace, loot
        i += 1
        r = spend()            # move to the next element
        if r:
            return r, trace, loot * 0.5
    return "ok", trace, loot


def table(P, n=20000, seed=1, rows=((5,), (10,), (15,))):
    rng = random.Random(seed)
    out = []
    for (intel,) in rows:
        for defense in (1, 2, 3, 4, 5):
            variants = [("easy", False)]
            if defense >= 3:
                variants += [("hard", False), ("hard", True)]
            if defense == 5:
                variants += [("mixed", True)]
            for branch, shield in variants:
                res = {"ok": 0, "ice": 0, "patrol": 0, "timeout": 0}
                tr = lt = 0.0
                for _ in range(n):
                    r, t, l = dive(intel, defense, branch, P, rng, shield=shield)
                    res[r] += 1; tr += t; lt += l
                name = branch + ("+щит" if shield else "")
                out.append((intel, defense, name, res["ok"] / n, (res["ice"] + res["patrol"]) / n, res["timeout"] / n, tr / n, lt / n))
    return out


def show(title, rows):
    print(title)
    print("Инт защ ветка      | пройти  ICE-конец  время   след  добыча")
    for intel, d, b, ok, ice, to, tr, lt in rows:
        print(f"{intel:3} {d:3} {b:10} | {ok*100:5.0f}%  {ice*100:6.0f}%  {to*100:5.0f}%  {tr:5.1f}  {lt:5.2f}")
    print()


TARGET = {(10, 1): 0.95, (10, 3): 0.75, (10, 5): 0.45}


def search():
    best = None
    for base in (0.25, 0.30, 0.35):
        for ps in (0.03, 0.035, 0.04):
            for pst in (0.06, 0.07, 0.08):
                for at in (2, 3, 4):
                    for patrol in (0.25, 0.35, 0.45):
                        P = dict(PROPOSAL, base=base, per_skill=ps, per_str=pst, alert_at=at, patrol=patrol)
                        rows = {(r[0], r[1], r[2]): r[3] for r in table(P, n=3000, rows=((10,), (15,)))}
                        err = sum((rows[(i, d, "easy")] - t) ** 2 for (i, d), t in TARGET.items())
                        lift = rows[(15, 5, "easy")] - rows[(10, 5, "easy")]
                        err += 0.5 * (lift - 0.18) ** 2
                        if best is None or err < best[0]:
                            best = (err, base, ps, pst, at, patrol)
    print("best:", best)


if __name__ == "__main__":
    if "--search" in sys.argv:
        search()
    else:
        show("Сейчас (числа из Defs/Cyberspace.xml):", table(CURRENT))
        show("Предложение (тревога ноды + новые числа):", table(PROPOSAL))
