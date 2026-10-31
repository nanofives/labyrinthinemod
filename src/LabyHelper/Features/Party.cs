using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppSteamworks;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Shares which cosmetics each player owns, so the host can drop only items someone in the party is missing.
///
/// The game only sends a player's *equipped* items to the host (CmdSetCustomization), never the unlocked list,
/// so every player needs LabyHelper. Each one publishes its unlocked set as a bitset in its Steam lobby member
/// data (key "lh_owned"); everyone reads everyone. The lobby comes from ExtendedSteamTransport.LobbyID.
/// Players without the mod show up as "sin datos" and are ignored. Solo / EOS lobbies: only you count.
/// </summary>
internal static class Party
{
    private const string Key = "lh_owned", VersionKey = "lh_ver", PickedKey = "lh_picked";

    /// <summary>
    /// What each other modded player picked up in their current case ("seed:id,id" in lobby member data "lh_picked").
    /// Cosmetics are spawned and picked up locally on every peer, so this is the only way to know who still has to
    /// grab theirs.
    /// </summary>
    public static readonly Dictionary<ulong, (int seed, HashSet<ushort> ids)> PickedBy = new();
    private const float PublishEvery = 15f, ReadEvery = 3f;
    // A player who just joined hasn't published yet: they count as "still loading" this long before "no mod".
    private const float JoinGrace = 30f;
    private static readonly Dictionary<ulong, float> FirstSeen = new();
    private static ulong _publishedLobby;

    /// <summary>
    /// <paramref name="Remembered"/>: in the game but not in the Steam lobby (joined through crossplay / EOS, or Steam
    /// dropped them), so <paramref name="Owned"/> is their last saved list (KnownPlayers) and what they pick up in this
    /// case is unknown.
    /// </summary>
    public record Member(ulong SteamId, string Name, bool IsMe, HashSet<ushort> Owned, bool Remembered = false)
    {
        public bool HasData => Owned != null;
        /// <summary>We hear about their pickups (they're in the Steam lobby with the mod).</summary>
        public bool Live => HasData && !Remembered;
    }

    private static readonly HashSet<string> ReportedOutside = new();

    /// <summary>
    /// Players in the match (PlayerNetworkSync nameplates) who aren't Steam lobby members: everything the mod shares goes
    /// through Steam lobby member data, which they can't read or write. Matched by name with KnownPlayers.
    /// </summary>
    private static void AddOutsideLobby(List<Member> members)
    {
        List<Players.Other> inGame;
        try { inGame = Players.Others(); }
        catch { return; }
        foreach (var o in inGame)
        {
            string name = o.Name?.Trim();
            if (string.IsNullOrEmpty(name) || members.Any(m => string.Equals(m.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase))) continue;
            var known = KnownPlayers.All.FirstOrDefault(k => string.Equals(k.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));
            var owned = known?.OwnedSet();
            members.Add(new Member(known?.Id ?? 0, name, false, owned, Remembered: true));
            if (ReportedOutside.Add(name))
                Core.Log.Msg($"Party: {name} is in the match but not in the Steam lobby (crossplay?) -> " +
                             (owned != null ? $"using their saved list ({owned.Count} owned)." : "no saved list, no data."));
        }
    }

    public static List<Member> Members { get; private set; } = new();
    private static float _nextPublish, _nextRead, _nextPicks;
    private static string _lastPublished;
    private static int _version;

    /// <summary>Bumped whenever the party's owned sets change, so dependents can recompute.</summary>
    public static int Version => _version;

    public static void Update()
    {
        if (!Settings.PartySync.Value) return;
        float now = Time.unscaledTime;
        if (now >= _nextPicks)
        {
            _nextPicks = now + 1f; // the host's picks must be out before a case starts
            Distribution.PublishHostPicks();
        }
        if (now >= _nextRead)
        {
            _nextRead = now + ReadEvery;
            // Publish right away in a new lobby (joining used to wait up to 15 s, and the others saw "no mod").
            if ((Lobby()?.m_SteamID ?? 0) != _publishedLobby) _nextPublish = 0;
            if (now >= _nextPublish) { _nextPublish = now + PublishEvery; Publish(); }
            Read();
        }
    }

