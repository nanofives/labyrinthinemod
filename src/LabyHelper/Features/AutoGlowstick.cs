using System;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Drops a glowstick on its own every few seconds while you walk a case.
///
/// InventoryController.HandleInput, on releasing the throw key, calls PlayerNetworkSync.ThrowGlowstick(strength)
/// (strength 0 for a short press), which sends CmdThrowGlowstick so every player sees it. ThrowGlowstick does nothing
/// unless your left hand holds a glowstick (left arm AnimationState GlowStick = 52), so this never swaps what you hold.
/// Skipped while downed and when you haven't moved since the last one, so they don't pile up in one spot.
/// </summary>
internal static class AutoGlowstick
{
    private const float MinMove = 4f; // metres from the previous drop
    private static float _next;
    private static Vector3 _lastDrop = new(float.NaN, 0, 0);

    public static void OnSceneLoaded()
    {
        _next = Time.unscaledTime + 5f;
        _lastDrop = new Vector3(float.NaN, 0, 0);
    }

    public static void Update()
    {
        if (!Settings.AutoGlowstick.Value || !GameState.InCase || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + Mathf.Max(2f, Settings.AutoGlowstickSeconds.Value);
        try
        {
            Il2Cpp.PlayerNetworkSync me = null;
            foreach (var p in UnityEngine.Object.FindObjectsOfType<Il2Cpp.PlayerNetworkSync>())
                if (p != null && p.isLocalPlayer) { me = p; break; }
            if (me == null || me.isDead) return;

            var pos = me.transform.position;
            if (!float.IsNaN(_lastDrop.x) && Vector3.Distance(pos, _lastDrop) < MinMove) return;
            me.ThrowGlowstick(0f);
            _lastDrop = pos;
        }
        catch (Exception e)
        {
            Core.Log.Warning($"AutoGlowstick: {e.Message}");
            _next = Time.unscaledTime + 30f;
        }
    }
}
