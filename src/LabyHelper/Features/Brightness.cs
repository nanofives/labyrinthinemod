using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace LabyHelper.Features;

/// <summary>
/// The in-game slider (LabyrinthineSettingsManager.SetBrightness) only writes ColorAdjustments.postExposure
/// on the volume profiles it knows about, and the menu caps it. We instead add on top of the *blended*
/// result: a postfix on VolumeManager.Update(stack, ...) bumps the stack's postExposure and gamma.
/// That covers every global and local volume in every map, and the game's own slider keeps working.
/// </summary>
internal static class Brightness
{
    private const float ExposureStep = 0.25f;
    private const float GammaStep = 0.05f;

    // Guards against stacking our offset twice if the stack was not re-blended since our last write.
    private static readonly Dictionary<IntPtr, float> LastExposureWritten = new();
    private static readonly Dictionary<IntPtr, float> LastGammaWritten = new();

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        var target = AccessTools.Method(typeof(VolumeManager), nameof(VolumeManager.Update),
            new[] { typeof(VolumeStack), typeof(Transform), typeof(LayerMask) });
        if (target == null)
        {
            Core.Log.Error("Brightness: VolumeManager.Update(VolumeStack, Transform, LayerMask) not found.");
            return;
        }
        harmony.Patch(target, postfix: new HarmonyMethod(typeof(Brightness), nameof(UpdatePostfix)));
        Core.Log.Msg("Brightness: hooked VolumeManager.Update.");
    }

    private static void UpdatePostfix(VolumeStack stack)
    {
        if (!Settings.BrightnessEnabled.Value || stack == null) return;
        if (!GameState.InCase) return; // lobby and menus stay at the game's exposure, for readability
        try
        {
            ApplyExposure(stack);
            ApplyGamma(stack);
        }
        catch (Exception e)
        {
            Settings.BrightnessEnabled.Value = false;
            Core.Log.Error($"Brightness disabled after error: {e}");
        }
    }

    private static void ApplyExposure(VolumeStack stack)
    {
        var comp = stack.GetComponent(Il2CppType.Of<ColorAdjustments>());
        var ca = comp?.TryCast<ColorAdjustments>();
        if (ca == null) return;

        float extra = Settings.ExtraExposureEV.Value;
        float current = ca.postExposure.value;
        float baseline = LastExposureWritten.TryGetValue(ca.Pointer, out var written) && Mathf.Approximately(current, written)
            ? current - _lastExposureExtra
            : current;
        float result = baseline + extra;
        ca.postExposure.value = result;
        LastExposureWritten[ca.Pointer] = result;
        _lastExposureExtra = extra;
    }

    private static void ApplyGamma(VolumeStack stack)
    {
        var comp = stack.GetComponent(Il2CppType.Of<LiftGammaGain>());
        var lgg = comp?.TryCast<LiftGammaGain>();
        if (lgg == null) return;

        float extra = Settings.GammaBoost.Value;
        Vector4 g = lgg.gamma.value;
        float baseline = LastGammaWritten.TryGetValue(lgg.Pointer, out var written) && Mathf.Approximately(g.w, written)
            ? g.w - _lastGammaExtra
            : g.w;
        g.w = baseline + extra;
        lgg.gamma.value = g;
        LastGammaWritten[lgg.Pointer] = g.w;
        _lastGammaExtra = extra;
    }

    private static float _lastExposureExtra;
    private static float _lastGammaExtra;

    public static void HandleHotkeys()
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.F6))
        {
            Settings.BrightnessEnabled.Value = !Settings.BrightnessEnabled.Value;
            Changed();
        }
        else if (Input.GetKeyDown(KeyCode.F7))
        {
            if (shift) Settings.GammaBoost.Value = Mathf.Max(-0.5f, Settings.GammaBoost.Value - GammaStep);
            else Settings.ExtraExposureEV.Value = Mathf.Max(-3f, Settings.ExtraExposureEV.Value - ExposureStep);
            Changed();
        }
        else if (Input.GetKeyDown(KeyCode.F8))
        {
            if (shift) Settings.GammaBoost.Value = Mathf.Min(1f, Settings.GammaBoost.Value + GammaStep);
            else Settings.ExtraExposureEV.Value = Mathf.Min(5f, Settings.ExtraExposureEV.Value + ExposureStep);
            Changed();
        }
    }

    private static void Changed()
    {
        Settings.Save();
        Toast.Show(Settings.BrightnessEnabled.Value
            ? $"{Loc.T("Brillo extra ON  |  exposición", "Extra brightness ON  |  exposure")} +{Settings.ExtraExposureEV.Value:0.00} EV  |  gamma +{Settings.GammaBoost.Value:0.00}"
            : Loc.T("Brillo extra OFF", "Extra brightness OFF"));
    }
}
