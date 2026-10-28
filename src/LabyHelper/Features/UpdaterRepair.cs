using System;
using System.IO;
using System.Reflection;
using MelonLoader.Utils;

namespace LabyHelper.Features;

/// <summary>
/// The updater plugin only replaces Mods\LabyHelper.dll, so it can't fix itself. The mod carries the current
/// plugin as an embedded resource and swaps Plugins\LabyHelper.Updater.dll when the installed one is older (the
/// 0.3.7-0.3.12 plugin failed every download). The running plugin is moved aside first (Windows lets a loaded file
/// be renamed), the new one takes effect on the next game start. Only touches an updater that is already installed.
/// </summary>
internal static class UpdaterRepair
{
    private const string Name = "LabyHelper.Updater.dll";

    public static void Run()
    {
        try
        {
            string target = Path.Combine(MelonEnvironment.PluginsDirectory, Name);
            string aside = Path.Combine(MelonEnvironment.UserDataDirectory, Name + ".old");
            if (File.Exists(aside)) { try { File.Delete(aside); } catch { /* still loaded: next time */ } }
            if (!File.Exists(target)) return;

            using var res = Assembly.GetExecutingAssembly().GetManifestResourceStream(Name);
            if (res == null) return;
            using var ms = new MemoryStream();
            res.CopyTo(ms);
            var bytes = ms.ToArray();

            string temp = Path.Combine(MelonEnvironment.UserDataDirectory, Name + ".new");
            File.WriteAllBytes(temp, bytes);
            Version bundled = AssemblyName.GetAssemblyName(temp).Version;
            Version installed = AssemblyName.GetAssemblyName(target).Version;
            if (bundled <= installed) { File.Delete(temp); return; }

            if (File.Exists(aside)) File.Move(aside, aside + "." + DateTime.Now.Ticks); // couldn't delete it above
            File.Move(target, aside);
            File.Move(temp, target);
            Core.Log.Msg($"UpdaterRepair: updater plugin {installed} -> {bundled} (active from the next game start).");
        }
        catch (Exception e) { Core.Log.Warning($"UpdaterRepair: {e.Message}"); }
    }
}
