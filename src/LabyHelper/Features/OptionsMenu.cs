using System;
using MelonLoader;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// "Opciones LabyHelper": every mod setting in one panel. A button shows up while the game's own settings screen
/// is open (detected through LabyrinthineSettingsManager's controls being visible); F1 opens it anywhere.
/// The difficulty gate can only be switched off here (no hotkey), by design.
/// </summary>
internal static class OptionsMenu
{
    private const float RowH = 30f;
    private static bool _open, _settingsOpen, _dirty;
    private static float _saveAt;
    private static float _scroll, _nextCheck;
    private static CursorLockMode _prevLock;
    private static bool _prevVisible;

    private static readonly string[] SeasonModes =
        { "Off", "Random", "Halloween", "Christmas", "Easter", "Valentines", "StPatrick", "Summer" };
    private static readonly string[] MarkerModes = { "Off", "Hotkey", "Always" };
    // tileSize 10 maps with rare pieces (re/EXPERIMENT-foreign-pieces.md)
    private static readonly string[] DonorMazes =
        { "Maze_F", "Maze_Backrooms", "Maze_E", "Maze_J", "Maze_C", "Maze_I", "Maze_N", "Maze_A", "Maze_B", "Maze_L", "Maze_P", "Maze_H", "Maze_G" };

    public static void Update()
    {
        if (GameState.InCase) { SetOpen(false); _settingsOpen = false; return; } // panels are lobby/menu only
        if (Input.GetKeyDown(KeyCode.F1)) SetOpen(!_open);
        if (Time.unscaledTime >= _nextCheck)
        {
            _nextCheck = Time.unscaledTime + 0.5f;
            _settingsOpen = GameSettingsVisible();
        }
        // Sliders change continuously while dragged: save once they settle (and always on close).
        if (_dirty && Time.unscaledTime >= _saveAt) { _dirty = false; Settings.Save(); }
        if (_dirty == false) _saveAt = Time.unscaledTime + 1f;
        if (_open)
        {
            // Gameplay relocks the cursor every frame; keep it free while the panel is up.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
        }
    }

    private static void SetOpen(bool open)
    {
        if (open == _open) return;
        _open = open;
        if (open)
        {
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
        }
        else
        {
            Settings.Save();
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
            CaseBoard.RefreshAll();
        }
    }

    private static bool GameSettingsVisible()
    {
        try
        {
            var m = Il2Cpp.LabyrinthineSettingsManager.instance;
            if (m == null) return false;
            return Visible(m.brightness) || Visible(m.masterVolume) || Visible(m.mouseSensitivity)
                   || Visible(m.languageDropdown) || Visible(m.resolutionDropdown);
        }
        catch { return false; }
    }

    private static bool Visible(Component c) => c != null && c.gameObject.activeInHierarchy;

