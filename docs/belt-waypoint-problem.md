# BepInEx 6 IL2CPP Mod — Belt Waypoint Placement Problem

## Context

I'm making a BepInEx 6 IL2CPP mod for the game "Moon Sweepers" (Unity 6000.6, BepInEx 6.0.0-be.791). The mod copies a rectangular area of buildings/belts into a schematic file and re-places them elsewhere — like a blueprint system.

The game runs on a **spherical moon** (local up = position.normalized). Structures sit on foundations.

## Schematic Format

```json
{
  "Format": 1,
  "Items": [
    { "Index": 0, "BuildKey": "...", "Position": [x,y,z], "Rotation": [x,y,z,w], "IsFoundation": true }
  ],
  "Belts": [
    {
      "Start": [x,y,z],
      "End": [x,y,z],
      "StartDirection": [dx,dy,dz],
      "EndDirection": [dx,dy,dz],
      "Via": [ [x,y,z], [x,y,z] ],
      "ViaDirections": [ [dx,dy,dz], [dx,dy,dz] ],
      "StartConnected": true,
      "EndConnected": true
    }
  ]
}
```

**Via** and **ViaDirections** are waypoints that define the belt's curved path. Without them, the belt is a straight line or gentle curve between Start and End.

---

## Game API (from IL2CPP interop analysis)

### Building a conveyor

```csharp
// Single belt:
MoonWorld.TryBuildConveyor(MoonWalker walker, ConveyorPlan plan, out string message, out int beltId)
// Returns bool (success/failure), message = rejection reason

// Route (multiple segments):
MoonWorld.TryBuildConveyorRoute(MoonWalker walker, IList<ConveyorPlan> plans, out string message, List<int> builtIds)

// Route planner — splits one plan into multiple plans:
MoonWorld.RouteConveyor(MoonWalker walker, ConveyorPlan plan, List<ConveyorPlan> outputPlans)

// Check without building:
MoonWorld.CheckConveyor(MoonWalker walker, ConveyorPlan plan, out string msg, out MoonConveyorPath path, out decimal cost)
MoonWorld.CheckConveyor(MoonWalker walker, ConveyorPlan plan, out string msg, out MoonConveyorPath path, out decimal cost, out bool valid)

// Route validation:
MoonWorld.ConveyorRouteValid(ConveyorPlan plan, MoonConveyorKit kit, Vector3 gravity, out MoonConveyorPath path, out string msg, out bool valid) [private]

// Unify route segments:
MoonWorld.UnifyRoute(List<ConveyorPlan> plans, MoonConveyorKit kit)

// Place multiple belts:
MoonWorld.PlaceBelts(List<ConveyorPlan> plans, bool arg1, bool arg2) [private]
```

### ConveyorPlan struct

```csharp
public struct ConveyorPlan {
    public Vector3 start;
    public Vector3 end;
    public Vector3 startDirection;
    public Vector3 endDirection;
    public Vector3 startUp;
    public Vector3 endUp;
    public int startStructure;  // machine port ID to connect to
    public int endStructure;    // machine port ID to connect to
    public int startBelt;       // existing belt ID to connect to
    public int endBelt;         // existing belt ID to connect to
    // NOTE: NO fields for Via/ViaDirections in ConveyorPlan!
}
```

### MoonConveyorSnapshot (stored belt data)

```csharp
// MoonConveyorSnapshot HAS via data:
MoonConveyorSnapshot.via          // Il2CppStructArray<Vector3> — waypoint positions
MoonConveyorSnapshot.viaDirections // Il2CppStructArray<Vector3> — waypoint directions
MoonConveyorSnapshot.WorldVia(snapshot) // returns Il2CppStructArray<Vector3>
MoonConveyorSnapshot.ReadVia(BinaryReader, snapshot)  // static
MoonConveyorSnapshot.WriteVia(BinaryWriter, snapshot) // static
```

### MoonConveyorPath (belt geometry/rendering)

```csharp
// Constructor WITH via arrays (for rendering):
MoonWorld.ConveyorPath(Vector3 start, Vector3 startDir, Vector3 end, Vector3 endDir,
                       Vector3 gravityPos, Vector3 startUp, Vector3 endUp,
                       Il2CppStructArray<Vector3> via,           // waypoints
                       Il2CppStructArray<Vector3> viaDirections) // waypoint dirs

// Constructor from ConveyorPlan (no via):
MoonWorld.ConveyorPath(ConveyorPlan plan)

// Constructor from snapshot (has via):
MoonWorld.ConveyorPath(MoonConveyorSnapshot snapshot)
```

### Other relevant methods

