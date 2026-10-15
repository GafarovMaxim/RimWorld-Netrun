"""Repacks the deck frame textures in place without visible change (deck_art.clean_alpha): clears the colour under
fully transparent pixels and re-saves with PNG optimisation. New textures get this in deck_art.save already.
Usage: python tools/pack_frames.py [folder ...]  (default: every theme folder under Textures/UI/Netrun/Frame)."""
import os
import sys
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
import deck_art as A

folders = sys.argv[1:] or [os.path.join(A.FRAME, d) for d in sorted(os.listdir(A.FRAME)) if os.path.isdir(os.path.join(A.FRAME, d))]
before = after = 0
for folder in folders:
    for f in sorted(os.listdir(folder)):
        if not f.endswith(".png"):
            continue
        p = os.path.join(folder, f)
        s0 = os.path.getsize(p)
        img = Image.open(p)
        img.load()
        tmp = p + ".tmp"
        A.clean_alpha(img).save(tmp, "PNG", optimize=True)
        if os.path.getsize(tmp) < s0:
            os.replace(tmp, p)
        else:
            os.remove(tmp)
        before += s0
        after += os.path.getsize(p)
print(f"{before // 1024} KB -> {after // 1024} KB")
