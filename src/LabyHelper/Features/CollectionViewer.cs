using System.Collections.Generic;
using System.Linq;
using Il2CppValkoGames.Labyrinthine.UI.Customization;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Collection browser: while the game's cosmetics menu (CustomizationUIController) is open, a "Colección" button
/// shows up in its corner; F2 opens it anywhere. Tabs: missing / owned / all, and traditional vs hardcore,
/// grouped by body part, with counts per category.
/// </summary>
internal static class CollectionViewer
{
    private enum Show { Missing, Owned, All }
    private enum Kind { Traditional, Hardcore, Both }

    private const float RowH = 40f;
    private static bool _open;
    private static Show _show = Show.Missing;
    private static Kind _kind = Kind.Both;
    private static float _scroll, _nextMenuCheck, _nextRefresh;
    private static bool _menuOpen;
    private static CustomizationUIController _menu;
    private static List<Catalog.Item> _all = new();
    private static List<(string header, Catalog.Item item)> _rows = new();

    public static void Update()
    {
        if (GameState.InCase) { _open = false; _menuOpen = false; return; } // panels are lobby/menu only
        if (Input.GetKeyDown(KeyCode.F2)) Toggle();

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

    private static void Toggle()
    {
        _open = !_open;
        _nextRefresh = 0;
        _scroll = 0;
    }

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
        _all = Catalog.All().GroupBy(i => i.Id).Select(g => g.First()).ToList();
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
        _rows = q.OrderBy(i => i.IsHardcore)
                 .ThenBy(i => i.Part.ToString())
                 .ThenBy(i => i.Rarity)
                 .ThenBy(i => i.Name)
                 .Select(i => ((i.IsHardcore ? "HARDCORE · " : Loc.T("Tradicionales · ", "Traditional · ")) + Catalog.PartName(i.Part), i))
                 .ToList();
    }

    public static void Draw()
    {
        if (_menuOpen && !_open)
        {
            var b = new Rect(Screen.width - 190, 20, 170, 40);
            if (Ui.Tab(b, Loc.T("Colección (F2)", "Collection (F2)"), false)) Toggle();
        }
        if (!_open) return;

        var area = new Rect(Screen.width / 2f - 340, 60, 680, Screen.height - 120);
        Ui.Panel(area);
        float x = area.x + 14, y = area.y + 10, w = area.width - 28;

        Ui.Text(new Rect(x, y, w - 40, 26), Loc.T("Colección de cosméticos", "Cosmetics collection"), Ui.Header, Color.white);
        if (GUI.Button(new Rect(area.xMax - 44, y, 30, 26), "X")) { _open = false; return; }
        y += 32;

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

        // Rows with a section header whenever the group changes.
        var layout = new List<(float y, string header, Catalog.Item item)>();
        float cy = 0;
        string last = null;
        foreach (var (header, item) in _rows)
        {
            if (header != last) { layout.Add((cy, header, null)); cy += 30; last = header; }
            layout.Add((cy, null, item));
            cy += RowH;
        }

        var view = new Rect(x, y, w, area.yMax - y - 10);
        _scroll = Ui.KeyScroll(_scroll, cy - view.height);
        var s = GUI.BeginScrollView(view, new Vector2(0, _scroll), new Rect(0, 0, w - 20, cy));
        _scroll = s.y;
        foreach (var (ry, header, item) in layout)
        {
            if (ry + RowH < _scroll || ry > _scroll + view.height) continue; // off-screen
            if (header != null)
            {
                Ui.Text(new Rect(0, ry + 4, w, 24), header, Ui.Bold,
                    header.StartsWith("HARDCORE") ? Catalog.RarityColor(Il2CppCharacterCustomization.ItemRarity.Hardcore) : new Color(0.6f, 0.8f, 1f));
                continue;
            }
            Catalog.DrawIcon(new Rect(10, ry + 2, RowH - 4, RowH - 4), item.Id);
            Ui.Text(new Rect(RowH + 16, ry, 330, RowH), item.Name, item.Owned ? Ui.Label : Ui.Bold, item.Owned ? Color.white : new Color(1f, 0.9f, 0.5f));
            Ui.Text(new Rect(RowH + 350, ry, 110, RowH), Catalog.RarityLabel(item.Rarity), Ui.Small, Catalog.RarityColor(item.Rarity));
            Ui.Text(new Rect(w - 120, ry, 100, RowH), item.Owned ? Loc.T("lo tenés", "owned") : Loc.T("FALTA", "MISSING"), Ui.Small, item.Owned ? Color.gray : new Color(1f, 0.6f, 0.4f));
        }
        GUI.EndScrollView();
    }
}
