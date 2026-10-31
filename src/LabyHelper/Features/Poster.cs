using System;
using System.IO;
using System.Linq;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace LabyHelper.Features;

/// <summary>
/// A poster on the nearest wall to the case cosmetic (by request): rays go out in 24 directions at eye height from
/// the cosmetic, and the closest near-vertical surface within 6 m gets a framed quad with the picture, facing out.
/// Local only (each player with the mod places their own; same cosmetic and walls, so same spot). The picture is the
/// embedded poster.png, or UserData\LabyHelper.poster.png if present. Off in F1 > Alerts.
/// The frame is an RGB LED strip (by request): unlit dots in a rainbow that runs around the poster, plus 8 small
/// point lights along it, coloured like the dots under them, so the wall and the poster glow around it.
/// </summary>
internal static class Poster
{
    private const float MaxDist = 6f, Size = 2.4f, Eye = 1.6f, Centre = 2.9f;
    private static GameObject _go;
    private static bool _done;
    private static float _next;
    private static Texture2D _tex;

    public static void OnSceneLoaded()
    {
        _done = false;
        _go = null; // destroyed with the scene
        _ledMat = null;
        LedLights.Clear();
        _next = Time.unscaledTime + 4f; // maze modules finish spawning a little after the scene loads
    }

    public static void Update()
    {
        AnimateLeds();
        if (_done || !GameState.InCase || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 2f;
        if (!Settings.PosterNearCosmetic.Value) return;
        var target = CosmeticAlert.PendingTargets().FirstOrDefault();
        if (target.name == null) return; // cosmetic not spawned (or not found) yet
        _done = true;
        try { Place(target.pos); }
        catch (Exception e) { Core.Log.Warning($"Poster: {e.Message}"); }
    }

    private static void Place(Vector3 cosmetic)
    {
        var origin = cosmetic + Vector3.up * Eye;
        RaycastHit best = default;
        float bestDist = float.MaxValue;
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            if (!Physics.Raycast(origin, dir, out var hit, MaxDist, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Mathf.Abs(hit.normal.y) > 0.3f || hit.distance < 0.3f) continue; // floors, ramps, the item itself
            if (hit.collider != null && hit.collider.attachedRigidbody != null) continue; // props that move, players
            if (hit.distance < bestDist) { bestDist = hit.distance; best = hit; }
        }
        if (bestDist == float.MaxValue)
        {
            Core.Log.Msg("Poster: no wall within 6 m of the cosmetic.");
            return;
        }

        var normal = new Vector3(best.normal.x, 0f, best.normal.z).normalized;
        // As high as the wall allows: centre at 2.9 m (bottom edge 1.7 m), lowered while the top edge would stick
        // out above the wall or the poster would run off its sides.
        float centre = Centre;
        for (; centre > Size / 2f + 0.2f; centre -= 0.2f)
            if (WallCovers(best.point, normal, cosmetic.y + centre)) break;
        var at = new Vector3(best.point.x, cosmetic.y + centre, best.point.z);
        _go = new GameObject("LabyHelper Poster");
        _go.transform.position = at + normal * 0.03f;
        _go.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up); // a quad shows its -Z side

        // Dark frame slightly larger, then the picture just in front of it, then the LED strip around it.
        Quad(_go.transform, Size + 0.2f, 0f, Solid(new Color(0.08f, 0.06f, 0.05f)));
        Quad(_go.transform, Size, 0.01f, Picture());
        try { Leds(_go.transform); }
        catch (Exception e) { Core.Log.Warning($"Poster: LED strip: {e.Message}"); }
        Core.Log.Msg($"Poster: on a wall {bestDist:0.0} m from the cosmetic at {best.point}.");
    }

    // ---- LED strip ----

    private const int LedDots = 32;           // dots per side
    private const float LedLoop = 6f;         // seconds for the rainbow to go once along a side
    private const float LedLumen = 220f, LedRange = 3.5f;
    private static Material _ledMat;
    private static Texture2D _ledTex;
    private static readonly System.Collections.Generic.List<(HDAdditionalLightData light, float u)> LedLights = new();

