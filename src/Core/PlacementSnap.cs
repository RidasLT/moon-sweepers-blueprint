using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MoonCleaners;

namespace MoonCubeCopy.Core;

/// <summary>
/// Snaps a schematic to the nearest foundation(s) on the moon surface.
/// Pair branch: when schematic has its own foundations, snaps them to field foundations.
/// Single branch: snaps items to nearest foundation.
/// </summary>
public static class PlacementSnap
{
    /// <summary>
    /// Maximum vertical distance (in meters) for same-layer foundation matching.
    /// </summary>
    public const double LayerWindow = 10.0;

    public struct SnapResult
    {
        public Vector3 Origin;
        public Quaternion Rotation;
        public string DiagMessage;
    }

    /// <summary>
    /// Solve placement: find the best origin point for the schematic, snapped to existing foundations.
    /// </summary>
    public static SnapResult Solve(Schematic sch, Vector3 worldPoint, Vector3 aimDirection,
        MoonWorld world, List<MoonDepotSnapshot> fieldFoundations)
    {
        var up = worldPoint.normalized;
        var result = new SnapResult();

        bool hasOwnFoundations = sch.Items.Any(i => i.IsFoundation);

        if (hasOwnFoundations && fieldFoundations.Count > 0)
        {
            // Pair branch: snap schematic's foundations to field foundations
            result = SolvePair(sch, worldPoint, aimDirection, up, world, fieldFoundations);
        }
        else if (fieldFoundations.Count > 0)
        {
            // Single branch: snap to nearest foundation
            result = SolveSingle(sch, worldPoint, aimDirection, up, fieldFoundations);
        }
        else
        {
            // No foundations: place at world point
            result.Origin = worldPoint;
            result.Rotation = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(aimDirection, up), up);
            result.DiagMessage = "no foundations nearby";
        }

        return result;
    }

    private static SnapResult SolvePair(Schematic sch, Vector3 worldPoint, Vector3 aimDir,
        Vector3 up, MoonWorld world, List<MoonDepotSnapshot> fieldFoundations)
    {
        var result = new SnapResult();

        // Find the nearest field foundation horizontally
        MoonDepotSnapshot nearest = default;
        float bestDist = float.MaxValue;

        foreach (var f in fieldFoundations)
        {
            var fp = f.Pose.Position;
            var diff = fp - worldPoint;
            // Horizontal distance only
            var horiz = diff - Vector3.Dot(diff, up) * up;
            float d = horiz.magnitude;
            // Same-layer filter
            if (Math.Abs(Vector3.Dot(diff, up)) > LayerWindow) continue;
            if (d < bestDist)
            {
                bestDist = d;
                nearest = f;
            }
        }

        if (bestDist < 30f)
        {
            var seatPos = nearest.Pose.Position;
            // Vertical correction: seat schematic onto foundation top
            var dy = Vector3.Dot(seatPos - worldPoint, up);
            if (Math.Abs(dy) <= LayerWindow)
            {
                result.Origin = worldPoint + up * dy;
                result.DiagMessage = $"seated on field (dropped {dy:F1} m)";
            }
            else
            {
                // Safety net: use horizontal-only distance
                result.Origin = worldPoint;
                result.DiagMessage = $"pair snap dy={dy:F1} exceeds window, using aim point";
            }
        }
        else
        {
            result.Origin = worldPoint;
            result.DiagMessage = "no field foundation within 30m";
        }

        result.Rotation = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(aimDir, up), up);

        return result;
    }

    private static SnapResult SolveSingle(Schematic sch, Vector3 worldPoint, Vector3 aimDir,
        Vector3 up, List<MoonDepotSnapshot> fieldFoundations)
    {
        var result = new SnapResult();

        MoonDepotSnapshot nearest = default;
        float bestDist = float.MaxValue;
        foreach (var f in fieldFoundations)
        {
            float d = Vector3.Distance(f.Pose.Position, worldPoint);
            if (d < bestDist)
            {
                bestDist = d;
                nearest = f;
            }
        }

        result.Origin = nearest.Pose.Position;
        result.Rotation = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(aimDir, up), up);
        result.DiagMessage = $"snapped to nearest foundation ({bestDist:F1} m)";

        return result;
    }
}
