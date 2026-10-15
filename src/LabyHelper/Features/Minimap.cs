using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppValkoGames.Labyrinthine.Monsters;
using UnityEngine;
using AnimState = Il2Cpp.AnimationState;
using CaseInteractableObject = Il2CppRandomGeneration.Interactable.CaseInteractableObject;
using InsertPlaceholder = Il2CppRandomGeneration.Interactable.RndInteractableItemInsertPlaceholder;
using ObjectiveTrigger = Il2CppRandomGeneration.RndUpdateObjectivesOnTrigger;
using MazeGInteractable = Il2CppValkoGames.Labyrinthine.Cases.Interactions.RndMazeGInteractable;
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
/// still on the ground, the exit and the case objectives (see RefreshObjectives). F4 shows/hides it.
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
    private enum Clue { Item, Slot, Zone }
    private static readonly List<(Vector3 pos, Clue kind)> Clues = new();
    private static float _nextClues;
    private static string _clueSummary;
    private static readonly Color ClueColor = new(0.95f, 0.35f, 1f);
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
        if (Time.unscaledTime >= _nextClues) RefreshObjectives();
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
        float size = Mathf.Clamp(Settings.MinimapSize.Value, 120f, 600f);
        float scale = size / Mathf.Max(_sizeX, _sizeY);
        float w = _sizeX * scale, h = _sizeY * scale;
        var r = new Rect(Screen.width - w - 16, 16, w, h);

        Ui.Panel(new Rect(r.x - 4, r.y - 4, w + 8, h + 8));
        GUI.DrawTexture(r, _tex, ScaleMode.StretchToFill, true);
        _style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

        // Grid y grows north; screen y grows down.
        Vector2 ToScreen(Vector2 g) => new(r.x + (g.x + 0.5f) * scale, r.yMax - (g.y + 0.5f) * scale);

        if (_end.x >= 0) Dot(ToScreen(_end), Mathf.Max(6f, scale * 1.5f), new Color(1f, 0.3f, 0.25f), "S");
        foreach (var (pos, _, _) in CosmeticAlert.PendingTargets())
            Dot(ToScreen(ToGrid(pos)), 8f, new Color(0.35f, 0.69f, 1f), "◆");
        foreach (var (pos, kind) in Clues)
            Dot(ToScreen(ToGrid(pos)), 10f, ClueColor, kind switch { Clue.Slot => "O", Clue.Zone => "◎", _ => "◆" });
        foreach (var (pos, chasing) in Monsters)
            Dot(ToScreen(ToGrid(pos)), chasing ? 11f : 9f, chasing ? new Color(1f, 0.15f, 0.1f) : new Color(1f, 0.6f, 0.15f), "X");
        foreach (var p in Players.Others())
            Dot(ToScreen(ToGrid(p.Pos)), 8f, p.Dead ? new Color(1f, 0.45f, 0.4f) : new Color(0.45f, 1f, 0.5f), "●");

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

    /// <summary>
    /// What the case asks you to do, once a second (only what's still pending):
    ///  - CaseInteractableObject (seals, effigies, levers, gasoline, map-specific items): pick up / use / destroy while
    ///    not IsPickedUp, not IsInserted, not IsDisabled. Magenta diamond.
    ///  - RndInteractableItemInsertPlaceholder: where a picked-up item goes, until IsInserted. Magenta O.
    ///  - RndUpdateObjectivesOnTrigger: "reach this place" objectives, while their trigger collider is on. Magenta ◎.
    ///  - RndMazeGInteractable (Maze_G objective objects). Magenta diamond.
    /// </summary>
    private static void RefreshObjectives()
    {
        _nextClues = Time.unscaledTime + 1f;
        Clues.Clear();
        try
        {
            foreach (var o in UnityEngine.Object.FindObjectsOfType<CaseInteractableObject>())
            {
                if (o == null || !o.isActiveAndEnabled || o.IsDisabled || o.IsInserted) continue;
                bool slot = o.TryCast<InsertPlaceholder>() != null;
                if (!slot && o.IsPickedUp) continue;
                Clues.Add((o.transform.position, slot ? Clue.Slot : Clue.Item));
            }
            foreach (var t in UnityEngine.Object.FindObjectsOfType<ObjectiveTrigger>())
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                var col = t.GetComponent<Collider>();
                if (col != null && !col.enabled) continue;
                Clues.Add((t.transform.position, Clue.Zone));
            }
            foreach (var g in UnityEngine.Object.FindObjectsOfType<MazeGInteractable>())
                if (g != null && g.isActiveAndEnabled) Clues.Add((g.transform.position, Clue.Item));
            string summary = $"{Clues.Count(c => c.kind == Clue.Item)} item(s), {Clues.Count(c => c.kind == Clue.Slot)} slot(s), {Clues.Count(c => c.kind == Clue.Zone)} zone(s)";
            if (summary != _clueSummary) Core.Log.Msg($"Minimap: objectives pending: {summary}.");
            _clueSummary = summary;
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Minimap: objectives: {e.Message}");
            _nextClues = Time.unscaledTime + 10f;
        }
    }
}
