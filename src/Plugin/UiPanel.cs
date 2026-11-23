using MoonCubeCopy.Core;

namespace MoonCubeCopy.Plugin;

/// <summary>
/// UI panel for the blueprint mod — shows status, counts, and controls.
/// </summary>
public static class UiPanel
{
    private static string lastRefreshKey = "";

    /// <summary>
    /// Build the height-phase panel content.
    /// </summary>
    public static string HeightPanel(Session s)
    {
        var (d, f, b) = s.MarkedCounts;
        int total = d + f + b;

        string text = Loc.F("Inside: {0} devices · {1} foundations · {2} belts", d, f, b);

        if (s.ClearMode)
        {
            int aboveGround = total - s.MarkedBuried;
            text += "\n" + Loc.F("{0} above ground · {1} buried below", aboveGround, s.MarkedBuried);
        }

        return text;
    }

    /// <summary>
    /// Check if the panel needs refreshing.
    /// </summary>
    public static bool NeedsRefresh(Session s)
    {
        var (d, f, b) = s.MarkedCounts;
        string key = $"{d}|{f}|{b}|{s.MarkedBuried}";
        if (key == lastRefreshKey) return false;
        lastRefreshKey = key;
        return true;
    }
}
