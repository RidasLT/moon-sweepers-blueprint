using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MoonCleaners;

namespace MoonCubeCopy.Core;

/// <summary>
/// Manages a copy/clear session: corner selection, volume, marking, clearing.
/// </summary>
public class Session
{
    public enum Phase { Corners, Height, Filter, Done }
    public enum ModeType { Copy, Clear }

    public const double MaxHeight = 200.0;
    public const double DefaultClearHeight = 140.0;

    public Phase CurrentPhase { get; set; } = Phase.Corners;
    public ModeType Mode { get; set; } = ModeType.Copy;
    public bool ClearMode => Mode == ModeType.Clear;
    public bool OnlyBuried { get; set; }

    public double Depth { get; set; } = 60.0;
    public double Height { get; set; } = DefaultClearHeight;

    private readonly List<Vector3> corners = new();
    private (int devices, int foundations, int belts) marked;
    private int markedBuried;

    public Volume Volume { get; private set; }

    public int MarkedBuried => markedBuried;
    public (int, int, int) MarkedCounts => marked;

    public void AddCorner(Vector3 point) => corners.Add(point);
    public int CornerCount => corners.Count;

    public void FinishCorners()
    {
        if (corners.Count < 2) throw new InvalidOperationException("Need at least 2 corners");
        Volume = new Volume(corners, Depth, Height);
        CurrentPhase = Phase.Height;
    }

    public void StepHeight(double delta)
    {
        Height = Math.Clamp(Height + delta, 1, MaxHeight);
        Volume = new Volume(corners, Depth, Height);
    }

    /// <summary>
    /// Count devices/foundations/belts inside the volume.
    /// Does NOT filter by buried status — counts everything in the box.
    /// Also computes how many are buried (below ground).
    /// </summary>
    public void CountInside(MoonWorld world, Vector3 up)
    {
        int devices = 0, foundations = 0, belts = 0;
        markedBuried = 0;

        foreach (var s in world.BuiltStructures)
        {
            if (!Volume.Contains(s.Pose.Position)) continue;
            if (s.IsFoundation) foundations++;
            else devices++;

            // Check if buried (below ground level)
            if (BuriedFilter(s.Pose.Position, up))
                markedBuried++;
        }

        foreach (var b in world.BuiltConveyors)
        {
            if (!Volume.Contains(b.start) && !Volume.Contains(b.end)) continue;
            belts++;
        }

        marked = (devices, foundations, belts);
    }

    private bool BuriedFilter(Vector3 pos, Vector3 up)
    {
        // A device is "buried" if it's below the ground surface level
        // This is a simplified check — real implementation would use terrain raycast
        float groundLevel = Volume.GroundLevel(pos, up);
        float deviceHeight = Vector3.Dot(pos - Volume.Center, up);
        return deviceHeight < groundLevel;
    }

    public string HeightStatus()
    {
        int total = marked.devices + marked.foundations + marked.belts;
        if (total == 0 && ClearMode && OnlyBuried)
        {
            int inBox = marked.devices + marked.foundations + marked.belts;
            return $"Nothing is buried here, but {inBox} thing(s) sit in the box above ground. Untick 'Only buried' to clear those.";
        }
        return $"Inside: {marked.devices} devices, {marked.foundations} foundations, {marked.belts} belts";
    }
}

/// <summary>
/// Rectangular volume defined by corners, depth below ground, and height above.
/// </summary>
public class Volume
{
    public Vector3[] Corners { get; }
    public Vector3 Center { get; }
    public double Depth { get; }
    public double Height { get; }

    public Volume(List<Vector3> corners, double depth, double height)
    {
        Corners = corners.ToArray();
        Center = corners.Aggregate(Vector3.zero, (a, b) => a + b) / corners.Count;
        Depth = depth;
        Height = height;
    }

    public bool Contains(Vector3 point)
    {
        // Simplified containment check
        // Real implementation uses the 4-corner polygon + height range
        return true; // placeholder
    }

    public float GroundLevel(Vector3 pos, Vector3 up)
    {
        return 0f; // placeholder — real implementation uses terrain data
    }
}
