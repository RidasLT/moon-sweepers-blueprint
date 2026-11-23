using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoonCubeCopy.Core;

public class Schematic
{
    [JsonPropertyName("Format")]
    public int Format { get; set; } = 1;

    [JsonPropertyName("gameBuild")]
    public string GameBuild { get; set; } = "";

    [JsonPropertyName("Items")]
    public List<Item> Items { get; set; } = new();

    [JsonPropertyName("Belts")]
    public List<Belt> Belts { get; set; } = new();

    public static Schematic Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Schematic>(json)
               ?? throw new InvalidDataException("Failed to parse schematic");
    }

    public void Save(string path)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(this, opts);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Normalize the schematic so the base level is at y=0 and centered at origin.
    /// For foundation schematics, uses the most common foundation level as the base.
    /// </summary>
    public Schematic Normalized()
    {
        var copy = JsonSerializer.Deserialize<Schematic>(JsonSerializer.Serialize(this))!;
        if (copy.Items.Count == 0 && copy.Belts.Count == 0) return copy;

        // Use foundations for base level if any exist, else all items
        var basis = copy.Items.Any(i => i.IsFoundation)
            ? copy.Items.Where(i => i.IsFoundation)
            : copy.Items.AsEnumerable();

        if (!basis.Any()) basis = copy.Items;

        // Most common Y level (grouped by 0.05 increments)
        double minY = basis
            .GroupBy(i => Math.Round(i.Position[1] / 0.05))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First()
            .Average(i => i.Position[1]);

        // Center XZ
        var allPositions = copy.Items.Select(i => i.Position)
            .Concat(copy.Belts.Select(b => b.Start))
            .Concat(copy.Belts.Select(b => b.End));

        double cx = allPositions.Average(p => p[0]);
        double cz = allPositions.Average(p => p[2]);

        foreach (var item in copy.Items)
        {
            item.Position[0] -= cx;
            item.Position[1] -= minY;
            item.Position[2] -= cz;
        }

        foreach (var belt in copy.Belts)
        {
            OffsetPoint(belt.Start, cx, minY, cz);
            OffsetPoint(belt.End, cx, minY, cz);
            if (belt.Via != null)
                foreach (var v in belt.Via)
                    OffsetPoint(v, cx, minY, cz);
        }

        return copy;
    }

    private static void OffsetPoint(double[] p, double cx, double cy, double cz)
    {
        p[0] -= cx;
        p[1] -= cy;
        p[2] -= cz;
    }
}

public class Item
{
    [JsonPropertyName("Index")]
    public int Index { get; set; }

    [JsonPropertyName("BuildKey")]
    public string BuildKey { get; set; } = "";

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("Position")]
    public double[] Position { get; set; } = new double[3];

    [JsonPropertyName("Rotation")]
    public double[] Rotation { get; set; } = new double[4];

    [JsonPropertyName("Style")]
    public int Style { get; set; }

    [JsonPropertyName("IsFoundation")]
    public bool IsFoundation { get; set; }

    [JsonPropertyName("SupportIndex")]
    public int SupportIndex { get; set; } = -1;
}

public class Belt
{
    [JsonPropertyName("Start")]
    public double[] Start { get; set; } = new double[3];

    [JsonPropertyName("End")]
    public double[] End { get; set; } = new double[3];

    [JsonPropertyName("StartDirection")]
    public double[] StartDirection { get; set; } = new double[3];

    [JsonPropertyName("EndDirection")]
    public double[] EndDirection { get; set; } = new double[3];

    [JsonPropertyName("Via")]
    public List<double[]> Via { get; set; } = new();

    [JsonPropertyName("ViaDirections")]
    public List<double[]> ViaDirections { get; set; } = new();

    [JsonPropertyName("StartConnected")]
    public bool StartConnected { get; set; }

    [JsonPropertyName("EndConnected")]
    public bool EndConnected { get; set; }
}
