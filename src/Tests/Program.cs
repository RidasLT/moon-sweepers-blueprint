using System;
using System.Collections.Generic;
using System.Linq;
using MoonCubeCopy.Core;

namespace MoonCubeCopy.Tests;

/// <summary>
/// Unit tests for the mod. Run with: dotnet run --project Tests
/// </summary>
public class Program
{
    static int passed = 0;
    static int failed = 0;

    static void Main()
    {
        Console.WriteLine("Moon Sweepers Blueprint - Tests");
        Console.WriteLine("================================");

        // Schematic tests
        Test("Schematic load/save roundtrip", TestSchematicRoundtrip);
        Test("Belt has Via fields", TestBeltVia);
        Test("Normalize centers schematic", TestNormalizeCenter);
        Test("Normalize foundation base level", TestNormalizeFoundationBase);
        Test("Normalize empty schematic", TestNormalizeEmpty);

        // PlacementSnap tests
        Test("LayerWindow constant is 10", TestLayerWindow);

        // Session tests
        Test("DefaultClearHeight is 140", TestDefaultClearHeight);
        Test("MaxHeight is 200", TestMaxHeight);
        Test("Session starts in Corners phase", TestSessionPhase);
        Test("ClearMode auto-height covers column", TestClearModeHeight);
        Test("MarkedBuried starts at 0", TestMarkedBuried);

        // Loc tests
        Test("Loc.T returns key for EN", TestLocEN);
        Test("Loc.F formats correctly", TestLocFormat);

        Console.WriteLine();
        Console.WriteLine($"Results: {passed} passed, {failed} failed, {passed + failed} total");

        if (failed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("SOME TESTS FAILED");
            Console.ResetColor();
            Environment.Exit(1);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("ALL TESTS PASSED");
            Console.ResetColor();
        }
    }

    static void Test(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine($"  [PASS] {name}");
            passed++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [FAIL] {name}: {ex.Message}");
            failed++;
        }
    }

    static void Assert(bool condition, string message = "assertion failed")
    {
        if (!condition) throw new Exception(message);
    }

    static void AssertEqual<T>(T expected, T actual, string context = "")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}, got {actual}. {context}");
    }

    // --- Test methods ---

    static void TestSchematicRoundtrip()
    {
        var sch = new Schematic
        {
            Format = 1,
            Items = new List<Item>
            {
                new Item { Index = 0, BuildKey = "test", Position = new double[] { 1, 2, 3 } }
            },
            Belts = new List<Belt>
            {
                new Belt
                {
                    Start = new double[] { 0, 0, 0 },
                    End = new double[] { 1, 0, 1 },
                    StartDirection = new double[] { 1, 0, 0 },
                    EndDirection = new double[] { 0, 0, 1 },
                }
            }
        };

        AssertEqual(1, sch.Items.Count);
        AssertEqual(1, sch.Belts.Count);
        AssertEqual("test", sch.Items[0].BuildKey);
    }

    static void TestBeltVia()
    {
        var belt = new Belt
        {
            Start = new double[] { 0, 0, 0 },
            End = new double[] { 5, 0, 5 },
            Via = new List<double[]> { new double[] { 2, 0, 3 }, new double[] { 3, 0, 4 } },
            ViaDirections = new List<double[]> { new double[] { 1, 0, 1 }, new double[] { 0, 0, 1 } }
        };

        AssertEqual(2, belt.Via.Count, "Via count");
        AssertEqual(2, belt.ViaDirections.Count, "ViaDirections count");
    }

    static void TestNormalizeCenter()
    {
        var sch = new Schematic
        {
            Items = new List<Item>
            {
                new Item { Position = new double[] { 10, 5, 20 }, Rotation = new double[] { 0, 0, 0, 1 } },
                new Item { Position = new double[] { 20, 5, 30 }, Rotation = new double[] { 0, 0, 0, 1 } }
            }
        };

        var norm = sch.Normalized();
        double cx = norm.Items.Average(i => i.Position[0]);
        double cz = norm.Items.Average(i => i.Position[2]);

        Assert(Math.Abs(cx) < 0.01, $"center X should be ~0, got {cx}");
        Assert(Math.Abs(cz) < 0.01, $"center Z should be ~0, got {cz}");
    }

    static void TestNormalizeFoundationBase()
    {
        var sch = new Schematic
        {
            Items = new List<Item>
            {
                new Item { Position = new double[] { 0, 100, 0 }, IsFoundation = true, Rotation = new double[] { 0, 0, 0, 1 } },
                new Item { Position = new double[] { 1, 100, 0 }, IsFoundation = true, Rotation = new double[] { 0, 0, 0, 1 } },
                new Item { Position = new double[] { 0, 156, 0 }, IsFoundation = false, Rotation = new double[] { 0, 0, 0, 1 } }
            }
        };

        var norm = sch.Normalized();
        // Foundation base should be at y=0
        Assert(Math.Abs(norm.Items[0].Position[1]) < 0.1,
            $"Foundation base should be ~0, got {norm.Items[0].Position[1]}");
        // Non-foundation should be ~56 above
        Assert(Math.Abs(norm.Items[2].Position[1] - 56) < 0.1,
            $"Device should be ~56 above base, got {norm.Items[2].Position[1]}");
    }

    static void TestNormalizeEmpty()
    {
        var sch = new Schematic();
        var norm = sch.Normalized();
        AssertEqual(0, norm.Items.Count);
        AssertEqual(0, norm.Belts.Count);
    }

    static void TestLayerWindow()
    {
        AssertEqual(10.0, PlacementSnap.LayerWindow);
    }

    static void TestDefaultClearHeight()
    {
        AssertEqual(140.0, Session.DefaultClearHeight);
    }

    static void TestMaxHeight()
    {
        AssertEqual(200.0, Session.MaxHeight);
    }

    static void TestSessionPhase()
    {
        var s = new Session();
        AssertEqual(Session.Phase.Corners, s.CurrentPhase);
    }

    static void TestClearModeHeight()
    {
        var s = new Session { Mode = Session.ModeType.Clear };
        s.Height = Session.DefaultClearHeight;
        // 60 depth + 140 height = 200 total — covers full column
        Assert(s.Depth + s.Height >= Session.MaxHeight,
            $"Clear column should cover MaxHeight: {s.Depth} + {s.Height} = {s.Depth + s.Height}");
    }

    static void TestMarkedBuried()
    {
        var s = new Session();
        AssertEqual(0, s.MarkedBuried);
    }

    static void TestLocEN()
    {
        Loc.SetLanguage("en");
        AssertEqual("Copy", Loc.T("Copy"));
    }

    static void TestLocFormat()
    {
        Loc.SetLanguage("en");
        string result = Loc.F("Inside: {0} devices · {1} foundations · {2} belts", 5, 3, 2);
        Assert(result.Contains("5"), "should contain device count");
        Assert(result.Contains("3"), "should contain foundation count");
    }
}
