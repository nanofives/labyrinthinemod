using System.Collections.Generic;
using System.Linq;
using Il2CppValkoGames.Labyrinthine.UI.Customization;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Collection browser: while the game's cosmetics menu (CustomizationUIController) is open, a "Colección" button
/// shows up in its corner; F2 opens it anywhere (menus, lobby, during a case). Drawn in the game's style (Ui). Tabs: missing / owned / all, and traditional vs hardcore,
/// grouped by body part, with counts per category. Hovering a row shows the cosmetic large next to the panel.
/// </summary>
internal static class CollectionViewer
{
    private enum Show { Missing, Owned, All }
    private enum Kind { Traditional, Hardcore, Both }

    private const float RowH = 52f;
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

    private static GUIStyle _name, _nameBold, _small, _filter, _partTab;
    private static Font _font;

    private static void Styles()
    {
        if (_name != null && _font == UiSkin.Font) return;
        _font = UiSkin.Font;
        _name = Ui.Sized(Ui.Label, 24);
        _nameBold = Ui.Sized(Ui.Bold, 24);
        _small = Ui.Sized(Ui.Label, 21);
        _filter = Ui.Sized(Ui.TabStyle, 21);
        _partTab = Ui.Sized(Ui.TabStyle, 21);
    }

    public static void Draw()
    {
        Ui.BeginScaled();
        try { DrawScaled(); }
        finally { Ui.EndScaled(); }
    }

