using System.Collections.Generic;
using System.Globalization;

namespace MoonCubeCopy.Plugin;

/// <summary>
/// Localization helper — supports EN and LT (Lithuanian).
/// </summary>
public static class Loc
{
    private static string currentLang = "en";

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["Inside: {0} devices · {1} foundations · {2} belts"] = new()
        {
            ["lt"] = "Viduje: {0} įrenginiai · {1} pamatai · {2} juostos"
        },
        ["{0} above ground · {1} buried below"] = new()
        {
            ["lt"] = "{0} virš žemės · {1} užkastų apačioje"
        },
        ["Nothing is buried here, but {0} thing(s) sit in the box above ground. Untick 'Only buried' to clear those."] = new()
        {
            ["lt"] = "Čia nieko neužkasta, bet {0} daiktas(-ai) yra dėžėje virš žemės. Nuimkite „Tik užkasti“, kad išvalytumėte tuos."
        },
        ["Copy"] = new() { ["lt"] = "Kopijuoti" },
        ["Place"] = new() { ["lt"] = "Padėti" },
        ["Clear"] = new() { ["lt"] = "Išvalyti" },
        ["Find buried"] = new() { ["lt"] = "Rasti užkastus" },
        ["Library"] = new() { ["lt"] = "Biblioteka" },
        ["Save"] = new() { ["lt"] = "Išsaugoti" },
        ["Delete"] = new() { ["lt"] = "Ištrinti" },
        ["Cancel"] = new() { ["lt"] = "Atšaukti" },
        ["Height"] = new() { ["lt"] = "Aukštis" },
        ["Depth"] = new() { ["lt"] = "Gylis" },
        ["Only buried"] = new() { ["lt"] = "Tik užkasti" },
    };

    public static void SetLanguage(string lang) => currentLang = lang.ToLowerInvariant();

    public static string T(string key)
    {
        if (currentLang != "en" &&
            Translations.TryGetValue(key, out var dict) &&
            dict.TryGetValue(currentLang, out var translated))
        {
            return translated;
        }
        return key;
    }

    public static string F(string key, params object[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, T(key), args);
    }
}
