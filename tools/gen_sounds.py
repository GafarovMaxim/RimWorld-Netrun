#!/usr/bin/env python3
"""
Procedural sound pack for Hacking Expansion: Netrun (no samples, no third-party licences).

    python3 tools/gen_sounds.py            # writes Sounds/Netrun/**.ogg (needs numpy and ffmpeg)
    python3 tools/gen_sounds.py --wav DIR  # also keeps the raw wav files in DIR

Everything is synthesised from sines, noise and simple filters. Ambient loops are periodic by construction
(every partial is a multiple of 1/length, noise is built in the frequency domain), so they loop without a click.
Levels: one-shots peak around -6 dBFS, ambients sit around -26 dBFS RMS; final volume is set in Defs/Sounds.xml.
"""
import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

SR = 44100
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Sounds", "Netrun")


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


# ---------- UI one-shots ----------

def ui_click(seed):
    r = rng(seed)
    f = 1700 + r.uniform(-120, 120)
    a = sine(f, 0.03) * env_ad(int(SR * 0.03), 0.0005, 0.007)
    n = hp(white(int(SR * 0.03), seed), 3000) * env_ad(int(SR * 0.03), 0.0002, 0.003) * 0.6
    return fades(peak_norm(a + n, -9))


def ui_hover(seed):
    f = 2300 + seed * 140
    a = sine(f, 0.04) * env_ad(int(SR * 0.04), 0.004, 0.012)
    return fades(peak_norm(a, -20))


def ui_type(seed):
    r = rng(seed + 50)
    n = bp(white(int(SR * 0.025), seed + 50), 1500, 5500) * env_ad(int(SR * 0.025), 0.0003, 0.004)
    t = sine(900 + r.uniform(-90, 90), 0.025) * env_ad(int(SR * 0.025), 0.0003, 0.005) * 0.5
    return fades(peak_norm(n + t, -16))


def ui_confirm():
    a = sine(660, 0.09) * env_ad(int(SR * 0.09), 0.003, 0.05)
    b = sine(990, 0.14) * env_ad(int(SR * 0.14), 0.003, 0.07)
    x = np.zeros(int(SR * 0.3))
    mix_at(x, a, 0)
    mix_at(x, b, int(SR * 0.08))
    x = reverb(x + 0.25 * lp(np.sign(x) * np.abs(x) ** 0.8, 3000), 0.5, 0.18)
    return fades(peak_norm(x, -9))


def ui_deny():
    d = 0.22
    x = (np.sign(sine(110, d)) * 0.5 + sine(165, d) * 0.5) * env_ad(int(SR * d), 0.004, 0.09)
    x = x * (0.6 + 0.4 * np.sign(sine(22, d)))
    return fades(peak_norm(lp(x, 1400), -9))


def ui_tab():
    x = np.zeros(int(SR * 0.16))
    for i, f in enumerate((880, 1175, 1568)):
        mix_at(x, sine(f, 0.05) * env_ad(int(SR * 0.05), 0.002, 0.018), int(SR * 0.035 * i))
    return fades(peak_norm(x, -12))


def ui_open():
    d = 0.7
    n = int(SR * d)
    s = sweep(160, 1300, d) * np.linspace(0, 1, n) ** 2 * np.exp(-np.linspace(0, 2.5, n))
    nz = bp(white(n, 3), 800, 6000) * np.exp(-np.linspace(0, 6, n)) * 0.35
    x = s + nz
    for i, f in enumerate((1046, 1568)):
        mix_at(x, sine(f, 0.08) * env_ad(int(SR * 0.08), 0.002, 0.03) * 0.5, int(SR * (0.42 + 0.09 * i)))
    return fades(peak_norm(reverb(x, 0.7, 0.22), -8))


def ui_close():
    d = 0.45
    n = int(SR * d)
    s = sweep(1100, 150, d) * np.exp(-np.linspace(0, 4, n))
    nz = bp(white(n, 4), 600, 4000) * np.exp(-np.linspace(0, 7, n)) * 0.25
    return fades(peak_norm(s + nz, -10))


