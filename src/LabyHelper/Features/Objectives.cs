using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CaseInteractableObject = Il2CppRandomGeneration.Interactable.CaseInteractableObject;
using InsertPlaceholder = Il2CppRandomGeneration.Interactable.RndInteractableItemInsertPlaceholder;
using ObjectiveTrigger = Il2CppRandomGeneration.RndUpdateObjectivesOnTrigger;
using MazeGInteractable = Il2CppValkoGames.Labyrinthine.Cases.Interactions.RndMazeGInteractable;

namespace LabyHelper.Features;

/// <summary>
/// What the case still asks you to do, refreshed once a second during a case, for the minimap, the F9 panel and the
/// F9 world markers (only what's still pending):
///  - CaseInteractableObject (seals, effigies, levers, gasoline, map-specific items): pick up / use / destroy while
///    not IsPickedUp, not IsInserted, not IsDisabled.
///  - RndInteractableItemInsertPlaceholder: where a picked-up item goes, until IsInserted.
///  - RndUpdateObjectivesOnTrigger: "reach this place" objectives, while their trigger collider is on.
///  - RndMazeGInteractable (Maze_G objective objects).
/// </summary>
internal static class Objectives
{
    public enum Kind { Item, Slot, Zone }
    public record Clue(Vector3 Pos, Kind Kind, string Label);

    public static readonly List<Clue> Pending = new();
    public static readonly Color Colour = new(0.95f, 0.35f, 1f);
    private static float _next;
    private static string _summary;

    public static void OnSceneLoaded()
    {
        Pending.Clear();
        _summary = null;
        _next = 0;
    }

    public static void Update()
    {
        if (!GameState.InCase || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 1f;
        Pending.Clear();
        try
        {
            foreach (var o in UnityEngine.Object.FindObjectsOfType<CaseInteractableObject>())
            {
                if (o == null || !o.isActiveAndEnabled || o.IsDisabled || o.IsInserted) continue;
                bool slot = o.TryCast<InsertPlaceholder>() != null;
                if (!slot && o.IsPickedUp) continue;
                Pending.Add(new Clue(o.transform.position, slot ? Kind.Slot : Kind.Item,
                    slot ? Loc.T("dónde colocarlo", "where it goes") : Label(o.GetIl2CppType().Name, o.gameObject.name)));
            }
            foreach (var t in UnityEngine.Object.FindObjectsOfType<ObjectiveTrigger>())
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                var col = t.GetComponent<Collider>();
                if (col != null && !col.enabled) continue;
                Pending.Add(new Clue(t.transform.position, Kind.Zone, Loc.T("lugar a alcanzar", "place to reach")));
            }
            foreach (var g in UnityEngine.Object.FindObjectsOfType<MazeGInteractable>())
                if (g != null && g.isActiveAndEnabled) Pending.Add(new Clue(g.transform.position, Kind.Item, Clean(g.gameObject.name)));

            string summary = string.Join(", ", Pending.GroupBy(c => c.Label).Select(g => $"{g.Count()}x {g.Key}"));
            if (summary != _summary) Core.Log.Msg($"Objectives pending: {(summary.Length == 0 ? "none" : summary)}.");
            _summary = summary;
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Objectives: {e.Message}");
            _next = Time.unscaledTime + 10f;
        }
    }

    public static string Glyph(Kind k) => k switch { Kind.Slot => "O", Kind.Zone => "◎", _ => "◆" };

    private static string Label(string type, string goName) => type switch
    {
        "RndEffigy" => Loc.T("efigie", "effigy"),
        "RndLever" => Loc.T("palanca", "lever"),
        "RndGasoline" => Loc.T("nafta", "gasoline"),
        _ => Clean(goName),
    };

    /// <summary>"Seal_Pickup(Clone)" -> "seal pickup"; falls back to "objeto".</summary>
    private static string Clean(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Loc.T("objeto", "item");
        name = name.Replace("(Clone)", "").Replace("_", " ").Trim();
        if (name.StartsWith("Rnd", StringComparison.Ordinal) && name.Length > 3) name = name.Substring(3);
        return name.Length == 0 ? Loc.T("objeto", "item") : name.ToLowerInvariant();
    }
}
