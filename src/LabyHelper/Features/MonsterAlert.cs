using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppValkoGames.Labyrinthine.Monsters;
using UnityEngine;
using AnimState = Il2Cpp.AnimationState;

namespace LabyHelper.Features;

/// <summary>
/// Red banner naming every monster that is chasing right now ("PIGMAN is chasing"), for all monster types.
/// A monster counts as chasing when MonsterNetworkSync.IsChasingPlayer is set or its synced animation state is
/// spot / run / charge (the state is what clients reliably receive). The monster's type comes from its
/// AIController.MonsterType (present on every peer, disabled on clients); the GameObject name is the fallback.
/// No distance, by request. Client-side only.
/// </summary>
internal static class MonsterAlert
{
    private const float Every = 0.4f;
    private static float _next;
    private static readonly Dictionary<IntPtr, string> Names = new();

    public static void OnSceneLoaded() => Names.Clear();

    public static void Update()
    {
        if (!Settings.MonsterChaseAlert.Value || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + Every;
        try
        {
            var chasing = new List<string>();
            foreach (var sync in UnityEngine.Object.FindObjectsOfType<MonsterNetworkSync>())
            {
                if (sync == null || !sync.IsEnabled) continue;
                var state = sync.CurrentAnimationState;
                bool charging = state == AnimState.MonsterCharge;
                if (!(sync.IsChasingPlayer || charging || state == AnimState.MonsterRun || state == AnimState.MonsterSpot)) continue;
                string name = NameOf(sync);
                chasing.Add(charging ? Loc.T($"{name} (cargando)", $"{name} (charging)") : name);
            }
            if (chasing.Count == 0) return;

            var list = chasing.GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Count()}x {g.Key}" : g.Key).ToList();
            string who = string.Join(", ", list);
            bool plural = chasing.Count > 1;
            Toast.Show(plural ? Loc.T($"Te persiguen: {who}", $"Chasing: {who}")
                              : Loc.T($"{who} te persigue", $"{who} is chasing"), Every + 0.4f, Toast.Slot.Alert);
        }
        catch (Exception e)
        {
            Core.Log.Warning($"MonsterAlert: {e.Message}");
            _next = Time.unscaledTime + 10f;
        }
    }

    private static string NameOf(MonsterNetworkSync sync)
    {
        if (Names.TryGetValue(sync.Pointer, out var cached)) return cached;
        string name = null;
        try
        {
            var ai = sync.GetComponent<AIController>() ?? sync.transform.root.GetComponentInChildren<AIController>(true);
            if (ai != null && ai.MonsterType != MonsterType.None) name = Friendly(ai.MonsterType);
        }
        catch { /* fall back to the object name */ }
        name ??= sync.transform.root.name.Replace("(Clone)", "").Trim();
        name = name.ToUpperInvariant();
        Names[sync.Pointer] = name;
        return name;
    }

    /// <summary>MonsterType enum -> display name (the game has no localised monster names).</summary>
    private static string Friendly(MonsterType t) => t switch
    {
        MonsterType.Crypt_Zombie => "Crypt Zombie",
        MonsterType.SwampDog => "Swamp Dog",
        MonsterType.BigSwampDog => "Big Swamp Dog",
        MonsterType.BlackBride => "Black Bride",
        MonsterType.TrenchStalker => "Trench Stalker",
        MonsterType.Witch_Pale => "Pale Witch",
        MonsterType.Clubfoot_Frost => "Frost Clubfoot",
        MonsterType.Smiley_Joker => "Joker Smiley",
        MonsterType.Wickerman_Smoldering => "Smoldering Wickerman",
        MonsterType.Pigman_Hunter => "Hunter Pigman",
        MonsterType.TrenchStalker_Crypt => "Crypt Trench Stalker",
        MonsterType.BlackBride_Crimson => "Crimson Bride",
        MonsterType.Oni_Ash => "Ash Oni",
        MonsterType.ChainsawMan => "Chainsaw Man",
        _ => t.ToString(),
    };
}
