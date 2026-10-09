using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Il2CppCharacterCustomization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader.Utils;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// The cosmetic pool of a map lives on the RndCustomizationSpawner components of that map's maze modules,
/// which don't exist in the lobby. We learn them whenever a map is played (host or client) and persist them
/// to UserData/LabyHelper.pools.json so the case board can predict the cosmetic before you start.
/// Order matters: WeightedRandom walks the list cumulatively, so we store (itemID, weight) in game order.
/// </summary>
internal static class PoolCache
{
    /// <param name="OnRoot">Spawn only honours the exclusion list when the pickup sits on the prefab root.</param>
    /// <param name="FromMap">True if it comes from the spawner's possiblePrefabs (map-specific), false for the global pool.</param>
    public record Entry(ushort Id, double Weight, bool OnRoot = true, bool FromMap = false);

    public class MapPools
    {
        // Each distinct candidate list seen on this map's spawners (normally exactly one).
        public List<List<Entry>> Normal { get; set; } = new();
        public List<List<Entry>> Hardcore { get; set; } = new();
    }

    public class SeasonalPool
    {
        public float Chance { get; set; }
        public List<Entry> Items { get; set; } = new();
    }

    public class Data
    {
        public int Version { get; set; }
        public Dictionary<string, MapPools> Maps { get; set; } = new();
        public Dictionary<string, SeasonalPool> Seasonal { get; set; } = new();
        public Dictionary<string, float> ChanceForItem { get; set; } = new();
    }

