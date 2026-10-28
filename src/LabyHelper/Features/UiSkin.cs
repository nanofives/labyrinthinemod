using System;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Borrows the game's UI look at runtime: the Larke Sans font (the only TTF the build keeps; its menus use the SDF
/// versions of the same family) and the sprites of its main menu and popups. Each sprite is copied into a texture of
/// our own (scaled down when it's huge) so it survives the menu scene unloading. Looked up every few seconds until all
/// are found; the main menu, where the game always starts, has them all.
/// </summary>
internal static class UiSkin
{
    public static Font Font { get; private set; }
    public static Texture2D PanelBg { get; private set; }   // "Background 2 (400ppu)": rough-edged rectangle, 9-slice
    public static float PanelBorder { get; private set; }   // its slice border in our copy's pixels
    public static Texture2D Stroke { get; private set; }    // "Brush Impact 2": menu button brush
    public static Texture2D Stroke2 { get; private set; }   // "Brush Impact": the crossplay strip's brush
    public static Texture2D Knob { get; private set; }      // "Brush Radial 2": brushed dot
    public static Texture2D Round { get; private set; }     // "Rounded Circle 64px (350ppu)": solid circle, 9-slice into pills
    public static Texture2D Ring { get; private set; }      // "Circle Outline 64px - Stroke 8px (350ppu)"

    private static float _next = 2f;
    private static int _tries;

    private static bool Complete => Font != null && PanelBg != null && Stroke != null && Stroke2 != null && Knob != null && Round != null && Ring != null;

    public static void Update()
    {
        if (Complete || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 3f;
        _tries++;
        try
        {
            if (Font == null)
                foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                    if (f != null && f.name.StartsWith("Larke", StringComparison.OrdinalIgnoreCase))
                    {
                        // A copy of our own: the lobby's text uses this same dynamic font, and sharing its glyph atlas
                        // made both sides evict each other's glyphs every frame (garbled F-key bar in the lobby).
                        var own = UnityEngine.Object.Instantiate(f);
                        own.name = "Larke Sans Bold (LabyHelper)";
                        own.hideFlags = HideFlags.HideAndDontSave;
                        bool works;
                        try { works = own.dynamic && own.HasCharacter('A'); } // GetCharacterInfo is stripped from this build
                        catch { works = true; }
                        Core.Log.Msg(works ? "UiSkin: using a private copy of Larke Sans." : "UiSkin: the font copy has no glyphs; sharing the game's.");
                        if (!works) { UnityEngine.Object.Destroy(own); own = f; }
                        Font = own;
                        Font.add_textureRebuilt((Action<Font>)OnRebuilt);
                        break;
                    }

            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null) continue;
                switch (s.name)
                {
                    case "Background 2 (400ppu)" when PanelBg == null:
                        PanelBg = Copy(s, 960);
                        if (PanelBg != null) PanelBorder = s.border.x * PanelBg.width / s.textureRect.width;
                        break;
                    case "Brush Impact 2" when Stroke == null: Stroke = Copy(s, 512); break;
                    case "Brush Impact" when Stroke2 == null: Stroke2 = Copy(s, 512); break;
                    case "Brush Radial 2" when Knob == null: Knob = Copy(s, 128); break;
                    case "Rounded Circle 64px (350ppu)" when Round == null: Round = Copy(s, 128); break;
                    case "Circle Outline 64px - Stroke 8px (350ppu)" when Ring == null: Ring = Copy(s, 128); break;
                }
            }
            if (Complete) Core.Log.Msg("UiSkin: game font and sprites ready.");
            else if (_tries == 20)
                Core.Log.Warning($"UiSkin: still missing (font {Font != null}, panel {PanelBg != null}, brush {Stroke != null}/{Stroke2 != null}, knob {Knob != null}, pill {Round != null}/{Ring != null}); flat fallback meanwhile.");
        }
        catch (Exception e) { Core.Log.Warning($"UiSkin: {e.Message}"); _next = Time.unscaledTime + 30f; }
    }

    private static int _rebuilds;
    private static float _rebuildWindow;
    private static bool _warned;

    /// <summary>A glyph atlas rebuild now and then is normal (new size or letters); dozens a second means a fight over it.</summary>
    private static void OnRebuilt(Font f)
    {
        if (f != Font) return;
        if (Time.unscaledTime - _rebuildWindow > 10f) { _rebuildWindow = Time.unscaledTime; _rebuilds = 0; }
        if (++_rebuilds > 40 && !_warned)
        {
            _warned = true;
            Core.Log.Warning("UiSkin: the font atlas is rebuilding every frame (text may look garbled).");
        }
    }

    /// <summary>The sprite's pixels in a texture of our own, at most <paramref name="maxW"/> wide.</summary>
    private static Texture2D Copy(Sprite s, int maxW)
    {
        var src = s.texture;
        if (src == null) return null;
        var r = s.textureRect;
        float k = Mathf.Min(1f, maxW / r.width);
        int w = Mathf.Max(1, Mathf.RoundToInt(r.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(r.height * k));
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(src, rt, new Vector2(r.width / src.width, r.height / src.height), new Vector2(r.x / src.width, r.y / src.height));
            RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            t.Apply(true);
            return t;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
