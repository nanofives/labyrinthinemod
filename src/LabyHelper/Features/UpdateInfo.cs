using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using MelonLoader.Utils;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// In-game side of the auto-updater (src/Updater, a MelonLoader plugin that swaps Mods\LabyHelper.dll before mods load).
/// The plugin can't read our preferences that early, so the AutoUpdate setting is mirrored to the opt-out file
/// UserData\LabyHelper.noupdate. While the game runs, asks GitHub (off the main thread) for the latest version every
/// half hour and, with AutoUpdate on, downloads it and puts it in Mods\ right away: Windows lets the loaded DLL be
/// renamed, so it is moved aside to UserData\ and the new file takes its place. The running mod stays the old one;
/// the new one loads on the next game start, which then shows the usual "updated itself" notice.
/// </summary>
internal static class UpdateInfo
{
    private const string Api = "https://api.github.com/repos/nanofives/labyrinthinemod/releases/latest";
    private const string AssetName = "LabyHelper.dll";
    private const float FirstCheck = 90f, Interval = 30 * 60f; // the plugin already checked at startup

    public static string Latest { get; private set; }      // "0.3.9", null while unknown
    public static string Staged { get; private set; }      // version already in Mods\, waiting for a restart
    public static bool Checking { get; private set; }
    public static bool CheckFailed { get; private set; }
    private static float _nextCheck = FirstCheck;
    private static volatile bool _announce;

    private static string UserData => MelonEnvironment.UserDataDirectory;
    private static string OptOutFile => Path.Combine(UserData, "LabyHelper.noupdate");
    private static string Aside => Path.Combine(UserData, AssetName + ".old");
    public static bool PluginInstalled => File.Exists(Path.Combine(MelonEnvironment.PluginsDirectory, "LabyHelper.Updater.dll"));

    public static void Init()
    {
        Sync();
        Settings.AutoUpdate.OnEntryValueChanged.Subscribe((_, _) => Sync());
        try { if (File.Exists(Aside)) File.Delete(Aside); } // the previous session's DLL, no longer loaded
        catch { /* still in use: next time */ }
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

    public static void Update()
    {
        if (_announce)
        {
            _announce = false;
            Toast.Show(Loc.T($"LabyHelper {Staged} descargado: se activa al reiniciar el juego",
                             $"LabyHelper {Staged} downloaded: active after restarting the game"), 15f);
        }
        if (Checking || Staged != null || Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + Interval;
        Check();
    }

    /// <summary>Checks now if it hasn't yet this session (the F1 menu calls this when it opens).</summary>
    public static void CheckOnce()
    {
        if (Latest != null || Checking || CheckFailed) return;
        _nextCheck = Time.unscaledTime + Interval;
        Check();
    }

    private static void Check()
    {
        Checking = true;
        bool install = Settings.AutoUpdate.Value;
        Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("LabyHelper");
                using var quick = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                using var doc = JsonDocument.Parse(await http.GetStringAsync(Api, quick.Token));
                var root = doc.RootElement;
                Latest = (root.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
                CheckFailed = false;
                if (install && NewerAvailable) await Install(http, root);
            }
            catch (Exception e)
            {
                CheckFailed = Latest == null;
                Core.Log.Warning($"UpdateInfo: {e.Message}");
            }
            finally { Checking = false; }
        });
    }

    /// <summary>Downloads the release's DLL and swaps it into Mods\. Any failure leaves the installed file as it was.</summary>
    private static async Task Install(HttpClient http, JsonElement release)
    {
        string target = Path.Combine(MelonEnvironment.ModsDirectory, AssetName);
        if (!File.Exists(target)) return;
        var latest = Normalize(Version.Parse(Latest));
        if (Normalize(AssemblyName.GetAssemblyName(target).Version) >= latest) { Staged = Latest; return; } // already there

        string url = null;
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
            if (asset.GetProperty("name").GetString() == AssetName) url = asset.GetProperty("browser_download_url").GetString();
        if (url == null) return;

        string temp = Path.Combine(UserData, AssetName + ".download");
        File.WriteAllBytes(temp, await http.GetByteArrayAsync(url));
        Version downloaded = AssemblyName.GetAssemblyName(temp).Version; // throws if it isn't a valid .NET assembly
        if (Normalize(downloaded) != latest)
        {
            File.Delete(temp);
            Core.Log.Warning($"UpdateInfo: downloaded DLL is {downloaded}, expected {Latest}.");
            return;
        }

        try { if (File.Exists(Aside)) File.Delete(Aside); }
        catch { File.Move(Aside, Aside + "." + DateTime.Now.Ticks); } // still loaded from an earlier swap
        File.Copy(target, Path.Combine(UserData, AssetName + ".bak"), overwrite: true); // same backup the plugin keeps
        File.Move(target, Aside); // allowed while loaded; UserData so MelonLoader never loads it
        try { File.Move(temp, target); }
        catch { File.Move(Aside, target); throw; }

        File.WriteAllText(Path.Combine(UserData, "LabyHelper.updated.txt"), $"{BuildInfo.Version} {Latest}");
        Staged = Latest;
        _announce = true;
        Core.Log.Msg($"UpdateInfo: LabyHelper {Latest} installed in Mods, active from the next game start.");
    }

    /// <summary>True when GitHub has a newer version than the one running.</summary>
    public static bool NewerAvailable =>
        Latest != null && Version.TryParse(Latest, out var l) && Version.TryParse(BuildInfo.Version, out var c) && Normalize(l) > Normalize(c);

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
