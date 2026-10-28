using System.Collections.Generic;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Strip across the top of the screen with one key cap per F key and what it does, lit when that feature is on.
/// Drawn by OptionsMenu all the time (menus, lobby, cases), in the game's brush style. In a case it
/// stays clear of the minimap and wraps into more rows if needed.
/// </summary>
internal static class HotkeyBar
{
    private static GUIStyle _key, _label;

    private record Entry(string Key, string Label, bool? On);

    private static List<Entry> Entries() => new()
    {
        new("F1", Loc.T("Opciones", "Options"), null),
        new("F2", Loc.T("Colección", "Collection"), null),
        new("M", Loc.T("Minimapa grande y ruta", "Big minimap and route"), null),
        new("TAB", Loc.T("Leyenda", "Legend"), null),
        new("F6", Loc.T("Brillo", "Brightness"), Settings.BrightnessEnabled.Value),
        new("F7/F8", Loc.T("Exposición -/+", "Exposure -/+"), null),
        new("F9", Loc.T("Cosmético y jugadores", "Cosmetic and players"), CosmeticAlert.Pinned),
    };

    private static Font _font;

    /// <summary>Draws the bar from y = top; returns its bottom edge.</summary>
    public static float Draw(float top)
    {
        if (_key == null || _font != UiSkin.Font)
        {
            _font = UiSkin.Font;
            _key = new GUIStyle(Ui.Bold) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            _label = new GUIStyle(Ui.Label) { fontSize = 26, alignment = TextAnchor.MiddleLeft };
        }

        const float h = 48f, gap = 34f, pad = 40f; // twice the original size (by request)
        // Keep clear of the minimap (top right) during a case.
        // The minimap is drawn in real pixels; this bar in the panels' scaled layout.
        float reserved = GameState.InCase && Settings.MinimapEnabled.Value ? (Mathf.Clamp(Settings.MinimapSize.Value, 120f, 600f) + 40f) / Ui.Scale : 16f;
        float maxW = Mathf.Max(300f, Ui.W - 2f * reserved);

        // Lay the entries out in rows that fit maxW.
        var rows = new List<List<(Entry e, float kw, float lw)>> { new() };
        float rowW = 0f;
        foreach (var e in Entries())
        {
            float kw = _key.CalcSize(new GUIContent(e.Key)).x + 34f;
            float lw = _label.CalcSize(new GUIContent(Ui.Caps(e.Label))).x + 2f;
            float w = kw + 12f + lw;
            if (rows[^1].Count > 0 && rowW + gap + w > maxW - 2 * pad) { rows.Add(new()); rowW = 0f; }
            rowW += (rows[^1].Count > 0 ? gap : 0f) + w;
            rows[^1].Add((e, kw, lw));
        }

        float y = top;
        foreach (var row in rows)
        {
            float w = 0f;
            foreach (var (_, kw, lw) in row) w += (w > 0 ? gap : 0f) + kw + 12f + lw;
            float x = Ui.W / 2f - w / 2f;
            // Black brush strip behind the row, like the game's crossplay and name tags.
            Ui.Stroke(new Rect(x - pad, y - 2f, w + 2 * pad, h + 20f), new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.85f), 1f, alt: true);
            foreach (var (e, kw, lw) in row)
            {
                // Key cap: a white brush dab when the feature is on, grey otherwise.
                var keyRect = new Rect(x, y + 8f, kw, h);
                Ui.Stroke(keyRect, e.On == false ? new Color(0.55f, 0.55f, 0.55f, 1f) : e.On == true ? Ui.Paper : new Color(0.82f, 0.82f, 0.8f, 1f));
                Ui.Text(keyRect, e.Key, _key, Ui.Ink);
                Ui.Text(new Rect(x + kw + 12f, y + 8f, lw, h), e.Label, _label, e.On == false ? Ui.Dim : Ui.Paper);
                x += kw + 12f + lw + gap;
            }
            y += h + 16f;
        }
        return y;
    }
}