    /// <summary>
    /// Four bars just outside the picture, each a quad whose texture is a row of rainbow dots, rotated so the strip
    /// runs clockwise around the poster; all share one unlit material whose texture offset scrolls (AnimateLeds).
    /// Two point lights per bar (its start corner and its middle) take the colour of the dot there.
    /// </summary>
    private static void Leds(Transform parent)
    {
        LedLights.Clear();
        _ledTex ??= LedTexture();
        var shader = Shader.Find("HDRP/Unlit");
        if (shader == null) throw new Exception("no HDRP/Unlit shader");
        _ledMat = new Material(shader);
        _ledMat.SetTexture("_UnlitColorMap", _ledTex);
        _ledMat.SetColor("_UnlitColor", new Color(3f, 3f, 3f, 1f)); // HDR: brighter than the lit walls around it
        float half = Size / 2f + 0.05f, len = Size + 0.14f, thick = 0.045f;
        // top (left to right), right (top to bottom), bottom (right to left), left (bottom to top)
        var sides = new[] { (new Vector2(0, half), 0f), (new Vector2(half, 0), -90f), (new Vector2(0, -half), 180f), (new Vector2(-half, 0), 90f) };
        foreach (var (pos, angle) in sides)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var col = bar.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            bar.name = "LabyHelper LED";
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(pos.x, pos.y, -0.02f);
            // A quad seen from its -Z side (as the poster is) has +X on the viewer's left: flip so u runs clockwise.
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, angle) * Quaternion.Euler(0f, 0f, 180f);
            bar.transform.localScale = new Vector3(len, thick, 1f);
            var r = bar.GetComponent<MeshRenderer>();
            r.sharedMaterial = _ledMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (float u in new[] { 0.04f, 0.5f })
            {
                var lgo = new GameObject("LabyHelper LED light");
                lgo.transform.SetParent(parent, false);
                // Along the bar in the poster's plane, a little out from the wall so it lights the wall around.
                var along = bar.transform.localRotation * new Vector3((u - 0.5f) * len, 0f, 0f);
                lgo.transform.localPosition = new Vector3(pos.x, pos.y, -0.25f) + along;
                var hd = lgo.AddHDLight(HDLightTypeAndShape.Point);
                hd.SetIntensity(LedLumen, LightUnit.Lumen);
                hd.SetRange(LedRange);
                var light = lgo.GetComponent<Light>();
                if (light != null) light.shadows = LightShadows.None;
                LedLights.Add((hd, u));
            }
        }
        Core.Log.Msg($"Poster: LED strip with {LedLights.Count} lights.");
    }

    /// <summary>One side's dots: lit dot, dark gap, hue going once around the colour wheel.</summary>
    private static Texture2D LedTexture()
    {
        var t = new Texture2D(LedDots * 2, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave,
        };
        for (int i = 0; i < LedDots * 2; i++)
            t.SetPixel(i, 0, i % 2 == 0 ? Color.HSVToRGB((i + 0.5f) / (LedDots * 2f), 1f, 1f) : new Color(0.02f, 0.02f, 0.02f, 1f));
        t.Apply(false, false);
        return t;
    }

    /// <summary>Every frame while the poster exists: scroll the rainbow and recolour the lights to match.</summary>
    private static void AnimateLeds()
    {
        if (_go == null || _ledMat == null) return;
        try
        {
            float offset = Time.time / LedLoop % 1f;
            _ledMat.SetTextureOffset("_UnlitColorMap", new Vector2(-offset, 0f)); // colours flow clockwise
            foreach (var (light, u) in LedLights)
                if (light != null) light.SetColor(Color.HSVToRGB(((u - offset) % 1f + 1f) % 1f, 1f, 1f));
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Poster: LED animation: {e.Message}");
            _ledMat = null;
        }
    }

    /// <summary>The wall is there behind the poster's four corners at this centre height.</summary>
    private static bool WallCovers(Vector3 point, Vector3 normal, float centreY)
    {
        var side = Vector3.Cross(Vector3.up, normal).normalized;
        float h = Size / 2f - 0.05f;
        foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            var corner = new Vector3(point.x, centreY, point.z) + side * (sx * h) + Vector3.up * (sy * h) + normal * 0.5f;
            if (!Physics.Raycast(corner, -normal, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Vector3.Dot(hit.normal, normal) < 0.8f) return false;
        }
        return true;
    }

    private static void Quad(Transform parent, float size, float outward, Texture2D tex)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var col = q.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.Destroy(col);
        q.transform.SetParent(parent, false);
        q.transform.localPosition = new Vector3(0f, 0f, -outward);
        q.transform.localScale = new Vector3(size, size, 1f);
        var r = q.GetComponent<MeshRenderer>();
        r.material = MaterialFor(tex);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>HDRP Lit (so it's dark until a light or the flashlight hits it, like the walls), else whatever lit material the scene uses.</summary>
    private static Material MaterialFor(Texture2D tex)
    {
        var shader = Shader.Find("HDRP/Lit");
        Material m;
        if (shader != null) m = new Material(shader);
        else
        {
            var donor = UnityEngine.Object.FindObjectsOfType<MeshRenderer>()
                .Select(x => x.sharedMaterial).FirstOrDefault(x => x != null && x.HasProperty("_BaseColorMap"));
            if (donor == null) throw new Exception("no HDRP/Lit shader or lit material to copy");
            m = new Material(donor);
        }
        m.SetTexture("_BaseColorMap", tex);
        m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_NormalMap")) m.SetTexture("_NormalMap", null);
        if (m.HasProperty("_MaskMap")) m.SetTexture("_MaskMap", null);
        return m;
    }

    private static Texture2D Picture()
    {
        if (_tex != null) return _tex;
        byte[] bytes = null;
        string custom = Path.Combine(MelonEnvironment.UserDataDirectory, "LabyHelper.poster.png");
        if (File.Exists(custom)) bytes = File.ReadAllBytes(custom);
        else
        {
            using var res = typeof(Poster).Assembly.GetManifestResourceStream("poster.png");
            if (res != null) { using var ms = new MemoryStream(); res.CopyTo(ms); bytes = ms.ToArray(); }
        }
        _tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
        if (bytes == null || !ImageConversion.LoadImage(_tex, bytes)) _tex = Solid(Color.magenta);
        return _tex;
    }

    private static Texture2D Solid(Color c)
    {
        var t = Ui.Solid(c);
        return t;
    }
}
