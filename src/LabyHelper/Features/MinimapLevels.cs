using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Floors of the current map for the minimap. The maze grid (MazeGenerator.mazeMap) is a single plane, but some
/// structures go below or above it (the crypts under Maze_H, at about y = -26). The NavMesh covers everything
/// walkable on every floor, so its triangulation (NavMesh.CalculateTriangulation) is split by height:
///  - flat triangles are binned by height; bins with enough area, merged across gaps under 3 m, are the floors;
///  - each floor is rasterised into its own texture in grid space (4 px per tile);
///  - triangles between floors (ramps, stairs) are grouped by tile into connections, each with the two floors it
///    joins, drawn as ▲ / ▼ on the floor you're on.
/// The floor closest to the grid's own height is the main one (drawn with the maze texture as before).
/// </summary>
internal static class MinimapLevels
{
    public class Level
    {
        public float MinY, MaxY;
        public Texture2D Tex;
        public Rect GridRect; // grid units covered by Tex (cell x spans [x, x + 1))
        public int Number; // 0 = main floor, -1 below, +1 above...
        public float Mid => (MinY + MaxY) / 2f;
    }

    public record Stair(Vector2 Grid, int From, int To); // level indices: lower, upper

    public static readonly List<Level> Levels = new();
    public static readonly List<Stair> Stairs = new();
    public static int Main = -1;

    private const float Bin = 0.5f, MergeGap = 3f, BandPad = 1.5f, PxPerTile = 4f;
    private static int _vertexCount = -1;

    public static void Clear()
    {
        foreach (var l in Levels) if (l.Tex != null) UnityEngine.Object.Destroy(l.Tex);
        Levels.Clear();
        Stairs.Clear();
        Main = -1;
        _vertexCount = -1;
    }

