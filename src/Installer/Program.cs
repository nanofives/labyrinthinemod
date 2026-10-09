using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LabyHelperSetup;

/// <summary>
/// One-file installer: finds Labyrinthine through Steam, backs up the saves, and unpacks the embedded payload
/// (MelonLoader + LabyHelper + default config) into the game folder so it is ready to launch from Steam.
/// Usage: LabyHelper-Setup.exe [--uninstall] [--path "D:\...\Labyrinthine"] [--yes]
/// </summary>
internal static class Program
{
    private const string AppId = "1302240";
    private const string GameExe = "Labyrinthine.exe";
    private const string ProcessName = "Labyrinthine";

    // Everything the install puts in the game folder, for uninstall. UserData is kept unless asked.
    private static readonly string[] InstalledFiles = { "version.dll", @"Mods\LabyHelper.dll", "LabyHelper-LEEME.txt" };
    private static readonly string[] InstalledDirs = { "MelonLoader" };

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "LabyHelper - instalador";
        bool uninstall = args.Contains("--uninstall");
        bool yes = args.Contains("--yes");
        string forcedPath = ArgValue(args, "--path");

        if (args.Contains("--detect"))
        {
            Console.WriteLine(FindGameFolder() ?? "(no encontrado)");
            return 0;
        }

