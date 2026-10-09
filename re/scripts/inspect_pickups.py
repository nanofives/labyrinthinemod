"""How many CustomizationPickup components load their model at runtime (loadObjectModel) vs carry a baked model.
usage: python inspect_pickups.py"""
import collections
import os

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

DATA = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine\Labyrinthine_Data"
HERE = os.path.dirname(os.path.abspath(__file__))
gen = TypeTreeGenerator("2022.3.62f3")
gen.load_local_dll_folder(os.path.join(HERE, "..", "il2cpp-dump", "DummyDll"))
nodes = None

env = UnityPy.load(DATA)
stats = collections.Counter()
samples = collections.defaultdict(list)
for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    try:
        mb = obj.read(check_read=False)
        sc = mb.m_Script.read() if mb.m_Script else None
    except Exception:
        continue
    if sc is None or sc.m_ClassName != "CustomizationPickup":
        continue
    if nodes is None:
        nodes = gen.get_nodes_up(sc.m_AssemblyName, f"{sc.m_Namespace}.{sc.m_ClassName}")
    t = obj.read_typetree(nodes)
    key = (bool(t.get("loadObjectModel")), bool(t.get("unlockOnContractCompleted")))
    stats[key] += 1
    if len(samples[key]) < 5:
        samples[key].append((os.path.basename(obj.assets_file.name), t.get("itemID")))
for k, v in stats.items():
    print(f"loadObjectModel={k[0]} unlockOnContractCompleted={k[1]}: {v}  e.g. {samples[k]}")
