using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// The settings that change what a case generates (cosmetic choice, seasonal event, map variety).
///
/// Cosmetics and maze pieces are generated locally on every peer (RndSpawnerBase.SpawnItem is a plain
/// Object.Instantiate driven by the maze RNG; MazeGenerator runs on every client), so any change must be identical
/// on all players or each one sees a different item / different pieces. The lobby owner publishes these values in
/// Steam lobby member data ("lh_rules") and every modded player uses them; if anyone lacks the mod, Active is
/// false and the game runs unmodified for everybody. Solo: your own settings.
/// </summary>
internal static class Rules
{
    public const string Key = "lh_rules";

    public static bool Active { get; private set; }
    public static string WhyInactive { get; private set; }

    public static bool OnlyNew, Distribute, AllSeasonal, PreferNew, Variety;
    public static string Seasonal = "Off";
    public static float SeasonalChance = -1f, RareChance = 5f, SubmazeMult = 1f;

    private static float _next;
    private static string _last;

    /// <summary>Our own values, as published when we are the lobby owner.</summary>
    public static string Mine()
    {
        var ci = CultureInfo.InvariantCulture;
        var kv = new Dictionary<string, string>
        {
            ["new"] = B(Settings.RandomCasesOnlyNew.Value),
            ["dist"] = B(Settings.DistributeAcrossCases.Value),
            ["allseas"] = B(Settings.PoolAddAllSeasonal.Value),
            ["prefer"] = B(Settings.PoolPreferNew.Value),
            ["seas"] = Settings.SeasonalOverride.Value ?? "Off",
            ["seaschance"] = Settings.SeasonalChance.Value.ToString(ci),
            ["variety"] = B(Settings.MapVarietyEnabled.Value),
            ["rare"] = Settings.MapRareChance.Value.ToString(ci),
            ["submaze"] = Settings.MapSubmazeMultiplier.Value.ToString(ci),
        };
        return string.Join(";", kv.Select(p => $"{p.Key}={p.Value}"));
    }

    /// <summary>Re-reads the lobby (throttled unless forced). Call with force right before generation hooks.</summary>
    public static void Refresh(bool force = false)
    {
        if (!force && Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 2f;

        if (!Party.Agreed(Key, Mine(), out var value, out var why))
        {
            if (Active || _last != "inactive:" + why) Core.Log.Msg($"Rules: off for everyone ({why}).");
            Active = false;
            WhyInactive = why;
            _last = "inactive:" + why;
            return;
        }
        Parse(value);
        Active = true;
        WhyInactive = null;
        if (value != _last) Core.Log.Msg($"Rules: {value}");
        _last = value;
    }

    private static void Parse(string value)
    {
        var ci = CultureInfo.InvariantCulture;
        var d = value.Split(';').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
        OnlyNew = d.GetValueOrDefault("new") == "1";
        Distribute = d.GetValueOrDefault("dist") == "1";
        AllSeasonal = d.GetValueOrDefault("allseas") == "1";
        PreferNew = d.GetValueOrDefault("prefer") == "1";
        Seasonal = d.GetValueOrDefault("seas") ?? "Off";
        Variety = d.GetValueOrDefault("variety") == "1";
        SeasonalChance = F(d, "seaschance", -1f, ci);
        RareChance = F(d, "rare", 5f, ci);
        SubmazeMult = F(d, "submaze", 1f, ci);
    }

    private static float F(Dictionary<string, string> d, string k, float def, CultureInfo ci)
        => d.TryGetValue(k, out var s) && float.TryParse(s, NumberStyles.Float, ci, out var v) ? v : def;

    private static string B(bool b) => b ? "1" : "0";
}
