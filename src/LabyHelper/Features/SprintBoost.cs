using System;
using HarmonyLib;
using PlayerControl = Il2Cpp.PlayerControl;
using PlayerControlPreset = Il2Cpp.PlayerControlPreset;

namespace LabyHelper.Features;

/// <summary>
/// Faster sprint for your own player only. PlayerControl.ApplyInput computes the move speed as
/// CurrentPlayerControlPreset.MovementSpeed x a constant, times a fixed sprint constant when inputData.sprint is set
/// (crouch uses preset.CrouchMultiplier instead), times the active MovementModifier. PlayerControl only drives the
/// local player and monsters never use it, so scaling MovementSpeed for the duration of ApplyInput while sprinting
/// changes nothing else. Movement is client-side and synced by position, so other players see it without the mod.
/// </summary>
internal static class SprintBoost
{
    private static PlayerControlPreset _preset;
    private static float _original;

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        var target = AccessTools.Method(typeof(PlayerControl), "ApplyInput");
        if (target == null)
        {
            Core.Log.Warning("SprintBoost: PlayerControl.ApplyInput not found.");
            return;
        }
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(SprintBoost), nameof(Prefix)),
            postfix: new HarmonyMethod(typeof(SprintBoost), nameof(Postfix)));
        Core.Log.Msg($"SprintBoost: hooked PlayerControl.ApplyInput (x{Settings.SprintSpeedMultiplier.Value:0.00}).");
    }

    private static void Prefix(PlayerControl __instance)
    {
        Restore(); // in case a previous call threw before its postfix
        try
        {
            float mult = Settings.SprintSpeedMultiplier.Value;
            if (__instance == null || Math.Abs(mult - 1f) < 0.001f) return;
            var input = __instance.inputData;
            var preset = __instance.CurrentPlayerControlPreset;
            if (input == null || !input.sprint || preset == null) return;
            _preset = preset;
            _original = preset.MovementSpeed;
            preset.MovementSpeed = _original * mult;
        }
        catch (Exception e)
        {
            Core.Log.Warning($"SprintBoost: {e.Message}");
            Restore();
        }
    }

    private static void Postfix() => Restore();

    private static void Restore()
    {
        if (_preset == null) return;
        try { _preset.MovementSpeed = _original; }
        catch { /* preset destroyed with the scene */ }
        _preset = null;
    }
}