    /// <summary>Index of the floor containing this height (nearest band), or -1 if there are no floors.</summary>
    public static int LevelOf(float y)
    {
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < Levels.Count; i++)
        {
            var l = Levels[i];
            float d = y < l.MinY - BandPad ? l.MinY - BandPad - y : y > l.MaxY + BandPad ? y - l.MaxY - BandPad : 0f;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>(Re)builds the floors from the NavMesh; a no-op when it hasn't changed since the last call.</summary>
    public static void Analyze(Func<Vector3, Vector2> toGrid, float gridY)
    {
        var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
        var verts = tri.vertices?.ToArray();
        var idx = tri.indices?.ToArray();
        if (verts == null || idx == null || verts.Length == 0 || verts.Length == _vertexCount) return;
        _vertexCount = verts.Length;
        foreach (var l in Levels) if (l.Tex != null) UnityEngine.Object.Destroy(l.Tex);
        Levels.Clear();
        Stairs.Clear();
        Main = -1;

        int n = idx.Length / 3;
        var cy = new float[n];
        var area = new float[n];
        var span = new float[n];
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int t = 0; t < n; t++)
        {
            Vector3 a = verts[idx[3 * t]], b = verts[idx[3 * t + 1]], c = verts[idx[3 * t + 2]];
            cy[t] = (a.y + b.y + c.y) / 3f;
            span[t] = Mathf.Max(a.y, b.y, c.y) - Mathf.Min(a.y, b.y, c.y);
            area[t] = Mathf.Abs((b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z)) / 2f;
            minY = Mathf.Min(minY, cy[t]);
            maxY = Mathf.Max(maxY, cy[t]);
        }

        // Height histogram of the flat triangles, by area.
        int bins = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / Bin) + 1);
        var hist = new float[bins];
        float total = 0f;
        for (int t = 0; t < n; t++)
        {
            if (span[t] > 1f) continue;
            hist[(int)((cy[t] - minY) / Bin)] += area[t];
            total += area[t];
        }
        if (total <= 0f) return;

        // Floors: runs of bins with real area, merged across small gaps; tiny ones dropped.
        var bands = new List<(float lo, float hi, float area)>();
        int gapBins = Mathf.RoundToInt(MergeGap / Bin);
        int start = -1, lastFull = -1;
        float acc = 0f;
        for (int i = 0; i <= bins; i++)
        {
            bool full = i < bins && hist[i] >= total * 0.003f;
            if (full)
            {
                if (start < 0) start = i;
                lastFull = i;
                acc += hist[i];
            }
            else if (start >= 0 && (i == bins || i - lastFull > gapBins))
            {
                bands.Add((minY + start * Bin, minY + (lastFull + 1) * Bin, acc));
                start = -1;
                acc = 0f;
            }
        }
        bands.RemoveAll(b => b.area < total * 0.02f);
        if (bands.Count < 2) { Log(1); return; } // one floor: the maze grid already says it all

        foreach (var (lo, hi, _) in bands) Levels.Add(new Level { MinY = lo, MaxY = hi });
        Main = LevelOf(gridY);
        for (int i = 0; i < Levels.Count; i++) Levels[i].Number = i - Main; // sorted by height: below main < 0

        // Assign triangles to floors; the rest are connections between floors.
        var perLevel = Levels.Select(_ => new List<int>()).ToList();
        var links = new Dictionary<Vector2Int, (float lo, float hi, Vector2 sum, int count)>();
        for (int t = 0; t < n; t++)
        {
            int li = InBand(cy[t]);
            if (li >= 0 && span[t] <= 2f) { perLevel[li].Add(t); continue; }
            Vector3 a = verts[idx[3 * t]], b = verts[idx[3 * t + 1]], c = verts[idx[3 * t + 2]];
            var g = toGrid((a + b + c) / 3f);
            var cell = new Vector2Int(Mathf.FloorToInt(g.x + 0.5f), Mathf.FloorToInt(g.y + 0.5f));
            float lo = Mathf.Min(a.y, b.y, c.y), hi = Mathf.Max(a.y, b.y, c.y);
            links[cell] = links.TryGetValue(cell, out var e)
                ? (Mathf.Min(e.lo, lo), Mathf.Max(e.hi, hi), e.sum + g, e.count + 1)
                : (lo, hi, g, 1);
            if (li >= 0) perLevel[li].Add(t); // a steep piece still belongs on its floor's picture
        }
        // A connection touches two floors: its lowest point near one, its highest near another.
        foreach (var (cell, e) in MergeCells(links))
        {
            int from = LevelOf(e.lo), to = LevelOf(e.hi);
            if (from >= 0 && to >= 0 && from != to) Stairs.Add(new Stair(e.sum / e.count, from, to));
        }

        for (int i = 0; i < Levels.Count; i++) Rasterise(Levels[i], perLevel[i], verts, idx, toGrid);
        Log(Levels.Count);
    }

    private static int InBand(float y)
    {
        for (int i = 0; i < Levels.Count; i++)
            if (y >= Levels[i].MinY - BandPad && y <= Levels[i].MaxY + BandPad) return i;
        return -1;
    }

    /// <summary>Joins neighbouring cells of the same ramp, so one staircase gives one marker.</summary>
    private static IEnumerable<(Vector2Int, (float lo, float hi, Vector2 sum, int count))> MergeCells(
        Dictionary<Vector2Int, (float lo, float hi, Vector2 sum, int count)> cells)
    {
        var seen = new HashSet<Vector2Int>();
        foreach (var start in cells.Keys)
        {
            if (!seen.Add(start)) continue;
            var acc = cells[start];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var nb = new Vector2Int(c.x + dx, c.y + dy);
                        if (!cells.TryGetValue(nb, out var e) || !seen.Add(nb)) continue;
                        acc = (Mathf.Min(acc.lo, e.lo), Mathf.Max(acc.hi, e.hi), acc.sum + e.sum, acc.count + e.count);
                        queue.Enqueue(nb);
                    }
            }
            yield return (start, acc);
        }
    }

    private static void Rasterise(Level level, List<int> tris, Vector3[] verts, int[] idx, Func<Vector3, Vector2> toGrid)
    {
        if (tris.Count == 0) return;
        var g = new Vector2[tris.Count * 3];
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < tris.Count; i++)
            for (int k = 0; k < 3; k++)
            {
                // Grid coordinates are cell centres; +0.5 puts them in texture cell space.
                var p = toGrid(verts[idx[3 * tris[i] + k]]) + new Vector2(0.5f, 0.5f);
                g[3 * i + k] = p;
                x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
            }
        x0 = Mathf.Floor(x0) - 1; y0 = Mathf.Floor(y0) - 1; x1 = Mathf.Ceil(x1) + 1; y1 = Mathf.Ceil(y1) + 1;
        int w = Mathf.Clamp((int)((x1 - x0) * PxPerTile), 1, 2048), h = Mathf.Clamp((int)((y1 - y0) * PxPerTile), 1, 2048);
        var px = new Color32[w * h];
        var floor = new Color32(150, 150, 150, 235);
        for (int i = 0; i < tris.Count; i++)
        {
            Vector2 a = Px(g[3 * i]), b = Px(g[3 * i + 1]), c = Px(g[3 * i + 2]);
            int minX = Mathf.Max(0, (int)Mathf.Min(a.x, b.x, c.x)), maxX = Mathf.Min(w - 1, (int)Mathf.Max(a.x, b.x, c.x));
            int minY = Mathf.Max(0, (int)Mathf.Min(a.y, b.y, c.y)), maxY = Mathf.Min(h - 1, (int)Mathf.Max(a.y, b.y, c.y));
            float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(den) < 1e-6f) continue;
            for (int py = minY; py <= maxY; py++)
                for (int qx = minX; qx <= maxX; qx++)
                {
                    float sx = qx + 0.5f, sy = py + 0.5f;
                    float l1 = ((b.y - c.y) * (sx - c.x) + (c.x - b.x) * (sy - c.y)) / den;
                    float l2 = ((c.y - a.y) * (sx - c.x) + (a.x - c.x) * (sy - c.y)) / den;
                    if (l1 >= -0.05f && l2 >= -0.05f && 1f - l1 - l2 >= -0.05f) px[py * w + qx] = floor;
                }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixels32(px);
        tex.Apply(false);
        level.Tex = tex;
        level.GridRect = Rect.MinMaxRect(x0, y0, x1, y1);

        Vector2 Px(Vector2 p) => new((p.x - x0) * PxPerTile, (p.y - y0) * PxPerTile);
    }

    private static void Log(int floors)
        => Core.Log.Msg(floors < 2
            ? $"Minimap: one floor ({_vertexCount} navmesh vertices)."
            : $"Minimap: {floors} floors ({string.Join(", ", Levels.Select(l => $"{l.Number:+0;-0;0} y {l.MinY:0}..{l.MaxY:0}"))}), " +
              $"{Stairs.Count} stair(s)/ramp(s) between them.");
}
