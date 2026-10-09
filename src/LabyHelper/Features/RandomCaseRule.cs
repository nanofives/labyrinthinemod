using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2CppCharacterCustomization;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppRandomGeneration.Contracts;
using UnityEngine;
using WeightedPrefab = Il2Cpp.WeightedPrefab;
using WeightedPrefabSetSO = Il2CppValkoGames.Labyrinthine.Data.WeightedPrefabSetSO;

namespace LabyHelper.Features;

/// <summary>
/// Host rule for the random case files on the board (not custom cases, not story):
///  - a cosmetic always spawns (chanceForItem[type] = 1 for that SpawnCustomizationItems call);
///  - it is always one the party still needs, from the set Distribution assigned to this case;
///  - the item is picked here (seeded by the contract, identical on every modded peer, since cosmetics are spawned
///    locally by each peer). If its prefab isn't loaded in this map, a pickup with loadObjectModel (472 of 504
///    pickups load their model from the item ID in Start) carries it and its itemID is swapped before Start runs.
/// Spawn builds its candidates from globalPrefabs.Value + possiblePrefabs (or the hardcore pair) and may swap to a
/// seasonal list. For the duration of Spawn we point globalPrefabs at an empty set, put only the allowed prefabs
/// (from the global, map and seasonal pools loaded in this scene) in possiblePrefabs, and zero the seasonal
/// chances, so the game's own weighted pick runs over allowed items only. Everything is restored afterwards.
/// Other cases keep the game's behaviour. Off only from the options menu (by request).
/// </summary>
internal static class RandomCaseRule
{
    private static float? _savedChance;
    private static int _savedIndex;

