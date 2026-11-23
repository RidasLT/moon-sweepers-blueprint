# Moon Sweepers Blueprint

A **BepInEx 6 IL2CPP** mod for [Moon Sweepers](https://store.steampowered.com/app/2722730/Moon_Sweepers/) that adds a blueprint/schematic system — copy builds, save them to a library, and paste them back with native game placement.

> **Status:** Work in progress (v1.0.38). Belt waypoint placement is a [known open problem](#known-issues).

## Features

- **Copy Area** — Select 4 corner markers to define a region, then capture all structures, foundations, and conveyor belts within it into a schematic
- **Schematic Library** — Save/load `.mcc.json` schematic files with thumbnail previews
- **Native Placement** — Paste schematics using the game's own build API with automatic foundation snapping and belt connection
- **Clear Area** — Remove all structures within a marked region
- **Find Buried** — Locate devices that ended up underground (spherical moon surface issues)
- **Height Status** — UI panel showing device counts, foundation counts, belt counts, and above/buried breakdown
- **Bilingual UI** — English and Lithuanian (EN/LT) interface

## Technical Details

| | |
|---|---|
| **Game** | Moon Sweepers v1.0.6 |
| **Engine** | Unity 6000.6 (IL2CPP) |
| **Mod Framework** | BepInEx 6.0.0-be.791 + HarmonyX |
| **Mod Engine** | ModEngine 1.0.2.0 |
| **Build Target** | .NET 6.0 (via Retarget from .NET 10) |

### Moon = Sphere

The game world is a sphere, not flat. This affects everything:
- **Local "up"** = `position.normalized`
- Foundations must be flat relative to the surface
- Height validation has ~5-6 cm tolerance vs foundation top
- `supportOffset ≈ 3.989` for "Stilt platform - 4 m"

### Schematic Format (`.mcc.json`)

```json
{
  "Format": 1,
  "gameBuild": "1.0.6",
  "Items": [
    {
      "Index": 0,
      "BuildKey": "stilt_platform_4m",
      "Position": [x, y, z],
      "Rotation": [x, y, z, w],
      "IsFoundation": true,
      "SupportIndex": -1
    }
  ],
  "Belts": [
    {
      "Start": [x, y, z],
      "End": [x, y, z],
      "StartDirection": [x, y, z],
      "EndDirection": [x, y, z],
      "Via": [[x, y, z], ...],
      "ViaDirections": [[x, y, z], ...]
    }
  ]
}
```

## Project Structure

```
src/
├── Core/
│   ├── Schematic.cs        # Schematic data model, save/load, normalization
│   ├── PlacementSnap.cs    # Foundation & belt snapping logic
│   └── Session.cs          # Copy/clear session, volume counting
├── Plugin/
│   ├── Plugin.cs           # BepInEx plugin entry point
│   ├── NativeBuild.cs      # ⭐ Game API placement (belt problem is here)
│   ├── GhostModel.cs       # Preview/ghost rendering (belt segments)
│   ├── Controller.cs       # Main workflow controller
│   ├── Markers.cs          # Visual area markers
│   ├── UiPanel.cs          # Height status UI panel
│   └── Loc.cs              # EN/LT translations
├── Tests/
│   └── Program.cs          # Unit tests (13 of 41)
└── docs/
    ├── belt-waypoint-problem.md   # Detailed problem description & API analysis
    └── reconstruction-notes.md    # Notes on code reconstruction accuracy
```

## Known Issues

### Belt Waypoint Placement (Help Wanted! 🔧)

**The core unsolved problem:** Belts with tight turns require waypoints (`Via` array in schematic). Currently, these belts fail to place because:

1. **`ConveyorPlan` has no Via fields** — The game's build API (`TryBuildConveyor`) takes a `ConveyorPlan` with only start/end points, no waypoints
2. **`Il2CppStructArray<Vector3>` crashes** — Constructing IL2CPP native arrays for waypoints causes uncatchable `AccessViolationException`
3. **Segment splitting creates mess** — Breaking a waypoint belt into straight segments produces overlapping/disconnected belts

**Promising unexplored approach:** The game has `RouteConveyor()` and `TryBuildConveyorRoute()` methods that could potentially handle multi-segment belt routing natively. See [`docs/belt-waypoint-problem.md`](docs/belt-waypoint-problem.md) for the full API analysis.

Another blueprint mod ([MoonPrints](https://thunderstore.io/c/moon-sweepers/p/Foundationless/MoonPrints/)) also uses plain `TryBuildConveyor` without waypoints — confirming this is an unsolved problem in the modding community.

## Building

The mod is built with .NET SDK 10 targeting `net10.0`, then retargeted to .NET 6 using a Cecil-based Retarget tool (required for BepInEx IL2CPP runtime compatibility):

```bash
dotnet build -c Release
dotnet $RETARGET_PATH/Retarget.dll bin/Release/net10.0/MoonCubeCopy.dll output/Moon\ Sweepers\ Blueprint.dll /path/to/dotnet6/libs
```

Expected Retarget result: `bad=0`

### Dependencies (not included)

- BepInEx 6.0.0-be.791 (IL2CPP build for Moon Sweepers)
- ModEngine 1.0.2.0
- Game assemblies: `Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`

## Installation

1. Install [BepInEx 6](https://github.com/BepInEx/BepInEx) for Moon Sweepers
2. Place `Moon Sweepers Blueprint.dll` into `BepInEx/plugins/`
3. Launch the game — schematics are saved to `BepInEx/Moon Sweepers Blueprint/`

## Code Reconstruction Note

This source code was **reconstructed from memory** after the original project files were lost. The core logic (`NativeBuild.cs`, `GhostModel.cs`, `Schematic.cs`) is accurate. Some helper files are simplified. Missing files: `Holo.cs`, `Overhead.cs`, `PlacementDiag.cs`, `Capture.cs`, `Library.cs`, `BuriedFilter.cs`, `.csproj`. See [`docs/reconstruction-notes.md`](docs/reconstruction-notes.md) for details.

## Contributing

Contributions welcome, especially for the [belt waypoint problem](#belt-waypoint-placement-help-wanted-)! If you have experience with IL2CPP modding or Moon Sweepers internals, check out the detailed problem description in `docs/belt-waypoint-problem.md`.

## License

MIT

## Author

**Ridas_LT**
