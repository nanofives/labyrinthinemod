using System;
using System.Collections.Generic;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// IMGUI building blocks shared by the mod's panels (manual layout; GUILayout is awkward under IL2CPP), drawn in the
/// game's own style: the Larke Sans font of its menus, rough-edged panels ("Background 2"), white brush strokes behind
/// the selected or hovered button that fill left to right like the main menu's ("Brush Impact 2"), and the crossplay
/// switch's pill with a brushed knob ("Rounded Circle", "Circle Outline", "Brush Radial 2"). The font and sprites
/// are borrowed from the running game (UiSkin), never shipped; until they're found everything falls back to flat
/// shapes and the default font.
/// </summary>
internal static class Ui
{
    private static GUIStyle _label, _bold, _header, _small, _title, _tab, _button, _blank;
    private static Font _styledWith;
    private static Texture2D _panelBg, _white;

    public static readonly Color Ink = new(0.04f, 0.04f, 0.05f, 1f);         // the game's black brush
    public static readonly Color Paper = new(0.95f, 0.95f, 0.93f, 1f);       // its white strokes and text
    public static readonly Color Dim = new(0.66f, 0.66f, 0.64f, 1f);         // secondary text
    public static readonly Color Accent = new(0.85f, 0.78f, 0.6f, 1f);       // section titles (warm, like the lobby fire)

    private static void EnsureStyles()
    {
        var font = UiSkin.Font;
        if (_label != null && font == _styledWith) return;
        _styledWith = font;
        _label = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = false };
        if (font != null) _label.font = font;
        _bold = new GUIStyle(_label) { fontStyle = font != null ? FontStyle.Normal : FontStyle.Bold }; // Larke is already bold
        _header = new GUIStyle(_bold) { fontSize = 19 };
        _small = new GUIStyle(_label) { fontSize = 13 };
        _title = new GUIStyle(_bold) { fontSize = 42 };
        _tab = new GUIStyle(_bold) { fontSize = 20, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
        _button = new GUIStyle(_tab) { fontSize = 23 };
        _blank = new GUIStyle();
    }

    public static GUIStyle Label { get { EnsureStyles(); return _label; } }
    public static GUIStyle Bold { get { EnsureStyles(); return _bold; } }
    public static GUIStyle Header { get { EnsureStyles(); return _header; } }
    public static GUIStyle Small { get { EnsureStyles(); return _small; } }
    public static GUIStyle Title { get { EnsureStyles(); return _title; } }
    public static GUIStyle TabStyle { get { EnsureStyles(); return _tab; } }

    /// <summary>
    /// How every text of the mod is written (Mariano): capitals, no accents. Larke Sans has no accented letters (the
    /// game falls back to Noto for them), so they came out in another font and size; ñ becomes N, ¿ and ¡ go.
    /// </summary>
    public static string Caps(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // Explicit map: the game runs .NET with invariant globalization, where Normalize() and ToUpperInvariant()
        // leave non-ASCII letters alone.
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char ch in s)
        {
            char c = ch;
            int i = Accented.IndexOf(c);
            if (i >= 0) c = Plain[i];
            else if (c == '¿' || c == '¡') continue;
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    private const string Accented = "áàâäãåÁÀÂÄÃÅéèêëÉÈÊËíìîïÍÌÎÏóòôöõÓÒÔÖÕúùûüÚÙÛÜñÑçÇýÿÝ";
    private const string Plain    = "AAAAAAAAAAAAEEEEEEEEIIIIIIIIOOOOOOOOOOUUUUUUUUNNCCYYY";

    /// <summary>The IMGUI default font (Core.OnGUI swaps the skin's for Larke): for map symbols Larke doesn't have.</summary>
    public static Font SymbolFont { get; set; }

    /// <summary>A copy of a style at another size (cached by the caller).</summary>
    public static GUIStyle Sized(GUIStyle s, int size) => new(s) { fontSize = size };

    // ---- resolution: panels are laid out for 1080p and scaled up on bigger screens ----

    public static float Scale => Mathf.Max(1f, Screen.height / 1080f);
    public static float W => Screen.width / Scale;   // logical screen size inside BeginScaled
    public static float H => Screen.height / Scale;
    private static Matrix4x4 _savedMatrix;

    public static void BeginScaled()
    {
        _savedMatrix = GUI.matrix;
        float k = Scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
    }

    public static void EndScaled() => GUI.matrix = _savedMatrix;

    // ---- surfaces ----

    /// <summary>The game's rough-edged black panel, semi-transparent.</summary>
    public static void Panel(Rect r, float alpha = 0.74f)
    {
        var bg = UiSkin.PanelBg;
        if (bg == null) { FlatPanel(r); return; }
        // The ragged edge is ~1/4 of the slice; keep it about 60 px on big panels and smaller on tooltips.
        float corner = Mathf.Min(64f, Mathf.Min(r.width, r.height) * 0.25f);
        Sliced(r, bg, UiSkin.PanelBorder, corner, new Color(Ink.r, Ink.g, Ink.b, alpha));
    }

    /// <summary>Plain dark translucent rectangle (the minimap, where a ragged edge would hide cells).</summary>
    public static void FlatPanel(Rect r)
    {
        if (_panelBg == null)
        {
            _panelBg = Solid(new Color(0.05f, 0.05f, 0.07f, 0.92f));
        }
        GUI.DrawTexture(r, _panelBg);
    }

    public static void Fill(Rect r, Color c)
    {
        _white ??= Solid(Color.white);
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, _white);
        GUI.color = prev;
    }

