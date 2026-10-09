using System;
using System.Collections.Generic;
using UnityEngine;

namespace LabyHelper;

/// <summary>On-screen messages drawn with IMGUI from Core.OnGUI. Each slot has its own area so they don't overwrite each other.</summary>
internal static class Toast
{
    public enum Slot { Info, Alert }

    /// <summary>A row with a picture: the item's icon (the game's render of the 3D model) over its rarity frame.</summary>
    public record ItemRow(Sprite Icon, Sprite Frame, string Text);

    private class Entry
    {
        public string Text;
        public List<ItemRow> Rows;
        public string Footer;
        public float Until;
    }

    private const float IconSize = 72f;
    private static readonly Dictionary<Slot, Entry> Entries = new()
    {
        [Slot.Info] = new Entry(),
        [Slot.Alert] = new Entry(),
    };
    private static GUIStyle _info, _alert;

    public static void Show(string text, float seconds = 2.5f, Slot slot = Slot.Info)
    {
        var e = Entries[slot];
        e.Text = text;
        e.Rows = null;
        e.Footer = null;
        e.Until = Time.unscaledTime + seconds;
    }

    public static void ShowItems(string header, List<ItemRow> rows, string footer, float seconds)
    {
        var e = Entries[Slot.Info];
        e.Text = header;
        e.Rows = rows;
        e.Footer = footer;
        e.Until = Time.unscaledTime + seconds;
    }

    public static void Draw()
    {
        _info ??= new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
        _alert ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };

        var info = Entries[Slot.Info];
        if (info.Text != null && Time.unscaledTime <= info.Until)
        {
            float y = 20;
            y = DrawText(20, y, info.Text);
            if (info.Rows != null)
            {
                foreach (var row in info.Rows)
                {
                    DrawItemIcon(new Rect(20, y, IconSize, IconSize), row.Icon, row.Frame);
                    DrawShadowed(new Rect(20 + IconSize + 12, y, 1100, IconSize), row.Text, _info, Color.white);
                    y += IconSize + 6;
                }
            }
            if (info.Footer != null) DrawText(20, y, info.Footer);
        }

        var alert = Entries[Slot.Alert];
        if (alert.Text != null && Time.unscaledTime <= alert.Until)
            DrawShadowed(new Rect(0, Screen.height * 0.12f, Screen.width, 40), alert.Text, _alert, new Color(1f, 0.45f, 0.35f));
    }

    private static float DrawText(float x, float y, string text)
    {
        float h = 28 * text.Split('\n').Length + 4;
        DrawShadowed(new Rect(x, y, 1200, h), text, _info, Color.white);
        return y + h;
    }

    private static readonly Dictionary<IntPtr, Rect> UvCache = new();

    /// <summary>
    /// Draws a sprite that may live in an atlas. Rect-packed sprites expose textureRect; tight-packed ones throw on
    /// it, so we fall back to the bounding box of the sprite's mesh UVs (already in atlas space). False if nothing
    /// could be drawn.
    /// </summary>
    public static bool DrawSprite(Rect r, Sprite s)
    {
        if (s == null) return false;
        try
        {
            var tex = s.texture;
            if (tex == null) { LogFailure(s, "no texture"); return false; }
            if (!UvCache.TryGetValue(s.Pointer, out var uv))
            {
                try
                {
                    var tr = s.textureRect;
                    uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
                }
                catch
                {
                    var uvs = s.uv;
                    if (uvs == null || uvs.Length == 0) { LogFailure(s, "no textureRect and no uv"); return false; }
                    float minX = 1, minY = 1, maxX = 0, maxY = 0;
                    foreach (var p in uvs)
                    {
                        minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
                        maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
                    }
                    uv = Rect.MinMaxRect(minX, minY, maxX, maxY);
                }
                UvCache[s.Pointer] = uv;
            }
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            return true;
        }
        catch (Exception e) { LogFailure(s, e.GetType().Name + ": " + e.Message); return false; }
    }

    private static GUIStyle _placeholder;

    /// <summary>Icon over its rarity frame; a labelled placeholder (e.g. ♪ for music) when there's no drawable icon.</summary>
    public static void DrawItemIcon(Rect r, Sprite icon, Sprite frame, string placeholder = null)
    {
        if (frame == null || !DrawSprite(r, frame)) GUI.Box(r, GUIContent.none);
        float pad = r.width * 0.1f;
        var inner = new Rect(r.x + pad, r.y + pad, r.width - 2 * pad, r.height - 2 * pad);
        if (DrawSprite(inner, icon) || placeholder == null) return;
        DrawPlaceholder(inner, placeholder);
    }

    public static void DrawPlaceholder(Rect inner, string placeholder)
    {
        _placeholder ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        _placeholder.fontSize = Mathf.Max(9, (int)(inner.height * (placeholder.Length <= 2 ? 0.5f : 0.22f)));
        GUI.Label(inner, placeholder, _placeholder);
    }

    private static readonly HashSet<IntPtr> LoggedFailures = new();

    private static void LogFailure(Sprite s, string why)
    {
        try
        {
            if (LoggedFailures.Count < 40 && LoggedFailures.Add(s.Pointer))
                LabyHelper.Core.Log.Msg($"Icon '{s.name}' not drawable: {why}");
        }
        catch { }
    }

    private static void DrawShadowed(Rect rect, string text, GUIStyle style, Color color)
    {
        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);
        GUI.color = color;
        GUI.Label(rect, text, style);
        GUI.color = Color.white;
    }
}
