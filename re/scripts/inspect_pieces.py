"""Inspect MazeGenerator piece sets per map, offline, for the "foreign pieces" experiment.

For each map: tileSize, rareGenerationChance, wall/start/end arrays, every Submaze (index 0 = the main maze, see
GetFloorPiece: possibleSubmazes[mazeTypeMap[x,y]]) with its piece arrays, transitionPieces, materialsToCombine.
For each piece prefab (with --deep): renderer count, lightmapIndex values, light probe usage, Light components,
colliders, MonoBehaviour classes, NetworkIdentity, mesh AABB extents (footprint vs tileSize).
Also scene-level: LightmapSettings (lightmap count, mode) and the MonoBehaviour classes present in levelN (what an
additive load of that scene would wake up).

usage: python inspect_pieces.py [--deep] [--scene-scripts] [Maze_A Maze_N ...]   (no maps = all, summary only)
Writes JSON next to stdout output: re/scripts/out/pieces_<map>.json
"""
import json
import os
import sys
from collections import Counter

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

GAME = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine"
DATA = os.path.join(GAME, "Labyrinthine_Data")
HERE = os.path.dirname(os.path.abspath(__file__))
DUMMY = os.path.join(HERE, "..", "il2cpp-dump", "DummyDll")
OUTDIR = os.path.join(HERE, "out")

MAZE_TYPES = ["Maze_A", "Maze_B", "Maze_C", "Maze_D", "Maze_E", "Maze_F", "Maze_G", "Maze_H", "Maze_I", "Maze_J",
              "Maze_K", "Maze_L", "Maze_M", "Maze_N", "Maze_O", "Maze_P", "Maze_Backrooms", "Maze_Q", "Maze_R"]
SUB_ARRAYS = ["straightPieces", "straightPiecesRare", "endCaps", "endCapsRare", "threewayPieces",
              "threewayPiecesRare", "turnPieces", "turnPiecesRare", "floorPieces", "floorPiecesRare",
              "safezonePieces", "safezonePiecesRare"]

args = [a for a in sys.argv[1:] if not a.startswith("--")]
DEEP = "--deep" in sys.argv
SCENE_SCRIPTS = "--scene-scripts" in sys.argv

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


def deref(af, pptr):
    if not pptr or pptr.get("m_PathID", 0) == 0:
        return None
    fid = pptr["m_FileID"]
    try:
        if fid == 0:
            target = af
        else:
            name = os.path.basename(af.externals[fid - 1].path).lower()
            target = next((f for k, f in af.environment.files.items() if os.path.basename(k).lower() == name), None)
            if target is None:
                target = af.environment.get_cab(name)
        return target.objects.get(pptr["m_PathID"]) if target is not None else None
    except Exception:
        return None


def go_name(go):
    try:
        return go.read_typetree().get("m_Name", "?")
    except Exception:
        return "?"


def walk(go, info, depth=0):
    """Accumulate component info over a prefab hierarchy."""
    try:
        g = go.read_typetree()
    except Exception:
        return
    info["objects"] += 1
    transform = None
    for comp in g.get("m_Component", []):
        c = deref(go.assets_file, comp["component"])
        if c is None:
            continue
        t = c.type.name
        if t in ("Transform", "RectTransform"):
            transform = c
        elif t == "MeshRenderer" or t == "SkinnedMeshRenderer":
            info["renderers"] += 1
            try:
                r = c.read_typetree()
                info["lightmapIndex"][r.get("m_LightmapIndex", -1)] += 1
                info["lightProbeUsage"][r.get("m_LightProbeUsage", -1)] += 1
                info["receiveGI"][r.get("m_ReceiveGI", -1)] += 1
                info["staticFlags"][g.get("m_StaticEditorFlags", 0)] += 1
                for m in r.get("m_Materials", []):
                    mo = deref(c.assets_file, m)
                    if mo is not None:
                        info["materials"][go_name(mo)] += 1
            except Exception as e:
                info["errors"].append(f"renderer: {e}")
        elif t == "MeshFilter":
            try:
                mesh = deref(c.assets_file, c.read_typetree().get("m_Mesh"))
                if mesh is not None:
                    aabb = mesh.read_typetree().get("m_LocalAABB", {})
                    ext = aabb.get("m_Extent", {})
                    info["maxExtent"] = max(info["maxExtent"], ext.get("x", 0), ext.get("z", 0))
            except Exception:
                pass
        elif t == "Light":
            try:
                l = c.read_typetree()
                info["lights"].append({"type": l.get("m_Type"), "bake": l.get("m_Lightmapping")})
            except Exception:
                info["lights"].append({"type": "?"})
        elif t in ("BoxCollider", "MeshCollider", "CapsuleCollider", "SphereCollider"):
            info["colliders"][t] += 1
        elif t == "LODGroup":
            info["lodGroups"] += 1
        elif t == "ReflectionProbe":
            info["reflectionProbes"] += 1
        elif t == "MonoBehaviour":
            sc = script_of(c)
            if sc is not None:
                info["scripts"][sc.m_ClassName] += 1
        else:
            info["other"][t] += 1
    if transform is None or depth > 12:
        return
    try:
        for child in transform.read_typetree().get("m_Children", []):
            ct = deref(transform.assets_file, child)
            if ct is None:
                continue
            cgo = deref(ct.assets_file, ct.read_typetree()["m_GameObject"])
            if cgo is not None:
                walk(cgo, info, depth + 1)
    except Exception as e:
        info["errors"].append(f"children: {e}")


_prefab_cache = {}


