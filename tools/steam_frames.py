"""The Steam description gallery of all deck frames (docs/steam/img/en_frames.png, ru_frames.png)."""
import os
from PIL import Image, ImageDraw
import deck_art as A

ROOT = os.path.join(os.path.dirname(__file__), "..")
FRAMES = [("Cyber", "NR-1 CYBERDECK", "КИБЕРДЕКА NR-1"), ("Iron", "IRON COMPANY", "IRON COMPANY"), ("Street", "STREET", "СТРИТ"),
          ("Kurenai", "KURENAI SYSTEMS", "KURENAI SYSTEMS"), ("Nomad", "NOMAD", "КОЧЕВНИК"), ("Retro", "NR-80 RETRO", "NR-80 РЕТРО"),
          ("Relic", "RELIC", "РЕЛИКТ")]
for lang, idx in (("en", 1), ("ru", 2)):
    W, cw, ch, gap = 1270, 300, 240, 14
    img = Image.new("RGBA", (W, 2 * (ch + 40) + 2 * gap), (12, 14, 16, 255))
    d = ImageDraw.Draw(img)
    for i, f in enumerate(FRAMES):
        row, col = divmod(i, 4)
        n = 4 if row == 0 else 3
        x = gap + col * (cw + gap) + (0 if row == 0 else (cw + gap) // 2)
        y = gap + row * (ch + 40 + gap)
        p = Image.open(os.path.join(ROOT, "Textures/UI/Netrun/Frame", f[0], "Preview.png")).convert("RGBA").resize((cw, ch), Image.LANCZOS)
        img.alpha_composite(p, (x, y))
        d.text((x + cw / 2, y + ch + 20), f[idx], font=A.font(16), fill=(90, 230, 130, 255), anchor="mm")
    img.convert("RGB").save(os.path.join(ROOT, "docs/steam/img", lang + "_frames.png"))
print("ok")