    private static void DrawScaled()
    {
        if (_menuOpen && !_open)
        {
            var b = new Rect(Ui.W - 300, 20, 280, 52);
            if (Ui.Button(b, Loc.T("Colección (F2)", "Collection (F2)"))) Toggle();
        }
        if (!_open) return;

        Styles();
        var area = OptionsMenu.PanelRect(); // same place and size as F1
        Ui.Panel(area);
        const float pad = 44f;
        float x = area.x + pad, y = area.y + 30, w = area.width - 2 * pad;

        string title = Loc.T("COLECCIÓN", "COLLECTION");
        Ui.Text(new Rect(x, y, 600, 48), title, Ui.Title, Ui.Paper);
        float tw = Ui.Title.CalcSize(new GUIContent(title)).x;
        int trad = _all.Count(i => !i.IsHardcore), tradOwned = _all.Count(i => !i.IsHardcore && i.Owned);
        int hc = _all.Count(i => i.IsHardcore), hcOwned = _all.Count(i => i.IsHardcore && i.Owned);
        Ui.Text(new Rect(x + tw + 20, y + 10, w - tw - 80, 36),
            $"{Loc.T("Tradicionales", "Traditional")} {tradOwned}/{trad}   ·   HARDCORE {hcOwned}/{hc}   ·   Total {tradOwned + hcOwned}/{trad + hc}",
            _small, Ui.Dim);
        if (Ui.Close(new Rect(area.xMax - pad - 44, y + 2, 44, 44))) { SetOpen(false); return; }
        y += 64;
        y = PartTabs(x, y, w) + 14;

        // Filters: what to show, and which kind.
        float fw = 220, fh = 48, fg = 8;
        if (Ui.Tab(new Rect(x, y, fw, fh), Loc.T("Faltan", "Missing"), _show == Show.Missing, _filter)) { _show = Show.Missing; _nextRefresh = 0; _scroll = 0; }
        if (Ui.Tab(new Rect(x + (fw + fg), y, fw, fh), Loc.T("Tengo", "Owned"), _show == Show.Owned, _filter)) { _show = Show.Owned; _nextRefresh = 0; _scroll = 0; }
        if (Ui.Tab(new Rect(x + 2 * (fw + fg), y, fw, fh), Loc.T("Todos", "All"), _show == Show.All, _filter)) { _show = Show.All; _nextRefresh = 0; _scroll = 0; }
        float kx = x + 3 * (fw + fg) + 40;
        if (kx + 3 * (fw + fg) > x + w) { kx = x; y += fh + 8; }
        if (Ui.Tab(new Rect(kx, y, fw, fh), Loc.T("Tradicionales", "Traditional"), _kind == Kind.Traditional, _filter)) { _kind = Kind.Traditional; _nextRefresh = 0; _scroll = 0; }
        if (Ui.Tab(new Rect(kx + (fw + fg), y, fw, fh), "Hardcore", _kind == Kind.Hardcore, _filter)) { _kind = Kind.Hardcore; _nextRefresh = 0; _scroll = 0; }
        if (Ui.Tab(new Rect(kx + 2 * (fw + fg), y, fw, fh), Loc.T("Ambos", "Both"), _kind == Kind.Both, _filter)) { _kind = Kind.Both; _nextRefresh = 0; _scroll = 0; }
        y += fh + 18;

        // Only the selected tab's items (the tabs are the groups).
        float cy = _rows.Count * RowH;
        var view = new Rect(x - 8, y, w + 16, area.yMax - y - 26);
        float lw = view.width - 24;
        Ui.BeginScroll(view, ref _scroll, cy, "f2");
        Catalog.Item hovered = null;
        var mouse = Event.current.mousePosition; // content coordinates here
        for (int k = 0; k < _rows.Count; k++)
        {
            var item = _rows[k].item;
            float ry = k * RowH;
            if (ry + RowH < _scroll || ry > _scroll + view.height) continue; // off-screen
            var row = new Rect(0, ry, lw, RowH);
            // Only the part of the list inside the view counts (rows above/below are clipped).
            if (row.Contains(mouse) && mouse.y >= _scroll && mouse.y <= _scroll + view.height) hovered = item;
            Ui.RowHover(row, "f2:" + item.Id);
            Catalog.DrawIcon(new Rect(10, ry + 3, RowH - 6, RowH - 6), item.Id);
            Ui.Text(new Rect(RowH + 20, ry, lw * 0.5f, RowH), item.Name, item.Owned ? _name : _nameBold, item.Owned ? Ui.Paper : new Color(1f, 0.88f, 0.55f));
            Ui.Text(new Rect(RowH + 20 + lw * 0.5f, ry, 200, RowH), Catalog.RarityLabel(item.Rarity), _small, Catalog.RarityColor(item.Rarity));
            Ui.Text(new Rect(lw - 170, ry, 160, RowH), item.Owned ? Loc.T("lo tenés", "owned") : Loc.T("FALTA", "MISSING"), _small, item.Owned ? Ui.Dim : new Color(1f, 0.6f, 0.4f));
        }
        Ui.EndScroll(ref _scroll);
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
        const float h = 52f, gap = 6f;
        float cx = x;
        foreach (var part in parts)
        {
            var items = pool.Where(i => i.Part == part).ToList();
            string label = $"{Catalog.PartName(part)}  {items.Count(i => i.Owned)}/{items.Count}";
            float tw = _partTab.CalcSize(new GUIContent(Ui.Caps(label))).x + 44f;
            if (cx > x && cx + tw > x + w) { cx = x; y += h + gap; }
            if (Ui.Tab(new Rect(cx, y, tw, h), label, part == _part, _partTab) && part != _part)
            {
                _part = part;
                _scroll = 0;
                _nextRefresh = 0;
            }
            cx += tw + gap;
        }
        return y + h;
    }

    /// <summary>Large picture of the hovered cosmetic, beside the panel (right side, or left if it doesn't fit).</summary>
    private static void DrawPreview(Rect area, Catalog.Item item)
    {
        const float pad = 22f, textH = 60f;
        float pw = PreviewSize + 2 * pad, ph = PreviewSize + 2 * pad + textH;
        float px = area.xMax + 12 + pw <= Ui.W ? area.xMax + 12 : Mathf.Max(0, area.x - 12 - pw);
        float py = Mathf.Clamp(Event.current.mousePosition.y - ph / 2, 10, Ui.H - ph - 10);
        var box = new Rect(px, py, pw, ph);
        Ui.Panel(box);
        Catalog.DrawIcon(new Rect(px + pad, py + pad, PreviewSize, PreviewSize), item.Id);
        float ty = py + pad + PreviewSize + 4;
        Ui.Text(new Rect(px + pad, ty, PreviewSize, 26), item.Name, Ui.Header, Ui.Paper);
        Ui.Text(new Rect(px + pad, ty + 26, PreviewSize, 24),
            $"{Catalog.RarityLabel(item.Rarity)} · {Catalog.PartName(item.Part)} · {(item.Owned ? Loc.T("lo tenés", "owned") : Loc.T("FALTA", "MISSING"))}",
            Ui.Small, Catalog.RarityColor(item.Rarity));
    }
}
