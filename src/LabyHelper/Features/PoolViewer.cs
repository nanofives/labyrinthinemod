using System.Collections.Generic;
using System.Linq;
using Il2CppRandomGeneration.Contracts;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// F3: live view of the cosmetic pool for the current contract, under the active rules (difficulty gate,
/// prefer-new, all-seasonal, forced event). Recomputed every second. PageUp/PageDown or the wheel scroll.
/// </summary>
internal static class PoolViewer
{
    private const float RowH = 34f;
    private static bool _open;
    private static float _nextRefresh, _scroll;
    private static Predictor.Odds _odds;
    private static Contract _contract;
    private static List<(ushort id, double p, bool owned)> _rows = new();
    private static Dictionary<ushort, Catalog.Item> _items = new();

    public static void Update()
    {
        if (GameState.InCase) { _open = false; return; } // panels are lobby/menu only
        if (Input.GetKeyDown(KeyCode.F3)) { _open = !_open; _nextRefresh = 0; }
        if (!_open || Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 1f;

        _contract = Il2Cpp.GameValues.instance?.RandomMazeContract;
        if (_contract == null) { _odds = null; return; }
        _odds = Predictor.Compute(_contract, Rules.Active);
        _items = Catalog.All().GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        _rows = _odds.Items
            .Select(kv => (kv.Key, kv.Value, CosmeticAlert.IsUnlocked(kv.Key) == true))
            .OrderBy(r => r.Item3)            // missing first
            .ThenByDescending(r => r.Value)
            .ToList();
    }

    public static void Draw()
    {
        if (!_open) return;
        var area = new Rect(Screen.width - 560, 80, 540, Mathf.Min(Screen.height - 160, 760));
        Ui.Panel(area);
        float x = area.x + 14, y = area.y + 10, w = area.width - 28;

        Ui.Text(new Rect(x, y, w, 26), Loc.T("Pool de cosméticos (F3)", "Cosmetic pool (F3)"), Ui.Header, Color.white);
        y += 30;

        if (_contract == null || _odds == null)
        {
            Ui.Text(new Rect(x, y, w, 24), Loc.T("Sin caso activo.", "No active case."), Ui.Label, Color.gray);
            return;
        }
        if (_odds.Problem != null)
        {
            Ui.Text(new Rect(x, y, w, 24), $"{_contract.MazeType}: {Loc.T(_odds.Problem, "no data for this map")}.", Ui.Label, Color.gray);
            return;
        }

        string rulesState = Rules.Active
            ? Loc.T("reglas LabyHelper activas", "LabyHelper rules on")
            : Loc.T($"reglas apagadas: {Rules.WhyInactive}", $"rules off: {Rules.WhyInactive}");
        string gate = _odds.RuleApplies
            ? Loc.T($"<color=#70ff90>CASO RANDOM</color>: siempre sale cosmético, {_odds.Allowed.Count} nuevos asignados",
                    $"<color=#70ff90>RANDOM CASE</color>: always drops, {_odds.Allowed.Count} new items assigned")
            : Loc.T("Reglas del juego (caso custom o regla apagada)", "Game rules (custom case or rule off)");
        string party = string.Join(" · ", Party.Members.Select(m => m.HasData
            ? $"{m.Name}: {Catalog.AllIds().Count(id => !m.Owned.Contains(id))} {Loc.T("faltan", "missing")}"
            : $"{m.Name}: {Loc.T("sin datos (sin mod)", "no data (no mod)")}"));
        string chance = _odds.SpawnChance is float sc ? $"{sc * 100:0}%" : "?";
        var rich = new GUIStyle(Ui.Label) { richText = true };
        Ui.Text(new Rect(x, y, w, 22), $"{_contract.MazeType} · {_contract.ContractType} · {rulesState}", rich, Color.white);
        y += 22;
        Ui.Text(new Rect(x, y, w, 22), gate, rich, Color.white);
        y += 22;
        if (party.Length > 0)
        {
            Ui.Text(new Rect(x, y, w, 22), party, Ui.Small, new Color(0.75f, 0.85f, 1f));
            y += 22;
        }
        Ui.Text(new Rect(x, y, w, 22),
            Loc.T($"Aparece cosmético: {chance} · si aparece, nuevo: {_odds.PNew * 100:0}%", $"Cosmetic spawns: {chance} · if it does, new: {_odds.PNew * 100:0}%") +
            (_odds.SeasonalChance > 0 ? $" · {_odds.Event} {_odds.SeasonalChance * 100:0}%" : ""), rich, Color.white);
        y += 28;

        var view = new Rect(x, y, w, area.yMax - y - 10);
        float contentH = _rows.Count * RowH;
        _scroll = Ui.KeyScroll(_scroll, contentH - view.height);
        var scroll = GUI.BeginScrollView(view, new Vector2(0, _scroll), new Rect(0, 0, w - 20, contentH));
        _scroll = scroll.y;

        int first = Mathf.Max(0, (int)(_scroll / RowH));
        int last = Mathf.Min(_rows.Count, first + (int)(view.height / RowH) + 2);
        for (int i = first; i < last; i++)
        {
            var (id, p, owned) = _rows[i];
            float ry = i * RowH;
            Catalog.DrawIcon(new Rect(0, ry + 1, RowH - 2, RowH - 2), id);

            _items.TryGetValue(id, out var item);
            string rarity = item != null ? Catalog.RarityLabel(item.Rarity) : "?";
            Color rc = item != null ? Catalog.RarityColor(item.Rarity) : Color.white;
            string pool = _odds.SeasonalIds.Contains(id) ? Loc.T("temporada", "seasonal") : _odds.MapIds.Contains(id) ? Loc.T("EXCL. MAPA", "MAP EXCL.") : Loc.T("común", "common");

            Ui.Text(new Rect(RowH + 6, ry, 250, RowH), CosmeticAlert.Name(id), owned ? Ui.Label : Ui.Bold, owned ? Color.gray : Color.white);
            Ui.Text(new Rect(RowH + 262, ry, 90, RowH), rarity, Ui.Small, rc);
            Ui.Text(new Rect(RowH + 352, ry, 80, RowH), pool, Ui.Small, Color.gray);
            Ui.Text(new Rect(w - 80, ry, 60, RowH), $"{p * 100:0.0}%", Ui.Bold, owned ? Color.gray : new Color(0.5f, 1f, 0.6f));
        }
        GUI.EndScrollView();
    }
}
