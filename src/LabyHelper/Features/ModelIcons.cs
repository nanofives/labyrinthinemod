using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Il2CppCharacterCustomization;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace LabyHelper.Features;

/// <summary>
/// Icons rendered from an item's 3D model, for items whose icon sprite can't be drawn (8 music discs have no icon
/// at all; every item has a model via CustomizationItem.AssetReference).
/// One item at a time: AssetReference.InstantiateAsync far below the world, renderers moved to an isolated layer
/// with their materials swapped for unlit copies of the same texture (no lights or exposure to tune), a disabled
/// HDRP camera renders it once into a 128x128 RenderTexture, then everything is destroyed. Results are cached.
/// </summary>
internal static class ModelIcons
{
    private const int Layer = 31;
    private const int Size = 128;
    private static readonly Vector3 Spot = new(0f, -5000f, 0f);

    private static readonly Dictionary<ushort, RenderTexture> Done = new();
    private static readonly HashSet<ushort> Failed = new();
    private static readonly Queue<ushort> Pending = new();
    private static readonly HashSet<ushort> Queued = new();
    private static bool _busy;
    private static Shader _unlit;

    /// <summary>The rendered icon, or null (and queues it) while it isn't ready.</summary>
    public static Texture Get(ushort id)
    {
        if (Done.TryGetValue(id, out var rt) && rt != null && rt.IsCreated()) return rt;
        if (!Failed.Contains(id) && Queued.Add(id)) Pending.Enqueue(id);
        return null;
    }

    public static void Update()
    {
        if (_busy || Pending.Count == 0 || GameState.InCase) return;
        _busy = true;
        MelonCoroutines.Start(Render(Pending.Dequeue()));
    }

    private static IEnumerator Render(ushort id)
    {
        GameObject model = null, camGo = null;
        try
        {
            var item = Catalog.ItemById(id);
            var ar = item?.AssetReference;
            if (ar == null || !ar.RuntimeKeyIsValid()) { Fail(id, "no model reference"); yield break; }

            var handle = ar.InstantiateAsync(Spot, Quaternion.identity, null);
            float until = Time.unscaledTime + 10f;
            while (!handle.IsDone && Time.unscaledTime < until) yield return null;
            model = handle.IsDone ? handle.Result : null;
            if (model == null) { Fail(id, "model didn't load"); yield break; }

            foreach (var b in model.GetComponentsInChildren<Behaviour>(true)) b.enabled = false; // no gameplay scripts
            var renderers = model.GetComponentsInChildren<Renderer>(true).Where(r => r != null).ToList();
            if (renderers.Count == 0) { Fail(id, "model has no renderers"); yield break; }
            foreach (var r in renderers)
            {
                r.gameObject.layer = Layer;
                r.enabled = true;
                r.sharedMaterials = r.sharedMaterials.Select(Unlit).ToArray();
            }
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);

            camGo = new GameObject("LabyHelper_IconCamera");
            var cam = camGo.AddComponent<Camera>();
            var hd = camGo.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = new Color(0, 0, 0, 0);
            hd.volumeLayerMask = 0; // no fog/exposure volumes
            cam.enabled = false;
            cam.cullingMask = 1 << Layer;
            cam.orthographic = true;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
            cam.orthographicSize = radius * 1.05f;
            var dir = new Vector3(0.6f, 0.45f, -1f).normalized;
            cam.transform.position = bounds.center + dir * radius * 4f;
            cam.transform.LookAt(bounds.center);
            cam.nearClipPlane = radius * 0.5f;
            cam.farClipPlane = radius * 8f;

            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = $"LabyHelper_Icon_{id}" };
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            Done[id] = rt;
            Core.Log.Msg($"ModelIcons: rendered #{id} {CosmeticAlert.Name(id)} ({renderers.Count} renderers).");
        }
        finally
        {
            if (camGo != null) Object.Destroy(camGo);
            if (model != null) Object.Destroy(model);
            _busy = false;
        }
    }

    private static Material Unlit(Material source)
    {
        _unlit ??= Shader.Find("HDRP/Unlit");
        if (source == null || _unlit == null) return source;
        var m = new Material(_unlit);
        Texture tex = null;
        foreach (var prop in new[] { "_BaseColorMap", "_MainTex", "_UnlitColorMap", "_BaseMap" })
            if (source.HasProperty(prop) && (tex = source.GetTexture(prop)) != null) break;
        var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                  : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
        if (tex != null) m.SetTexture("_UnlitColorMap", tex);
        m.SetColor("_UnlitColor", new Color(color.r, color.g, color.b, 1f));
        return m;
    }

    private static void Fail(ushort id, string why)
    {
        Failed.Add(id);
        Core.Log.Msg($"ModelIcons: #{id} {CosmeticAlert.Name(id)}: {why}.");
    }
}
