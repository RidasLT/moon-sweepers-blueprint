using System;
using System.Collections.Generic;
using System.Linq;
using MoonCleaners;
using MoonCubeCopy.Core;
using UnityEngine;

namespace MoonCubeCopy.Plugin;

/// <summary>
/// Main controller for the blueprint mod.
/// Handles copy/place/clear workflows, UI integration, and game tick updates.
/// </summary>
public class Controller
{
    private Session session;
    private Schematic currentSchematic;
    private bool placing;

    public Session CurrentSession => session;
    public Schematic CurrentSchematic => currentSchematic;
    public bool IsPlacing => placing;

    /// <summary>
    /// Start a new copy session.
    /// </summary>
    public void StartCopy()
    {
        session = new Session { Mode = Session.ModeType.Copy };
    }

    /// <summary>
    /// Start a new clear session.
    /// </summary>
    public void StartClear()
    {
        session = new Session
        {
            Mode = Session.ModeType.Clear,
            Height = Session.DefaultClearHeight // 140m — covers full column automatically
        };
    }

    /// <summary>
    /// Begin placing a schematic.
    /// </summary>
    public void StartPlacing(Schematic sch)
    {
        currentSchematic = sch.Normalized();
        placing = true;
    }

    /// <summary>
    /// Execute placement at the given position.
    /// </summary>
    public (int placed, int total) ExecutePlacement(Vector3 origin, Quaternion rotation)
    {
        if (currentSchematic == null) return (0, 0);

        var result = NativePlace.Place(currentSchematic, origin, rotation, skipRejected: true);
        placing = false;
        return result;
    }

    /// <summary>
    /// Called each game tick when in placing mode.
    /// </summary>
    public void PlacingTick(Vector3 aimPoint, Vector3 aimDirection, MoonWorld world)
    {
        if (!placing || currentSchematic == null) return;

        // Find nearby foundations for snapping
        var foundations = new List<MoonDepotSnapshot>();
        foreach (var s in world.BuiltStructures)
        {
            if (s.IsFoundation && Vector3.Distance(s.Pose.Position, aimPoint) < 50f)
                foundations.Add(s);
        }

        var snap = PlacementSnap.Solve(currentSchematic, aimPoint, aimDirection,
            world, foundations);

        // Update ghost preview position (actual rendering done in Holo/Markers)
        // PlacementDiag logs snap diagnostics
    }

    /// <summary>
    /// Refresh world state for clear mode.
    /// </summary>
    public void RefreshWorld(MoonWorld world)
    {
        if (session == null) return;
        var up = session.Volume?.Center.normalized ?? Vector3.up;
        session.CountInside(world, up);
    }

    /// <summary>
    /// Calculate marker lift for clear/delete markers — at ground level, not underground.
    /// </summary>
    public Vector3[] MarkerLift(Session s, Vector3 up)
    {
        if (s?.Volume == null) return Array.Empty<Vector3>();

        // Corners lifted to ground level (not underground box bottom)
        var q = s.Volume.Corners.Select(c => c + up * (float)s.Depth).ToArray();
        return q;
    }
}