def prefab_info(af, pptr):
    go = deref(af, pptr)
    if go is None:
        return {"name": None}
    key = (go.assets_file.name, go.path_id)
    if key in _prefab_cache:
        return _prefab_cache[key]
    res = {"name": go_name(go), "file": os.path.basename(go.assets_file.name), "pathId": go.path_id}
    if DEEP:
        info = {"objects": 0, "renderers": 0, "lightmapIndex": Counter(), "lightProbeUsage": Counter(),
                "receiveGI": Counter(), "staticFlags": Counter(), "materials": Counter(), "maxExtent": 0.0,
                "lights": [], "colliders": Counter(), "lodGroups": 0, "reflectionProbes": 0, "scripts": Counter(),
                "other": Counter(), "errors": []}
        walk(go, info)
        info = {k: (dict(v) if isinstance(v, Counter) else v) for k, v in info.items()}
        res.update(info)
    _prefab_cache[key] = res
    return res


def arr(af, lst):
    return [prefab_info(af, p) for p in (lst or [])]


print("Loading game data (lazy)...", flush=True)
env = UnityPy.load(DATA)

scenes = []
for obj in env.objects:
    if obj.type.name == "BuildSettings":
        scenes = [os.path.splitext(os.path.basename(s))[0] for s in obj.read_typetree()["scenes"]]
        break

maze_scene = {}
mazegens = {}
for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    sc = script_of(obj)
    if sc is None:
        continue
    if sc.m_ClassName == "MazeTypesConfigSO" and not maze_scene:
        for i, d in enumerate(tree(obj, sc).get("mazeTypesData", [])):
            if i < len(MAZE_TYPES) and d.get("sceneName"):
                maze_scene[MAZE_TYPES[i]] = d["sceneName"]
    elif sc.m_ClassName == "MazeGenerator":
        mazegens.setdefault(os.path.basename(obj.assets_file.name), []).append((obj, sc))

lower = [s.lower() for s in scenes]
summary = {}
os.makedirs(OUTDIR, exist_ok=True)
for maze, scene in maze_scene.items():
    if args and maze not in args:
        continue
    if scene.lower() not in lower:
        print(f"{maze}: scene {scene} not in build")
        continue
    n = lower.index(scene.lower())
    gens = mazegens.get(f"level{n}", []) + mazegens.get(f"sharedassets{n}.assets", [])
    if not gens:
        print(f"{maze} ({scene}, level{n}): no MazeGenerator")
        continue
    obj, sc = gens[0]
    t = tree(obj, sc)
    af = obj.assets_file
    subs = []
    for i, s in enumerate(t.get("possibleSubmazes", [])):
        subs.append({"index": i, "chanceToSpawn": s.get("chanceToSpawn"), "spawnTries": s.get("spawnTries"),
                     "radius": [s.get("submazeRadiusMin"), s.get("submazeRadiusMax")],
                     **{a: arr(af, s.get(a)) for a in SUB_ARRAYS}})
    res = {"maze": maze, "scene": scene, "level": n, "tileSize": t.get("tileSize"),
           "rareGenerationChance": t.get("rareGenerationChance"), "rotationOffsetY": t.get("rotationOffsetY"),
           "wallPieces": arr(af, t.get("wallPieces")), "wallPiecesRare": arr(af, t.get("wallPiecesRare")),
           "startPieces": arr(af, t.get("startPieces")), "endPieces": arr(af, t.get("endPieces")),
           "transitionPieces": [{"connection": tp.get("connection"), "n": len(tp.get("transitionPieces") or []),
                                 "nRare": len(tp.get("transitionPiecesRare") or [])}
                                for tp in t.get("transitionPieces", [])],
           "materialsToCombine": [go_name(m) if m else None for m in
                                  (deref(af, p) for p in t.get("materialsToCombine", []))],
           "submazes": subs, "mazeGenerators": len(gens)}

    # scene-level lighting and what an additive load would wake up
    level = next((f for k, f in env.files.items() if os.path.basename(k).lower() == f"level{n}"), None)
    if level is not None:
        lm = next((o for o in level.objects.values() if o.type.name == "LightmapSettings"), None)
        if lm is not None:
            try:
                l = lm.read_typetree()
                res["lightmaps"] = len(l.get("m_Lightmaps", []))
                res["lightmapsMode"] = l.get("m_LightmapsMode")
                res["lightProbes"] = bool(l.get("m_LightProbes", {}).get("m_PathID"))
            except Exception as e:
                res["lightmaps"] = f"err {e}"
        if SCENE_SCRIPTS:
            cls = Counter()
            types = Counter()
            for o in level.objects.values():
                types[o.type.name] += 1
                if o.type.name == "MonoBehaviour":
                    s2 = script_of(o)
                    if s2 is not None:
                        cls[f"{s2.m_Namespace}.{s2.m_ClassName}".lstrip(".")] += 1
            res["sceneScripts"] = dict(cls.most_common())
            res["sceneTypes"] = dict(types.most_common())

    with open(os.path.join(OUTDIR, f"pieces_{maze}.json"), "w", encoding="utf-8") as f:
        json.dump(res, f, indent=1, default=str)

    def cnt(s):
        return "/".join(str(len(s[a])) for a in SUB_ARRAYS)
    print(f"{maze:15} {scene:28} level{n:<3} tile={res['tileSize']} rare%={res['rareGenerationChance']} "
          f"walls={len(res['wallPieces'])}+{len(res['wallPiecesRare'])}r subs={len(subs)} "
          f"lightmaps={res.get('lightmaps')} trans={len(res['transitionPieces'])}", flush=True)
    for s in subs:
        names = sorted({p['name'] for a in SUB_ARRAYS for p in s[a] if p.get('name')})
        print(f"   sub{s['index']} chance={s['chanceToSpawn']} counts(st/stR/ec/ecR/3w/3wR/tu/tuR/fl/flR/sz/szR)={cnt(s)} "
              f"e.g. {names[:4]}", flush=True)
