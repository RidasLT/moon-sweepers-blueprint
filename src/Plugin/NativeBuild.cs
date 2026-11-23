using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using MoonCleaners;
using MoonCubeCopy.Core;
using UnityEngine;

namespace MoonCubeCopy.Plugin;

/// <summary>
/// Native placement: takes a Schematic and places it in the game world using game APIs.
/// </summary>
public static class NativePlace
{
    private static ManualLogSource Log => Plugin.Log;

    /// <summary>
    /// Place all items and belts from a schematic at the given origin.
    /// </summary>
    /// <param name="sch">The schematic to place.</param>
    /// <param name="origin">World position for the schematic origin.</param>
    /// <param name="rotation">World rotation for the schematic.</param>
    /// <param name="skipRejected">If true, skip rejected items/belts instead of stopping.</param>
    /// <returns>(placed count, total count)</returns>
    public static (int placed, int total) Place(Schematic sch, Vector3 origin,
        Quaternion rotation, bool skipRejected)
    {
        var world = MoonWorld.Instance;
        var walker = MoonWalker.Local;
        var up = origin.normalized;

        int placed = 0;
        int total = sch.Items.Count + sch.Belts.Count;

        // --- Place structures/foundations ---
        var builtIds = new Dictionary<int, int>(); // schematic index → game ID
        foreach (var item in sch.Items)
        {
            var worldPos = World(origin, rotation, item.Position);
            var worldRot = rotation * Q(item.Rotation);

            var plan = new StructurePlan
            {
                position = worldPos,
                rotation = worldRot,
                kind = world.BuildKeyToKind(item.BuildKey),
            };

            // Set support (foundation) reference
            if (item.SupportIndex >= 0 && builtIds.ContainsKey(item.SupportIndex))
            {
                plan.support = builtIds[item.SupportIndex];
                plan.snapped = true;
            }

            if (item.Style > 0)
                world.SetFoundationStyle(item.Style);

            var check = world.BeginStructureCheck(plan);
            while (!check.Done) check.Advance(3.0);

            if (check.Valid)
            {
                string msg = null;
                if (world.TryBuildStructure(walker, plan, check, ref msg))
                {
                    // Find newly created structure ID
                    // (simplified — real code tracks IDs before/after)
                    builtIds[item.Index] = FindNewStructureId(world);
                    placed++;
                }
                else
                {
                    Log.LogWarning($"Structure rejected: {msg} ({item.BuildKey})");
                    if (!skipRejected) break;
                }
            }
            else
            {
                Log.LogWarning($"Structure check failed: {check.Reason} ({item.BuildKey})");
                if (!skipRejected) break;
            }
        }

        // --- Place belts ---
        var existingBelts = world.BuiltConveyors.ToList();
        var builtBelts = new List<int>();

        foreach (var b in sch.Belts)
        {
            var plan = new MoonWorld.ConveyorPlan
            {
                start = World(origin, rotation, b.Start),
                end = World(origin, rotation, b.End),
                startDirection = Dir(origin, rotation, b.StartDirection),
                endDirection = Dir(origin, rotation, b.EndDirection),
                startUp = up,
                endUp = up
            };

            // Connect to machines or existing belts at endpoints
            int sm = world.MachineWithPort(plan.start, true);
            int em = world.MachineWithPort(plan.end, false);

            if (sm > 0)
                plan.startStructure = sm;
            else
                plan.startBelt = FindBelt(existingBelts, builtBelts, plan.start, true);

            if (em > 0)
                plan.endStructure = em;
            else
                plan.endBelt = FindBelt(existingBelts, builtBelts, plan.end, false);

            // ============================================================
            // BUILD THE BELT
            // Currently: simple TryBuildConveyor with no Via/waypoints.
            // This FAILS for tight turns — game says "Turn too tight".
            //
            // KNOWN ISSUE: Belts with Via (waypoints) in the schematic
            // cannot be placed faithfully because:
            //   1. ConveyorPlan has no Via fields
            //   2. Il2CppStructArray<Vector3> crashes (AccessViolation)
            //   3. Splitting into segments creates messy overlapping belts
            //
            // POTENTIAL FIX: Use RouteConveyor + TryBuildConveyorRoute
            // See ChatGPT_problema.md for details.
            // ============================================================

            string msg = null;
            int beltId = 0;
            if (world.TryBuildConveyor(walker, plan, ref msg, ref beltId))
            {
                builtBelts.Add(beltId);
                placed++;
                Log.LogInfo($"Belt placed OK (id={beltId})");
            }
            else
            {
                Log.LogWarning($"Belt rejected: {msg}");
                if (b.Via.Count > 0)
                    Log.LogWarning($"  Belt had {b.Via.Count} waypoints — tight turn likely");
                if (!skipRejected) break;
            }
        }

        Log.LogInfo($"Placed {placed}/{total}");
        return (placed, total);
    }

    /// <summary>
    /// Transform a schematic-local position to world position.
    /// </summary>
    private static Vector3 World(Vector3 origin, Quaternion rot, double[] local)
    {
        var v = new Vector3((float)local[0], (float)local[1], (float)local[2]);
        return origin + rot * v;
    }

    /// <summary>
    /// Transform a schematic-local direction to world direction.
    /// </summary>
    private static Vector3 Dir(Vector3 origin, Quaternion rot, double[] local)
    {
        var v = new Vector3((float)local[0], (float)local[1], (float)local[2]);
        return (rot * v).normalized;
    }

    private static Quaternion Q(double[] q)
    {
        return new Quaternion((float)q[0], (float)q[1], (float)q[2], (float)q[3]);
    }

    /// <summary>
    /// Find an existing belt whose start or end is near the given point.
    /// </summary>
    private static int FindBelt(List<MoonConveyorSnapshot> existing,
        List<int> built, Vector3 point, bool isStart)
    {
        float threshold = 0.15f;

        // Check built belts first (from this placement session)
        foreach (int id in built)
        {
            var snap = existing.FirstOrDefault(s => s.id == id);
            if (snap != null)
            {
                Vector3 ep = isStart ? snap.end : snap.start;
                if (Vector3.Distance(ep, point) < threshold)
                    return id;
            }
        }

        // Then check pre-existing belts
        foreach (var snap in existing)
        {
            Vector3 ep = isStart ? snap.end : snap.start;
            if (Vector3.Distance(ep, point) < threshold)
                return snap.id;
        }

        return 0;
    }

    private static int FindNewStructureId(MoonWorld world)
    {
        // Simplified — real implementation tracks IDs before/after build
        return 0;
    }
}
