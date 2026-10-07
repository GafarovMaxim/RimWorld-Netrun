#!/usr/bin/env python3
"""
Procedural sound packs for Hacking Expansion: Netrun (no samples, no third-party licences).

    python3 tools/gen_sounds.py                 # all packs: Sounds/Netrun/<Pack>/**.ogg and Defs/Sounds.xml
    python3 tools/gen_sounds.py Neon            # one pack
    python3 tools/gen_sounds.py Neon glitch     # one pack, sounds whose name contains "glitch"

Needs numpy and ffmpeg. Everything is synthesised from sines, noise and simple filters. Ambient loops are periodic by
construction (every partial is a multiple of 1/length, noise is built in the frequency domain), so they loop without a
click. A pack is a STYLE (timbre, scale, noise, reverb, click type) plus its own four ambient loops. Files are mono;
one-shots 32 kHz, ambients 22.05 kHz, to keep the decoded audio small (RimWorld loads mod audio at start).
"""
import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

SR = 44100
BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
ROOT = os.path.join(BASE, "Sounds", "Netrun")


# ---------- building blocks ----------

def tt(d):
    return np.arange(int(SR * d)) / SR


def rng(seed):
    return np.random.default_rng(seed)


def env_ad(n, attack, decay_tau):
    e = np.exp(-np.arange(n) / SR / decay_tau)
    a = max(1, int(attack * SR))
    e[:a] *= np.linspace(0, 1, a)
    return e


def sine(freq, d, phase=0.0):
    """freq: number or array of instantaneous frequencies."""
    n = int(SR * d)
    f = np.full(n, float(freq)) if np.isscalar(freq) else np.asarray(freq, dtype=float)
    return np.sin(phase + 2 * np.pi * np.cumsum(f) / SR)


def sweep(f0, f1, d, curve="exp"):
    n = int(SR * d)
    x = np.linspace(0, 1, n)
    f = f0 * (f1 / f0) ** x if curve == "exp" else f0 + (f1 - f0) * x
    return sine(f, d)


def square(freq, d, soft=0.3):
    return np.tanh(np.sin(2 * np.pi * np.cumsum(np.full(int(SR * d), float(freq))) / SR) / max(soft, 1e-3)) * 0.8


def white(n, seed=0):
    return rng(seed).standard_normal(n)


def fft_filter(x, shape):
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    return np.fft.irfft(spec * shape(f), n=len(x))


def lp(x, fc):
    return fft_filter(x, lambda f: 1 / (1 + (f / fc) ** 4))


def hp(x, fc):
    return fft_filter(x, lambda f: 1 - 1 / (1 + (f / fc) ** 4))


def bp(x, f0, f1):
    return fft_filter(x, lambda f: (1 / (1 + (f / f1) ** 4)) * (1 - 1 / (1 + (f / f0) ** 4)))


def pad(x, n):
    out = np.zeros(n)
    out[: min(n, len(x))] = x[:n]
    return out


def add(*xs):
    n = max(len(x) for x in xs)
    return sum(pad(x, n) for x in xs)


def mix_at(dst, src, pos, gain=1.0, wrap=False):
    n = len(dst)
    if wrap:
        idx = (np.arange(len(src)) + pos) % n
        np.add.at(dst, idx, src * gain)
    else:
        end = min(n, pos + len(src))
        if end > pos:
            dst[pos:end] += src[: end - pos] * gain


def reverb(x, rt=1.2, wet=0.3, seed=7, pre=0.01):
    n = int(SR * rt * 1.5)
    ir = white(n, seed) * np.exp(-np.arange(n) / SR / (rt / 4.6))
    ir = lp(ir, 5000)
    ir[: int(pre * SR)] = 0
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    total = len(x) + n
    y = np.fft.irfft(np.fft.rfft(x, total) * np.fft.rfft(ir, total), n=total)
    out = np.zeros(total)
    out[: len(x)] += x * (1 - wet)
    out += y * wet * 2.0
    return out


def echo(x, delay, fb=0.4, taps=4, tail=None):
    n = len(x) + int(SR * delay * taps) + 1
    out = np.zeros(n)
    for i in range(taps + 1):
        mix_at(out, x, int(SR * delay * i), fb ** i)
    return out


def crush(x, bits=6, hold=4):
    q = 2 ** (bits - 1)
    y = np.round(x * q) / q
    return np.repeat(y[::hold], hold)[: len(x)]


def fades(x, a=0.003, b=0.01):
    x = x.copy()
    na, nb = int(a * SR), int(b * SR)
    if na:
        x[:na] *= np.linspace(0, 1, na)
    if nb:
        x[-nb:] *= np.linspace(1, 0, nb)
    return x


def peak_norm(x, db=-6.0):
    p = np.max(np.abs(x)) + 1e-9
    return x * (10 ** (db / 20) / p)


def rms_norm(x, db=-26.0):
    r = np.sqrt(np.mean(x ** 2)) + 1e-9
    return x * (10 ** (db / 20) / r)


def trim(x, thresh=2e-3):
    idx = np.where(np.abs(x) > thresh)[0]
    return x[: idx[-1] + 1] if len(idx) else x


def periodic_noise(n, lo, hi, slope=0.0, seed=0):
    """Noise that loops perfectly: random phases on a shaped spectrum, inverse FFT."""
    r = rng(seed)
    f = np.fft.rfftfreq(n, 1 / SR)
    mag = np.zeros_like(f)
    band = (f >= lo) & (f <= hi)
    mag[band] = (np.maximum(f[band], 1.0) / max(lo, 1.0)) ** slope
    ph = r.uniform(0, 2 * np.pi, len(f))
    y = np.fft.irfft(mag * np.exp(1j * ph), n=n)
    return y / (np.std(y) + 1e-9)


