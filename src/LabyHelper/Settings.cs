using MelonLoader;

namespace LabyHelper;

/// <summary>Persisted in UserData/MelonPreferences.cfg, section [LabyHelper].</summary>
internal static class Settings
{
    private static MelonPreferences_Category _cat;

    public static MelonPreferences_Entry<string> Language;
    public static MelonPreferences_Entry<bool> BrightnessEnabled;
    public static MelonPreferences_Entry<float> ExtraExposureEV;
    public static MelonPreferences_Entry<float> GammaBoost;
    public static MelonPreferences_Entry<bool> DisableCheatDetector;
    public static MelonPreferences_Entry<bool> CosmeticAlertEnabled;
    public static MelonPreferences_Entry<float> CosmeticAlertSeconds;
    public static MelonPreferences_Entry<float> PigmanAudioMultiplier;
    public static MelonPreferences_Entry<bool> MonsterChaseAlert;
    public static MelonPreferences_Entry<string> SeasonalOverride;
    public static MelonPreferences_Entry<float> SeasonalChance;
    public static MelonPreferences_Entry<bool> CaseBoardPrediction;
    public static MelonPreferences_Entry<string> MarkerMode;
    public static MelonPreferences_Entry<bool> PoolPreferNew;
    public static MelonPreferences_Entry<bool> CaseBoardPostIt;
    public static MelonPreferences_Entry<float> CaseBoardPostItSize;
    public static MelonPreferences_Entry<bool> SelfReviveAlways;
    public static MelonPreferences_Entry<float> SprintSpeedMultiplier;
    public static MelonPreferences_Entry<bool> AutoGlowstick;
    public static MelonPreferences_Entry<bool> AutoUpdate;
    public static MelonPreferences_Entry<bool> BoardOnlySealed;
    public static MelonPreferences_Entry<bool> SmallMapsOnly;
    public static MelonPreferences_Entry<bool> MoreDlcMaps;
    public static MelonPreferences_Entry<bool> CosmeticPanelOnByDefault;
    public static MelonPreferences_Entry<float> AutoGlowstickSeconds;
    public static MelonPreferences_Entry<bool> MinimapEnabled;
    public static MelonPreferences_Entry<float> MinimapSize;
    public static MelonPreferences_Entry<bool> MinimapLegend;
    public static MelonPreferences_Entry<float> HudTextTop;
    public static MelonPreferences_Entry<bool> AutoFlashlight;
    public static MelonPreferences_Entry<bool> PoolAddAllSeasonal;
    public static MelonPreferences_Entry<bool> PoolEverything;
    public static MelonPreferences_Entry<bool> RandomCasesOnlyNew;
    public static MelonPreferences_Entry<bool> DistributeAcrossCases;
    public static MelonPreferences_Entry<bool> PartySync;
    public static MelonPreferences_Entry<bool> MapVarietyEnabled;
    public static MelonPreferences_Entry<float> MapRareChance;
    public static MelonPreferences_Entry<float> MapSubmazeMultiplier;
#if EXPERIMENTS
    public static MelonPreferences_Entry<bool> ExperimentForeignPieces;
    public static MelonPreferences_Entry<string> ExperimentDonorMaze;
#endif

