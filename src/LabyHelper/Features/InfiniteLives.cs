using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// Host: the lobby's "Infinite lives" switch (LobbySync.infiniteLives, a SyncVar the case reads when it starts; with it
/// GameValues.Lives is -1) comes on by itself, so every case you pick starts with infinite lives. Uses the game's own
/// path (LobbySync.ServerTryToggleInfiniteLives, the one behind the lobby button), so clients see it as usual.
/// Switching it off with the game's button is respected until another case is picked (LobbySync.selectedZone changes)
/// or the lobby loads again. Off in F1 > Gameplay.
/// </summary>
internal static class InfiniteLives
{
    private static float _next;
    private static bool _wasOn, _userOff;
    private static int _lastZone = -1;

    public static void OnSceneLoaded()
    {
        _wasOn = false;
        _userOff = false;
        _lastZone = -1;
    }

    public static void Update()
    {
        if (!Settings.InfiniteLivesByDefault.Value || !GameState.InLobby || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.5f;
        if (!Il2CppMirror.NetworkServer.active) return; // only the host decides
        var sync = Il2Cpp.LobbySync.instance;
        if (sync == null) return;

        int zone = sync.NetworkselectedZone;
        if (zone != _lastZone) { _lastZone = zone; _userOff = false; } // a case was picked: on again
        bool on = sync.NetworkinfiniteLives;
        if (!on && _wasOn) _userOff = true; // switched off with the game's button
        if (!on && !_userOff)
        {
            sync.ServerTryToggleInfiniteLives();
            on = sync.NetworkinfiniteLives;
            Core.Log.Msg($"InfiniteLives: on (zone {zone}).");
        }
        _wasOn = on;
    }
}