def ui_glitch(seed=1):
    r = rng(seed)
    parts = []
    for i in range(r.integers(4, 7)):
        d = r.uniform(0.012, 0.03)
        base = sine(r.uniform(300, 3200), d) if r.random() < 0.6 else white(int(SR * d), seed * 10 + i)
        parts.append(base * env_ad(len(base), 0.0005, d / 3))
        parts.append(np.zeros(int(SR * r.uniform(0.005, 0.02))))
    x = np.concatenate(parts)
    x = crush(x, bits=5, hold=3)
    return fades(peak_norm(bp(x, 400, 9000), -11))


def ui_notify():
    x = np.zeros(int(SR * 0.5))
    mix_at(x, sine(880, 0.12) * env_ad(int(SR * 0.12), 0.003, 0.06), 0)
    mix_at(x, sine(1320, 0.2) * env_ad(int(SR * 0.2), 0.003, 0.1), int(SR * 0.11))
    x = echo(reverb(x, 0.8, 0.3), 0.16, 0.35, 2)
    return fades(peak_norm(x, -9))


def ui_alert():
    x = np.zeros(int(SR * 0.9))
    for i in range(3):
        d = 0.12
        b = (square(520, d, 0.5) * 0.5 + sine(780, d) * 0.5) * env_ad(int(SR * d), 0.004, 0.07)
        mix_at(x, b, int(SR * 0.24 * i))
    return fades(peak_norm(lp(reverb(x, 0.5, 0.2), 3500), -8))


def ui_heat_up():
    d = 1.3
    n = int(SR * d)
    low = sweep(50, 90, d, "lin") * np.linspace(0.3, 1, n) * np.exp(-np.linspace(0, 2.2, n))
    beat = np.zeros(n)
    for k, p in enumerate((0.0, 0.17, 0.5, 0.67)):
        mix_at(beat, sine(62, 0.18) * env_ad(int(SR * 0.18), 0.003, 0.06), int(SR * p), 1.0 if k % 2 == 0 else 0.7)
    hi = sweep(500, 1800, d) * np.linspace(0, 0.35, n) ** 2 * np.exp(-np.linspace(0, 5, n))
    return fades(peak_norm(reverb(low * 0.5 + beat + hi, 1.0, 0.2), -7))


def ui_heat_down():
    x = np.zeros(int(SR * 1.2))
    for i, f in enumerate((1568, 1175, 880, 659)):
        mix_at(x, sine(f, 0.4) * env_ad(int(SR * 0.4), 0.004, 0.16), int(SR * 0.1 * i), 0.8 ** i)
    return fades(peak_norm(reverb(x, 1.2, 0.35), -10))


def ui_scan():
    x = sine(1250, 0.5) * env_ad(int(SR * 0.5), 0.002, 0.12)
    x = x + 0.4 * sine(2500, 0.5) * env_ad(int(SR * 0.5), 0.002, 0.05)
    return fades(peak_norm(reverb(x, 1.6, 0.45), -10))


def ui_download():
    r = rng(11)
    x = np.zeros(int(SR * 1.0))
    pos = 0
    while pos < int(SR * 0.85):
        d = r.uniform(0.014, 0.03)
        f = r.choice([700, 880, 1100, 1320, 1760, 2200])
        mix_at(x, sine(f, d) * env_ad(int(SR * d), 0.001, d / 3), pos, r.uniform(0.4, 0.9))
        pos += int(SR * r.uniform(0.02, 0.045))
    return fades(peak_norm(reverb(x, 0.4, 0.12), -12))


def ui_dive_start():
    d = 1.7
    n = int(SR * d)
    up = sweep(80, 2200, d) * np.linspace(0, 1, n) ** 1.5
    nz = bp(white(n, 5), 400, 8000) * np.linspace(0, 1, n) ** 2 * 0.4
    x = (up * 0.6 + nz) * np.concatenate([np.ones(int(n * 0.8)), np.linspace(1, 0, n - int(n * 0.8))])
    r = rng(2)
    for i in range(14):
        mix_at(x, sine(r.choice([1568, 2093, 2637, 3136]), 0.05) * env_ad(int(SR * 0.05), 0.001, 0.02) * 0.4,
               int(SR * r.uniform(0.6, 1.6)))
    return fades(peak_norm(reverb(x, 1.0, 0.25), -7), 0.01, 0.05)