def snap(freq, T):
    """Nearest frequency whose period divides the loop length T seconds."""
    return round(freq * T) / T



# ---------- pack styles ----------

STYLE = {}

PACKS = {
    # name: (russian label, description, style)
    "Clean": ("Чистый", "Тихий терминал: ровный гул, редкие писки. Шума почти нет.",
              dict(tone="soft", noise=0.35, wet=1.0, bright=9000, click="tick", scale=None, pitch=1.0)),
    "Minimal": ("Минимализм", "Почти тишина: короткие мягкие щелчки, эмбиент едва слышен.",
                dict(tone="sine", noise=0.0, wet=0.6, bright=5000, click="soft", scale=None, pitch=1.0)),
    "Neon": ("Неон", "Синтвейв: тёплые пады, арпеджио, мелодичные сигналы.",
             dict(tone="tri", noise=0.25, wet=1.3, bright=8000, click="pluck", scale=[0, 2, 3, 5, 7, 8, 10], pitch=1.0)),
    "RetroDeck": ("Ретро-дека", "Кибердека 80-х: реле, клавиши, квадратные писки, сонар.",
                  dict(tone="square", noise=0.5, wet=0.5, bright=4500, click="relay", scale=None, pitch=0.85)),
    "Deep": ("Глубина", "Просторные колокольчики, суб-бас, длинное эхо. Без шума.",
             dict(tone="bell", noise=0.0, wet=1.8, bright=7000, click="soft", scale=[0, 2, 4, 7, 9], pitch=0.8)),
}


