using System.Collections.Generic;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// The other players in the scene (PlayerNetworkSync exists on every peer for every player), for the F9 panel and
/// markers. Name from the player's nameplate (nameplateController.namePlate), refreshed twice a second.
/// </summary>
internal static class Players
{
    public record Other(Vector3 Pos, string Name, bool Dead);

    private static readonly List<Other> Cache = new();
    private static float _next;

    private static bool _safe;
    private static float _nextSafe;

    /// <summary>
    /// You are inside a safe zone of the case (PlayerNetworkSync.SafezoneType, set by the LightZone triggers: light,
    /// fire, interiors, spider tunnels), checked twice a second.
    /// </summary>
    public static bool MeInSafeZone()
    {
        if (Time.unscaledTime < _nextSafe) return _safe;
        _nextSafe = Time.unscaledTime + 0.5f;
        _safe = false;
        try
        {
            foreach (var p in Object.FindObjectsOfType<Il2Cpp.PlayerNetworkSync>())
                if (p != null && p.isLocalPlayer) { _safe = (int)p.SafezoneType != 0 && !p.isDead; break; }
        }
        catch { /* not in a match */ }
        return _safe;
    }

    public static List<Other> Others()
    {
        if (Time.unscaledTime < _next) return Cache;
        _next = Time.unscaledTime + 0.5f;
        Cache.Clear();
        try
        {
            int n = 0;
            foreach (var p in Object.FindObjectsOfType<Il2Cpp.PlayerNetworkSync>())
            {
                if (p == null || p.isLocalPlayer) continue;
                n++;
                string name = null;
                try { name = p.nameplateController?.namePlate?.text; }
                catch { /* nameplate not ready */ }
                if (string.IsNullOrWhiteSpace(name)) name = Loc.T($"Jugador {n}", $"Player {n}");
                Cache.Add(new Other(p.transform.position, name.Trim(), p.isDead));
            }
        }
        catch { /* not in a match */ }
        return Cache;
    }
}
