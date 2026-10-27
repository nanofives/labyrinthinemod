using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppValkoGames.Labyrinthine.Monsters;
using UnityEngine;
using AnimState = Il2Cpp.AnimationState;

namespace LabyHelper.Features;

/// <summary>
/// Red banner naming every monster that is chasing you right now ("PIGMAN is chasing"), for all monster types.
/// A monster counts as chasing when MonsterNetworkSync.IsChasingPlayer is set or its synced animation state is
/// spot / run / charge (the state is what clients reliably receive). The monster's type comes from its
/// AIController.MonsterType (present on every peer, disabled on clients); the GameObject name is the fallback.
/// Individual: the chase target lives in the host's behaviour tree and isn't synced, so a chase counts as yours
/// when you are the living player closest to the monster (with a small margin). No distance shown, by request.
/// Client-side only.
/// </summary>
internal static class MonsterAlert
{
    private const float Every = 0.4f;
    private const float Margin = 4f; // metres: a teammate this much closer still counts you as chased
    private static float _next;
    private static readonly Dictionary<IntPtr, string> Names = new();

    public static void OnSceneLoaded() => Names.Clear();

    public static void Update()
    {
        if (!Settings.MonsterChaseAlert.Value || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + Every;
        try
        {
            Il2Cpp.PlayerNetworkSync me = null;
            var others = new List<Vector3>();
            foreach (var p in UnityEngine.Object.FindObjectsOfType<Il2Cpp.PlayerNetworkSync>())
            {
                if (p == null) continue;
                if (p.isLocalPlayer) me = p;
                else if (!p.isDead) others.Add(p.transform.position);
            }
            if (me == null || me.isDead) return;
            Vector3 mine = me.transform.position;

            var chasing = new List<string>();
            foreach (var sync in UnityEngine.Object.FindObjectsOfType<MonsterNetworkSync>())
            {
                if (sync == null || !sync.IsEnabled) continue;
                var state = sync.CurrentAnimationState;
                bool charging = state == AnimState.MonsterCharge;
                if (!(sync.IsChasingPlayer || charging || state == AnimState.MonsterRun || state == AnimState.MonsterSpot)) continue;
                if (!ChasingMe(sync.transform.position, mine, others)) continue;
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

    /// <summary>True when no other living player is clearly closer to the monster than we are.</summary>
    private static bool ChasingMe(Vector3 monster, Vector3 me, List<Vector3> others)
    {
        float mine = Vector3.Distance(monster, me);
        foreach (var o in others)
            if (Vector3.Distance(monster, o) + Margin < mine) return false;
        return true;
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
        MonsterType.DLC_Egyptian_Anubis => "Anubis",
        MonsterType.DLC_Egyptian_Mummy => "Mummy",
        MonsterType.DLC_Egyptian_Sand_Zombie => "Sand Zombie",
        MonsterType.DLC_Egyptian_Forewarned_Ouphris => "Ouphris",
        MonsterType.DLC_Egyptian_Forewarned_Necreph => "Necreph",
        _ => t.ToString(),
    };
}
