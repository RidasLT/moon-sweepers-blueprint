using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MoonCleaners;
using MoonCubeCopy.Core;
using UnityEngine;

namespace MoonCubeCopy.Plugin;

/// <summary>
/// Native placement: takes a Schematic and places devices + belts on EXISTING foundations.
/// Foundations are NOT copied — only devices (machines) and conveyor belts.
///
/// v1.0.39 changes:
/// - FIX (bud3699): Il2CppStructArray uses (long) not (IntPtr) — fixes crash
/// - Simplified: no foundation placement, devices snap to existing foundations
/// - Belt waypoints now work correctly with Via arrays
/// </summary>
public static class NativePlace
{
    private static ManualLogSource Log => Plugin.Log;

    /// <summary>
    /// Place devices and belts from a schematic onto existing foundations.
    /// Foundations in the schematic are skipped — only devices and belts are placed.
    /// </summary>
    /// <param name="sch">The schematic to place.</param>
    /// <param name="origin">World position for the schematic origin.</param>
    /// <param name="rotation">World rotation for the schematic.</param>
    /// <param name="skipRejected">If true, skip rejected items/belts instead of stopping.</param>
    /// <returns>(placed count, skipped foundations, total devices+belts)</returns>
    public static (int placed, int skippedFoundations, int total) Place(
        Schematic sch, Vector3 origin, Quaternion rotation, bool skipRejected)
    {
        var world = MoonWorld.Instance;
        var walker = MoonWalker.Local;
        var up = origin.normalized;

        // Separate foundations from devices
        var devices = sch.Items.Where(i => !i.IsFoundation).ToList();
        int skippedFoundations = sch.Items.Count(i => i.IsFoundation);
        int total = devices.Count + sch.Belts.Count;
        int placed = 0;

        if (skippedFoundations > 0)
            Log.LogInfo($"Skipping {skippedFoundations} foundations (place on existing only)");

        // --- Place devices (non-foundation items) ---
        foreach (var item in devices)
        {
            var worldPos = World(origin, rotation, item.Position);
            var worldRot = rotation * Q(item.Rotation);

            var plan = new StructurePlan
            {
                position = worldPos,
                rotation = worldRot,
                kind = world.BuildKeyToKind(item.BuildKey),
            };

            // Find nearest existing foundation to snap to
            int nearestFoundation = FindNearestFoundation(world, worldPos);
            if (nearestFoundation > 0)
            {
                plan.support = nearestFoundation;
                plan.snapped = true;
            }

            var check = world.BeginStructureCheck(plan);
            while (!check.Done) check.Advance(3.0);

            if (check.Valid)
            {
                string msg = null;
                if (world.TryBuildStructure(walker, plan, check, ref msg))
                {
                    placed++;
                    Log.LogInfo($"Device placed: {item.BuildKey}");
                }
                else
                {
                    Log.LogWarning($"Device rejected: {msg} ({item.BuildKey})");
                    if (!skipRejected) break;
                }
            }
            else
            {
                Log.LogWarning($"Device check failed: {check.Reason} ({item.BuildKey})");
                if (!skipRejected) break;
            }
        }

        // --- Place belts (now with Via waypoint support!) ---
        var existingBelts = world.BuiltConveyors.ToList();
        var builtBelts = new List<int>();

        foreach (var b in sch.Belts)
        {
            var startPos = World(origin, rotation, b.Start);
            var endPos = World(origin, rotation, b.End);

            var plan = new MoonWorld.ConveyorPlan
            {
                start = startPos,
                end = endPos,
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
            // BUILD THE BELT — now with Via waypoint support!
            //
            // FIX (bud3699): Il2CppStructArray<Vector3> must use (long) cast,
            // not (IntPtr). (IntPtr) wraps a pointer at address=count → crash.
            // (long) properly allocates memory → no crash.
            // ============================================================

            bool hasVia = b.Via.Count > 0 && b.Via.Count == b.ViaDirections.Count;

            if (hasVia)
            {
                // Build via arrays with proper memory allocation
                var viaArray = new Il2CppStructArray<Vector3>((long)b.Via.Count);
                var viaDirArray = new Il2CppStructArray<Vector3>((long)b.ViaDirections.Count);

                for (int i = 0; i < b.Via.Count; i++)
                {
                    viaArray[i] = World(origin, rotation, b.Via[i]);
                    viaDirArray[i] = Dir(origin, rotation, b.ViaDirections[i]);
                }

                plan.via = viaArray;
                plan.viaDirections = viaDirArray;
            }

            string msg = null;
            int beltId = 0;
            if (world.TryBuildConveyor(walker, plan, ref msg, ref beltId))
            {
                builtBelts.Add(beltId);
                placed++;
                string viaInfo = hasVia ? $" (with {b.Via.Count} waypoints)" : "";
                Log.LogInfo($"Belt placed OK{viaInfo} (id={beltId})");
            }
            else
            {
                Log.LogWarning($"Belt rejected: {msg}");
                if (hasVia)
                    Log.LogWarning($"  Belt had {b.Via.Count} waypoints");
                if (!skipRejected) break;
            }
        }

        Log.LogInfo($"Placed {placed}/{total} (skipped {skippedFoundations} foundations)");
        return (placed, skippedFoundations, total);
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
    /// Find the nearest existing foundation to a world position.
    /// </summary>
    private static int FindNearestFoundation(MoonWorld world, Vector3 pos)
    {
        float bestDist = 6.0f; // max snap distance
        int bestId = 0;

        foreach (var structure in world.BuiltStructures)
        {
            if (!structure.isFoundation) continue;
            float dist = Vector3.Distance(structure.position, pos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestId = structure.id;
            }
        }

        return bestId;
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
}
