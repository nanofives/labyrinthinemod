using System.Collections.Generic;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Strip across the top of the screen with one key cap per F key and what it does, lit when that feature is on.
/// Drawn by OptionsMenu wherever F1 works (lobby, a case's safe zones, and while the panel is open). In a case it
/// stays clear of the minimap and wraps into more rows if needed.
/// </summary>
internal static class HotkeyBar
{
    private static GUIStyle _key, _label;
    private static Texture2D _keyBg, _keyOn, _barBg;

    private record Entry(string Key, string Label, bool? On);

    private static List<Entry> Entries() => new()
    {
        new("F1", Loc.T("Opciones", "Options"), null),
        new("F2", Loc.T("Colección", "Collection"), null),
        new("F4", Loc.T("Minimapa", "Minimap"), Settings.MinimapEnabled.Value),
        new("F6", Loc.T("Brillo", "Brightness"), Settings.BrightnessEnabled.Value),
        new("F7/F8", Loc.T("Exposición -/+", "Exposure -/+"), null),
        new("F9", Loc.T("Cosmético y jugadores", "Cosmetic and players"), CosmeticAlert.Pinned),
    };

    /// <summary>Draws the bar from y = top; returns its bottom edge.</summary>
    public static float Draw(float top)
    {
        _key ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _label ??= new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleLeft };
        _keyBg ??= Solid(new Color(0.85f, 0.85f, 0.88f, 1f));
        _keyOn ??= Solid(new Color(0.45f, 0.85f, 0.5f, 1f));
        _barBg ??= Solid(new Color(0.05f, 0.05f, 0.07f, 0.8f));

        const float h = 48f, gap = 28f, pad = 16f; // twice the original size (by request)
        // Keep clear of the minimap (top right) during a case.
        float reserved = GameState.InCase && Settings.MinimapEnabled.Value ? Mathf.Clamp(Settings.MinimapSize.Value, 120f, 600f) + 40f : 16f;
        float maxW = Mathf.Max(300f, Screen.width - 2f * reserved);

        // Lay the entries out in rows that fit maxW.
        var rows = new List<List<(Entry e, float kw, float lw)>> { new() };
        float rowW = 0f;
        foreach (var e in Entries())
        {
            float kw = _key.CalcSize(new GUIContent(e.Key)).x + 24f;
            float lw = _label.CalcSize(new GUIContent(e.Label)).x + 2f;
            float w = kw + 10f + lw;
            if (rows[^1].Count > 0 && rowW + gap + w > maxW - 2 * pad) { rows.Add(new()); rowW = 0f; }
            rowW += (rows[^1].Count > 0 ? gap : 0f) + w;
            rows[^1].Add((e, kw, lw));
        }

        float y = top;
        foreach (var row in rows)
        {
            float w = 0f;
            foreach (var (_, kw, lw) in row) w += (w > 0 ? gap : 0f) + kw + 10f + lw;
            float x = Screen.width / 2f - w / 2f;
            GUI.DrawTexture(new Rect(x - pad, y, w + 2 * pad, h + 16f), _barBg);
            foreach (var (e, kw, lw) in row)
            {
                var keyRect = new Rect(x, y + 8f, kw, h);
                GUI.DrawTexture(keyRect, e.On == true ? _keyOn : _keyBg);
                var prev = GUI.color;
                GUI.color = new Color(0.08f, 0.08f, 0.1f);
                GUI.Label(keyRect, e.Key, _key);
                GUI.color = e.On == false ? new Color(0.65f, 0.65f, 0.65f) : Color.white;
                GUI.Label(new Rect(x + kw + 10f, y + 8f, lw, h), e.Label, _label);
                GUI.color = prev;
                x += kw + 10f + lw + gap;
            }
            y += h + 16f;
        }
        return y;
    }

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
