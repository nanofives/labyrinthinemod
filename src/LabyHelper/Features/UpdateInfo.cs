using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MelonLoader.Utils;

namespace LabyHelper.Features;

/// <summary>
/// F1 side of the auto-updater (src/Updater, a MelonLoader plugin that swaps Mods\LabyHelper.dll before mods load).
/// The plugin can't read our preferences that early, so the AutoUpdate setting is mirrored to the opt-out file
/// UserData\LabyHelper.noupdate. Also asks GitHub (off the main thread) for the latest version to show in F1.
/// </summary>
internal static class UpdateInfo
{
    private const string Api = "https://api.github.com/repos/nanofives/labyrinthinemod/releases/latest";

    public static string Latest { get; private set; }      // "0.3.9", null while unknown
    public static bool Checking { get; private set; }
    public static bool CheckFailed { get; private set; }
    private static bool _checked;

    private static string OptOutFile => Path.Combine(MelonEnvironment.UserDataDirectory, "LabyHelper.noupdate");
    public static bool PluginInstalled => File.Exists(Path.Combine(MelonEnvironment.PluginsDirectory, "LabyHelper.Updater.dll"));

    public static void Init()
    {
        Sync();
        Settings.AutoUpdate.OnEntryValueChanged.Subscribe((_, _) => Sync());
    }

    private static void Sync()
    {
        try
        {
            if (Settings.AutoUpdate.Value) { if (File.Exists(OptOutFile)) File.Delete(OptOutFile); }
            else if (!File.Exists(OptOutFile)) File.WriteAllText(OptOutFile, "LabyHelper: auto-update off (F1 > Updates).");
        }
        catch (Exception e) { Core.Log.Warning($"UpdateInfo: {e.Message}"); }
    }

    /// <summary>Starts the GitHub check once per session (the F1 menu calls this when it opens).</summary>
    public static void CheckOnce()
    {
        if (_checked) return;
        _checked = true;
        Checking = true;
        Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("LabyHelper");
                using var doc = JsonDocument.Parse(await http.GetStringAsync(Api));
                Latest = (doc.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
            }
            catch { CheckFailed = true; }
            finally { Checking = false; }
        });
    }

    /// <summary>True when GitHub has a newer version than the one running.</summary>
    public static bool NewerAvailable =>
        Latest != null && Version.TryParse(Latest, out var l) && Version.TryParse(BuildInfo.Version, out var c) && l > c;
}
