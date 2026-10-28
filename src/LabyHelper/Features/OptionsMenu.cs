using System;
using MelonLoader;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// "Opciones LabyHelper": every mod setting in one wide panel, split into tabs, drawn in the game's style (Ui).
/// F1 opens it anywhere (menus, lobby, during a case); over gameplay it stops the player and frees the cursor like the
/// game's own popups. A button also shows up while the game's settings screen is open (detected through
/// LabyrinthineSettingsManager's controls being visible). A bar across the top of the screen lists the F keys in the
/// lobby and in a case's safe zones.
/// The difficulty gate and the minimap can only be switched here (no hotkey), by design.
/// </summary>
internal static class OptionsMenu
{
    private enum Page { General, Difficulty, Variety, Cosmetics, Alerts, Board, Picture, Gameplay, Experimental }

    private const float RowH = 58f;
    private static Page _page = Page.General;
    private static readonly float[] Scroll = new float[Enum.GetValues(typeof(Page)).Length];
    private static float _contentH = 600f; // measured on the previous frame
    private static bool _open, _settingsOpen, _dirty;
    private static float _saveAt, _nextCheck;
    private static CursorLockMode _prevLock;
    private static bool _prevVisible;
    private static bool _confirmRestart, _confirmUpdate;
    private static Action _preview; // set by the hovered (or dragged) row that has one; drawn over everything

    private static readonly string[] SeasonModes =
        { "Off", "Random", "Halloween", "Christmas", "Easter", "Valentines", "StPatrick", "Summer" };
    private static readonly string[] MarkerModes = { "Off", "Hotkey", "Always" };
    // tileSize 10 maps with rare pieces (re/EXPERIMENT-foreign-pieces.md)
    private static readonly string[] DonorMazes =
        { "Maze_F", "Maze_Backrooms", "Maze_E", "Maze_J", "Maze_C", "Maze_I", "Maze_N", "Maze_A", "Maze_B", "Maze_L", "Maze_P", "Maze_H", "Maze_G" };

