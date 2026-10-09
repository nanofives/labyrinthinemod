using System;
using System.Collections.Generic;
using Il2CppValkoGames.Labyrinthine.Monsters;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Scales min/max distance of every AudioSource under the Pigman (and Hunter Pigman): footsteps, MonsterAudio, state
/// loops. Below 1 it is quieter (heard only closer; 0.4 = 2.5x quieter, the default by request), above 1 louder.
/// Volumes are left alone because MonsterAudio fades them itself.
/// The chase banner for every monster lives in MonsterAlert.
/// Client-side only: works whether or not you host.
/// </summary>
internal static class Pigman
{
    private const float ScanInterval = 1f;
    private static float _nextScan;
    private static readonly HashSet<IntPtr> Boosted = new();
    private static readonly List<PigmanMonsterAnimationHandler> Known = new();

    public static void OnSceneLoaded()
    {
        Boosted.Clear();
        Known.Clear();
    }

    public static void Update()
    {
        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + ScanInterval;
            Scan();
        }
    }

    private static void Scan()
    {
        try
        {
            Known.Clear();
            foreach (var handler in UnityEngine.Object.FindObjectsOfType<PigmanMonsterAnimationHandler>())
            {
                if (handler == null) continue;
                Known.Add(handler);
                if (Math.Abs(Settings.PigmanAudioMultiplier.Value - 1f) > 0.01f) BoostAudio(handler);
            }
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Pigman: scan failed: {e.Message}");
        }
    }

    private static void BoostAudio(PigmanMonsterAnimationHandler handler)
    {
        float mult = Settings.PigmanAudioMultiplier.Value;
        var root = handler.transform.root;
        int count = 0;
        foreach (var src in root.GetComponentsInChildren<AudioSource>(true))
        {
            if (src == null || !Boosted.Add(src.Pointer)) continue;
            src.minDistance *= mult;
            src.maxDistance *= mult;
            count++;
        }
        if (count > 0) Core.Log.Msg($"Pigman: sound range x{mult} on {count} audio sources of '{root.name}'.");
    }
}