def ui_dive_success():
    notes = (523.25, 659.25, 783.99, 1046.5, 1318.5)
    x = np.zeros(int(SR * 1.6))
    for i, f in enumerate(notes):
        d = 0.5
        b = (sine(f, d) + 0.3 * sine(f * 2, d)) * env_ad(int(SR * d), 0.004, 0.18)
        mix_at(x, b, int(SR * 0.085 * i), 0.9)
    return fades(peak_norm(reverb(x, 1.3, 0.4), -8), 0.003, 0.05)


def ui_dive_fail():
    d = 1.3
    n = int(SR * d)
    s = sweep(220, 40, d) * np.exp(-np.linspace(0, 3, n))
    nz = lp(white(n, 8), 2500) * np.exp(-np.linspace(0, 5, n)) * 0.6
    x = np.tanh(2.5 * (s + nz))
    x = crush(x, 6, 3) * 0.8 + x * 0.2
    return fades(peak_norm(lp(x, 4000), -7), 0.002, 0.05)


def ui_ice():
    d = 0.6
    n = int(SR * d)
    thump = sine(np.linspace(110, 38, n), d) * np.exp(-np.arange(n) / SR / 0.14)
    nz = bp(white(n, 9), 200, 4000) * np.exp(-np.arange(n) / SR / 0.06) * 0.7
    x = np.tanh(2.2 * (thump + nz))
    return fades(peak_norm(reverb(x, 0.6, 0.15), -6), 0.001, 0.03)


def ui_hack():
    x = np.zeros(int(SR * 0.4))
    for i, f in enumerate((440, 554, 740)):
        mix_at(x, (sine(f, 0.12) * 0.7 + 0.3 * square(f, 0.12, 0.6)) * env_ad(int(SR * 0.12), 0.002, 0.06), int(SR * 0.07 * i))
    return fades(peak_norm(lp(reverb(x, 0.4, 0.15), 4000), -10))


# ---------- AI ----------

def ai_awaken():
    d = 4.5
    n = int(SR * d)
    t = np.arange(n) / SR
    swell = np.minimum(1, t / 2.6) ** 2 * np.exp(-np.maximum(0, t - 3.4) / 0.6)
    pad_ = np.zeros(n)
    for f in (110, 164.81, 220, 277.18, 329.63):
        for det in (-0.6, 0.0, 0.7):
            pad_ += sine(f + det, d, rng(int(f * 10 + det * 10)).uniform(0, 6)) / 5
    pad_ *= swell
    shim = bp(white(n, 12), 3000, 9000) * swell * 0.18 * (0.6 + 0.4 * np.sin(2 * np.pi * 5 * t))
    x = pad_ * 0.5 + shim
    for i, f in enumerate((523.25, 659.25, 783.99, 1046.5)):
        mix_at(x, sine(f, 0.6) * env_ad(int(SR * 0.6), 0.005, 0.25) * 0.35, int(SR * (2.9 + 0.12 * i)))
    return fades(peak_norm(reverb(x, 2.2, 0.4), -8), 0.01, 0.4)


def ai_whisper():
    d = 1.8
    n = int(SR * d)
    t = np.arange(n) / SR
    nz = white(n, 21)
    x = bp(nz, 700, 1100) + 0.8 * bp(nz, 1800, 2600) + 0.5 * bp(nz, 3200, 4200)
    syl = np.clip(np.sin(2 * np.pi * 3.1 * t + 0.6), 0, 1) ** 2 * np.minimum(1, t / 0.4) * np.minimum(1, (d - t) / 0.5)
    return fades(peak_norm(reverb(x * syl, 1.4, 0.35), -14), 0.02, 0.1)


