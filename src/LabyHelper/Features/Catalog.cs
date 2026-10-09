using System.Collections.Generic;
using System.Linq;
using Il2CppCharacterCustomization;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>Every cosmetic in the game's ItemsCollectionSO, with the bits the viewers need.</summary>
internal static class Catalog
{
    public record Item(ushort Id, string Name, ItemRarity Rarity, BodyPart Part, bool Owned)
    {
        public bool IsHardcore => Rarity == ItemRarity.Hardcore;
    }

    /// <summary>The cosmetics collection: the manager's, else the largest ItemsCollectionSO loaded (there can be
    /// several; picking the first one could miss most items).</summary>
    public static ItemsCollectionSO Collection()
    {
        var all = Resources.FindObjectsOfTypeAll<ItemsCollectionSO>();
        var mine = CustomizationManager.Instance?.Collection;
        if (!_diagDone && all.Length > 0)
        {
            _diagDone = true;
            foreach (var c in all)
            {
                int n = c.collection?.Length ?? 0, withIcon = 0;
                if (c.collection != null) foreach (var it in c.collection) if (it != null && it.Icon != null) withIcon++;
                Core.Log.Msg($"Catalog: collection '{c.name}' {n} items, {withIcon} with a loaded icon" +
                             (mine != null && mine.Pointer == c.Pointer ? " (CustomizationManager's)" : "") + ".");
            }
        }
        return mine ?? all.OrderByDescending(c => c.collection?.Length ?? 0).FirstOrDefault();
    }

    private static bool _diagDone;

    /// <summary>
    /// Item picture for any panel: the icon sprite over its rarity frame; else an icon rendered from the 3D model
    /// (ModelIcons, async); else a text placeholder for its body part (♪ for music).
    /// </summary>
    public static void DrawIcon(Rect r, ushort id)
    {
        var (icon, frame) = CosmeticAlert.Visuals(id);
        if (frame == null || !Toast.DrawSprite(r, frame)) GUI.Box(r, GUIContent.none);
        float pad = r.width * 0.1f;
        var inner = new Rect(r.x + pad, r.y + pad, r.width - 2 * pad, r.height - 2 * pad);
        if (Toast.DrawSprite(inner, icon)) return;
        var tex = ModelIcons.Get(id);
        if (tex != null) { GUI.DrawTexture(inner, tex, ScaleMode.ScaleToFit, true); return; }
        var item = Collection() is { } col && col.TryGetItem(id, out var it) ? it : null;
        Toast.DrawPlaceholder(inner, item != null ? Placeholder(item.BodyPart) : "?");
    }

    public static IEnumerable<ushort> AllIds()
    {
        var col = Collection();
        if (col?.collection == null) yield break;
        foreach (var it in col.collection)
            if (it != null) yield return it.ItemID;
    }

    public static List<Item> All()
    {
        var list = new List<Item>();
        var col = Collection();
        if (col?.collection == null) return list;
        foreach (var it in col.collection)
        {
            if (it == null) continue;
            list.Add(new Item(it.ItemID, CosmeticAlert.Name(it.ItemID), it.ItemRarity, it.BodyPart,
                CosmeticAlert.IsUnlocked(it.ItemID) == true));
        }
        return list;
    }

    /// <summary>Rarity as shown everywhere in the mod; hardcore stands out from the traditional ones.</summary>
    public static string RarityLabel(ItemRarity r) => r == ItemRarity.Hardcore ? "HARDCORE" : r.ToString();

    public static Color RarityColor(ItemRarity r) => r switch
    {
        ItemRarity.Common => new Color(0.85f, 0.85f, 0.85f),
        ItemRarity.Uncommon => new Color(0.45f, 0.85f, 0.45f),
        ItemRarity.Rare => new Color(0.4f, 0.65f, 1f),
        ItemRarity.Event => new Color(1f, 0.75f, 0.3f),
        ItemRarity.Hardcore => new Color(1f, 0.3f, 0.3f),
        ItemRarity.Limited => new Color(0.8f, 0.5f, 1f),
        _ => Color.white,
    };

    private static readonly System.Collections.Generic.Dictionary<string, (string en, string es)> Maps = new()
    {
        ["Maze_A"] = ("Unkept Hedges", "Setos No Conservados"), ["Maze_B"] = ("Kept Hedges", "Setos Conservados"),
        ["Maze_C"] = ("Forest", "Bosque"), ["Maze_E"] = ("Trenches", "Trincheras"), ["Maze_F"] = ("Cornfield", "Maizal"),
        ["Maze_G"] = ("Manor", "Mansión"), ["Maze_H"] = ("Crypts", "Criptas"), ["Maze_I"] = ("Sewers", "Alcantarillas"),
        ["Maze_J"] = ("Carnival", "Carnaval"), ["Maze_K"] = ("Mines", "Minas"), ["Maze_L"] = ("Snowy Hedges", "Setos Nevados"),
        ["Maze_M"] = ("Ruins", "Ruinas"), ["Maze_N"] = ("Dead Forest", "Bosque Muerto"), ["Maze_O"] = ("Fog City", "Ciudad de la Niebla"),
        ["Maze_P"] = ("Bamboo Forest", "Bosque de Bambú"), ["Maze_Backrooms"] = ("Backrooms", "Backrooms"),
    };

    /// <summary>Map display name from the game's own quest texts (quest_CCMaze_X); falls back to the ID.</summary>
    public static string MapName(string mazeType)
        => Maps.TryGetValue(mazeType, out var n) ? Loc.T(n.es, n.en) : mazeType;

    /// <summary>Short label drawn when an item has no drawable icon (music tracks have none).</summary>
    public static string Placeholder(BodyPart p) => p switch
    {
        BodyPart.Music => "♪",
        BodyPart.Head => Loc.T("Cabeza", "Head"),
        BodyPart.Clothing => Loc.T("Ropa", "Clothing"),
        BodyPart.Wrist => Loc.T("Muñeca", "Wrist"),
        BodyPart.Flashlight => Loc.T("Linterna", "Flashlight"),
        BodyPart.Lantern => Loc.T("Farol", "Lantern"),
        BodyPart.Glowstick => Loc.T("Barra de luz", "Glowstick"),
        BodyPart.Face => Loc.T("Cara", "Face"),
        BodyPart.Finger => Loc.T("Dedo", "Finger"),
        _ => "?",
    };

    public static string PartName(BodyPart p)
    {
        try
        {
            var g = Collection()?.GetGroupName(p);
            if (!string.IsNullOrWhiteSpace(g)) return g;
        }
        catch { /* localisation not ready */ }
        return p.ToString();
    }
}
