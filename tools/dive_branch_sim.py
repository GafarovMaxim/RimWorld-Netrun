"""Оценка баланса ветвления хака (docs/DIVE_BRANCHING.md). Только расчёт, в игру не входит.
Допущения: Black ICE бьёт с шансом 0.6 за ход ноды; исход удара: тошнота 60% (нырок продолжается),
блокировка 30% и кома 10% (нырок кончается, добыча теряется). Щит поглощает первый удар.
"""
import random, sys

def dive(mult, ice_turns, shield, trace, rng, hit=0.6):
    shield_left = 1 if shield else 0
    for _ in range(ice_turns):
        if rng.random() < hit:
            if shield_left:
                shield_left -= 1
                continue
            r = rng.random()
            if r >= 0.6:            # блокировка или кома
                return 0.0, trace
    return mult, trace

def ev(mult, ice_turns, trace, shield_share, n=200000, seed=1):
    rng = random.Random(seed); tot = 0.0; ok = 0
    for _ in range(n):
        g, _t = dive(mult, ice_turns, rng.random() < shield_share, trace, rng)
        tot += g; ok += g > 0
    return tot / n, ok / n

paths = {
    "линейный (как сейчас, ICE 2 хода)": (1.0, 2, 4),
    "сложный x1.6, ICE 2 хода": (1.6, 2, 4),
    "сложный x1.8, ICE 2 хода": (1.8, 2, 4),
    "простой x0.7, ICE 0": (0.7, 0, 1),
    "простой x0.8, ICE 0": (0.8, 0, 1),
    "простой x0.9, ICE 0": (0.9, 0, 1),
}
print(f"{'путь':38} {'EV(доля награды)':>18} {'успех':>8} {'след':>6} {'EV на 1 след':>13}")
for name, (m, t, tr) in paths.items():
    for sh in (0.0, 0.6):
        e, p = ev(m, t, tr, sh)
        print(f"{name:38} {e:18.2f} {p:8.0%} {tr:6d} {e/tr:13.2f}   щит {int(sh*100)}%")
