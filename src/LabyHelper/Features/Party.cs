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
    private const string Key = "lh_owned", VersionKey = "lh_ver";
    private const float PublishEvery = 15f, ReadEvery = 3f;
    // A player who just joined hasn't published yet: they count as "still loading" this long before "no mod".
    private const float JoinGrace = 30f;
    private static readonly Dictionary<ulong, float> FirstSeen = new();
    private static ulong _publishedLobby;

    public record Member(ulong SteamId, string Name, bool IsMe, HashSet<ushort> Owned)
    {
        public bool HasData => Owned != null;
    }

    public static List<Member> Members { get; private set; } = new();
    private static float _nextPublish, _nextRead;
    private static string _lastPublished;
    private static int _version;

    /// <summary>Bumped whenever the party's owned sets change, so dependents can recompute.</summary>
    public static int Version => _version;

    public static void Update()
    {
        if (!Settings.PartySync.Value) return;
        float now = Time.unscaledTime;
        if (now >= _nextRead)
        {
            _nextRead = now + ReadEvery;
            // Publish right away in a new lobby (joining used to wait up to 15 s, and the others saw "no mod").
            if ((Lobby()?.m_SteamID ?? 0) != _publishedLobby) _nextPublish = 0;
            if (now >= _nextPublish) { _nextPublish = now + PublishEvery; Publish(); }
            Read();
        }
    }

    /// <summary>Items that at least one player with data doesn't own yet (just you when solo).</summary>
    public static HashSet<ushort> Needed()
    {
        var all = Catalog.AllIds().Where(id => !BrokenItems.IsBroken(id)).ToHashSet(); // never hand out items without a model
        var withData = Members.Where(m => m.HasData).ToList();
        if (withData.Count == 0)
            return all.Where(id => CosmeticAlert.IsUnlocked(id) == false).ToHashSet();
        return all.Where(id => withData.Any(m => !m.Owned.Contains(id))).ToHashSet();
    }

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
            members.Add(new Member(me, Loc.T("Vos", "You"), true, PoolBoost.OwnedIds().ToHashSet()));
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
                }
            }
            catch (Exception e) { Core.Log.Warning($"Party: read failed: {e.Message}"); }
        }

        if (!SameAs(members))
        {
            Members = members;
            _version++;
            Core.Log.Msg("Party: " + string.Join(", ", members.Select(m =>
                $"{m.Name} {(m.HasData ? m.Owned.Count + " owned" : "no data")}")) + $" -> {Needed().Count} needed.");
            Distribution.Invalidate();
            CaseBoard.RefreshAll();
        }
    }

    private static bool SameAs(List<Member> other)
        => other.Count == Members.Count && other.Zip(Members).All(p =>
               p.First.SteamId == p.Second.SteamId && p.First.HasData == p.Second.HasData &&
               (!p.First.HasData || p.First.Owned.SetEquals(p.Second.Owned)));

    // Bitset by item ID, base64. 600 items -> ~100 chars, far under Steam's 8 KB member data limit.
    private static string Encode(IEnumerable<ushort> ids)
    {
        var list = ids.ToList();
        if (list.Count == 0) return "1:";
        var bytes = new byte[list.Max() / 8 + 1];
        foreach (var id in list) bytes[id / 8] |= (byte)(1 << (id % 8));
        return "1:" + Convert.ToBase64String(bytes);
    }

    private static HashSet<ushort> Decode(string value)
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