        try
        {
            Header();
            string game = forcedPath ?? FindGameFolder() ?? AskForFolder();
            if (game == null || !File.Exists(Path.Combine(game, GameExe)))
            {
                Fail("No encontré Labyrinthine. Pasá la carpeta con --path \"...\\Labyrinthine\".");
                return Pause(2, yes);
            }
            Info($"Juego encontrado en: {game}");

            if (!uninstall && !yes && IsInstalled(game))
            {
                Console.Write("LabyHelper ya está instalado. [A]ctualizar, [D]esinstalar o [C]ancelar? ");
                var key = char.ToUpperInvariant(Console.ReadKey().KeyChar);
                Console.WriteLine();
                if (key == 'D') uninstall = true;
                else if (key != 'A') return Pause(0, yes);
            }

            WaitForGameClosed(yes);

            if (uninstall)
            {
                Uninstall(game, yes);
                Ok("Desinstalado. El juego queda como vino de Steam.");
            }
            else
            {
                BackupSaves();
                Install(game);
                Ok("Listo. Abrí Labyrinthine desde Steam como siempre.");
                Info("La primera vez tarda 1-2 minutos: MelonLoader prepara sus archivos (y baja .NET 6 si falta).");
                Info("Teclas: F1 opciones, F2 colección, F3 pool, F5 levantarse, F6-F8 brillo, F9 cosmético del caso.");
            }
            return Pause(0, yes);
        }
        catch (UnauthorizedAccessException)
        {
            Fail("Sin permiso para escribir en la carpeta del juego. Ejecutá el instalador como administrador.");
            return Pause(3, yes);
        }
        catch (Exception e)
        {
            Fail($"Error: {e.Message}");
            return Pause(1, yes);
        }
    }

    // ---------- locating the game ----------

    private static string FindGameFolder()
    {
        foreach (var steam in SteamRoots())
        {
            foreach (var lib in Libraries(steam))
            {
                var manifest = Path.Combine(lib, "steamapps", $"appmanifest_{AppId}.acf");
                if (!File.Exists(manifest)) continue;
                var m = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                string dir = Path.Combine(lib, "steamapps", "common", m.Success ? m.Groups[1].Value : "Labyrinthine");
                if (File.Exists(Path.Combine(dir, GameExe))) return dir;
            }
        }
        return null;
    }

    private static IEnumerable<string> SteamRoots()
    {
        var found = new List<string>();
        void Add(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            p = Path.GetFullPath(p.Replace('/', '\\'));
            if (Directory.Exists(p) && !found.Contains(p, StringComparer.OrdinalIgnoreCase)) found.Add(p);
        }
        Add(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string);
        Add(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string);
        Add(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string);
        Add(@"C:\Program Files (x86)\Steam");
        return found;
    }

    private static IEnumerable<string> Libraries(string steamRoot)
    {
        yield return steamRoot;
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;
        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
        {
            var lib = m.Groups[1].Value.Replace(@"\\", @"\");
            if (!string.Equals(lib.TrimEnd('\\'), steamRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                yield return lib;
        }
    }

    private static string AskForFolder()
    {
        Info("No encontré el juego automáticamente. Elegí la carpeta de Labyrinthine.");
        using var dlg = new FolderBrowserDialog { Description = "Carpeta de Labyrinthine (la que tiene Labyrinthine.exe)" };
        return dlg.ShowDialog() == DialogResult.OK ? dlg.SelectedPath : null;
    }

    // ---------- install / uninstall ----------

    private static bool IsInstalled(string game) => File.Exists(Path.Combine(game, @"Mods\LabyHelper.dll"));

    private static void Install(string game)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("payload.zip")
                           ?? throw new InvalidOperationException("El instalador no trae el paquete (payload.zip).");
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        int files = 0;
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
            string dest = Path.GetFullPath(Path.Combine(game, entry.FullName));
            if (!dest.StartsWith(Path.GetFullPath(game), StringComparison.OrdinalIgnoreCase)) continue; // zip-slip guard

            // Keep the player's own settings on update.
            bool isConfig = entry.FullName.StartsWith("UserData/", StringComparison.OrdinalIgnoreCase);
            if (isConfig && File.Exists(dest)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            entry.ExtractToFile(dest, overwrite: true);
            files++;
        }
        Info($"{files} archivos copiados.");
    }

    private static void Uninstall(string game, bool yes)
    {
        foreach (var f in InstalledFiles)
        {
            var p = Path.Combine(game, f);
            if (File.Exists(p)) File.Delete(p);
        }
        foreach (var d in InstalledDirs)
        {
            var p = Path.Combine(game, d);
            if (Directory.Exists(p)) Directory.Delete(p, true);
        }
        // Remove the folders MelonLoader creates, only if nothing else lives there.
        foreach (var d in new[] { "Mods", "Plugins", "UserLibs" })
        {
            var p = Path.Combine(game, d);
            if (Directory.Exists(p) && !Directory.EnumerateFileSystemEntries(p).Any()) Directory.Delete(p);
        }
        var userData = Path.Combine(game, "UserData");
        if (Directory.Exists(userData))
        {
            bool remove = yes;
            if (!yes)
            {
                Console.Write("¿Borrar también la configuración (UserData)? [s/N] ");
                remove = char.ToLowerInvariant(Console.ReadKey().KeyChar) == 's';
                Console.WriteLine();
            }
            if (remove) Directory.Delete(userData, true);
        }
    }

    private static void BackupSaves()
    {
        var saves = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            @"AppData\LocalLow\Valko Game Studios\Labyrinthine");
        if (!Directory.Exists(saves)) return;
        var dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LabyHelper backups", $"saves_{DateTime.Now:yyyy-MM-dd_HHmmss}");
        CopyDir(saves, dest);
        Info($"Backup de tus partidas en: {dest}");
    }

    private static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    private static void WaitForGameClosed(bool yes)
    {
        while (Process.GetProcessesByName(ProcessName).Length > 0)
        {
            if (yes) throw new InvalidOperationException("Labyrinthine está abierto. Cerralo y volvé a ejecutar.");
            Console.Write("Labyrinthine está abierto. Cerralo y apretá Enter para seguir...");
            Console.ReadLine();
        }
    }

    // ---------- console helpers ----------

    private static string ArgValue(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static void Header()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"LabyHelper {typeof(Program).Assembly.GetName().Version.ToString(3)} para Labyrinthine (MelonLoader 0.7.3)");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void Info(string s) => Console.WriteLine("  " + s);

    private static void Ok(string s)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n" + s);
        Console.ResetColor();
    }

    private static void Fail(string s)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n" + s);
        Console.ResetColor();
    }

    private static int Pause(int code, bool yes)
    {
        if (!yes)
        {
            Console.WriteLine("\nApretá una tecla para cerrar.");
            Console.ReadKey(true);
        }
        return code;
    }
}
