using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppRandomGeneration.Contracts;

namespace LabyHelper.Features;

/// <summary>
/// Spreads the cosmetics the party still needs across the random case files on the board, so each case offers a
/// different set and every needed item lives in exactly one case (any case of its kind, not only the maps that
/// natively drop it: RandomCaseRule swaps the pickup's itemID).
///
/// A case can only drop prefabs loaded in its map: the global pool, its own map pool and the seasonal pools
/// (hardcore cases: the hardcore pools). Assignment is deterministic: most constrained items first (map
/// exclusives have one possible case), each to the eligible case with the fewest items so far, ties broken by a
/// hash of the case seed. Stored by contract seed so the maze scene can look it up after leaving the lobby.
/// </summary>
internal static class Distribution
{
    private static readonly Dictionary<int, HashSet<ushort>> BySeed = new();
    private static string _inputsKey;

    public static void Invalidate() => _inputsKey = null;

    /// <summary>Contracts on the lobby board that are random case files (not custom cases).</summary>
    public static List<Contract> BoardContracts()
    {
        var list = new List<Contract>();
        try
        {
            var sync = Il2Cpp.LobbySync.instance;
            var contracts = sync?.contracts;
            if (contracts == null) return list;
            for (int i = 0; i < contracts.Count; i++)
            {
                var c = contracts[i];
                if (c != null && !c.IsCustom) list.Add(c);
            }
        }
        catch { /* not in the lobby */ }
        return list;
    }

    /// <summary>Item IDs this case may drop: candidates of its map/type, with seasonal pools for non-hardcore.</summary>
    public static HashSet<ushort> Candidates(Contract c)
    {
        var set = new HashSet<ushort>();
        if (!PoolCache.Current.Maps.TryGetValue(c.MazeType.ToString(), out var mp)) return set;
        bool hardcore = c.ContractType == ContractType.Hardcore;
        var lists = hardcore ? mp.Hardcore : mp.Normal;
        foreach (var e in lists.SelectMany(l => l)) set.Add(e.Id);
        if (!hardcore) foreach (var e in PoolCache.Current.Seasonal.Values.SelectMany(v => v.Items)) set.Add(e.Id);
        set.Remove(0xFFFF);
        return set;
    }

    /// <summary>
    /// Every item any random case of this kind can drop once RandomCaseRule may swap a pickup's itemID: all maps'
    /// pools (normal or hardcore) plus the seasonal pools for non-hardcore cases.
    /// </summary>
    public static HashSet<ushort> Obtainable(bool hardcore)
    {
        var set = new HashSet<ushort>();
        foreach (var mp in PoolCache.Current.Maps.Values)
            foreach (var e in (hardcore ? mp.Hardcore : mp.Normal).SelectMany(l => l)) set.Add(e.Id);
        if (!hardcore) foreach (var e in PoolCache.Current.Seasonal.Values.SelectMany(v => v.Items)) set.Add(e.Id);
        set.Remove(0xFFFF);
        return set;
    }

    /// <summary>New items assigned to this case, or null if the case isn't part of a known board.</summary>
    public static HashSet<ushort> AssignedTo(Contract c)
    {
        Refresh();
        return BySeed.TryGetValue(c.Seed, out var s) ? s : null;
    }

    /// <summary>What this random case may drop under the "only new" rule.</summary>
    public static HashSet<ushort> Allowed(Contract c)
    {
        var needed = Party.Needed();
        var cand = Obtainable(c.ContractType == ContractType.Hardcore);
        cand.IntersectWith(needed);
        if (!Rules.Distribute) return cand;
        var assigned = AssignedTo(c);
        return assigned != null ? assigned.Where(cand.Contains).ToHashSet() : cand;
    }

    private static void Refresh()
    {
        var board = BoardContracts();
        if (board.Count == 0) return; // keep the last assignment (we're in a case)

        string key = string.Join(";", board.Select(c => $"{c.Seed}:{c.MazeType}:{c.ContractType}")) + $"|p{Party.Version}|{PoolCache.Current.Maps.Count}";
        if (key == _inputsKey) return;
        _inputsKey = key;

        var needed = Party.Needed();
        var cands = board.ToDictionary(c => c.Seed, Candidates);
        var load = board.ToDictionary(c => c.Seed, _ => new HashSet<ushort>());

        // Prefer the cases whose own map pool has the item (keeps map exclusives on their map); otherwise any case
        // of the same kind can take it, since RandomCaseRule swaps the itemID of a pickup that loads its model.
        var hcItems = Obtainable(true);
        var normalItems = Obtainable(false);
        // Store / limited / level-reward items aren't in any case pool: never assign them.
        var items = needed.Where(id => hcItems.Contains(id) || normalItems.Contains(id))
            .Select(id =>
            {
                var native = board.Where(c => cands[c.Seed].Contains(id)).Select(c => c.Seed).ToList();
                if (native.Count > 0) return (id, cases: native);
                bool hc = hcItems.Contains(id) && !normalItems.Contains(id);
                return (id, cases: board.Where(c => (c.ContractType == ContractType.Hardcore) == hc).Select(c => c.Seed).ToList());
            })
            .Where(x => x.cases.Count > 0)
            .OrderBy(x => x.cases.Count).ThenBy(x => x.id);
        foreach (var (id, cases) in items)
        {
            int target = cases.OrderBy(s => load[s].Count).ThenBy(s => Mix(s, id)).First();
            load[target].Add(id);
        }

        BySeed.Clear();
        foreach (var (seed, set) in load) BySeed[seed] = set;
        int droppable = needed.Count(id => hcItems.Contains(id) || normalItems.Contains(id));
        Core.Log.Msg("Distribution: " + string.Join(", ", board.Select(c => $"{c.MazeType}/{c.ContractType}={load[c.Seed].Count}")) +
                     $" of {droppable} droppable ({needed.Count} needed, the rest only from store/rewards).");
    }

    private static uint Mix(int seed, ushort id) => unchecked((uint)(seed * 2654435761u) ^ (uint)(id * 40503u));
}