def ai_request():
    x = np.zeros(int(SR * 0.7))
    mix_at(x, sine(np.linspace(520, 700, int(SR * 0.16)), 0.16) * env_ad(int(SR * 0.16), 0.004, 0.09), 0)
    mix_at(x, sine(np.linspace(700, 1040, int(SR * 0.26)), 0.26) * env_ad(int(SR * 0.26), 0.004, 0.14), int(SR * 0.17))
    return fades(peak_norm(echo(reverb(x, 0.9, 0.3), 0.2, 0.3, 2), -10))


def ai_fulfilled():
    x = np.zeros(int(SR * 1.2))
    for f in (261.63, 329.63, 392.0, 523.25):
        mix_at(x, (sine(f, 0.9) + 0.2 * sine(f * 3, 0.9)) * env_ad(int(SR * 0.9), 0.02, 0.35) * 0.5, 0)
    return fades(peak_norm(reverb(x, 1.5, 0.4), -9), 0.01, 0.1)


def ai_angry():
    d = 1.0
    n = int(SR * d)
    t = np.arange(n) / SR
    base = np.sign(sine(np.linspace(180, 90, n), d)) * 0.5 + np.sign(sine(np.linspace(190, 96, n), d)) * 0.5
    x = base * (0.6 + 0.4 * np.sign(sine(14, d))) * np.exp(-t / 0.6)
    x += bp(white(n, 31), 800, 5000) * np.exp(-t / 0.2) * 0.4
    return fades(peak_norm(lp(np.tanh(1.8 * x), 5000), -8), 0.003, 0.05)


def ai_feed():
    r = rng(41)
    x = np.zeros(int(SR * 0.55))
    for i in range(7):
        d = r.uniform(0.02, 0.05)
        b = crush(bp(white(int(SR * d), 41 + i), 600, 5000), 5, 4) * env_ad(int(SR * d), 0.001, d / 3)
        mix_at(x, b, int(SR * (0.07 * i + r.uniform(0, 0.02))), r.uniform(0.5, 1.0))
    mix_at(x, sine(660, 0.1) * env_ad(int(SR * 0.1), 0.004, 0.05) * 0.5, int(SR * 0.45))
    return fades(peak_norm(x, -11))


def ai_chime():
    x = np.zeros(int(SR * 1.3))
    for i, f in enumerate((1318.5, 1760.0)):
        mix_at(x, (sine(f, 1.0) + 0.3 * sine(f * 2.76, 1.0)) * env_ad(int(SR * 1.0), 0.002, 0.3), int(SR * 0.16 * i))
    return fades(peak_norm(reverb(x, 1.5, 0.4), -11), 0.002, 0.1)


def ai_echo():
    d = 2.0
    n = int(SR * d)
    f = 440 * (1 + 0.012 * np.sin(2 * np.pi * 5 * np.arange(n) / SR)) * np.linspace(1, 0.82, n)
    x = sine(f, d) * np.minimum(1, np.arange(n) / SR / 0.3) * np.exp(-np.arange(n) / SR / 0.7)
    return fades(peak_norm(reverb(lp(x, 2500), 2.4, 0.5), -12), 0.01, 0.2)


# ---------- ambient loops (seamless) ----------

def amb_terminal(T=32.0):
    n = int(SR * T)
    out = []
    for ch in range(2):
        s = ch * 100
        t = np.arange(n) / SR
        x = np.zeros(n)
        for f, a in ((55, 1.0), (110, 0.5), (165, 0.18), (220, 0.12)):
            ff = snap(f, T) + (0.0 if ch == 0 else 1 / T * 3)
            x += a * np.sin(2 * np.pi * ff * t + rng(int(f) + s).uniform(0, 6))
        x *= 1 + 0.12 * np.sin(2 * np.pi * snap(0.16, T) * t)
        fan = periodic_noise(n, 60, 2200, -0.7, seed=3 + s) * 0.22
        hiss = periodic_noise(n, 3000, 9000, 0.0, seed=5 + s) * 0.03
        y = x * 0.5 + fan + hiss
        r = rng(77 + s)
        for _ in range(9):
            f = r.choice([1760, 2093, 2637, 3136, 3951])
            d = r.uniform(0.03, 0.07)
            b = sine(f, d) * env_ad(int(SR * d), 0.002, d / 3) * r.uniform(0.04, 0.09)
            mix_at(y, echo(b, 0.18, 0.4, 2), int(r.uniform(0, n - SR)), wrap=True)
        out.append(y)
    st = np.stack(out, axis=1)
    return rms_norm(st, -27)


