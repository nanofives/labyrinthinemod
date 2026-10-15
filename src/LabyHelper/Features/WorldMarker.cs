using System.Linq;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Floating label over each cosmetic still in the map, drawn on top of everything (visible through walls).
/// Mode "Hotkey" shows it while F9 is toggled on, "Always" keeps it on, "Off" disables it.
/// Off-screen targets are pinned to the screen edge with an arrow. While F9 is on, the other players get a marker too.
/// </summary>
internal static class WorldMarker
{
    private static GUIStyle _style;

    public static void Draw()
    {
        string mode = Settings.MarkerMode.Value ?? "Hotkey";
        if (mode.Equals("Off", System.StringComparison.OrdinalIgnoreCase)) return;
        if (!mode.Equals("Always", System.StringComparison.OrdinalIgnoreCase) && !CosmeticAlert.Pinned) return;

        var cam = Camera.main;
        if (cam == null) return;
        _style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (CosmeticAlert.Pinned)
        {
            DrawPlayers(cam);
            DrawObjectives(cam);
        }

        foreach (var (pos, name, id) in CosmeticAlert.PendingTargets())
        {
            Vector3 world = pos + Vector3.up * 0.6f;
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool behind = sp.z < 0;
            if (behind) sp = -sp;

            float x = sp.x, y = Screen.height - sp.y;
            const float margin = 60f;
            bool offscreen = behind || x < margin || x > Screen.width - margin || y < margin || y > Screen.height - margin;
            x = Mathf.Clamp(x, margin, Screen.width - margin);
            y = Mathf.Clamp(y, margin, Screen.height - margin);

            float meters = Vector3.Distance(cam.transform.position, pos);
            string label = offscreen ? $"{Arrow(x, y)} {name}\n{meters:0} m" : $"◆ {name}\n{meters:0} m";
            var rect = new Rect(x - 150, y - 25, 300, 50);
            const float icon = 56f;
            var (sprite, frame) = CosmeticAlert.Visuals(id);
            if (sprite != null) Toast.DrawItemIcon(new Rect(x - icon / 2, rect.y - icon - 2, icon, icon), sprite, frame);

            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), label, _style);
            GUI.color = new Color(0.35f, 0.69f, 1f);
            GUI.Label(rect, label, _style);
            GUI.color = Color.white;
        }

        if (Settings.CollectibleMarkers.Value) DrawCollectibles(cam);
    }

    private static readonly Color PlayerColor = new(0.45f, 1f, 0.5f), DownColor = new(1f, 0.45f, 0.4f);

    /// <summary>Other players, pinned to the screen edge when off-screen (same style as the cosmetic marker).</summary>
    private static void DrawPlayers(Camera cam)
    {
        foreach (var p in Players.Others())
        {
            Vector3 sp = cam.WorldToScreenPoint(p.Pos + Vector3.up * 2.1f);
            bool behind = sp.z < 0;
            if (behind) sp = -sp;
            float x = sp.x, y = Screen.height - sp.y;
            const float margin = 60f;
            bool offscreen = behind || x < margin || x > Screen.width - margin || y < margin || y > Screen.height - margin;
            x = Mathf.Clamp(x, margin, Screen.width - margin);
            y = Mathf.Clamp(y, margin, Screen.height - margin);

            float meters = Vector3.Distance(cam.transform.position, p.Pos);
            string name = p.Dead ? $"{p.Name} {Loc.T("(caído)", "(down)")}" : p.Name;
            string label = offscreen ? $"{Arrow(x, y)} {name}\n{meters:0} m" : $"▼ {name}\n{meters:0} m";
            var rect = new Rect(x - 150, y - 25, 300, 50);
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), label, _style);
            GUI.color = p.Dead ? DownColor : PlayerColor;
            GUI.Label(rect, label, _style);
            GUI.color = Color.white;
        }
    }

    /// <summary>Pending case objectives (magenta), no edge arrows: the minimap already shows the off-screen ones.</summary>
    private static void DrawObjectives(Camera cam)
    {
        _small ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var camPos = cam.transform.position;
        foreach (var c in Objectives.Pending)
        {
            Vector3 sp = cam.WorldToScreenPoint(c.Pos + Vector3.up * 0.8f);
            if (sp.z < 0 || sp.x < 0 || sp.x > Screen.width || sp.y < 0 || sp.y > Screen.height) continue;
            string label = $"{Objectives.Glyph(c.Kind)} {c.Label}  {Vector3.Distance(camPos, c.Pos):0} m";
            var rect = new Rect(sp.x - 130, Screen.height - sp.y - 12, 260, 24);
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), label, _small);
            GUI.color = Objectives.Colour;
            GUI.Label(rect, label, _small);
            GUI.color = Color.white;
        }
    }

    private static GUIStyle _small;

    /// <summary>Tickets, reroll coins and XP: the nearest ones in front of the camera, no edge arrows (less clutter).</summary>
    private static void DrawCollectibles(Camera cam)
    {
        _small ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var camPos = cam.transform.position;
        foreach (var c in Collectibles.Live.OrderBy(i => (i.Pos - camPos).sqrMagnitude).Take(Settings.CollectibleMaxMarkers.Value))
        {
            Vector3 sp = cam.WorldToScreenPoint(c.Pos + Vector3.up * 0.3f);
            if (sp.z < 0 || sp.x < 0 || sp.x > Screen.width || sp.y < 0 || sp.y > Screen.height) continue;
            float meters = Vector3.Distance(camPos, c.Pos);
            string label = $"● {Collectibles.Label(c.Type)}{(c.Amount > 1 ? $" x{c.Amount}" : "")}  {meters:0} m";
            var rect = new Rect(sp.x - 110, Screen.height - sp.y - 12, 220, 24);
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), label, _small);
            GUI.color = Collectibles.Color(c.Type);
            GUI.Label(rect, label, _small);
            GUI.color = Color.white;
        }
    }

    private static string Arrow(float x, float y)
    {
        float dx = x - Screen.width / 2f, dy = y - Screen.height / 2f;
        if (Mathf.Abs(dx) > Mathf.Abs(dy)) return dx > 0 ? "►" : "◄";
        return dy > 0 ? "▼" : "▲";
    }
}