    public static void Draw()
    {
        if (_settingsOpen && !_open)
        {
            if (Ui.Tab(new Rect(Screen.width - 230, Screen.height - 70, 210, 44), Loc.T("Opciones LabyHelper (F1)", "LabyHelper options (F1)"), false)) SetOpen(true);
        }
        if (!_open) return;

        var area = new Rect(Screen.width / 2f - 330, 50, 660, Screen.height - 100);
        Ui.Panel(area);
        float x = area.x + 16, y = area.y + 10, w = area.width - 32;
        Ui.Text(new Rect(x, y, w - 50, 28), Loc.T($"Opciones LabyHelper {BuildInfo.Version}", $"LabyHelper options {BuildInfo.Version}"), Ui.Header, Color.white);
        if (GUI.Button(new Rect(area.xMax - 46, y, 30, 26), "X")) { SetOpen(false); return; }
        y += 34;

        var view = new Rect(x, y, w, area.yMax - y - 10);
        float contentH = 46 * RowH + 11 * 34;
        var s = GUI.BeginScrollView(view, new Vector2(0, _scroll), new Rect(0, 0, w - 20, contentH));
        _scroll = s.y;
        float cy = 0, cw = w - 24;

        Section(ref cy, cw, Loc.T("General", "General"));
        Cycle(ref cy, cw, Loc.T("Idioma", "Language"), Settings.Language, Loc.Languages);

        Section(ref cy, cw, Loc.T("Dificultad (host)", "Difficulty (host)"));
        Toggle(ref cy, cw, Loc.T("Casos random: siempre cosmético y solo nuevos", "Random cases: always a cosmetic, only new ones"), Settings.RandomCasesOnlyNew);
        Toggle(ref cy, cw, Loc.T("Repartir los nuevos entre las 4 carpetas", "Spread new items across the 4 case files"), Settings.DistributeAcrossCases);
        Toggle(ref cy, cw, Loc.T("Sincronizar con los jugadores (necesitan el mod)", "Sync with other players (they need the mod)"), Settings.PartySync);

        Section(ref cy, cw, Loc.T("Variedad de mapa (casos random, todos con el mod)", "Map variety (random cases, everyone modded)"));
        Toggle(ref cy, cw, Loc.T("Más piezas raras y zonas mezcladas", "More rare pieces and mixed zones"), Settings.MapVarietyEnabled);
        Slider(ref cy, cw, Loc.T("Piezas raras (%, juego: 5)", "Rare pieces (%, game: 5)"), Settings.MapRareChance, 5f, 100f, "0");
        Slider(ref cy, cw, Loc.T("Zonas mezcladas (x)", "Mixed zones (x)"), Settings.MapSubmazeMultiplier, 1f, 5f, "0.0");

        Section(ref cy, cw, Loc.T("Cosméticos (host)", "Cosmetics (host)"));
        Cycle(ref cy, cw, Loc.T("Evento de temporada forzado", "Forced seasonal event"), Settings.SeasonalOverride, SeasonModes);
        Slider(ref cy, cw, Loc.T("Chance de temporada (-1 = la del juego)", "Seasonal chance (-1 = game default)"), Settings.SeasonalChance, -1f, 1f, "0.00");
        Toggle(ref cy, cw, Loc.T("Priorizar los que nadie del grupo tiene", "Prefer items nobody in the party owns"), Settings.PoolPreferNew);
        Toggle(ref cy, cw, Loc.T("Sumar todos los eventos al pool", "Add every event to the pool"), Settings.PoolAddAllSeasonal);

        Section(ref cy, cw, Loc.T("Avisos", "Alerts"));
        Toggle(ref cy, cw, Loc.T("Aviso del cosmético del caso", "Case cosmetic alert"), Settings.CosmeticAlertEnabled);
        Slider(ref cy, cw, Loc.T("Segundos del aviso", "Alert seconds"), Settings.CosmeticAlertSeconds, 2f, 30f, "0");
        Cycle(ref cy, cw, Loc.T("Marcador en el mundo (F9)", "World marker (F9)"), Settings.MarkerMode, MarkerModes);
        Toggle(ref cy, cw, Loc.T("Marcar tickets, fichas de reroll y XP", "Mark tickets, reroll coins and XP"), Settings.CollectibleMarkers);
        Slider(ref cy, cw, Loc.T("Alcance del sonido del Pigman (x, menos = más silencioso)", "Pigman sound range (x, lower = quieter)"), Settings.PigmanAudioMultiplier, 0.2f, 3f, "0.0");
        Toggle(ref cy, cw, Loc.T("Cartel cuando un monstruo te persigue", "Banner when a monster chases you"), Settings.MonsterChaseAlert);

        Section(ref cy, cw, Loc.T("Tablero de casos", "Case board"));
        Toggle(ref cy, cw, Loc.T("Probabilidades en las carpetas", "Odds on the case folders"), Settings.CaseBoardPrediction);
        Toggle(ref cy, cw, Loc.T("Post-it con imagen", "Picture post-it"), Settings.CaseBoardPostIt);
        Slider(ref cy, cw, Loc.T("Tamaño del post-it (líneas)", "Post-it size (lines)"), Settings.CaseBoardPostItSize, 0.5f, 6f, "0.0");

        Section(ref cy, cw, Loc.T("Imagen", "Picture"));
        Toggle(ref cy, cw, Loc.T("Brillo extra (F6)", "Extra brightness (F6)"), Settings.BrightnessEnabled);
        Slider(ref cy, cw, Loc.T("Exposición extra (EV)", "Extra exposure (EV)"), Settings.ExtraExposureEV, -1f, 5f, "0.00");
        Slider(ref cy, cw, Loc.T("Gamma extra", "Extra gamma"), Settings.GammaBoost, 0f, 1f, "0.00");

        Section(ref cy, cw, Loc.T("Partida", "Gameplay"));
        Toggle(ref cy, cw, Loc.T("Levantarse solo (F5)", "Get up on your own (F5)"), Settings.SelfReviveAlways);
        Toggle(ref cy, cw, Loc.T("Ignorar detector de trampas del juego", "Ignore the game's cheat detector"), Settings.DisableCheatDetector);

#if EXPERIMENTS
        Section(ref cy, cw, Loc.T("Experimental (solo jugando solo)", "Experimental (solo play only)"));
        Toggle(ref cy, cw, Loc.T("Piezas raras de otro mapa (se cargan en el lobby)", "Rare pieces from another map (loaded in the lobby)"), Settings.ExperimentForeignPieces);
        Cycle(ref cy, cw, Loc.T("Mapa donante", "Donor map"), Settings.ExperimentDonorMaze, DonorMazes);
#endif

        GUI.EndScrollView();
    }

