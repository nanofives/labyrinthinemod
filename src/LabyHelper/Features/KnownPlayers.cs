using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MelonLoader.Utils;

namespace LabyHelper.Features;

/// <summary>
/// Every player who was ever in a lobby with us and published their unlocked cosmetics (Party "lh_owned"), kept in
/// UserData/LabyHelper.players.json with the last list we saw, so the host can aim the random cases at people who
/// aren't online right now. Each one has a mode, picked in F1 > Jugadores:
///  - Auto: counts only while in the lobby (what the mod always did).
///  - Missing ("le falta"): the case gives what they lack, present or not.
///  - Owns ("lo tiene"): only items they already own (so "A has it, B doesn't" is possible).
///  - Ignore: never counts, even in the lobby.
/// Party.Needed combines them; <see cref="MissingAll"/> says whether the item must be missing for any or for all of
/// the "needs it" group.
/// </summary>
internal static class KnownPlayers
{
    public enum Mode { Auto, Missing, Owns, Ignore }

    public class Entry
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public string Owned { get; set; } // Party encoding (bitset base64)
        public long LastSeen { get; set; } // unix seconds
        public Mode Mode { get; set; }

        private string _decodedFrom;
        private HashSet<ushort> _decoded;

        public HashSet<ushort> OwnedSet()
        {
            if (_decodedFrom != Owned) { _decoded = Party.Decode(Owned); _decodedFrom = Owned; }
            return _decoded;
        }
    }

    public class Data
    {
        public List<Entry> Players { get; set; } = new();
        public bool MissingAll { get; set; }
    }

    private static readonly string FilePath = Path.Combine(MelonEnvironment.UserDataDirectory, "LabyHelper.players.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static Data _data;
    private static long _savedAt;

    private static Data D
    {
        get
        {
            if (_data != null) return _data;
            try
            {
                if (File.Exists(FilePath)) _data = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath));
            }
            catch (Exception e) { Core.Log.Warning($"KnownPlayers: couldn't read {FilePath}: {e.Message}"); }
            _data ??= new Data();
            _data.Players ??= new List<Entry>();
            return _data;
        }
    }

    public static IReadOnlyList<Entry> All => D.Players;

    public static bool MissingAll
    {
        get => D.MissingAll;
        set { if (D.MissingAll == value) return; D.MissingAll = value; Save(); Party.SelectionChanged(); }
    }

    /// <summary>True when any player has a mode other than Auto (the board then says so).</summary>
    public static bool Custom => D.Players.Any(p => p.Mode != Mode.Auto) || D.MissingAll;

    public static Mode ModeOf(ulong id) => Find(id)?.Mode ?? Mode.Auto;

    public static void SetMode(ulong id, Mode mode)
    {
        var e = Find(id);
        if (e == null || e.Mode == mode) return;
        e.Mode = mode;
        Save();
        Party.SelectionChanged();
    }

    public static void ResetModes()
    {
        foreach (var p in D.Players) p.Mode = Mode.Auto;
        D.MissingAll = false;
        Save();
        Party.SelectionChanged();
    }

    public static void Forget(ulong id)
    {
        if (D.Players.RemoveAll(p => p.Id == id) == 0) return;
        Save();
        Party.SelectionChanged();
    }

    private static Entry Find(ulong id) => D.Players.FirstOrDefault(p => p.Id == id);

    /// <summary>Called by Party for every lobby member with data (us included). Saves when something changed.</summary>
    public static void Remember(ulong id, string name, HashSet<ushort> owned)
    {
        if (id == 0 || owned == null || owned.Count == 0) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var e = Find(id);
        bool changed = false;
        if (e == null)
        {
            e = new Entry { Id = id };
            D.Players.Add(e);
            changed = true;
            Core.Log.Msg($"KnownPlayers: remembering {name} ({owned.Count} owned).");
        }
        if (!string.IsNullOrEmpty(name) && e.Name != name) { e.Name = name; changed = true; }
        var old = e.OwnedSet();
        if (old == null || !old.SetEquals(owned)) { e.Owned = Party.Encode(owned); changed = true; }
        e.LastSeen = now;
        // LastSeen alone is written at most every 10 minutes.
        if (changed || now - _savedAt > 600) Save();
    }

    private static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(D, Json));
            _savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        catch (Exception e) { Core.Log.Warning($"KnownPlayers: couldn't save: {e.Message}"); }
    }
}