def amb_cyberspace(T=36.0):
    n = int(SR * T)
    out = []
    for ch in range(2):
        s = ch * 200
        t = np.arange(n) / SR
        x = np.zeros(n)
        for f, a in ((110, 0.6), (164.81, 0.5), (220, 0.45), (329.63, 0.3), (440, 0.18)):
            for det in (-0.4, 0.5):
                ff = snap(f + det + ch * 0.25, T)
                lfo = 0.6 + 0.4 * np.sin(2 * np.pi * snap(0.07 + 0.03 * (f % 3), T) * t + rng(int(f) + s).uniform(0, 6))
                x += a * lfo * np.sin(2 * np.pi * ff * t + rng(int(f * 7) + s).uniform(0, 6))
        air = periodic_noise(n, 400, 5000, -0.5, seed=9 + s) * 0.18
        sweep_env = 0.5 + 0.5 * np.sin(2 * np.pi * snap(0.045, T) * t + ch)
        y = x * 0.12 + air * sweep_env
        r = rng(91 + s)
        for _ in range(7):
            f = r.choice([1318.5, 1760, 2093, 2637, 3520])
            d = 0.5
            b = sine(f, d) * env_ad(int(SR * d), 0.003, 0.12) * r.uniform(0.04, 0.09)
            mix_at(y, echo(b, 0.3, 0.45, 3), int(r.uniform(0, n - SR * 2)), wrap=True)
        out.append(y)
    return rms_norm(np.stack(out, axis=1), -26)


def amb_dive(T=24.0):
    n = int(SR * T)
    out = []
    for ch in range(2):
        s = ch * 300
        t = np.arange(n) / SR
        throb = 0.55 + 0.45 * np.sin(2 * np.pi * snap(0.75, T) * t)
        x = np.zeros(n)
        for f, a in ((55, 1.0), (82.41, 0.6), (110, 0.5), (116.54, 0.3)):
            x += a * np.sin(2 * np.pi * snap(f + ch * 0.2, T) * t + rng(int(f) + s).uniform(0, 6))
        x *= throb * 0.4
        rise = periodic_noise(n, 800, 7000, 0.3, seed=13 + s) * (0.5 + 0.5 * np.sin(2 * np.pi * snap(1 / 12, T) * t)) ** 2 * 0.12
        y = x + rise
        r = rng(131 + s)
        for _ in range(12):
            d = r.uniform(0.02, 0.05)
            b = sine(r.choice([2093, 2793, 3520, 4186]), d) * env_ad(int(SR * d), 0.001, d / 3) * r.uniform(0.03, 0.08)
            mix_at(y, b, int(r.uniform(0, n - SR)), wrap=True)
        out.append(y)
    return rms_norm(np.stack(out, axis=1), -25)


def amb_server(T=20.0):
    """Mono: it is positioned on the building."""
    n = int(SR * T)
    t = np.arange(n) / SR
    hum = np.zeros(n)
    for f, a in ((snap(50, T), 1.0), (snap(100, T), 0.5), (snap(150, T), 0.25)):
        hum += a * np.sin(2 * np.pi * f * t)
    fan = periodic_noise(n, 120, 3500, -0.4, seed=17) * (0.8 + 0.2 * np.sin(2 * np.pi * snap(0.3, T) * t))
    coil = np.sin(2 * np.pi * snap(3100, T) * t) * 0.02 * (0.5 + 0.5 * np.sin(2 * np.pi * snap(0.4, T) * t))
    y = hum * 0.12 + fan * 0.4 + coil
    r = rng(171)
    for _ in range(6):
        d = 0.03
        b = sine(r.choice([1568, 2093, 2637]), d) * env_ad(int(SR * d), 0.001, 0.01) * 0.05
        mix_at(y, b, int(r.uniform(0, n - SR)), wrap=True)
    return rms_norm(y, -24)


