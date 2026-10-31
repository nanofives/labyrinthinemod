using System.Linq;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// A huge shiny banner across the top when everyone has picked up the case cosmetic (by request). Fires once per case,
/// on the change from "someone still has to pick it up" to "nobody" (CosmeticAlert.StillToPickUp, which knows only the
/// players with the mod; those without it are named under the banner). Gold text that pops in, with a pulsing glow
/// and a white shine sweeping across, then fades out.
/// </summary>
internal static class AllPickedBanner
{
    private const float Seconds = 7f;
    private static bool _sawMissing, _shown;
    private static float _since = -100f, _next;
    private static string _footer;
    private static GUIStyle _big, _small;

    public static void OnSceneLoaded()
    {
        _sawMissing = _shown = false;
        _since = -100f;
    }

    public static void Update()
    {
        if (_shown || !GameState.InCase || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.5f;
        if (!CosmeticAlert.CaseHasCosmetic()) return;
        int missing = CosmeticAlert.StillToPickUp().Count;
        if (missing > 0) { _sawMissing = true; return; }
        if (!_sawMissing) return; // nothing seen missing yet (cosmetic just spawned, party data not in)
        _shown = true;
        _since = Time.unscaledTime;
        var unknown = Party.Members.Where(m => !m.IsMe && !m.HasData).Select(m => m.Name).ToList();
        _footer = unknown.Count > 0 ? Loc.T("Sin datos (sin el mod): ", "Unknown (no mod): ") + string.Join(", ", unknown) : null;
        Core.Log.Msg("AllPickedBanner: everyone picked up the cosmetic.");
    }

    public static void Draw()
    {
        float t = Time.unscaledTime - _since;
        if (t < 0f || t > Seconds) return;
        float alpha = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((Seconds - t) / 0.8f);
        float pop = 1f + 0.35f * Mathf.Exp(-t * 6f) * Mathf.Cos(t * 18f); // springs in

        Ui.BeginScaled();
        try
        {
            int size = Mathf.RoundToInt(96 * pop);
            _big ??= new GUIStyle(Ui.Title) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            _big.fontSize = size;
            _small ??= new GUIStyle(Ui.Label) { alignment = TextAnchor.MiddleCenter, fontSize = 30 };
            string text = Ui.Caps(Loc.T("Todos agarraron el cosmetico!", "Everyone picked up the cosmetic!"));
            var r = new Rect(0, Ui.H * 0.16f - size, Ui.W, size * 2f);

            // Glow: the text smeared in a ring of soft gold copies, breathing.
            float glow = 0.5f + 0.5f * Mathf.Sin(t * 6f);
            var gold = new Color(1f, 0.78f, 0.2f);
            for (int ring = 3; ring >= 1; ring--)
            {
                float d = ring * (3f + 3f * glow);
                var c = new Color(gold.r, gold.g, gold.b, alpha * (0.10f + 0.06f * glow) / ring);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6f;
                    Label(new Rect(r.x + Mathf.Cos(a) * d, r.y + Mathf.Sin(a) * d, r.width, r.height), text, c);
                }
            }
            Label(new Rect(r.x + 4, r.y + 5, r.width, r.height), text, new Color(0f, 0f, 0f, alpha * 0.8f)); // shadow

            // Gold body, shimmering towards white.
            var body = Color.Lerp(gold, new Color(1f, 0.97f, 0.8f), 0.35f + 0.35f * Mathf.Sin(t * 9f));
            body.a = alpha;
            Label(r, text, body);

            // A white shine sweeping left to right every 1.5 s, clipped to a band over the text.
            float textW = _big.CalcSize(new GUIContent(text)).x;
            float x0 = (Ui.W - textW) / 2f, sweep = (t % 1.5f) / 1.5f;
            float band = textW * 0.12f, bx = x0 - band + sweep * (textW + 2 * band);
            GUI.BeginGroup(new Rect(bx, r.y, band, r.height));
            Label(new Rect(-bx, 0, r.width, r.height), text, new Color(1f, 1f, 1f, alpha * 0.9f));
            GUI.EndGroup();

            if (_footer != null)
                Label(new Rect(0, r.yMax - 6, Ui.W, 40), _footer, new Color(1f, 0.92f, 0.7f, alpha * 0.9f), _small);
        }
        finally { Ui.EndScaled(); }
    }

    private static void Label(Rect r, string text, Color c, GUIStyle style = null)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.Label(r, text, style ?? _big);
        GUI.color = prev;
    }
}
