"""List MonoScripts of interest and which data file holds them."""
import UnityPy, os, sys
G = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine\Labyrinthine_Data"
want = {"RndCustomizationSpawner", "CustomizationPickup", "WeightedPrefabSetSO", "MazeTypeConfig", "RndCustomizationItemsManager"}
env = UnityPy.load(os.path.join(G, "globalgamemanagers.assets"))
for obj in env.objects:
    if obj.type.name == "MonoScript":
        d = obj.read()
        if d.m_ClassName in want:
            print(d.m_ClassName, d.m_Namespace, d.m_AssemblyName, "pathID", obj.path_id)
