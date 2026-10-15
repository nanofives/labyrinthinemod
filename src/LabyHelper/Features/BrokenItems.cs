using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppCharacterCustomization;
using UnityEngine.AddressableAssets;

namespace LabyHelper.Features;

/// <summary>
/// Cosmetics whose 3D model isn't in the game's Addressables catalog. Item #434 (a Christmas hat with no name,
/// localisationKey empty) sits in the Christmas seasonal pool but its AssetReference key has no location: equipping
/// it makes CustomizationController.AddItemCoroutine throw InvalidKeyException ("No Location found") and the
/// character customization hangs. Our forced/all-seasonal pools could hand it out off-season.
///  - Party.Needed skips them, so the rules never assign them (and PoolBoost then excludes them from drops).
///  - CustomizationManager.AddItem (local equip) and ServerAddItem (host; CmdSetCustomization also goes through it)
///    refuse them, so an already unlocked one can't break anyone's character.
/// Checked at runtime with Addressables.LoadResourceLocationsAsync(RuntimeKey), cached per item.
/// </summary>
internal static class BrokenItems
{
    private static readonly Dictionary<ushort, bool> Cache = new();

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(CustomizationManager), nameof(CustomizationManager.AddItem)),
            prefix: new HarmonyMethod(typeof(BrokenItems), nameof(AddItemPrefix)));
        harmony.Patch(AccessTools.Method(typeof(CustomizationManager), "ServerAddItem"),
            prefix: new HarmonyMethod(typeof(BrokenItems), nameof(ServerAddItemPrefix)));
        Core.Log.Msg("BrokenItems: hooked CustomizationManager.AddItem/ServerAddItem.");
    }

    /// <summary>True when the item exists but its model can't be loaded. Unknown (catalog not ready) = false.</summary>
    public static bool IsBroken(ushort id)
    {
        if (Cache.TryGetValue(id, out var known)) return known;
        var item = Catalog.ItemById(id);
        if (item == null) return false; // not cached: ask again once the catalog is loaded
        bool broken = false;
        try
        {
            var ar = item.AssetReference;
            if (ar != null && ar.RuntimeKeyIsValid())
            {
                var h = Addressables.LoadResourceLocationsAsync(ar.RuntimeKey, null);
                var locations = h.WaitForCompletion();
                broken = locations == null || locations.Cast<Il2CppSystem.Collections.Generic.ICollection<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>>().Count == 0;
                Addressables.Release(h);
            }
        }
        catch (Exception e)
        {
            Core.Log.Warning($"BrokenItems: #{id}: {e.Message}");
            return false;
        }
        Cache[id] = broken;
        if (broken) Core.Log.Msg($"BrokenItems: #{id} {CosmeticAlert.Name(id)} has no model in the game files; never dropped or equipped.");
        return broken;
    }

    private static bool AddItemPrefix(ushort itemID, ref bool __result)
    {
        if (!IsBroken(itemID)) return true;
        __result = false;
        Toast.Show(Loc.T("Ese cosmético no tiene modelo en los archivos del juego y trabaría tu personaje: no se equipa.",
                         "That cosmetic has no model in the game files and would hang your character: not equipped."), 5f);
        return false;
    }

    private static bool ServerAddItemPrefix(ushort itemID)
    {
        if (!IsBroken(itemID)) return true;
        Core.Log.Msg($"BrokenItems: refused to equip #{itemID} on a player.");
        return false;
    }
}