    /// <summary>A horizontal brush stroke (stretched), optionally only its left <paramref name="fill"/> part.</summary>
    public static void Stroke(Rect r, Color c, float fill = 1f, bool alt = false)
    {
        if (fill <= 0.001f) return;
        var t = alt ? UiSkin.Stroke2 : UiSkin.Stroke;
        if (t == null) { Fill(new Rect(r.x, r.y + r.height * 0.12f, r.width * fill, r.height * 0.76f), c); return; }
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, r.width * fill, r.height), t, new Rect(0, 0, fill, 1));
        GUI.color = prev;
    }

    /// <summary>Nine-slice draw: <paramref name="border"/> texture pixels per side shown as <paramref name="corner"/> screen pixels.</summary>
    public static void Sliced(Rect r, Texture2D t, float border, float corner, Color c)
    {
        float cx = Mathf.Min(corner, r.width / 2f), cy = Mathf.Min(corner, r.height / 2f);
        float bu = border / t.width, bv = border / t.height;
        float[] xs = { r.x, r.x + cx, r.xMax - cx, r.xMax };
        float[] ys = { r.y, r.y + cy, r.yMax - cy, r.yMax };
        float[] us = { 0f, bu, 1f - bu, 1f };
        float[] vs = { 1f, 1f - bv, bv, 0f }; // texture v runs bottom-up
        var prev = GUI.color;
        GUI.color = c;
        for (int i = 0; i < 3; i++)
        for (int j = 0; j < 3; j++)
        {
            float w = xs[i + 1] - xs[i], h = ys[j + 1] - ys[j];
            if (w <= 0.01f || h <= 0.01f) continue;
            GUI.DrawTextureWithTexCoords(new Rect(xs[i], ys[j], w, h), t, new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]));
        }
        GUI.color = prev;
    }

    private static void Pill(Rect r, Texture2D t, Color c)
    {
        if (t == null) { Fill(r, c); return; }
        Sliced(r, t, t.width / 2f, r.height / 2f, c);
    }

    private static void Knob(Rect r, Color c)
    {
        var t = UiSkin.Knob;
        if (t == null) { Fill(r, c); return; }
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, t);
        GUI.color = prev;
    }

    public static void Text(Rect r, string text, GUIStyle style, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        // Larke Sans sits low in IMGUI's box (tall ascent): lift it so centred text is really centred.
        var font = style.font != null ? style.font : GUI.skin.font;
        if (font != null && font == UiSkin.Font && style.alignment is TextAnchor.MiddleLeft or TextAnchor.MiddleCenter or TextAnchor.MiddleRight)
            r.y -= Mathf.Round((style.fontSize > 0 ? style.fontSize : 14) * 0.14f);
        GUI.Label(r, Caps(text), style); // always capitals without accents (Mariano), like the game's menus
        GUI.color = prev;
    }

    // ---- controls ----

    private static readonly Dictionary<string, float> Anim = new();

    /// <summary>0..1 hover animation per control, eased like the main menu's brush fill.</summary>
    private static float Hover(string key, bool hot)
    {
        Anim.TryGetValue(key, out float v);
        if (Event.current.type == EventType.Repaint)
        {
            v = Mathf.MoveTowards(v, hot ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            Anim[key] = v;
        }
        return v;
    }

    private static bool Clicked(Rect r) { EnsureStyles(); return GUI.Button(r, GUIContent.none, _blank); }

    /// <summary>An invisible click area.</summary>
    public static bool Hit(Rect r) => Clicked(r);

    private static bool MouseIn(Rect r) => r.Contains(Event.current.mousePosition);

    /// <summary>A tab: white brush behind the selected one (black text), the brush filling in on hover.</summary>
    public static bool Tab(Rect r, string text, bool selected, GUIStyle style = null)
    {
        EnsureStyles();
        style ??= _tab;
        float h = Hover("tab:" + text + r.x + r.y, MouseIn(r));
        if (selected) Stroke(r, Paper);
        else Stroke(r, new Color(Paper.r, Paper.g, Paper.b, 0.22f), Ease(h));
        Text(r, Caps(text), style, selected ? Ink : Color.Lerp(Dim, Paper, h));
        return Clicked(r);
    }

    /// <summary>A button: faint brush at rest, white brush filling in on hover (text turns black, like the menu).</summary>
    public static bool Button(Rect r, string text, bool enabled = true)
    {
        EnsureStyles();
        float h = enabled ? Hover("btn:" + text + r.x + r.y, MouseIn(r)) : 0f;
        Stroke(r, new Color(Paper.r, Paper.g, Paper.b, enabled ? 0.16f : 0.07f), 1f, alt: true);
        Stroke(r, Paper, Ease(h), alt: true);
        Text(r, Caps(text), _button, !enabled ? new Color(0.45f, 0.45f, 0.45f) : h > 0.55f ? Ink : Paper);
        return Clicked(r) && enabled;
    }

    /// <summary>Close button (top right of a panel).</summary>
    public static bool Close(Rect r)
    {
        EnsureStyles();
        float h = Hover("close" + r.x + r.y, MouseIn(r));
        Knob(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), new Color(Paper.r, Paper.g, Paper.b, 0.12f + 0.88f * h));
        Text(r, "X", _tab, h > 0.5f ? Ink : Paper);
        return Clicked(r);
    }

    /// <summary>The crossplay-style switch: outlined pill, knob left when off; filled pill, dark knob right when on.</summary>
    public static bool Switch(Rect r, bool on, string key)
    {
        float h = Hover("sw:" + key, MouseIn(r));
        float t = Ease(Anim.TryGetValue("swv:" + key, out var v) ? v : (on ? 1f : 0f));
        if (Event.current.type == EventType.Repaint) Anim["swv:" + key] = Mathf.MoveTowards(t, on ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        var paper = Paper;
        if (h > 0f) Pill(r, UiSkin.Round, new Color(0.49f, 0.49f, 0.49f, 0.35f * h));
        Pill(r, UiSkin.Round, new Color(paper.r, paper.g, paper.b, 0.92f * t));
        Pill(r, UiSkin.Ring, paper);
        float k = r.height - 10f;
        var knob = new Rect(Mathf.Lerp(r.x + 5f, r.xMax - 5f - k, t), r.y + 5f, k, k);
        Knob(knob, Color.Lerp(paper, Ink, t));
        return Clicked(r) ? !on : on;
    }

    private static GUIStyle _arrow;
    private static int _dragId;

    /// <summary>The slider with this key is being dragged.</summary>
    public static bool IsDragging(string key) => _dragId != 0 && _dragId == key.GetHashCode();

    /// <summary>Thin track with a brushed knob; drag anywhere on it.</summary>
    public static float Slider(Rect r, float value, float min, float max, string key)
    {
        int id = key.GetHashCode();
        var e = Event.current;
        float knob = Mathf.Min(24f, r.height);
        float x0 = r.x + knob / 2f, x1 = r.xMax - knob / 2f;
        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
        {
            _dragId = id;
            value = Mathf.Lerp(min, max, Mathf.InverseLerp(x0, x1, e.mousePosition.x));
            e.Use();
        }
        else if (_dragId == id)
        {
            if (e.type == EventType.MouseUp || !Input.GetMouseButton(0)) _dragId = 0;
            else value = Mathf.Lerp(min, max, Mathf.InverseLerp(x0, x1, e.mousePosition.x));
            if (e.type == EventType.MouseDrag) e.Use();
        }

        float f = Mathf.InverseLerp(min, max, value);
        float cy = r.y + r.height / 2f;
        float h = Hover("sl:" + key, MouseIn(r) || _dragId == id);
        Pill(new Rect(x0 - 3, cy - 3, x1 - x0 + 6, 6), UiSkin.Round, new Color(Paper.r, Paper.g, Paper.b, 0.22f));
        Pill(new Rect(x0 - 3, cy - 3, (x1 - x0) * f + 6, 6), UiSkin.Round, new Color(Paper.r, Paper.g, Paper.b, 0.75f + 0.25f * h));
        float ks = knob * (0.85f + 0.15f * h);
        Knob(new Rect(Mathf.Lerp(x0, x1, f) - ks / 2f, cy - ks / 2f, ks, ks), Paper);
        return Mathf.Clamp(value, min, max);
    }

    /// <summary>"&lt; VALUE &gt;" picker like the game's settings; returns -1, 0 or +1.</summary>
    public static int Arrows(Rect r, string value, string key)
    {
        EnsureStyles();
        float aw = r.height;
        var left = new Rect(r.x, r.y, aw, r.height);
        var right = new Rect(r.xMax - aw, r.y, aw, r.height);
        int step = 0;
        float hl = Hover("al:" + key, MouseIn(left)), hr = Hover("ar:" + key, MouseIn(right));
        _arrow ??= new GUIStyle(_tab) { fontSize = 30, font = SymbolFont, alignment = TextAnchor.MiddleCenter };
        Knob(left, new Color(Paper.r, Paper.g, Paper.b, 0.18f + 0.82f * hl));
        Knob(right, new Color(Paper.r, Paper.g, Paper.b, 0.18f + 0.82f * hr));
        Text(left, "◀", _arrow, hl > 0.5f ? Ink : Paper);
        Text(right, "▶", _arrow, hr > 0.5f ? Ink : Paper);
        Text(new Rect(r.x + aw, r.y, r.width - 2 * aw, r.height), Caps(value), _button, Paper);
        if (Clicked(left)) step = -1;
        if (Clicked(right)) step = 1;
        if (step == 0 && Clicked(new Rect(r.x + aw, r.y, r.width - 2 * aw, r.height))) step = 1;
        return step;
    }

    /// <summary>A soft brush behind the hovered row of a list.</summary>
    public static void RowHover(Rect r, string key)
    {
        float h = Hover("row:" + key, MouseIn(r));
        if (h > 0f) Stroke(r, new Color(Paper.r, Paper.g, Paper.b, 0.07f * h));
    }

    /// <summary>Section title with a brush underline.</summary>
    public static void Section(Rect r, string text, GUIStyle style)
    {
        Text(new Rect(r.x, r.y, r.width, r.height - 8), Caps(text), style, Accent);
        float w = Mathf.Min(r.width, style.CalcSize(new GUIContent(Caps(text))).x + 40f);
        Stroke(new Rect(r.x - 6, r.yMax - 10, w, 8), new Color(Accent.r, Accent.g, Accent.b, 0.55f));
    }

    private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    // ---- scrolling (the default skin's scrollbars don't match; this one is a thin brushed bar) ----

    private static readonly Stack<(Rect view, float contentH, float scroll, string key)> Scrolls = new();

    /// <summary>Clips to <paramref name="view"/> and shifts the content by the scroll; mouse wheel, PageUp/Down and dragging the bar scroll it.</summary>
    public static void BeginScroll(Rect view, ref float scroll, float contentH, string key)
    {
        var e = Event.current;
        float max = Mathf.Max(0f, contentH - view.height);
        if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) { scroll += e.delta.y * 40f; e.Use(); }
        if (view.Contains(e.mousePosition) || Captured) scroll = KeyScroll(scroll, max);
        scroll = Mathf.Clamp(scroll, 0f, max);
        Scrolls.Push((view, contentH, scroll, key));
        GUI.BeginGroup(view);
        GUI.BeginGroup(new Rect(0, -scroll, view.width, Mathf.Max(contentH, view.height)));
    }

    public static void EndScroll(ref float scroll)
    {
        GUI.EndGroup();
        GUI.EndGroup();
        var (view, contentH, _, key) = Scrolls.Pop();
        if (contentH <= view.height + 1f) return;
        float max = contentH - view.height;
        var track = new Rect(view.xMax - 6, view.y + 4, 4, view.height - 8);
        float thumbH = Mathf.Max(40f, track.height * view.height / contentH);
        var thumb = new Rect(track.x - 1, track.y + (track.height - thumbH) * (scroll / max), 6, thumbH);
        Pill(track, UiSkin.Round, new Color(Paper.r, Paper.g, Paper.b, 0.12f));
        Pill(thumb, UiSkin.Round, new Color(Paper.r, Paper.g, Paper.b, 0.6f));
        // Drag the bar: map the mouse onto the whole track.
        var grab = new Rect(track.x - 10, track.y, 24, track.height);
        int id = ("scroll:" + key).GetHashCode();
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(e.mousePosition)) { _dragId = id; e.Use(); }
        if (_dragId == id)
        {
            if (Input.GetMouseButton(0))
                scroll = Mathf.Clamp01((e.mousePosition.y - track.y - thumbH / 2f) / (track.height - thumbH)) * max;
            else _dragId = 0;
            if (e.type == EventType.MouseDrag) e.Use();
        }
    }

    // ---- input capture ----

    private static readonly HashSet<string> Holders = new();
    private static bool _savedControl = true;
    private static bool? _savedLook;
    private static CursorLockMode _savedLock = CursorLockMode.Locked;
    private static bool _savedVisible;

    /// <summary>
    /// While a panel (F1, F2) is open over gameplay: the player stops moving and looking around and the cursor is free,
    /// the same way the game's own full-screen popups do it (TutorialFullScreenPopup.Show: PlayerControl.CanControl =
    /// false, LabyrinthineInputManager.SetActionSet(menu: true), cursor unlocked). Released when the last panel closes.
    /// Not used when the panel opens from one of the game's menus, which already handle input.
    /// </summary>
    public static void Capture(string who, bool on)
    {
        bool before = Holders.Count > 0;
        // Only over gameplay: menus have no player, and relocking the cursor there would hide it.
        if (on && GameState.InMenu) return;
        // A free cursor means one of the game's menus is up (customization, pause...): it already handles input.
        if (on && Holders.Count == 0 && Cursor.lockState != CursorLockMode.Locked) return;
        if (on) Holders.Add(who); else Holders.Remove(who);
        bool now = Holders.Count > 0;
        if (now == before) return;
        try
        {
            var look = Il2Cpp.PlayerLook.Instance; // the camera has its own switch
            if (now)
            {
                _savedLock = Cursor.lockState;
                _savedVisible = Cursor.visible;
                _savedControl = Il2Cpp.PlayerControl.CanControl;
                Il2Cpp.PlayerControl.CanControl = false;
                _savedLook = look != null ? look.CanControl : null;
                if (look != null) look.CanControl = false;
                Il2Cpp.LabyrinthineInputManager.SetActionSet(true);
            }
            else
            {
                Il2Cpp.PlayerControl.CanControl = _savedControl;
                if (look != null && _savedLook != null) look.CanControl = _savedLook.Value;
                Il2Cpp.LabyrinthineInputManager.SetActionSet(false);
                // Back to how the cursor was when the panel opened (locked in gameplay, free in a game menu).
                Cursor.lockState = _savedLock;
                Cursor.visible = _savedVisible;
            }
        }
        catch (Exception e) { Core.Log.Warning($"Ui: input capture: {e.Message}"); }
    }

    public static bool Captured => Holders.Count > 0;

    /// <summary>Every frame while captured: gameplay keeps relocking the cursor.</summary>
    public static void KeepCursorFree()
    {
        if (Holders.Count == 0) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Scroll offset driven by keys too, since the cursor is locked during gameplay.</summary>
    public static float KeyScroll(float scroll, float max)
    {
        if (Input.GetKey(KeyCode.PageDown)) scroll += 900f * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.PageUp)) scroll -= 900f * Time.unscaledDeltaTime;
        return Mathf.Clamp(scroll, 0f, Mathf.Max(0f, max));
    }

    public static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
