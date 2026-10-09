"""Check that generated type trees let us read RndCustomizationSpawner fields."""
import UnityPy, os
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
GAME = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine"
D = os.path.join(GAME, "Labyrinthine_Data")
gen = TypeTreeGenerator("2022.3.62f3")
gen.load_local_dll_folder(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "il2cpp-dump", "DummyDll"))
env = UnityPy.load(os.path.join(D, "sharedassets17.assets"))
env.typetree_generator = gen
n = 0
for obj in env.objects:
    if obj.type.name != "MonoBehaviour": continue
    try:
        mb = obj.read(check_read=False)
        sc = mb.m_Script.read() if mb.m_Script else None
    except Exception:
        continue
    if sc is None or sc.m_ClassName != "RndCustomizationSpawner": continue
    tt = obj.read_typetree()
    print({k: (v if k not in ("spawnPoints",) else f"[{len(v)}]") for k, v in tt.items()})
    n += 1
    if n >= 2: break
