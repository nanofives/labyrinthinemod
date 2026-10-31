using LabyHelper.Features;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(LabyHelper.Core), "LabyHelper", LabyHelper.BuildInfo.Version, "Mariano")]
[assembly: MelonGame("Valko Game Studios", "Labyrinthine")]

namespace LabyHelper;

public class Core : MelonMod
{
    internal static MelonLogger.Instance Log;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        Settings.Init();
        AnnounceUpdate();
        UpdateInfo.Init();
        UpdaterRepair.Run();
        PoolCache.Load();
        CheatDetector.Apply(HarmonyInstance);
        Brightness.Apply(HarmonyInstance);
        CosmeticAlert.Apply(HarmonyInstance);
        Seasonal.Apply(HarmonyInstance);
        CaseBoard.Apply(HarmonyInstance);
        PoolBoost.Apply(HarmonyInstance);
        SelfRevive.Apply(HarmonyInstance);
        SprintBoost.Apply(HarmonyInstance);
        BrokenItems.Apply(HarmonyInstance);
        BoardSeals.Apply(HarmonyInstance);
        MapSizeCap.Apply(HarmonyInstance);
        DlcMaps.Apply(HarmonyInstance);
        RandomCaseRule.Apply(HarmonyInstance);
        MapVariety.Apply(HarmonyInstance);
#if EXPERIMENTS
        ForeignPieces.Apply(HarmonyInstance);
#endif
        Log.Msg("Loaded. F6 brillo on/off, F7/F8 exposicion -/+, Shift+F7/F8 gamma -/+, F9 cosmetico del caso, F2 coleccion, F1 opciones.");
    }

    public override void OnUpdate()
    {
        Safe("UiSkin.Update", () => UiSkin.Update());
        Safe("GameRestart.Update", () => GameRestart.Update());
        Safe("Brightness.HandleHotkeys", () => Brightness.HandleHotkeys());
        Safe("CosmeticAlert.Update", () => CosmeticAlert.Update());
        Safe("AllPickedBanner.Update", () => AllPickedBanner.Update());
        Safe("Pigman.Update", () => Pigman.Update());
        Safe("MonsterAlert.Update", () => MonsterAlert.Update());
        Safe("Revived.Update", () => Revived.Update());
        Safe("AutoGlowstick.Update", () => AutoGlowstick.Update());
        Safe("AutoFlashlight.Update", () => AutoFlashlight.Update());
        Safe("Objectives.Update", () => Objectives.Update());
        Safe("Minimap.Update", () => Minimap.Update());
        Safe("SelfRevive.Update", () => SelfRevive.Update());
        Safe("Party.Update", () => Party.Update());
        Safe("Distribution.Update", () => Distribution.Update());
        Safe("Rules.Refresh", () => Rules.Refresh());
        Safe("CaseBoard.Update", () => CaseBoard.Update());
#if EXPERIMENTS
        Safe("ForeignPieces.Update", () => ForeignPieces.Update());
#endif
        Safe("ModelIcons.Update", () => ModelIcons.Update());
        Safe("CollectionViewer.Update", () => CollectionViewer.Update());
        Safe("OptionsMenu.Update", () => OptionsMenu.Update());
        Safe("WinCursor.Update", () => WinCursor.Update());
        Safe("InfiniteLives.Update", () => InfiniteLives.Update());
        Safe("Poster.Update", () => Poster.Update());
        Safe("UpdateInfo.Update", () => UpdateInfo.Update());
        if (_learnAt.Count > 0 && Time.unscaledTime >= _learnAt[0])
        {
            _learnAt.RemoveAt(0);
            PoolCache.LearnFromScene();
        }
    }

    // Maze modules finish spawning a while after the scene loads; scan for cosmetic spawners a few times.
    private static readonly System.Collections.Generic.List<float> _learnAt = new();

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
#if EXPERIMENTS
        if (ForeignPieces.OnSceneLoaded(sceneName)) return; // donor scene of the experiment, loaded and dropped in the lobby
