using System;
using HarmonyLib;
using Il2CppValkoGames.Labyrinthine.Cases.Inventory;
using Il2CppValkoGames.Labyrinthine.Players;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Get up on your own after a monster downs you, using the game's own self-revive flow.
///
/// Normally self-revive needs the selfReviveItem in your inventory:
///  - Death.AvailableInventoryItemsChangedCallback sets the isSelfReviveAvailable SyncVar via
///    CmdSetSelfReviveAvailable(inventory.ContainsItem(item)), which shows the "hold to get up" prompt.
///  - Death.HandleSelfReviveInput, once the hold bar fills, calls RndPlayerInventory.RemoveItem(item) and only
///    sends CmdReviveSelf if that succeeded. The server-side CmdReviveSelf does no item check.
/// We keep the availability on and let RemoveItem "succeed" without consuming anything while that input runs.
/// On by default; on/off only from F1 (by request). No hotkey.
/// </summary>
internal static class SelfRevive
{
    private static bool _inSelfReviveInput;
    private static bool _forced;
    private static float _nextCheck;

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(Death), nameof(Death.AvailableInventoryItemsChangedCallback)),
            prefix: new HarmonyMethod(typeof(SelfRevive), nameof(AvailabilityPrefix)));
        harmony.Patch(AccessTools.Method(typeof(Death), "HandleSelfReviveInput"),
            prefix: new HarmonyMethod(typeof(SelfRevive), nameof(InputPrefix)),
            postfix: new HarmonyMethod(typeof(SelfRevive), nameof(InputPostfix)));
        harmony.Patch(AccessTools.Method(typeof(RndPlayerInventory), nameof(RndPlayerInventory.RemoveItem)),
            prefix: new HarmonyMethod(typeof(SelfRevive), nameof(RemoveItemPrefix)));
        Core.Log.Msg($"SelfRevive: hooked (enabled: {Settings.SelfReviveAlways.Value}).");
    }

    // Keep the game from switching availability back off when you don't carry the item.
    private static bool AvailabilityPrefix(Death __instance)
    {
        if (!Settings.SelfReviveAlways.Value || __instance == null || !__instance.isOwned) return true;
        if (!__instance.IsSelfReviveAvailable) __instance.CmdSetSelfReviveAvailable(true);
        return false;
    }

    private static void InputPrefix() => _inSelfReviveInput = true;
    private static void InputPostfix() => _inSelfReviveInput = false;

    // Only inside HandleSelfReviveInput: report success without taking the item.
    private static bool RemoveItemPrefix(ref bool __result)
    {
        if (!_inSelfReviveInput || !Settings.SelfReviveAlways.Value) return true;
        __result = true;
        return false;
    }

    public static void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 1f;
        var death = LocalDeath();
        if (death == null) return;

        try
        {
            if (Settings.SelfReviveAlways.Value)
            {
                if (!death.IsSelfReviveAvailable) death.CmdSetSelfReviveAvailable(true);
                _forced = true;
            }
            else if (_forced)
            {
                _forced = false;
                death.AvailableInventoryItemsChangedCallback(); // back to the real inventory state
            }
        }
        catch (Exception e)
        {
            Core.Log.Warning($"SelfRevive: {e.Message}");
        }
    }

    private static bool IsDowned(Death death)
    {
        try { return death.playerNetworkSync != null && death.playerNetworkSync.IsDead; }
        catch { return false; }
    }

    private static Death LocalDeath()
    {
        try
        {
            foreach (var d in UnityEngine.Object.FindObjectsOfType<Death>())
                if (d != null && d.isOwned) return d;
        }
        catch { /* not in a match */ }
        return null;
    }
}
