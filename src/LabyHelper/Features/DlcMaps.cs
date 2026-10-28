using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppRandomGeneration.Contracts;

namespace LabyHelper.Features;

/// <summary>
/// Host rule: more Egypt maps on the case board when everybody in the lobby owns the Egyptian DLC.
/// MazeTypesConfigSO.GetRandomByLevel picks each file's map: the level range's maps, plus (when the host owns the DLC,
/// via HandleDLCMapsSelectionLogic) the DLC_Egyptian_mazeTypes ones, each new DLC map forced once, then a weighted
/// pick. For that call the DLC maps' weights are multiplied, and restored right after. Only when every lobby member
/// reports the DLC (Party "lh_dlc", so players without the mod count as "no"): the game refuses to start a DLC map
/// otherwise. Ownership is the game's own check; nothing is unlocked.
/// </summary>
internal static class DlcMaps
{
    private const float Boost = 4f;
    private static readonly List<(MazeTypeWeighted w, float weight)> Saved = new();

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        try
        {
            harmony.Patch(AccessTools.Method(typeof(MazeTypesConfigSO), nameof(MazeTypesConfigSO.GetRandomByLevel)),
                prefix: new HarmonyMethod(typeof(DlcMaps), nameof(Prefix)),
                postfix: new HarmonyMethod(typeof(DlcMaps), nameof(Postfix)));
            Core.Log.Msg($"DlcMaps: hooked MazeTypesConfigSO.GetRandomByLevel (enabled: {Settings.MoreDlcMaps.Value}).");
        }
        catch (Exception e) { Core.Log.Msg($"DlcMaps: not available on this game build ({e.GetType().Name})."); }
    }

    private static void Prefix(MazeTypesConfigSO __instance)
    {
        try
        {
            if (!Settings.MoreDlcMaps.Value || __instance == null || !Party.EveryoneHasEgyptDlc()) return;
            var maps = __instance.DLC_Egyptian_mazeTypes?.Value;
            if (maps == null) return;
            foreach (var w in maps)
            {
                if (w == null) continue;
                Saved.Add((w, w.weight));
                w.weight *= Boost;
            }
        }
        catch (Exception e) { Core.Log.Warning($"DlcMaps: {e.Message}"); Restore(); }
    }

    private static void Postfix() => Restore();

    private static void Restore()
    {
        foreach (var (w, weight) in Saved) w.weight = weight;
        Saved.Clear();
    }
}