    public static void Init()
    {
        _cat = MelonPreferences.CreateCategory("LabyHelper");
        Language = _cat.CreateEntry("Language", "Auto",
            description: "Idioma del mod / mod language: Auto (idioma del sistema), Español, English.");
        BrightnessEnabled = _cat.CreateEntry("BrightnessEnabled", true,
            description: "Suma exposicion/gamma encima del brillo del juego.");
        ExtraExposureEV = _cat.CreateEntry("ExtraExposureEV", 0.75f,
            description: "EV extra sobre ColorAdjustments.postExposure. 1 EV = el doble de luz.");
        GammaBoost = _cat.CreateEntry("GammaBoost", 0.15f,
            description: "Offset sumado a LiftGammaGain.gamma.w. Aclara sombras sin quemar luces. Rango util 0 a 0.5.");
        DisableCheatDetector = _cat.CreateEntry("DisableCheatDetector", true,
            description: "Fuerza CheatingDetector.CheatingDetected = false.");
        CosmeticAlertEnabled = _cat.CreateEntry("CosmeticAlertEnabled", true,
            description: "Avisa al empezar el caso que cosmetico spawneo. F9 lo muestra/oculta con distancia y direccion.");
        CosmeticAlertSeconds = _cat.CreateEntry("CosmeticAlertSeconds", 10f,
            description: "Segundos que queda el aviso inicial en pantalla.");
        PigmanAudioMultiplier = _cat.CreateEntry("PigmanAudioMultiplier", 0.4f,
            description: "Alcance del sonido del Pigman: menos de 1 = mas silencioso (0.4 = 2.5 veces), mas de 1 = mas fuerte, 1 = original.");
        MonsterChaseAlert = _cat.CreateEntry("MonsterChaseAlert", true,
            description: "Cartel rojo con el nombre de cada monstruo que te esta persiguiendo (todos los monstruos).");
        SeasonalOverride = _cat.CreateEntry("SeasonalOverride", "Random",
            description: "Evento usado al spawnear cosmeticos en casos custom: Off, Random, Halloween, Christmas, Easter, Valentines, StPatrick, Summer. Se cambia en Opciones LabyHelper (F1).");
        SeasonalChance = _cat.CreateEntry("SeasonalChance", -1f,
            description: "Probabilidad de que el cosmetico spawneado sea de temporada (0 a 1). -1 = la del juego.");
        CaseBoardPrediction = _cat.CreateEntry("CaseBoardPrediction", true,
            description: "Muestra en cada carpeta del tablero de casos que cosmetico va a salir. Aprende el pool de cada mapa al jugarlo.");
        MarkerMode = _cat.CreateEntry("MarkerMode", "Hotkey",
            description: "Marcador sobre el cosmetico, visible a traves de paredes: Off, Hotkey (mientras F9 este activo), Always.");
        SelfReviveAlways = _cat.CreateEntry("SelfReviveAlways", true,
            description: "Levantarse solo despues de que un monstruo te tire, sin el item ni ayuda (usa la auto-reanimacion del juego). Se cambia solo en Opciones LabyHelper (F1).");
        SprintSpeedMultiplier = _cat.CreateEntry("SprintSpeedMultiplier", 1.25f,
            description: "Velocidad al correr de tu jugador (x, 1 = la del juego). No afecta a los monstruos. Cada jugador necesita el mod para tenerla.");
        BoardOnlySealed = _cat.CreateEntry("BoardOnlySealed", true,
            description: "Como host: todos los casos random del tablero salen con sello (azul o HARDCORE). HARDCORE solo mientras a alguien del grupo le falte un cosmetico HARDCORE.");
        SmallMapsOnly = _cat.CreateEntry("SmallMapsOnly", true,
            description: "Como host: las carpetas random del tablero salen de tamano mediano, chico o muy chico (Grande -> Muy chico, Muy grande -> Chico, Gigante -> Mediano).");
        MoreDlcMaps = _cat.CreateEntry("MoreDlcMaps", true,
            description: "Como host: si todos en el lobby tienen el DLC egipcio, los mapas egipcios salen 4 veces mas seguido en el tablero.");
        AutoUpdate = _cat.CreateEntry("AutoUpdate", true,
            description: "Al abrir el juego, baja la ultima version de GitHub antes de cargar el mod (plugin LabyHelper.Updater).");
        CosmeticPanelOnByDefault = _cat.CreateEntry("CosmeticPanelOnByDefault", true,
            description: "F9 (aviso fijo del cosmetico, jugadores y marcadores) arranca prendido en cada caso.");
        AutoGlowstick = _cat.CreateEntry("AutoGlowstick", true,
            description: "Al empezar el caso pone el glowstick en la mano izquierda y tira uno solo cada AutoGlowstickInterval segundos mientras avanzas.");
        // New key (was AutoGlowstickSeconds = 15) so the new 2 s default applies to existing configs.
        AutoGlowstickSeconds = _cat.CreateEntry("AutoGlowstickInterval", 2f,
            description: "Segundos entre glowsticks automaticos (minimo 1).");
        MinimapEnabled = _cat.CreateEntry("MinimapEnabled", true,
            description: "Minimapa fijo arriba a la derecha durante los casos. F4 lo muestra/oculta.");
        MinimapSize = _cat.CreateEntry("MinimapSize", 260f,
            description: "Lado del minimapa en pixeles (120 a 600).");
        MinimapLegend = _cat.CreateEntry("MinimapLegend", true,
            description: "Leyenda de colores y simbolos debajo del minimapa.");
        HudTextTop = _cat.CreateEntry("HudTextTopPct", 11f,
            description: "Altura del texto de LabyHelper a la izquierda (aviso del cosmetico, F9), en % de la pantalla desde arriba. Mas abajo que las condiciones del caso del juego.");
        AutoFlashlight = _cat.CreateEntry("AutoFlashlight", true,
            description: "Al empezar el caso pone la linterna en la mano derecha (encendida), si el caso la tiene.");
        CaseBoardPostIt = _cat.CreateEntry("CaseBoardPostIt", true,
            description: "Post-it con la imagen del exclusivo mas probable que no tenes, debajo del texto de cada carpeta.");
        CaseBoardPostItSize = _cat.CreateEntry("CaseBoardPostItSize", 3f,
            description: "Lado del post-it medido en lineas de texto de la carpeta (0.5 a 6).");
        PoolPreferNew = _cat.CreateEntry("PoolPreferNew", true,
            description: "Como host: excluye del sorteo los cosmeticos que ya tenes, asi sale uno nuevo mientras quede alguno en el pool. Se cambia en Opciones LabyHelper (F1).");
        PoolAddAllSeasonal = _cat.CreateEntry("PoolAddAllSeasonal", true,
            description: "Como host: suma al pool del mapa los cosmeticos de todos los eventos de temporada (no aplica a Hardcore).");
        PoolEverything = _cat.CreateEntry("PoolEverything", true,
            description: "Como host: los casos random tambien pueden dar cosmeticos que no salen de ningun caso (tienda, recompensas por nivel, limitados). No incluye musica ni el DLC egipcio.");
        RandomCasesOnlyNew = _cat.CreateEntry("RandomCasesOnlyNew", true,
            description: "Como host: en los casos random (del tablero y custom) siempre sale un cosmetico y solo uno que le falte a alguien del grupo. Solo se apaga desde Opciones LabyHelper (F1).");
        DistributeAcrossCases = _cat.CreateEntry("DistributeAcrossCases", true,
            description: "Reparte los cosmeticos faltantes entre las carpetas random del tablero: cada uno puede salir en una sola.");
        MapVarietyEnabled = _cat.CreateEntry("MapVarietyEnabled", true,
            description: "Casos random: mas piezas raras y mas zonas mezcladas (submazes). Solo se aplica si todos en el lobby tienen el mod; usan los valores del host.");
        MapRareChance = _cat.CreateEntry("MapRareChance", 25f,
            description: "Porcentaje de piezas en su variante rara (el juego usa 5).");
        MapSubmazeMultiplier = _cat.CreateEntry("MapSubmazeMultiplier", 2f,
            description: "Multiplica la chance de que aparezcan zonas de otro tipo de laberinto en los mapas que las tienen.");
#if EXPERIMENTS
        ExperimentForeignPieces = _cat.CreateEntry("ExperimentForeignPieces", false,
            description: "EXPERIMENTAL, solo jugando solo: mezcla piezas raras de otro mapa (ExperimentDonorMaze) en los casos random. Carga ese mapa unos segundos en el lobby. Apagado por defecto.");
        ExperimentDonorMaze = _cat.CreateEntry("ExperimentDonorMaze", "Maze_F",
            description: "Mapa que presta sus piezas raras: Maze_A..Maze_R o Maze_Backrooms. Solo se usa en mapas con el mismo tileSize (casi todos usan 10; D=7, R=21, K/O/Q=30).");
#endif
        PartySync = _cat.CreateEntry("PartySync", true,
            description: "Comparte por el lobby de Steam que cosmeticos tiene cada jugador (todos necesitan el mod).");
    }

    public static void Save() => _cat.SaveToFile(false);
}
