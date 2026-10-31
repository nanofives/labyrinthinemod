using System;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Notices when the local player gets back up (self-revive, a teammate's revive or a respawn) and tells AutoFlashlight
/// and AutoGlowstick to equip again. Death.Die calls InventoryController.RemoveInHandItems (both hands emptied) and
/// Death.Respawn never puts them back, so after getting up the flashlight was off and the auto glowsticks stopped
/// (PlayerNetworkSync.ThrowGlowstick needs the glowstick in the left hand). Polls PlayerNetworkSync.isDead.
/// </summary>
internal static class Revived
{
    private static bool _wasDead;
    private static float _next;

    public static void OnSceneLoaded() => _wasDead = false;

    public static void Update()
    {
        if (!GameState.InCase || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.25f;
        try
        {
            Il2Cpp.PlayerNetworkSync me = null;
            foreach (var p in UnityEngine.Object.FindObjectsOfType<Il2Cpp.PlayerNetworkSync>())
                if (p != null && p.isLocalPlayer) { me = p; break; }
            if (me == null) return;
            bool dead = me.isDead;
            if (_wasDead && !dead)
            {
                Core.Log.Msg("Revived: back up, equipping the flashlight and glowstick again.");
                AutoFlashlight.Rearm();
                AutoGlowstick.Rearm();
            }
            _wasDead = dead;
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Revived: {e.Message}");
            _next = Time.unscaledTime + 10f;
        }
    }
}
