using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSteamworks;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LabyHelper.Features;

/// <summary>
/// EXPERIMENT (off by default, solo only): rare maze pieces from another map ("donor") mixed into random cases.
///
/// Every piece getter picks from possibleSubmazes[mazeTypeMap[x,y]] (index 0 = the main maze) and uses the *Rare
/// array when Random.Range(0,100) &lt;= rareGenerationChance. Pieces are prefabs that only exist in each map's own
/// scene (levelN/sharedassetsN; not in Addressables), and GenerateMap is tail-called from MazeGenerator.Start, so
/// it can't wait for an async load. Instead:
///  1. In the lobby (solo), the donor scene is loaded additively. While it loads, its scene-level scripts are
///     muted (Awake/OnEnable/Start/OnDisable/OnDestroy prefixes, only for objects in that scene; list verified
///     offline to have unique native bodies), its roots are deactivated on sceneLoaded, the donor MazeGenerator's
///     rare arrays are copied and flagged DontUnloadUnusedAsset, and the scene is unloaded again.
///  2. In GenerateMap (random case, solo, same tileSize and rotationOffsetY) the donor pieces are appended to the
///     current main maze's rare arrays. The scene's MazeGenerator is rebuilt per map, so nothing to restore.
/// Generation runs on every peer from the contract seed, so this must never run with other players.
/// Findings and test plan: re/EXPERIMENT-foreign-pieces.md.
/// </summary>
internal static class ForeignPieces
{
    private const string Tag = "ForeignPieces";

    // Scene-level scripts of the Rand_Maze_* scenes (union of all 19, from re/scripts/inspect_pieces.py) whose
    // lifecycle methods have a native body of their own. Folded bodies (shared with other methods) are left out.
    private static readonly (string Type, string[] Methods)[] Mute =
    {
        ("AmbientAudioSource", new[] { "Awake" }),
        ("CharacterCustomization.RndCustomizationItemsManager", new[] { "Awake" }),
        ("Dissonance.DissonanceComms", new[] { "OnEnable", "Start", "OnDisable", "OnDestroy" }),
        ("DissonanceLocalPlayerNameInitializer", new[] { "Awake" }),
        ("ExperienceBarUI", new[] { "Start" }),
        ("GPUInstancer.GPUInstancerPrefabManager", new[] { "Awake" }),
        ("GPUInstancer.GPUInstancerManager", new[] { "Awake", "OnEnable", "Start", "OnDisable" }),
        ("GameManager", new[] { "Awake", "Start" }),
        ("MazeGenerator", new[] { "Awake", "Start" }),
        ("Mirror.NetworkIdentity", new[] { "Awake", "OnDestroy" }),
        ("PillarManager", new[] { "Awake" }),
        ("RandomGeneration.RndObjectiveManager", new[] { "Awake", "OnDestroy" }),
        ("SceneAudioListener", new[] { "Awake" }),
        ("TeleporterPoints", new[] { "Awake" }),
        ("Unity.AI.Navigation.NavMeshModifier", new[] { "OnEnable", "OnDisable" }),
        ("Unity.AI.Navigation.NavMeshModifierVolume", new[] { "OnEnable", "OnDisable" }),
        ("Unity.AI.Navigation.NavMeshSurface", new[] { "OnEnable", "OnDisable" }),
        ("UnityEngine.Rendering.HighDefinition.StaticLightingSky", new[] { "OnEnable", "OnDisable" }),
        ("UnityEngine.Rendering.Volume", new[] { "OnEnable", "OnDisable" }),
        ("ValkoGames.Labyrinthine.Cases.Misc.AztecTunnelsOcclusionController", new[] { "Awake" }),
        ("ValkoGames.Labyrinthine.Cases.Misc.CaseRoofCollider", new[] { "Awake" }),
        ("ValkoGames.Labyrinthine.Cases.PathVisualisation.PathVisualisationUI", new[] { "OnDisable" }),
        ("ValkoGames.Labyrinthine.Cases.RndStats", new[] { "Awake" }),
        ("ValkoGames.Labyrinthine.Dev.DevToolsSync", new[] { "Start", "OnDestroy" }),
        ("ValkoGames.Labyrinthine.Interactions.Story.SceneGameValues", new[] { "Awake" }),
        ("ValkoGames.Labyrinthine.Misc.FogProfileChanger", new[] { "Start", "OnDestroy" }),
        ("ValkoGames.Labyrinthine.Misc.PlayRandomSoundsAroundPlayers", new[] { "Start" }),
        ("ValkoGames.Labyrinthine.Systems.Navigation.Pathfinding3D", new[] { "Awake" }),
        ("ValkoGames.Labyrinthine.Systems.ProceduralLightProbes.LightProbeManager", new[] { "Awake", "OnDestroy" }),
        ("ValkoGames.Labyrinthine.VR.UI.WorldSpaceOverlayUI", new[] { "Start" }),
        ("WeatherFollow", new[] { "Start" }),
        ("ftLightmapsStorage", new[] { "Awake", "Start", "OnDestroy" }),
    };

