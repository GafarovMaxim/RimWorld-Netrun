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
import zlib
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
    "Clean": ("Чистый", "Неоновая ночь: тёплые аналоговые пады, стеклянные электропианные сигналы, редкая сирена вдали.",
              dict(tone="soft", noise=0.35, wet=1.0, bright=9000, click="tick", scale=None, pitch=1.0)),
    "Minimal": ("Минимализм", "Холодная комната: короткие чистые пипы, тихий дрон и далёкий колокол, всё очень скупо.",
                dict(tone="sine", noise=0.0, wet=0.6, bright=5000, click="soft", scale=None, pitch=1.0)),
    "Neon": ("Неон", "Синтвейв: суперсо-пады, бас-арпеджио, пилообразные щипки и гейт-эхо.",
             dict(tone="tri", noise=0.25, wet=1.3, bright=8000, click="pluck", scale=[0, 2, 3, 5, 7, 8, 10], pitch=1.0)),
    "RetroDeck": ("Ретро-дека", "Кибердека 80-х: чип-звуки в 8 бит, реле, модемные трели, сонар.",
                  dict(tone="square", noise=0.5, wet=0.5, bright=4500, click="relay", scale=None, pitch=0.85)),
    "Deep": ("Глубина", "Тяжёлая бездна: смычковые дроны, огромные колокола, гонги и длинное эхо.",
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



# ---------- v3: pack identity (voices, motifs, sweeps) and cyberpunk ambients ----------

EXTRA = {
    "Clean": dict(voice="fm", atk=0.003, dec=0.5, rt=0.9, wetm=0.28, echo=True, gate=False, crushb=0, sweep="smooth", level=-9,
                  M=dict(confirm=[(659, 0, .14), (988, .08, .22)], notify=[(880, 0, .14), (1319, .11, .26)],
                         tab=[(880, 0, .06), (1175, .035, .06), (1568, .07, .08)], hack=[(440, 0, .1), (554, .07, .1), (740, .14, .18)],
                         success=[(523, 0, .5), (659, .09, .5), (784, .18, .5), (1047, .27, .5), (1319, .36, .8)],
                         heat_down=[(1568, 0, .4), (1175, .1, .4), (880, .2, .4), (659, .3, .6)],
                         chime=[(1319, 0, 1.0), (1760, .16, 1.2)], fulfilled=[(262, 0, .9), (330, 0, .9), (392, 0, .9), (523, 0, .9)],
                         deny=[(330, 0, .12), (247, .1, .2)], request=[(523, 0, .14), (659, .13, .14), (880, .26, .3)],
                         alert=[(740, 0, .1), (740, .24, .1), (740, .48, .1)])),
    "Minimal": dict(voice="sine", atk=0.002, dec=0.3, rt=0.5, wetm=0.1, echo=False, gate=False, crushb=0, sweep="tiny", level=-14,
                    M=dict(confirm=[(523, 0, .09)], notify=[(784, 0, .1)], tab=[(660, 0, .04)], hack=[(392, 0, .06)],
                           success=[(392, 0, .3), (523, .13, .4)], heat_down=[(523, 0, .3), (392, .16, .4)],
                           chime=[(880, 0, .5)], fulfilled=[(330, 0, .6), (440, 0, .6)], deny=[(196, 0, .14)],
                           request=[(440, 0, .12), (587, .14, .2)], alert=[(330, 0, .12), (330, .3, .12)])),
    "Neon": dict(voice="saw", atk=0.002, dec=0.35, rt=1.1, wetm=0.35, echo=True, gate=True, crushb=0, sweep="saw", level=-8,
                 M=dict(confirm=[(440, 0, .12), (659, .07, .12), (880, .14, .3)], notify=[(660, 0, .12), (880, .09, .12), (1320, .18, .3)],
                        tab=[(330, 0, .06), (440, .04, .06), (660, .08, .08)], hack=[(220, 0, .1), (330, .06, .1), (440, .12, .2)],
                        success=[(440, 0, .2), (523, .06, .2), (659, .12, .2), (880, .18, .2), (1047, .24, .2), (1319, .3, .2), (1568, .36, .2), (2093, .42, .5)],
                        heat_down=[(1319, 0, .2), (1047, .08, .2), (880, .16, .2), (659, .24, .2), (523, .32, .4)],
                        chime=[(880, 0, .3), (1320, .12, .5)], fulfilled=[(220, 0, 1.2), (330, 0, 1.2), (440, 0, 1.2), (523, 0, 1.2)],
                        deny=[(220, 0, .25), (208, .03, .25)], request=[(440, 0, .1), (523, .1, .1), (659, .2, .3)],
                        alert=[(880, 0, .5), (660, .25, .5)])),
    "RetroDeck": dict(voice="chip", atk=0.001, dec=0.9, rt=0.3, wetm=0.08, echo=False, gate=True, crushb=6, sweep="steps", level=-8,
                      M=dict(confirm=[(523, 0, .05), (659, .05, .05), (784, .1, .12)], notify=[(1047, 0, .07), (1047, .1, .07)],
                             tab=[(1568, 0, .03)], hack=[(262, 0, .05), (262, .06, .05), (523, .12, .1)],
                             success=[(988, 0, .07), (1319, .07, .3), (659, .4, .08), (784, .48, .08), (1047, .56, .3)],
                             heat_down=[(1047, 0, .05), (988, .06, .05), (880, .12, .05), (784, .18, .05), (659, .24, .05), (523, .3, .05), (392, .36, .1)],
                             chime=[(1568, 0, .06), (2093, .08, .12)], fulfilled=[(262, 0, .1), (330, .1, .1), (392, .2, .1), (523, .3, .3)],
                             deny=[(110, 0, .1), (98, .1, .2)], request=[(523, 0, .06), (784, .08, .06), (1047, .16, .12)],
                             alert=[(1320, 0, .06), (880, .08, .06), (1320, .16, .06), (880, .24, .06), (1320, .4, .06), (880, .48, .06)])),
    "Deep": dict(voice="bell", atk=0.004, dec=0.9, rt=2.8, wetm=0.5, echo=True, gate=False, crushb=0, sweep="swell", level=-8,
                 M=dict(confirm=[(196, 0, 1.4), (294, .16, 1.2)], notify=[(330, 0, 1.6)], tab=[(262, 0, .4)], hack=[(147, 0, .7)],
                        success=[(262, 0, 1.4), (330, .4, 1.4), (392, .8, 1.4), (523, 1.2, 2.0)],
                        heat_down=[(523, 0, 1.0), (440, .45, 1.0), (330, .9, 1.0), (262, 1.35, 1.6)],
                        chime=[(392, 0, 1.6), (587, .3, 1.6)], fulfilled=[(147, 0, 2.0), (220, 0, 2.0), (294, 0, 2.0)],
                        deny=[(98, 0, .8)], request=[(220, 0, .5), (294, .4, .9)], alert=[(110, 0, 2.0), (165, .05, 2.0)])),
}


# Notes of the dive-action sounds per pack: a step through the net (move), a botched hack, a jack-out (exit).
# The step back (back) is derived from the step: the same notes in reverse order, a bit lower.
EXTRA["Clean"]["M"].update(move=[(784, 0, .07), (1175, .05, .14)], hackfail=[(330, 0, .1), (233, .07, .1), (165, .14, .24)],
                           exit=[(1175, 0, .1), (880, .07, .1), (587, .14, .1), (392, .21, .1), (262, .28, .4)])
EXTRA["Minimal"]["M"].update(move=[(660, 0, .04), (880, .04, .07)], hackfail=[(220, 0, .09), (185, .08, .16)],
                             exit=[(660, 0, .06), (440, .06, .08), (330, .14, .22)])
EXTRA["Neon"]["M"].update(move=[(330, 0, .05), (495, .04, .05), (660, .08, .12)], hackfail=[(220, 0, .1), (208, .02, .1), (165, .1, .1), (147, .18, .26)],
                          exit=[(1320, 0, .07), (990, .05, .07), (660, .1, .07), (495, .15, .07), (330, .2, .07), (220, .25, .32)])
EXTRA["RetroDeck"]["M"].update(move=[(1047, 0, .03), (1319, .035, .05)], hackfail=[(131, 0, .06), (110, .06, .06), (98, .12, .18)],
                               exit=[(988, 0, .05), (784, .05, .05), (659, .1, .05), (523, .15, .05), (392, .2, .05), (262, .25, .16)])
EXTRA["Deep"]["M"].update(move=[(294, 0, .35), (392, .1, .45)], hackfail=[(98, 0, .8), (104, .02, .8)],
                          exit=[(392, 0, .8), (294, .35, .8), (196, .7, 1.4)])


def dive_move():
    return motif("move", -17)


def dive_back():
    m = STYLE["M"]["move"]
    freqs = [f for f, _, _ in m][::-1]
    STYLE["M"]["_back"] = [(round(f * 0.7), st, d) for f, (_, st, d) in zip(freqs, m)]
    return motif("_back", -18)


def _glitch_burst(dur, seed, gain):
    n = int(SR * dur)
    return crush(bp(white(n, seed), 400, 5000) * env_ad(n, 0.0008, dur / 3), 5, 3) * gain


def dive_hackfail():
    notes = STYLE["M"]["hackfail"]
    total = max(st + d for _, st, d in notes) + 0.05
    x = np.zeros(int(SR * (total + 0.1)))
    for f, st, d in notes:
        n = int(SR * d)
        mix_at(x, vtone(nt(f), d) * env_ad(n, STYLE["atk"], max(d * STYLE["dec"], 0.03)), int(SR * st))
    mix_at(x, _glitch_burst(0.06, 21, 0.5), 0)
    mix_at(x, _glitch_burst(0.04, 22, 0.35), int(SR * 0.12))
    return vfx(x, total, -9)


def dive_exit():
    notes = STYLE["M"]["exit"]
    total = max(st + d for _, st, d in notes) + 0.05
    x = np.zeros(int(SR * (total + 0.2)))
    for f, st, d in notes:
        n = int(SR * d)
        mix_at(x, vtone(nt(f), d) * env_ad(n, STYLE["atk"], max(d * STYLE["dec"], 0.03)), int(SR * st))
    # the plug comes out: a short dry click at the end
    n = int(SR * 0.012)
    mix_at(x, bp(white(n, 31), 1200, 6000) * env_ad(n, 0.0002, 0.003) * 0.7, int(SR * (total - 0.02)))
    return vfx(x, total, -10)


def saw_w(f, n):
    fa = np.full(n, float(f)) if np.isscalar(f) else np.asarray(f, dtype=float)
    return 2 * ((np.cumsum(fa) / SR) % 1.0) - 1


def vtone(f, d):
    """A note in the current pack's voice: fm (glass e-piano), sine, saw (supersaw pluck), chip (25% pulse), bell."""
    k = STYLE["voice"]
    n = int(SR * d)
    fa = np.full(n, float(f)) if np.isscalar(f) else np.asarray(f, dtype=float)
    t = np.arange(n) / SR
    ph = 2 * np.pi * np.cumsum(fa) / SR
    if k == "fm":
        return (np.sin(ph + 1.6 * np.exp(-t / 0.18) * np.sin(2 * ph)) + 0.25 * np.sin(ph * 4.0) * np.exp(-t / 0.05)) / 1.2
    if k == "saw":
        x = (saw_w(fa * 0.994, n) + saw_w(fa, n) + saw_w(fa * 1.006, n)) / 3
        return lp(x, 3800) if n > 64 else x
    if k == "chip":
        frac = (np.cumsum(fa) / SR) % 1.0
        return np.where(frac < 0.25, 1.0, -1.0) * 0.7
    if k == "bell":
        return np.sin(ph + 2.4 * np.sin(1.414 * ph) * np.exp(-t / 0.5)) * 0.9 + 0.2 * np.sin(2.76 * ph) * np.exp(-t / 0.3)
    return np.sin(ph)


def vfx(x, tail=0.0, db=None):
    """Pack effects: bit crush, reverb, gate; then level."""
    if STYLE["crushb"]:
        x = crush(x, STYLE["crushb"], 2)
    if STYLE["wetm"] > 0:
        x = reverb(x, min(STYLE["rt"], 0.5 + 2.0 * tail) if tail else STYLE["rt"], STYLE["wetm"])
    if STYLE["echo"]:
        x = echo(x, 0.19, 0.3, 2)
    if STYLE["gate"]:
        n = int(SR * (tail + 0.12))
        if 0 < n < len(x):
            x = x[: n + int(SR * 0.04)].copy()
            x[n:] *= np.linspace(1, 0, len(x) - n)
    return fades(peak_norm(lp(x, STYLE["bright"]), db if db is not None else STYLE["level"]), 0.002, 0.02)


def motif(key, db=None, mult=1.0):
    notes = STYLE["M"][key]
    total = max(st + d for _, st, d in notes) + 0.05
    x = np.zeros(int(SR * (total + 0.1)))
    for f, st, d in notes:
        n = int(SR * d)
        w = vtone(nt(f), d) * env_ad(n, STYLE["atk"], max(d * STYLE["dec"], 0.03))
        mix_at(x, w, int(SR * st))
    return vfx(x * mult, total, db)


def ui_confirm():
    return motif("confirm")


def ui_notify():
    return motif("notify")


def ui_tab():
    return motif("tab", -12)


def ui_hack():
    return motif("hack", -10)


def ui_heat_down():
    return motif("heat_down", -10)


def dive_success():
    return motif("success")


def ai_chime():
    return motif("chime", -11)


def ai_fulfilled():
    return motif("fulfilled", -9)


def ai_request():
    return motif("request", -10)


def ui_deny():
    k = STYLE["voice"]
    if k == "chip":
        d = 0.3
        x = crush(np.sign(sine(np.linspace(130, 70, int(SR * d)), d)) * 0.7 + 0.3 * white(int(SR * d), 5), 4, 3) * env_ad(int(SR * d), 0.001, 0.12)
        return fades(peak_norm(lp(x, 3000), -9))
    if k == "saw":
        d = 0.35
        x = (saw_w(np.linspace(220, 110, int(SR * d)), int(SR * d)) + saw_w(np.linspace(226, 112, int(SR * d)), int(SR * d))) * env_ad(int(SR * d), 0.003, 0.15)
        return fades(peak_norm(lp(x, 1500), -8))
    if k == "bell":
        return motif("deny", -9)
    return motif("deny", -10)


def ui_alert():
    k = STYLE["voice"]
    if k == "saw":
        d = 1.0
        n = int(SR * d)
        f = np.concatenate([np.linspace(440, 880, n // 2), np.linspace(880, 440, n - n // 2)])
        x = (saw_w(f, n) + saw_w(f * 1.01, n)) * 0.5 * np.sin(np.pi * np.arange(n) / n) ** 0.5
        return fades(peak_norm(lp(reverb(x, 1.0, 0.25), 3500), -8), 0.01, 0.1)
    if k == "bell":
        x = motif("alert", -7)
        n = int(SR * 2.2)
        rumble = lp(white(n, 4), 120) * np.exp(-np.arange(n) / SR / 0.9) * 4
        return fades(peak_norm(add(x, rumble * 0.5), -7))
    return motif("alert", -8)


def ui_open():
    k = STYLE["sweep"]
    if k == "tiny":
        return motif("tab", -12)
    if k == "steps":
        x = np.zeros(int(SR * 0.7))
        for i, f in enumerate((262, 330, 392, 523, 659, 784, 1047)):
            mix_at(x, vtone(f, 0.06) * env_ad(int(SR * 0.06), 0.001, 0.05), int(SR * 0.055 * i))
        mix_at(x, vtone(1047, 0.2) * env_ad(int(SR * 0.2), 0.001, 0.12), int(SR * 0.42))
        return vfx(x, 0.6, -8)
    if k == "saw":
        d = 0.9
        n = int(SR * d)
        f = np.linspace(80, 900, n)
        x = (saw_w(f, n) + saw_w(f * 1.008, n)) * 0.5 * np.linspace(0, 1, n) ** 1.3 * np.exp(-np.linspace(0, 1.5, n))
        x = lp(x, 2500)
        return vfx(x, 0.9, -8)
    if k == "swell":
        d = 2.4
        n = int(SR * d)
        t = np.arange(n) / SR
        x = sum(np.sin(2 * np.pi * f * t) for f in (73.4, 110, 146.8)) * np.minimum(1, t / 1.4) ** 2 * np.exp(-np.maximum(0, t - 1.5) / 0.5) * 0.4
        mix_at(x, vtone(220, 1.2) * env_ad(int(SR * 1.2), 0.005, 0.5), int(SR * 1.3))
        return vfx(x, 2.4, -8)
    d = 0.7
    n = int(SR * d)
    s = sweep(160, 1300, d) * np.linspace(0, 1, n) ** 2 * np.exp(-np.linspace(0, 2.5, n))
    x = s + vtone(1568, 0.1)[:int(SR * 0.1)].tolist().__len__() * 0
    return vfx(add(s, pad(vtone(1175, 0.08) * env_ad(int(SR * 0.08), 0.002, 0.03) * 0.5, 0) if False else s * 0), 0.7, -8) if False else vfx(s, 0.7, -8)


def ui_close():
    k = STYLE["sweep"]
    if k == "tiny":
        return motif("tab", -14)
    if k == "steps":
        x = np.zeros(int(SR * 0.4))
        for i, f in enumerate((784, 659, 523, 392, 262)):
            mix_at(x, vtone(f, 0.05) * env_ad(int(SR * 0.05), 0.001, 0.04), int(SR * 0.05 * i))
        return vfx(x, 0.3, -10)
    if k == "swell":
        return motif("heat_down", -12, 0.6)
    if k == "saw":
        # a power-down: four plucks falling a fifth apart, then a soft sub thump (the old saw sweep sounded odd)
        x = np.zeros(int(SR * 0.9))
        for i, f in enumerate((1047, 784, 523, 392)):
            n = int(SR * 0.16)
            mix_at(x, lp(vtone(nt(f), 0.16), 3000) * env_ad(n, 0.002, 0.06) * (1.0 - 0.12 * i), int(SR * 0.075 * i))
        n = int(SR * 0.5)
        sub = sine(np.linspace(110, 52, n), 0.5) * env_ad(n, 0.004, 0.18) * 0.7
        mix_at(x, sub, int(SR * 0.3))
        return vfx(x, 0.6, -10)
    d = 0.5
    n = int(SR * d)
    f = np.linspace(900, 80, n)
    x = (saw_w(f, n) * 0.5 if k == "saw" else np.sin(2 * np.pi * np.cumsum(f) / SR)) * np.exp(-np.linspace(0, 4, n))
    return vfx(x, 0.5, -10)


def ui_heat_up():
    k = STYLE["sweep"]
    d = 1.4
    n = int(SR * d)
    if k == "steps":
        x = np.zeros(n)
        pos = 0.0
        gap = 0.28
        for i in range(6):
            mix_at(x, vtone(110 + 12 * i, 0.12) * env_ad(int(SR * 0.12), 0.001, 0.08), int(SR * pos))
            pos += gap
            gap *= 0.78
        return vfx(x, 1.2, -7)
    if k == "swell":
        t = np.arange(n) / SR
        x = lp(white(n, 6), 90) * 6 * np.exp(-t / 0.7) + sum(np.sin(2 * np.pi * f * t) for f in (55, 82)) * np.exp(-t / 0.9) * 0.4
        mix_at(x, vtone(110, 1.2) * env_ad(int(SR * 1.2), 0.004, 0.5), 0)
        return vfx(x, 1.4, -6)
    if k == "saw":
        f = np.linspace(55, 440, n) ** 1.0
        x = (saw_w(f, n) + saw_w(f * 1.01, n)) * 0.5 * np.linspace(0.1, 1, n) ** 2
        return vfx(lp(x, 2200), 1.4, -7)
    if k == "tiny":
        x = np.zeros(int(SR * 0.5))
        for i in range(2):
            mix_at(x, sine(70, 0.15) * env_ad(int(SR * 0.15), 0.004, 0.05), int(SR * 0.2 * i))
        return vfx(x, 0.4, -10)
    low = sweep(50, 90, d, "lin") * np.linspace(0.3, 1, n) * np.exp(-np.linspace(0, 2.2, n))
    beat = np.zeros(n)
    for j, pp in enumerate((0.0, 0.17, 0.5, 0.67)):
        mix_at(beat, sine(62, 0.18) * env_ad(int(SR * 0.18), 0.003, 0.06), int(SR * pp), 1.0 if j % 2 == 0 else 0.7)
    hi = sweep(500, 1800, d) * np.linspace(0, 0.35, n) ** 2 * np.exp(-np.linspace(0, 5, n))
    return vfx(low * 0.5 + beat + hi, 1.3, -7)


def dive_start():
    k = STYLE["sweep"]
    d = 1.9
    n = int(SR * d)
    if k == "steps":
        x = np.zeros(n)
        r = rng(3)
        pos = 0.0
        gap = 0.16
        notes = [262, 330, 392, 523, 659, 784, 1047, 1319]
        for i in range(18):
            mix_at(x, vtone(notes[min(i // 2, 7)] * 2 ** (i % 2 * 0), 0.05) * env_ad(int(SR * 0.05), 0.001, 0.04), int(SR * pos))
            pos += gap
            gap = max(0.05, gap * 0.9)
        x += crush(white(n, 8) * np.linspace(0, 0.15, n), 3, 4)
        return vfx(x, 1.7, -7)
    if k == "swell":
        t = np.arange(n) / SR
        x = sum(np.sin(2 * np.pi * f * t) for f in (55, 82.4, 110)) * np.minimum(1, t / 1.3) ** 2 * 0.4
        for j, f in enumerate((147, 220, 294, 440)):
            mix_at(x, vtone(f, 1.0) * env_ad(int(SR * 1.0), 0.004, 0.4) * 0.5, int(SR * (0.9 + 0.2 * j)))
        return vfx(x, 1.9, -7)
    if k == "saw":
        f = np.geomspace(60, 1800, n)
        x = (saw_w(f, n) + saw_w(f * 1.01, n)) * 0.5 * np.linspace(0, 1, n) ** 1.5
        gate_ = (np.sin(2 * np.pi * np.linspace(2, 9, n) * np.arange(n) / SR) > -0.3).astype(float)
        return vfx(lp(x * (0.5 + 0.5 * gate_), 3500), 1.9, -7)
    if k == "tiny":
        return motif("success", -12, 0.5)
    up = sweep(80, 2200, d) * np.linspace(0, 1, n) ** 1.5
    x = up * 0.6 * np.concatenate([np.ones(int(n * 0.8)), np.linspace(1, 0, n - int(n * 0.8))])
    r = rng(2)
    for i in range(14):
        mix_at(x, vtone(r.choice([1568, 2093, 2637, 3136]), 0.05) * env_ad(int(SR * 0.05), 0.001, 0.02) * 0.4, int(SR * r.uniform(0.6, 1.6)))
    return vfx(x, 1.9, -7)


def dive_fail():
    k = STYLE["sweep"]
    d = 1.3
    n = int(SR * d)
    if k == "steps":
        x = np.zeros(n)
        for i, f in enumerate((784, 659, 523, 440, 349, 262, 196, 131)):
            mix_at(x, vtone(f, 0.09) * env_ad(int(SR * 0.09), 0.001, 0.07), int(SR * 0.085 * i))
        x += crush(white(n, 9) * np.exp(-np.arange(n) / SR / 0.3) * 0.4, 3, 4)
        return vfx(x, 1.2, -7)
    if k == "saw":
        f = np.linspace(440, 40, n)
        x = (saw_w(f, n) + saw_w(f * 1.02, n)) * 0.5 * np.exp(-np.arange(n) / SR / 0.6)
        return vfx(lp(x, 1800), 1.3, -7)
    if k == "swell":
        t = np.arange(n) / SR
        x = lp(white(n, 10), 140) * 5 * np.exp(-t / 0.5) + vtone(65, 1.3) * env_ad(n, 0.003, 0.5)
        return vfx(x, 1.3, -6)
    s = sweep(220, 40, d) * np.exp(-np.linspace(0, 3, n))
    z = lp(white(n, 8), 2500) * np.exp(-np.linspace(0, 5, n)) * 0.25
    x = np.tanh(2.5 * (s + z))
    return vfx(crush(x, 6, 3) * 0.8 + x * 0.2, 1.3, -7)


# ---------- v3 ambients: cyberpunk, audible, still almost noise-free ----------

def saw_per(f, T, n, det=0.004):
    t = np.arange(n) / SR
    out = np.zeros(n)
    for dd in (-det, 0.0, det):
        ff = snap(f * (1 + dd), T)
        out += 2 * ((ff * t) % 1.0) - 1
    return out / 3


def swept_pad(freqs, T, n, lo, hi, rate, amp=1.0, det=0.004, seed=0):
    """Supersaw chord through a slowly sweeping low-pass (periodic: two filtered copies crossfaded by a periodic LFO)."""
    x = sum(saw_per(f, T, n, det) for f in freqs) / max(1, len(freqs))
    t = np.arange(n) / SR
    m = 0.5 + 0.5 * np.sin(2 * np.pi * snap(rate, T) * t + rng(seed).uniform(0, 6.28))
    return amp * (lp(x, lo) * (1 - m) + lp(x, hi) * m)


def siren(T, n, f0=420, f1=640, period=8.0, vol=0.06, at=0.0):
    d = period
    m = int(SR * d)
    t = np.arange(m) / SR
    f = f0 + (f1 - f0) * (0.5 - 0.5 * np.cos(2 * np.pi * t / d))
    w = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.sin(np.pi * t / d) ** 2 * vol
    w = echo(w, 0.45, 0.5, 3)
    y = np.zeros(n)
    mix_at(y, w, int(SR * at), 1.0, wrap=True)
    return y


def bellp(f, vol=0.12, d=1.6):
    k = tone(f, d, "bell") * env_ad(int(SR * d), 0.004, 0.5) * vol
    return echo(k, 0.5, 0.5, 3)


def chirp_burst(r, vol=0.05, steps=10, base=(900, 2400)):
    out = []
    for _ in range(steps):
        d = r.uniform(0.02, 0.05)
        out.append(tone(r.uniform(*base), d, "sine") * env_ad(int(SR * d), 0.001, d / 2) * vol)
    return np.concatenate(out)


def amb_v3(pack, name):
    T = 16.0 if name != "server" else 12.0
    n = int(SR * T)
    t = np.arange(n) / SR
    y = np.zeros(n)
    r = rng(zlib.crc32(f"{pack}/{name}".encode()) % 10000)
    am9 = [110, 164.81, 220, 329.63, 392]
    if pack == "Clean":
        if name == "terminal":
            y += swept_pad(am9, T, n, 380, 1500, 0.06) * 0.5 + part(55, T, n) * 0.15
            y += siren(T, n, 420, 640, 8.0, 0.025, 1.0)
        elif name == "cyberspace":
            y += swept_pad([f * 2 for f in am9], T, n, 500, 2200, 0.05, seed=2) * 0.45 + part(110, T, n) * 0.1
            step = T / 32
            for i in range(32):
                f = [440, 659, 880, 659, 523, 659, 784, 988][i % 8]
                # a dull pluck (no metallic ring): the same melody, without the bell
                b = lp(tone(f, 0.3, "sine") + 0.25 * tone(f * 2, 0.3, "sine"), 2200) * env_ad(int(SR * 0.3), 0.004, 0.05) * 0.06
                mix_at(y, echo(b, 0.375, 0.35, 3), int(SR * step * i), 1.0, wrap=True)
            y += siren(T, n, 500, 760, 10.0, 0.03, 4.0)
        elif name == "dive":
            y += swept_pad([110, 164.81, 207.65], T, n, 260, 900, 0.09, seed=3) * 0.5
            throb = 0.5 + 0.5 * np.sin(2 * np.pi * snap(1.5, T) * t)
            y += part(55, T, n) * throb * 0.35
            sprinkle(y, T, 8, lambda rr: tone(rr.choice([1760, 2349, 2794]), 0.05) * env_ad(int(SR * 0.05), 0.001, 0.015) * 0.07, 43)
        else:
            y += swept_pad([55, 110, 165], T, n, 260, 700, 0.1, seed=4) * 0.5 + part(50, T, n) * 0.12 + part(3100, T, n) * 0.005
    elif pack == "Minimal":
        if name == "terminal":
            y += (part(110, T, n) + 0.6 * part(165, T, n) + 0.3 * part(82.41, T, n)) * slow(T, n, 0.1, 0.4) * 0.25
            sprinkle(y, T, 2, lambda rr: bellp(rr.choice([880, 1175]), 0.07, 1.5), 51, 3.0)
        elif name == "cyberspace":
            y += (part(130.8, T, n) + 0.7 * part(196, T, n) + 0.4 * part(261.6, T, n)) * slow(T, n, 0.07, 0.5) * 0.22
            y += siren(T, n, 600, 780, 12.0, 0.03, 3.0)
            sprinkle(y, T, 2, lambda rr: bellp(rr.choice([1175, 1568]), 0.07, 1.5), 52, 3.0)
        elif name == "dive":
            for k in range(int(T * 1.2)):
                p = k / 1.2
                mix_at(y, sine(58, 0.18) * env_ad(int(SR * 0.18), 0.01, 0.05) * 0.5, int(SR * p), 1.0, wrap=True)
                mix_at(y, sine(52, 0.16) * env_ad(int(SR * 0.16), 0.01, 0.04) * 0.3, int(SR * (p + 0.2)), 1.0, wrap=True)
            y += part(110, T, n) * 0.07
        else:
            y += (part(50, T, n) + 0.5 * part(100, T, n)) * 0.25
    elif pack == "Neon":
        chords = [(220, 261.63, 329.63), (174.61, 220, 261.63), (261.63, 329.63, 392), (196, 246.94, 293.66)]
        seg = T / 4
        for ci, ch in enumerate(chords):
            center = (ci + 0.5) * seg
            dist = np.abs(((t - center + T / 2) % T) - T / 2)
            env = np.clip(1 - dist / seg, 0, 1) ** 1.2
            y += env * swept_pad([f for f in ch], T, n, 500, 2400, 0.07, seed=ci) * 0.5
        if name in ("terminal", "cyberspace", "dive"):
            step = T / 32
            for i in range(32):
                ci = (i * 4) // 32
                root = chords[ci][0] / 2
                b = lp(saw_w(root, int(SR * 0.28)) + saw_w(root * 1.005, int(SR * 0.28)), 500) * env_ad(int(SR * 0.28), 0.004, 0.12) * (0.25 if name != "terminal" else 0.14)
                mix_at(y, b, int(SR * step * i), 1.0, wrap=True)
        if name in ("cyberspace", "dive"):
            step = T / 32
            patt = [0, 2, 1, 2, 0, 2, 1, 2]
            for i in range(32):
                ci = (i * 4) // 32
                f = chords[ci][patt[i % 8]] * 4
                b = vtone_pluck(f, 0.2) * 0.06
                mix_at(y, echo(b, 0.375, 0.4, 2), int(SR * step * i + 0), 1.0, wrap=True)
        if name == "dive":
            for i in range(16):
                mix_at(y, sine(np.linspace(110, 45, int(SR * 0.2)), 0.2) * env_ad(int(SR * 0.2), 0.002, 0.08) * 0.45, int(SR * (T / 16) * i), 1.0, wrap=True)
        if name == "server":
            y *= 0.6
            y += part(55, T, n) * 0.18
    elif pack == "RetroDeck":
        y += (part(60, T, n) + 0.4 * part(180, T, n) + 0.2 * part(300, T, n)) * 0.12
        y += part(2610, T, n) * 0.004
        relay = lambda rr: add(sine(150, 0.04) * env_ad(int(SR * 0.04), 0.0004, 0.01), bp(white(int(SR * 0.02), int(rr.integers(1000))), 900, 6000) * env_ad(int(SR * 0.02), 0.0002, 0.003) * 0.6) * 0.09
        if name in ("terminal", "cyberspace", "dive"):
            bass = [55, 55, 82.41, 65.41, 55, 55, 98, 73.42]
            step = T / 32 if name != "terminal" else T / 16
            cnt = 32 if name != "terminal" else 16
            for i in range(cnt):
                f = bass[i % 8]
                frac = (np.cumsum(np.full(int(SR * 0.12), f * 2)) / SR) % 1.0
                b = np.where(frac < 0.25, 1.0, -1.0) * env_ad(int(SR * 0.12), 0.001, 0.07) * (0.16 if name != "dive" else 0.22)
                mix_at(y, lp(b, 900), int(SR * step * i), 1.0, wrap=True)
        if name == "cyberspace":
            scale = [440, 523.25, 587.33, 659.25, 784, 880]
            rr = rng(60)
            for i in range(32):
                if rr.random() < 0.55:
                    frac = (np.cumsum(np.full(int(SR * 0.1), rr.choice(scale) * 2)) / SR) % 1.0
                    b = np.where(frac < 0.25, 1.0, -1.0) * env_ad(int(SR * 0.1), 0.001, 0.04) * 0.05
                    mix_at(y, echo(b, 0.25, 0.4, 3), int(SR * (T / 32) * i), 1.0, wrap=True)
        if name == "dive":
            for k in range(4):
                mix_at(y, echo(sine(1200, 0.4) * env_ad(int(SR * 0.4), 0.002, 0.09) * 0.09, 0.5, 0.5, 4), int(SR * 4 * k), 1.0, wrap=True)
        sprinkle(y, T, 8, relay, 61)
        sprinkle(y, T, 2, lambda rr: crush(chirp_burst(rr, 0.05, 12), 5, 3), 62, 3.0)
        if name == "server":
            sprinkle(y, T, 6, lambda rr: bp(white(int(SR * 0.012), int(rr.integers(1000))), 1500, 4500) * env_ad(int(SR * 0.012), 0.0002, 0.003) * 0.08, 63)
    else:  # Deep
        bow = lambda fr, cutoff=450, amp=1.0: lp(saw_per(fr, T, n, 0.006), cutoff) * slow(T, n, 0.06, 0.5, int(fr)) * amp
        if name == "terminal":
            y += bow(55, 380, 0.5) + bow(82.41, 420, 0.4) + part(41.2, T, n) * 0.3
        elif name == "cyberspace":
            y += bow(110, 600, 0.35) + bow(146.83, 700, 0.3) + bow(220, 800, 0.2)
            y += siren(T, n, 300, 470, 12.0, 0.05, 2.0)
        elif name == "dive":
            pulse = (0.5 + 0.5 * np.sin(2 * np.pi * snap(0.25, T) * t - 1.5)) ** 3
            y += part(36.71, T, n) * 0.5 + bow(73.42, 500, 0.5) * pulse
            y += bow(77.78, 500, 0.3) * pulse
        else:
            y += part(41.2, T, n) * 0.35 + bow(82.41, 300, 0.3)
    return rms_norm(y, STYLE_RMS[pack][name])


STYLE_RMS = {
    "Clean": dict(terminal=-21, cyberspace=-21, dive=-20, server=-24),
    "Minimal": dict(terminal=-25, cyberspace=-25, dive=-24, server=-27),
    "Neon": dict(terminal=-20, cyberspace=-20, dive=-19, server=-24),
    "RetroDeck": dict(terminal=-21, cyberspace=-21, dive=-20, server=-24),
    "Deep": dict(terminal=-20, cyberspace=-20, dive=-19, server=-24),
}


def vtone_pluck(f, d):
    n = int(SR * d)
    w = lp(saw_w(f, n) + saw_w(f * 1.006, n), 3500) * env_ad(n, 0.002, 0.06)
    return w


AMBIENT = {p: (lambda name, p=p: amb_v3(p, name)) for p in ("Clean", "Minimal", "Neon", "RetroDeck", "Deep")}

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
    "Dive/Move": (dive_move, "a step to the next step of a node", 40, "0.97~1.03", 2),
    "Dive/Back": (dive_back, "a step back (the same step reversed and lower)", 40, "0.97~1.03", 2),
    "Dive/HackFail": (dive_hackfail, "a hack of a gate or ICE fails", 55, "1~1", 1),
    "Dive/Exit": (dive_exit, "jack out of the dive (disconnect)", 55, "1~1", 1),
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
    "Ambient/Terminal": ("terminal", "terminal open (Core, Archive, Board, Club, Studio, server room)", 55),
    "Ambient/Cyberspace": ("cyberspace", "Gateway (cyberspace map)", 55),
    "Ambient/Dive": ("dive", "inside a dive", 60),
    "Ambient/Server": ("server", "AI server building (Building.soundAmbient, positional)", 45),
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
    server = "Server" in name
    if loop:
        lines += ["    <sustain>true</sustain>", "    <sustainFadeoutTime>1.6</sustainFadeoutTime>"]
    lines.append(f"    <maxSimultaneous>{maxsim}</maxSimultaneous>")
    if loop:
        # Sustainers may not use the default PrioritizeNewest (a config error at load).
        lines.append("    <priorityMode>PrioritizeNearest</priorityMode>")
    lines += ["    <subSounds>", "      <li>", f"        <grains>{grain}</grains>",
              f"        <volumeRange>{30 if server else vol}~{30 if server else vol}</volumeRange>"]
    if loop:
        lines += ["        <sustainAttack>1.6</sustainAttack>", "        <sustainSkipFirstAttack>false</sustainSkipFirstAttack>"]
    lines.append(f"        <pitchRange>{pitch}</pitchRange>")
    # The hum is not muted on pause: the terminal pauses the game, and the hum belongs to the room.
    lines += ["        <distRange>4~26</distRange>"] if server else ["        <onCamera>true</onCamera>"]
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
        STYLE.update(EXTRA[pack])
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
