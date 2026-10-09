using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2CppCharacterCustomization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using WeightedPrefab = Il2Cpp.WeightedPrefab;

namespace LabyHelper.Features;

/// <summary>
/// Host-side bias of RndCustomizationSpawner.Spawn towards cosmetics you don't own:
///  - PreferNew: adds your unlocked item IDs to the exclusion list Spawn receives. Spawn re-rolls up to 50 times
///    while the pick is excluded and then walks the list for the first allowed item, so you get a new one
///    whenever the pool still has any.
///  - AddAllSeasonal: appends every seasonal event's prefabs (already loaded via RndCustomizationItemsManager)
///    to the spawner's map pool for this one call, then restores it.
/// Only prefabs present in the session are used, so clients spawn the same networked object.
/// </summary>
internal static class PoolBoost
{
    private static RndCustomizationSpawner _patched;
    private static Il2CppReferenceArray<WeightedPrefab> _savedPossible;

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        var spawn = AccessTools.Method(typeof(RndCustomizationSpawner), nameof(RndCustomizationSpawner.Spawn));
        harmony.Patch(spawn,
            prefix: new HarmonyMethod(typeof(PoolBoost), nameof(SpawnPrefix)),
            postfix: new HarmonyMethod(typeof(PoolBoost), nameof(SpawnPostfix)));
        Core.Log.Msg($"PoolBoost: hooked Spawn (prefer new: {Settings.PoolPreferNew.Value}, all seasonal: {Settings.PoolAddAllSeasonal.Value}).");
    }

    private static void SpawnPrefix(RndCustomizationSpawner __instance,
        Il2CppSystem.Collections.Generic.List<ushort> excludedItemIDs)
    {
        try
        {
            var contract = Il2Cpp.GameValues.instance?.RandomMazeContract;
            bool hardcore = contract != null && contract.ContractType == Il2CppRandomGeneration.Contracts.ContractType.Hardcore;

            Rules.Refresh(force: true);
            if (!Rules.Active) return;
            // Random cases under the "only new" rule are handled by RandomCaseRule.
            if (RandomCaseRule.Applies(contract)) return;

            // Every peer spawns locally: exclude what the whole party owns (same on every peer), not your own items.
            if (contract != null && excludedItemIDs != null && Rules.PreferNew)
            {
                int added = 0;
                foreach (var id in PartyOwnedByAll())
                {
                    if (!excludedItemIDs.Contains(id)) { excludedItemIDs.Add(id); added++; }
                }
                if (added > 0) Core.Log.Msg($"PoolBoost: excluded {added} items the whole party owns.");
            }

            if (Rules.AllSeasonal && !hardcore && __instance != null)
            {
                var extra = AllSeasonalPrefabs();
                if (extra.Count > 0)
                {
                    _patched = __instance;
                    _savedPossible = __instance.possiblePrefabs;
                    var merged = (_savedPossible?.ToArray() ?? Array.Empty<WeightedPrefab>()).Concat(extra).ToArray();
                    __instance.possiblePrefabs = new Il2CppReferenceArray<WeightedPrefab>(merged);
                    Core.Log.Msg($"PoolBoost: map pool {_savedPossible?.Length ?? 0} -> {merged.Length} with all seasonal items.");
                }
            }
        }
        catch (Exception e)
        {
            Core.Log.Error($"PoolBoost: prefix failed, game logic left as is: {e}");
            Restore();
        }
    }

    private static void SpawnPostfix() => Restore();

    private static void Restore()
    {
        if (_patched != null)
        {
            _patched.possiblePrefabs = _savedPossible;
            _patched = null;
            _savedPossible = null;
        }
    }

    /// <summary>Items every party member owns (with Party data); just yours when solo.</summary>
    public static IEnumerable<ushort> PartyOwnedByAll()
    {
        var needed = Party.Needed();
        return Catalog.AllIds().Where(id => !needed.Contains(id));
    }

    public static IEnumerable<ushort> OwnedIds()
    {
        // UnlockedItems holds ACTk ObscuredUShorts; asking the save per item avoids decoding them ourselves.
        var save = CustomizationManager.Instance?.CustomizationSave;
        var collection = CustomizationManager.Instance?.Collection;
        if (save == null || collection == null) yield break;
        foreach (var item in collection.collection)
            if (item != null && save.IsItemUnlocked(item.ItemID)) yield return item.ItemID;
    }

    private static List<WeightedPrefab> AllSeasonalPrefabs()
    {
        var list = new List<WeightedPrefab>();
        var manager = RndCustomizationItemsManager.Instance;
        if (manager?.seasonalItems == null) return list;
        foreach (var cfg in manager.seasonalItems)
        {
            if (cfg?.prefabs == null) continue;
            foreach (var wp in cfg.prefabs)
                if (wp != null) list.Add(wp);
        }
        return list;
    }
}