    private static readonly string[] InteropAssemblies =
    {
        "Assembly-CSharp", "Assembly-CSharp-firstpass", "Il2CppGPUInstancer", "Il2CppDissonanceVoip", "Il2CppMirror",
        "Unity.AI.Navigation", "Il2CppBakeryRuntimeAssembly", "Unity.RenderPipelines.Core.Runtime",
        "Unity.RenderPipelines.HighDefinition.Runtime",
    };

    private static readonly string[] Categories = { "straight", "endcap", "threeway", "turn", "floor", "wall" };

    private sealed class Donor
    {
        public string Maze, Scene;
        public int TileSize;
        public float RotationY;
        public readonly GameObject[][] Pieces = new GameObject[Categories.Length][];
        public readonly Dictionary<int, int> CategoryById = new();
        public int Count => Pieces.Sum(p => p?.Length ?? 0);
        public int Alive => Pieces.Sum(p => p?.Count(g => g != null) ?? 0);
    }

    private static HarmonyLib.Harmony _harmony;
    private static bool _mutePatched, _countPatched, _harvesting;
    private static string _loadingScene; // donor scene name while it is loaded (muting is on)
    private static int _loadingHandle;    // its Scene.handle, so a normal load of the same map is never muted
    private static float _nextPoll, _lobbySince = -1f, _retryAt;
    private static Donor _donor;
    private static bool _counting;
    private static readonly int[] Picks = new int[Categories.Length];
    private static int _foreignPicks;

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        _harmony = harmony;
        harmony.Patch(AccessTools.Method(typeof(Il2Cpp.MazeGenerator), "GenerateMap"),
            prefix: new HarmonyMethod(typeof(ForeignPieces), nameof(GenerateMapPrefix)),
            postfix: new HarmonyMethod(typeof(ForeignPieces), nameof(GenerateMapPostfix)));
        Core.Log.Msg($"{Tag}: hooked GenerateMap (experiment {(Settings.ExperimentForeignPieces.Value ? "ON" : "off")}, donor {Settings.ExperimentDonorMaze.Value}).");
    }

    /// <summary>Called first from Core.OnSceneWasLoaded. True when the scene is our donor (the caller skips its usual work).</summary>
    public static bool OnSceneLoaded(string sceneName)
    {
        if (_loadingScene == null) return false;
        if (sceneName.Equals(_loadingScene, StringComparison.OrdinalIgnoreCase) && DonorScene() is Scene s)
        {
            DeactivateRoots(s);
            return true;
        }
        Core.Log.Warning($"{Tag}: {sceneName} loaded while the donor was loading.");
        return false;
    }

    /// <summary>Polled from Core.OnUpdate: loads the donor once we've been in the lobby a few seconds.</summary>
    public static void Update()
    {
        if (!Settings.ExperimentForeignPieces.Value || _harvesting) return;
        float now = Time.unscaledTime;
        if (now < _nextPoll) return;
        _nextPoll = now + 2f;
        string active;
        try { active = SceneManager.GetActiveScene().name; } catch { return; }
        if (active == null || !active.StartsWith("Lobby", StringComparison.OrdinalIgnoreCase)) { _lobbySince = -1f; return; }
        if (_lobbySince < 0f) _lobbySince = now;
        if (now - _lobbySince < 4f || now < _retryAt) return; // let the lobby settle; don't hammer on failures
        string maze = (Settings.ExperimentDonorMaze.Value ?? "").Trim();
        if (_donor != null && _donor.Maze == maze && _donor.Alive == _donor.Count) return;
        _retryAt = now + 30f;
        MelonCoroutines.Start(Harvest());
    }

    // ---------------------------------------------------------------- lobby: load donor, copy, unload

    private static IEnumerator Harvest()
    {
        _harvesting = true;
        string maze = (Settings.ExperimentDonorMaze.Value ?? "").Trim();
        string scene = "Rand_" + maze;
        if (!Solo(out var why))
        {
            Core.Log.Msg($"{Tag}: donor not loaded, not solo ({why}).");
            _harvesting = false;
            yield break;
        }

        var sw = Stopwatch.StartNew();
        var op = BeginLoad(scene);
        if (op == null) { _harvesting = false; yield break; }
        while (!op.isDone) yield return null;
        long loadMs = sw.ElapsedMilliseconds;

        Donor donor = null;
        AsyncOperation unload = null;
        if (DonorScene() is Scene loaded)
        {
            DeactivateRoots(loaded); // in case sceneLoaded fired before we could react
            donor = Copy(maze, loaded);
            try { unload = SceneManager.UnloadSceneAsync(loaded); }
            catch (Exception e) { Core.Log.Warning($"{Tag}: unload {scene} failed: {e.Message}"); }
        }
        else Core.Log.Warning($"{Tag}: donor scene {scene} vanished before it could be read (scene change?).");
        if (unload != null) while (!unload.isDone) yield return null;
        _loadingScene = null;
        _loadingHandle = 0;

        if (donor != null)
        {
            _donor = donor;
            Core.Log.Msg($"{Tag}: donor {maze} ready: {Summary(donor)}, tileSize {donor.TileSize}, rotY {donor.RotationY:0}. " +
                         $"Load {loadMs} ms, total {sw.ElapsedMilliseconds} ms.");
            Toast.Show(Loc.T($"Experimento: piezas de {maze} listas ({donor.Count})", $"Experiment: {maze} pieces ready ({donor.Count})"), 4f);
        }
        _harvesting = false;
    }

    private static AsyncOperation BeginLoad(string scene)
    {
        try
        {
            if (Application.CanStreamedLevelBeLoaded(scene) == false)
            {
                Core.Log.Warning($"{Tag}: scene {scene} is not in the build (ExperimentDonorMaze = Maze_A..Maze_R or Maze_Backrooms).");
                return null;
            }
            EnsureMutePatched();
            EnsureCountPatched();
            _loadingScene = scene;
            _loadingHandle = 0;
            Core.Log.Msg($"{Tag}: loading donor scene {scene} additively...");
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            if (op == null) { _loadingScene = null; return null; }
            // The loading scene is listed right away; remember its handle.
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (!sc.isLoaded && sc.name.Equals(scene, StringComparison.OrdinalIgnoreCase)) { _loadingHandle = sc.handle; break; }
            }
            Core.Log.Msg($"{Tag}: donor scene handle {(_loadingHandle != 0 ? _loadingHandle.ToString() : "unknown, matching by name")}.");
            return op;
        }
        catch (Exception e)
        {
            Core.Log.Error($"{Tag}: could not load {scene}: {e}");
            _loadingScene = null;
            return null;
        }
    }

    /// <summary>The additive donor scene, if it is loaded (matched by handle when known).</summary>
    private static Scene? DonorScene()
    {
        if (_loadingScene == null) return null;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var sc = SceneManager.GetSceneAt(i);
            if (sc.isLoaded && IsDonor(sc)) return sc;
        }
        return null;
    }

    /// <summary>True for the additive donor scene; never for the active scene (a real load of that map).</summary>
    private static bool IsDonor(Scene sc)
    {
        if (_loadingScene == null) return false;
        if (_loadingHandle != 0) return sc.handle == _loadingHandle;
        return sc.name.Equals(_loadingScene, StringComparison.OrdinalIgnoreCase) && SceneManager.GetActiveScene().handle != sc.handle;
    }

    private static void DeactivateRoots(Scene s)
    {
        try
        {
            int n = 0;
            foreach (var root in s.GetRootGameObjects())
                if (root != null && root.activeSelf) { root.SetActive(false); n++; }
            if (n > 0) Core.Log.Msg($"{Tag}: deactivated {n} root object(s) of {s.name}.");
        }
        catch (Exception e) { Core.Log.Warning($"{Tag}: deactivate {s.name}: {e.Message}"); }
    }

    private static Donor Copy(string maze, Scene s)
    {
        string scene = s.name;
        try
        {
            Il2Cpp.MazeGenerator gen = null;
            foreach (var root in s.GetRootGameObjects())
            {
                gen = root.GetComponentInChildren<Il2Cpp.MazeGenerator>(true);
                if (gen != null) break;
            }
            if (gen == null) { Core.Log.Warning($"{Tag}: no MazeGenerator in {scene}."); return null; }
            var main = gen.possibleSubmazes != null && gen.possibleSubmazes.Length > 0 ? gen.possibleSubmazes[0] : null;
            if (main == null) { Core.Log.Warning($"{Tag}: {scene} has no main piece set."); return null; }

            var d = new Donor { Maze = maze, Scene = scene, TileSize = gen.tileSize, RotationY = gen.rotationOffsetY };
            d.Pieces[0] = Keep(main.straightPiecesRare);
            d.Pieces[1] = Keep(main.endCapsRare);
            d.Pieces[2] = Keep(main.threewayPiecesRare);
            d.Pieces[3] = Keep(main.turnPiecesRare);
            d.Pieces[4] = Keep(main.floorPiecesRare);
            d.Pieces[5] = Keep(gen.wallPiecesRare);
            for (int c = 0; c < d.Pieces.Length; c++)
                foreach (var go in d.Pieces[c])
                    d.CategoryById[go.GetInstanceID()] = c;
            if (d.Count == 0) Core.Log.Warning($"{Tag}: {maze} has no rare pieces to lend.");
            return d;
        }
        catch (Exception e)
        {
            Core.Log.Error($"{Tag}: copying pieces from {scene} failed: {e}");
            return null;
        }
    }

    /// <summary>Copies the array and keeps each prefab (and its meshes/materials) through scene changes.</summary>
    private static GameObject[] Keep(Il2CppReferenceArray<GameObject> arr)
    {
        if (arr == null) return Array.Empty<GameObject>();
        var list = new List<GameObject>();
        foreach (var go in arr)
        {
            if (go == null) continue;
            go.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null) m.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) mf.sharedMesh.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            list.Add(go);
        }
        return list.ToArray();
    }

    // ---------------------------------------------------------------- maze: inject

    private static void GenerateMapPrefix(Il2Cpp.MazeGenerator __instance)
    {
        _counting = false;
        if (!Settings.ExperimentForeignPieces.Value) return;
        try
        {
            var c = Il2Cpp.GameValues.instance?.RandomMazeContract;
            if (__instance == null || c == null) return;
            if (c.IsCustom) { Core.Log.Msg($"{Tag}: custom case, not applied."); return; }
            if (!Solo(out var why)) { Core.Log.Msg($"{Tag}: not applied, not solo ({why})."); return; }
            var d = _donor;
            if (d == null) { Core.Log.Msg($"{Tag}: no donor loaded yet (it loads in the lobby, solo)."); return; }
            string here = c.MazeType.ToString();
            if (here == d.Maze) { Core.Log.Msg($"{Tag}: {here} is the donor itself, not applied."); return; }
            if (__instance.tileSize != d.TileSize || Math.Abs(__instance.rotationOffsetY - d.RotationY) > 0.01f)
            {
                Core.Log.Msg($"{Tag}: {here} tileSize {__instance.tileSize}/rotY {__instance.rotationOffsetY:0} vs donor " +
                             $"{d.Maze} {d.TileSize}/{d.RotationY:0}: not applied.");
                return;
            }
            int alive = d.Alive;
            if (alive < d.Count) Core.Log.Warning($"{Tag}: only {alive}/{d.Count} donor prefabs survived the scene change.");
            if (alive == 0) return;

            var main = __instance.possibleSubmazes != null && __instance.possibleSubmazes.Length > 0 ? __instance.possibleSubmazes[0] : null;
            if (main == null) return;
            var own = new[]
            {
                Len(main.straightPiecesRare), Len(main.endCapsRare), Len(main.threewayPiecesRare),
                Len(main.turnPiecesRare), Len(main.floorPiecesRare), Len(__instance.wallPiecesRare),
            };
            main.straightPiecesRare = Append(main.straightPiecesRare, d.Pieces[0]);
            main.endCapsRare = Append(main.endCapsRare, d.Pieces[1]);
            main.threewayPiecesRare = Append(main.threewayPiecesRare, d.Pieces[2]);
            main.turnPiecesRare = Append(main.turnPiecesRare, d.Pieces[3]);
            main.floorPiecesRare = Append(main.floorPiecesRare, d.Pieces[4]);
            __instance.wallPiecesRare = Append(__instance.wallPiecesRare, d.Pieces[5]);

            Array.Clear(Picks);
            _foreignPicks = 0;
            _counting = true;
            var parts = Categories.Select((name, i) => $"{name} {own[i]}+{d.Pieces[i].Count(g => g != null)}");
            Core.Log.Msg($"{Tag}: injected {d.Maze} rare pieces into {here}: {string.Join(", ", parts)} " +
                         $"(own+foreign), rare chance {__instance.rareGenerationChance:0}%.");
        }
        catch (Exception e) { Core.Log.Error($"{Tag}: inject failed, map generates as usual: {e}"); }
    }

    private static void GenerateMapPostfix()
    {
        if (!_counting) return;
        _counting = false;
        var parts = Categories.Select((name, i) => $"{name} {Picks[i]}");
        Core.Log.Msg($"{Tag}: generation done, foreign pieces picked {_foreignPicks} times ({string.Join(", ", parts)}). " +
                     "Count includes discarded attempts.");
    }

    private static void PiecePostfix(GameObject __result)
    {
        if (!_counting || __result == null || _donor == null) return;
        if (_donor.CategoryById.TryGetValue(__result.GetInstanceID(), out int c))
        {
            Picks[c]++;
            _foreignPicks++;
        }
    }

    private static int Len(Il2CppReferenceArray<GameObject> a) => a?.Length ?? 0;

    private static Il2CppReferenceArray<GameObject> Append(Il2CppReferenceArray<GameObject> own, GameObject[] extra)
    {
        var add = extra.Where(g => g != null).ToArray();
        if (add.Length == 0) return own;
        var list = new List<GameObject>();
        if (own != null) list.AddRange(own);
        list.AddRange(add);
        return new Il2CppReferenceArray<GameObject>(list.ToArray());
    }

    private static string Summary(Donor d) =>
        string.Join(", ", Categories.Select((name, i) => $"{name} {d.Pieces[i].Length}")) +
        $" (e.g. {string.Join(", ", d.Pieces.SelectMany(p => p).Take(4).Select(g => g.name))})";

    // ---------------------------------------------------------------- solo check

    private static bool Solo(out string why)
    {
        why = null;
        try
        {
            if (Il2CppMirror.NetworkClient.active && !Il2CppMirror.NetworkServer.active) { why = "sos cliente"; return false; }
            if (Il2CppMirror.NetworkServer.active && Il2CppMirror.NetworkServer.connections.Count > 1)
            {
                why = $"{Il2CppMirror.NetworkServer.connections.Count} conexiones";
                return false;
            }
            var t = Object.FindObjectOfType<Il2Cpp.ExtendedSteamTransport>();
            if (t == null) return true;
            var id = t.LobbyID;
            if (id.m_SteamID == 0) return true;
            int n = SteamMatchmaking.GetNumLobbyMembers(id);
            if (n > 1) { why = $"{n} jugadores en el lobby de Steam"; return false; }
            return true;
        }
        catch (Exception e)
        {
            why = e.Message;
            return false;
        }
    }

    // ---------------------------------------------------------------- patches applied on first use

    private static void EnsureCountPatched()
    {
        if (_countPatched) return;
        _countPatched = true;
        int ok = 0;
        foreach (var name in new[] { "GetStraightPiece", "GetEndCapPiece", "GetThreewayPiece", "GetTurnPiece", "GetFloorPiece", "GetWallPiece" })
        {
            try
            {
                _harmony.Patch(AccessTools.Method(typeof(Il2Cpp.MazeGenerator), name),
                    postfix: new HarmonyMethod(typeof(ForeignPieces), nameof(PiecePostfix)));
                ok++;
            }
            catch (Exception e) { Core.Log.Warning($"{Tag}: could not hook {name}: {e.Message}"); }
        }
        Core.Log.Msg($"{Tag}: counting hooks {ok}/6.");
    }

    private static void EnsureMutePatched()
    {
        if (_mutePatched) return;
        _mutePatched = true;
        var prefix = new HarmonyMethod(typeof(ForeignPieces), nameof(MutePrefix));
        int ok = 0, total = 0;
        var missing = new List<string>();
        foreach (var (typeName, methods) in Mute)
        {
            var type = Resolve(typeName);
            foreach (var m in methods)
            {
                total++;
                if (type == null) { missing.Add($"{typeName}.{m}"); continue; }
                try
                {
                    var mi = AccessTools.DeclaredMethod(type, m, Type.EmptyTypes);
                    if (mi == null) { missing.Add($"{typeName}.{m}"); continue; }
                    _harmony.Patch(mi, prefix: prefix);
                    ok++;
                }
                catch (Exception e) { missing.Add($"{typeName}.{m} ({e.Message})"); }
            }
        }
        Core.Log.Msg($"{Tag}: muted {ok}/{total} donor lifecycle methods." +
                     (missing.Count > 0 ? " Missing: " + string.Join(", ", missing) : ""));

        // sceneLoaded listeners that react to ANY scene. Mirror's NetworkManager.OnSceneLoaded calls
        // NetworkServer.SpawnObjects() and tried to network-spawn the donor's (muted) NetworkIdentities, throwing in
        // NetworkIdentity.OnStartServer (seen in testing). The others re-apply lightmaps, reset objectives/subtitles.
        var sceneLoaded = new HarmonyMethod(typeof(ForeignPieces), nameof(SceneLoadedPrefix));
        int sl = 0;
        foreach (var typeName in new[] { "Mirror.NetworkManager", "PrefabLightmapData", "Subtitles.SubtitleManager",
                                         "Objectives.ObjectiveManager", "StatsMonitor.StatsMonitor" })
        {
            try
            {
                var type = Resolve(typeName);
                var mi = type == null ? null : AccessTools.DeclaredMethod(type, "OnSceneLoaded");
                if (mi == null) { Core.Log.Warning($"{Tag}: {typeName}.OnSceneLoaded not found."); continue; }
                _harmony.Patch(mi, prefix: sceneLoaded);
                sl++;
            }
            catch (Exception e) { Core.Log.Warning($"{Tag}: could not hook {typeName}.OnSceneLoaded: {e.Message}"); }
        }
        Core.Log.Msg($"{Tag}: donor scene hidden from {sl}/5 sceneLoaded listeners.");
    }

    /// <summary>sceneLoaded listeners ignore the donor scene.</summary>
    private static bool SceneLoadedPrefix(Scene scene)
    {
        try { return !IsDonor(scene); }
        catch { return true; }
    }

    /// <summary>Skips lifecycle methods of objects that belong to the donor scene while it is loaded.</summary>
    private static bool MutePrefix(Component __instance)
    {
        if (_loadingScene == null) return true;
        try
        {
            return __instance == null || !IsDonor(__instance.gameObject.scene);
        }
        catch { return true; }
    }

    /// <summary>Il2Cpp interop names: global types live in "Il2Cpp", namespaced ones get an "Il2Cpp" prefix
    /// (Unity and Unity.* assemblies keep their names).</summary>
    private static Type Resolve(string il2cppName)
    {
        var candidates = il2cppName.Contains('.')
            ? new[] { "Il2Cpp" + il2cppName, il2cppName }
            : new[] { "Il2Cpp." + il2cppName, il2cppName };
        var assemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
        foreach (var name in InteropAssemblies)
        {
            if (assemblies.Any(a => a.GetName().Name == name)) continue;
            try { assemblies.Add(Assembly.Load(name)); } catch { /* not present */ }
        }
        foreach (var asm in assemblies)
            foreach (var cand in candidates)
            {
                Type t = null;
                try { t = asm.GetType(cand, false); } catch { }
                if (t != null && typeof(Component).IsAssignableFrom(t)) return t;
            }
        return null;
    }
}