#endif
        Safe("GameState.OnSceneLoaded", () => GameState.OnSceneLoaded());
        Safe("CosmeticAlert.OnSceneLoaded", () => CosmeticAlert.OnSceneLoaded());
        Safe("AllPickedBanner.OnSceneLoaded", () => AllPickedBanner.OnSceneLoaded());
        Safe("Pigman.OnSceneLoaded", () => Pigman.OnSceneLoaded());
        Safe("MonsterAlert.OnSceneLoaded", () => MonsterAlert.OnSceneLoaded());
        Safe("Revived.OnSceneLoaded", () => Revived.OnSceneLoaded());
        Safe("AutoGlowstick.OnSceneLoaded", () => AutoGlowstick.OnSceneLoaded());
        Safe("AutoFlashlight.OnSceneLoaded", () => AutoFlashlight.OnSceneLoaded());
        Safe("Objectives.OnSceneLoaded", () => Objectives.OnSceneLoaded());
        Safe("Minimap.OnSceneLoaded", () => Minimap.OnSceneLoaded());
        Safe("Poster.OnSceneLoaded", () => Poster.OnSceneLoaded());
        Safe("InfiniteLives.OnSceneLoaded", () => InfiniteLives.OnSceneLoaded());
        Safe("OptionsMenu.OnSceneLoaded", () => OptionsMenu.OnSceneLoaded());
        Safe("CollectionViewer.OnSceneLoaded", () => CollectionViewer.OnSceneLoaded());
        _learnAt.Clear();
        foreach (var delay in new[] { 5f, 15f, 40f }) _learnAt.Add(Time.unscaledTime + delay);
    }

    public override void OnGUI()
    {
        // Every label of the mod in the game's menu font (styles with no font of their own use the skin's).
        var skinFont = GUI.skin.font;
        Ui.SymbolFont ??= skinFont;
        if (UiSkin.Font != null) GUI.skin.font = UiSkin.Font;
        try { DrawGui(); }
        finally { GUI.skin.font = skinFont; }
    }

    private static void DrawGui()
    {
        Safe("WorldMarker.Draw", () => WorldMarker.Draw());
        Safe("Minimap.Draw", () => Minimap.Draw());
        Safe("Minimap.DrawRouteMarkers", () => Minimap.DrawRouteMarkers());
        Safe("CollectionViewer.Draw", () => CollectionViewer.Draw());
        Safe("OptionsMenu.Draw", () => OptionsMenu.Draw());
        Safe("CaseBoard.DrawTooltip", () => CaseBoard.DrawTooltip());
        Safe("Toast.Draw", () => Toast.Draw());
        Safe("AllPickedBanner.Draw", () => AllPickedBanner.Draw());
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> LastError = new();

    /// <summary>
    /// Runs one feature's per-frame step so a failure stays in that feature: before this, one exception (e.g. a game
    /// type missing on an older game build) ended OnUpdate early and Party never published, so the host saw "no tiene
    /// LabyHelper". Each distinct error is logged once per feature.
    /// </summary>
    private static void Safe(string name, System.Action step)
    {
        try { step(); }
        catch (System.Exception e)
        {
            string msg = e.GetType().Name + ": " + e.Message;
            if (LastError.TryGetValue(name, out var last) && last == msg) return;
            LastError[name] = msg;
            Log.Error($"{name}: {e}");
        }
    }

    /// <summary>The updater plugin leaves "old new" in UserData\LabyHelper.updated.txt after swapping the DLL.</summary>
    private static void AnnounceUpdate()
    {
        try
        {
            string path = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "LabyHelper.updated.txt");
            if (!System.IO.File.Exists(path)) return;
            string from = System.IO.File.ReadAllText(path).Split(' ')[0];
            System.IO.File.Delete(path);
            Toast.Show(Loc.T($"LabyHelper se actualizó solo: {from} -> {BuildInfo.Version}",
                             $"LabyHelper updated itself: {from} -> {BuildInfo.Version}"), 15f);
            Log.Msg($"Updated from {from} to {BuildInfo.Version}.");
        }
        catch (System.Exception e) { Log.Warning($"Update notice: {e.Message}"); }
    }
}
