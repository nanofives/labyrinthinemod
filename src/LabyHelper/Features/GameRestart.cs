using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using MelonLoader.Utils;
using UnityEngine;

namespace LabyHelper.Features;

/// <summary>
/// F1 > General: "Restart the game" and "Force update". Both close the game and leave a small hidden batch script
/// behind that waits for this process to exit, (for the update) moves the downloaded DLL over Mods\LabyHelper.dll,
/// which is locked while the game runs, and starts the game again through Steam. The forced update downloads the
/// latest release's LabyHelper.dll even when the installed version is the same or auto-update is off, so it also
/// repairs a broken install.
/// </summary>
internal static class GameRestart
{
    private const string Api = "https://api.github.com/repos/nanofives/labyrinthinemod/releases/latest";
    private const string SteamUrl = "steam://rungameid/1302240";

    public static string Status { get; private set; }   // shown under the buttons; null when idle
    public static bool Busy { get; private set; }
    private static volatile string _readyDll;           // downloaded and verified, waiting for the main thread
    private static string _readyVersion;

    public static void Update()
    {
        if (_readyDll == null) return;
        string dll = _readyDll;
        _readyDll = null;
        string mods = Path.Combine(MelonEnvironment.ModsDirectory, "LabyHelper.dll");
        string note = Path.Combine(MelonEnvironment.UserDataDirectory, "LabyHelper.updated.txt");
        // The mod announces "updated itself" on the next start from this file, the same way the updater plugin does.
        Restart($"move /y \"{dll}\" \"{mods}\" >nul\r\n> \"{note}\" echo {BuildInfo.Version} {_readyVersion}\r\n");
    }

    /// <summary>Closes the game and starts it again through Steam.</summary>
    public static void Restart() => Restart("");

    private static void Restart(string beforeLaunch)
    {
        try
        {
            int pid = Process.GetCurrentProcess().Id;
            string script = Path.Combine(MelonEnvironment.UserDataDirectory, "LabyHelper.restart.cmd");
            // Wait up to ~60 s for a clean exit, then force it (the game sometimes hangs on quit with Steam networking).
            File.WriteAllText(script,
                "@echo off\r\n" +
                "set n=0\r\n" +
                ":wait\r\n" +
                $"tasklist /FI \"PID eq {pid}\" /NH | find \"{pid}\" >nul || goto gone\r\n" +
                "set /a n+=1\r\n" +
                $"if %n% GEQ 60 taskkill /F /PID {pid} >nul 2>&1\r\n" +
                "timeout /t 1 /nobreak >nul\r\n" +
                "goto wait\r\n" +
                ":gone\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                beforeLaunch +
                $"start \"\" \"{SteamUrl}\"\r\n" +
                "del \"%~f0\"\r\n");
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            Status = Loc.T("Reiniciando...", "Restarting...");
            Settings.Save();
            Core.Log.Msg("GameRestart: quitting, the game starts again through Steam.");
            Application.Quit();
        }
        catch (Exception e)
        {
            Status = Loc.T($"No se pudo reiniciar: {e.Message}", $"Couldn't restart: {e.Message}");
            Core.Log.Warning($"GameRestart: {e}");
            Busy = false;
        }
    }

    /// <summary>Downloads the latest release's DLL off the main thread, then restarts with it in place.</summary>
    public static void ForceUpdate()
    {
        if (Busy) return;
        Busy = true;
        Status = Loc.T("Buscando la última versión en GitHub...", "Checking GitHub for the latest version...");
        Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("LabyHelper");
                using var doc = JsonDocument.Parse(await http.GetStringAsync(Api));
                string tag = (doc.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
                string url = null;
                foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
                    if (a.GetProperty("name").GetString() == "LabyHelper.dll") url = a.GetProperty("browser_download_url").GetString();
                if (url == null) throw new Exception(Loc.T($"el release {tag} no trae LabyHelper.dll", $"release {tag} has no LabyHelper.dll"));

                Status = Loc.T($"Descargando {tag}...", $"Downloading {tag}...");
                string temp = Path.Combine(MelonEnvironment.UserDataDirectory, "LabyHelper.dll.download");
                File.WriteAllBytes(temp, await http.GetByteArrayAsync(url));
                var v = AssemblyName.GetAssemblyName(temp).Version; // throws if it isn't a valid .NET assembly
                Status = Loc.T($"Listo ({v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}): reiniciando el juego...", $"Ready ({v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}): restarting the game...");
                _readyVersion = $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
                _readyDll = temp;
            }
            catch (Exception e)
            {
                Status = Loc.T($"No se pudo actualizar: {e.Message}", $"Update failed: {e.Message}");
                Busy = false;
            }
        });
    }
}
