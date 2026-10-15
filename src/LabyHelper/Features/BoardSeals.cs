using System;
using System.Linq;
using HarmonyLib;
using Il2CppCharacterCustomization;
using Il2CppRandomGeneration.Contracts;

namespace LabyHelper.Features;

/// <summary>
/// Host rule: every random case on the board has a seal. Contract.GenerateContract asks FindContractType for
/// Normal (no seal), Rare (blue seal) or Hardcore before deriving everything else (lives, difficulty...), so a postfix
/// there changes the case consistently. Normal becomes Rare; Hardcore stays only while someone in the party still
/// lacks a HARDCORE-rarity cosmetic that hardcore cases drop, otherwise it becomes Rare too.
/// Only the host generates contracts (clients get them through Contract.Recreate, no regeneration), so this needs
/// no agreement between peers. Saved board contracts are regenerated from their seed on load, so they follow too.
/// Custom cases keep their chosen type.
/// </summary>
internal static class BoardSeals
{
    public static void Apply(HarmonyLib.Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(Contract), "FindContractType"),
            postfix: new HarmonyMethod(typeof(BoardSeals), nameof(Postfix)));
        Core.Log.Msg($"BoardSeals: hooked Contract.FindContractType (enabled: {Settings.BoardOnlySealed.Value}).");
    }

    private static void Postfix(Contract __instance, ref ContractType __result)
    {
        try
        {
            if (!Settings.BoardOnlySealed.Value || __instance == null || __instance.IsCustom) return;
            var original = __result;
            if (__result == ContractType.Normal) __result = ContractType.Rare;
            else if (__result == ContractType.Hardcore && HardcoreNeeded() == false) __result = ContractType.Rare;
            if (__result != original) Core.Log.Msg($"BoardSeals: case {__instance.Seed} {original} -> {__result}.");
        }
        catch (Exception e) { Core.Log.Warning($"BoardSeals: {e.Message}"); }
    }

    /// <summary>
    /// Whether anyone in the party still lacks a HARDCORE cosmetic that hardcore cases can drop. Null while the catalog
    /// isn't loaded yet (then hardcore cases are left alone rather than guessed away).
    /// </summary>
    public static bool? HardcoreNeeded()
    {
        if (!Catalog.AllIds().Any()) return null;
        var droppable = Distribution.Obtainable(hardcore: true);
        return Party.Needed().Any(id => droppable.Contains(id) && Catalog.ItemById(id)?.ItemRarity == ItemRarity.Hardcore);
    }
}