    private static RndCustomizationSpawner _spawner;
    private static bool _hardcore;
    private static WeightedPrefabSetSO _savedGlobal;
    private static Il2CppReferenceArray<WeightedPrefab> _savedPossible;
    private static readonly List<(SeasonalItemsConfig cfg, float chance)> SavedSeasonal = new();
    private static WeightedPrefabSetSO _emptySet;

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(RndCustomizationItemsManager), "SpawnCustomizationItems"),
            prefix: new HarmonyMethod(typeof(RandomCaseRule), nameof(SpawnItemsPrefix)),
            postfix: new HarmonyMethod(typeof(RandomCaseRule), nameof(SpawnItemsPostfix)));
        harmony.Patch(AccessTools.Method(typeof(RndCustomizationSpawner), nameof(RndCustomizationSpawner.Spawn)),
            prefix: new HarmonyMethod(typeof(RandomCaseRule), nameof(SpawnPrefix)),
            postfix: new HarmonyMethod(typeof(RandomCaseRule), nameof(SpawnPostfix)));
        harmony.Patch(AccessTools.Method(typeof(CustomizationPickup), nameof(CustomizationPickup.Start)),
            prefix: new HarmonyMethod(typeof(RandomCaseRule), nameof(PickupStartPrefix)));
        Core.Log.Msg($"RandomCaseRule: hooked (enabled: {Settings.RandomCasesOnlyNew.Value}, distribute: {Settings.DistributeAcrossCases.Value}).");
    }

    /// <summary>True when this contract is a random case file and the rule is on.</summary>
    public static bool Applies(Contract c) => Rules.Active && Rules.OnlyNew && c != null && !c.IsCustom;

    private static Contract Current() => Il2Cpp.GameValues.instance?.RandomMazeContract;

    private static void SpawnItemsPrefix(RndCustomizationItemsManager __instance)
    {
        try
        {
            var c = Current();
            if (c == null || __instance?.chanceForItem == null) return;
            PoolCache.LearnFromScene(); // capture chanceForItem/seasonal before we touch them
            if (!Applies(c) || Distribution.Allowed(c).Count == 0) return;

            _savedIndex = (int)c.ContractType;
            _savedChance = __instance.chanceForItem[_savedIndex];
            __instance.chanceForItem[_savedIndex] = 1f;
            Core.Log.Msg($"RandomCaseRule: {c.MazeType}/{c.ContractType}, cosmetic chance {_savedChance:0.00} -> 1.");
        }
        catch (Exception e) { Core.Log.Warning($"RandomCaseRule: {e.Message}"); }
    }

    private static void SpawnItemsPostfix(RndCustomizationItemsManager __instance)
    {
        if (_savedChance == null || __instance?.chanceForItem == null) return;
        __instance.chanceForItem[_savedIndex] = _savedChance.Value;
        _savedChance = null;
    }

    private static void SpawnPrefix(RndCustomizationSpawner __instance)
    {
        try
        {
            var c = Current();
            if (__instance == null || !Applies(c)) return;
            var allowed = Distribution.Allowed(c);
            if (allowed.Count == 0)
            {
                Core.Log.Msg("RandomCaseRule: nothing new left for this case; game picks as usual.");
                return;
            }

            _hardcore = c.ContractType == ContractType.Hardcore;
            var pool = new List<WeightedPrefab>();
            pool.AddRange(Values(_hardcore ? __instance.globalPrefabsHardcore : __instance.globalPrefabs));
            pool.AddRange((_hardcore ? __instance.possiblePrefabsHardcore : __instance.possiblePrefabs)?.ToArray() ?? Array.Empty<WeightedPrefab>());
            var manager = RndCustomizationItemsManager.Instance;
            if (!_hardcore && manager?.seasonalItems != null)
                foreach (var cfg in manager.seasonalItems)
                    if (cfg?.prefabs != null) pool.AddRange(cfg.prefabs);
            var loaded = pool.Where(wp => wp != null && IdOf(wp) != null)
                             .GroupBy(wp => IdOf(wp).Value).ToDictionary(g => g.Key, g => g.First());

            // Same choice on every modded peer: seeded by the contract, over a sorted list.
            ushort target = PickTarget(c, allowed);
            WeightedPrefab use;
            if (loaded.TryGetValue(target, out var native))
            {
                use = native;
                _swapFrom = null;
            }
            else
            {
                // Not loaded in this map: spawn a pickup that loads its model from the item ID, then swap the ID.
                var carrier = loaded.OrderBy(kv => kv.Key).Select(kv => kv.Value).FirstOrDefault(LoadsModel);
                if (carrier == null)
                {
                    Core.Log.Msg("RandomCaseRule: no model-loading pickup in this map to carry a foreign item; game picks as usual.");
                    return;
                }
                use = carrier;
                _swapFrom = IdOf(carrier);
                _swapTo = target;
            }

            _spawner = __instance;
            _emptySet ??= MakeEmptySet();
            var single = new Il2CppReferenceArray<WeightedPrefab>(new[] { use });
            if (_hardcore)
            {
                _savedGlobal = __instance.globalPrefabsHardcore;
                _savedPossible = __instance.possiblePrefabsHardcore;
                __instance.globalPrefabsHardcore = _emptySet;
                __instance.possiblePrefabsHardcore = single;
            }
            else
            {
                _savedGlobal = __instance.globalPrefabs;
                _savedPossible = __instance.possiblePrefabs;
                __instance.globalPrefabs = _emptySet;
                __instance.possiblePrefabs = single;
                if (manager?.seasonalItems != null)
                    foreach (var cfg in manager.seasonalItems)
                        if (cfg != null) { SavedSeasonal.Add((cfg, cfg.chance)); cfg.chance = 0f; }
            }
            Core.Log.Msg($"RandomCaseRule: {c.MazeType}/{c.ContractType} -> {CosmeticAlert.Name(target)} (#{target}) " +
                         (_swapFrom != null ? $"carried by #{_swapFrom}" : "native to this map") +
                         $", out of {allowed.Count} assigned; party needed hash {NeededHash()}.");
        }
        catch (Exception e)
        {
            Core.Log.Error($"RandomCaseRule: prefix failed, game logic left as is: {e}");
            _swapFrom = null;
            Restore();
        }
    }

    // Pending swap: the next world pickup carrying _swapFrom gets _swapTo before its Start coroutine reads it.
    private static ushort? _swapFrom;
    /// <summary>Item that last arrived on a carrier pickup (shown as "from another map" by CosmeticAlert).</summary>
    public static ushort? LastCarried;
    private static ushort _swapTo;

    private static void PickupStartPrefix(CustomizationPickup __instance)
    {
        if (_swapFrom == null || __instance == null) return;
        try
        {
            if (__instance.transform.localPosition == Vector3.zero) return; // template/preview
            if (__instance.ItemID != _swapFrom.Value) return;
            __instance.itemID = _swapTo;
            LastCarried = _swapTo;
            Core.Log.Msg($"RandomCaseRule: pickup #{_swapFrom} became {CosmeticAlert.Name(_swapTo)} (#{_swapTo}).");
        }
        catch (Exception e) { Core.Log.Warning($"RandomCaseRule: swap failed: {e.Message}"); }
        finally { _swapFrom = null; }
    }

    /// <summary>Weighted pick over the allowed items, seeded by the contract so every peer picks the same.</summary>
    public static ushort PickTarget(Contract c, HashSet<ushort> allowed)
    {
        var weights = new Dictionary<ushort, double>();
        foreach (var mp in PoolCache.Current.Maps.Values)
            foreach (var e in mp.Normal.Concat(mp.Hardcore).SelectMany(l => l))
                if (allowed.Contains(e.Id)) weights[e.Id] = Math.Max(weights.GetValueOrDefault(e.Id), e.Weight);
        foreach (var e in PoolCache.Current.Seasonal.Values.SelectMany(v => v.Items))
            if (allowed.Contains(e.Id)) weights[e.Id] = Math.Max(weights.GetValueOrDefault(e.Id), e.Weight);
        var items = allowed.OrderBy(id => id).Select(id => (id, w: Math.Max(weights.GetValueOrDefault(id, 1), 0.01))).ToList();
        double total = items.Sum(x => x.w);
        double r = new System.Random(unchecked(c.Seed * 31 + c.SecondSeed) ^ 0x4C48).NextDouble() * total;
        foreach (var (id, w) in items)
        {
            if (r < w) return id;
            r -= w;
        }
        return items[^1].id;
    }

    private static bool LoadsModel(WeightedPrefab wp)
    {
        var go = wp?.Value;
        var p = go == null ? null : go.GetComponent<CustomizationPickup>() ?? go.GetComponentInChildren<CustomizationPickup>(true);
        return p != null && p.loadObjectModel;
    }

    private static string NeededHash()
    {
        uint h = 2166136261;
        foreach (var id in Party.Needed().OrderBy(i => i)) h = (h ^ id) * 16777619;
        return h.ToString("x8");
    }

    private static void SpawnPostfix() => Restore();

    private static void Restore()
    {
        if (_spawner != null)
        {
            if (_hardcore)
            {
                _spawner.globalPrefabsHardcore = _savedGlobal;
                _spawner.possiblePrefabsHardcore = _savedPossible;
            }
            else
            {
                _spawner.globalPrefabs = _savedGlobal;
                _spawner.possiblePrefabs = _savedPossible;
            }
            _spawner = null;
        }
        foreach (var (cfg, chance) in SavedSeasonal) cfg.chance = chance;
        SavedSeasonal.Clear();
    }

    private static IEnumerable<WeightedPrefab> Values(WeightedPrefabSetSO so)
    {
        var v = so?.Value;
        return v == null ? Enumerable.Empty<WeightedPrefab>() : v.ToArray();
    }

    private static readonly Dictionary<IntPtr, ushort?> IdCache = new();

    private static ushort? IdOf(WeightedPrefab wp)
    {
        var go = wp?.Value;
        if (go == null) return null;
        if (IdCache.TryGetValue(go.Pointer, out var cached)) return cached;
        var p = go.GetComponent<CustomizationPickup>() ?? go.GetComponentInChildren<CustomizationPickup>(true);
        ushort? id = p != null ? p.ItemID : null;
        IdCache[go.Pointer] = id;
        return id;
    }

    private static WeightedPrefabSetSO MakeEmptySet()
    {
        var so = ScriptableObject.CreateInstance(Il2CppType.Of<WeightedPrefabSetSO>()).Cast<WeightedPrefabSetSO>();
        so.value = new Il2CppReferenceArray<WeightedPrefab>(0);
        so.hideFlags = HideFlags.HideAndDontSave;
        return so;
    }
}
