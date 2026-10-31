using System;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Puts the flashlight in your right hand once when a case starts, which is what switches it on (the light follows
/// the right arm state). Same call the game makes when you cycle the right hand:
/// InventoryController.GetNextRightArmAnimationState returns Flashlight (77) with customItemID 0 when
/// GameValues.hasFlashlight, so InventoryController.SetRightArmState(Flashlight, 0). Cases without a flashlight
/// (GameValues.hasFlashlight false) are left alone, and so is a hand that already holds it.
/// </summary>
internal static class AutoFlashlight
{
    private const Il2Cpp.AnimationState Flashlight = (Il2Cpp.AnimationState)77;
    private static bool _done;
    private static float _at, _giveUpAt;
    private static float _keepUntil; // after getting up: put it back if the game empties the hand again this soon

    /// <summary>After getting back up (Revived): the game emptied both hands when you went down.</summary>
    public static void Rearm()
    {
        _done = false;
        _at = Time.unscaledTime + 1f;
        _giveUpAt = Time.unscaledTime + 15f;
        _keepUntil = Time.unscaledTime + 5f;
    }

    public static void OnSceneLoaded()
    {
        _done = false;
        _at = Time.unscaledTime + 3f;
        _giveUpAt = Time.unscaledTime + 30f;
        _keepUntil = 0f;
    }

    private static bool HandEmpty()
    {
        try
        {
            var anim = Il2Cpp.InventoryController.Instance?.anim;
            return anim != null && Il2Cpp.GameValues.instance != null && Il2Cpp.GameValues.instance.hasFlashlight &&
                   anim.RightArmState != Flashlight;
        }
        catch { return false; }
    }

    public static void Update()
    {
        if (_done && Time.unscaledTime < _keepUntil && Time.unscaledTime >= _at) _done = !HandEmpty();
        if (_done || !Settings.AutoFlashlight.Value || !GameState.InCase || Time.unscaledTime < _at) return;
        _at = Time.unscaledTime + 1f; // retried every second until the inventory is ready
        if (Time.unscaledTime > _giveUpAt) { _done = true; return; }
        try
        {
            var values = Il2Cpp.GameValues.instance;
            var inv = Il2Cpp.InventoryController.Instance;
            var anim = inv?.anim;
            if (values == null || inv == null || anim == null) return;
            _done = true;
            if (!values.hasFlashlight || anim.RightArmState == Flashlight) return;
            var was = anim.RightArmState;
            inv.SetRightArmState(Flashlight, 0, true);
            Core.Log.Msg($"AutoFlashlight: flashlight in the right hand (was {was}).");
        }
        catch (Exception e)
        {
            Core.Log.Warning($"AutoFlashlight: {e.Message}");
            _done = true;
        }
    }
}
