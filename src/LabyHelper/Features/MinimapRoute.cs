using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Big map and route (by request). M opens the minimap large in the middle of the screen (same marks as the corner
/// one); left click marks a destination, right click clears it. The route there is a shortest path over the maze grid
/// (MazeGenerator.mazeMap, every tile that isn't a wall), recomputed from where you stand four times a second, drawn on
/// both maps, and marked in the world: a big 3D arrow at the next corner pointing the way to turn, plus labels with
/// the distances to that corner and to the destination, through walls.
/// The grid is the maze's main floor: steps across closed doors are found with physics and routed around, and the
/// game's NavMesh path says where the real way changes floor (stairs label). Reaching the destination clears it.
/// </summary>
internal static partial class Minimap
{
    private static bool _big;
    private static Vector2Int? _target;
    private static readonly List<Vector2Int> Route = new();
    private static float _nextRoute;
    private static bool[,] _walk; // filled by Build
    private static GUIStyle _routeStyle, _bigTitle;
    private static readonly Color RouteColour = new(0.3f, 0.9f, 1f);

    public static bool BigOpen => _big;

    public static void CloseBig() { if (_big) SetBig(false); }

    private static void SetBig(bool open)
    {
        if (open == _big) return;
        _big = open;
        if (open)
        {
            Ui.Capture("MAP", true); // player stops, cursor free (released below)
            OptionsMenu.Close();
            CollectionViewer.Close();
        }
        else Ui.Capture("MAP", false);
    }

    private static void ResetRoute()
    {
        SetBig(false);
        _target = null;
        Route.Clear();
        Edges.Clear();
        Blocked.Clear();
        BlockedOnRoute.Clear();
        _levelAt = null;
        _arrowGo = null; // destroyed with the scene
    }

    /// <summary>Called from Update while in a case.</summary>
    private static void UpdateRoute()
    {
        if (Input.GetKeyDown(KeyCode.M) && _mapPtr != IntPtr.Zero) SetBig(!_big);
        if (_big && Input.GetKeyDown(KeyCode.Escape)) SetBig(false);
        UpdateArrow();
        if (_target == null || _walk == null || Time.unscaledTime < _nextRoute) return;
        _nextRoute = Time.unscaledTime + 0.25f;
        var cam = Camera.main;
        if (cam == null) return;
        var g = ToGrid(cam.transform.position - new Vector3(0f, 1.6f, 0f));
        var start = Nearest(new Vector2Int(Mathf.RoundToInt(g.x), Mathf.RoundToInt(g.y)));
        Route.Clear();
        if (start == null) return;
        if ((start.Value - _target.Value).sqrMagnitude <= 1)
        {
            _target = null;
            Toast.Show(Loc.T("Llegaste al destino", "You reached the destination"), 3f);
            return;
        }
        RouteAvoidingDoors(start.Value, _target.Value);
        if (Time.unscaledTime >= _nextLevels) { _nextLevels = Time.unscaledTime + 1f; FindLevelChange(cam.transform.position); }
    }

    // ---- closed doors ----

    // Edge between two neighbouring cells -> (blocked, when it was probed). Probed again after DoorRecheck seconds, so
    // a door that opens frees the way.
    private static readonly Dictionary<(Vector2Int, Vector2Int), (bool blocked, float at)> Edges = new();
    private static readonly HashSet<(Vector2Int, Vector2Int)> Blocked = new();
    private static readonly List<Vector2Int> BlockedOnRoute = new(); // doors the route still crosses (no way around)
    private const float DoorRecheck = 2f, ProbeHeight = 1.2f, ProbeRadius = 0.25f;

    /// <summary>
    /// Shortest grid route that doesn't cross a closed door or gate. The grid has no doors, so each step of the
    /// candidate route is probed with a sphere cast at chest height; a door-like collider (IsDoor) blocks that step and
    /// the search runs again without it, up to 40 times. With no way around, the route goes through and the doors on
    /// it are labelled "puerta cerrada".
    /// </summary>
    private static void RouteAvoidingDoors(Vector2Int from, Vector2Int to)
    {
        BlockedOnRoute.Clear();
        foreach (var k in Edges.Where(e => Time.unscaledTime - e.Value.at > DoorRecheck).Select(e => e.Key).ToList())
        {
            Edges.Remove(k);
            Blocked.Remove(k);
        }
        var path = new List<Vector2Int>();
        for (int tries = 0; tries < 40; tries++)
        {
            path.Clear();
            Bfs(from, to, path);
            if (path.Count < 2) break;
            bool clear = true;
            for (int i = 1; i < path.Count; i++)
                if (EdgeBlocked(path[i - 1], path[i])) { clear = false; break; }
            if (clear) { Route.AddRange(path); return; }
        }
        // No door-free way: the plain route, with its closed doors marked.
        var saved = new List<(Vector2Int, Vector2Int)>(Blocked);
        Blocked.Clear();
        path.Clear();
        Bfs(from, to, path);
        foreach (var k in saved) Blocked.Add(k);
        Route.AddRange(path);
        for (int i = 1; i < path.Count; i++)
            if (Blocked.Contains(Key(path[i - 1], path[i]))) BlockedOnRoute.Add(path[i]);
    }

    private static (Vector2Int, Vector2Int) Key(Vector2Int a, Vector2Int b)
        => a.x < b.x || (a.x == b.x && a.y <= b.y) ? (a, b) : (b, a);

    private static bool EdgeBlocked(Vector2Int a, Vector2Int b)
    {
        var k = Key(a, b);
        if (Edges.TryGetValue(k, out var e)) return e.blocked;
        bool blocked = false;
        try
        {
            var pa = GridToWorld(a, _origin.y + ProbeHeight);
            var pb = GridToWorld(b, _origin.y + ProbeHeight);
            var d = pb - pa;
            foreach (var hit in Physics.SphereCastAll(pa, ProbeRadius, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (IsDoor(hit.collider)) { blocked = true; break; }
        }
        catch { /* physics not ready */ }
        Edges[k] = (blocked, Time.unscaledTime);
        if (blocked) Blocked.Add(k);
        return blocked;
    }

    /// <summary>
    /// A closed door or gate: the game's Door with isOpen false, or a collider under an object whose name says
    /// door/gate (Gate, CryptGate, MazeGate, MausoleumDoor, the Egyptian and valve gates...). An open door has swung
    /// its collider out of the corridor, so it isn't hit.
    /// </summary>
    private static bool IsDoor(Collider c)
    {
        if (c == null) return false;
        var door = c.GetComponentInParent<Il2Cpp.Door>();
        if (door != null) return !door.isOpen;
        for (var t = c.transform; t != null; t = t.parent)
        {
            string n = t.name ?? "";
            if (n.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("gate", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("puerta", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    // ---- other floors ----

    private static float _nextLevels;
    private static Vector3? _levelAt; // where the real walking path changes floor
    private static int _levelDir;     // -1 down, +1 up
    private static float _levelDist;  // metres walked until there

    /// <summary>
    /// The grid route stays on the maze's main plane, but the real way can go down to a basement or up to another
    /// floor. The game's NavMesh covers every floor, so its path from you to the destination shows where the height
    /// changes by more than 2 m: that spot gets a "bajá / subí las escaleras" label.
    /// </summary>
    private static void FindLevelChange(Vector3 eye)
    {
        _levelAt = null;
        if (_target == null) return;
        try
        {
            var feet = eye - new Vector3(0f, 1.6f, 0f);
            var goal = GridToWorld(_target.Value, _origin.y);
            if (!UnityEngine.AI.NavMesh.SamplePosition(feet, out var a, 3f, UnityEngine.AI.NavMesh.AllAreas)) return;
            if (!UnityEngine.AI.NavMesh.SamplePosition(goal, out var b, 4f, UnityEngine.AI.NavMesh.AllAreas)) return;
            var path = new UnityEngine.AI.NavMeshPath();
            if (!UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, path)) return;
            var corners = path.corners;
            if (corners == null || corners.Length < 2) return;
            float walked = 0f, baseY = corners[0].y;
            for (int i = 1; i < corners.Length; i++)
            {
                float seg = Vector3.Distance(corners[i - 1], corners[i]);
                walked += seg;
                float dy = corners[i].y - baseY;
                if (Mathf.Abs(dy) < 2f) continue;
                _levelAt = corners[i - 1]; // the last corner still at your height: where the stairs start
                _levelDir = dy < 0 ? -1 : 1;
                _levelDist = walked - seg;
                return;
            }
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Route: floors: {e.Message}");
            _nextLevels = Time.unscaledTime + 10f;
        }
    }

    /// <summary>The closest walkable cell to <paramref name="c"/> (clamped into the grid), within 4 cells.</summary>
    private static Vector2Int? Nearest(Vector2Int c)
    {
        c = new Vector2Int(Mathf.Clamp(c.x, 0, _sizeX - 1), Mathf.Clamp(c.y, 0, _sizeY - 1));
        for (int r = 0; r <= 4; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    int x = c.x + dx, y = c.y + dy;
                    if (x >= 0 && y >= 0 && x < _sizeX && y < _sizeY && _walk[x, y]) return new Vector2Int(x, y);
                }
        return null;
    }

    private static readonly Vector2Int[] Steps = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    private static void Bfs(Vector2Int from, Vector2Int to, List<Vector2Int> path)
    {
        var prev = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
        var q = new Queue<Vector2Int>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (c == to) break;
            foreach (var s in Steps)
            {
                var n = c + s;
                if (n.x < 0 || n.y < 0 || n.x >= _sizeX || n.y >= _sizeY || !_walk[n.x, n.y] || prev.ContainsKey(n)) continue;
                prev[n] = c;
                q.Enqueue(n);
            }
        }
        if (!prev.ContainsKey(to)) return; // unreachable on this floor
        for (var c = to; ; c = prev[c]) { path.Add(c); if (c == from) break; }
        path.Reverse();
    }

    private static Vector3 GridToWorld(Vector2Int c, float y)
    {
        var w = _origin + _ax * c.x + _ay * c.y;
        return new Vector3(w.x, y, w.z);
    }

    /// <summary>Where the screen rectangle of the map goes: the corner, or big in the middle while M is open.</summary>
    private static Rect MapRect(float viewW, float viewH, out float scale)
    {
        if (!_big)
        {
            float size = Mathf.Clamp(Settings.MinimapSize.Value, 120f, 600f);
            scale = size / Mathf.Max(viewW, viewH);
            return new Rect(Screen.width - viewW * scale - 16, 16, viewW * scale, viewH * scale);
        }
        // Bigger mazes get more of the screen: a small one (~15 tiles) about two thirds of the height, as before, a big
        // one (60+) almost all of it, each axis fitted on its own so a long, flat maze uses the width.
        float t = Mathf.InverseLerp(15f, 60f, Mathf.Max(viewW, viewH));
        float availW = Screen.width * Mathf.Lerp(0.6f, 0.95f, t), availH = Screen.height * Mathf.Lerp(0.66f, 0.84f, t);
        scale = Mathf.Min(availW / viewW, availH / viewH);
        float w = viewW * scale, h = viewH * scale;
        return new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f + Screen.height * 0.03f, w, h);
    }

    /// <summary>Behind the big map: dim the game and say what the mouse does.</summary>
    private static void DrawBigBackdrop(Rect v)
    {
        Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));
        _bigTitle ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * Ui.Scale), alignment = TextAnchor.MiddleCenter };
        Ui.Text(new Rect(0, v.y - 70 * Ui.Scale, Screen.width, 50 * Ui.Scale),
            Loc.T("Clic: marcar destino   ·   Clic derecho: borrarlo   ·   M o Esc: cerrar",
                  "Click: mark destination   ·   Right click: clear it   ·   M or Esc: close"), _bigTitle, Ui.Paper);
    }

    /// <summary>Clicks on the big map set or clear the destination.</summary>
    private static void HandleBigClicks(Rect v, Rect r, float scale)
    {
        var e = Event.current;
        if (e.type != EventType.MouseDown || !v.Contains(e.mousePosition)) return;
        if (e.button == 1) { _target = null; Route.Clear(); e.Use(); return; }
        if (e.button != 0) return;
        var g = new Vector2Int(Mathf.RoundToInt((e.mousePosition.x - r.x) / scale - 0.5f), Mathf.RoundToInt((r.yMax - e.mousePosition.y) / scale - 0.5f));
        var cell = _walk != null ? Nearest(g) : null;
        if (cell != null) { _target = cell; _nextRoute = 0f; }
        e.Use();
    }

    /// <summary>The route as a solid line and the destination ring, on whichever map is being drawn.</summary>
    private static void DrawRouteOnMap(Func<Vector2, Vector2> toScreen, float scale)
    {
        if (_target == null) return;
        if (Route.Count >= 2)
        {
            var pts = new List<Vector2>(Route.Count);
            foreach (var c in Route) pts.Add(toScreen(c));
            DrawLine(pts, RouteColour, Mathf.Clamp(scale * 0.45f, 3f, 8f));
        }
        foreach (var d in BlockedOnRoute) Dot(toScreen(d), Mathf.Max(12f, scale), new Color(1f, 0.45f, 0.35f), "X"); // closed doors
        float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
        Dot(toScreen(_target.Value), Mathf.Max(12f, scale) * pulse, RouteColour, "◎");
    }

    private static void DrawLine(List<Vector2> points, Color col, float width)
    {
        if (points.Count < 2 || Event.current.type != EventType.Repaint) return;
        _px ??= Ui.Solid(Color.white);
        var saved = GUI.matrix;
        GUI.color = col;
        for (int i = 1; i < points.Count; i++)
        {
            var from = points[i - 1];
            var d = points[i] - from;
            GUI.matrix = saved;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x - width / 2f, from.y - width / 2f, d.magnitude + width, width), _px);
        }
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    /// <summary>In the world, through walls: the next corner to turn at, and the destination, with walking distances.</summary>
    public static void DrawRouteMarkers()
    {
        if (_target == null || Route.Count < 2 || !GameState.InCase || _big) return;
        var cam = Camera.main;
        if (cam == null) return;
        float y = cam.transform.position.y - 0.6f;
        float cell = _ax.magnitude;
        _routeStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        // First cell where the direction changes = where to turn next.
        int turn = Route.Count - 1;
        for (int i = 1; i < Route.Count - 1; i++)
            if (Route[i] - Route[i - 1] != Route[i + 1] - Route[i]) { turn = i; break; }
        float total = (Route.Count - 1) * cell;
        if (turn < Route.Count - 1)
            Label(cam, GridToWorld(Route[turn], y), Loc.T($"Doblá acá\n{turn * cell:0} m", $"Turn here\n{turn * cell:0} m"), Color.white, turn * cell);
        string floors = _levelAt == null ? "" : _levelDir < 0
            ? Loc.T("\n(otro nivel, abajo)", "\n(another floor, below)") : Loc.T("\n(otro nivel, arriba)", "\n(another floor, above)");
        Label(cam, GridToWorld(Route[^1], y + 1f), Loc.T($"Destino\n{total:0} m", $"Destination\n{total:0} m") + floors, RouteColour, total);
        foreach (var d in BlockedOnRoute)
        {
            float m = Mathf.Max(0, Route.IndexOf(d)) * cell;
            Label(cam, GridToWorld(d, y + 0.5f), Loc.T($"Puerta cerrada\n{m:0} m", $"Closed door\n{m:0} m"), new Color(1f, 0.45f, 0.35f), m);
        }
        if (_levelAt != null)
            Label(cam, _levelAt.Value + Vector3.up * 1.4f,
                  (_levelDir < 0 ? Loc.T("Bajá las escaleras", "Go down the stairs") : Loc.T("Subí las escaleras", "Go up the stairs")) + $"\n{_levelDist:0} m",
                  new Color(1f, 0.85f, 0.3f), _levelDist);
    }

    // ---- 3D turn arrow ----

    private static GameObject _arrowGo;
    private static Material _arrowMat;

    /// <summary>
    /// Every frame: a big glowing arrow at the next corner, standing upright and facing you as you walk towards it
    /// (its plane is perpendicular to the corridor you're in), pointing the way to turn. It bobs, nudges towards the
    /// turn and pulses. Hidden when there's no turn left before the destination, or no route.
    /// </summary>
    private static void UpdateArrow()
    {
        int turn = NextTurn();
        var cam = Camera.main;
        if (turn < 0 || cam == null || !GameState.InCase)
        {
            if (_arrowGo != null) _arrowGo.SetActive(false);
            return;
        }
        if (_arrowGo == null) { if (_arrowFailed) return; _arrowGo = MakeArrow3D(); }
        if (_arrowGo == null) return;

        Vector2Int a = Route[turn] - Route[turn - 1], b = Route[turn + 1] - Route[turn];
        var approach = (_ax * a.x + _ay * a.y).normalized;
        var outDir = (_ax * b.x + _ay * b.y).normalized;
        approach.y = 0f;
        outDir.y = 0f;
        float t = Time.unscaledTime;
        float feet = cam.transform.position.y - 1.6f;
        var corner = GridToWorld(Route[turn], feet);
        _arrowGo.SetActive(true);
        _arrowGo.transform.position = corner + Vector3.up * (2.2f + 0.15f * Mathf.Sin(t * 2.5f)) + outDir * (0.35f * Mathf.Sin(t * 4f));
        _arrowGo.transform.rotation = Quaternion.LookRotation(outDir, -approach); // flat in the plane facing you, pointing the turn
        float s = 1f + 0.06f * Mathf.Sin(t * 4f);
        _arrowGo.transform.localScale = new Vector3(s, s, s);
        if (_arrowMat != null)
        {
            var c = RouteColour * (0.75f + 0.25f * Mathf.Sin(t * 4f));
            c.a = 1f;
            _arrowMat.SetColor("_UnlitColor", c);
            _arrowMat.SetColor("_BaseColor", c);
        }
    }

    /// <summary>Index in Route of the next cell where the direction changes, or -1 when there's none.</summary>
    private static int NextTurn()
    {
        if (_target == null || Route.Count < 3) return -1;
        for (int i = 1; i < Route.Count - 1; i++)
            if (Route[i] - Route[i - 1] != Route[i + 1] - Route[i]) return i;
        return -1;
    }

    /// <summary>A 3 m arrow pointing +Z, lying in the XZ plane, 0.2 m thick, unlit cyan (glows in the dark maze).</summary>
    private static GameObject MakeArrow3D()
    {
        try
        {
            // Outline (x, z), counter-clockwise seen from +Y: shaft then head.
            var o = new[]
            {
                new Vector2(-0.3f, -1.5f), new Vector2(0.3f, -1.5f), new Vector2(0.3f, 0.2f), new Vector2(0.85f, 0.2f),
                new Vector2(0f, 1.5f), new Vector2(-0.85f, 0.2f), new Vector2(-0.3f, 0.2f),
            };
            const float half = 0.1f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var p in o) verts.Add(new Vector3(p.x, half, p.y));  // 0..6 top
            foreach (var p in o) verts.Add(new Vector3(p.x, -half, p.y)); // 7..13 bottom
            // Top (clockwise seen from +Y): shaft quad (0,1,2,6) and head triangle (5,4,3), whose base covers the joint.
            int[] top = { 0, 2, 1, 0, 6, 2, 5, 4, 3 };
            tris.AddRange(top);
            for (int i = 0; i < top.Length; i += 3) tris.AddRange(new[] { top[i + 2] + 7, top[i + 1] + 7, top[i] + 7 }); // bottom faces down
            // Sides: one quad per outline edge, with its own vertices.
            for (int i = 0; i < o.Length; i++)
            {
                int j = (i + 1) % o.Length, k = verts.Count;
                verts.Add(new Vector3(o[i].x, half, o[i].y));
                verts.Add(new Vector3(o[j].x, half, o[j].y));
                verts.Add(new Vector3(o[j].x, -half, o[j].y));
                verts.Add(new Vector3(o[i].x, -half, o[i].y));
                tris.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                tris.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 }); // both windings: no culling surprises
            }
            var mesh = new Mesh { name = "LabyHelper Arrow" };
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("LabyHelper Turn Arrow");
            go.AddComponent<MeshFilter>().mesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("HDRP/Lit");
            if (shader == null) throw new Exception("no HDRP shader found");
            _arrowMat = new Material(shader);
            _arrowMat.SetColor("_UnlitColor", RouteColour);
            _arrowMat.SetColor("_BaseColor", RouteColour);
            if (_arrowMat.HasProperty("_EmissiveColor")) _arrowMat.SetColor("_EmissiveColor", RouteColour * 8f);
            r.material = _arrowMat;
            Core.Log.Msg($"Route: 3D turn arrow ready ({shader.name}).");
            return go;
        }
        catch (Exception e)
        {
            Core.Log.Warning($"Route: 3D arrow: {e.Message}");
            _arrowFailed = true;
            return null;
        }
    }

    private static bool _arrowFailed;

    // Route labels: never smaller than LabelMin (1080p pixels), growing up to LabelMax as you walk up to the spot.
    private const float LabelMin = 22f, LabelMax = 54f, LabelFar = 40f, LabelNear = 3f;

    private static void Label(Camera cam, Vector3 world, string text, Color colour, float metres)
    {
        float near = Mathf.InverseLerp(LabelFar, LabelNear, metres);
        int size = Mathf.RoundToInt(Mathf.Lerp(LabelMin, LabelMax, near * near) * Ui.Scale);
        _routeStyle.fontSize = size;
        Vector3 sp = cam.WorldToScreenPoint(world);
        bool behind = sp.z < 0;
        if (behind) sp = -sp;
        float w = size * 14f, h = size * 2.6f, margin = size * 3f;
        float x = Mathf.Clamp(sp.x, margin, Screen.width - margin), yy = Mathf.Clamp(Screen.height - sp.y, h / 2f + 10f, Screen.height - h / 2f - 10f);
        var rect = new Rect(x - w / 2f, yy - h / 2f, w, h);
        float shadow = Mathf.Max(2f, size / 12f);
        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x + shadow, rect.y + shadow, rect.width, rect.height), Ui.Caps(text), _routeStyle);
        GUI.color = colour;
        GUI.Label(rect, Ui.Caps(text), _routeStyle);
        GUI.color = Color.white;
    }
}
