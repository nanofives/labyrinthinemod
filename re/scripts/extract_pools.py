"""Build LabyHelper.pools.json offline from the game's asset files, so the case board shows odds for maps you
haven't played yet.

Chain: MazeTypesConfigSO.mazeTypesData[MazeType].sceneName -> BuildSettings scene index N -> levelN + sharedassetsN
-> every RndCustomizationSpawner there -> (globalPrefabs SO + possiblePrefabs) -> prefab GameObject ->
CustomizationPickup.itemID. Seasonal pools and chanceForItem come from RndCustomizationItemsManager.

The build has no type trees, so they are generated from Il2CppDumper's DummyDll (TypeTreeGeneratorAPI).

usage: python extract_pools.py [out.json]
"""
import json
import os
import sys

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

GAME = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine"
DATA = os.path.join(GAME, "Labyrinthine_Data")
HERE = os.path.dirname(os.path.abspath(__file__))
DUMMY = os.path.join(HERE, "..", "il2cpp-dump", "DummyDll")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "src", "LabyHelper", "pools.default.json")

MAZE_TYPES = ["Maze_A", "Maze_B", "Maze_C", "Maze_D", "Maze_E", "Maze_F", "Maze_G", "Maze_H", "Maze_I", "Maze_J",
              "Maze_K", "Maze_L", "Maze_M", "Maze_N", "Maze_O", "Maze_P", "Maze_Backrooms", "Maze_Q", "Maze_R"]
EVENTS = ["Halloween", "Christmas", "Easter", "Valentines", "StPatrick", "Summer"]
CONTRACT_TYPES = ["Normal", "Rare", "Hardcore"]

gen = TypeTreeGenerator("2022.3.62f3")
gen.load_local_dll_folder(DUMMY)
_nodes = {}


def script_of(obj):
    try:
        mb = obj.read(check_read=False)
        return mb.m_Script.read() if mb.m_Script else None
    except Exception:
        return None


def tree(obj, script):
    key = (script.m_AssemblyName, script.m_Namespace, script.m_ClassName)
    if key not in _nodes:
        full = f"{script.m_Namespace}.{script.m_ClassName}" if script.m_Namespace else script.m_ClassName
        _nodes[key] = gen.get_nodes_up(script.m_AssemblyName, full)
    return obj.read_typetree(_nodes[key])


def deref(assets_file, pptr):
    """PPtr dict -> ObjectReader (or None)."""
    if not pptr or pptr.get("m_PathID", 0) == 0:
        return None
    fid = pptr["m_FileID"]
    try:
        target = assets_file if fid == 0 else assets_file.environment.get_cab(assets_file.externals[fid - 1].path.split("/")[-1].lower()) \
            or assets_file.environment.files.get(assets_file.externals[fid - 1].path.split("/")[-1])
        if target is None:
            name = os.path.basename(assets_file.externals[fid - 1].path).lower()
            target = next((f for k, f in assets_file.environment.files.items() if os.path.basename(k).lower() == name), None)
        return target.objects.get(pptr["m_PathID"]) if target is not None else None
    except Exception:
        return None


pickup_cache = {}


def pickup_id(assets_file, go_pptr):
    """(itemID, onRoot) for a prefab GameObject, searching children if the root has no CustomizationPickup."""
    go = deref(assets_file, go_pptr)
    if go is None:
        return None, False
    key = (go.assets_file.name, go.path_id)
    if key in pickup_cache:
        return pickup_cache[key]
    res = find_pickup(go, depth=0)
    pickup_cache[key] = res
    return res


def find_pickup(go, depth):
    try:
        g = go.read_typetree()
    except Exception:
        return None, False
    transform = None
    for comp in g.get("m_Component", []):
        c = deref(go.assets_file, comp["component"])
        if c is None:
            continue
        if c.type.name == "MonoBehaviour":
            sc = script_of(c)
            if sc is not None and sc.m_ClassName == "CustomizationPickup":
                return tree(c, sc)["itemID"], depth == 0
        elif c.type.name in ("Transform", "RectTransform"):
            transform = c
    if transform is None or depth > 4:
        return None, False
    try:
        for child in transform.read_typetree().get("m_Children", []):
            ct = deref(transform.assets_file, child)
            if ct is None:
                continue
            cgo = deref(ct.assets_file, ct.read_typetree()["m_GameObject"])
            if cgo is not None:
                cid, _ = find_pickup(cgo, depth + 1)
                if cid is not None:
                    return cid, False
    except Exception:
        pass
    return None, False


