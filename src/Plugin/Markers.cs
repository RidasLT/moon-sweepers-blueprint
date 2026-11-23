using System.Linq;
using UnityEngine;

namespace MoonCubeCopy.Plugin;

/// <summary>
/// Renders selection/deletion markers — the colored box and fill overlay.
/// </summary>
public static class Markers
{
    private const float fillAlpha = 0.45f;  // Fill transparency (was 0.28)

    /// <summary>
    /// Draw the selection box with fill at ground level.
    /// </summary>
    public static void DrawBox(Vector3[] corners, Vector3 up, float depth, float height,
        Color color, bool isOverhead)
    {
        // Ground-level corners: lift from underground to surface
        var groundCorners = corners.Select(c => c + up * depth).ToArray();

        // Draw fill at ground level
        var fillColor = new Color(color.r, color.g, color.b, fillAlpha);
        DrawQuad(groundCorners, fillColor);

        // Draw outline
        for (int i = 0; i < groundCorners.Length; i++)
        {
            int next = (i + 1) % groundCorners.Length;
            DrawLine(groundCorners[i], groundCorners[next], color);
        }

        // Draw vertical edges (ground to top)
        var topCorners = groundCorners.Select(c => c + up * (float)height).ToArray();
        for (int i = 0; i < groundCorners.Length; i++)
        {
            DrawLine(groundCorners[i], topCorners[i], color * 0.5f);
        }

        // If overhead view, lift fill slightly for visibility
        if (isOverhead)
        {
            // Additional lift in overhead mode
        }
    }

    private static void DrawQuad(Vector3[] corners, Color color)
    {
        // GL drawing for the filled quad — uses Unity GL API
        // Real implementation draws a transparent quad between the 4 corners
    }

    private static void DrawLine(Vector3 a, Vector3 b, Color color)
    {
        // GL drawing for a line — uses Unity GL API
    }
}
