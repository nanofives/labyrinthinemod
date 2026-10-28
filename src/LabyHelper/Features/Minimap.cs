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
/// still on the ground, the exit and the case objectives (Objectives). F4 shows/hides it.
/// Safe zones (LightZone triggers) are drawn semi-transparent green on top, and the view grows past the maze edge to cover the start area and wherever the players walk outside the grid. Under it, who still has to pick up
/// the case cosmetic.
/// </summary>
internal static class Minimap
{
    private static Texture2D _tex;
    private static IntPtr _mapPtr;
    private static int _sizeX, _sizeY;
    private static Vector3 _origin, _ax, _ay; // world = origin + gx * ax + gy * ay (x/z used)
    private static Vector2 _end = new(-1, -1);
    private static float _nextCheck;
    private static GUIStyle _style;
    private static readonly List<(Vector3 pos, bool chasing)> Monsters = new();
    private static float _nextMonsters;
    private static Texture2D _arrow;

    /// <summary>64x64 arrowhead pointing up: yellow fill, black outline, notched tail so the direction is obvious.</summary>
    private static Texture2D MakeArrow()
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
                px[y * n + x] = d < -3f ? new Color32(255, 220, 40, 255) : d < 0.5f ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
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
    }

    public static void Update()
    {
        if (GameState.InCase && Input.GetKeyDown(KeyCode.F4))
        {
            Settings.MinimapEnabled.Value = !Settings.MinimapEnabled.Value;
            Settings.Save();
            Toast.Show(Settings.MinimapEnabled.Value ? Loc.T("Minimapa: ON", "Minimap: ON") : Loc.T("Minimapa: OFF", "Minimap: OFF"), 2f);
        }
        if (!Settings.MinimapEnabled.Value || !GameState.InCase) return;
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
        _end = new Vector2(-1, -1);
        for (int y = 0; y < sy; y++)
            for (int x = 0; x < sx; x++)
            {
                var t = map[x, y];
                if (t == TileType.End) _end = new Vector2(x, y);
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
        if (!Settings.MinimapEnabled.Value || !GameState.InCase || _tex == null || _mapPtr == IntPtr.Zero) return;
        Grow();
        float size = Mathf.Clamp(Settings.MinimapSize.Value, 120f, 600f);
        float viewW = _view.width, viewH = _view.height;
        float scale = size / Mathf.Max(viewW, viewH);
        var v = new Rect(Screen.width - viewW * scale - 16, 16, viewW * scale, viewH * scale); // whole view
        // The maze grid inside the view (grid cell x covers [x, x+1)).
        var r = new Rect(v.x - _view.xMin * scale, v.yMax - (_sizeY - _view.yMin) * scale, _sizeX * scale, _sizeY * scale);

        // Grid y grows north; screen y grows down.
        Vector2 ToScreen(Vector2 g) => new(r.x + (g.x + 0.5f) * scale, r.yMax - (g.y + 0.5f) * scale);
        Rect CellRect(Rect g) => Rect.MinMaxRect(r.x + g.xMin * scale, r.yMax - g.yMax * scale, r.x + g.xMax * scale, r.yMax - g.yMin * scale);

        // Floor you're on (by your feet); on another floor than the maze's, that floor's picture replaces the grid.
        var cam0 = Camera.main;
        int floor = cam0 != null && MinimapLevels.Levels.Count > 1 ? MinimapLevels.LevelOf(cam0.transform.position.y - 1.6f) : -1;
        var level = floor >= 0 && floor != MinimapLevels.Main ? MinimapLevels.Levels[floor] : null;

        // Dark backing only behind the maze grid; entrance/exit areas outside it stay over the game view.
        Ui.Panel(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8));
        if (level?.Tex != null)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.15f); // the maze above/below, faint, for orientation
            GUI.DrawTexture(r, _tex, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            GUI.DrawTexture(CellRect(level.GridRect), level.Tex, ScaleMode.StretchToFill, true);
        }
        else GUI.DrawTexture(r, _tex, ScaleMode.StretchToFill, true);
        _style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

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
            var a = ToScreen(ToGrid(b.min));
            var c = ToScreen(ToGrid(b.max));
            var zr = Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
            if (zr.width < 3f) zr = new Rect(zr.center.x - 1.5f, zr.y, 3f, zr.height);
            if (zr.height < 3f) zr = new Rect(zr.x, zr.center.y - 1.5f, zr.width, 3f);
            GUI.DrawTexture(zr, _safe);
        }

        // Things on another floor than yours are drawn faint.
        Color On(Color c, float y) => floor < 0 || MinimapLevels.LevelOf(y) == floor ? c : new Color(c.r, c.g, c.b, 0.35f);
        if (_end.x >= 0) Dot(ToScreen(_end), Mathf.Max(6f, scale * 1.5f), On(new Color(1f, 0.3f, 0.25f), _origin.y), "S");
        foreach (var (pos, _, _) in CosmeticAlert.PendingTargets())
            Dot(ToScreen(ToGrid(pos)), 8f, On(new Color(0.35f, 0.69f, 1f), pos.y), "◆");
        foreach (var c in Objectives.Pending)
            Dot(ToScreen(ToGrid(c.Pos)), 10f, On(Objectives.Colour, c.Pos.y), Objectives.Glyph(c.Kind));
        foreach (var (pos, chasing) in Monsters)
            Dot(ToScreen(ToGrid(pos)), chasing ? 11f : 9f, On(chasing ? new Color(1f, 0.15f, 0.1f) : new Color(1f, 0.6f, 0.15f), pos.y), "X");
        foreach (var p in Players.Others())
            Dot(ToScreen(ToGrid(p.Pos)), 8f, On(p.Dead ? new Color(1f, 0.45f, 0.4f) : new Color(0.45f, 1f, 0.5f), p.Pos.y), "●");

        // Which floor, when the map has more than one.
        if (floor >= 0)
        {
            _levelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft };
            int num = MinimapLevels.Levels[floor].Number;
            string label = num == 0 ? Loc.T("Nivel principal", "Main floor")
                : num < 0 ? Loc.T($"Subsuelo {-num}", $"Basement {-num}") : Loc.T($"Piso +{num}", $"Floor +{num}");
            var lr = new Rect(r.x + 4, r.yMax - 24, 220, 22);
            GUI.color = Color.black;
            GUI.Label(new Rect(lr.x + 1, lr.y + 1, lr.width, lr.height), label, _levelStyle);
            GUI.color = new Color(0.4f, 0.95f, 1f);
            GUI.Label(lr, label, _levelStyle);
            GUI.color = Color.white;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            var me = ToScreen(ToGrid(cam.transform.position));
            var ahead = ToScreen(ToGrid(cam.transform.position + new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z).normalized * 10f));
            float angle = Mathf.Atan2(ahead.x - me.x, -(ahead.y - me.y)) * Mathf.Rad2Deg; // 0 = up on screen
            // Big arrow (drawn texture, not a font glyph): yellow with a black outline, pointing where you look.
            _arrow ??= MakeArrow();
            float a = Mathf.Clamp(size * 0.11f, 22f, 44f);
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, me);
            GUI.DrawTexture(new Rect(me.x - a / 2, me.y - a / 2, a, a), _arrow, ScaleMode.StretchToFill, true);
            GUI.matrix = saved;
        }

        // Who still has to pick up the case cosmetic, under the map (party members with the mod).
        var missing = CosmeticAlert.StillToPickUp();
        if (missing.Count > 0)
        {
            _note ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight, wordWrap = true };
            string names = string.Join(", ", missing.Select(m => m.IsMe ? Loc.T("vos", "you") : m.Name));
            string text = Loc.T($"◆ Falta agarrar el cosmético: {names}", $"◆ Cosmetic not picked up yet: {names}");
            var tr = new Rect(Screen.width - 16 - Mathf.Max(v.width, 320f), v.yMax + 8, Mathf.Max(v.width, 320f), 44);
            GUI.color = Color.black;
            GUI.Label(new Rect(tr.x + 1, tr.y + 1, tr.width, tr.height), text, _note);
            GUI.color = new Color(0.35f, 0.69f, 1f);
            GUI.Label(tr, text, _note);
            GUI.color = Color.white;
        }
    }

    private static GUIStyle _note, _levelStyle;
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
        foreach (var b in SafeZones) { pts.Add(ToGrid(b.min)); pts.Add(ToGrid(b.max)); }
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

    private static void Dot(Vector2 at, float size, Color color, string glyph)
    {
        _style.fontSize = Mathf.RoundToInt(Mathf.Clamp(size * 1.6f, 10f, 26f));
        var rect = new Rect(at.x - 15, at.y - 15, 30, 30);
        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), glyph, _style);
        GUI.color = color;
        GUI.Label(rect, glyph, _style);
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
                Monsters.Add((sync.transform.position, chasing));
            }
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Minimap: monsters: {e.Message}");
            _nextMonsters = Time.unscaledTime + 10f;
        }
    }
}
