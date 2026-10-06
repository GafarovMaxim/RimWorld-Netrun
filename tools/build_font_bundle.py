"""Builds AssetBundles/netrun_fonts_<platform> (run: python tools/build_font_bundle.py win|mac|linux; needs UnityPy and Pillow,
Fonts/Tektur-Reg.ttf next to the mod, and any tiny Unity 2022.3.35f1 bundle as SRC; only its container is reused).
Originally: a Unity 2022.3.35f1 bundle holding the Tektur dynamic font.
Starts from a tiny shader bundle of the same Unity version, turns its one object into a Font with the TTF bytes."""
import copy, os, sys, hashlib
import UnityPy
from UnityPy.helpers import Tpk
from UnityPy.helpers.UnityVersion import UnityVersion
from UnityPy.enums import ClassIDType
from PIL import ImageFont

PLATFORM = sys.argv[1] if len(sys.argv) > 1 else "win"
SRC = r"E:\SteamLibrary\steamapps\workshop\content\294100\3781872865\AssetBundles\firmamentfx_" + PLATFORM
HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = r"E:\SteamLibrary\steamapps\common\RimWorld\Mods\HackingExpansionNetrun\AssetBundles"
FONTS = [("Tektur", os.path.join(HERE, "..", "Fonts", "Tektur-Reg.ttf"))]

env = UnityPy.load(SRC)
sf = list(env.objects)[0].assets_file
version = UnityVersion.from_str(sf.unity_version)
node = Tpk.get_typetree_node(int(ClassIDType.Font), version)
SIZES = {"int": 4, "unsigned int": 4, "SInt32": 4, "UInt32": 4, "float": 4, "SInt64": 8, "UInt64": 8, "bool": 1, "char": 1,
         "UInt8": 1, "SInt8": 1, "SInt16": 2, "UInt16": 2, "double": 8}
for n in node.traverse():
    n.m_RefTypeHash = n.m_RefTypeHash or 0
    n.m_TypeFlags = 1 if n.m_Type == "Array" else 0
    n.m_VariableCount = n.m_VariableCount or 0
    if n.m_Type in SIZES:
        n.m_ByteSize = SIZES[n.m_Type]

shader = next(o for o in sf.objects.values() if o.class_id == 48)
bundle_obj = next(o for o in sf.objects.values() if o.class_id == 142)

# A Font type entry in the file's type table.
ftype = copy.copy(sf.types[0])
ftype.class_id = int(ClassIDType.Font)
ftype.node = node
ftype.script_type_index = -1
ftype.type_dependencies = []
sf.types.append(ftype)

name, path = FONTS[0]
data = open(path, "rb").read()
size = 16
pil = ImageFont.truetype(path, size)
ascent, descent = pil.getmetrics()

shader.serialized_type = ftype
shader.class_id = int(ClassIDType.Font)
shader.type_id = len(sf.types) - 1
shader.type = ClassIDType.Font
font = {
    "m_Name": name,
    "m_LineSpacing": float(ascent + descent),
    "m_DefaultMaterial": {"m_FileID": 0, "m_PathID": 0},
    "m_FontSize": float(size),
    "m_Texture": {"m_FileID": 0, "m_PathID": 0},
    "m_AsciiStartOffset": 0,
    "m_Tracking": 1.0,
    "m_CharacterSpacing": 0,
    "m_CharacterPadding": 1,
    "m_ConvertCase": -2,
    "m_CharacterRects": [],
    "m_KerningValues": [],
    "m_PixelScale": 0.1,
    "m_FontData": list(data),
    "m_Ascent": float(ascent),
    "m_Descent": float(-descent),
    "m_DefaultStyle": 0,
    "m_FontNames": [name],
    "m_FallbackFonts": [],
    "m_FontRenderingMode": 0,
    "m_UseLegacyBoundsCalculation": False,
    "m_ShouldRoundAdvanceValue": True,
}
shader.save_typetree(font, nodes=node)

ab = bundle_obj.read_typetree()
asset = {"m_FileID": 0, "m_PathID": shader.path_id}
ab["m_Name"] = "netrun_fonts_" + PLATFORM
ab["m_AssetBundleName"] = "netrun_fonts_" + PLATFORM
ab["m_PreloadTable"] = [asset]
ab["m_Container"] = [("assets/netrun/fonts/tektur.ttf", {"preloadIndex": 0, "preloadSize": 1, "asset": asset})]
bundle_obj.save_typetree(ab)

# A unique internal file name, so it never clashes with the bundle it was made from.
cab = "CAB-" + hashlib.md5(("netrun_fonts_tektur_" + PLATFORM).encode()).hexdigest()
old = sf.name
files = env.file.files
files[cab] = files.pop(old)
sf.name = cab
try:
    env.file._needs_rebuild = True
except Exception:
    pass

os.makedirs(OUT_DIR, exist_ok=True)
out = os.path.join(OUT_DIR, "netrun_fonts_" + PLATFORM)
with open(out, "wb") as f:
    f.write(env.file.save(packer="lz4"))
print("written", out, os.path.getsize(out))

# Read it back.
env2 = UnityPy.load(out)
for o in env2.objects:
    print(o.type.name, o.path_id, o.peek_name() if hasattr(o, "peek_name") else "")
    if o.type.name == "Font":
        d = o.read_typetree()
        print("font", d["m_Name"], len(d["m_FontData"]), d["m_LineSpacing"], d["m_Ascent"], d["m_Descent"])
    if o.type.name == "AssetBundle":
        print(o.read_typetree()["m_Container"])
print("files:", list(env2.file.files.keys()))
