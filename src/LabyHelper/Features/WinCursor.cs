using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// The Windows arrow while a mod panel is open (F1, F2, big map M). The game's default cursor (a Player Settings
/// texture; the only code that calls Cursor.SetCursor is the loadout screen) has its tip away from where the click
/// lands, so marking a destination on the big map was imprecise. The real system arrow is copied from Windows
/// (LoadCursor IDC_ARROW drawn over black and over white to recover its alpha) with its own hotspot, and set as a
/// hardware cursor; closing the panel puts back the game's default (SetCursor(null)).
/// </summary>
internal static class WinCursor
{
    private static bool _on, _failed;
    private static Texture2D _tex;
    private static Vector2 _hotspot;

    public static void Update()
    {
        bool want = OptionsMenu.IsOpen || CollectionViewer.IsOpen || Minimap.BigOpen;
        if (want == _on || _failed) return;
        try
        {
            if (want)
            {
                if (_tex == null && !Build()) { _failed = true; return; }
                Cursor.SetCursor(_tex, _hotspot, CursorMode.Auto);
            }
            else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _on = want;
        }
        catch (Exception e)
        {
            Core.Log.Warning($"WinCursor: {e.Message}");
            _failed = true;
        }
    }

    private static bool Build()
    {
        IntPtr cursor = LoadCursor(IntPtr.Zero, IdcArrow);
        if (cursor == IntPtr.Zero || !GetIconInfo(cursor, out var info)) return false;
        if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
        if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
        int w = Math.Max(32, GetSystemMetrics(SmCxCursor)), h = Math.Max(32, GetSystemMetrics(SmCyCursor));

        var black = Render(cursor, w, h, 0xFF000000u);
        var white = Render(cursor, w, h, 0xFFFFFFFFu);
        if (black == null || white == null) return false;

        _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "LabyHelper Cursor" };
        UnityEngine.Object.DontDestroyOnLoad(_tex);
        _tex.hideFlags = HideFlags.HideAndDontSave;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                uint b = black[y * w + x], wh = white[y * w + x];
                float a = 1f - Mathf.Clamp01(((wh >> 8) & 0xFF) / 255f - ((b >> 8) & 0xFF) / 255f); // green channel
                Color c = a <= 0.001f ? new Color(0, 0, 0, 0)
                    : new Color(Mathf.Clamp01(((b >> 16) & 0xFF) / 255f / a), Mathf.Clamp01(((b >> 8) & 0xFF) / 255f / a),
                                Mathf.Clamp01((b & 0xFF) / 255f / a), a);
                _tex.SetPixel(x, h - 1 - y, c); // DIB rows go top-down, texture rows bottom-up
            }
        _tex.Apply(false, false);
        _hotspot = new Vector2(info.xHotspot, info.yHotspot);
        Core.Log.Msg($"WinCursor: Windows arrow {w}x{h}, hotspot {info.xHotspot},{info.yHotspot}.");
        return true;
    }

    /// <summary>The cursor drawn on a w x h 32-bit DIB filled with <paramref name="bg"/>, top-down, 0xAARRGGBB.</summary>
    private static uint[] Render(IntPtr cursor, int w, int h, uint bg)
    {
        IntPtr screen = GetDC(IntPtr.Zero), dc = CreateCompatibleDC(screen), bmp = IntPtr.Zero;
        try
        {
            var bmi = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32,
            };
            bmp = CreateDIBSection(dc, ref bmi, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (bmp == IntPtr.Zero || bits == IntPtr.Zero) return null;
            IntPtr old = SelectObject(dc, bmp);
            var px = new int[w * h];
            Array.Fill(px, unchecked((int)bg));
            Marshal.Copy(px, 0, bits, px.Length);
            DrawIconEx(dc, 0, 0, cursor, w, h, 0, IntPtr.Zero, DiNormal);
            GdiFlush();
            Marshal.Copy(bits, px, 0, px.Length);
            SelectObject(dc, old);
            var result = new uint[px.Length];
            for (int i = 0; i < px.Length; i++) result[i] = unchecked((uint)px[i]);
            return result;
        }
        finally
        {
            if (bmp != IntPtr.Zero) DeleteObject(bmp);
            DeleteDC(dc);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    private const int IdcArrow = 32512, SmCxCursor = 13, SmCyCursor = 14, DiNormal = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO { public bool fIcon; public int xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr hInstance, int name);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int w, int h, int step, IntPtr brush, int flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
}
