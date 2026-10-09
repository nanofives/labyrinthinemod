using System;
using UnityEngine;

namespace LabyHelper;

/// <summary>Two-language UI text. Settings.Language: Auto (system language), Español, English.</summary>
internal static class Loc
{
    public static readonly string[] Languages = { "Auto", "Español", "English" };

    public static bool Spanish
    {
        get
        {
            string lang = Settings.Language?.Value ?? "Auto";
            if (lang.Equals("Español", StringComparison.OrdinalIgnoreCase) || lang.Equals("Spanish", StringComparison.OrdinalIgnoreCase)) return true;
            if (lang.Equals("English", StringComparison.OrdinalIgnoreCase)) return false;
            return Application.systemLanguage == SystemLanguage.Spanish;
        }
    }

    public static string T(string es, string en) => Spanish ? es : en;
}