```csharp
MoonWorld.LiftRoute(IList<RouteLeg> legs, float a, float b, float c, 
                    List<ConveyorPlan> outputPlans, bool d, float e, float f)
MoonWorld.ShiftedBelt(StructurePlan plan, out MoonConveyorPath path, out Ends ends)
MoonWorld.CutBelts(StructurePlan plan, out MoonConveyorPath p1, out Ends e1, 
                   out MoonConveyorPath p2, out Ends e2)
```

---

## THE PROBLEM

When a belt has a **tight turn** (sharp curve), the game needs **waypoints (Via)** to define the path. Without waypoints, `TryBuildConveyor` rejects the belt with: **"Turn too tight: give it more room"**.

### What I tried and why it failed:

#### Attempt 1: Pass via arrays to TryBuildConveyor
`ConveyorPlan` struct does **NOT** have via/viaDirections fields. The only way to build is through `TryBuildConveyor(walker, plan, ...)` which uses `ConveyorPlan` — no way to pass waypoints.

#### Attempt 2: Create Il2CppStructArray for preview rendering
Creating `new Il2CppStructArray<Vector3>(count)` and writing elements via indexer, then passing to `ConveyorPath` constructor — this causes an **uncatchable `AccessViolationException`** crash:

```
AccessViolationException: Attempted to read or write protected memory.
  at Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase`1.get_Length()
  → il2cpp_array_length(nativeArray)
```

This crashes the entire game (Windows and Proton). Cannot be caught with try/catch. This is an IL2CPP interop limitation with struct arrays.

#### Attempt 3: Split belt into straight segments at waypoints
Instead of one belt Start→Via1→Via2→End, build separate short belts: Start→Via1, Via1→Via2, Via2→End.

**Result**: The game places 25 belts instead of 9, creates messy crossing conveyors with free ends. Each segment is treated independently and the game's internal routing creates chaos.

#### Current workaround:
Build each belt as a single conveyor from Start to End, ignoring Via. Works for gentle curves. Game rejects tight turns. User finishes 1-2 sharp-turn belts by hand.

---

## WHAT NEEDS TO BE SOLVED

Find a way to build a conveyor belt WITH waypoints through BepInEx 6 IL2CPP interop.

### Possible approaches to investigate:

#### 1. Use RouteConveyor to auto-split
`RouteConveyor(walker, plan, outputPlans)` takes ONE ConveyorPlan and outputs a List of plans. The game uses this to split a user-drawn belt into proper segments. Then `TryBuildConveyorRoute(walker, plans, ...)` or `PlaceBelts(plans, ...)` builds them all.

**Question**: Does RouteConveyor handle the waypoint routing internally? If we give it start→end, does it figure out the via points on its own? Or does it need the via points as input somehow?

#### 2. Post-build modification of MoonConveyorSnapshot
Build a straight belt first with TryBuildConveyor, then find the resulting `MoonConveyorSnapshot` and modify its `.via` and `.viaDirections` fields directly. The snapshot already has these fields — maybe writing to them after building would adjust the belt's path.

**Question**: Would modifying snapshot.via/viaDirections after building actually change the belt's physical path, or just its stored data? Does the game rebuild geometry from snapshot data?

#### 3. Fix Il2CppStructArray allocation
Maybe there's a safe way to create `Il2CppStructArray<Vector3>`:
- Use `Il2CppStructArray<Vector3>.WrapNativeGenericArrayPointer()` with manually allocated IL2CPP memory via `IL2CPP.il2cpp_array_new(classPtr, count)`
- Use Marshal.AllocHGlobal + manual IL2CPP array memory layout
- Find the actual crash cause — maybe the array class pointer is wrong, or the element size is wrong

#### 4. Use ConveyorPath from snapshot
Build a dummy snapshot with via data, then call `ConveyorPath(snapshot)` to get a path, and use that path somehow to build the actual belt.

#### 5. Call the game's internal build method at a lower level
The game itself builds belts with waypoints. There must be a code path that does it. Perhaps through `PlaceBelts` (which is private but could be called via reflection/HarmonyX), or through a save/load mechanism where we write belt data with via points and trigger a reload.

#### 6. Use MoonWorld.CheckConveyor + ConveyorRouteValid
These methods take a ConveyorPlan and return a MoonConveyorPath. Maybe the returned path already includes auto-calculated waypoints for tight turns? If so, we could extract those waypoints and use approach #2 to set them.

---

## Technical Environment

- **Game**: Moon Sweepers v1.0.6 (Unity 6000.6, IL2CPP backend)
- **Mod framework**: BepInEx 6.0.0-be.791 (IL2CPP branch)
- **Interop**: Il2CppInterop (HarmonyX for patching)
- **Build target**: .NET 6.0 (via Retarget from .NET 10)
- **Namespace**: `MoonCleaners` (game's main namespace)

## Current NativeBuild.cs placement code (simplified)

```csharp
public static class NativePlace
{
    public static (int placed, int total) Place(Schematic sch, Vector3 origin, 
        Quaternion rotation, bool skipRejected)
    {
        var world = MoonWorld.Instance;
        var walker = MoonWalker.Local;
        var up = origin.normalized;
        
        // Place items (structures/foundations) — works fine
        foreach (var item in sch.Items) { /* ... TryBuild ... */ }
        
        // Place belts — THIS IS WHERE THE PROBLEM IS
        var existingBelts = world.BuiltConveyors.ToList();
        var builtBelts = new List<int>();
        
        foreach (var b in sch.Belts)
        {
            var plan = new MoonWorld.ConveyorPlan
            {
                start = World(origin, b.Start),
                end = World(origin, b.End),
                startDirection = Dir(origin, b.StartDirection),
                endDirection = Dir(origin, b.EndDirection),
                startUp = up,
                endUp = up
            };
            
            // Connect to machines or existing belts
            int sm = world.MachineWithPort(plan.start, true);
            int em = world.MachineWithPort(plan.end, false);
            if (sm > 0) plan.startStructure = sm;
            else plan.startBelt = FindBelt(existingBelts, builtBelts, plan.start, true);
            if (em > 0) plan.endStructure = em;
            else plan.endBelt = FindBelt(existingBelts, builtBelts, plan.end, false);
            
            // Build — THIS FAILS for tight turns because no Via data
            if (world.TryBuildConveyor(walker, plan, out string msg, out int id))
            {
                builtBelts.Add(id);
                placed++;
            }
            else if (!skipRejected)
            {
                Log.Warning($"Belt rejected: {msg}");
            }
        }
        
        return (placed, total);
    }
}
```

## What I Need

A working approach to place conveyor belts WITH tight turns (Via + ViaDirections) in a BepInEx 6 IL2CPP mod, without crashing the game. The key insight is that `ConveyorPlan` doesn't have via fields, but `MoonConveyorSnapshot` does, and there's a `RouteConveyor` method that might handle the routing automatically.

---

## REFERENCE: Another mod's belt placement code (MoonPrints — decompiled)

This is the decompiled code from "MoonPrints", another blueprint mod for the same game. Note how it also uses `TryBuildConveyor` with a plain `ConveyorPlan` and does NOT handle Via waypoints. It also doesn't use `RouteConveyor` or `TryBuildConveyorRoute`. This confirms the problem is unsolved in the modding community.

```csharp
// From MoonPrints mod — GamePlacement.cs (decompiled)
// Belt placement section:

Belt[] array2 = Placement.OrderBelts(plan);
foreach (Belt belt2 in array2)
{
    RequireAnchorReach();
    ConveyorPlan val6 = GameAdapter.BeltPlan(belt2, ids);
    HashSet<int> hashSet = (from b in GameAdapter.Belts(world)
        select b.id).ToHashSet();
    
    bool flag2;
    using (BlueprintConveyorReach.Use(world, val6))
    {
        // Simple TryBuildConveyor — no Via, no RouteConveyor
        flag2 = world.TryBuildConveyor(walker, val6, ref text2, ref num4);
    }
    
    if (!flag2)
    {
        // Throws on failure — doesn't skip like our mod does
        throw new InvalidOperationException("Conveyor " + belt2.Id + " placement failed: " + text2);
    }
}
```

## DISCOVERY: Potentially useful game methods NOT used by any mod

From my analysis of Assembly-CSharp.dll, these methods exist but are unused by either mod:

### RouteConveyor — the most promising lead
```
MoonWorld.RouteConveyor(MoonWalker walker, ConveyorPlan plan, List<ConveyorPlan> outputPlans)
```
This takes ONE ConveyorPlan and outputs a LIST of ConveyorPlans. The game likely uses this 
to auto-split a belt drawn by the player into proper segments with waypoints handled internally.

### TryBuildConveyorRoute — builds a route of multiple belts
```
MoonWorld.TryBuildConveyorRoute(MoonWalker walker, IList<ConveyorPlan> plans, out string message, List<int> builtIds)
```
This builds multiple ConveyorPlans as one route. Used AFTER RouteConveyor splits the belt.

### Possible workflow:
1. Create ConveyorPlan with start/end (same as now)
2. Call RouteConveyor(walker, plan, outputPlans) — game auto-splits into proper segments
3. Call TryBuildConveyorRoute(walker, outputPlans, ...) — builds all segments as a connected route

This might handle tight turns correctly because the game's internal RouteConveyor would 
figure out the waypoint routing on its own!
