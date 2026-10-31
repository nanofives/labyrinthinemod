using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppValkoGames.Labyrinthine.Monsters;
using UnityEngine;
using AnimState = Il2Cpp.AnimationState;
using MazeGenerator = Il2Cpp.MazeGenerator;
using TileType = Il2CppValkoGames.Labyrinthine.Cases.TileType;

namespace LabyHelper.Features;

/// <summary>
/// Fixed (north-up) minimap in the top-right corner during random cases.
///
/// MazeGenerator.instance.mazeMap is the generated layout as a TileType grid (Walkable, Wall, Landmark, Safezone,
/// LandmarkBorder, Start, End); generation runs on every peer, so clients have it too. World/grid conversion comes
/// from MazeGenerator.GridToWorldPos, sampled at three points so any offset or rotation is handled. Shows you (arrow),
/// the other players, the monsters (MonsterNetworkSync, synced to every peer; orange, red while chasing), the cosmetic
/// still on the ground, the exit, the case objectives (Objectives) and a fading trail of where each player walked. Switched on/off only in F1 > Gameplay (no hotkey).
/// Safe zones (LightZone triggers) are drawn semi-transparent green on top, and the view grows past the maze edge to cover the start area and wherever the players walk outside the grid. Under it, who still has to pick up
/// the case cosmetic.
/// </summary>
internal static partial class Minimap
{
    private static Texture2D _tex;
    private static IntPtr _mapPtr;
    private static int _sizeX, _sizeY;
    private static Vector3 _origin, _ax, _ay; // world = origin + gx * ax + gy * ay (x/z used)
    private static Vector2 _end = new(-1, -1);
    private static float _nextCheck;
    private static GUIStyle _style;
    private static readonly List<(IntPtr id, Vector3 pos, bool chasing)> Monsters = new();
    private static float _nextMonsters;
    private static Texture2D _arrow;

    /// <summary>64x64 arrowhead pointing up: yellow fill, black outline, notched tail so the direction is obvious.</summary>
    private static Texture2D MakeArrow() => MakeArrow(new Color32(255, 220, 40, 255));

    /// <summary>The arrow with a white fill, tinted per player through GUI.color (the black outline stays black).</summary>
    private static Texture2D _arrowWhite;

