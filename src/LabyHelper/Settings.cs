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
    public static MelonPreferences_Entry<bool> CollectibleMarkers;
    public static MelonPreferences_Entry<int> CollectibleMaxMarkers;
    public static MelonPreferences_Entry<bool> PoolPreferNew;
    public static MelonPreferences_Entry<bool> CaseBoardPostIt;
    public static MelonPreferences_Entry<float> CaseBoardPostItSize;
    public static MelonPreferences_Entry<bool> SelfReviveAlways;
    public static MelonPreferences_Entry<bool> PoolAddAllSeasonal;
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
            description: "Avisa al empezar el caso que cosmetico spawneo. F9 lo repite con distancia y direccion.");
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
        CollectibleMarkers = _cat.CreateEntry("CollectibleMarkers", true,
            description: "Marca tickets, fichas de reroll y XP del caso junto con el cosmetico (F9 o Always) y los resume en el aviso de F9.");
        CollectibleMaxMarkers = _cat.CreateEntry("CollectibleMaxMarkers", 15,
            description: "Cuantos coleccionables marcar a la vez (los mas cercanos).");
        MarkerMode = _cat.CreateEntry("MarkerMode", "Hotkey",
            description: "Marcador sobre el cosmetico, visible a traves de paredes: Off, Hotkey (8 s tras F9), Always.");
        SelfReviveAlways = _cat.CreateEntry("SelfReviveAlways", true,
            description: "Levantarse solo despues de que un monstruo te tire, sin el item ni ayuda (usa la auto-reanimacion del juego). F5 alterna; estando caido, F5 te levanta.");
        CaseBoardPostIt = _cat.CreateEntry("CaseBoardPostIt", true,
            description: "Post-it con la imagen del exclusivo mas probable que no tenes, debajo del texto de cada carpeta.");
        CaseBoardPostItSize = _cat.CreateEntry("CaseBoardPostItSize", 3f,
            description: "Lado del post-it medido en lineas de texto de la carpeta (0.5 a 6).");
        PoolPreferNew = _cat.CreateEntry("PoolPreferNew", true,
            description: "Como host: excluye del sorteo los cosmeticos que ya tenes, asi sale uno nuevo mientras quede alguno en el pool. Se cambia en Opciones LabyHelper (F1).");
        PoolAddAllSeasonal = _cat.CreateEntry("PoolAddAllSeasonal", true,
            description: "Como host: suma al pool del mapa los cosmeticos de todos los eventos de temporada (no aplica a Hardcore).");
        RandomCasesOnlyNew = _cat.CreateEntry("RandomCasesOnlyNew", true,
            description: "Como host: en los casos random del tablero siempre sale un cosmetico y solo uno que le falte a alguien del grupo. Los casos custom quedan como el juego. Solo se apaga desde Opciones LabyHelper (F1).");
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
