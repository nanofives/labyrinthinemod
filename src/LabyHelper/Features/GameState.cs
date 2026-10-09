using UnityEngine.SceneManagement;

namespace LabyHelper.Features;

/// <summary>Where the player is, from the active scene name (refreshed on every scene load).</summary>
internal static class GameState
{
    public static string Scene { get; private set; } = "";

    public static bool InLobby => Scene.StartsWith("Lobby", System.StringComparison.OrdinalIgnoreCase);
    public static bool InMenu => Scene.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0 || Scene.Length == 0;

    /// <summary>Playing a level (random case, custom case or story): anything that isn't the lobby or a menu.</summary>
    public static bool InCase => !InLobby && !InMenu && Scene != "LoadingScreen";

    public static void OnSceneLoaded()
    {
        try { Scene = SceneManager.GetActiveScene().name ?? ""; }
        catch { Scene = ""; }
    }
}
