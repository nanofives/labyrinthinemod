using System;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Drops a glowstick on its own every few seconds while you walk a case, and puts the glowstick in your left hand
/// once when the case starts (InventoryController.SetLeftArmState(GlowStick, 0): customItemID 0 is the plain
/// glowstick, as in InventoryController.GetNextLeftArmAnimationState; the current state is AnimationHandler.LeftArmState).
///
/// InventoryController.HandleInput, on releasing the throw key, calls PlayerNetworkSync.ThrowGlowstick(strength)
/// (strength 0 for a short press), which sends CmdThrowGlowstick so every player sees it. ThrowGlowstick does nothing
/// unless your left hand holds a glowstick (left arm AnimationState GlowStick = 52); after the initial equip we never
/// swap what you hold, so picking the compass or radio stops the drops until you take the glowstick back.
/// Skipped while downed and when you haven't moved since the last one, so they don't pile up in one spot.
/// </summary>
internal static class AutoGlowstick
{
    private const float MinMove = 3f; // metres from the previous drop
    private static float _next;
    private static Vector3 _lastDrop = new(float.NaN, 0, 0);
    private static bool _equipped;
    private static float _equipAt;

    public static void OnSceneLoaded()
    {
        _next = Time.unscaledTime + 5f;
        _equipped = false;
        _equipAt = Time.unscaledTime + 3f;
        _lastDrop = new Vector3(float.NaN, 0, 0);
    }

    public static void Update()
    {
        if (!Settings.AutoGlowstick.Value || !GameState.InCase) return;
        if (!_equipped && Time.unscaledTime >= _equipAt) EquipGlowstick();
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + Mathf.Max(1f, Settings.AutoGlowstickSeconds.Value);
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

    /// <summary>Once per case: glowstick in the left hand (retried every second until the inventory is ready).</summary>
    private static void EquipGlowstick()
    {
        _equipAt = Time.unscaledTime + 1f;
        try
        {
            var inv = Il2Cpp.InventoryController.Instance;
            var anim = inv?.anim;
            if (inv == null || anim == null) return;
            if (anim.LeftArmState != Il2Cpp.AnimationState.GlowStick)
            {
                inv.SetLeftArmState(Il2Cpp.AnimationState.GlowStick, 0, true);
                Core.Log.Msg($"AutoGlowstick: glowstick in the left hand (was {anim.LeftArmState}).");
            }
            _equipped = true;
        }
        catch (Exception e)
        {
            Core.Log.Warning($"AutoGlowstick: {e.Message}");
            _equipped = true;
        }
    }
}
