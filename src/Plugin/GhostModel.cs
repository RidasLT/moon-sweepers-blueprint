using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MoonCleaners;
using MoonCubeCopy.Core;
using UnityEngine;

namespace MoonCubeCopy.Plugin;

/// <summary>
/// Creates ghost preview meshes for a schematic — the transparent overlay shown before placing.
///
/// FIX (bud3699): Il2CppStructArray must be constructed with (long) not (IntPtr).
/// (IntPtr) wraps a native pointer at the address = count, causing AccessViolation.
/// (long) properly allocates memory for the array.
/// </summary>
public static class GhostModel
{
    /// <summary>
    /// Build a preview mesh for a single belt, now with proper Via waypoint support.
    /// </summary>
    public static List<Mesh> BeltBar(Belt b, Vector3 origin, Quaternion rotation)
    {
        var meshes = new List<Mesh>();

        var kit = MoonConveyorKit.Load()
                  ?? throw new InvalidOperationException("MoonConveyorKit not loaded");

        Vector3 V3(double[] p) => new((float)p[0], (float)p[1], (float)p[2]);

        var startPos = origin + rotation * V3(b.Start);
        var endPos = origin + rotation * V3(b.End);
        var startDir = (rotation * V3(b.StartDirection)).normalized;
        var endDir = (rotation * V3(b.EndDirection)).normalized;
        var up = origin.normalized;

        bool hasVia = b.Via.Count > 0 && b.Via.Count == b.ViaDirections.Count;

        try
        {
            Il2CppStructArray<Vector3> viaArray = null;
            Il2CppStructArray<Vector3> viaDirArray = null;

            if (hasVia)
            {
                // FIX: use (long) instead of (IntPtr) to properly allocate memory
                viaArray = new Il2CppStructArray<Vector3>((long)b.Via.Count);
                viaDirArray = new Il2CppStructArray<Vector3>((long)b.ViaDirections.Count);

                for (int i = 0; i < b.Via.Count; i++)
                {
                    viaArray[i] = origin + rotation * V3(b.Via[i]);
                    viaDirArray[i] = (rotation * V3(b.ViaDirections[i])).normalized;
                }
            }

            var gravityPos = startPos - up * 10000f;
            var path = new MoonConveyorPath(
                startPos, startDir, endPos, endDir,
                gravityPos, up, up,
                viaArray, viaDirArray);

            var mesh = new Mesh();
            var materials = new List<Material>();
            MoonConveyorMesh.Build(mesh, materials, path, kit,
                default, default, Vector3.zero, false, false, null, null);

            meshes.Add(mesh);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Belt preview failed: {ex.Message}");

            // Fallback: render without via (straight line)
            try
            {
                var gravityPos = startPos - up * 10000f;
                var path = new MoonConveyorPath(
                    startPos, startDir, endPos, endDir,
                    gravityPos, up, up,
                    null, null);

                var mesh = new Mesh();
                var materials = new List<Material>();
                MoonConveyorMesh.Build(mesh, materials, path, kit,
                    default, default, Vector3.zero, false, false, null, null);

                meshes.Add(mesh);
            }
            catch (Exception ex2)
            {
                Plugin.Log.LogWarning($"Belt preview fallback also failed: {ex2.Message}");
            }
        }

        return meshes;
    }

    /// <summary>
    /// Build preview meshes for all belts in a schematic.
    /// </summary>
    public static List<Mesh> AllBelts(Schematic sch, Vector3 origin, Quaternion rotation)
    {
        var allMeshes = new List<Mesh>();
        foreach (var belt in sch.Belts)
        {
            allMeshes.AddRange(BeltBar(belt, origin, rotation));
        }
        return allMeshes;
    }
}