    public static void Update()
    {
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

    public static void OnSceneLoaded() => SetOpen(false);

    private static float _top = 8f; // below the F-key bar when it's on screen (updated every frame)

    /// <summary>Where the F1 panel goes; the F2 collection uses the same rectangle.</summary>
    public static Rect PanelRect()
    {
        float w = Mathf.Min(1480f, Ui.W - 48f);
        float top = Mathf.Max(_top, 16f);
        return new Rect(Ui.W / 2f - w / 2f, top, w, Ui.H - top - 24f);
    }

    private static void SetOpen(bool open)
    {
        if (open == _open) return;
        _open = open;
        _confirmRestart = _confirmUpdate = false;
        if (open)
        {
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
            // Over gameplay (not from the game's settings screen): stop the player and free the cursor.
            Ui.Capture("F1", !_settingsOpen);
            UpdateInfo.CheckOnce();
        }
        else
        {
            Ui.Capture("F1", false);
            Settings.Save();
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
            CaseBoard.RefreshAll();
        }
    }

    /// <summary>Where the F-key bar shows: the lobby, or a case while you're in a safe zone (and with a panel up).</summary>
    private static bool BarVisible => GameState.InLobby || (GameState.InCase && Players.MeInSafeZone());

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

    private static (Page page, string es, string en)[] Pages()
    {
        var list = new System.Collections.Generic.List<(Page, string, string)>
        {
            (Page.General, "General", "General"),
            (Page.Difficulty, "Dificultad", "Difficulty"),
            (Page.Variety, "Mapa", "Map"),
            (Page.Cosmetics, "Cosméticos", "Cosmetics"),
            (Page.Alerts, "Avisos", "Alerts"),
            (Page.Board, "Tablero", "Case board"),
            (Page.Picture, "Imagen", "Picture"),
            (Page.Gameplay, "Partida", "Gameplay"),
        };
#if EXPERIMENTS
        list.Add((Page.Experimental, "Experimental", "Experimental"));
#endif
        return list.ToArray();
    }

    /// <summary>Drawn at 1080p layout scaled to the screen; the previews draw afterwards in real pixels.</summary>
    public static void Draw()
    {
        _preview = null;
        Ui.BeginScaled();
        try { DrawScaled(); }
        finally { Ui.EndScaled(); }
        _preview?.Invoke();
    }

    private static void DrawScaled()
    {
        float top = 8f;
        if (_open || CollectionViewer.IsOpen || BarVisible) top = HotkeyBar.Draw(top) + 8f;
        _top = top;
        if (_settingsOpen && !_open)
        {
            if (Ui.Button(new Rect(Ui.W - 380, Ui.H - 84, 350, 56), Loc.T("Opciones LabyHelper (F1)", "LabyHelper options (F1)"))) SetOpen(true);
        }
        if (!_open) return;

        var area = PanelRect();
        Styles();
        Ui.Panel(area);
        float pad = 44f;
        float x = area.x + pad, y = area.y + 30, w = area.width - 2 * pad;

        // Title: big name, version beside it.
        Ui.Text(new Rect(x, y, 400, 48), "LABYHELPER", Ui.Title, Ui.Paper);
        float tw = Ui.Title.CalcSize(new GUIContent("LABYHELPER")).x;
        Ui.Text(new Rect(x + tw + 16, y + 10, 300, 36), $"v{BuildInfo.Version} · {Loc.T("opciones", "options")}", _small, Ui.Dim);
        if (Ui.Close(new Rect(area.xMax - pad - 44, y + 2, 44, 44))) { SetOpen(false); return; }
        y += 64;

        // Tabs across the whole width.
        var pages = Pages();
        float tabW = w / pages.Length, tabH = 56f;
        var tabStyle = TabStyleFor(tabW, pages);
        for (int i = 0; i < pages.Length; i++)
        {
            var (p, es, en) = pages[i];
            if (Ui.Tab(new Rect(x + i * tabW + 3, y, tabW - 6, tabH), Loc.T(es, en), _page == p, tabStyle) && _page != p)
            {
                _page = p;
                _confirmRestart = _confirmUpdate = false;
            }
        }
        y += tabH + 18;

        var view = new Rect(x - 8, y, w + 16, area.yMax - y - 26);
        int pi = (int)_page;
        Ui.BeginScroll(view, ref Scroll[pi], _contentH, "f1:" + _page);
        float cy = 4, cw = view.width - 24;
        switch (_page)
        {
            case Page.General: General(ref cy, cw); break;
            case Page.Difficulty: Difficulty(ref cy, cw); break;
            case Page.Variety: Variety(ref cy, cw); break;
            case Page.Cosmetics: Cosmetics(ref cy, cw); break;
            case Page.Alerts: Alerts(ref cy, cw); break;
            case Page.Board: Board(ref cy, cw); break;
            case Page.Picture: Picture(ref cy, cw); break;
            case Page.Gameplay: Gameplay(ref cy, cw); break;
#if EXPERIMENTS
            case Page.Experimental:
                Section(ref cy, cw, Loc.T("Solo jugando solo", "Solo play only"));
                Toggle(ref cy, cw, Loc.T("Piezas raras de otro mapa (se cargan en el lobby)", "Rare pieces from another map (loaded in the lobby)"), Settings.ExperimentForeignPieces);
                Cycle(ref cy, cw, Loc.T("Mapa donante", "Donor map"), Settings.ExperimentDonorMaze, DonorMazes);
                break;
#endif
        }
        _contentH = cy + 8f;
        Ui.EndScroll(ref Scroll[pi]);
    }

    // ---- previews of the graphic options ----

    /// <summary>The cosmetic alert at its real height (Toast draws after the panel, so it shows on top).</summary>
    private static void PreviewHud() =>
        Toast.Show(Loc.T("Cosmetico del caso: EJEMPLO (vista previa)", "Case cosmetic: EXAMPLE (preview)"), 0.3f);

    private static void PreviewChase() =>
        Toast.Show(Loc.T("PIGMAN te persigue A VOS (vista previa)", "PIGMAN is chasing YOU (preview)"), 0.3f, Toast.Slot.Alert);

    private static void PreviewMinimap() => Minimap.DrawPreview();

    /// <summary>
    /// Grey steps from black to mid-grey, as the game shows them and with the extra exposure and gamma on top
    /// (approximate: the real result also depends on the map's own lighting).
    /// </summary>
    private static void BrightnessPreview(ref float cy, float cw)
    {
        Section(ref cy, cw, Loc.T("Vista previa (aproximada)", "Preview (approximate)"));
        const int steps = 12;
        float sw = Mathf.Min(64f, (cw - 260f) / steps), sh = 54f, x0 = 240f;
        Ui.Text(new Rect(14, cy, 220, sh), Loc.T("Juego", "Game"), _small, Ui.Dim);
        Ui.Text(new Rect(14, cy + sh + 8, 220, sh), "LabyHelper", _small, Ui.Paper);
        float ev = Settings.BrightnessEnabled.Value ? Settings.ExtraExposureEV.Value : 0f;
        float g = Settings.BrightnessEnabled.Value ? Settings.GammaBoost.Value : 0f;
        for (int i = 0; i < steps; i++)
        {
            float v = 0.6f * i / (steps - 1);
            float lin = Mathf.Pow(v, 2.2f) * Mathf.Pow(2f, ev);
            float after = Mathf.Pow(Mathf.Pow(Mathf.Clamp01(lin), 1f / 2.2f), 1f / (1f + 2f * g));
            Ui.Fill(new Rect(x0 + i * sw, cy, sw - 4, sh), new Color(v, v, v, 1f));
            Ui.Fill(new Rect(x0 + i * sw, cy + sh + 8, sw - 4, sh), new Color(after, after, after, 1f));
        }
        cy += 2 * sh + 24;
    }

    /// <summary>A case folder with its text lines and the post-it at the chosen size (in lines of that text).</summary>
    private static void PostItPreview(ref float cy, float cw)
    {
        Section(ref cy, cw, Loc.T("Vista previa", "Preview"));
        const float line = 22f;
        float note = line * Mathf.Clamp(Settings.CaseBoardPostItSize.Value, 0.5f, 6f);
        var folder = new Rect(14, cy, 420, Mathf.Max(170f, 5 * line + note + 30f));
        Ui.Fill(folder, new Color(0.78f, 0.7f, 0.52f, 1f));
        for (int i = 0; i < 4; i++) Ui.Fill(new Rect(folder.x + 22, folder.y + 20 + i * line, 220 - (i % 2) * 70, line * 0.45f), new Color(0.12f, 0.1f, 0.08f, 0.75f));
        if (Settings.CaseBoardPostIt.Value)
        {
            var n = new Rect(folder.x + 22, folder.y + 24 + 4 * line, note, note);
            Ui.Fill(n, new Color(0.98f, 0.9f, 0.35f, 1f));
            Ui.Fill(new Rect(n.x + note * 0.15f, n.y + note * 0.15f, note * 0.7f, note * 0.7f), new Color(0.3f, 0.3f, 0.3f, 0.5f));
        }
        cy += folder.height + 10;
    }

    // ---- pages ----

    private static void General(ref float cy, float cw)
    {
        Section(ref cy, cw, Loc.T("Idioma", "Language"));
        Cycle(ref cy, cw, Loc.T("Idioma del mod", "Mod language"), Settings.Language, Loc.Languages);

        Section(ref cy, cw, Loc.T("Actualizaciones", "Updates"));
        Toggle(ref cy, cw, Loc.T("Actualizar solo al abrir el juego", "Update automatically when the game starts"), Settings.AutoUpdate);
        Ui.Text(new Rect(14, cy, cw - 14, RowH), UpdateStatus(), _small, UpdateInfo.NewerAvailable ? new Color(1f, 0.88f, 0.55f) : Ui.Dim);
        cy += RowH;

        // Two actions side by side; each asks once more before closing the game.
        float bw = Mathf.Min(460f, (cw - 40f) / 2f), bh = 64f;
        var upd = new Rect(14, cy + 6, bw, bh);
        var rst = new Rect(14 + bw + 28, cy + 6, bw, bh);
        string updLabel = _confirmUpdate ? Loc.T("¿Seguro? Cierra el juego", "Sure? Closes the game") : Loc.T("Forzar actualización", "Force update");
        if (Ui.Button(upd, updLabel, !GameRestart.Busy))
        {
            if (_confirmUpdate) GameRestart.ForceUpdate();
            _confirmUpdate = !_confirmUpdate;
            _confirmRestart = false;
        }
        string rstLabel = _confirmRestart ? Loc.T("¿Seguro? Cierra el juego", "Sure? Closes the game") : Loc.T("Reiniciar el juego", "Restart the game");
        if (Ui.Button(rst, rstLabel, !GameRestart.Busy))
        {
            if (_confirmRestart) GameRestart.Restart();
            _confirmRestart = !_confirmRestart;
            _confirmUpdate = false;
        }
        cy += bh + 16;
        string note = GameRestart.Status ?? Loc.T(
            "Forzar actualización baja la última versión de GitHub (aunque sea la misma) y reinicia el juego por Steam.",
            "Force update downloads the latest version from GitHub (even if it's the same) and restarts the game through Steam.");
        Ui.Text(new Rect(14, cy, cw - 14, RowH), note, _small, GameRestart.Status != null ? Ui.Paper : Ui.Dim);
        cy += RowH;
    }

    private static void Difficulty(ref float cy, float cw)
    {
        Section(ref cy, cw, Loc.T("Casos random (host)", "Random cases (host)"));
        Toggle(ref cy, cw, Loc.T("Siempre cosmético y solo nuevos", "Always a cosmetic, only new ones"), Settings.RandomCasesOnlyNew);
        Toggle(ref cy, cw, Loc.T("Repartir los nuevos entre las 4 carpetas", "Spread new items across the 4 case files"), Settings.DistributeAcrossCases);
        Section(ref cy, cw, Loc.T("Tablero de casos (host)", "Case board (host)"));
        Toggle(ref cy, cw, Loc.T("Carpetas siempre con sello (azul o HARDCORE si falta alguno)", "Case files always sealed (blue, or HARDCORE while one is missing)"), Settings.BoardOnlySealed);
        Toggle(ref cy, cw, Loc.T("Carpetas de tamaño mediano a muy chico", "Case files medium to very small"), Settings.SmallMapsOnly);
        Toggle(ref cy, cw, Loc.T("Más mapas egipcios si todos tienen el DLC", "More Egypt maps if everyone owns the DLC"), Settings.MoreDlcMaps);
        Section(ref cy, cw, Loc.T("Grupo", "Party"));
        Toggle(ref cy, cw, Loc.T("Sincronizar con los jugadores (necesitan el mod)", "Sync with other players (they need the mod)"), Settings.PartySync);
    }

    private static void Variety(ref float cy, float cw)
    {
        Section(ref cy, cw, Loc.T("Variedad de mapa (casos random, todos con el mod)", "Map variety (random cases, everyone modded)"));
        Toggle(ref cy, cw, Loc.T("Más piezas raras y zonas mezcladas", "More rare pieces and mixed zones"), Settings.MapVarietyEnabled);
        Slider(ref cy, cw, Loc.T("Piezas raras (%, juego: 5)", "Rare pieces (%, game: 5)"), Settings.MapRareChance, 5f, 100f, "0");
        Slider(ref cy, cw, Loc.T("Zonas mezcladas (x)", "Mixed zones (x)"), Settings.MapSubmazeMultiplier, 1f, 5f, "0.0");
    }

    private static void Cosmetics(ref float cy, float cw)
    {
        Section(ref cy, cw, Loc.T("Temporada (host)", "Seasonal (host)"));
        Cycle(ref cy, cw, Loc.T("Evento de temporada forzado", "Forced seasonal event"), Settings.SeasonalOverride, SeasonModes);
        Slider(ref cy, cw, Loc.T("Chance de temporada (-1 = la del juego)", "Seasonal chance (-1 = game default)"), Settings.SeasonalChance, -1f, 1f, "0.00");
        Section(ref cy, cw, Loc.T("Pool (host)", "Pool (host)"));
        Toggle(ref cy, cw, Loc.T("Priorizar los que nadie del grupo tiene", "Prefer items nobody in the party owns"), Settings.PoolPreferNew);
        Toggle(ref cy, cw, Loc.T("Sumar todos los eventos al pool", "Add every event to the pool"), Settings.PoolAddAllSeasonal);
        Toggle(ref cy, cw, Loc.T("Sumar los de tienda, recompensas y limitados", "Add store, reward and limited items"), Settings.PoolEverything);
    }

    private static void Alerts(ref float cy, float cw)
    {
        Section(ref cy, cw, Loc.T("Cosmético del caso", "Case cosmetic"));
        Toggle(ref cy, cw, Loc.T("Aviso del cosmético del caso", "Case cosmetic alert"), Settings.CosmeticAlertEnabled, PreviewHud);
        Slider(ref cy, cw, Loc.T("Segundos del aviso", "Alert seconds"), Settings.CosmeticAlertSeconds, 2f, 30f, "0", PreviewHud);
        Slider(ref cy, cw, Loc.T("Altura del texto de la izquierda (% desde arriba)", "Left text height (% from the top)"), Settings.HudTextTop, 0f, 70f, "0", PreviewHud);
        Cycle(ref cy, cw, Loc.T("Marcador en el mundo (F9)", "World marker (F9)"), Settings.MarkerMode, MarkerModes);
        Toggle(ref cy, cw, Loc.T("F9 prendido al empezar cada caso", "F9 on when each case starts"), Settings.CosmeticPanelOnByDefault);
        Toggle(ref cy, cw, Loc.T("Poster en la pared mas cercana al cosmetico", "Poster on the wall nearest the cosmetic"), Settings.PosterNearCosmetic);
        Section(ref cy, cw, Loc.T("Monstruos", "Monsters"));
        Toggle(ref cy, cw, Loc.T("Cartel cuando un monstruo te persigue", "Banner when a monster chases you"), Settings.MonsterChaseAlert, PreviewChase);
        Slider(ref cy, cw, Loc.T("Alcance del sonido del Pigman (x, menos = más silencioso)", "Pigman sound range (x, lower = quieter)"), Settings.PigmanAudioMultiplier, 0.2f, 3f, "0.0");
    }

    private static void Board(ref float cy, float cw)
    {
        Section(ref cy, cw, Loc.T("Carpetas del tablero", "Case folders"));
        Toggle(ref cy, cw, Loc.T("Probabilidades en las carpetas", "Odds on the case folders"), Settings.CaseBoardPrediction);
        Toggle(ref cy, cw, Loc.T("Post-it con imagen", "Picture post-it"), Settings.CaseBoardPostIt);
        Slider(ref cy, cw, Loc.T("Tamaño del post-it (líneas)", "Post-it size (lines)"), Settings.CaseBoardPostItSize, 0.5f, 6f, "0.0");
        PostItPreview(ref cy, cw);
    }

    private static void Picture(ref float cy, float cw)
    {
        Section(ref cy, cw, Loc.T("Brillo", "Brightness"));
        Toggle(ref cy, cw, Loc.T("Brillo extra (F6)", "Extra brightness (F6)"), Settings.BrightnessEnabled);
        Slider(ref cy, cw, Loc.T("Exposición extra (EV)", "Extra exposure (EV)"), Settings.ExtraExposureEV, -1f, 5f, "0.00");
        Slider(ref cy, cw, Loc.T("Gamma extra", "Extra gamma"), Settings.GammaBoost, 0f, 1f, "0.00");
        BrightnessPreview(ref cy, cw);
    }

    private static void Gameplay(ref float cy, float cw)
    {
        Section(ref cy, cw, Loc.T("Jugador", "Player"));
        Toggle(ref cy, cw, Loc.T("Vidas infinitas al elegir un caso (host)", "Infinite lives when you pick a case (host)"), Settings.InfiniteLivesByDefault);
        Toggle(ref cy, cw, Loc.T("Levantarse solo", "Get up on your own"), Settings.SelfReviveAlways);
        Slider(ref cy, cw, Loc.T("Velocidad al correr (x, juego: 1)", "Sprint speed (x, game: 1)"), Settings.SprintSpeedMultiplier, 1f, 2f, "0.00");
        Toggle(ref cy, cw, Loc.T("Ignorar detector de trampas del juego", "Ignore the game's cheat detector"), Settings.DisableCheatDetector);
        Section(ref cy, cw, Loc.T("Al empezar el caso", "When the case starts"));
        Toggle(ref cy, cw, Loc.T("Prender la linterna", "Switch the flashlight on"), Settings.AutoFlashlight);
        Toggle(ref cy, cw, Loc.T("Tirar glowsticks solo", "Drop glowsticks automatically"), Settings.AutoGlowstick);
        Slider(ref cy, cw, Loc.T("Cada cuántos segundos", "Every N seconds"), Settings.AutoGlowstickSeconds, 1f, 60f, "0");
        Section(ref cy, cw, Loc.T("Minimapa", "Minimap"));
        Toggle(ref cy, cw, Loc.T("Minimapa", "Minimap"), Settings.MinimapEnabled, PreviewMinimap);
        Slider(ref cy, cw, Loc.T("Tamaño del minimapa (px)", "Minimap size (px)"), Settings.MinimapSize, 120f, 600f, "0", PreviewMinimap);
        Toggle(ref cy, cw, Loc.T("Leyenda del minimapa", "Minimap legend"), Settings.MinimapLegend, PreviewMinimap);
        Toggle(ref cy, cw, Loc.T("Recorrido de cada jugador", "Each player's trail"), Settings.MinimapTrail, PreviewMinimap);
    }

    // ---- controls ----

    private static GUIStyle _small, _row, _section;
    private static Font _stylesFont;
    private static GUIStyle _tabFit;
    private static float _tabFitW;
    private static string _tabFitLang;

    private static void Styles()
    {
        if (_small != null && _stylesFont == UiSkin.Font) return;
        _stylesFont = UiSkin.Font;
        _small = Ui.Sized(Ui.Label, 21);
        _row = Ui.Sized(Ui.Label, 26);
        _section = Ui.Sized(Ui.Header, 26);
        _tabFit = null;
    }

    /// <summary>The biggest tab text (up to 27) at which every tab label fits its width.</summary>
    private static GUIStyle TabStyleFor(float tabW, (Page, string es, string en)[] pages)
    {
        if (_tabFit != null && Mathf.Abs(_tabFitW - tabW) < 1f && _tabFitLang == Loc.T("es", "en")) return _tabFit;
        _tabFitW = tabW;
        _tabFitLang = Loc.T("es", "en");
        for (int size = 27; size >= 12; size--)
        {
            _tabFit = Ui.Sized(Ui.TabStyle, size);
            bool fits = true;
            foreach (var (_, es, en) in pages)
                if (_tabFit.CalcSize(new GUIContent(Ui.Caps(Loc.T(es, en)))).x > tabW - 26f) { fits = false; break; }
            if (fits) break;
        }
        return _tabFit;
    }

    private static string UpdateStatus()
    {
        string installed = Loc.T($"Instalada: {BuildInfo.Version}", $"Installed: {BuildInfo.Version}");
        if (!UpdateInfo.PluginInstalled)
            return installed + Loc.T(" · falta el actualizador: instalá una vez con LabyHelper-Setup.exe",
                                     " · updater missing: install once with LabyHelper-Setup.exe");
        if (UpdateInfo.Checking) return installed + Loc.T(" · buscando en GitHub...", " · checking GitHub...");
        if (UpdateInfo.CheckFailed || UpdateInfo.Latest == null) return installed + Loc.T(" · no se pudo consultar GitHub", " · couldn't reach GitHub");
        if (!UpdateInfo.NewerAvailable) return installed + Loc.T(" · es la última", " · up to date");
        return installed + (Settings.AutoUpdate.Value
            ? Loc.T($" · nueva: {UpdateInfo.Latest}, se instala al reiniciar el juego", $" · new: {UpdateInfo.Latest}, installs on the next game start")
            : Loc.T($" · nueva: {UpdateInfo.Latest} (actualización automática apagada)", $" · new: {UpdateInfo.Latest} (auto-update off)"));
    }

    private static void Section(ref float y, float w, string title)
    {
        if (y > 10) y += 14;
        Ui.Section(new Rect(14, y, w - 14, 50), title, _section);
        y += 58;
    }

    private static void Hovered(Rect row, string key, Action preview)
    {
        if (preview != null && (row.Contains(Event.current.mousePosition) || Ui.IsDragging(key))) _preview = preview;
    }

    // Label on the left; control in the right column.
    private static float CtrlX(float w) => Mathf.Max(w * 0.6f, w - 520f);

    private static void Toggle(ref float y, float w, string label, MelonPreferences_Entry<bool> e, Action preview = null)
    {
        var row = new Rect(0, y, w, RowH);
        Hovered(row, e.Identifier, preview);
        Ui.RowHover(row, e.Identifier);
        Ui.Text(new Rect(14, y, CtrlX(w) - 24, RowH), label, _row, Ui.Paper);
        var sw = new Rect(CtrlX(w), y + (RowH - 34) / 2f, 70, 34);
        bool v = Ui.Switch(sw, e.Value, e.Identifier);
        Ui.Text(new Rect(sw.xMax + 14, y, 120, RowH), e.Value ? Loc.T("SI", "ON") : "NO", _small, e.Value ? Ui.Paper : Ui.Dim);
        // The label is clickable too.
        if (Ui.Hit(new Rect(0, y, CtrlX(w) - 10, RowH))) v = !e.Value;
        if (v != e.Value) { e.Value = v; Settings.Save(); }
        y += RowH;
    }

    private static void Slider(ref float y, float w, string label, MelonPreferences_Entry<float> e, float min, float max, string fmt, Action preview = null)
    {
        var row = new Rect(0, y, w, RowH);
        Hovered(row, e.Identifier, preview);
        Ui.RowHover(row, e.Identifier);
        Ui.Text(new Rect(14, y, CtrlX(w) - 24, RowH), label, _row, Ui.Paper);
        float sx = CtrlX(w), sw = w - sx - 110;
        float v = Ui.Slider(new Rect(sx, y + 8, sw, RowH - 16), e.Value, min, max, e.Identifier);
        if (Math.Abs(v - e.Value) > 0.0001f) { e.Value = (float)Math.Round(v, 2); _dirty = true; }
        Ui.Text(new Rect(sx + sw + 20, y, 90, RowH), e.Value.ToString(fmt), _row, Ui.Paper);
        y += RowH;
    }

    private static void Cycle(ref float y, float w, string label, MelonPreferences_Entry<string> e, string[] options, Action preview = null)
    {
        var row = new Rect(0, y, w, RowH);
        Hovered(row, e.Identifier, preview);
        Ui.RowHover(row, e.Identifier);
        Ui.Text(new Rect(14, y, CtrlX(w) - 24, RowH), label, _row, Ui.Paper);
        float sx = CtrlX(w);
        int step = Ui.Arrows(new Rect(sx - 8, y + 4, Mathf.Min(360f, w - sx - 8), RowH - 8), e.Value, e.Identifier);
        if (step != 0)
        {
            int i = Array.FindIndex(options, o => o.Equals(e.Value, StringComparison.OrdinalIgnoreCase));
            if (i < 0) i = 0;
            e.Value = options[((i + step) % options.Length + options.Length) % options.Length];
            Settings.Save();
        }
        y += RowH;
    }
}
