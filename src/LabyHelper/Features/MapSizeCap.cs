using System;
using HarmonyLib;
using Il2CppRandomGeneration.Contracts;

namespace LabyHelper.Features;

/// <summary>
/// Host rule: random case files are at most Medium. Contract.GenerateContract asks FindMapSizeEnum for the size
/// (a pick among the sizes the player's level allows, VerySmall always among them); a postfix folds the big ones
/// down: Large -> VerySmall, VeryLarge -> Small, Giant -> Medium, so the board keeps a mix. Like BoardSeals, only the
/// host generates contracts (clients receive them), so no agreement is needed. Custom cases keep their chosen size.
/// </summary>
internal static class MapSizeCap
{
    public static void Apply(HarmonyLib.Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(Contract), "FindMapSizeEnum"),
            postfix: new HarmonyMethod(typeof(MapSizeCap), nameof(Postfix)));
        Core.Log.Msg($"MapSizeCap: hooked Contract.FindMapSizeEnum (enabled: {Settings.SmallMapsOnly.Value}).");
    }

    private static void Postfix(Contract __instance, ref MapSize __result)
    {
        try
        {
            if (!Settings.SmallMapsOnly.Value || __instance == null || __instance.IsCustom || __result <= MapSize.Medium) return;
            var original = __result;
            __result = (MapSize)(((int)__result - (int)MapSize.Large) % 3); // 3 -> 0, 4 -> 1, 5 -> 2
            Core.Log.Msg($"MapSizeCap: {original} -> {__result}.");
        }
        catch (Exception e) { Core.Log.Warning($"MapSizeCap: {e.Message}"); }
    }
}
