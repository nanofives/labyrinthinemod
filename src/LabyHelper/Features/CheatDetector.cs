using HarmonyLib;

namespace LabyHelper.Features;

/// <summary>
/// ACTk-backed CheatingDetector flips an ObscuredBool on speedhack or obscured-value tampering.
/// Our hooks don't trip it, but Cheat Engine sessions can; keep the getter honest-to-us.
/// </summary>
internal static class CheatDetector
{
    public static void Apply(HarmonyLib.Harmony harmony)
    {
        var getter = AccessTools.PropertyGetter(typeof(Il2CppValkoGames.Labyrinthine.Misc.CheatingDetector),
            "CheatingDetected");
        if (getter == null)
        {
            Core.Log.Warning("CheatDetector: getter not found, skipping.");
            return;
        }
        harmony.Patch(getter, prefix: new HarmonyMethod(typeof(CheatDetector), nameof(Prefix)));
        Core.Log.Msg($"CheatDetector: hooked (ignored: {Settings.DisableCheatDetector.Value}).");
    }

    // Always patched so the option can be switched from the options menu without restarting.
    private static bool Prefix(ref bool __result)
    {
        if (!Settings.DisableCheatDetector.Value) return true;
        __result = false;
        return false;
    }
}
