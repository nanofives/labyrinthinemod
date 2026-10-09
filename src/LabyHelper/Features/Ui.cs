using UnityEngine;

namespace LabyHelper.Features;

/// <summary>Small IMGUI building blocks shared by the viewers (manual layout; GUILayout is awkward under IL2CPP).</summary>
internal static class Ui
{
    private static GUIStyle _label, _bold, _header, _small;
    private static Texture2D _panelBg;

    public static GUIStyle Label => _label ??= new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = false };
    public static GUIStyle Bold => _bold ??= new GUIStyle(Label) { fontStyle = FontStyle.Bold };
    public static GUIStyle Header => _header ??= new GUIStyle(Label) { fontSize = 19, fontStyle = FontStyle.Bold };
    public static GUIStyle Small => _small ??= new GUIStyle(Label) { fontSize = 13 };

    /// <summary>Dark translucent panel background.</summary>
    public static void Panel(Rect r)
    {
        if (_panelBg == null)
        {
            _panelBg = new Texture2D(1, 1);
            _panelBg.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.07f, 0.92f));
            _panelBg.Apply();
        }
        GUI.DrawTexture(r, _panelBg);
    }

    public static void Text(Rect r, string text, GUIStyle style, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        GUI.Label(r, text, style);
        GUI.color = prev;
    }

    /// <summary>A toggle-looking button: highlighted when selected.</summary>
    public static bool Tab(Rect r, string text, bool selected)
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = selected ? new Color(0.35f, 0.6f, 1f) : new Color(0.3f, 0.3f, 0.3f);
        bool clicked = GUI.Button(r, text);
        GUI.backgroundColor = prev;
        return clicked;
    }

    /// <summary>Scroll offset driven by keys too, since the cursor is locked during gameplay.</summary>
    public static float KeyScroll(float scroll, float max)
    {
        if (Input.GetKey(KeyCode.PageDown)) scroll += 900f * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.PageUp)) scroll -= 900f * Time.unscaledDeltaTime;
        return Mathf.Clamp(scroll, 0f, Mathf.Max(0f, max));
    }
}
