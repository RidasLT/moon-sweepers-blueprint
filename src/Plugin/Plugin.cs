using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace MoonCubeCopy.Plugin;

[BepInPlugin("com.ridas.moonsweepersblueprint", "Moon Sweepers Blueprint", ModVersion)]
public class Plugin : BasePlugin
{
    public const string ModVersion = "1.0.39";

    internal static ManualLogSource Log { get; private set; } = null!;
    internal static string SchematicDirectory { get; private set; } = null!;

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo($"Moon Sweepers Blueprint v{ModVersion} loading...");

        // Set up schematic directory
        SchematicDirectory = Path.Combine(Paths.BepInExRootPath, "Moon Sweepers Blueprint");
        Directory.CreateDirectory(SchematicDirectory);

        // Migrate old folder name
        MigrateOldFolder();

        // Apply Harmony patches
        var harmony = new Harmony("com.ridas.moonsweepersblueprint");
        harmony.PatchAll();

        Log.LogInfo($"Moon Sweepers Blueprint v{ModVersion} loaded. Schematics in: {SchematicDirectory}");
    }

    /// <summary>
    /// Migrate schematics from old "MoonCubeCopy" folder to "Moon Sweepers Blueprint".
    /// </summary>
    private void MigrateOldFolder()
    {
        string oldDir = Path.Combine(Paths.BepInExRootPath, "MoonCubeCopy");
        if (!Directory.Exists(oldDir)) return;

        try
        {
            foreach (var file in Directory.GetFiles(oldDir, "*.*"))
            {
                string dest = Path.Combine(SchematicDirectory, Path.GetFileName(file));
                if (!File.Exists(dest))
                {
                    File.Move(file, dest);
                    Log.LogInfo($"Migrated: {Path.GetFileName(file)}");
                }
            }

            // Remove old directory if empty
            if (Directory.GetFiles(oldDir).Length == 0 &&
                Directory.GetDirectories(oldDir).Length == 0)
            {
                Directory.Delete(oldDir);
                Log.LogInfo("Removed old MoonCubeCopy folder");
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Migration error: {ex.Message}");
        }
    }
}
