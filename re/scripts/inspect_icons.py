"""Which CustomizationItems in ItemsCollectionSO have an icon sprite, per body part; how the sprites are packed.
usage: python inspect_icons.py"""
import collections
import os

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

DATA = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine\Labyrinthine_Data"
HERE = os.path.dirname(os.path.abspath(__file__))
PARTS = {0: "Head", 1: "Clothing", 2: "Wrist", 3: "Flashlight", 4: "Lantern", 5: "Glowstick", 6: "Face", 7: "Finger", 100: "Music"}

gen = TypeTreeGenerator("2022.3.62f3")
gen.load_local_dll_folder(os.path.join(HERE, "..", "il2cpp-dump", "DummyDll"))
env = UnityPy.load(DATA)

for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    try:
        mb = obj.read(check_read=False)
        sc = mb.m_Script.read() if mb.m_Script else None
    except Exception:
        continue
    if sc is None or sc.m_ClassName != "ItemsCollectionSO":
        continue
    t = obj.read_typetree(gen.get_nodes_up(sc.m_AssemblyName, f"{sc.m_Namespace}.{sc.m_ClassName}"))
    items = t["collection"]
    stats = collections.Counter()
    packing = collections.Counter()
    samples = collections.defaultdict(list)
    for it in items:
        part = PARTS.get(it["bodyPart"], str(it["bodyPart"]))
        has = it["icon"]["m_PathID"] != 0
        has_asset = bool(it.get("assetReference", {}).get("m_AssetGUID"))
        stats[(part, has, has_asset)] += 1
        if has and len(samples[part]) < 1:
            try:
                spr = obj.assets_file.environment  # noqa
            except Exception:
                pass
    print(f"{len(items)} items in {os.path.basename(obj.assets_file.name)}")
    for (part, has, asset), n in sorted(stats.items()):
        print(f"  {part:10} icon={'yes' if has else 'NO '} model(AssetReference)={'yes' if asset else 'no'}: {n}")
    break

# --- how icon sprites are stored (late-binding atlas?)
checked = collections.Counter()
for it in items[:400]:
    p = it["icon"]
    if p["m_PathID"] == 0:
        continue
    try:
        fid = p["m_FileID"]
        af = obj.assets_file
        target = af if fid == 0 else next((f for k, f in env.files.items()
                                            if os.path.basename(k).lower() == os.path.basename(af.externals[fid - 1].path).lower()), None)
        so = target.objects[p["m_PathID"]]
        st = so.read_typetree()
        tex = st["m_RD"]["texture"]["m_PathID"]
        atlas = st.get("m_SpriteAtlas", {}).get("m_PathID", 0)
        checked[(os.path.basename(target.name), "texture" if tex else "NO texture", "atlas" if atlas else "no atlas")] += 1
    except Exception as e:
        checked[("error", type(e).__name__, "")] += 1
for k, v in checked.most_common():
    print("  sprite:", k, v)
