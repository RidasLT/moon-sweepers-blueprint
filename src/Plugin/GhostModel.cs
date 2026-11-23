using System;
using System.Collections.Generic;
using MoonCleaners;
using MoonCubeCopy.Core;
using UnityEngine;

namespace MoonCubeCopy.Plugin;

/// <summary>
/// Creates ghost preview meshes for a schematic — the transparent overlay shown before placing.
/// Belt preview uses segment-based rendering to avoid Il2CppStructArray crash.
/// </summary>
public static class GhostModel
{
    /// <summary>
    /// Build a preview mesh for a single belt, rendering via waypoints as separate segments.
    /// Each segment is a straight piece of conveyor mesh — this avoids passing
    /// Il2CppStructArray&lt;Vector3&gt; to MoonConveyorPath which causes AccessViolationException.
    /// </summary>
    public static List<Mesh> BeltBar(Belt b, Vector3 origin, Quaternion rotation)
    {
        var meshes = new List<Mesh>();

        var kit = MoonConveyorKit.Load()
                  ?? throw new InvalidOperationException("MoonConveyorKit not loaded");

        Vector3 V3(double[] p) => new((float)p[0], (float)p[1], (float)p[2]);

        bool hasVia = b.Via.Count > 0 && b.Via.Count == b.ViaDirections.Count;

        // Build list of all points along the belt: start, via1, via2, ..., end
        var pts = new List<Vector3> { V3(b.Start) };
        var dirs = new List<Vector3> { V3(b.StartDirection) };

        if (hasVia)
        {
            for (int vi = 0; vi < b.Via.Count; vi++)
            {
                pts.Add(V3(b.Via[vi]));
                dirs.Add(V3(b.ViaDirections[vi]));
            }
        }

        pts.Add(V3(b.End));
        dirs.Add(V3(b.EndDirection));

        // Render each segment separately — no Via arrays passed to MoonConveyorPath
        for (int k = 0; k + 1 < pts.Count; k++)
        {
            var s = origin + rotation * pts[k];
            var e = origin + rotation * pts[k + 1];
            var sd = (rotation * dirs[k]).normalized;
            var ed = (rotation * dirs[k + 1]).normalized;

            try
            {
                // Create path WITHOUT via (null, null) — safe, no crash
                var gravityPos = s - Vector3.up * 10000f;
                var path = new MoonConveyorPath(s, sd, e, ed,
                    gravityPos, Vector3.up, Vector3.up,
                    null, null); // ← No via arrays! This is the crash fix.

                var mesh = new Mesh();
                var materials = new List<Material>();
                MoonConveyorMesh.Build(mesh, materials, path, kit,
                    default, default, Vector3.zero, false, false, null, null);

                meshes.Add(mesh);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Belt preview segment {k} failed: {ex.Message}");
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