    /// <summary>
    /// Items the random cases aim at. By default: what at least one player in the lobby with data doesn't own yet
    /// (just you when solo). F1 > Jugadores (KnownPlayers) changes who counts: remembered players marked "le falta"
    /// count even when absent, "lo tiene" players must already own the item, "no cuenta" ones are skipped, and the
    /// item may have to be missing for all of the group instead of any.
    /// </summary>
    public static HashSet<ushort> Needed()
    {
        var all = Catalog.AllIds().Where(id => !BrokenItems.IsBroken(id)).ToHashSet(); // never hand out items without a model
        var withData = Members.Where(m => m.HasData).ToList();
        if (withData.Count == 0 && !KnownPlayers.Custom)
            return all.Where(id => CosmeticAlert.IsUnlocked(id) == false).ToHashSet();

        var (missing, owns) = Groups(withData);
        if (missing.Count == 0) // nobody left in the "needs it" group: the lobby as always
            return all.Where(id => withData.Any(m => !m.Owned.Contains(id))).ToHashSet();
        return all.Where(id => (KnownPlayers.MissingAll ? missing.All(s => !s.Contains(id)) : missing.Any(s => !s.Contains(id)))
                               && owns.All(s => s.Contains(id))).ToHashSet();
    }

    /// <summary>True when the player selection leaves nobody to aim at, so Needed falls back to the lobby.</summary>
    public static bool SelectionEmpty() => Groups(Members.Where(m => m.HasData).ToList()).missing.Count == 0;

    /// <summary>
    /// Owned sets of the "needs it" group (lobby members on Auto or Missing, plus absent remembered players on
    /// Missing) and of the "must own it" group (Owns, present or not).
    /// </summary>
    private static (List<HashSet<ushort>> missing, List<HashSet<ushort>> owns) Groups(List<Member> withData)
    {
        var missing = new List<HashSet<ushort>>();
        var owns = new List<HashSet<ushort>>();
        var present = new HashSet<ulong>();
        foreach (var m in withData)
        {
            present.Add(m.SteamId);
            switch (KnownPlayers.ModeOf(m.SteamId))
            {
                case KnownPlayers.Mode.Auto:
                case KnownPlayers.Mode.Missing: missing.Add(m.Owned); break;
                case KnownPlayers.Mode.Owns: owns.Add(m.Owned); break;
            }
        }
        foreach (var p in KnownPlayers.All)
        {
            if (present.Contains(p.Id)) continue;
            var set = p.OwnedSet();
            if (set == null) continue;
            if (p.Mode == KnownPlayers.Mode.Missing) missing.Add(set);
            else if (p.Mode == KnownPlayers.Mode.Owns) owns.Add(set);
        }
        return (missing, owns);
    }

    /// <summary>F1 > Jugadores changed a mode: recompute the board, picks and post-its.</summary>
    public static void SelectionChanged()
    {
        _version++;
        Distribution.Invalidate();
        CaseBoard.RefreshAll();
        Core.Log.Msg($"Party: player selection changed -> {Needed().Count} needed.");
    }

    /// <summary>Whether this member is currently in the lobby (Members has them with data).</summary>
    public static bool InLobby(ulong id) => Members.Any(m => m.SteamId == id);

    private static CSteamID? Lobby()
    {
        try
        {
            var t = UnityEngine.Object.FindObjectOfType<Il2Cpp.ExtendedSteamTransport>();
            if (t == null) return null;
            var id = t.LobbyID;
            return id.m_SteamID == 0 ? null : id;
        }
        catch { return null; }
    }

