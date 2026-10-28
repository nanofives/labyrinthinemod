using System;
using System.IO;
using System.Linq;
using MelonLoader.Utils;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// A poster on the nearest wall to the case cosmetic (by request): rays go out in 24 directions at eye height from
/// the cosmetic, and the closest near-vertical surface within 6 m gets a framed quad with the picture, facing out.
/// Local only (each player with the mod places their own; same cosmetic and walls, so same spot). The picture is the
/// embedded poster.png, or UserData\LabyHelper.poster.png if present. Off in F1 > Alerts.
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
        _next = Time.unscaledTime + 4f; // maze modules finish spawning a little after the scene loads
    }

    public static void Update()
    {
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

        // Dark frame slightly larger, then the picture just in front of it.
        Quad(_go.transform, Size + 0.1f, 0f, Solid(new Color(0.08f, 0.06f, 0.05f)));
        Quad(_go.transform, Size, 0.01f, Picture());
        Core.Log.Msg($"Poster: on a wall {bestDist:0.0} m from the cosmetic at {best.point}.");
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
