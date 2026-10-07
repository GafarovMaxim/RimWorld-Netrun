"""Shared drawing helpers for Netrun concept art (flat neon icons, thick dark outline, octagon rims).

Everything is drawn at SS x the final size and downscaled, so edges stay smooth.
Used by tools/gen_concepts10_art.py. Needs Pillow only.
"""
import math
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SS = 4  # supersampling factor

# Palette taken from the mod's existing icons
INK = (6, 8, 12, 255)          # outline
DARK = (14, 19, 27, 255)       # inner fill
DARK2 = (24, 32, 44, 255)      # inner fill, lighter
CYAN = (51, 214, 240, 255)
GOLD = (245, 184, 58, 255)
PURPLE = (180, 92, 255, 255)
GREEN = (60, 230, 120, 255)
RED = (240, 52, 72, 255)
MAGENTA = (255, 61, 139, 255)
WHITE = (236, 242, 248, 255)
STEEL = (62, 70, 84, 255)
STEEL_HI = (104, 116, 134, 255)

FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FONT_BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
FONT_MONO = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf"


def font(size, bold=False, mono=False):
    return ImageFont.truetype(FONT_MONO if mono else FONT_BOLD if bold else FONT, size)


def canvas(size):
    """Transparent supersampled canvas for an icon of `size` px."""
    return Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))


def finish(img, size):
    return img.resize((size, size), Image.LANCZOS)


def with_alpha(color, a):
    return (color[0], color[1], color[2], a)


def mix(c1, c2, t):
    return tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(4))


def regular_poly(cx, cy, r, n, rot=0.0):
    return [(cx + r * math.cos(rot + 2 * math.pi * i / n), cy + r * math.sin(rot + 2 * math.pi * i / n)) for i in range(n)]


def octagon(cx, cy, r):
    """Flat-sided octagon (sides axis-aligned), like the node and daemon rims."""
    return regular_poly(cx, cy, r / math.cos(math.pi / 8), 8, math.pi / 8)


def chamfer_rect(x0, y0, x1, y1, c):
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


def poly(img, pts, fill):
    ImageDraw.Draw(img).polygon(pts, fill=fill)


def glow(img, shape_layer, radius, strength=1.0):
    """Puts a blurred copy of shape_layer under img (outer neon glow)."""
    g = shape_layer.filter(ImageFilter.GaussianBlur(radius))
    if strength != 1.0:
        a = g.getchannel("A").point(lambda v: min(255, int(v * strength)))
        g.putalpha(a)
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.alpha_composite(g)
    out.alpha_composite(img)
    return out


def radial_fill(size, center_color, edge_color, mask):
    """Radial gradient clipped by mask (L image)."""
    w, h = size
    grad = Image.new("RGBA", size)
    cx, cy = w / 2, h / 2
    maxd = math.hypot(cx, cy)
    # coarse gradient, then upscale: fast enough and smooth after blur
    small = Image.new("RGBA", (64, 64))
    px = small.load()
    for y in range(64):
        for x in range(64):
            d = math.hypot(x - 31.5, y - 31.5) / 45.0
            px[x, y] = mix(center_color, edge_color, min(1.0, d))
    grad = small.resize(size, Image.BICUBIC)
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    out.paste(grad, (0, 0), mask)
    return out


def mask_of(size, pts):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def ellipse_mask(size, box):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).ellipse(box, fill=255)
    return m


def scanlines(img, mask, step, color):
    """Faint horizontal CRT lines inside mask."""
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for y in range(0, img.size[1], step):
        d.line([(0, y), (img.size[0], y)], fill=color, width=max(1, step // 3))
    clipped = Image.new("RGBA", img.size, (0, 0, 0, 0))
    clipped.paste(layer, (0, 0), mask)
    img.alpha_composite(clipped)


def grid(img, mask, step, color):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for x in range(0, img.size[0], step):
        d.line([(x, 0), (x, img.size[1])], fill=color, width=max(1, step // 14))
    for y in range(0, img.size[1], step):
        d.line([(0, y), (img.size[0], y)], fill=color, width=max(1, step // 14))
    clipped = Image.new("RGBA", img.size, (0, 0, 0, 0))
    clipped.paste(layer, (0, 0), mask)
    img.alpha_composite(clipped)


def thick_line(d, pts, color, width):
    d.line(pts, fill=color, width=int(width), joint="curve")
    r = width / 2
    for x, y in (pts[0], pts[-1]):
        d.ellipse((x - r, y - r, x + r, y + r), fill=color)


def outlined_line(img, pts, color, width, ink_extra):
    d = ImageDraw.Draw(img)
    thick_line(d, pts, INK, width + ink_extra)
    thick_line(d, pts, color, width)


def outlined_poly(img, pts, color, ink_width):
    """Polygon with a dark outline drawn around it (outline grows outward)."""
    d = ImageDraw.Draw(img)
    d.polygon(pts, fill=INK)
    d.line(pts + [pts[0]], fill=INK, width=int(ink_width * 2), joint="curve")
    d.polygon(pts, fill=color)


def outlined_ellipse(img, box, color, ink_width):
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = box
    d.ellipse((x0 - ink_width, y0 - ink_width, x1 + ink_width, y1 + ink_width), fill=INK)
    d.ellipse(box, fill=color)


def silhouette(icon):
    """What VAE shows for a locked achievement: GUI.color black over the icon."""
    out = Image.new("RGBA", icon.size, (0, 0, 0, 0))
    black = Image.new("RGBA", icon.size, (0, 0, 0, 255))
    out.paste(black, (0, 0), icon.getchannel("A"))
    return out


def text_center(img, xy, text, size, color, bold=True, mono=False):
    d = ImageDraw.Draw(img)
    f = font(size, bold, mono)
    w = d.textlength(text, font=f)
    d.text((xy[0] - w / 2, xy[1]), text, font=f, fill=color)
