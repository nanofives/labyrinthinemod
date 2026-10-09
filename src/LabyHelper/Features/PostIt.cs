using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// A yellow post-it with an item's icon, stuck under a case folder's info text on the lobby board.
/// The icon (an atlas sprite, not CPU-readable) is composited onto yellow paper in a RenderTexture with
/// Graphics.DrawTexture, then shown on a quad parented to the TMP text, so it inherits the folder's
/// position, rotation and scale. The quad reuses the folder photo's material when possible (known to render
/// in HDRP), falling back to HDRP/Unlit.
/// </summary>
internal static class PostIt
{
    private const string ObjectName = "LabyHelper_PostIt";
    private const int TexSize = 256;
    private static readonly Color Paper = new(1f, 0.93f, 0.45f, 1f);
    private static readonly Dictionary<ushort, RenderTexture> Cache = new();
    private static bool _logged;

    /// <summary>Place or update the post-it under <paramref name="text"/>; itemId null removes it.</summary>
    public static void Attach(TMP_Text text, ushort? itemId, Renderer photoTemplate)
    {
        if (text == null) return;
        var existing = text.transform.Find(ObjectName);

        if (itemId == null || !Settings.CaseBoardPostIt.Value)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }

        var tex = Compose(itemId.Value);
        if (tex == null) return;

        GameObject go;
        Renderer renderer;
        if (existing != null)
        {
            go = existing.gameObject;
            renderer = go.GetComponent<MeshRenderer>();
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = ObjectName;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            go.transform.SetParent(text.transform, false);
            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MakeMaterial(photoTemplate);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.layer = text.gameObject.layer;
        }
        go.SetActive(true);

        var mat = renderer.material;
        mat.mainTexture = tex;
        mat.mainTextureScale = Vector2.one;
        mat.mainTextureOffset = Vector2.zero;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", Color.white);
        if (mat.HasProperty("_UnlitColorMap")) mat.SetTexture("_UnlitColorMap", tex);

        Place(text, go.transform);
    }

    /// <summary>Square below the rendered text, centred, sized from the text's line height.</summary>
    private static void Place(TMP_Text text, Transform postIt)
    {
        // ignoreActiveState: the board text may not have been meshed yet when UpdateUI runs.
        text.ForceMeshUpdate(true, true);
        Bounds b = text.textBounds;
        float line = Mathf.Max(text.fontSize, 0.0001f);
        if (text.TryCast<TextMeshPro>() != null) line *= 0.1f; // 3D TMP: 10 pt = 1 local unit

        float centerX, bottom;
        bool valid = b.extents.x > 0 && b.extents.y > 0 && b.extents.y < 1e6f;
        if (valid)
        {
            centerX = b.center.x;
            bottom = b.min.y;
        }
        else
        {
            // Empty bounds (text not rendered yet): fall back to the text box.
            var r = text.rectTransform.rect;
            centerX = r.center.x;
            bottom = r.yMin;
        }

        // Sized in text lines so it scales with the folder; small enough to read it zoomed in on the reroll view.
        // Fixed size in the text's local units: TMP auto-sizes the folder font with its content, so tying the
        // post-it to the font made it grow once we stopped adding text. 3 (the setting) -> 0.21 local, ~25 cm.
        float size = 0.07f * Mathf.Clamp(Settings.CaseBoardPostItSize.Value, 0.5f, 6f);
        float gap = line * 0.25f;
        postIt.localScale = new Vector3(size, size, 1f);
        // A slight turn sells the "stuck on" look; pull it towards the viewer to avoid z-fighting.
        postIt.localRotation = Quaternion.Euler(0, 0, -4f);
        postIt.localPosition = new Vector3(centerX, bottom - gap - size / 2f, -line * 0.05f);
        if (!_logged)
        {
            _logged = true;
            Core.Log.Msg($"PostIt: {(valid ? "text bounds" : "rect fallback")} bottom {bottom:0.###}, size {size:0.###} (local), " +
                         $"world pos {postIt.position}");
        }
    }

    private static Material MakeMaterial(Renderer photoTemplate)
    {
        Material m = null;
        try
        {
            if (photoTemplate != null && photoTemplate.sharedMaterial != null)
                m = new Material(photoTemplate.sharedMaterial);
        }
        catch { /* fall through */ }
        if (m == null)
        {
            var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Texture");
            m = new Material(shader);
        }
        m.name = ObjectName;
        return m;
    }

    /// <summary>Icon on yellow paper with a thin darker top band (the sticky strip).</summary>
    private static RenderTexture Compose(ushort itemId)
    {
        if (Cache.TryGetValue(itemId, out var cached) && cached != null && cached.IsCreated()) return cached;

        var (icon, _) = CosmeticAlert.Visuals(itemId);
        if (icon == null || icon.texture == null) return null;

        var rt = new RenderTexture(TexSize, TexSize, 0, RenderTextureFormat.ARGB32) { name = $"{ObjectName}_{itemId}" };
        rt.Create();
        var prev = RenderTexture.active;
        bool pushed = false;
        try
        {
            RenderTexture.active = rt;
            GL.PushMatrix();
            pushed = true;
            GL.LoadPixelMatrix(0, TexSize, TexSize, 0);
            GL.Clear(true, true, Paper);

            var strip = new Texture2D(1, 1);
            strip.SetPixel(0, 0, new Color(0.93f, 0.82f, 0.3f, 1f));
            strip.Apply();
            Graphics.DrawTexture(new Rect(0, 0, TexSize, TexSize * 0.12f), strip);

            var tex = icon.texture;
            var tr = icon.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            float pad = TexSize * 0.14f;
            Graphics.DrawTexture(new Rect(pad, pad + TexSize * 0.04f, TexSize - 2 * pad, TexSize - 2 * pad), tex, uv, 0, 0, 0, 0);
        }
        catch (Exception e)
        {
            Core.Log.Warning($"PostIt: compose failed for {itemId}: {e.Message}");
        }
        finally
        {
            if (pushed) GL.PopMatrix();
            RenderTexture.active = prev;
        }
        Cache[itemId] = rt;
        return rt;
    }
}
