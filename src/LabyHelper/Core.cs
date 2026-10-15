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
        RandomCaseRule.Apply(HarmonyInstance);
        MapVariety.Apply(HarmonyInstance);
#if EXPERIMENTS
        ForeignPieces.Apply(HarmonyInstance);
#endif
        Log.Msg("Loaded. F6 brillo on/off, F7/F8 exposicion -/+, Shift+F7/F8 gamma -/+, F9 cosmetico del caso, F5 levantarse (caido), F3 pool en vivo, F2 coleccion, F1 opciones.");
    }

    public override void OnUpdate()
    {
        Brightness.HandleHotkeys();
        CosmeticAlert.Update();
        Pigman.Update();
        MonsterAlert.Update();
        AutoGlowstick.Update();
        Minimap.Update();
        SelfRevive.Update();
        Party.Update();
        Rules.Refresh();
        CaseBoard.Update();
#if EXPERIMENTS
        ForeignPieces.Update();
#endif
        Collectibles.Update();
        ModelIcons.Update();
        PoolViewer.Update();
        CollectionViewer.Update();
        OptionsMenu.Update();
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
        GameState.OnSceneLoaded();
        CosmeticAlert.OnSceneLoaded();
        Pigman.OnSceneLoaded();
        MonsterAlert.OnSceneLoaded();
        AutoGlowstick.OnSceneLoaded();
        Minimap.OnSceneLoaded();
        Collectibles.OnSceneLoaded();
        _learnAt.Clear();
        foreach (var delay in new[] { 5f, 15f, 40f }) _learnAt.Add(Time.unscaledTime + delay);
    }

    public override void OnGUI()
    {
        WorldMarker.Draw();
        Minimap.Draw();
        PoolViewer.Draw();
        CollectionViewer.Draw();
        OptionsMenu.Draw();
        CaseBoard.DrawTooltip();
        Toast.Draw();
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