def tone(f, d, kind=None):
    """A musical tone in the current pack's timbre; f is a number or an array of frequencies."""
    kind = kind or STYLE["tone"]
    n = int(SR * d)
    fa = np.full(n, float(f)) if np.isscalar(f) else np.asarray(f, dtype=float)
    ph = 2 * np.pi * np.cumsum(fa) / SR
    t = np.arange(n) / SR
    if kind == "sine":
        return np.sin(ph)
    if kind == "soft":
        return (np.sin(ph) + 0.25 * np.sin(2 * ph) + 0.1 * np.sin(3 * ph)) / 1.2
    if kind == "tri":
        return sum(((-1) ** ((k - 1) // 2)) * np.sin(k * ph) / k ** 2 for k in (1, 3, 5, 7, 9)) * 0.9
    if kind == "square":
        return lp(np.tanh(3 * np.sin(ph)) * 0.8, 3800) if n > 64 else np.tanh(3 * np.sin(ph)) * 0.8
    if kind == "bell":
        return np.sin(ph + 2.2 * np.sin(1.414 * ph) * np.exp(-t / 0.35)) * 0.9
    if kind == "pluck":
        return sum(np.sin(k * ph) / k * np.exp(-t * k * 4) for k in range(1, 6)) * 1.1
    return np.sin(ph)


def nt(f):
    """Pitch of a note for this pack: global pitch factor, then snapped to its scale."""
    f = f * STYLE["pitch"]
    sc = STYLE["scale"]
    if not sc:
        return f
    semi = 12 * np.log2(f / 440.0)
    base = np.floor(semi / 12) * 12
    cands = [base + s + o for s in sc for o in (-12, 0, 12)]
    best = min(cands, key=lambda c: abs(c - semi))
    return 440.0 * 2 ** (best / 12)


def nz(x):
    return x * STYLE["noise"]


def wet(w):
    return min(0.7, w * STYLE["wet"])


def rv(x, rt, w, seed=7):
    return reverb(x, rt * (0.7 + 0.3 * STYLE["wet"]), wet(w), seed)


def finish(x, db=-6.0):
    return fades(peak_norm(lp(x, STYLE["bright"]), db))


# ---------- UI one-shots ----------

def ui_click(i):
    k = STYLE["click"]
    r = rng(i)
    if k == "tick":
        a = sine(1700 + r.uniform(-120, 120), 0.03) * env_ad(int(SR * 0.03), 0.0005, 0.007)
        n = hp(white(int(SR * 0.03), i), 3000) * env_ad(int(SR * 0.03), 0.0002, 0.003) * 0.4
        return finish(a + n, -9)
    if k == "relay":
        a = sine(150 + r.uniform(-15, 15), 0.04) * env_ad(int(SR * 0.04), 0.0004, 0.01)
        n = bp(white(int(SR * 0.02), i), 900, 6000) * env_ad(int(SR * 0.02), 0.0002, 0.003) * 0.6
        return finish(add(a * 0.8, n), -8)
    if k == "pluck":
        a = tone(nt(1760 * (1 + 0.12 * i)), 0.08) * env_ad(int(SR * 0.08), 0.001, 0.02)
        return finish(a, -10)
    a = tone(1400 + 90 * i, 0.05, "sine") * env_ad(int(SR * 0.05), 0.003, 0.014)
    return finish(a, -12)


def ui_hover(i):
    a = tone(nt(2300 + i * 150), 0.04, "sine") * env_ad(int(SR * 0.04), 0.004, 0.012)
    return finish(a, -20)


def ui_type(i):
    k = STYLE["click"]
    r = rng(i + 50)
    if k == "relay":
        n = bp(white(int(SR * 0.02), i + 50), 700, 4500) * env_ad(int(SR * 0.02), 0.0002, 0.004)
        t = sine(420 + r.uniform(-40, 40), 0.02) * env_ad(int(SR * 0.02), 0.0003, 0.006) * 0.7
        return finish(add(n, t), -12)
    if STYLE["noise"] == 0:
        return finish(tone(nt(900 + i * 70), 0.03, "sine") * env_ad(int(SR * 0.03), 0.002, 0.008), -20)
    n = nz(bp(white(int(SR * 0.025), i + 50), 1500, 5500)) * env_ad(int(SR * 0.025), 0.0003, 0.004)
    t = tone(nt(900 + r.uniform(-90, 90)), 0.025) * env_ad(int(SR * 0.025), 0.0003, 0.005) * 0.5
    return finish(n + t, -16)


def ui_confirm():
    a = tone(nt(660), 0.09) * env_ad(int(SR * 0.09), 0.003, 0.05)
    b = tone(nt(990), 0.14) * env_ad(int(SR * 0.14), 0.003, 0.07)
    x = np.zeros(int(SR * 0.3))
    mix_at(x, a, 0)
    mix_at(x, b, int(SR * 0.08))
    return finish(rv(x, 0.5, 0.18), -9)


def ui_deny():
    d = 0.22
    k = STYLE["tone"]
    x = (np.sign(sine(110, d)) * 0.5 + sine(165, d) * 0.5) * env_ad(int(SR * d), 0.004, 0.09)
    x = x * (0.6 + 0.4 * np.sign(sine(22, d)))
    if k in ("sine", "bell", "soft"):
        x = (sine(180, d) + sine(171, d)) * 0.5 * env_ad(int(SR * d), 0.004, 0.1)
    return finish(lp(x, 1400), -9)


def ui_tab():
    x = np.zeros(int(SR * 0.16))
    for i, f in enumerate((880, 1175, 1568)):
        mix_at(x, tone(nt(f), 0.05) * env_ad(int(SR * 0.05), 0.002, 0.018), int(SR * 0.035 * i))
    return finish(x, -12)


def ui_open():
    d = 0.7
    n = int(SR * d)
    s = sweep(160, 1300, d) * np.linspace(0, 1, n) ** 2 * np.exp(-np.linspace(0, 2.5, n))
    x = s + nz(bp(white(n, 3), 800, 6000)) * np.exp(-np.linspace(0, 6, n)) * 0.35
    for i, f in enumerate((1046, 1568)):
        mix_at(x, tone(nt(f), 0.08) * env_ad(int(SR * 0.08), 0.002, 0.03) * 0.5, int(SR * (0.42 + 0.09 * i)))
    return finish(rv(x, 0.7, 0.22), -8)


def ui_close():
    d = 0.45
    n = int(SR * d)
    x = sweep(1100, 150, d) * np.exp(-np.linspace(0, 4, n)) + nz(bp(white(n, 4), 600, 4000)) * np.exp(-np.linspace(0, 7, n)) * 0.25
    return finish(x, -10)


def ui_glitch(i=1):
    r = rng(i)
    parts = []
    for j in range(r.integers(4, 7)):
        d = r.uniform(0.012, 0.03)
        if r.random() < 0.6 or STYLE["noise"] == 0:
            base = tone(nt(r.uniform(300, 3200)), d, "sine")
        else:
            base = white(int(SR * d), i * 10 + j) * STYLE["noise"]
        parts.append(base * env_ad(len(base), 0.0005, d / 3))
        parts.append(np.zeros(int(SR * r.uniform(0.005, 0.02))))
    x = crush(np.concatenate(parts), bits=5, hold=3)
    return finish(bp(x, 400, 9000), -11)


def ui_notify():
    x = np.zeros(int(SR * 0.5))
    mix_at(x, tone(nt(880), 0.12) * env_ad(int(SR * 0.12), 0.003, 0.06), 0)
    mix_at(x, tone(nt(1320), 0.2) * env_ad(int(SR * 0.2), 0.003, 0.1), int(SR * 0.11))
    return finish(echo(rv(x, 0.8, 0.3), 0.16, 0.35, 2), -9)


def ui_alert():
    x = np.zeros(int(SR * 0.9))
    for i in range(3):
        d = 0.12
        b = (tone(nt(520), d, "square") * 0.5 + tone(nt(780), d) * 0.5) * env_ad(int(SR * d), 0.004, 0.07)
        mix_at(x, b, int(SR * 0.24 * i))
    return finish(lp(rv(x, 0.5, 0.2), 3500), -8)


def ui_heat_up():
    d = 1.3
    n = int(SR * d)
    low = sweep(50, 90, d, "lin") * np.linspace(0.3, 1, n) * np.exp(-np.linspace(0, 2.2, n))
    beat = np.zeros(n)
    for k, p in enumerate((0.0, 0.17, 0.5, 0.67)):
        mix_at(beat, sine(62, 0.18) * env_ad(int(SR * 0.18), 0.003, 0.06), int(SR * p), 1.0 if k % 2 == 0 else 0.7)
    hi = sweep(500, 1800, d) * np.linspace(0, 0.35, n) ** 2 * np.exp(-np.linspace(0, 5, n))
    return finish(rv(low * 0.5 + beat + hi, 1.0, 0.2), -7)


def ui_heat_down():
    x = np.zeros(int(SR * 1.2))
    for i, f in enumerate((1568, 1175, 880, 659)):
        mix_at(x, tone(nt(f), 0.4) * env_ad(int(SR * 0.4), 0.004, 0.16), int(SR * 0.1 * i), 0.8 ** i)
    return finish(rv(x, 1.2, 0.35), -10)


def ui_scan():
    x = tone(nt(1250), 0.5, "sine") * env_ad(int(SR * 0.5), 0.002, 0.12)
    x = x + 0.4 * tone(nt(2500), 0.5, "sine") * env_ad(int(SR * 0.5), 0.002, 0.05)
    return finish(rv(x, 1.6, 0.45), -10)


def ui_download():
    r = rng(11)
    x = np.zeros(int(SR * 1.0))
    pos = 0
    while pos < int(SR * 0.85):
        d = r.uniform(0.014, 0.03)
        f = r.choice([700, 880, 1100, 1320, 1760, 2200])
        mix_at(x, tone(nt(f), d) * env_ad(int(SR * d), 0.001, d / 3), pos, r.uniform(0.4, 0.9))
        pos += int(SR * r.uniform(0.02, 0.045))
    return finish(rv(x, 0.4, 0.12), -12)


def ui_hack():
    x = np.zeros(int(SR * 0.4))
    for i, f in enumerate((440, 554, 740)):
        mix_at(x, tone(nt(f), 0.12) * env_ad(int(SR * 0.12), 0.002, 0.06), int(SR * 0.07 * i))
    return finish(lp(rv(x, 0.4, 0.15), 4000), -10)


def dive_start():
    d = 1.7
    n = int(SR * d)
    up = sweep(80, 2200, d) * np.linspace(0, 1, n) ** 1.5
    x = (up * 0.6 + nz(bp(white(n, 5), 400, 8000)) * np.linspace(0, 1, n) ** 2 * 0.4)
    x = x * np.concatenate([np.ones(int(n * 0.8)), np.linspace(1, 0, n - int(n * 0.8))])
    r = rng(2)
    for i in range(14):
        mix_at(x, tone(nt(r.choice([1568, 2093, 2637, 3136])), 0.05) * env_ad(int(SR * 0.05), 0.001, 0.02) * 0.4, int(SR * r.uniform(0.6, 1.6)))
    return fades(peak_norm(lp(rv(x, 1.0, 0.25), STYLE["bright"]), -7), 0.01, 0.05)


def dive_success():
    x = np.zeros(int(SR * 1.6))
    for i, f in enumerate((523.25, 659.25, 783.99, 1046.5, 1318.5)):
        d = 0.5
        b = (tone(nt(f), d) + 0.3 * tone(nt(f * 2), d, "sine")) * env_ad(int(SR * d), 0.004, 0.18)
        mix_at(x, b, int(SR * 0.085 * i), 0.9)
    return finish(rv(x, 1.3, 0.4), -8)


def dive_fail():
    d = 1.3
    n = int(SR * d)
    s = sweep(220, 40, d) * np.exp(-np.linspace(0, 3, n))
    z = lp(white(n, 8), 2500) * np.exp(-np.linspace(0, 5, n)) * 0.6 * max(STYLE["noise"], 0.25)
    x = np.tanh(2.5 * (s + z))
    x = crush(x, 6, 3) * 0.8 + x * 0.2
    return finish(lp(x, 4000), -7)


def dive_ice():
    d = 0.6
    n = int(SR * d)
    thump = sine(np.linspace(110, 38, n), d) * np.exp(-np.arange(n) / SR / 0.14)
    z = bp(white(n, 9), 200, 4000) * np.exp(-np.arange(n) / SR / 0.06) * 0.7 * max(STYLE["noise"], 0.3)
    return finish(rv(np.tanh(2.2 * (thump + z)), 0.6, 0.15), -6)


# ---------- AI ----------

def ai_awaken():
    d = 4.5
    n = int(SR * d)
    t = np.arange(n) / SR
    swell = np.minimum(1, t / 2.6) ** 2 * np.exp(-np.maximum(0, t - 3.4) / 0.6)
    pad_ = np.zeros(n)
    for f in (110, 164.81, 220, 277.18, 329.63):
        for det in (-0.6, 0.0, 0.7):
            pad_ += tone(nt(f) + det, d, "sine") / 5
    pad_ *= swell
    shim = nz(bp(white(n, 12), 3000, 9000)) * swell * 0.18 * (0.6 + 0.4 * np.sin(2 * np.pi * 5 * t))
    x = pad_ * 0.5 + shim
    for i, f in enumerate((523.25, 659.25, 783.99, 1046.5)):
        mix_at(x, tone(nt(f), 0.6) * env_ad(int(SR * 0.6), 0.005, 0.25) * 0.35, int(SR * (2.9 + 0.12 * i)))
    return fades(peak_norm(lp(rv(x, 2.2, 0.4), STYLE["bright"]), -8), 0.01, 0.4)


def ai_whisper():
    d = 1.8
    n = int(SR * d)
    t = np.arange(n) / SR
    if STYLE["noise"] == 0:
        x = sum(tone(nt(f), d, "sine") * a for f, a in ((220, 1), (330, 0.7), (495, 0.5))) * np.exp(-((t - 0.9) ** 2) / 0.25)
        return finish(rv(x, 1.6, 0.4), -14)
    nzv = white(n, 21)
    x = bp(nzv, 700, 1100) + 0.8 * bp(nzv, 1800, 2600) + 0.5 * bp(nzv, 3200, 4200)
    syl = np.clip(np.sin(2 * np.pi * 3.1 * t + 0.6), 0, 1) ** 2 * np.minimum(1, t / 0.4) * np.minimum(1, (d - t) / 0.5)
    return fades(peak_norm(lp(rv(x * syl, 1.4, 0.35), STYLE["bright"]), -14), 0.02, 0.1)


def ai_request():
    x = np.zeros(int(SR * 0.7))
    mix_at(x, tone(np.linspace(nt(520), nt(700), int(SR * 0.16)), 0.16) * env_ad(int(SR * 0.16), 0.004, 0.09), 0)
    mix_at(x, tone(np.linspace(nt(700), nt(1040), int(SR * 0.26)), 0.26) * env_ad(int(SR * 0.26), 0.004, 0.14), int(SR * 0.17))
    return finish(echo(rv(x, 0.9, 0.3), 0.2, 0.3, 2), -10)


def ai_fulfilled():
    x = np.zeros(int(SR * 1.2))
    for f in (261.63, 329.63, 392.0, 523.25):
        mix_at(x, (tone(nt(f), 0.9) + 0.2 * tone(nt(f * 3), 0.9, "sine")) * env_ad(int(SR * 0.9), 0.02, 0.35) * 0.5, 0)
    return finish(rv(x, 1.5, 0.4), -9)


def ai_angry():
    d = 1.0
    n = int(SR * d)
    t = np.arange(n) / SR
    base = np.sign(sine(np.linspace(180, 90, n), d)) * 0.5 + np.sign(sine(np.linspace(190, 96, n), d)) * 0.5
    x = base * (0.6 + 0.4 * np.sign(sine(14, d))) * np.exp(-t / 0.6)
    x += nz(bp(white(n, 31), 800, 5000)) * np.exp(-t / 0.2) * 0.4
    return finish(lp(np.tanh(1.8 * x), 5000), -8)


def ai_feed():
    r = rng(41)
    x = np.zeros(int(SR * 0.55))
    for i in range(7):
        d = r.uniform(0.02, 0.05)
        if STYLE["noise"] == 0:
            b = tone(nt(r.choice([700, 900, 1200])), d, "sine") * env_ad(int(SR * d), 0.001, d / 3)
        else:
            b = crush(bp(white(int(SR * d), 41 + i), 600, 5000), 5, 4) * env_ad(int(SR * d), 0.001, d / 3)
        mix_at(x, b, int(SR * (0.07 * i + r.uniform(0, 0.02))), r.uniform(0.5, 1.0))
    mix_at(x, tone(nt(660), 0.1) * env_ad(int(SR * 0.1), 0.004, 0.05) * 0.5, int(SR * 0.45))
    return finish(x, -11)


def ai_chime():
    x = np.zeros(int(SR * 1.3))
    for i, f in enumerate((1318.5, 1760.0)):
        mix_at(x, (tone(nt(f), 1.0, "bell") + 0.3 * tone(nt(f * 2.76), 1.0, "sine")) * env_ad(int(SR * 1.0), 0.002, 0.3), int(SR * 0.16 * i))
    return finish(rv(x, 1.5, 0.4), -11)


def ai_echo():
    d = 2.0
    n = int(SR * d)
    f = nt(440) * (1 + 0.012 * np.sin(2 * np.pi * 5 * np.arange(n) / SR)) * np.linspace(1, 0.82, n)
    x = tone(f, d, "sine") * np.minimum(1, np.arange(n) / SR / 0.3) * np.exp(-np.arange(n) / SR / 0.7)
    return finish(rv(lp(x, 2500), 2.4, 0.5), -12)


# ---------- ambient loops (seamless, mono, low noise) ----------

def part(f, T, n, amp=1.0, ph=None, seed=0):
    t = np.arange(n) / SR
    p = rng(int(f * 10) + seed).uniform(0, 6.28) if ph is None else ph
    return amp * np.sin(2 * np.pi * snap(f, T) * t + p)


def slow(T, n, rate, depth=0.4, seed=0):
    t = np.arange(n) / SR
    return 1 - depth + depth * (0.5 + 0.5 * np.sin(2 * np.pi * snap(rate, T) * t + rng(seed).uniform(0, 6.28)))


def sprinkle(y, T, count, fn, seed, margin=2.5, wrap=True):
    r = rng(seed)
    n = len(y)
    for _ in range(count):
        mix_at(y, fn(r), int(r.uniform(0, n - SR * margin)), 1.0, wrap=wrap)


def ping(r, notes, vol=(0.04, 0.09), d=0.5, kind="sine", e_delay=0.3, taps=3):
    f = r.choice(notes)
    b = tone(f, d, kind) * env_ad(int(SR * d), 0.003, 0.12) * r.uniform(*vol)
    return echo(b, e_delay, 0.45, taps)


PENT = [1318.5, 1760, 2093, 2637, 3520]


def chord_pad(T, n, chords, kind="tri", level=0.12, seed=0):
    """Slowly crossfading chords, periodic over T."""
    t = np.arange(n) / SR
    seg = T / len(chords)
    y = np.zeros(n)
    for ci, ch in enumerate(chords):
        center = (ci + 0.5) * seg
        dist = np.abs(((t - center + T / 2) % T) - T / 2)
        env = np.clip(1 - dist / seg, 0, 1) ** 1.5
        for f in ch:
            for det in (-0.3, 0.35):
                y += env * part(f + det, T, n, 1.0, seed=ci) / 2 if kind == "sine" else env * soft_tri(snap(f + det, T), t) / 2
    return y * level


def soft_tri(f, t):
    ph = 2 * np.pi * f * t
    return sum(((-1) ** ((k - 1) // 2)) * np.sin(k * ph) / k ** 2 for k in (1, 3, 5)) * 0.9


def amb_clean(name):
    T = 16.0 if name != "server" else 12.0
    n = int(SR * T)
    if name == "terminal":
        y = (part(55, T, n, 1.0) + part(110, T, n, 0.5) + part(165, T, n, 0.15)) * slow(T, n, 0.12, 0.25) * 0.5
        y += periodic_noise(n, 4000, 9000, 0.0, seed=5) * 0.004
        sprinkle(y, T, 3, lambda r: ping(r, PENT, (0.03, 0.06)), 77)
        return rms_norm(y, -30)
    if name == "cyberspace":
        y = np.zeros(n)
        for f, a in ((110, 0.6), (164.81, 0.5), (220, 0.45), (329.63, 0.25)):
            for det in (-0.4, 0.5):
                y += a * slow(T, n, 0.06 + 0.02 * (f % 3), 0.5, int(f)) * part(f + det, T, n, 1.0)
        y *= 0.15
        y += periodic_noise(n, 600, 4000, -0.5, seed=9) * 0.01
        sprinkle(y, T, 4, lambda r: ping(r, PENT, (0.04, 0.08), 0.5, "sine", 0.3), 91)
        return rms_norm(y, -29)
    if name == "dive":
        throb = 0.6 + 0.4 * np.sin(2 * np.pi * snap(0.75, T) * np.arange(n) / SR)
        y = (part(55, T, n) + 0.6 * part(82.41, T, n) + 0.5 * part(110, T, n)) * throb * 0.4
        sprinkle(y, T, 4, lambda r: tone(r.choice([2093, 2793, 3520]), 0.04) * env_ad(int(SR * 0.04), 0.001, 0.012) * 0.05, 131)
        return rms_norm(y, -28)
    y = (part(50, T, n) + 0.5 * part(100, T, n) + 0.25 * part(150, T, n)) * 0.12
    y += periodic_noise(n, 150, 2500, -0.4, seed=17) * 0.05
    return rms_norm(y, -27)


def amb_minimal(name):
    T = 16.0 if name != "server" else 12.0
    n = int(SR * T)
    if name == "terminal":
        y = part(82.41, T, n) * slow(T, n, 0.1, 0.3) * 0.5
        sprinkle(y, T, 1, lambda r: ping(r, [1760, 2093], (0.05, 0.08), 0.6), 7)
        return rms_norm(y, -36)
    if name == "cyberspace":
        y = (part(110, T, n) + 0.5 * part(164.81, T, n)) * slow(T, n, 0.07, 0.5) * 0.4
        sprinkle(y, T, 2, lambda r: ping(r, [1760, 2637], (0.04, 0.07), 0.6), 8)
        return rms_norm(y, -36)
    if name == "dive":
        t = np.arange(n) / SR
        y = np.zeros(n)
        for k in range(int(T * 1.1)):
            p = k / 1.1
            mix_at(y, sine(58, 0.16) * env_ad(int(SR * 0.16), 0.01, 0.05), int(SR * p), 1.0, wrap=True)
            mix_at(y, sine(52, 0.14) * env_ad(int(SR * 0.14), 0.01, 0.04), int(SR * (p + 0.2)), 0.6, wrap=True)
        return rms_norm(y, -33)
    return rms_norm(part(50, T, n) + 0.3 * part(100, T, n), -34)


def amb_neon(name):
    T = 16.0 if name != "server" else 12.0
    n = int(SR * T)
    chords = [(220, 261.63, 329.63), (174.61, 220, 261.63), (261.63, 329.63, 392), (196, 246.94, 293.66)]
    if name == "terminal":
        y = chord_pad(T, n, chords, "tri", 0.13)
        y += part(55, T, n) * 0.08 * slow(T, n, 0.25, 0.6)
        return rms_norm(y, -29)
    if name == "cyberspace":
        y = chord_pad(T, n, chords, "tri", 0.1)
        step = T / 32
        pattern = [0, 2, 1, 2, 0, 2, 1, 2]
        for s in range(32):
            ci = (s * len(chords)) // 32
            f = chords[ci][pattern[s % 8]] * 2
            b = tone(f, 0.25, "pluck") * env_ad(int(SR * 0.25), 0.002, 0.07) * 0.05
            mix_at(y, echo(b, 0.375, 0.4, 2), int(SR * step * s), 1.0, wrap=True)
        return rms_norm(y, -29)
    if name == "dive":
        y = chord_pad(T, n, chords, "tri", 0.07)
        step = T / 32
        for s in range(32):
            ci = (s * len(chords)) // 32
            f = chords[ci][0] / 2
            b = lp(np.tanh(2 * np.sin(2 * np.pi * f * np.arange(int(SR * 0.3)) / SR)), 500) * env_ad(int(SR * 0.3), 0.004, 0.09) * 0.18
            mix_at(y, b, int(SR * step * s), 1.0, wrap=True)
        return rms_norm(y, -27)
    y = lp(np.tanh(1.5 * np.sin(2 * np.pi * snap(55, T) * np.arange(n) / SR)), 400) * slow(T, n, 0.2, 0.5) * 0.3
    return rms_norm(y + part(110, T, n) * 0.05, -29)


def amb_retro(name):
    T = 16.0 if name != "server" else 12.0
    n = int(SR * T)
    relay = lambda r: add(sine(150, 0.04) * env_ad(int(SR * 0.04), 0.0004, 0.01), bp(white(int(SR * 0.02), int(r.integers(1000))), 900, 6000) * env_ad(int(SR * 0.02), 0.0002, 0.003) * 0.6) * 0.07
    if name == "terminal":
        y = (part(60, T, n) + 0.4 * part(180, T, n) + 0.2 * part(300, T, n)) * 0.12
        y += part(2610, T, n) * 0.004
        sprinkle(y, T, 7, relay, 201)
        sprinkle(y, T, 2, lambda r: tone(r.choice([880, 1320, 1760]), 0.07, "square") * env_ad(int(SR * 0.07), 0.002, 0.04) * 0.05, 202)
        return rms_norm(y, -30)
    if name == "cyberspace":
        y = part(55, T, n) * 0.1
        r = rng(210)
        scale = [220, 261.63, 293.66, 329.63, 392, 440, 523.25]
        for s in range(32):
            if r.random() < 0.45:
                b = tone(r.choice(scale) * 2, 0.12, "square") * env_ad(int(SR * 0.12), 0.002, 0.05) * 0.05
                mix_at(y, echo(b, 0.25, 0.4, 3), int(SR * T / 32 * s), 1.0, wrap=True)
        return rms_norm(y, -30)
    if name == "dive":
        t = np.arange(n) / SR
        y = lp(np.sign(np.sin(2 * np.pi * snap(1.5, T) * t)) * 0.5 + 0.5, 150) * part(55, T, n) * 0.5
        for k in range(4):
            b = tone(1200, 0.4, "sine") * env_ad(int(SR * 0.4), 0.002, 0.09) * 0.08
            mix_at(y, echo(b, 0.5, 0.5, 4), int(SR * (T / 4) * k), 1.0, wrap=True)
        return rms_norm(y, -28)
    y = (part(60, T, n) + 0.4 * part(120, T, n)) * 0.12 + periodic_noise(n, 200, 2000, -0.5, seed=19) * 0.04
    sprinkle(y, T, 6, lambda r: bp(white(int(SR * 0.012), int(r.integers(1000))), 1500, 4500) * env_ad(int(SR * 0.012), 0.0002, 0.003) * 0.06, 220)
    return rms_norm(y, -29)


def amb_deep(name):
    T = 16.0 if name != "server" else 12.0
    n = int(SR * T)
    bell = lambda r, notes, vol=(0.05, 0.1): echo(tone(r.choice(notes), 1.2, "bell") * env_ad(int(SR * 1.2), 0.004, 0.45) * r.uniform(*vol), 0.55, 0.5, 3)
    if name == "terminal":
        y = (part(41.2, T, n) + 0.7 * part(55, T, n) + 0.4 * part(82.41, T, n)) * slow(T, n, 0.07, 0.4) * 0.35
        sprinkle(y, T, 2, lambda r: bell(r, [220, 329.63, 440]), 301, 3.0)
        return rms_norm(y, -30)
    if name == "cyberspace":
        y = (part(110, T, n) + 0.8 * part(146.83, T, n) + 0.4 * part(220, T, n)) * slow(T, n, 0.05, 0.5) * 0.25
        y += periodic_noise(n, 150, 900, -1.0, seed=29) * slow(T, n, 0.06, 0.8) * 0.012
        sprinkle(y, T, 4, lambda r: bell(r, [659.25, 987.77, 1318.5, 1760]), 311, 3.0)
        return rms_norm(y, -29)
    if name == "dive":
        t = np.arange(n) / SR
        pulse = (0.5 + 0.5 * np.sin(2 * np.pi * snap(0.25, T) * t - 1.5)) ** 3
        y = (part(36.71, T, n) * 0.5 + lp(np.tanh(2 * np.sin(2 * np.pi * snap(73.42, T) * t)), 400) * pulse * 0.4)
        sprinkle(y, T, 2, lambda r: bell(r, [440, 659.25], (0.03, 0.06)), 321, 3.0)
        return rms_norm(y, -28)
    y = (part(41.2, T, n) + 0.5 * part(82.41, T, n)) * slow(T, n, 0.08, 0.4) * 0.3
    return rms_norm(y, -30)


AMBIENT = {"Clean": amb_clean, "Minimal": amb_minimal, "Neon": amb_neon, "RetroDeck": amb_retro, "Deep": amb_deep}

# ---------- catalogue and output ----------

# name -> (generator, description, volume %, pitch range, max simultaneous, when)
ONE_SHOTS = {
    "UI/Click": (lambda: [ui_click(1), ui_click(2), ui_click(3)], "button press", 50, "0.96~1.04", 3),
    "UI/Hover": (lambda: [ui_hover(1), ui_hover(2)], "hover over a button or node (not more often than once per 80 ms)", 30, "0.97~1.03", 2),
    "UI/Type": (lambda: [ui_type(1), ui_type(2), ui_type(3), ui_type(4)], "one key tick while text is typed in", 38, "0.95~1.05", 4),
    "UI/Confirm": (ui_confirm, "confirm: contract accepted, deal made, scrub done", 55, "1~1", 1),
    "UI/Deny": (ui_deny, "refusal: unavailable action, failed deal", 55, "1~1", 1),
    "UI/Tab": (ui_tab, "switch of a tab or a screen", 50, "0.98~1.02", 1),
    "UI/Open": (ui_open, "the terminal window opens", 55, "1~1", 1),
    "UI/Close": (ui_close, "the terminal window closes", 50, "1~1", 1),
    "UI/Glitch": (lambda: [ui_glitch(1), ui_glitch(2), ui_glitch(3)], "short glitch (bottom bar on screen change, node frames); only with animations on", 45, "0.92~1.08", 2),
    "UI/Notify": (ui_notify, "new Archive entry, letter from the net", 50, "1~1", 1),
    "UI/Alert": (ui_alert, "Heat reached Target or Red zone, counter-hack, raid warning", 50, "1~1", 1),
    "UI/HeatUp": (ui_heat_up, "Heat level went up", 55, "1~1", 1),
    "UI/HeatDown": (ui_heat_down, "Heat level went down, scrub done", 50, "1~1", 1),
    "UI/Scan": (ui_scan, "node search, Scan action in a dive", 50, "1~1", 1),
    "UI/Download": (ui_download, "Download action, cyberdata received", 45, "1~1", 1),
    "UI/Hack": (ui_hack, "Hack action of a dive passes a gate", 50, "1~1", 1),
    "Dive/Start": (dive_start, "a dive starts", 60, "1~1", 1),
    "Dive/Success": (dive_success, "clean exit from a dive", 60, "1~1", 1),
    "Dive/Fail": (dive_fail, "dive failed or aborted", 60, "1~1", 1),
    "Dive/ICE": (dive_ice, "hit from ICE or Black ICE", 65, "0.95~1.05", 1),
    "AI/Awaken": (ai_awaken, "an AI wakes up", 60, "1~1", 1),
    "AI/Whisper": (ai_whisper, "AI events with a message, wild AI in memory", 40, "0.9~1.1", 1),
    "AI/Request": (ai_request, "an AI asks for something", 50, "1~1", 1),
    "AI/Fulfilled": (ai_fulfilled, "request fulfilled, trust grows", 55, "1~1", 1),
    "AI/Angry": (ai_angry, "AI is hurt, request failed, escape", 55, "1~1", 1),
    "AI/Feed": (ai_feed, "feeding an AI", 50, "1~1", 1),
    "AI/Chime": (ai_chime, "small AI event, service done", 45, "1~1", 1),
    "AI/Echo": (ai_echo, "Echo AI (a dead colonist) speaks", 45, "1~1", 1),
}

LOOPS = {
    "Ambient/Terminal": ("terminal", "terminal open (Core, Archive, Board, Club, Studio, server room)", 28),
    "Ambient/Cyberspace": ("cyberspace", "Gateway (cyberspace map)", 28),
    "Ambient/Dive": ("dive", "inside a dive", 30),
    "Ambient/Server": ("server", "AI server building (Building.soundAmbient, positional)", 22),
}


def write_wav(path, x, sr=SR):
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(data.tobytes())


def to_ogg(wav, ogg, rate, quality):
    os.makedirs(os.path.dirname(ogg), exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-ac", "1", "-ar", str(rate), "-c:a", "libvorbis", "-q:a", str(quality), ogg], check=True)


def sound_def(pack, name, desc, vol, pitch, maxsim, folder, loop):
    safe = name.replace("/", "_")
    path = f"Netrun/{pack}/{name}"
    grain = (f'<li Class="AudioGrain_Folder"><clipFolderPath>{path}</clipFolderPath></li>' if folder
             else f'<li Class="AudioGrain_Clip"><clipPath>{path}</clipPath></li>')
    lines = [f"  <!-- {desc} -->", "  <SoundDef>", f"    <defName>NR_{pack}_{safe}</defName>", "    <context>Any</context>" if "Server" not in name else "    <context>MapOnly</context>"]
    if loop:
        lines.append("    <sustain>true</sustain>")
    lines += [f"    <maxSimultaneous>{maxsim}</maxSimultaneous>", "    <subSounds>", "      <li>", f"        <grains>{grain}</grains>",
              f"        <volumeRange>{vol}~{vol}</volumeRange>", f"        <pitchRange>{pitch}</pitchRange>"]
    lines.append("        <distRange>0~14</distRange>" if "Server" in name else "        <onCamera>true</onCamera>")
    lines += ["      </li>", "    </subSounds>", "  </SoundDef>", ""]
    return lines


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    packs = [a for a in args if a in PACKS] or list(PACKS)
    only = [a for a in args if a not in PACKS]
    tmp = tempfile.mkdtemp()
    xml = ['<?xml version="1.0" encoding="utf-8"?>', "<Defs>", "",
           "  <!-- Generated by tools/gen_sounds.py. SoundDef names: NR_<Pack>_<Folder>_<Name>, volumes are percent. See docs/SOUNDS.md. -->", ""]
    report = []
    for pack in PACKS:
        STYLE.clear()
        STYLE.update(PACKS[pack][2])
        for name, (fn, desc, vol, pitch, maxsim) in ONE_SHOTS.items():
            variants = fn() if name in ("UI/Click", "UI/Hover", "UI/Type", "UI/Glitch") else None
            if pack in packs and (not only or any(o.lower() in name.lower() for o in only)):
                clips = variants if variants is not None else [fn()]
                for i, x in enumerate(clips):
                    x = trim(x)
                    rel = f"{name}/{name.split('/')[1].lower()}{i + 1}" if variants is not None else name
                    wav = os.path.join(tmp, f"{pack}_{rel.replace('/', '_')}.wav")
                    write_wav(wav, x)
                    to_ogg(wav, os.path.join(ROOT, pack, rel + ".ogg"), 32000, 4)
                    report.append((f"{pack}/{rel}", len(x) / SR, 20 * np.log10(np.max(np.abs(x)) + 1e-9), 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)))
            xml += sound_def(pack, name, desc, vol, pitch, maxsim, variants is not None, False)
        for name, (key, desc, vol) in LOOPS.items():
            if pack in packs and (not only or any(o.lower() in name.lower() for o in only)):
                x = AMBIENT[pack](key)
                wav = os.path.join(tmp, f"{pack}_{name.replace('/', '_')}.wav")
                write_wav(wav, x)
                to_ogg(wav, os.path.join(ROOT, pack, name + ".ogg"), 22050, 3)
                report.append((f"{pack}/{name}", len(x) / SR, 20 * np.log10(np.max(np.abs(x)) + 1e-9), 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)))
            xml += sound_def(pack, name, desc, vol, "1~1", 1, False, True)
    xml += ["</Defs>", ""]
    if len(packs) == len(PACKS) and not only:
        with open(os.path.join(BASE, "Defs", "Sounds.xml"), "w", encoding="utf-8") as f:
            f.write("\n".join(xml))
    for name, d, pk, rm in report:
        print(f"{name:34s} {d:6.2f}s  peak {pk:6.1f} dB  rms {rm:6.1f} dB")


if __name__ == "__main__":
    main()
