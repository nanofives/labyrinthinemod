using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(LabyHelper.Updater.Updater), "LabyHelper Updater", LabyHelper.Updater.BuildInfo.Version, "Mariano")]
[assembly: MelonGame("Valko Game Studios", "Labyrinthine")]

namespace LabyHelper.Updater;

/// <summary>
/// Keeps Mods\LabyHelper.dll on the latest GitHub release. Runs in OnPreModsLoaded: plugins load first, the mod
/// isn't loaded (or locked) yet, so the file can be swapped in place. Compares the installed DLL's assembly version
/// with the latest release tag (vX.Y.Z) and downloads its "LabyHelper.dll" asset. Any failure (offline, GitHub down,
/// bad download) leaves the installed mod untouched; the whole check is capped at a few seconds.
/// Opt out: create UserData\LabyHelper.noupdate.
/// </summary>
public class Updater : MelonPlugin
{
    private const string Api = "https://api.github.com/repos/nanofives/labyrinthinemod/releases/latest";
    private const string AssetName = "LabyHelper.dll";

    public override void OnPreModsLoaded()
    {
        try { Run(); }
        catch (Exception e) { LoggerInstance.Warning($"Update check failed, keeping the installed version: {e.Message}"); }
    }

    private void Run()
    {
        string userData = MelonEnvironment.UserDataDirectory;
        if (File.Exists(Path.Combine(userData, "LabyHelper.noupdate")))
        {
            LoggerInstance.Msg("Auto-update off (UserData\\LabyHelper.noupdate).");
            return;
        }
        string target = Path.Combine(MelonEnvironment.ModsDirectory, AssetName);
        if (!File.Exists(target)) return; // mod not installed: nothing to update
        Version installed = AssemblyName.GetAssemblyName(target).Version;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LabyHelper-Updater");
        using var doc = JsonDocument.Parse(http.GetStringAsync(Api).GetAwaiter().GetResult());
        var root = doc.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return;
        if (Normalize(latest) <= Normalize(installed))
        {
            LoggerInstance.Msg($"LabyHelper {Short(installed)} is up to date.");
            return;
        }

        string url = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
            if (asset.GetProperty("name").GetString() == AssetName) url = asset.GetProperty("browser_download_url").GetString();
        if (url == null)
        {
            LoggerInstance.Warning($"Release {tag} has no {AssetName}; keeping {Short(installed)}.");
            return;
        }

        string temp = target + ".download";
        http.Timeout = TimeSpan.FromSeconds(30);
        File.WriteAllBytes(temp, http.GetByteArrayAsync(url).GetAwaiter().GetResult());
        Version downloaded = AssemblyName.GetAssemblyName(temp).Version; // throws if it isn't a valid .NET assembly
        if (Normalize(downloaded) != Normalize(latest))
        {
            File.Delete(temp);
            LoggerInstance.Warning($"Downloaded DLL is {Short(downloaded)}, expected {Short(latest)}; keeping {Short(installed)}.");
            return;
        }

        File.Copy(target, Path.Combine(userData, "LabyHelper.dll.bak"), overwrite: true); // outside Mods so it isn't loaded
        File.Move(temp, target, overwrite: true);
        File.WriteAllText(Path.Combine(userData, "LabyHelper.updated.txt"), $"{Short(installed)} {Short(latest)}");
        LoggerInstance.Msg($"Updated LabyHelper {Short(installed)} -> {Short(latest)}.");
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
    private static string Short(Version v) => $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
}
