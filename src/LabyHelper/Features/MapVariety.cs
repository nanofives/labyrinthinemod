using System;
using System.Globalization;
using HarmonyLib;

namespace LabyHelper.Features;

/// <summary>
/// More variety in random case files.
///  - Rare tiles: every piece getter (floor, straight, turn, threeway, end cap, wall, safezone) rolls
///    UnityEngine.Random.Range(0, 100) and uses the rare variant when the roll is &lt;= rareGenerationChance (5 by
///    default). The roll happens either way, so a higher chance changes which variant goes where, not the layout.
///  - Submazes: maps with possibleSubmazes mix in sections of another maze type (lit and placed by the game
///    itself); we multiply their chanceToSpawn.
/// Each peer generates the maze locally from the contract seed, so everyone must apply the same values: the host
/// publishes its values in the Steam lobby (Party) and clients use them; if anyone lacks the mod, nothing changes.
/// Applied in a prefix to MazeGenerator.GenerateMap; the scene's MazeGenerator is rebuilt per map, so no restore.
/// </summary>
internal static class MapVariety
{
    public static void Apply(HarmonyLib.Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(Il2Cpp.MazeGenerator), "GenerateMap"),
            prefix: new HarmonyMethod(typeof(MapVariety), nameof(GenerateMapPrefix)));
        Core.Log.Msg($"MapVariety: hooked GenerateMap (rare {Settings.MapRareChance.Value:0}%, submaze x{Settings.MapSubmazeMultiplier.Value:0.0}).");
    }

    private static void GenerateMapPrefix(Il2Cpp.MazeGenerator __instance)
    {
        try
        {
            var c = Il2Cpp.GameValues.instance?.RandomMazeContract;
            if (__instance == null || c == null || c.IsCustom) return;
            Rules.Refresh(force: true);
            if (!Rules.Active)
            {
                Core.Log.Msg($"MapVariety: not applied ({Rules.WhyInactive}).");
                return;
            }
            if (!Rules.Variety) return;
            float rare = Rules.RareChance;
            float mult = Rules.SubmazeMult;

            float before = __instance.rareGenerationChance;
            __instance.rareGenerationChance = Math.Clamp(rare, 0f, 100f);

            int subs = 0;
            var submazes = __instance.possibleSubmazes;
            if (submazes != null)
            {
                foreach (var s in submazes)
                {
                    if (s == null) continue;
                    float cap = s.chanceToSpawn <= 1f ? 1f : 100f; // unit unknown: 0-1 or percent
                    s.chanceToSpawn = Math.Min(s.chanceToSpawn * mult, cap);
                    subs++;
                }
            }
            Core.Log.Msg($"MapVariety: {c.MazeType} rare tiles {before:0}% -> {__instance.rareGenerationChance:0}%, " +
                         $"{subs} submaze type(s) x{mult:0.0}.");
        }
        catch (Exception e) { Core.Log.Warning($"MapVariety: {e.Message}"); }
    }
}
