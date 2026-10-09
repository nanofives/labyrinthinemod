using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppRandomGeneration.Contracts;
using Il2CppValkoGames.Labyrinthine.Misc.TimeInfo;
using Entry = LabyHelper.Features.PoolCache.Entry;

namespace LabyHelper.Features;

/// <summary>
/// Odds of what a contract's cosmetic will be, computed exactly from the pools.
///
/// Why not the exact item: RndCustomizationSpawner.Spawn seeds its picks from MazeGenerator.seed/secondSeed, which
/// start as Contract.Seed/SecondSeed but get +0x4A3 / +0x18C1 for every failed MazeGenerator.GenerateNewMaze
/// attempt (CreateMaze retries up to 10 times). The number of failures depends on full maze generation with the
/// map's structures, which doesn't exist in the lobby. So from the board we can only give probabilities.
///
/// Distribution: Spawn retries up to 50 times while the pick is excluded, so excluded items are effectively
/// removed and the rest keep their relative weights. Not Hardcore + live event: with probability cfg.chance the
/// candidates are the event's prefabs instead.
/// </summary>
internal static class Predictor
{
    public class Odds
    {
        public string Problem;
        public double SeasonalChance;
        public string Event;
        public double PNew;                                   // chance the cosmetic is one you don't own
        public List<(ushort id, double p)> Exclusives = new(); // map-exclusive items with their chance
        public Dictionary<ushort, double> Items = new();      // full distribution, given that a cosmetic spawns
        public HashSet<ushort> MapIds = new();
        public HashSet<ushort> SeasonalIds = new();
        public float? SpawnChance;                             // chance a cosmetic spawns at all (null = unknown)
        public bool RuleApplies;                               // random case under the "only new" rule
        public HashSet<ushort> Allowed = new();                // items this case may drop under the rule
        public ushort? Target;                                 // exact item under the rule
    }

    public static Odds Compute(Contract c, bool asHost)
    {
        var o = new Odds();
        try
        {
            bool hardcore = c.ContractType == ContractType.Hardcore;
            if (!PoolCache.Current.Maps.TryGetValue(c.MazeType.ToString(), out var mp))
            {
                o.Problem = "mapa sin aprender";
                return o;
            }
            var lists = hardcore ? mp.Hardcore : mp.Normal;
            if (lists.Count == 0)
            {
                o.Problem = "mapa sin aprender";
                return o;
            }

            var excluded = new HashSet<ushort>();
            var ex = c.m_ExcludedCustomizationItemIDs;
            if (ex != null) for (int i = 0; i < ex.Count; i++) excluded.Add(ex[i]);

            var baseList = lists[0];
            o.SpawnChance = c.ContractType == ContractType.Normal ? null : 1f;
            o.MapIds = lists[0].Where(e => e.FromMap && PoolCache.IsMapExclusive(e.Id)).Select(e => e.Id).ToHashSet();
            o.SeasonalIds = PoolCache.Current.Seasonal.Values.SelectMany(v => v.Items).Select(e => e.Id).ToHashSet();

            // Random case under the rule: always spawns, weighted pick among the allowed (assigned) new items.
            if (asHost && RandomCaseRule.Applies(c))
            {
                o.Allowed = Features.Distribution.Allowed(c);
                if (o.Allowed.Count > 0)
                {
                    // The item is picked by RandomCaseRule with a contract-seeded RNG (independent of maze retries),
                    // so for these cases we know it exactly.
                    o.RuleApplies = true;
                    o.SpawnChance = 1f;
                    o.Target = RandomCaseRule.PickTarget(c, o.Allowed);
                    o.Items = new Dictionary<ushort, double> { [o.Target.Value] = 1.0 };
                    o.PNew = 1;
                    if (o.MapIds.Contains(o.Target.Value)) o.Exclusives = new() { (o.Target.Value, 1.0) };
                    return o;
                }
            }

            if (asHost)
            {
                if (!RandomCaseRule.Applies(c) && Rules.PreferNew) excluded.UnionWith(PoolBoost.PartyOwnedByAll());
                if (Rules.AllSeasonal && !hardcore)
                    baseList = baseList.Concat(PoolCache.Current.Seasonal.Values.SelectMany(v => v.Items)).ToList();
            }
            var normal = Distribution(baseList, excluded);
            Dictionary<ushort, double> seasonal = null;
            if (!hardcore)
            {
                var (evt, chance) = EffectiveEvent(c, asHost);
                if (evt != null && PoolCache.Current.Seasonal.TryGetValue(evt, out var sp) && sp.Items.Count > 0)
                {
                    o.Event = evt;
                    o.SeasonalChance = Math.Clamp(chance ?? sp.Chance, 0f, 1f);
                    seasonal = Distribution(sp.Items, excluded);
                }
            }

            var total = new Dictionary<ushort, double>();
            void Add(Dictionary<ushort, double> d, double w)
            {
                foreach (var (id, p) in d) total[id] = total.GetValueOrDefault(id) + p * w;
            }
            Add(normal, 1 - o.SeasonalChance);
            if (seasonal != null) Add(seasonal, o.SeasonalChance);

            o.Items = total;
            if (o.SpawnChance == null && PoolCache.Current.ChanceForItem.TryGetValue("Normal", out var sc)) o.SpawnChance = sc;
            o.PNew = total.Where(kv => CosmeticAlert.IsUnlocked(kv.Key) == false).Sum(kv => kv.Value);
            o.Exclusives = total.Where(kv => o.MapIds.Contains(kv.Key))
                                .OrderByDescending(kv => kv.Value)
                                .Select(kv => (kv.Key, kv.Value)).ToList();
        }
        catch (Exception e)
        {
            o.Problem = "error";
            Core.Log.Warning($"Predictor: {e.Message}");
        }
        return o;
    }

    /// <summary>Probability of each item after dropping excluded ones (Spawn re-rolls those).</summary>
    private static Dictionary<ushort, double> Distribution(List<Entry> list, HashSet<ushort> excluded)
    {
        var allowed = list.Where(e => !(e.OnRoot && excluded.Contains(e.Id))).ToList();
        if (allowed.Count == 0) allowed = list;
        double sum = allowed.Sum(e => e.Weight);
        var d = new Dictionary<ushort, double>();
        if (sum <= 0) return d;
        foreach (var e in allowed) d[e.Id] = d.GetValueOrDefault(e.Id) + e.Weight / sum;
        return d;
    }

    /// <summary>The event Spawn will see: our override when hosting, otherwise whatever is live on Steam's date.</summary>
    public static (string evt, float? chance) EffectiveEvent(Contract c, bool asHost)
    {
        if (asHost)
        {
            var forced = Seasonal.ChooseEvent(c.Seed, PoolCache.Current.Seasonal.Keys.ToList());
            if (forced != null)
                return (forced, Rules.SeasonalChance >= 0 ? Math.Min(Rules.SeasonalChance, 1f) : null);
        }
        try
        {
            if (SeasonalEventsTimeHandler.TryGetActiveSeasonalEvent(out var ev, out _, out _))
                return (ev.EventType.ToString(), null);
        }
        catch { /* not available yet */ }
        return (null, null);
    }
}