    private static Texture2D MakeArrow(Color32 fill)
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.hideFlags = HideFlags.HideAndDontSave;
        var px = new Color32[n * n];
        // Arrow polygon in texture coords (y up): tip, right wing, notch, left wing.
        var poly = new[] { new Vector2(32, 62), new Vector2(56, 4), new Vector2(32, 18), new Vector2(8, 4) };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = SignedDistance(p, poly);
                px[y * n + x] = d < -3f ? fill : d < 0.5f ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    /// <summary>Negative inside the polygon, distance to its edge in pixels.</summary>
    private static float SignedDistance(Vector2 p, Vector2[] poly)
    {
        float best = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[j], b = poly[i], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, (a + ab * t - p).magnitude);
            if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) / (b.y - a.y) * (b.x - a.x)) inside = !inside;
        }
        return inside ? -best : best;
    }

    public static void OnSceneLoaded()
    {
        _mapPtr = IntPtr.Zero;
        _end = new Vector2(-1, -1);
        Trails.Clear();
        ResetRoute();
    }

    // ---- trails: where each player walked lately, as a line (a point every TrailStep metres, the last TrailMax) ----

    private const string MeKey = "\u0001me";
    private const float TrailStep = 0.4f;
    private const int TrailMax = 500; // ~200 m
    private static readonly Dictionary<string, List<Vector3>> Trails = new();
    private static float _nextTrail;

    private static void RecordTrails()
    {
        _nextTrail = Time.unscaledTime + 0.05f;
        var cam = Camera.main;
        if (cam != null) AddTrail(MeKey, cam.transform.position - new Vector3(0f, 1.6f, 0f));
        foreach (var p in Players.Others())
            if (!p.Dead) AddTrail(p.Name ?? "?", p.Pos);
    }

    private static void AddTrail(string key, Vector3 pos)
    {
        if (!Trails.TryGetValue(key, out var list)) Trails[key] = list = new List<Vector3>();
        if (list.Count > 0)
        {
            var last = list[^1];
            float d = new Vector2(pos.x - last.x, pos.z - last.z).magnitude;
            if (d < TrailStep) return;
            if (d > 40f) list.Clear(); // teleported (respawn, level change): no line across the map
        }
        list.Add(pos);
        if (list.Count > TrailMax) list.RemoveAt(0);
    }

    // Monsters leave a shorter trail (the last ~25 m), kept per monster object and dropped when it despawns.
    private const int MonsterTrailMax = 60;
    private static readonly Dictionary<IntPtr, List<Vector3>> MonsterTrails = new();

    private static void AddMonsterTrail(IntPtr id, Vector3 pos)
    {
        if (!MonsterTrails.TryGetValue(id, out var list)) MonsterTrails[id] = list = new List<Vector3>();
        if (list.Count > 0)
        {
            var last = list[^1];
            float d = new Vector2(pos.x - last.x, pos.z - last.z).magnitude;
            if (d < TrailStep) return;
            if (d > 40f) list.Clear(); // teleported
        }
        list.Add(pos);
        if (list.Count > MonsterTrailMax) list.RemoveAt(0);
    }

    private static readonly Color MonsterColour = new(1f, 0.6f, 0.15f), MonsterChaseColour = new(1f, 0.15f, 0.1f);
    private static Texture2D _monsterIcon;

    /// <summary>64x64 eight-pointed spiky star: white fill (tinted when drawn) with a black outline.</summary>
    private static Texture2D MakeMonsterIcon()
    {
        const int n = 64, spikes = 8;
        var poly = new Vector2[spikes * 2];
        for (int i = 0; i < poly.Length; i++)
        {
            float a = Mathf.PI * i / spikes, rad = i % 2 == 0 ? 30f : 15f;
            poly[i] = new Vector2(32f + rad * Mathf.Sin(a), 32f + rad * Mathf.Cos(a));
        }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = SignedDistance(new Vector2(x + 0.5f, y + 0.5f), poly);
                px[y * n + x] = d < -3.5f ? new Color32(255, 255, 255, 255) : d < 0.5f ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    private static void MonsterIcon(Vector2 at, float size, Color col)
    {
        _monsterIcon ??= MakeMonsterIcon();
        GUI.color = col;
        GUI.DrawTexture(new Rect(at.x - size / 2, at.y - size / 2, size, size), _monsterIcon, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
    }

    private static readonly Color MeColour = new(1f, 0.86f, 0.16f);

    // ---- view cones: where each player is looking ----

    private static Texture2D _cone;

    /// <summary>Screen angle (0 = up, clockwise) of a world direction seen from a world position.</summary>
    private static float ScreenAngle(Vector3 from, Vector3 dir)
    {
        var a = ToScreenNoView(ToGrid(from));
        var b = ToScreenNoView(ToGrid(from + new Vector3(dir.x, 0f, dir.z).normalized * 10f));
        return Mathf.Atan2(b.x - a.x, -(b.y - a.y)) * Mathf.Rad2Deg;
    }

    // Grid y grows north, screen y down: enough for an angle, no offset or scale needed.
    private static Vector2 ToScreenNoView(Vector2 g) => new(g.x, -g.y);

    /// <summary>A 70-degree wedge from the player's dot towards where they look, fading with distance.</summary>
    private static void ViewCone(Vector2 at, float angle, float length, Color col)
    {
        if (Event.current.type != EventType.Repaint) return;
        _cone ??= MakeCone();
        var saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, at);
        GUI.color = new Color(col.r, col.g, col.b, col.a * 0.55f);
        GUI.DrawTexture(new Rect(at.x - length, at.y - length, 2f * length, 2f * length), _cone, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
        GUI.matrix = saved;
    }

    /// <summary>128x128, apex at the centre, opening upwards; alpha falls off with distance and towards the edges.</summary>
    private static Texture2D MakeCone()
    {
        const int n = 128;
        const float half = 35f * Mathf.Deg2Rad;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f; // texture y up = screen up
                float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dx, dy);
                float alpha = 0f;
                if (dy > 0f && r <= 1f && Mathf.Abs(a) <= half)
                    alpha = (1f - r) * Mathf.Clamp01((half - Mathf.Abs(a)) / (8f * Mathf.Deg2Rad));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    private static Texture2D _px;

    /// <summary>
    /// A continuous line through the points, fading out towards the oldest one, in the player's colour. Each segment is a
    /// thin texture rotated into place; consecutive points closer than a pixel on screen are merged. Repaint only.
    /// </summary>
    private static void DrawTrail(IReadOnlyList<Vector2> points, Color col, float width)
    {
        if (points.Count < 2 || Event.current.type != EventType.Repaint) return;
        _px ??= Ui.Solid(Color.white);
        var saved = GUI.matrix;
        var from = points[0];
        for (int i = 1; i < points.Count; i++)
        {
            var to = points[i];
            var d = to - from;
            float len = d.magnitude;
            if (len < 1f && i < points.Count - 1) continue;
            float age = (float)i / (points.Count - 1); // 1 = newest
            GUI.color = new Color(col.r, col.g, col.b, col.a * (0.15f + 0.75f * age));
            GUI.matrix = saved;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, from);
            // Overlap half a width at each end so the joints have no gaps.
            GUI.DrawTexture(new Rect(from.x - width / 2f, from.y - width / 2f, len + width, width), _px);
            from = to;
        }
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    public static void Update()
    {
        if (!GameState.InCase) return;
        UpdateRoute(); // M (big map) works with the corner minimap off too
        if (Input.GetKey(KeyCode.Tab)) _legendUntil = Time.unscaledTime + LegendSeconds;
        if (Settings.MinimapTrail.Value && _mapPtr != IntPtr.Zero && Time.unscaledTime >= _nextTrail) RecordTrails();
        if (Time.unscaledTime >= _nextMonsters) RefreshMonsters();
        if (_mapPtr != IntPtr.Zero && Time.unscaledTime >= _nextSafe) RefreshSafeZones();
        if (_mapPtr != IntPtr.Zero && _levelChecks.Count > 0 && Time.unscaledTime >= _levelChecks[0])
        {
            // The NavMesh can finish building a while after the maze: look again a few times.
            _levelChecks.RemoveAt(0);
            try { MinimapLevels.Analyze(ToGrid, _origin.y); }
            catch (Exception e) { Core.Log.Warning($"Minimap: floors: {e.Message}"); }
        }
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 1f;
        try { Build(); }
        catch (Exception e)
        {
            Core.Log.Warning($"Minimap: {e.Message}");
            _nextCheck = Time.unscaledTime + 15f;
        }
    }

    /// <summary>Builds the texture once per generated maze (the map object changes with every case).</summary>
    private static void Build()
    {
        var gen = MazeGenerator.instance;
        var map = gen?.mazeMap;
        if (map == null || map.Pointer == _mapPtr) return;
        int sx = map.SizeX, sy = map.SizeY;
        if (sx <= 0 || sy <= 0 || sx > 1024 || sy > 1024) return;

        var px = new Color32[sx * sy];
        _walk = new bool[sx, sy];
        _target = null;
        Route.Clear();
        _end = new Vector2(-1, -1);
        for (int y = 0; y < sy; y++)
            for (int x = 0; x < sx; x++)
            {
                var t = map[x, y];
                if (t == TileType.End) _end = new Vector2(x, y);
                _walk[x, y] = t != TileType.Wall;
                px[y * sx + x] = Colour(t);
            }
        if (_tex == null || _tex.width != sx || _tex.height != sy)
        {
            if (_tex != null) UnityEngine.Object.Destroy(_tex);
            _tex = new Texture2D(sx, sy, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _tex.hideFlags = HideFlags.HideAndDontSave;
        }
        _tex.SetPixels32(px);
        _tex.Apply(false);

        _origin = MazeGenerator.GridToWorldPos(0f, 0f);
        _ax = MazeGenerator.GridToWorldPos(1f, 0f) - _origin;
        _ay = MazeGenerator.GridToWorldPos(0f, 1f) - _origin;
        _sizeX = sx;
        _sizeY = sy;
        _mapPtr = map.Pointer;
        _view = new Rect(0, 0, sx, sy);
        Trails.Clear();
        MonsterTrails.Clear();
        _legendUntil = Time.unscaledTime + LegendSeconds; // new maze = the case just started
        MinimapLevels.Clear();
        _levelChecks.Clear();
        foreach (var d in new[] { 2f, 8f, 20f, 45f }) _levelChecks.Add(Time.unscaledTime + d);
        SafeZoneById.Clear();
        SafeZones.Clear();
        _nextSafe = 0f;
        Core.Log.Msg($"Minimap: maze {sx}x{sy}, grid x -> {_ax}, grid y -> {_ay}, origin {_origin}, exit {(_end.x >= 0 ? _end.ToString() : "none")}.");
    }

    private static Color32 Colour(TileType t) => t switch
    {
        TileType.Wall => new Color32(20, 20, 24, 235),
        TileType.Safezone => new Color32(70, 160, 90, 235),
        TileType.Landmark or TileType.LandmarkBorder => new Color32(90, 110, 160, 235),
        TileType.Start => new Color32(220, 200, 80, 235),
        TileType.End => new Color32(230, 70, 60, 235),
        _ => new Color32(150, 150, 150, 235),
    };

    /// <summary>World position -> grid coordinates (fractional), inverting the sampled GridToWorldPos.</summary>
    private static Vector2 ToGrid(Vector3 world)
    {
        float dx = world.x - _origin.x, dz = world.z - _origin.z;
        float a = _ax.x, b = _ay.x, c = _ax.z, d = _ay.z;
        float det = a * d - b * c;
        if (Mathf.Abs(det) < 1e-5f) return new Vector2(-1, -1);
        return new Vector2((d * dx - b * dz) / det, (-c * dx + a * dz) / det);
    }

    public static void Draw()
    {
        if ((!Settings.MinimapEnabled.Value && !_big) || !GameState.InCase || _tex == null || _mapPtr == IntPtr.Zero) return;
        Grow();
        float viewW = _view.width, viewH = _view.height;
        var v = MapRect(viewW, viewH, out float scale); // whole view: the corner, or big in the middle (M)
        float size = Mathf.Max(v.width, v.height);
        // The maze grid inside the view (grid cell x covers [x, x+1)).
        var r = new Rect(v.x - _view.xMin * scale, v.yMax - (_sizeY - _view.yMin) * scale, _sizeX * scale, _sizeY * scale);
        if (_big)
        {
            DrawBigBackdrop(v);
            HandleBigClicks(v, r, scale);
        }

        // Grid y grows north; screen y grows down.
        Vector2 ToScreen(Vector2 g) => new(r.x + (g.x + 0.5f) * scale, r.yMax - (g.y + 0.5f) * scale);
        Rect CellRect(Rect g) => Rect.MinMaxRect(r.x + g.xMin * scale, r.yMax - g.yMax * scale, r.x + g.xMax * scale, r.yMax - g.yMin * scale);

        // Floor you're on (by your feet); on another floor than the maze's, that floor's picture replaces the grid.
        var cam0 = Camera.main;
        int floor = cam0 != null && MinimapLevels.Levels.Count > 1 ? MinimapLevels.LevelOf(cam0.transform.position.y - 1.6f) : -1;
        var level = floor >= 0 && floor != MinimapLevels.Main ? MinimapLevels.Levels[floor] : null;

        // Dark backing only behind the maze grid; entrance/exit areas outside it stay over the game view.
        Ui.FlatPanel(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8));
        if (level?.Tex != null)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.15f); // the maze above/below, faint, for orientation
            GUI.DrawTexture(r, _tex, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            GUI.DrawTexture(CellRect(level.GridRect), level.Tex, ScaleMode.StretchToFill, true);
        }
        else GUI.DrawTexture(r, _tex, ScaleMode.StretchToFill, true);
        _style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, font = Ui.SymbolFont };

        // Stairs and ramps touching this floor: ▼ goes down, ▲ goes up.
        if (floor >= 0)
            foreach (var s in MinimapLevels.Stairs)
            {
                if (s.From == floor) Dot(ToScreen(s.Grid), 12f, new Color(0.4f, 0.95f, 1f), "▲");
                else if (s.To == floor) Dot(ToScreen(s.Grid), 12f, new Color(0.4f, 0.95f, 1f), "▼");
            }

        // Safe zones (LightZone triggers: light, fire, interiors, spider tunnels), from their collider bounds.
        _safe ??= Solid(new Color(0.3f, 0.95f, 0.45f, 0.22f)); // semi-transparent: the maze shows through
        foreach (var b in SafeZones)
        {
            // The start and exit areas outside the grid are safe zones too, big ones; they're already marked.
            var centre = ToGrid(b.center);
            if (centre.x < -0.5f || centre.y < -0.5f || centre.x > _sizeX - 0.5f || centre.y > _sizeY - 0.5f) continue;
            var a = ToScreen(ToGrid(b.min));
            var c = ToScreen(ToGrid(b.max));
            var zr = Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
            if (zr.width < 3f) zr = new Rect(zr.center.x - 1.5f, zr.y, 3f, zr.height);
            if (zr.height < 3f) zr = new Rect(zr.x, zr.center.y - 1.5f, zr.width, 3f);
            GUI.DrawTexture(zr, _safe);
        }

        // Things on another floor than yours are drawn faint.
        Color On(Color c, float y) => floor < 0 || MinimapLevels.LevelOf(y) == floor ? c : new Color(c.r, c.g, c.b, 0.35f);

        // Where everyone walked, under every other mark.
        if (Settings.MinimapTrail.Value)
        {
            float dot = Mathf.Clamp(scale * 0.3f, 2f, 4f); // line width
            var pts = new List<Vector2>(TrailMax);
            foreach (var (key, trail) in Trails)
            {
                if (trail.Count < 2) continue;
                pts.Clear();
                foreach (var w in trail) pts.Add(ToScreen(ToGrid(w)));
                var col = key == MeKey ? MeColour : Players.ColourOf(key);
                DrawTrail(pts, On(col, trail[^1].y), dot);
            }
            foreach (var (_, trail) in MonsterTrails)
            {
                if (trail.Count < 2) continue;
                pts.Clear();
                foreach (var w in trail) pts.Add(ToScreen(ToGrid(w)));
                DrawTrail(pts, On(MonsterColour, trail[^1].y), dot * 0.8f);
            }
        }
        DrawRouteOnMap(g => ToScreen(g), scale);
        if (_end.x >= 0) Dot(ToScreen(_end), Mathf.Max(6f, scale * 1.5f), On(new Color(1f, 0.3f, 0.25f), _origin.y), "S");
        foreach (var (pos, _, id) in CosmeticAlert.PendingTargets())
        {
            // Large and pulsing so it stands out: the item's picture inside a blue ring.
            var at = ToScreen(ToGrid(pos));
            float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
            float s = Mathf.Clamp(size * 0.13f, 26f, 48f) * pulse;
            var faint = On(Color.white, pos.y).a < 1f;
            _ring ??= MakeRing();
            GUI.color = faint ? new Color(0.35f, 0.69f, 1f, 0.4f) : new Color(0.35f, 0.69f, 1f);
            GUI.DrawTexture(new Rect(at.x - s * 0.75f, at.y - s * 0.75f, s * 1.5f, s * 1.5f), _ring);
            GUI.color = faint ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
            var (icon, frame) = CosmeticAlert.Visuals(id);
            if (icon != null) Toast.DrawItemIcon(new Rect(at.x - s / 2, at.y - s / 2, s, s), icon, frame);
            else Dot(at, 14f, new Color(0.35f, 0.69f, 1f), "◆");
            GUI.color = Color.white;
        }
        foreach (var c in Objectives.Pending)
            Dot(ToScreen(ToGrid(c.Pos)), 22f, On(Objectives.Colour, c.Pos.y), Objectives.Glyph(c.Kind), 40f); // big: easy to spot
        foreach (var (_, pos, chasing) in Monsters)
            MonsterIcon(ToScreen(ToGrid(pos)), chasing ? 26f * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 10f)) : 20f,
                On(chasing ? MonsterChaseColour : MonsterColour, pos.y));
        // Other players: a big dot in their own colour plus their name, so you can tell them apart.
        float cone = Mathf.Clamp(size * 0.2f, 40f, 90f);
        foreach (var p in Players.Others())
        {
            var at = ToScreen(ToGrid(p.Pos));
            var col = On(p.Dead ? new Color(1f, 0.45f, 0.4f) : Players.ColourOf(p.Name), p.Pos.y);
            if (!p.Dead)
            {
                // An arrow in their colour pointing where they look, like yours (downed players stay a red dot).
                float look = ScreenAngle(p.Pos, p.Look);
                ViewCone(at, look, cone, col);
                _arrowWhite ??= MakeArrow(new Color32(255, 255, 255, 255));
                float a = Mathf.Clamp(size * 0.1f, 20f, 40f);
                var saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(look, at);
                GUI.color = col;
                GUI.DrawTexture(new Rect(at.x - a / 2, at.y - a / 2, a, a), _arrowWhite, ScaleMode.StretchToFill, true);
                GUI.color = Color.white;
                GUI.matrix = saved;
            }
            else Dot(at, 15f, col, "●");
            _nameStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            string label = p.Dead ? $"{p.Name} {Loc.T("(caído)", "(down)")}" : p.Name;
            var nr = new Rect(at.x + 10, at.y - 10, 160, 20);
            GUI.color = new Color(0f, 0f, 0f, col.a);
            GUI.Label(new Rect(nr.x + 1, nr.y + 1, nr.width, nr.height), Ui.Caps(label), _nameStyle);
            GUI.color = col;
            GUI.Label(nr, Ui.Caps(label), _nameStyle);
            GUI.color = Color.white;
        }

        // Which floor, when the map has more than one.
        if (floor >= 0)
        {
            _levelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft };
            int num = MinimapLevels.Levels[floor].Number;
            string label = num == 0 ? Loc.T("Nivel principal", "Main floor")
                : num < 0 ? Loc.T($"Subsuelo {-num}", $"Basement {-num}") : Loc.T($"Piso +{num}", $"Floor +{num}");
            var lr = new Rect(r.x + 4, r.yMax - 24, 220, 22);
            GUI.color = Color.black;
            GUI.Label(new Rect(lr.x + 1, lr.y + 1, lr.width, lr.height), Ui.Caps(label), _levelStyle);
            GUI.color = new Color(0.4f, 0.95f, 1f);
            GUI.Label(lr, Ui.Caps(label), _levelStyle);
            GUI.color = Color.white;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            var me = ToScreen(ToGrid(cam.transform.position));
            var ahead = ToScreen(ToGrid(cam.transform.position + new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z).normalized * 10f));
            float angle = Mathf.Atan2(ahead.x - me.x, -(ahead.y - me.y)) * Mathf.Rad2Deg; // 0 = up on screen
            ViewCone(me, angle, Mathf.Clamp(size * 0.2f, 40f, 90f), MeColour);
            // Big arrow (drawn texture, not a font glyph): yellow with a black outline, pointing where you look.
            _arrow ??= MakeArrow();
            float a = Mathf.Clamp(size * 0.11f, 22f, 44f);
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, me);
            GUI.DrawTexture(new Rect(me.x - a / 2, me.y - a / 2, a, a), _arrow, ScaleMode.StretchToFill, true);
            GUI.matrix = saved;
        }

        if (_big) return;

        // Who still has to pick up the case cosmetic, under the map (party members with the mod).
        var missing = CosmeticAlert.StillToPickUp();
        float below = v.yMax + 8;
        if (missing.Count > 0)
        {
            below += 48;
            _note ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight, wordWrap = true };
            string names = string.Join(", ", missing.Select(m => m.IsMe ? Loc.T("vos", "you") : m.Name));
            string text = Loc.T($"◆ Falta agarrar el cosmético: {names}", $"◆ Cosmetic not picked up yet: {names}");
            var tr = new Rect(Screen.width - 16 - Mathf.Max(v.width, 320f), v.yMax + 8, Mathf.Max(v.width, 320f), 44);
            GUI.color = Color.black;
            GUI.Label(new Rect(tr.x + 1, tr.y + 1, tr.width, tr.height), Ui.Caps(text), _note);
            GUI.color = new Color(0.35f, 0.69f, 1f);
            GUI.Label(tr, Ui.Caps(text), _note);
            GUI.color = Color.white;
        }
        if (Settings.MinimapLegend.Value && Time.unscaledTime < _legendUntil)
            DrawLegend(Screen.width - 16, below, v.width, floor >= 0, Mathf.Clamp01((_legendUntil - Time.unscaledTime) / 0.5f));
    }

    // The legend shows for LegendSeconds when a case starts, then hides; TAB brings it back (held: stays up).
    private const float LegendSeconds = 10f;
    private static float _legendUntil;

    /// <summary>
    /// What each mark means, under the map and exactly as wide as it: two columns at the biggest text that fits, one
    /// column when two would need tiny text. F1 > Gameplay turns it off.
    /// </summary>
    private static void DrawLegend(float right, float top, float width, bool floors, float alpha)
    {
        var rows = new List<(string glyph, Color colour, string text)>
        {
            ("▲", new Color(1f, 0.86f, 0.16f), Loc.T("Vos", "You")),
            ("●", new Color(0.45f, 1f, 0.5f), Loc.T("Jugador (color propio)", "Player (own colour)")),
            ("—", MeColour, Loc.T("Recorrido", "Trail")),
            ("◢", MeColour, Loc.T("Hacia donde mira", "Where they look")),
            ("◯", new Color(0.35f, 0.69f, 1f), Loc.T("Cosmético", "Cosmetic")),
            ("◆", Objectives.Colour, Loc.T("Objetivo", "Objective")),
            ("O", Objectives.Colour, Loc.T("Dónde va el objeto", "Where an item goes")),
            ("◎", Objectives.Colour, Loc.T("Lugar a alcanzar", "Place to reach")),
            (null, MonsterColour, Loc.T("Monstruo y su recorrido", "Monster and its trail")),
            (null, MonsterChaseColour, Loc.T("Monstruo persiguiendo", "Monster chasing")),
            ("S", new Color(1f, 0.3f, 0.25f), "Sexy salida"),
            ("■", new Color(0.3f, 0.95f, 0.45f), Loc.T("Zona segura", "Safe zone")),
            ("■", new Color(0.35f, 0.43f, 0.63f), Loc.T("Landmark", "Landmark")),
        };
        if (floors)
        {
            rows.Add(("▼▲", new Color(0.4f, 0.95f, 1f), Loc.T("Bajada / subida", "Stairs down / up")));
            rows.Add(("●", new Color(1f, 1f, 1f, 0.35f), Loc.T("Tenue: en otro piso", "Faint: on another floor")));
        }
        // As big as the width allows, on an almost opaque backing so the maze behind doesn't get in the way.
        const float pad = 12f, gap = 12f;
        float inner = width - 2 * pad;
        float Need(int size, int cols)
        {
            var st = LegendStyle(size);
            float textW = 0f;
            foreach (var row in rows) textW = Mathf.Max(textW, st.CalcSize(new GUIContent(Ui.Caps(row.text))).x);
            return cols * (size * 1.7f + textW) + (cols - 1) * gap;
        }
        int cols = 2, size = 22;
        while (size > 14 && Need(size, 2) > inner) size--;
        if (Need(size, 2) > inner)
        {
            cols = 1;
            size = 22;
            while (size > 11 && Need(size, 1) > inner) size--;
        }
        var text = LegendStyle(size);
        _legendGlyph ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = Ui.SymbolFont };
        _legendGlyph.fontSize = size + 2;
        float rowH = size * 1.55f, glyphW = size * 1.7f, colW = (inner - (cols - 1) * gap) / cols;
        int perCol = (rows.Count + cols - 1) / cols;
        var box = new Rect(right - width, top, width, perCol * rowH + 2 * pad);
        Ui.Fill(box, new Color(0.03f, 0.03f, 0.04f, 0.88f * alpha));
        for (int i = 0; i < rows.Count; i++)
        {
            var (glyph, colour, label) = rows[i];
            float x = box.x + pad + (i / perCol) * (colW + gap), y = box.y + pad + (i % perCol) * rowH;
            GUI.color = new Color(colour.r, colour.g, colour.b, colour.a * alpha);
            if (glyph == null) MonsterIcon(new Vector2(x + glyphW / 2, y + rowH / 2), size * 1.1f, GUI.color); // drawn icon, no glyph
            else GUI.Label(new Rect(x, y, glyphW, rowH), glyph, _legendGlyph);
            GUI.color = new Color(0.95f, 0.95f, 0.93f, alpha);
            GUI.Label(new Rect(x + glyphW, y, colW - glyphW, rowH), Ui.Caps(label), text);
        }
        GUI.color = Color.white;
    }

    private static GUIStyle _legendGlyph;
    private static readonly Dictionary<int, GUIStyle> LegendStyles = new();

    private static GUIStyle LegendStyle(int size)
    {
        if (!LegendStyles.TryGetValue(size, out var st))
            LegendStyles[size] = st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleLeft };
        return st;
    }

    private static GUIStyle _note, _levelStyle, _nameStyle;
    private static Texture2D _ring;

    /// <summary>64x64 ring (white, tinted when drawn) for the cosmetic marker.</summary>
    private static Texture2D MakeRing()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 27f) / 4f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    /// <summary>World position of the maze exit (null when there's no maze or no exit tile).</summary>
    public static Vector3? ExitWorld => _mapPtr == IntPtr.Zero || _end.x < 0 ? null : _origin + _ax * _end.x + _ay * _end.y;
    private static readonly List<float> _levelChecks = new();
    private static Texture2D _safe;
    private static Rect _view; // grid-space area shown: the maze plus whatever around it the players have walked
    private static readonly List<Bounds> SafeZones = new();
    private static readonly Dictionary<IntPtr, Bounds> SafeZoneById = new();
    private static float _nextSafe;
    private const float MaxOutside = 12f; // tiles beyond the maze the view may grow

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    /// <summary>
    /// Grows the view (never shrinks it, so the map doesn't jump) to keep the safe zones, you and the other players in
    /// sight, with a tile of margin, up to MaxOutside tiles beyond the maze on each side.
    /// </summary>
    private static void Grow()
    {
        var pts = new List<Vector2>();
        var cam = Camera.main;
        if (cam != null) pts.Add(ToGrid(cam.transform.position));
        foreach (var p in Players.Others()) pts.Add(ToGrid(p.Pos));
        float x0 = _view.xMin, y0 = _view.yMin, x1 = _view.xMax, y1 = _view.yMax;
        foreach (var g in pts)
        {
            if (g.x < -1e4f) continue; // ToGrid failed
            // Grid coordinates are cell centres: cell x covers [x - 0.5, x + 0.5], drawn at [x, x + 1) in view units.
            x0 = Mathf.Min(x0, Mathf.Max(-MaxOutside, Mathf.Floor(g.x + 0.5f) - 1f));
            y0 = Mathf.Min(y0, Mathf.Max(-MaxOutside, Mathf.Floor(g.y + 0.5f) - 1f));
            x1 = Mathf.Max(x1, Mathf.Min(_sizeX + MaxOutside, Mathf.Ceil(g.x + 0.5f) + 1f));
            y1 = Mathf.Max(y1, Mathf.Min(_sizeY + MaxOutside, Mathf.Ceil(g.y + 0.5f) + 1f));
        }
        _view = Rect.MinMaxRect(x0, y0, x1, y1);
    }

    /// <summary>Safe zones of the current maze: every LightZone with a safezone type, as world bounds of its trigger.</summary>
    private static void RefreshSafeZones()
    {
        _nextSafe = Time.unscaledTime + 5f; // maze modules keep spawning for a while after the scene loads
        try
        {
            foreach (var z in UnityEngine.Object.FindObjectsOfType<Il2Cpp.LightZone>(true))
            {
                if (z == null || !z.gameObject.scene.IsValid() || (int)z.safezoneType == 0) continue;
                foreach (var col in z.GetComponents<Collider>())
                {
                    // Collider.bounds is empty while the object is off (occlusion), so boxes are computed by hand.
                    Bounds? b = null;
                    var box = col?.TryCast<BoxCollider>();
                    if (box != null)
                    {
                        var t = box.transform;
                        var bb = new Bounds(t.TransformPoint(box.center), Vector3.zero);
                        for (int i = 0; i < 8; i++)
                        {
                            var corner = box.center + Vector3.Scale(box.size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                            bb.Encapsulate(t.TransformPoint(corner));
                        }
                        b = bb;
                    }
                    else if (col != null && col.bounds.extents.sqrMagnitude > 0.01f) b = col.bounds;
                    if (b != null) SafeZoneById[col.Pointer] = b.Value;
                }
            }
            SafeZones.Clear();
            SafeZones.AddRange(SafeZoneById.Values);
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Minimap: safe zones: {e.Message}");
            _nextSafe = Time.unscaledTime + 30f;
        }
    }

    private static void Dot(Vector2 at, float size, Color color, string glyph, float maxFont = 26f)
    {
        _style.fontSize = Mathf.RoundToInt(Mathf.Clamp(size * 1.6f, 10f, maxFont));
        float box = Mathf.Max(30f, _style.fontSize * 1.4f);
        var rect = new Rect(at.x - box / 2, at.y - box / 2, box, box);
        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), Ui.Caps(glyph), _style);
        GUI.color = color;
        GUI.Label(rect, Ui.Caps(glyph), _style);
        GUI.color = Color.white;
    }

    /// <summary>Active monsters, 4 times a second. Chasing = same test as MonsterAlert (flag or spot/run/charge state).</summary>
    private static void RefreshMonsters()
    {
        _nextMonsters = Time.unscaledTime + 0.25f;
        Monsters.Clear();
        try
        {
            foreach (var sync in UnityEngine.Object.FindObjectsOfType<MonsterNetworkSync>())
            {
                if (sync == null || !sync.IsEnabled) continue;
                var st = sync.CurrentAnimationState;
                bool chasing = sync.IsChasingPlayer || st == AnimState.MonsterRun || st == AnimState.MonsterSpot || st == AnimState.MonsterCharge;
                Monsters.Add((sync.Pointer, sync.transform.position, chasing));
                if (Settings.MinimapTrail.Value) AddMonsterTrail(sync.Pointer, sync.transform.position);
            }
            foreach (var id in MonsterTrails.Keys.Where(k => !Monsters.Any(m => m.id == k)).ToList()) MonsterTrails.Remove(id);
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Minimap: monsters: {e.Message}");
            _nextMonsters = Time.unscaledTime + 10f;
        }
    }

    // ---- preview (F1): the minimap at its size and place, over a small made-up maze ----

    private static Texture2D _demo;
    private static List<Vector2Int> _demoPath, _demoPath2;
    private const int DemoN = 21;

    /// <summary>Drawn by F1 while you hover or drag a minimap option, so size, legend and trail can be judged.</summary>
    public static void DrawPreview()
    {
        if (_demo == null) BuildDemo();
        float size = Mathf.Clamp(Settings.MinimapSize.Value, 120f, 600f);
        var r = new Rect(Screen.width - size - 16, 16, size, size);
        float cell = size / DemoN;
        Vector2 At(Vector2Int g) => new(r.x + (g.x + 0.5f) * cell, r.yMax - (g.y + 0.5f) * cell);
        float alpha = Settings.MinimapEnabled.Value ? 1f : 0.35f;
        Ui.FlatPanel(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8));
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(r, _demo, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
        _style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, font = Ui.SymbolFont };

        // Walk along the made-up paths so the trail moves like in a case.
        int n1 = 1 + (int)(Time.unscaledTime * 3f) % _demoPath.Count, n2 = 1 + (int)(Time.unscaledTime * 2.5f) % _demoPath2.Count;
        if (Settings.MinimapTrail.Value)
        {
            float dot = Mathf.Clamp(cell * 0.3f, 2f, 4f);
            var p1 = new List<Vector2>();
            for (int i = Mathf.Max(0, n1 - 25); i < n1; i++) p1.Add(At(_demoPath[i]));
            var p2 = new List<Vector2>();
            for (int i = Mathf.Max(0, n2 - 25); i < n2; i++) p2.Add(At(_demoPath2[i]));
            DrawTrail(p2, new Color(0.45f, 1f, 0.5f, alpha), dot);
            DrawTrail(p1, new Color(MeColour.r, MeColour.g, MeColour.b, alpha), dot);
        }
        Dot(At(_demoPath2[n2 - 1]), 15f, new Color(0.45f, 1f, 0.5f, alpha), "●");
        Dot(At(_demoPath[n1 - 1]), Mathf.Max(10f, cell), new Color(MeColour.r, MeColour.g, MeColour.b, alpha), "▲");
        Dot(At(new Vector2Int(DemoN - 2, DemoN - 2)), Mathf.Max(6f, cell), new Color(1f, 0.3f, 0.25f, alpha), "S");

        Ui.Text(new Rect(r.x + 8, r.yMax - 28, r.width - 8, 26), Loc.T("Vista previa", "Preview"), Ui.Small, new Color(1f, 1f, 1f, 0.85f));
        if (Settings.MinimapLegend.Value) DrawLegend(Screen.width - 16, r.yMax + 8, r.width, false, 1f);
    }

    /// <summary>A 10x10-room maze (fixed seed) and two walks through it.</summary>
    private static void BuildDemo()
    {
        var wall = new bool[DemoN, DemoN];
        for (int x = 0; x < DemoN; x++) for (int y = 0; y < DemoN; y++) wall[x, y] = true;
        var rng = new System.Random(7);
        var stack = new Stack<Vector2Int>();
        wall[1, 1] = false;
        stack.Push(new Vector2Int(1, 1));
        var dirs = new[] { new Vector2Int(2, 0), new Vector2Int(-2, 0), new Vector2Int(0, 2), new Vector2Int(0, -2) };
        while (stack.Count > 0)
        {
            var c = stack.Peek();
            var options = dirs.Select(d => c + d).Where(n => n.x > 0 && n.y > 0 && n.x < DemoN - 1 && n.y < DemoN - 1 && wall[n.x, n.y]).ToList();
            if (options.Count == 0) { stack.Pop(); continue; }
            var next = options[rng.Next(options.Count)];
            wall[(c.x + next.x) / 2, (c.y + next.y) / 2] = false;
            wall[next.x, next.y] = false;
            stack.Push(next);
        }
        var px = new Color32[DemoN * DemoN];
        for (int y = 0; y < DemoN; y++)
            for (int x = 0; x < DemoN; x++)
                px[y * DemoN + x] = Colour(wall[x, y] ? TileType.Wall : x == DemoN - 2 && y == DemoN - 2 ? TileType.End : TileType.Walkable);
        _demo = new Texture2D(DemoN, DemoN, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        _demo.SetPixels32(px);
        _demo.Apply(false);
        _demoPath = Bfs(wall, new Vector2Int(1, 1), new Vector2Int(DemoN - 2, DemoN - 2));
        _demoPath2 = Bfs(wall, new Vector2Int(1, DemoN - 2), new Vector2Int(DemoN - 2, 1));
    }

    private static List<Vector2Int> Bfs(bool[,] wall, Vector2Int from, Vector2Int to)
    {
        var prev = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
        var q = new Queue<Vector2Int>();
        q.Enqueue(from);
        var steps = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (c == to) break;
            foreach (var s in steps)
            {
                var n = c + s;
                if (n.x < 0 || n.y < 0 || n.x >= DemoN || n.y >= DemoN || wall[n.x, n.y] || prev.ContainsKey(n)) continue;
                prev[n] = c;
                q.Enqueue(n);
            }
        }
        var path = new List<Vector2Int>();
        if (!prev.ContainsKey(to)) return new List<Vector2Int> { from };
        for (var c = to; ; c = prev[c]) { path.Add(c); if (c == from) break; }
        path.Reverse();
        return path;
    }
}
