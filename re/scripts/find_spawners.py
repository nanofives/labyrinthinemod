"""Scan every data file / addressable bundle for MonoBehaviours of the given script and report whether their
fields are readable (type tree present). usage: python find_spawners.py [ScriptName ...]"""
import UnityPy, os, sys, glob, time
G = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine\Labyrinthine_Data"
names = set(sys.argv[1:] or ["RndCustomizationSpawner"])
files = sorted(glob.glob(os.path.join(G, "level*")) + glob.glob(os.path.join(G, "*.assets"))
               + glob.glob(os.path.join(G, "StreamingAssets", "aa", "StandaloneWindows64", "*.bundle")))
files = [f for f in files if not f.endswith(".resS")]
t0 = time.time()
for f in files:
    try:
        env = UnityPy.load(f)
    except Exception as e:
        print("LOADFAIL", os.path.basename(f), e, flush=True); continue
    hits = 0; sample = None
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour": continue
        try:
            mb = obj.read(check_read=False)
            sc = mb.m_Script.read() if mb.m_Script else None
        except Exception:
            continue
        if sc is None or sc.m_ClassName not in names: continue
        hits += 1
        if sample is None:
            try:
                tt = obj.read_typetree()
                sample = ",".join(list(tt.keys())[:12])
            except Exception as e:
                sample = f"NO TYPETREE ({type(e).__name__})"
    if hits:
        print(f"{os.path.basename(f)}: {hits} -> {sample}", flush=True)
print(f"done in {time.time()-t0:.0f}s over {len(files)} files", flush=True)