    private const int FormatVersion = 2;
    private static readonly string FilePath = Path.Combine(MelonEnvironment.UserDataDirectory, "LabyHelper.pools.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static Data Current { get; private set; } = new();

    /// <summary>
    /// A spawner's possiblePrefabs mixes true exclusives with items every map shares (the basic beanies and caps
    /// sit in all 19 lists with weight 5). An item is a map exclusive only if exactly one map lists it there.
    /// </summary>
    public static bool IsMapExclusive(ushort id)
    {
        if (_mapCounts == null)
        {
            _mapCounts = new Dictionary<ushort, int>();
            foreach (var mp in Current.Maps.Values)
            {
                var ids = mp.Normal.Concat(mp.Hardcore).SelectMany(l => l).Where(e => e.FromMap).Select(e => e.Id).ToHashSet();
                foreach (var i in ids) _mapCounts[i] = _mapCounts.GetValueOrDefault(i) + 1;
            }
        }
        return _mapCounts.TryGetValue(id, out var n) && n == 1;
    }

    private static Dictionary<ushort, int> _mapCounts;

    /// <summary>Raised after new pool data is learned, so the case board can refresh.</summary>
    public static event Action Changed;

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath)) ?? new Data();
            if (Current.Version != FormatVersion) Current = new Data(); // missing/older format lacks FromMap
        }
        catch (Exception e)
        {
            Core.Log.Warning($"PoolCache: could not read {FilePath}: {e.Message}");
            Current = new Data();
        }
        int learned = Current.Maps.Count;
        MergeDefaults();
        _mapCounts = null;
        Core.Log.Msg($"PoolCache: {Current.Maps.Count} maps known ({learned} learned in game, " +
                     $"{_fromDefaults.Count} from the bundled extract).");
    }

    // Maps filled from the bundled extract (pools.default.json, built by re/scripts/extract_pools.py).
    // Playing the map replaces them with what the game actually has, and they are never written to the user file.
    private static readonly HashSet<string> _fromDefaults = new();

    private static void MergeDefaults()
    {
        try
        {
            using var stream = typeof(PoolCache).Assembly.GetManifestResourceStream("pools.default.json");
            if (stream == null) return;
            var defaults = JsonSerializer.Deserialize<Data>(new StreamReader(stream).ReadToEnd());
            if (defaults?.Maps == null) return;
            foreach (var (map, pools) in defaults.Maps)
            {
                if (Current.Maps.ContainsKey(map)) continue;
                Current.Maps[map] = pools;
                _fromDefaults.Add(map);
            }
            foreach (var (evt, sp) in defaults.Seasonal ?? new())
                if (!Current.Seasonal.ContainsKey(evt)) Current.Seasonal[evt] = sp;
            foreach (var (type, c) in defaults.ChanceForItem ?? new())
                if (!Current.ChanceForItem.ContainsKey(type)) Current.ChanceForItem[type] = c;
        }
        catch (Exception e)
        {
            Core.Log.Warning($"PoolCache: bundled pools unreadable: {e.Message}");
        }
    }

    private static void Save()
    {
        var toSave = new Data
        {
            Version = FormatVersion,
            Maps = Current.Maps.Where(kv => !_fromDefaults.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value),
            Seasonal = Current.Seasonal,
            ChanceForItem = Current.ChanceForItem,
        };
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(toSave, Json)); }
        catch (Exception e) { Core.Log.Warning($"PoolCache: could not write: {e.Message}"); }
    }

    /// <summary>Scan the current scene. Safe to call repeatedly; only writes when something new shows up.</summary>
    public static void LearnFromScene()
    {
        try
        {
            var contract = Il2Cpp.GameValues.instance?.RandomMazeContract;
            if (contract == null) return;
            string map = contract.MazeType.ToString();

            bool dirty = false;
            foreach (var spawner in UnityEngine.Object.FindObjectsOfType<RndCustomizationSpawner>(true))
                dirty |= LearnSpawner(map, spawner);
            dirty |= LearnManager();

            if (dirty)
            {
                Save();
                var mp = Current.Maps[map];
                Core.Log.Msg($"PoolCache: learned {map}: {mp.Normal.Count} normal list(s) " +
                             $"[{string.Join(" | ", mp.Normal.Select(l => l.Count))} items], {mp.Hardcore.Count} hardcore list(s).");
                _mapCounts = null;
                Changed?.Invoke();
            }
        }
        catch (Exception e)
        {
            Core.Log.Warning($"PoolCache: learn failed: {e.Message}");
        }
    }

    public static bool LearnSpawner(string map, RndCustomizationSpawner spawner)
    {
        if (spawner == null) return false;
        // First time we see a map that only had bundled data: the live game wins.
        if (_fromDefaults.Remove(map)) Current.Maps.Remove(map);
        if (!Current.Maps.TryGetValue(map, out var mp)) Current.Maps[map] = mp = new MapPools();

        var normal = Concat(spawner.globalPrefabs?.Value, spawner.possiblePrefabs);
        var hardcore = Concat(spawner.globalPrefabsHardcore?.Value, spawner.possiblePrefabsHardcore);
        return AddDistinct(mp.Normal, normal) | AddDistinct(mp.Hardcore, hardcore);
    }

    private static bool LearnManager()
    {
        var manager = RndCustomizationItemsManager.Instance;
        if (manager == null) return false;
        bool dirty = false;

        if (manager.seasonalItems != null)
        {
            foreach (var cfg in manager.seasonalItems)
            {
                if (cfg == null) continue;
                string key = cfg.seasonalEventType.ToString();
                var items = ToEntries(cfg.prefabs);
                if (items.Count == 0) continue;
                if (!Current.Seasonal.TryGetValue(key, out var old) || old.Chance != cfg.chance || !Same(old.Items, items))
                {
                    Current.Seasonal[key] = new SeasonalPool { Chance = cfg.chance, Items = items };
                    dirty = true;
                }
            }
        }

        var chances = manager.chanceForItem;
        if (chances != null)
        {
            string[] names = { "Normal", "Rare", "Hardcore" };
            for (int i = 0; i < chances.Length && i < names.Length; i++)
            {
                if (!Current.ChanceForItem.TryGetValue(names[i], out var c) || c != chances[i])
                {
                    Current.ChanceForItem[names[i]] = chances[i];
                    dirty = true;
                }
            }
        }
        return dirty;
    }

    private static List<Entry> Concat(Il2CppReferenceArray<Il2Cpp.WeightedPrefab> global, Il2CppReferenceArray<Il2Cpp.WeightedPrefab> map)
        => ToEntries(global).Concat(ToEntries(map, fromMap: true)).ToList();

    public static List<Entry> ToEntries(Il2CppReferenceArray<Il2Cpp.WeightedPrefab> prefabs, bool fromMap = false)
    {
        var list = new List<Entry>();
        if (prefabs == null) return list;
        foreach (var wp in prefabs)
        {
            // Keep entries without a pickup too (id 0xFFFF): they still take part in the weighted walk.
            ushort id = 0xFFFF;
            bool onRoot = false;
            var go = wp?.Value;
            if (go != null)
            {
                var root = go.GetComponent<CustomizationPickup>();
                var pickup = root ?? go.GetComponentInChildren<CustomizationPickup>(true);
                if (pickup != null) { id = pickup.ItemID; onRoot = root != null; }
            }
            list.Add(new Entry(id, wp?.Weight ?? 0, onRoot, fromMap));
        }
        return list;
    }

    private static bool AddDistinct(List<List<Entry>> lists, List<Entry> candidate)
    {
        if (candidate.Count == 0 || lists.Any(l => Same(l, candidate))) return false;
        lists.Add(candidate);
        return true;
    }

    private static bool Same(List<Entry> a, List<Entry> b) => a.Count == b.Count && a.SequenceEqual(b);
}
