"""Balance check for branched dives with the game's own rules and numbers (Defs/Cyberspace.xml).
Not part of the game. Plays many dives with a simple sensible policy and prints, per node and path:
success, failures by ICE, timeouts, the cyberdata brought home (vs the entry cost) and the trace.
Run: python tools/dive_branch_check.py
"""
import random

S = dict(maxTurns=8, hackBase=0.35, hackPerSkill=0.03, hackPerStrength=0.06, swordBonus=0.3, tracePerAction=1.0,
         iceAttackChance=0.6, retreatLootLoss=0.5, diveBaseCost=1, rewardMultLow=1.5, rewardMultMid=2.0, rewardMultBlackIce=3.0,
         hardReward=1.6, easyReward=0.8, hardTrace=4.0, easyTrace=1.0, passage=0.1, extraTurns=3)


def forks_for(defense):
    return 0 if defense <= 2 else 1 if defense <= 4 else 2


def build(defense, black_ice, ice_str, choice, rng, scan_passage):
    """A list of steps along the path actually taken: (kind, strength, extra)."""
    g = lambda d: max(1, defense + d + rng.randint(0, 2))
    steps = [("lobby", 0, None)]
    f = forks_for(defense)
    if f == 0:
        for _ in range(1 + defense // 2):
            steps.append(("gate", g(0), None))
        if rng.random() < 0.6:
            steps.append(("hidden_cache", 0, None))
        if black_ice:
            steps.append(("ice", ice_str, None))
        steps.append(("files", 0, None))
    else:
        steps.append(("gate", g(0), None))
        for k in range(f):
            if choice == "hard":
                steps += [("gate", g(1), None), ("cache", 0, None), ("ice", (ice_str if black_ice else defense + 1) - (1 if k > 0 else 0), None)]
            else:
                steps += [("gate", g(-1), None)] + ([("passage", 0, None)] if scan_passage else [])
            # The branches meet at a small junction (a visible cache), or at the main files after the last fork.
            steps.append(("cache", 0, None) if k < f - 1 else ("files", 0, None))
    steps.append(("memory", 0, None))
    steps.append(("exit", 0, None))
    return steps, f


def dive(defense, black_ice, choice, skill=8, apt=2, shield=0, scan_passage=True, rng=random, sword=False):
    ice_str = defense + rng.randint(1, 2)
    steps, f = build(defense, black_ice, ice_str, choice, rng, scan_passage)
    max_turns = S["maxTurns"] + f * S["extraTurns"]
    basis = max(S["diveBaseCost"] + defense // 2, S["diveBaseCost"] + defense // 2)
    if f == 0:
        mult = S["rewardMultBlackIce"] if black_ice else S["rewardMultLow"] if defense <= 2 else S["rewardMultMid"]
    else:
        base = S["rewardMultLow"] if defense <= 2 else S["rewardMultMid"]
        mult = base * (S["hardReward"] if choice == "hard" else S["easyReward"])
    trace = 0.0 if f == 0 else f * (S["hardTrace"] if choice == "hard" else S["easyTrace"])
    pos, turn, actions, loot, bonus = 0, 1, apt, 0.0, 0.0
    cleared = set()
    looted = set()

    def chance(strength):
        return min(0.95, max(0.05, S["hackBase"] + skill * S["hackPerSkill"] - strength * S["hackPerStrength"] + (S["swordBonus"] if sword else 0)))

    def ice_strike():
        nonlocal shield
        if shield > 0:
            shield -= 1
            return False
        return rng.random() >= 0.6  # lockout or coma ends the dive

    while True:
        nxt = pos + 1 if pos + 1 < len(steps) else None
        kind = steps[pos][0]
        nk = steps[nxt][0] if nxt is not None else None
        # Policy: hack what blocks, take what is here, scan the hidden cache ahead, move on, leave at the exit.
        if nk in ("gate", "ice") and nxt not in cleared:
            trace += S["tracePerAction"]
            if rng.random() < chance(steps[nxt][1]):
                cleared.add(nxt)
            elif nk == "ice" and ice_strike():
                return "ice", loot * 0, trace
        elif kind in ("files", "cache", "passage", "memory", "hidden_cache") and pos not in looted:
            trace += S["tracePerAction"]
            looted.add(pos)
            if kind == "files":
                loot += max(2, round(basis * (mult + bonus) * rng.uniform(0.85, 1.15)))
            elif kind in ("cache", "hidden_cache"):
                loot += rng.randint(1, 2)
            elif kind == "passage":
                bonus += S["passage"] * (S["rewardMultMid"] if defense > 2 else S["rewardMultLow"])
            elif kind == "memory":
                loot += 1.5 * 3 * 0.85  # advanced cyberdata, valued x3, most of the time
        elif nk in ("hidden_cache", "passage") and ("scan", nxt) not in cleared:
            trace += S["tracePerAction"]
            cleared.add(("scan", nxt))
        elif kind == "exit":
            return "ok", loot, trace
        else:
            trace += S["tracePerAction"]
            pos = nxt
        actions -= 1
        if actions <= 0:
            nxt = pos + 1 if pos + 1 < len(steps) else None
            if nxt is not None and steps[nxt][0] == "ice" and nxt not in cleared and rng.random() < S["iceAttackChance"]:
                if ice_strike():
                    return "ice", 0, trace
            turn += 1
            actions = apt
            if turn > max_turns:
                return "timeout", loot * (1 - S["retreatLootLoss"]), trace


def report(defense, black_ice, choice, shield=0, skill=8, n=20000, seed=7, sword=False, apt=2):
    rng = random.Random(seed)
    out = {"ok": 0, "ice": 0, "timeout": 0}
    loot = tr = 0.0
    for _ in range(n):
        r, l, t = dive(defense, black_ice, choice, skill=skill, shield=shield, rng=rng, sword=sword, apt=apt)
        out[r] += 1
        loot += l
        tr += t
    cost = S["diveBaseCost"] + defense // 2
    return out["ok"] / n, out["ice"] / n, out["timeout"] / n, loot / n, loot / n / cost, tr / n


RUNNERS = {"novice: skill 8": dict(skill=8), "prepared: skill 12, Sword, Shield": dict(skill=12, sword=True, shield=1)}

if __name__ == "__main__":
    for title, kw in RUNNERS.items():
        print("==", title)
        for defense, ice in [(2, False), (3, False), (4, True), (5, False), (5, True)]:
            for p in (["line"] if forks_for(defense) == 0 else ["hard", "easy"]):
                ok, ic, tm, l, x, t = report(defense, ice, p, **kw)
                print(f"  def {defense}{' +BlackICE' if ice else '         '} {p:5} ok {ok:4.0%}  ICE {ic:4.0%}  timeout {tm:4.0%}  cyberdata {l:5.1f} (x{x:.1f} cost)  trace {t:4.1f}")

if False:
    print(f"{'node':22} {'path':6} {'shield':6} {'ok':>5} {'ICE':>5} {'time':>5} {'cyberdata':>9} {'x cost':>7} {'trace':>6}")
    for defense, ice in [(2, False), (3, False), (4, True), (5, False), (5, True)]:
        paths = ["line"] if forks_for(defense) == 0 else ["hard", "easy"]
        for p in paths:
            for sh in (0, 1):
                ok, ic, tm, l, x, t = report(defense, ice, p, shield=sh)
                name = f"def {defense}{' +BlackICE' if ice else ''} forks {forks_for(defense)}"
                print(f"{name:22} {p:6} {sh:6} {ok:5.0%} {ic:5.0%} {tm:5.0%} {l:9.1f} {x:7.1f} {t:6.1f}")
