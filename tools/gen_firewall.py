"""Wall-mounted firewall (1x1, 256 px, Graphic_Multi) in the style of the AI server rack: olive-grey body, black
outline, one glowing accent (a cyan shield). Hangs on a wall like a wall lamp: the wall is behind the facing.
_south: on a wall to the north, front panel visible; _east: on a wall to the west, side view; _north: on a wall to the
south, only the top edge shows. Run: python tools/gen_firewall.py"""
import os
from PIL import Image, ImageDraw, ImageFilter

S = 4
W = 256 * S
BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Textures", "Things", "Building", "Misc", "NR_Firewall")

INK = (12, 14, 14, 255)
BODY = (78, 82, 70, 255)
BODY_HI = (104, 108, 94, 255)
BODY_LO = (58, 61, 52, 255)
PANEL = (34, 38, 38, 255)
CYAN = (70, 220, 230, 255)
AMBER = (240, 180, 60, 255)


def r(v):
    return int(v * S)


def glow_poly(img, pts, blur):
    g = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(g).polygon(pts, fill=(70, 220, 230, 150))
    return Image.alpha_composite(img, g.filter(ImageFilter.GaussianBlur(r(blur))))


def south():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # box hanging from the top edge (the wall is to the north)
    d.rounded_rectangle([r(52), r(0), r(204), r(150)], radius=r(9), fill=INK)
    d.rounded_rectangle([r(58), r(0), r(198), r(144)], radius=r(7), fill=BODY)
    d.rectangle([r(58), r(128), r(198), r(144)], fill=BODY_LO)
    for i in range(3):
        y = r(12 + i * 7)
        d.rounded_rectangle([r(72), y, r(184), y + r(3)], radius=r(1), fill=BODY_LO)
    d.rounded_rectangle([r(80), r(40), r(176), r(122)], radius=r(6), fill=INK)
    d.rounded_rectangle([r(85), r(45), r(171), r(117)], radius=r(5), fill=PANEL)
    shield = [(r(128), r(52)), (r(154), r(60)), (r(151), r(88)), (r(128), r(110)), (r(105), r(88)), (r(102), r(60))]
    img = glow_poly(img, shield, 6)
    d = ImageDraw.Draw(img)
    d.polygon(shield, outline=CYAN, width=r(4))
    d.line([(r(128), r(62)), (r(128), r(100))], fill=CYAN, width=r(3))
    d.line([(r(113), r(76)), (r(143), r(76))], fill=CYAN, width=r(3))
    for i, c in enumerate([CYAN, CYAN, AMBER]):
        x = r(160 + i * 10)
        d.ellipse([x, r(30), x + r(5), r(35)], fill=c)
    return img


def east():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # side view, wall to the west: a slim box against the left edge, the shield's glow on its face
    d.rounded_rectangle([r(0), r(46), r(70), r(210)], radius=r(8), fill=INK)
    d.rounded_rectangle([r(0), r(52), r(64), r(204)], radius=r(6), fill=BODY)
    d.rectangle([r(0), r(52), r(64), r(66)], fill=BODY_HI)
    d.rectangle([r(52), r(80), r(64), r(180)], fill=PANEL)
    face = [(r(60), r(96)), (r(70), r(100)), (r(70), r(160)), (r(60), r(166))]
    img = glow_poly(img, face, 5)
    d = ImageDraw.Draw(img)
    d.line([(r(64), r(100)), (r(64), r(160))], fill=CYAN, width=r(4))
    d.ellipse([r(40), r(70), r(46), r(76)], fill=AMBER)
    return img


def north():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # wall to the south: only the top of the box shows above it
    d.rounded_rectangle([r(52), r(196), r(204), r(256)], radius=r(8), fill=INK)
    d.rounded_rectangle([r(58), r(202), r(198), r(256)], radius=r(6), fill=BODY_HI)
    for i in range(4):
        x = r(78 + i * 28)
        d.rounded_rectangle([x, r(214), x + r(16), r(220)], radius=r(1), fill=BODY_LO)
    d.ellipse([r(176), r(230), r(182), r(236)], fill=CYAN)
    return img


for name, fn in (("south", south), ("east", east), ("north", north)):
    fn().resize((256, 256), Image.LANCZOS).save(f"{BASE}_{name}.png")
print("ok")
