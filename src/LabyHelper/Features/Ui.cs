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

    private static readonly System.Collections.Generic.HashSet<string> Holders = new();
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
        catch (System.Exception e) { Core.Log.Warning($"Ui: input capture: {e.Message}"); }
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
}
