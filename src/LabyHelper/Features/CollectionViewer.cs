using System.Collections.Generic;
using System.Linq;
using Il2CppValkoGames.Labyrinthine.UI.Customization;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Collection browser: while the game's cosmetics menu (CustomizationUIController) is open, a "Colección" button
/// shows up in its corner; F2 opens it anywhere. Tabs: missing / owned / all, and traditional vs hardcore,
/// grouped by body part, with counts per category. Hovering a row shows the cosmetic large next to the panel.
/// </summary>
internal static class CollectionViewer
{
    private enum Show { Missing, Owned, All }
    private enum Kind { Traditional, Hardcore, Both }

    private const float RowH = 40f;
    private const float PreviewSize = 300f;
    private static bool _open;
    private static Show _show = Show.Missing;
    private static Kind _kind = Kind.Both;
    private static Il2CppCharacterCustomization.BodyPart _part = Il2CppCharacterCustomization.BodyPart.Head; // selected tab
    private static float _scroll, _nextMenuCheck, _nextRefresh;
    private static bool _menuOpen;
    private static CustomizationUIController _menu;
    private static List<Catalog.Item> _all = new();
    private static List<(string header, Catalog.Item item)> _rows = new();

    public static void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2)) Toggle();
        if (_open && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
        Ui.KeepCursorFree();

        if (Time.unscaledTime >= _nextMenuCheck)
        {
            _nextMenuCheck = Time.unscaledTime + 0.5f;
            _menuOpen = IsMenuOpen();
        }
        if (_open && Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + 2f; // ownership changes rarely; keep it cheap
            Rebuild();
        }
    }

    public static bool IsOpen => _open;

    private static void Toggle() => SetOpen(!_open);

    private static void SetOpen(bool open)
    {
        _open = open;
        _nextRefresh = 0;
        _scroll = 0;
        // Over gameplay (not from the game's customization menu): stop the player and free the cursor.
        Ui.Capture("F2", open && !_menuOpen);
    }

    public static void OnSceneLoaded() => SetOpen(false);

    private static bool IsMenuOpen()
    {
        try
        {
            if (_menu == null || _menu.WasCollected) _menu = Object.FindObjectOfType<CustomizationUIController>();
            var cg = _menu?.canvasGroup;
            return cg != null && _menu.isActiveAndEnabled && cg.alpha > 0.5f && cg.interactable;
        }
        catch { return false; }
    }

    private static void Rebuild()
    {
        _all = Catalog.All().GroupBy(i => i.Id).Select(g => g.First()).Where(i => Catalog.Available(i.Id)).ToList();
        IEnumerable<Catalog.Item> q = _all;
        q = _show switch
        {
            Show.Missing => q.Where(i => !i.Owned),
            Show.Owned => q.Where(i => i.Owned),
            _ => q,
        };
        q = _kind switch
        {
            Kind.Traditional => q.Where(i => !i.IsHardcore),
            Kind.Hardcore => q.Where(i => i.IsHardcore),
            _ => q,
        };
        // Same groups and order as the game's customization menu (CustomizationUIController.UpdateItemsUI): one group
        // per body part in enum order (Head, Clothing, Wrist, Flashlight, Lantern, Glowstick, Face, Finger, then Music),
        // owned items in the order they were unlocked (CustomizationManager.UnlockedItems), then the rest in the
        // collection's order.
        var unlocked = UnlockOrder();
        var position = new Dictionary<ushort, int>();
        for (int i = 0; i < _all.Count; i++) position[_all[i].Id] = i;
        _rows = q.Where(i => i.Part == _part)
                 .OrderBy(i => unlocked.TryGetValue(i.Id, out var k) ? k : int.MaxValue)
                 .ThenBy(i => position[i.Id])
                 .Select(i => (Catalog.PartName(i.Part), i))
                 .ToList();
    }

    /// <summary>Item ID -> position in the unlock list the game's menu walks.</summary>
    private static Dictionary<ushort, int> UnlockOrder()
    {
        var order = new Dictionary<ushort, int>();
        try
        {
            var list = Il2CppCharacterCustomization.CustomizationManager.Instance?.UnlockedItems;
            if (list == null) return order;
            for (int i = 0; i < list.Count; i++)
            {
                ushort id = list[i];
                order.TryAdd(id, i);
            }
        }
        catch { /* save not loaded yet */ }
        return order;
    }

    public static void Draw()
    {
        if (_menuOpen && !_open)
        {
            var b = new Rect(Screen.width - 190, 20, 170, 40);
            if (Ui.Tab(b, Loc.T("Colección (F2)", "Collection (F2)"), false)) Toggle();
        }
        if (!_open) return;

        var area = OptionsMenu.PanelRect(); // same place and size as F1
        Ui.Panel(area);
        float x = area.x + 14, y = area.y + 10, w = area.width - 28;

        Ui.Text(new Rect(x, y, w - 40, 26), Loc.T("Colección de cosméticos", "Cosmetics collection"), Ui.Header, Color.white);
        if (GUI.Button(new Rect(area.xMax - 44, y, 30, 26), "X")) { SetOpen(false); return; }
        y += 32;
        y = PartTabs(x, y, w) + 8;

        int trad = _all.Count(i => !i.IsHardcore), tradOwned = _all.Count(i => !i.IsHardcore && i.Owned);
        int hc = _all.Count(i => i.IsHardcore), hcOwned = _all.Count(i => i.IsHardcore && i.Owned);
        Ui.Text(new Rect(x, y, w, 22), $"{Loc.T("Tradicionales", "Traditional")}: {tradOwned}/{trad}    HARDCORE: {hcOwned}/{hc}    Total: {tradOwned + hcOwned}/{trad + hc}",
            Ui.Bold, Color.white);
        y += 28;

        float tw = 110;
        if (Ui.Tab(new Rect(x, y, tw, 28), Loc.T("Faltan", "Missing"), _show == Show.Missing)) { _show = Show.Missing; _nextRefresh = 0; _scroll = 0; }
        if (Ui.Tab(new Rect(x + tw + 6, y, tw, 28), Loc.T("Tengo", "Owned"), _show == Show.Owned)) { _show = Show.Owned; _nextRefresh = 0; _scroll = 0; }
        if (Ui.Tab(new Rect(x + 2 * (tw + 6), y, tw, 28), Loc.T("Todos", "All"), _show == Show.All)) { _show = Show.All; _nextRefresh = 0; _scroll = 0; }
        y += 34;
        float kx = x;
        if (Ui.Tab(new Rect(kx, y, tw, 28), Loc.T("Tradicionales", "Traditional"), _kind == Kind.Traditional)) { _kind = Kind.Traditional; _nextRefresh = 0; _scroll = 0; }
        if (Ui.Tab(new Rect(kx + tw + 6, y, tw, 28), "Hardcore", _kind == Kind.Hardcore)) { _kind = Kind.Hardcore; _nextRefresh = 0; _scroll = 0; }
        if (Ui.Tab(new Rect(kx + 2 * (tw + 6), y, tw, 28), Loc.T("Ambos", "Both"), _kind == Kind.Both)) { _kind = Kind.Both; _nextRefresh = 0; _scroll = 0; }
        y += 38;

        // Only the selected tab's items (the tabs are the groups).
        var layout = new List<(float y, Catalog.Item item)>();
        float cy = 0;
        foreach (var (_, item) in _rows)
        {
            layout.Add((cy, item));
            cy += RowH;
        }

        var view = new Rect(x, y, w, area.yMax - y - 10);
        _scroll = Ui.KeyScroll(_scroll, cy - view.height);
        var s = GUI.BeginScrollView(view, new Vector2(0, _scroll), new Rect(0, 0, w - 20, cy));
        _scroll = s.y;
        Catalog.Item hovered = null;
        var mouse = Event.current.mousePosition; // scroll-view coordinates here
        foreach (var (ry, item) in layout)
        {
            if (ry + RowH < _scroll || ry > _scroll + view.height) continue; // off-screen
            // Only the part of the list inside the scroll view counts (rows above/below are clipped).
            if (new Rect(0, ry, w - 20, RowH).Contains(mouse) && mouse.y >= _scroll && mouse.y <= _scroll + view.height)
                hovered = item;
            Catalog.DrawIcon(new Rect(10, ry + 2, RowH - 4, RowH - 4), item.Id);
            Ui.Text(new Rect(RowH + 16, ry, 330, RowH), item.Name, item.Owned ? Ui.Label : Ui.Bold, item.Owned ? Color.white : new Color(1f, 0.9f, 0.5f));
            Ui.Text(new Rect(RowH + 350, ry, 110, RowH), Catalog.RarityLabel(item.Rarity), Ui.Small, Catalog.RarityColor(item.Rarity));
            Ui.Text(new Rect(w - 120, ry, 100, RowH), item.Owned ? Loc.T("lo tenés", "owned") : Loc.T("FALTA", "MISSING"), Ui.Small, item.Owned ? Color.gray : new Color(1f, 0.6f, 0.4f));
        }
        GUI.EndScrollView();
        if (hovered != null) DrawPreview(area, hovered);
    }

    /// <summary>
    /// One tab per body part (the game menu's groups, same order), with owned/total under the current filters; wraps
    /// into more rows when they don't fit. Returns the bottom edge.
    /// </summary>
    private static float PartTabs(float x, float y, float w)
    {
        IEnumerable<Catalog.Item> pool = _kind switch
        {
            Kind.Traditional => _all.Where(i => !i.IsHardcore),
            Kind.Hardcore => _all.Where(i => i.IsHardcore),
            _ => _all,
        };
        var parts = _all.Select(i => i.Part).Distinct().OrderBy(p => (int)p).ToList();
        if (parts.Count > 0 && !parts.Contains(_part)) { _part = parts[0]; _nextRefresh = 0; }
        _tabStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 14 };
        const float h = 34f, gap = 6f;
        float cx = x;
        foreach (var part in parts)
        {
            var items = pool.Where(i => i.Part == part).ToList();
            string label = $"{Catalog.PartName(part)}  {items.Count(i => i.Owned)}/{items.Count}";
            float tw = _tabStyle.CalcSize(new GUIContent(label)).x + 18f;
            if (cx > x && cx + tw > x + w) { cx = x; y += h + gap; }
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = part == _part ? new Color(0.35f, 0.6f, 1f) : new Color(0.3f, 0.3f, 0.3f);
            if (GUI.Button(new Rect(cx, y, tw, h), label, _tabStyle) && part != _part)
            {
                _part = part;
                _scroll = 0;
                _nextRefresh = 0;
            }
            GUI.backgroundColor = prev;
            cx += tw + gap;
        }
        return y + h;
    }

    private static GUIStyle _tabStyle;

    /// <summary>Large picture of the hovered cosmetic, beside the panel (right side, or left if it doesn't fit).</summary>
    private static void DrawPreview(Rect area, Catalog.Item item)
    {
        const float pad = 12f, textH = 56f;
        float pw = PreviewSize + 2 * pad, ph = PreviewSize + 2 * pad + textH;
        float px = area.xMax + 12 + pw <= Screen.width ? area.xMax + 12 : Mathf.Max(0, area.x - 12 - pw);
        float py = Mathf.Clamp(Event.current.mousePosition.y - ph / 2, 10, Screen.height - ph - 10);
        var box = new Rect(px, py, pw, ph);
        Ui.Panel(box);
        Catalog.DrawIcon(new Rect(px + pad, py + pad, PreviewSize, PreviewSize), item.Id);
        float ty = py + pad + PreviewSize + 4;
        Ui.Text(new Rect(px + pad, ty, PreviewSize, 26), item.Name, Ui.Bold, Color.white);
        Ui.Text(new Rect(px + pad, ty + 26, PreviewSize, 24),
            $"{Catalog.RarityLabel(item.Rarity)} · {Catalog.PartName(item.Part)} · {(item.Owned ? Loc.T("lo tenés", "owned") : Loc.T("FALTA", "MISSING"))}",
            Ui.Small, Catalog.RarityColor(item.Rarity));
    }
}