    // ---- controls ----

    private static void Section(ref float y, float w, string title)
    {
        y += 8;
        Ui.Text(new Rect(0, y, w, 26), title, Ui.Bold, new Color(0.6f, 0.8f, 1f));
        y += 26;
    }

    private static void Toggle(ref float y, float w, string label, MelonPreferences_Entry<bool> e)
    {
        bool v = GUI.Toggle(new Rect(10, y + 4, w - 10, RowH - 4), e.Value, "  " + label);
        if (v != e.Value) { e.Value = v; Settings.Save(); }
        y += RowH;
    }

    private static void Slider(ref float y, float w, string label, MelonPreferences_Entry<float> e, float min, float max, string fmt)
    {
        Ui.Text(new Rect(10, y, w * 0.55f, RowH), label, Ui.Small, Color.white);
        float v = GUI.HorizontalSlider(new Rect(w * 0.57f, y + 10, w * 0.3f, 18), e.Value, min, max);
        if (Math.Abs(v - e.Value) > 0.0001f) { e.Value = (float)Math.Round(v, 2); _dirty = true; }
        Ui.Text(new Rect(w * 0.89f, y, w * 0.11f, RowH), e.Value.ToString(fmt), Ui.Small, Color.white);
        y += RowH;
    }

    private static void IntSlider(ref float y, float w, string label, MelonPreferences_Entry<int> e, int min, int max)
    {
        Ui.Text(new Rect(10, y, w * 0.55f, RowH), label, Ui.Small, Color.white);
        int v = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(w * 0.57f, y + 10, w * 0.3f, 18), e.Value, min, max));
        if (v != e.Value) { e.Value = v; _dirty = true; }
        Ui.Text(new Rect(w * 0.89f, y, w * 0.11f, RowH), e.Value.ToString(), Ui.Small, Color.white);
        y += RowH;
    }

    private static void Cycle(ref float y, float w, string label, MelonPreferences_Entry<string> e, string[] options)
    {
        Ui.Text(new Rect(10, y, w * 0.55f, RowH), label, Ui.Small, Color.white);
        if (GUI.Button(new Rect(w * 0.57f, y + 3, w * 0.3f, RowH - 6), e.Value))
        {
            int i = Array.FindIndex(options, o => o.Equals(e.Value, StringComparison.OrdinalIgnoreCase));
            e.Value = options[(i + 1) % options.Length];
            Settings.Save();
        }
        y += RowH;
    }
}