    /// <summary>
    /// For settings that change local map generation and therefore must match on every peer. Only the lobby owner
    /// decides: it checks that everyone runs the mod and publishes its value with "on=1" or "on=0;why=..."; the
    /// others just follow that. (Each peer used to decide on its own, so when the host saw a newcomer without data
    /// and the newcomer saw everyone, they generated different mazes.) <paramref name="value"/> is the owner's
    /// published value (ours when solo). <paramref name="pending"/>: undecided only because someone joined less than
    /// JoinGrace seconds ago and hasn't published yet; callers keep their current state.
    /// </summary>
    public static bool Agreed(string key, string mine, out string value, out string why, out bool pending)
    {
        value = null;
        why = null;
        pending = false;
        var lobby = Lobby();
        if (lobby == null) { value = mine; return true; } // solo / not a Steam lobby
        try
        {
            ulong me = SteamUser.GetSteamID().m_SteamID;
            var owner = SteamMatchmaking.GetLobbyOwner(lobby.Value);
            int n = SteamMatchmaking.GetNumLobbyMembers(lobby.Value);
            var present = new HashSet<ulong>();
            for (int i = 0; i < n; i++) present.Add(SteamMatchmaking.GetLobbyMemberByIndex(lobby.Value, i).m_SteamID);
            foreach (var gone in FirstSeen.Keys.Where(k => !present.Contains(k)).ToList()) FirstSeen.Remove(gone); // rejoin = new grace
            foreach (var id in present) if (!FirstSeen.ContainsKey(id)) FirstSeen[id] = Time.unscaledTime;

            if (owner.m_SteamID != me)
            {
                string published = SteamMatchmaking.GetLobbyMemberData(lobby.Value, owner, key);
                if (string.IsNullOrEmpty(published))
                {
                    pending = Time.unscaledTime - FirstSeen[me] < JoinGrace;
                    why = pending ? "esperando al host" : "el host no tiene LabyHelper o no publicó su configuración";
                    return false;
                }
                var d = published.Split(';').Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
                                 .GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.First()[1]);
                if (d.TryGetValue("on", out var on) && on == "0")
                {
                    why = d.TryGetValue("why", out var w) && w.Length > 0 ? w : "el host las apagó";
                    return false;
                }
                value = published;
                return true;
            }

            foreach (var id in present)
            {
                if (id == me) continue;
                var cid = new CSteamID(id);
                // Any of our keys proves the mod: version and rules go out at once, the owned list once the save loads.
                if (!string.IsNullOrEmpty(SteamMatchmaking.GetLobbyMemberData(lobby.Value, cid, VersionKey)) ||
                    !string.IsNullOrEmpty(SteamMatchmaking.GetLobbyMemberData(lobby.Value, cid, Rules.Key)) ||
                    !string.IsNullOrEmpty(SteamMatchmaking.GetLobbyMemberData(lobby.Value, cid, Key))) continue;
                string name = Clean(SteamFriends.GetFriendPersonaName(cid));
                pending = Time.unscaledTime - FirstSeen[id] < JoinGrace;
                why = pending ? $"esperando a {name}" : $"{name} no tiene LabyHelper";
                if (!pending) SteamMatchmaking.SetLobbyMemberData(lobby.Value, key, $"{mine};on=0;why={why}");
                return false;
            }
            // Same rules but another mod version still picks differently (each peer spawns its own cosmetic, with its
            // own code): everyone must run the host's version, or the rules go off for all.
            foreach (var id in present)
            {
                if (id == me) continue;
                var cid = new CSteamID(id);
                string theirs = SteamMatchmaking.GetLobbyMemberData(lobby.Value, cid, VersionKey);
                if (theirs == BuildInfo.Version) continue;
                string name = Clean(SteamFriends.GetFriendPersonaName(cid));
                why = $"{name} tiene LabyHelper {(string.IsNullOrEmpty(theirs) ? "viejo" : Clean(theirs))} y vos {BuildInfo.Version}";
                SteamMatchmaking.SetLobbyMemberData(lobby.Value, key, $"{mine};on=0;why={why}");
                return false;
            }
            value = mine;
            SteamMatchmaking.SetLobbyMemberData(lobby.Value, key, $"{mine};on=1");
            return true;
        }
        catch (Exception e)
        {
            why = e.Message;
            return false;
        }
    }

    private static string Clean(string s) => (s ?? "?").Replace(";", ",").Replace("=", "-");

    private static void Publish()
    {
        try
        {
            var lobby = Lobby();
            if (lobby == null) return;
            if (lobby.Value.m_SteamID != _publishedLobby)
            {
                _publishedLobby = lobby.Value.m_SteamID;
                _lastPublished = null; // new lobby: its member data starts empty
            }
            // Presence marker, so the host knows we run the mod before our owned list is ready. The rules key is
            // written only by the lobby owner (Agreed), with its on/off decision.
            SteamMatchmaking.SetLobbyMemberData(lobby.Value, VersionKey, BuildInfo.Version);
            SteamMatchmaking.SetLobbyMemberData(lobby.Value, DlcKey, HaveEgyptDlc() ? "1" : "0");
            var owned = PoolBoost.OwnedIds().ToList();
            if (owned.Count == 0) return; // collection/save not loaded yet: don't tell the lobby we own nothing
            string value = Encode(owned);
            if (value == _lastPublished) return;
            SteamMatchmaking.SetLobbyMemberData(lobby.Value, Key, value);
            _lastPublished = value;
            Core.Log.Msg($"Party: published {owned.Count} owned items to the lobby.");
        }
        catch (Exception e) { Core.Log.Warning($"Party: publish failed: {e.Message}"); }
    }

    private static void Read()
    {
        var members = new List<Member>();
        ulong me = 0;
        try { me = SteamUser.GetSteamID().m_SteamID; } catch { /* Steam not ready */ }

        var lobby = Lobby();
        if (lobby == null)
        {
            var mine = PoolBoost.OwnedIds().ToHashSet();
            members.Add(new Member(me, Loc.T("Vos", "You"), true, mine));
            string myName = null;
            try { myName = SteamFriends.GetPersonaName(); } catch { /* Steam not ready */ }
            KnownPlayers.Remember(me, myName, mine);
        }
        else
        {
            try
            {
                int n = SteamMatchmaking.GetNumLobbyMembers(lobby.Value);
                for (int i = 0; i < n; i++)
                {
                    var id = SteamMatchmaking.GetLobbyMemberByIndex(lobby.Value, i);
                    bool isMe = id.m_SteamID == me;
                    string name = SteamFriends.GetFriendPersonaName(id);
                    HashSet<ushort> owned = isMe
                        ? PoolBoost.OwnedIds().ToHashSet()
                        : Decode(SteamMatchmaking.GetLobbyMemberData(lobby.Value, id, Key));
                    members.Add(new Member(id.m_SteamID, name, isMe, owned));
                    KnownPlayers.Remember(id.m_SteamID, name, owned);
                    if (!isMe) ReadPicked(id, SteamMatchmaking.GetLobbyMemberData(lobby.Value, id, PickedKey));
                }
            }
            catch (Exception e) { Core.Log.Warning($"Party: read failed: {e.Message}"); }
        }

        AddOutsideLobby(members); // also with no Steam lobby at all (crossplay-only match)

        if (!SameAs(members))
        {
            Members = members;
            _version++;
            Core.Log.Msg("Party: " + string.Join(", ", members.Select(m =>
                $"{m.Name} {(m.HasData ? m.Owned.Count + (m.Remembered ? " owned (saved)" : " owned") : "no data")}")) + $" -> {Needed().Count} needed.");
            Distribution.Invalidate();
            CaseBoard.RefreshAll();
        }
    }

    private static void ReadPicked(CSteamID id, string value)
    {
        if (string.IsNullOrEmpty(value)) { PickedBy.Remove(id.m_SteamID); return; }
        var parts = value.Split(':', 2);
        if (!int.TryParse(parts[0], out int seed)) return;
        var ids = new HashSet<ushort>();
        if (parts.Length > 1)
            foreach (var s in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (ushort.TryParse(s, out var v)) ids.Add(v);
        PickedBy[id.m_SteamID] = (seed, ids);
    }

    private const string DlcKey = "lh_dlc";

    /// <summary>Whether we own the Egyptian DLC (the game's own check), false on builds without it.</summary>
    public static bool HaveEgyptDlc()
    {
        try { return HaveEgyptDlcCore(); }
        catch { return false; }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool HaveEgyptDlcCore() => Il2CppValkoGames.Labyrinthine.DLCManager.HasEgyptianDLC;

    /// <summary>Every lobby member reported owning the Egyptian DLC (members without the mod can't, so they count as no).</summary>
    public static bool EveryoneHasEgyptDlc()
    {
        if (!HaveEgyptDlc()) return false;
        try
        {
            var lobby = Lobby();
            if (lobby == null) return true;
            ulong me = SteamUser.GetSteamID().m_SteamID;
            int n = SteamMatchmaking.GetNumLobbyMembers(lobby.Value);
            for (int i = 0; i < n; i++)
            {
                var id = SteamMatchmaking.GetLobbyMemberByIndex(lobby.Value, i);
                if (id.m_SteamID != me && SteamMatchmaking.GetLobbyMemberData(lobby.Value, id, DlcKey) != "1") return false;
            }
            return true;
        }
        catch { return false; }
    }

    private const string PicksKey = "lh_picks";
    private static string _lastPicks;

    /// <summary>True when we decide for the lobby: we own it, or there's no Steam lobby (solo).</summary>
    public static bool IAmHost()
    {
        try
        {
            var lobby = Lobby();
            if (lobby == null) return true;
            return SteamMatchmaking.GetLobbyOwner(lobby.Value).m_SteamID == SteamUser.GetSteamID().m_SteamID;
        }
        catch { return true; }
    }

    /// <summary>
    /// Host only: the cosmetic each case will drop, "seed:id,..." in lobby member data "lh_picks". Every peer spawns
    /// its own cosmetic, and computing the pick on each one diverged (pools learned from each player's own games,
    /// party data arriving at different times), so the host's choice is the one everybody uses.
    /// </summary>
    public static void PublishPicks(IEnumerable<KeyValuePair<int, ushort>> picks)
    {
        string value = string.Join(",", picks.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}"));
        if (value.Length == 0) value = "-";
        if (value == _lastPicks) return;
        try
        {
            var lobby = Lobby();
            if (lobby == null) return;
            SteamMatchmaking.SetLobbyMemberData(lobby.Value, PicksKey, value);
            _lastPicks = value;
            Core.Log.Msg($"Party: published picks {value}.");
        }
        catch (Exception e) { Core.Log.Warning($"Party: publish picks failed: {e.Message}"); }
    }

    /// <summary>
    /// Clients: the host's pick for this case seed. <paramref name="hostDecides"/> is false when the host publishes no
    /// picks (older version / no mod): then the caller decides locally as before.
    /// </summary>
    public static ushort? HostPick(int seed, out bool hostDecides)
    {
        hostDecides = false;
        try
        {
            var lobby = Lobby();
            if (lobby == null) return null;
            var owner = SteamMatchmaking.GetLobbyOwner(lobby.Value);
            string value = SteamMatchmaking.GetLobbyMemberData(lobby.Value, owner, PicksKey);
            if (string.IsNullOrEmpty(value)) return null;
            hostDecides = true;
            foreach (var pair in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out var s) && s == seed && ushort.TryParse(kv[1], out var id)) return id;
            }
        }
        catch { /* Steam not ready */ }
        return null;
    }

    private const string ChaseKey = "lh_chase";
    private static string _lastChase;

    /// <summary>
    /// Host only: who each chasing monster is after, "monsterNetId:playerNetId,..." in lobby member data "lh_chase"
    /// ("-" when nobody is chased, so "empty" still means "host without the mod"). The chase target lives only in the
    /// host's behaviour tree (AIController.Target); Mirror never sends it to the clients. Written only on change.
    /// </summary>
    public static void PublishChase(string value)
    {
        if (value == _lastChase) return;
        try
        {
            var lobby = Lobby();
            if (lobby == null) return;
            SteamMatchmaking.SetLobbyMemberData(lobby.Value, ChaseKey, value);
            _lastChase = value;
        }
        catch (Exception e) { Core.Log.Warning($"Party: publish chase failed: {e.Message}"); }
    }

    /// <summary>The host's chase targets (monster netId -> player netId), or null if the host doesn't publish them.</summary>
    public static Dictionary<uint, uint> ReadChase()
    {
        try
        {
            var lobby = Lobby();
            if (lobby == null) return null;
            var owner = SteamMatchmaking.GetLobbyOwner(lobby.Value);
            string value = SteamMatchmaking.GetLobbyMemberData(lobby.Value, owner, ChaseKey);
            if (string.IsNullOrEmpty(value)) return null;
            var map = new Dictionary<uint, uint>();
            foreach (var pair in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split(':');
                if (kv.Length == 2 && uint.TryParse(kv[0], out var m) && uint.TryParse(kv[1], out var p)) map[m] = p;
            }
            return map;
        }
        catch { return null; }
    }

    /// <summary>Tells the lobby what we picked up in this case (called at case start with nothing, and on pickup).</summary>
    public static void PublishPicked(int seed, IEnumerable<ushort> ids)
    {
        try
        {
            var lobby = Lobby();
            if (lobby == null) return;
            SteamMatchmaking.SetLobbyMemberData(lobby.Value, PickedKey, $"{seed}:{string.Join(",", ids)}");
            _nextRead = 0; // refresh the others soon too
        }
        catch (Exception e) { Core.Log.Warning($"Party: publish picked failed: {e.Message}"); }
    }

    private static bool SameAs(List<Member> other)
        => other.Count == Members.Count && other.Zip(Members).All(p =>
               p.First.SteamId == p.Second.SteamId && p.First.Name == p.Second.Name && p.First.HasData == p.Second.HasData &&
               p.First.Remembered == p.Second.Remembered &&
               (!p.First.HasData || p.First.Owned.SetEquals(p.Second.Owned)));

    // Bitset by item ID, base64. 600 items -> ~100 chars, far under Steam's 8 KB member data limit.
    public static string Encode(IEnumerable<ushort> ids)
    {
        var list = ids.ToList();
        if (list.Count == 0) return "1:";
        var bytes = new byte[list.Max() / 8 + 1];
        foreach (var id in list) bytes[id / 8] |= (byte)(1 << (id % 8));
        return "1:" + Convert.ToBase64String(bytes);
    }

    public static HashSet<ushort> Decode(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith("1:")) return null;
        var set = new HashSet<ushort>();
        try
        {
            var bytes = Convert.FromBase64String(value.Substring(2));
            for (int i = 0; i < bytes.Length * 8; i++)
                if ((bytes[i / 8] & (1 << (i % 8))) != 0) set.Add((ushort)i);
        }
        catch { return null; }
        return set;
    }
}