def entries(assets_file, weighted, from_map):
    out = []
    for wp in weighted or []:
        iid, root = pickup_id(assets_file, wp.get("Value"))
        out.append({"Id": iid if iid is not None else 0xFFFF, "Weight": float(wp.get("weight", 0)),
                    "OnRoot": bool(root), "FromMap": from_map})
    return out


def so_entries(assets_file, pptr):
    """Entries of a WeightedPrefabSetSO; its prefab pointers are relative to the SO's own file."""
    so = deref(assets_file, pptr)
    if so is None:
        return []
    sc = script_of(so)
    if sc is None:
        return []
    return entries(so.assets_file, tree(so, sc).get("value", []), False)


print("Loading game data (lazy)...", flush=True)
env = UnityPy.load(DATA)

# Scene list from BuildSettings
scenes = []
for obj in env.objects:
    if obj.type.name == "BuildSettings":
        scenes = [os.path.splitext(os.path.basename(s))[0] for s in obj.read_typetree()["scenes"]]
        break
print(f"{len(scenes)} scenes", flush=True)

maze_scene = {}
seasonal = {}
chance_for_item = {}
spawners_by_file = {}

for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    sc = script_of(obj)
    if sc is None:
        continue
    name = sc.m_ClassName
    if name == "MazeTypesConfigSO" and not maze_scene:
        data = tree(obj, sc).get("mazeTypesData", [])
        for i, d in enumerate(data):
            if i < len(MAZE_TYPES) and d.get("sceneName"):
                maze_scene[MAZE_TYPES[i]] = d["sceneName"]
    elif name == "RndCustomizationItemsManager" and not seasonal:
        t = tree(obj, sc)
        for i, c in enumerate(t.get("chanceForItem", [])):
            if i < len(CONTRACT_TYPES):
                chance_for_item[CONTRACT_TYPES[i]] = c
        for cfg in t.get("seasonalItems", []):
            evt = EVENTS[cfg["seasonalEventType"]] if cfg["seasonalEventType"] < len(EVENTS) else str(cfg["seasonalEventType"])
            items = entries(obj.assets_file, cfg.get("prefabs"), False)
            if items:
                seasonal[evt] = {"Chance": cfg.get("chance", 0), "Items": items}
    elif name == "RndCustomizationSpawner":
        spawners_by_file.setdefault(os.path.basename(obj.assets_file.name), []).append((obj, sc))

print(f"maze->scene: {maze_scene}", flush=True)
print(f"spawner files: {len(spawners_by_file)}; seasonal events: {list(seasonal)}", flush=True)

maps = {}
for maze, scene in maze_scene.items():
    lower = [s.lower() for s in scenes]
    if scene.lower() not in lower:
        print(f"  {maze}: scene {scene} not in build", flush=True)
        continue
    n = lower.index(scene.lower())
    normal, hardcore = [], []
    for fname in (f"level{n}", f"sharedassets{n}.assets"):
        for obj, sc in spawners_by_file.get(fname, []):
            t = tree(obj, sc)
            af = obj.assets_file
            nl = so_entries(af, t.get("globalPrefabs")) + entries(af, t.get("possiblePrefabs"), True)
            hl = so_entries(af, t.get("globalPrefabsHardcore")) + entries(af, t.get("possiblePrefabsHardcore"), True)
            if nl and nl not in normal:
                normal.append(nl)
            if hl and hl not in hardcore:
                hardcore.append(hl)
    if normal or hardcore:
        maps[maze] = {"Normal": normal, "Hardcore": hardcore}
    print(f"  {maze} ({scene}, level{n}): {len(normal)} normal list(s) {[len(l) for l in normal]}, "
          f"{len(hardcore)} hardcore", flush=True)

out = {"Version": 2, "Maps": maps, "Seasonal": seasonal, "ChanceForItem": chance_for_item}
with open(OUT, "w", encoding="utf-8") as f:
    json.dump(out, f, indent=1)
print(f"wrote {OUT}: {len(maps)} maps", flush=True)