# ---------- catalogue and output ----------

ONE_SHOTS = {
    "UI/Click/click1": lambda: ui_click(1),
    "UI/Click/click2": lambda: ui_click(2),
    "UI/Click/click3": lambda: ui_click(3),
    "UI/Hover/hover1": lambda: ui_hover(1),
    "UI/Hover/hover2": lambda: ui_hover(2),
    "UI/Type/type1": lambda: ui_type(1),
    "UI/Type/type2": lambda: ui_type(2),
    "UI/Type/type3": lambda: ui_type(3),
    "UI/Type/type4": lambda: ui_type(4),
    "UI/confirm": ui_confirm,
    "UI/deny": ui_deny,
    "UI/tab": ui_tab,
    "UI/open": ui_open,
    "UI/close": ui_close,
    "UI/Glitch/glitch1": lambda: ui_glitch(1),
    "UI/Glitch/glitch2": lambda: ui_glitch(2),
    "UI/Glitch/glitch3": lambda: ui_glitch(3),
    "UI/notify": ui_notify,
    "UI/alert": ui_alert,
    "UI/heat_up": ui_heat_up,
    "UI/heat_down": ui_heat_down,
    "UI/scan": ui_scan,
    "UI/download": ui_download,
    "UI/hack": ui_hack,
    "Dive/start": ui_dive_start,
    "Dive/success": ui_dive_success,
    "Dive/fail": ui_dive_fail,
    "Dive/ice": ui_ice,
    "AI/awaken": ai_awaken,
    "AI/whisper": ai_whisper,
    "AI/request": ai_request,
    "AI/fulfilled": ai_fulfilled,
    "AI/angry": ai_angry,
    "AI/feed": ai_feed,
    "AI/chime": ai_chime,
    "AI/echo": ai_echo,
}

LOOPS = {
    "Ambient/terminal": amb_terminal,
    "Ambient/cyberspace": amb_cyberspace,
    "Ambient/dive": amb_dive,
    "Ambient/server": amb_server,
}


def write_wav(path, x):
    x = np.clip(x, -1, 1)
    data = (x * 32767).astype("<i2")
    ch = 1 if data.ndim == 1 else data.shape[1]
    with wave.open(path, "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def to_ogg(wav, ogg, quality=4):
    os.makedirs(os.path.dirname(ogg), exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", str(quality), ogg], check=True)


def main():
    keep = sys.argv[sys.argv.index("--wav") + 1] if "--wav" in sys.argv else None
    only = [a for a in sys.argv[1:] if not a.startswith("--") and a != keep]
    tmp = keep or tempfile.mkdtemp()
    os.makedirs(tmp, exist_ok=True)
    report = []
    for name, fn in {**ONE_SHOTS, **LOOPS}.items():
        if only and not any(o in name for o in only):
            continue
        x = fn()
        if name in ONE_SHOTS:
            x = trim(x)
        wav = os.path.join(tmp, name.replace("/", "_") + ".wav")
        write_wav(wav, x)
        to_ogg(wav, os.path.join(ROOT, name + ".ogg"), 5 if name in ONE_SHOTS else 4)
        mono = x if x.ndim == 1 else x.mean(axis=1)
        report.append((name, len(x) / SR, 20 * np.log10(np.max(np.abs(x)) + 1e-9), 20 * np.log10(np.sqrt(np.mean(mono ** 2)) + 1e-9)))
    for name, d, pk, rm in report:
        print(f"{name:28s} {d:6.2f}s  peak {pk:6.1f} dB  rms {rm:6.1f} dB")


if __name__ == "__main__":
    main()
