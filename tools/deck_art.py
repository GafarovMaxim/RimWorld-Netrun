"""
Shared helpers for the deck theme generators (tools/gen_deck_*.py): materials, masks, bevels, small parts.

Everything is procedural and seeded, so a rerun gives the same files. Images are RGBA PIL images; materials are
float numpy arrays (H, W, 3) in 0..255. Shapes are drawn at SS times the size and shrunk for clean edges.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAME = os.path.join(BASE, "Textures", "UI", "Netrun", "Frame")
FONT_DIR = os.path.join(BASE, "tools", "fonts")
SS = 3


def hexc(h):
    return np.array([(h >> 16) & 255, (h >> 8) & 255, h & 255], dtype=np.float32)


def rng(seed):
    return np.random.default_rng(seed)


def font(size, bold=True):
    for name in ("Tektur-Medium.ttf" if bold else "Tektur-Regular.ttf",):
        path = os.path.join(FONT_DIR, name)
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.truetype("bahnschrift.ttf", size)


# ------------------------------------------------------------------ noise and materials

def value_noise(w, h, cell, seed):
    """Smooth noise in -0.5..0.5 with features about `cell` px wide."""
    gw, gh = max(2, int(w / cell) + 3), max(2, int(h / cell) + 3)
    g = rng(seed).random((gh, gw)).astype(np.float32)
    img = Image.fromarray((g * 255).astype("uint8")).resize((int(gw * cell), int(gh * cell)), Image.BICUBIC)
    a = np.asarray(img, dtype=np.float32)[:h, :w] / 255.0
    return a - 0.5


def fbm(w, h, cell, octaves, seed, falloff=0.5):
    out = np.zeros((h, w), dtype=np.float32)
    amp, total = 1.0, 0.0
    for i in range(octaves):
        out += value_noise(w, h, max(1.0, cell / (2 ** i)), seed + i * 17) * amp
        total += amp
        amp *= falloff
    return out / total


def grain(w, h, seed, amp):
    return (rng(seed).random((h, w)).astype(np.float32) - 0.5) * 2 * amp


def gradient_v(w, h, c0, c1):
    t = np.linspace(0, 1, h, dtype=np.float32)[:, None, None]
    return np.broadcast_to(c0 + (c1 - c0) * t, (h, w, 3)).copy()


def streaks(w, h, axis, amp, seed, blur=1.0):
    """Brushed metal: noise stretched along one axis."""
    r = rng(seed)
    if axis == "x":
        n = r.normal(0, 1, (h, 1)) * np.ones((1, w)) * 0.7 + r.normal(0, 1, (h, w)) * 0.3
    else:
        n = r.normal(0, 1, (1, w)) * np.ones((h, 1)) * 0.7 + r.normal(0, 1, (h, w)) * 0.3
    img = Image.fromarray(np.clip(n * 40 + 128, 0, 255).astype("uint8")).filter(ImageFilter.GaussianBlur(blur))
    return (np.asarray(img, dtype=np.float32) - 128.0) / 128.0 * amp


# ------------------------------------------------------------------ masks

def mask(size, fn, ss=SS, blur=0.0):
    """A float mask (H, W) in 0..1 drawn by fn(draw, s) at ss times the size, then shrunk (antialiased)."""
    w, h = size
    m = Image.new("L", (w * ss, h * ss), 0)
    fn(ImageDraw.Draw(m), ss)
    m = m.resize((w, h), Image.LANCZOS)
    if blur > 0:
        m = m.filter(ImageFilter.GaussianBlur(blur))
    return np.asarray(m, dtype=np.float32) / 255.0


def blur(arr, radius):
    img = Image.fromarray(np.clip(arr * 255, 0, 255).astype("uint8"))
    return np.asarray(img.filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255.0


def shade(alpha, radius=1.5, light=(-1.0, -1.0)):
    """Lighting of a raised shape: + on edges facing the light (top-left), - on the far ones."""
    b = blur(alpha, radius)
    gy, gx = np.gradient(b)
    lx, ly = light
    n = math.hypot(lx, ly)
    return -(gx * lx + gy * ly) / n * radius * 2.2


def inner_edge(alpha, radius):
    """1 at the inside rim of a shape fading to 0 at `radius` px inside."""
    return np.clip((1 - blur(alpha, radius)) * 2.0, 0, 1) * alpha


# ------------------------------------------------------------------ images

def to_img(rgb, alpha):
    a = np.clip(alpha, 0, 1)
    out = np.dstack([np.clip(rgb, 0, 255), a * 255]).astype("uint8")
    return Image.fromarray(out, "RGBA")


def over(dst, src, xy):
    """Alpha-composite src onto dst at xy (in place); src may stick out."""
    dst.alpha_composite(src, dest=(int(xy[0]), int(xy[1]))) if xy[0] >= 0 and xy[1] >= 0 else _over_clip(dst, src, xy)
    return dst


def _over_clip(dst, src, xy):
    x, y = int(xy[0]), int(xy[1])
    crop = src.crop((max(0, -x), max(0, -y), src.width, src.height))
    dst.alpha_composite(crop, dest=(max(0, x), max(0, y)))


def save(img, theme, name):
    folder = os.path.join(FRAME, theme)
    os.makedirs(folder, exist_ok=True)
    img.save(os.path.join(folder, name + ".png"), optimize=True)


def rivet(d, base, seed=0, dome=True):
    """A round rivet or screw head, d px wide, lit from the top-left, with a soft drop shadow."""
    pad = 2
    size = d + pad * 2
    a = mask((size, size), lambda dr, s: dr.ellipse([pad * s, pad * s, (pad + d) * s - 1, (pad + d) * s - 1], fill=255))
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    cx = cy = size / 2
    r = np.hypot(xx - cx, yy - cy) / (d / 2)
    rgb = np.broadcast_to(base, (size, size, 3)).copy()
    if dome:
        hl = np.clip(1 - np.hypot(xx - cx + d * 0.18, yy - cy + d * 0.2) / (d * 0.42), 0, 1) ** 1.6
        rgb += (hl * 90)[..., None]
        rgb *= (1 - 0.45 * np.clip(r - 0.55, 0, 1) * 2)[..., None]
    rgb += grain(size, size, seed, 6)[..., None]
    shadow = np.roll(np.roll(blur(a, 1.2), 1, 0), 1, 1) * 0.6
    img = to_img(np.zeros_like(rgb), shadow)
    img.alpha_composite(to_img(rgb, a))
    return img


def text_layer(size, text, fnt, fill, xy=(0, 0), anchor="la"):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(img).text(xy, text, font=fnt, fill=fill, anchor=anchor)
    return img


def spray(img, seed, keep=0.82):
    """Spray-paint look: knock random speckles out of a layer's alpha."""
    a = np.asarray(img, dtype=np.float32).copy()
    n = rng(seed).random(a.shape[:2])
    a[..., 3] *= (n < keep) * (0.75 + 0.25 * n)
    return Image.fromarray(np.clip(a, 0, 255).astype("uint8"), "RGBA")


def preview_box(w, h, color=(9, 10, 12)):
    return Image.new("RGBA", (w, h), color + (255,))


def nine_slice(src, size, l, t, r, b):
    """Stretch a 9-slice texture to `size` (as the game draws it), for previews."""
    W, H = size
    sw, sh = src.size
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    xs = [(0, l, 0, l), (l, sw - r, l, W - r), (sw - r, sw, W - r, W)]
    ys = [(0, t, 0, t), (t, sh - b, t, H - b), (sh - b, sh, H - b, H)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            if dx1 <= dx0 or dy1 <= dy0 or sx1 <= sx0 or sy1 <= sy0:
                continue
            part = src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.BICUBIC)
            out.alpha_composite(part, dest=(dx0, dy0))
    return out
