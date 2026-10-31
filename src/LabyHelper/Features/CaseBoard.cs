using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Il2CppRandomGeneration.Contracts;

namespace LabyHelper.Features;

/// <summary>
/// Case folders on the lobby board get a post-it with the cosmetic's picture (the folder text was hard to read, so we
/// don't write on it). Hovering the post-it shows a tooltip with everything: the exact item for random cases (or the
/// odds otherwise), rarity, how many new items the case holds, and which party members have / lack it.
/// </summary>
internal static class CaseBoard
{
    private const string Marker = "\n<size=80%><color=#000000>";
    private static readonly HashSet<IntPtr> Seen = new();
    private static readonly List<ContractUI> Live = new();
    private static readonly Dictionary<IntPtr, (Contract c, Predictor.Odds o, ushort? item)> Info = new();

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        var target = AccessTools.Method(typeof(ContractUI), "UpdateUI");
        if (target == null)
        {
            Core.Log.Warning("CaseBoard: ContractUI.UpdateUI not found.");
            return;
        }
        harmony.Patch(target, postfix: new HarmonyMethod(typeof(CaseBoard), nameof(UpdateUIPostfix)));
        PoolCache.Changed += RefreshAll;
        Core.Log.Msg("CaseBoard: hooked ContractUI.UpdateUI.");
    }

    private static void UpdateUIPostfix(ContractUI __instance)
    {
        if (__instance == null) return;
        if (Seen.Add(__instance.Pointer)) Live.Add(__instance);
        Decorate(__instance);
    }

    private static float _nextCheck;
    private static string _stateKey;

    /// <summary>
    /// Lobby: re-decorates the board when the rules switch on/off or the party changes (someone joins without the
    /// mod, owned lists arrive). Before this the post-its kept showing a "guaranteed" item after the rules had turned
    /// off, and the case dropped something else.
    /// </summary>
    public static void Update()
    {
        if (!GameState.InLobby || Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 1f;
        try
        {
            Distribution.Refresh(); // freezes each case's item even with the board info turned off
            string key = $"{Rules.Active}|{Rules.OnlyNew}|{Rules.Distribute}|{Party.Version}";
            if (key == _stateKey) return;
            _stateKey = key;
            RefreshAll();
        }
        catch (Exception e) { Core.Log.Warning($"CaseBoard: {e.Message}"); }
    }

    public static void RefreshAll()
    {
        Live.RemoveAll(u => u == null || u.WasCollected);
        foreach (var ui in Live) Decorate(ui);
    }

    private static void Decorate(ContractUI ui)
    {
        try
        {
            var text = ui.infoText;
            var contract = ui.Contract;
            if (text == null) return;

            // Remove text added by older versions; the info now lives in the hover tooltip.
            string current = text.text ?? "";
            int cut = current.IndexOf(Marker, StringComparison.Ordinal);
            if (cut >= 0) text.text = current.Substring(0, cut);

            if (!Settings.CaseBoardPrediction.Value || contract == null)
            {
                PostIt.Attach(text, null, null);
                Info.Remove(ui.Pointer);
                return;
            }
            var odds = Predictor.Compute(contract, Rules.Active);
            var item = PostItItem(odds);
            Info[ui.Pointer] = (contract, odds, item);

            Renderer photo = null;
            var photos = ui.photoMeshFilters;
            if (photos != null && photos.Length > 0 && photos[0] != null) photo = photos[0].GetComponent<Renderer>();
            PostIt.Attach(text, item, photo);
            LogLayoutOnce(text, photo);
        }
        catch (Exception e)
        {
            Core.Log.Warning($"CaseBoard: {e.Message}");
        }
    }

    /// <summary>
    /// The post-it only shows an item when it is the exact one the case drops (random case under the rule). Showing
    /// the likeliest exclusive otherwise read as a promise and the case dropped something else.
    /// </summary>
    private static ushort? PostItItem(Predictor.Odds o) => o.Problem == null && o.RuleApplies ? o.Target : null;

    private static bool _layoutLogged;

    private static void LogLayoutOnce(Il2CppTMPro.TMP_Text text, Renderer photo)
    {
        if (_layoutLogged) return;
        _layoutLogged = true;
        Core.Log.Msg($"CaseBoard layout: text {text.GetIl2CppType().Name} font {text.fontSize} bounds {text.textBounds} " +
                     $"lossyScale {text.transform.lossyScale}; photo shader {photo?.sharedMaterial?.shader?.name ?? "none"}");
    }

    // ------------------------------------------------------------------ hover tooltip

    private static readonly Vector3[] Corners = new Vector3[8];

    /// <summary>Drawn from Core.OnGUI: tooltip for the post-it under the mouse (lobby only).</summary>
    public static void DrawTooltip()
    {
        if (!GameState.InLobby || !Settings.CaseBoardPrediction.Value) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 mouse = Input.mousePosition;
        foreach (var ui in Live)
        {
            if (ui == null || ui.WasCollected || !Info.TryGetValue(ui.Pointer, out var info)) continue;
            var postIt = ui.infoText?.transform.Find("LabyHelper_PostIt");
            var r = postIt != null && postIt.gameObject.activeInHierarchy ? postIt.GetComponent<Renderer>() : null;
            if (r == null || !r.isVisible) continue;
            if (!ScreenRect(cam, r.bounds, out var rect) || !rect.Contains(mouse)) continue;
            DrawPanel(new Vector2(mouse.x, Screen.height - mouse.y), info.c, info.o, info.item);
            return;
        }
    }

    private static bool ScreenRect(Camera cam, Bounds b, out Rect rect)
    {
        rect = default;
        Vector3 c = b.center, e = b.extents;
        int i = 0;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    Corners[i++] = cam.WorldToScreenPoint(c + Vector3.Scale(e, new Vector3(x, y, z)));
        if (Corners.Any(p => p.z < 0)) return false;
        float minX = Corners.Min(p => p.x), maxX = Corners.Max(p => p.x), minY = Corners.Min(p => p.y), maxY = Corners.Max(p => p.y);
        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private static void DrawPanel(Vector2 at, Contract c, Predictor.Odds o, ushort? item)
    {
        var lines = new List<(string text, Color color, GUIStyle style)>();
        lines.Add(($"{Catalog.MapName(c.MazeType.ToString())} · {c.ContractType}", new Color(0.6f, 0.8f, 1f), Ui.Bold));

        if (o.Problem != null)
        {
            lines.Add((Loc.T("Sin datos para este mapa.", "No data for this map."), Color.gray, Ui.Label));
        }
        else if (o.RuleApplies && item is ushort t)
        {
            lines.Add((Loc.T("Cosmético nuevo garantizado:", "Guaranteed new cosmetic:"), Color.white, Ui.Label));
            lines.Add(($"{CosmeticAlert.Name(t)}  [{Rarity(t)}]", new Color(1f, 0.9f, 0.5f), Ui.Bold));
            lines.Add((Loc.T($"{o.Allowed.Count} cosméticos nuevos asignados a este caso", $"{o.Allowed.Count} new cosmetics assigned to this case"),
                       Color.gray, Ui.Small));
            AddParty(lines, t);
        }
        else
        {
            string chance = c.ContractType == ContractType.Normal && o.SpawnChance is float sc
                ? Loc.T($"Chance de cosmético: {sc * 100:0}%", $"Cosmetic chance: {sc * 100:0}%")
                : Loc.T("Cosmético garantizado", "Guaranteed cosmetic");
            if (!Rules.Active && Rules.WhyInactive != null)
                lines.Add((Loc.T($"Reglas apagadas: {Rules.WhyInactive}. Cosmético al azar.", $"Rules off: {Rules.WhyInactive}. Random cosmetic."),
                           new Color(1f, 0.55f, 0.4f), Ui.Small));
            lines.Add((chance, Color.white, Ui.Label));
            lines.Add((Loc.T($"{o.PNew * 100:0}% de que sea nuevo", $"{o.PNew * 100:0}% chance it's new"), Color.white, Ui.Label));
            if (o.SeasonalChance > 0) lines.Add(($"{o.Event}: {o.SeasonalChance * 100:0}%", Color.white, Ui.Label));
            foreach (var (id, p) in o.Exclusives.Take(3))
                lines.Add(($"{Loc.T("Exclusivo", "Exclusive")}: {CosmeticAlert.Name(id)} {p * 100:0.#}%", new Color(1f, 0.9f, 0.5f), Ui.Label));
            if (item is ushort ex) AddParty(lines, ex);
        }

        const float icon = 72f, pad = 10f, lineH = 24f;
        float w = 380f, h = pad * 2 + Math.Max(icon, lines.Count * lineH);
        float x = Mathf.Min(at.x + 18, Screen.width - w - 10), y = Mathf.Min(at.y + 18, Screen.height - h - 10);
        var panel = new Rect(x, y, w, h);
        Ui.Panel(panel);
        if (item is ushort shown)
        {
            Catalog.DrawIcon(new Rect(x + pad, y + pad, icon, icon), shown);
        }
        float tx = x + pad + icon + 10, ty = y + pad;
        foreach (var (text, color, style) in lines)
        {
            Ui.Text(new Rect(tx, ty, w - (tx - x) - pad, lineH), text, style, color);
            ty += lineH;
        }
    }

    /// <summary>Who in the party has the item (green) and who doesn't (red).</summary>
    private static void AddParty(List<(string, Color, GUIStyle)> lines, ushort id)
    {
        var members = Party.Members.Where(m => m.HasData).ToList();
        if (members.Count == 0) return;
        foreach (var m in members)
        {
            bool has = m.Owned.Contains(id);
            string saved = m.Remembered ? Loc.T(" (guardado)", " (saved)") : "";
            lines.Add((has ? Loc.T($"✔ {m.Name} lo tiene{saved}", $"✔ {m.Name} has it{saved}") : Loc.T($"✘ {m.Name} no lo tiene{saved}", $"✘ {m.Name} doesn't have it{saved}"),
                       has ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.45f, 0.4f), Ui.Label));
        }
        foreach (var m in Party.Members.Where(m => !m.HasData))
            lines.Add(($"? {m.Name} ({Loc.T("sin el mod", "no mod")})", Color.gray, Ui.Small));
    }

    private static string Rarity(ushort id)
    {
        var it = Catalog.ItemById(id);
        return it != null ? Catalog.RarityLabel(it.ItemRarity) : "?";
    }

    private static string ChanceText(string type)
        => PoolCache.Current.ChanceForItem.TryGetValue(type, out var c) ? $" ({c * 100:0}%)" : "";
}
