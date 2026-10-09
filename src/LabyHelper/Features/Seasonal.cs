using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2CppCharacterCustomization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppValkoGames.Labyrinthine.Misc.TimeInfo;
using EventType = Il2CppValkoGames.Labyrinthine.Misc.TimeInfo.SeasonalEventsTimeHandler.ESeasonalEventType;
using ISeasonalEvent = Il2CppValkoGames.Labyrinthine.Misc.TimeInfo.SeasonalEventsTimeHandler.ISeasonalEvent;
using SeasonalConfig = Il2CppValkoGames.Labyrinthine.Misc.TimeInfo.SeasonalEventsTimeHandler.SeasonalConfig;

namespace LabyHelper.Features;

/// <summary>
/// RndCustomizationSpawner.Spawn asks SeasonalEventsTimeHandler.TryGetActiveSeasonalEvent (Steam server date)
/// which event is live, then rolls SeasonalItemsConfig.chance to swap the cosmetic for one of that event's prefabs.
/// For the duration of Spawn only, we replace the static event table with a single all-year event of our choice,
/// then restore it. Contract rarity and the lobby timer, which read the same table, are untouched.
/// Spawning is host-side, so this only has effect when you host.
/// "Random" picks the event from the contract seed, so the case board can predict it.
/// </summary>
internal static class Seasonal
{
    private static Il2CppReferenceArray<ISeasonalEvent> _savedConfigs;
    private static readonly List<(SeasonalItemsConfig cfg, float chance)> SavedChances = new();


    public static void Apply(HarmonyLib.Harmony harmony)
    {
        var spawn = AccessTools.Method(typeof(RndCustomizationSpawner), nameof(RndCustomizationSpawner.Spawn));
        harmony.Patch(spawn,
            prefix: new HarmonyMethod(typeof(Seasonal), nameof(SpawnPrefix)),
            postfix: new HarmonyMethod(typeof(Seasonal), nameof(SpawnPostfix)));
        Core.Log.Msg($"Seasonal: hooked RndCustomizationSpawner.Spawn (mode: {Settings.SeasonalOverride.Value}).");
    }

    /// <summary>Event to force for a contract, or null for "leave the game alone". Deterministic per seed.</summary>
    public static string ChooseEvent(int contractSeed, List<string> withPrefabs)
    {
        string mode = Rules.Active ? (Rules.Seasonal?.Trim() ?? "Off") : "Off";
        if (mode.Equals("Off", StringComparison.OrdinalIgnoreCase) || withPrefabs.Count == 0) return null;

        if (mode.Equals("Random", StringComparison.OrdinalIgnoreCase))
        {
            string live = LiveEvent();
            var pool = withPrefabs.Where(e => e != live).OrderBy(e => e).ToList();
            if (pool.Count == 0) pool = withPrefabs.OrderBy(e => e).ToList();
            return pool[(int)((uint)contractSeed % (uint)pool.Count)];
        }
        var match = withPrefabs.FirstOrDefault(e => e.Equals(mode, StringComparison.OrdinalIgnoreCase));
        if (match == null)
            Core.Log.Warning($"Seasonal: '{mode}' has no prefabs. Valid: Off, Random, {string.Join(", ", withPrefabs)}");
        return match;
    }

    private static void SpawnPrefix(RndCustomizationSpawner __instance)
    {
        var contract = Il2Cpp.GameValues.instance?.RandomMazeContract;
        if (contract != null)
        {
            if (PoolCache.LearnSpawner(contract.MazeType.ToString(), __instance)) PoolCache.LearnFromScene();
            LogSeeds(contract);
        }

        try
        {
            Rules.Refresh(force: true);
            var manager = RndCustomizationItemsManager.Instance;
            if (manager == null || contract == null || !Rules.Active) return;
            var available = manager.seasonalItems
                .Where(c => c != null && c.prefabs != null && c.prefabs.Length > 0)
                .ToList();
            string chosenName = ChooseEvent(contract.Seed, available.Select(c => c.seasonalEventType.ToString()).Distinct().ToList());
            if (chosenName == null || !Enum.TryParse(chosenName, out EventType chosen)) return;

            var forced = new SeasonalConfig { startMonth = 1, startDay = 1, endMonth = 12, endDay = 31 };
            forced.EventType = chosen;

            _savedConfigs = SeasonalEventsTimeHandler.seasonalConfigs;
            var table = new Il2CppReferenceArray<ISeasonalEvent>(1);
            table[0] = forced.Cast<ISeasonalEvent>();
            SeasonalEventsTimeHandler.seasonalConfigs = table;

            float chance = Rules.SeasonalChance;
            if (chance >= 0f)
            {
                foreach (var cfg in available.Where(c => c.seasonalEventType == chosen))
                {
                    SavedChances.Add((cfg, cfg.chance));
                    cfg.chance = Math.Min(chance, 1f);
                }
            }
            Core.Log.Msg($"Seasonal: spawning with event {chosen}" + (chance >= 0f ? $", chance {chance:0.00}" : ""));
        }
        catch (Exception e)
        {
            Core.Log.Error($"Seasonal: prefix failed, game logic left as is: {e}");
            Restore();
        }
    }

    private static void SpawnPostfix() => Restore();

    /// <summary>Logs how many maze-generation retries shifted the seeds (each failed GenerateNewMaze adds 0x4A3/0x18C1).</summary>
    private static void LogSeeds(Il2CppRandomGeneration.Contracts.Contract contract)
    {
        try
        {
            var mg = Il2Cpp.MazeGenerator.instance;
            if (mg == null) return;
            int retries = unchecked(mg.seed - contract.Seed) / 0x4A3;
            Core.Log.Msg($"Spawn: contract seed {contract.Seed}/{contract.SecondSeed}, maze seed {mg.seed}/{mg.secondSeed} ({retries} maze retries)");
        }
        catch (Exception e) { Core.Log.Warning($"Spawn: seed log failed: {e.Message}"); }
    }

    private static void Restore()
    {
        if (_savedConfigs != null)
        {
            SeasonalEventsTimeHandler.seasonalConfigs = _savedConfigs;
            _savedConfigs = null;
        }
        foreach (var (cfg, chance) in SavedChances) cfg.chance = chance;
        SavedChances.Clear();
    }

    private static string LiveEvent()
    {
        try
        {
            return SeasonalEventsTimeHandler.TryGetActiveSeasonalEvent(out var ev, out _, out _) ? ev.EventType.ToString() : null;
        }
        catch { return null; }
    }

}
