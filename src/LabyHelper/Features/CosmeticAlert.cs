using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Il2CppCharacterCustomization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Announces the cosmetic that spawned in the current case. CustomizationPickup.Start runs on host and
/// clients alike (the pickup is a networked spawn), so this works without being host.
/// Each item is tagged by pool: map-exclusive (a spawner's possiblePrefabs), common (globalPrefabs),
/// or seasonal (RndCustomizationItemsManager.seasonalItems). F9 repeats it with distance and direction,
/// plus the map's exclusive pool.
/// </summary>
internal static class CosmeticAlert
{
    private static readonly List<CustomizationPickup> Live = new();
    private static readonly HashSet<IntPtr> Announced = new();
    private static readonly HashSet<ushort> PickedUp = new();
    // Picked up with unlockOnContractCompleted: CustomizationSave.UnlockItem parks them in unlockedItemsTemp and only
    // saves them in OnCotractFinished. Kept across scene loads until the next case starts.
    private static readonly HashSet<ushort> Pending = new();
    private static float _announceAt = -1f;
    private const float PinnedRefresh = 0.5f;
    private static float _nextPinned;

    /// <summary>F9 toggles the panel (and the world marker in Hotkey mode) on until pressed again.</summary>
    public static bool Pinned { get; private set; }

    // Pools for the current scene, built lazily from the scene's spawners.
    private static HashSet<ushort> _common, _exclusive, _seasonal;

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(CustomizationPickup), nameof(CustomizationPickup.Start)),
            postfix: new HarmonyMethod(typeof(CosmeticAlert), nameof(StartPostfix)));
        harmony.Patch(AccessTools.Method(typeof(CustomizationPickup), nameof(CustomizationPickup.Pickup)),
            postfix: new HarmonyMethod(typeof(CosmeticAlert), nameof(PickupPostfix)));
        harmony.Patch(AccessTools.Method(typeof(CustomizationSave), nameof(CustomizationSave.OnCotractFinished)),
            postfix: new HarmonyMethod(typeof(CosmeticAlert), nameof(ContractFinishedPostfix)));
        Core.Log.Msg("CosmeticAlert: hooked CustomizationPickup.Start/Pickup, CustomizationSave.OnCotractFinished.");
    }

    public static void OnSceneLoaded()
    {
        Live.Clear();
        Announced.Clear();
        PickedUp.Clear();
        if (GameState.InCase) Pending.Clear();
        _announceAt = -1f;
        Pinned = GameState.InCase && Settings.CosmeticPanelOnByDefault.Value; // F9 starts on in every case
        _nextPinned = Time.unscaledTime + 3f; // let the case's cosmetic spawn first
        _common = _exclusive = _seasonal = null;
    }

    private static void StartPostfix(CustomizationPickup __instance)
    {
        if (!Settings.CosmeticAlertEnabled.Value || __instance == null) return;
        // Pickups parked at the origin are previews/templates, not world spawns.
        if (__instance.transform.localPosition == Vector3.zero) return;
        if (!Announced.Add(__instance.Pointer)) return;

        Live.Add(__instance);
        PoolCache.LearnFromScene();
        Core.Log.Msg($"Cosmetic spawned: id {__instance.ItemID} {Describe(__instance.ItemID)} at {__instance.transform.position}");
        // Batch: a case can spawn more than one; announce once they have all started.
        _announceAt = Time.unscaledTime + 1.5f;
    }

    private static void PickupPostfix(CustomizationPickup __instance)
    {
        if (__instance == null) return;
        ushort id = __instance.ItemID;
        PickedUp.Add(id);
        bool pending = false;
        try { pending = __instance.unlockOnContractCompleted && IsUnlocked(id) != true; }
        catch { /* field not readable: say nothing about when it saves */ }
        if (pending) Pending.Add(id);
        Core.Log.Msg($"CosmeticAlert: picked up {Name(id)} (#{id}), {(pending ? "unlock saved when the case is completed" : "unlocked now")}.");
        Toast.Show($"{Loc.T("Agarraste", "Picked up")}: {Name(id)}\n" +
                   (pending ? Loc.T("Se guarda en tu colección al terminar el caso.", "It is added to your collection when the case is completed.")
                            : Loc.T("Ya está en tu colección.", "It's in your collection.")), 6f);
    }

    private static void ContractFinishedPostfix()
    {
        if (Pending.Count == 0) return;
        try
        {
            var saved = Pending.Where(id => IsUnlocked(id) == true).ToList();
            var missing = Pending.Except(saved).ToList();
            Core.Log.Msg($"CosmeticAlert: case completed, saved {string.Join(", ", saved.Select(Name))}" +
                         (missing.Count > 0 ? $"; not saved: {string.Join(", ", missing.Select(Name))}" : "") + ".");
            if (saved.Count > 0)
                Toast.Show(Loc.T($"Desbloqueado: {string.Join(", ", saved.Select(Name))}", $"Unlocked: {string.Join(", ", saved.Select(Name))}"), 6f);
            Pending.Clear();
        }
        catch (Exception e) { Core.Log.Warning($"CosmeticAlert: {e.Message}"); }
    }

    public static void Update()
    {
        if (_announceAt > 0 && Time.unscaledTime >= _announceAt)
        {
            _announceAt = -1f;
            Announce(withLocation: false, seconds: Settings.CosmeticAlertSeconds.Value);
        }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            Pinned = !Pinned;
            if (Pinned) _nextPinned = 0f;
            else Toast.Hide(Toast.Slot.Info);
        }
        // While pinned, redraw every half second so distance and direction follow you. A short message in the same
        // slot (e.g. "Picked up") is left on screen until it expires.
        if (Pinned && Time.unscaledTime >= _nextPinned && !Toast.ShowingPlainInfo)
        {
            _nextPinned = Time.unscaledTime + PinnedRefresh;
            Announce(withLocation: true, seconds: PinnedRefresh + 0.5f);
        }
    }

    private static void Announce(bool withLocation, float seconds)
    {
        Live.RemoveAll(p => p == null || p.WasCollected);
        var pending = Live.Where(p => !PickedUp.Contains(p.ItemID)).ToList();
        if (pending.Count == 0 && !withLocation) return; // nothing to announce automatically

        string header = pending.Count switch
        {
            0 when Live.Count == 0 && PickedUp.Count == 0 => Loc.T("No apareció ningún cosmético en este caso.", "No cosmetic spawned in this case."),
            0 when Pending.Count > 0 => Loc.T($"Agarrado, se guarda al terminar el caso: {string.Join(", ", Pending.Select(Name))}",
                                             $"Picked up, saved when the case is completed: {string.Join(", ", Pending.Select(Name))}"),
            0 => Loc.T("No quedan cosméticos por agarrar en este caso.", "No cosmetics left to pick up in this case."),
            1 => Loc.T("Cosmético en este caso:", "Cosmetic in this case:"),
            _ => Loc.T($"Cosméticos en este caso ({pending.Count}):", $"Cosmetics in this case ({pending.Count}):"),
        };

        var rows = new List<Toast.ItemRow>();
        foreach (var p in pending)
        {
            var (icon, frame) = Visuals(p.ItemID);
            string text = Describe(p.ItemID);
            if (withLocation) text += $"\n{Locate(p.transform.position)} {Loc.T("en línea recta", "as the crow flies")}";
            rows.Add(new Toast.ItemRow(icon, frame, text));
        }

        string footer = null;
        if (withLocation)
        {
            EnsurePools();
            if (_exclusive is { Count: > 0 })
                footer = Loc.T("Exclusivos posibles de este mapa: ", "Possible map exclusives: ") +
                         string.Join(", ", _exclusive.Select(id => $"{Name(id)}{(IsUnlocked(id) == true ? Loc.T(" (ya)", " (owned)") : "")}"));
        }
        if (withLocation && Settings.CollectibleMarkers.Value && Collectibles.Summary() is string col)
            footer = footer == null ? col : footer + "\n" + col;
        if (withLocation && Players.Others() is { Count: > 0 } others)
        {
            string who = Loc.T("Jugadores: ", "Players: ") + string.Join("  ·  ", others.Select(o =>
                $"{o.Name} {Locate(o.Pos)}{(o.Dead ? Loc.T(" (caído)", " (down)") : "")}"));
            footer = footer == null ? who : footer + "\n" + who;
        }
        Toast.ShowItems(header, rows, footer, seconds);
    }

    /// <summary>The item's icon (pre-rendered from its 3D model by the game) and the rarity frame used in the customization menu.</summary>
    public static (Sprite icon, Sprite frame) Visuals(ushort itemID)
    {
        // Icon and frame are fetched separately: GetFrameSprite throws for some rarities (frame array shorter than the
        // rarity enum), and a shared try/catch used to throw the valid icon away with it.
        if (VisualsCache.TryGetValue(itemID, out var hit)) return hit;
        Sprite icon = null, frame = null;
        CustomizationItem item = null;
        try { item = TryGetItem(itemID); icon = item?.Icon; }
        catch { /* no icon */ }
        if (item != null)
        {
            try { frame = Catalog.Collection()?.GetFrameSprite(item.ItemRarity); }
            catch { /* no frame for this rarity */ }
            VisualsCache[itemID] = (icon, frame);
        }
        return (icon, frame);
    }

    public static string Describe(ushort itemID, bool includePool = true)
    {
        var item = TryGetItem(itemID);
        string owned = IsUnlocked(itemID) switch { true => Loc.T("ya lo tenés", "owned"), false => Loc.T("NUEVO", "NEW"), _ => "?" };
        string rarity = item != null ? Catalog.RarityLabel(item.ItemRarity) : "?";
        string tag = includePool ? $"{rarity}, {PoolOf(itemID)}" : rarity;
        return $"{Name(itemID)} [{tag}] - {owned}";
    }

    /// <summary>Cosmetics still lying in the map, for WorldMarker.</summary>
    public static IEnumerable<(Vector3 pos, string name, ushort id)> PendingTargets()
    {
        Live.RemoveAll(p => p == null || p.WasCollected);
        foreach (var p in Live)
            if (!PickedUp.Contains(p.ItemID)) yield return (p.transform.position, Name(p.ItemID), p.ItemID);
    }

    private static string PoolOf(ushort itemID)
    {
        EnsurePools();
        if (RandomCaseRule.LastCarried == itemID) return Loc.T("traído de otro mapa", "brought from another map");
        if (_seasonal.Contains(itemID)) return Loc.T("temporada", "seasonal");
        if (_exclusive.Contains(itemID)) return Loc.T("EXCLUSIVO DEL MAPA", "MAP EXCLUSIVE");
        if (_common.Contains(itemID)) return Loc.T("pool común", "common pool");
        return Loc.T("pool desconocido", "unknown pool");
    }

    private static void EnsurePools()
    {
        if (_common != null) return;
        _common = new HashSet<ushort>();
        _exclusive = new HashSet<ushort>();
        _seasonal = new HashSet<ushort>();
        try
        {
            foreach (var spawner in UnityEngine.Object.FindObjectsOfType<RndCustomizationSpawner>(true))
            {
                AddIds(_common, spawner.globalPrefabs?.Value);
                AddIds(_common, spawner.globalPrefabsHardcore?.Value);
                AddIds(_exclusive, spawner.possiblePrefabs);
                AddIds(_exclusive, spawner.possiblePrefabsHardcore);
            }
            var manager = RndCustomizationItemsManager.Instance;
            if (manager != null && manager.seasonalItems != null)
                foreach (var cfg in manager.seasonalItems)
                    if (cfg != null) AddIds(_seasonal, cfg.prefabs);

            _exclusive.ExceptWith(_common);
            _exclusive.ExceptWith(_seasonal);
            _exclusive.RemoveWhere(id => !PoolCache.IsMapExclusive(id)); // shared by several maps: not exclusive
            Core.Log.Msg($"Pools: {_common.Count} common, {_exclusive.Count} map-exclusive, {_seasonal.Count} seasonal.");
        }
        catch (Exception e)
        {
            Core.Log.Warning($"CosmeticAlert: could not read spawner pools: {e.Message}");
        }
    }

    private static void AddIds(HashSet<ushort> into, Il2CppReferenceArray<Il2Cpp.WeightedPrefab> prefabs)
    {
        if (prefabs == null) return;
        foreach (var wp in prefabs)
        {
            var go = wp?.Value;
            if (go == null) continue;
            var pickup = go.GetComponentInChildren<CustomizationPickup>(true);
            if (pickup != null) into.Add(pickup.ItemID);
        }
    }

    private static readonly Dictionary<ushort, string> Names = new();
    private static readonly Dictionary<ushort, (Sprite, Sprite)> VisualsCache = new();

    public static void ClearCaches()
    {
        Names.Clear();
        VisualsCache.Clear();
    }

    public static string Name(ushort itemID)
    {
        if (Names.TryGetValue(itemID, out var cached)) return cached;
        var n = ResolveName(itemID);
        if (!n.StartsWith("item #")) Names[itemID] = n; // don't cache before the collection/localisation is ready
        return n;
    }

    private static string ResolveName(ushort itemID)
    {
        var item = TryGetItem(itemID);
        if (item == null) return $"item #{itemID}";
        try
        {
            var n = item.Name;
            if (!string.IsNullOrWhiteSpace(n)) return n;
        }
        catch { /* localisation not ready */ }
        if (!string.IsNullOrWhiteSpace(item.LocalisationKey)) return item.LocalisationKey;
        try
        {
            var icon = item.Icon;
            if (icon != null && !string.IsNullOrWhiteSpace(icon.name)) return icon.name;
        }
        catch { }
        return $"item #{itemID}";
    }

    private static CustomizationItem TryGetItem(ushort itemID)
    {
        try { return Catalog.ItemById(itemID); }
        catch (Exception e) { Core.Log.Warning($"CosmeticAlert: item lookup failed: {e.Message}"); return null; }
    }

    public static bool? IsUnlocked(ushort itemID)
    {
        try { return CustomizationManager.Instance?.CustomizationSave?.IsItemUnlocked(itemID); }
        catch { return null; }
    }

    /// <summary>"12 m a la derecha, más arriba" relative to where the camera looks.</summary>
    public static string Locate(Vector3 target)
    {
        var cam = Camera.main;
        if (cam == null) return Loc.T("posición desconocida", "unknown position");
        Vector3 delta = target - cam.transform.position;
        float meters = new Vector2(delta.x, delta.z).magnitude;
        float angle = Vector3.SignedAngle(Flat(cam.transform.forward), Flat(delta), Vector3.up);
        string dir = Mathf.Abs(angle) switch
        {
            <= 30f => Loc.T("adelante", "ahead"),
            >= 150f => Loc.T("atrás", "behind"),
            _ => angle > 0 ? Loc.T("a la derecha", "to the right") : Loc.T("a la izquierda", "to the left"),
        };
        string height = delta.y switch { > 3f => Loc.T(", más arriba", ", above"), < -3f => Loc.T(", más abajo", ", below"), _ => "" };
        return $"{meters:0} m {dir}{height}";
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
