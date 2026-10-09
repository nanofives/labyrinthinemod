import UnityPy, os, traceback
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
D = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine\Labyrinthine_Data"
gen = TypeTreeGenerator("2022.3.62f3")
gen.load_local_dll_folder(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "il2cpp-dump", "DummyDll"))
env = UnityPy.load(os.path.join(D, "sharedassets17.assets"))
# 1) find spawners WITHOUT generator (as in find_spawners), remember objects
spawners = []
for obj in env.objects:
    if obj.type.name != "MonoBehaviour": continue
    try:
        mb = obj.read(check_read=False); sc = mb.m_Script.read()
    except Exception: continue
    if sc.m_ClassName == "RndCustomizationSpawner": spawners.append((obj, sc))
print("spawners", len(spawners))
env.typetree_generator = gen
for obj, sc in spawners[:2]:
    try:
        nodes = gen.get_nodes_up(sc.m_AssemblyName, f"{sc.m_Namespace}.{sc.m_ClassName}" if sc.m_Namespace else sc.m_ClassName)
        tt = obj.read_typetree(nodes)
        print({k: (f"[{len(v)}]" if isinstance(v, list) and k=="spawnPoints" else v) for k, v in tt.items()})
    except Exception as e:
        traceback.print_exc()
