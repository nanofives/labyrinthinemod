using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppValkoGames.Labyrinthine.Cases.Interactions;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Tickets (Currency), reroll coins (RerollToken) and XP stamps (Experience) lying in the case: RndCollectible
/// objects spawned by RndCollectibleSpawner. Scanned every 2 s; picked ones disappear or get disabled, so only
/// active ones count. Shown by WorldMarker (F9 / Always) and summarised in the F9 alert.
/// </summary>
internal static class Collectibles
{
    public record Item(Vector3 Pos, ERndCollectibleType Type, int Amount);

    private static float _nextScan;
    public static List<Item> Live { get; private set; } = new();

    public static void Update()
    {
        if (!Settings.CollectibleMarkers.Value || Time.unscaledTime < _nextScan) return;
        _nextScan = Time.unscaledTime + 2f;
        try
        {
            var list = new List<Item>();
            foreach (var c in UnityEngine.Object.FindObjectsOfType<RndCollectible>())
            {
                if (c == null || !c.isActiveAndEnabled) continue;
                list.Add(new Item(c.transform.position, c.collectibleType, c.amount));
            }
            Live = list;
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Collectibles: {e.Message}");
            _nextScan = Time.unscaledTime + 30f;
        }
    }

    public static void OnSceneLoaded() => Live = new List<Item>();

    public static string Label(ERndCollectibleType t) => t switch
    {
        ERndCollectibleType.Currency => Loc.T("Tickets", "Tickets"),
        ERndCollectibleType.RerollToken => Loc.T("Ficha de reroll", "Reroll coin"),
        ERndCollectibleType.Experience => Loc.T("XP", "XP"),
        _ => t.ToString(),
    };

    public static Color Color(ERndCollectibleType t) => t switch
    {
        ERndCollectibleType.Currency => new Color(1f, 0.85f, 0.3f),
        ERndCollectibleType.RerollToken => new Color(0.55f, 1f, 0.6f),
        ERndCollectibleType.Experience => new Color(0.85f, 0.6f, 1f),
        _ => UnityEngine.Color.white,
    };

    /// <summary>"Tickets 4 (x20) · Ficha de reroll 1 · XP 6 (x120)" for the F9 alert.</summary>
    public static string Summary()
    {
        if (Live.Count == 0) return null;
        return Loc.T("Coleccionables: ", "Collectibles: ") + string.Join(" · ", Live.GroupBy(i => i.Type).OrderBy(g => g.Key).Select(g =>
        {
            int total = g.Sum(i => i.Amount);
            return $"{Label(g.Key)} {g.Count()}" + (total > g.Count() ? $" (x{total})" : "");
        }));
    }
}
