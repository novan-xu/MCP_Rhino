extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;
using MCP_Rhino.Server.Tools.Blocks;
using Line = rhinocommon::Rhino.Geometry.Line;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Transform = rhinocommon::Rhino.Geometry.Transform;
using UnitSystem = rhinocommon::Rhino.UnitSystem;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterStoneSurfaceBakeHandlers()
    {
        _extensionHandlers["stone-surface-bake-smoke-test"] = HandleStoneSurfaceBakeSmokeTest;
    }

    private bool HandleStoneSurfaceBakeSmokeTest(string[] args)
    {
        try
        {
            VerifyStoneSurfaceContract();
            VerifyStoneSurfacePlacement();
            VerifyStoneSurfaceUnits();
            var request = new StoneWireframeBakeRequest
            {
                FilePath = args.Length > 1 ? args[1] : "C:/mcp-rhino/stone-surface-smoke.3dm",
                OutputMode = StoneBakeOutputMode.WorldZSurface, SurfaceHeightInches = 5
            };
            if (McpRhinoPlugin.Instance is null)
            {
                RequireStoneWireframeLiveRequired(_stoneWireframeBakeSkill.Preview(request), "Surface preview");
                RequireStoneWireframeLiveRequired(_stoneWireframeBakeSkill.Apply(request), "Surface apply");
            }
            else
            {
                VerifyStoneSurfaceNativeGeometry();
                var preview = _stoneWireframeBakeSkill.Preview(request);
                RequireStoneWireframe(preview.Success && preview.Data is { FailedCount: 0, StoneOccurrenceCount: > 0 },
                    "Live surface preview failed: " + preview.Message);
                RequireStoneWireframe(preview.Data!.OutputMode == "WorldZSurface" && preview.Data.SurfaceHeightInches == 5,
                    "Live preview lost surface options.");
            }
            Console.WriteLine("[OK] stone-surface-bake: options, placements, units, safety, and live-only routing.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Stone surface bake smoke failed: " + ex);
            Environment.ExitCode = 1;
        }
        return true;
    }

    private static void VerifyStoneSurfaceContract()
    {
        var recording = new SurfaceRecordingOperator();
        var service = new RhinoStoneWireframeBakeService(recording);
        const string path = "C:/mcp-rhino/surface-contract.3dm";
        foreach (bool apply in new[] { false, true })
        {
            var result = apply ? service.Apply(path, null) : service.Preview(path, null);
            RequireStoneWireframe(result.Success && recording.Mode == StoneBakeOutputMode.Curve, "Curve default changed.");
            result = apply ? service.Apply(path, null, StoneBakeOutputMode.WorldZSurface, 5)
                : service.Preview(path, null, StoneBakeOutputMode.WorldZSurface, 5);
            RequireStoneWireframe(result.Success && recording.Mode == StoneBakeOutputMode.WorldZSurface && recording.Height == 5,
                "Surface mode/height did not reach the live adapter.");
            int calls = recording.Calls;
            foreach (double height in new[] { 0d, -5, double.NaN, double.PositiveInfinity })
            {
                result = apply ? service.Apply(path, null, StoneBakeOutputMode.WorldZSurface, height)
                    : service.Preview(path, null, StoneBakeOutputMode.WorldZSurface, height);
                RequireStoneWireframe(!result.Success && recording.Calls == calls, "Invalid height reached live code.");
            }
            result = apply ? service.Apply(path, null, (StoneBakeOutputMode)99, 5)
                : service.Preview(path, null, (StoneBakeOutputMode)99, 5);
            RequireStoneWireframe(!result.Success && recording.Calls == calls, "Invalid output mode reached live code.");
        }
        foreach (var type in new[] { typeof(PreviewStoneWireframeBakeTool), typeof(ApplyStoneWireframeBakeTool) })
        {
            var method = type.GetMethods().Single(m => m.Name.EndsWith("StoneWireframeBake", StringComparison.Ordinal));
            var mode = method.GetParameters().Single(p => p.Name == "outputMode");
            var height = method.GetParameters().Single(p => p.Name == "surfaceHeightInches");
            RequireStoneWireframe(mode.ParameterType == typeof(StoneBakeOutputMode)
                && Equals(mode.DefaultValue, StoneBakeOutputMode.Curve) && Equals(height.DefaultValue, 5d),
                "MCP options or defaults mismatch.");
        }
        VerifyStoneWireframeToolSafety();
    }

    private static void VerifyStoneSurfacePlacement()
    {
        // Slight local edge skew matches a real input class; it must not be flattened.
        var edge = new Line(new Point3d(-7.0625, 0, 0), new Point3d(-0.3125, -0.0000676, 0));
        var translated = Transform.Identity;
        translated.M03 = 193; translated.M13 = -72; translated.M23 = 11;
        var rotated = translated;
        rotated.M00 = 0; rotated.M10 = 1; rotated.M01 = -1; rotated.M11 = 0;
        var mirroredScaled = translated;
        mirroredScaled.M00 = -2; mirroredScaled.M11 = 3; mirroredScaled.M22 = -4;
        var tiltedSheared = translated;
        tiltedSheared.M00 = 0.8; tiltedSheared.M20 = 0.6; tiltedSheared.M01 = 0.3; tiltedSheared.M22 = 7;
        foreach (Transform source in new[] { Transform.Identity, translated, rotated, mirroredScaled, tiltedSheared })
        {
            RequireStoneWireframe(StoneSurfaceBakeGeometry.TryPlacement(edge, source, 0.001, out Transform placed),
                "Valid transformed edge rejected.");
            var expectedStart = edge.From; expectedStart.Transform(source);
            var expectedEnd = edge.To; expectedEnd.Transform(source);
            var actualStart = Point3d.Origin; actualStart.Transform(placed);
            var actualEnd = new Point3d(edge.Length, 0, 0); actualEnd.Transform(placed);
            var topStart = new Point3d(0, 0, 5); topStart.Transform(placed);
            var topEnd = new Point3d(edge.Length, 0, 5); topEnd.Transform(placed);
            RequireStoneWireframe(actualStart.DistanceTo(expectedStart) < 1e-9 && actualEnd.DistanceTo(expectedEnd) < 1e-9,
                "Full nested endpoint placement was not preserved.");
            RequireStoneWireframe(topStart.DistanceTo(new Point3d(expectedStart.X, expectedStart.Y, expectedStart.Z + 5)) < 1e-9
                && topEnd.DistanceTo(new Point3d(expectedEnd.X, expectedEnd.Y, expectedEnd.Z + 5)) < 1e-9,
                "World height inherited a parent scale or rotation.");
        }
        var vertical = new Line(Point3d.Origin, new Point3d(0, 0, 6));
        RequireStoneWireframe(!StoneSurfaceBakeGeometry.TryPlacement(vertical, Transform.Identity, 0.001, out _),
            "Degenerate world-Z edge was accepted.");
        var zero = new Line(Point3d.Origin, Point3d.Origin);
        RequireStoneWireframe(!StoneSurfaceBakeGeometry.TryPlacement(zero, Transform.Identity, 0.001, out _), "Zero edge accepted.");
        var perspective = Transform.Identity; perspective.M30 = 0.2;
        RequireStoneWireframe(!StoneSurfaceBakeGeometry.TryPlacement(edge, perspective, 0.001, out _), "Projective transform accepted.");
        Console.WriteLine("[OK] exact endpoints and world +Z under translation, rotation, mirror, scale, tilt, and shear.");
    }

    private static void VerifyStoneSurfaceUnits()
    {
        var unitCases = McpRhinoPlugin.Instance is null
            ? new[] { (UnitSystem.Inches, 5d) }
            : new[] { (UnitSystem.Inches, 5d), (UnitSystem.Millimeters, 127d), (UnitSystem.Feet, 5d / 12) };
        foreach (var (units, expected) in unitCases)
            RequireStoneWireframe(Math.Abs(StoneSurfaceBakeGeometry.HeightInModelUnits(5, units) - expected) < 1e-9,
                "Incorrect unit conversion for " + units);
        foreach (var units in new[] { UnitSystem.None, UnitSystem.Unset, UnitSystem.CustomUnits, (UnitSystem)200 })
        {
            bool rejected = false;
            try { StoneSurfaceBakeGeometry.HeightInModelUnits(5, units); }
            catch (ArgumentException) { rejected = true; }
            RequireStoneWireframe(rejected, "Unsupported units accepted: " + units);
        }
    }

    private static void VerifyStoneSurfaceNativeGeometry()
    {
        using var surface = StoneSurfaceBakeGeometry.Create(6, 5);
        RequireStoneWireframe(StoneSurfaceBakeGeometry.IsCompatible(surface, 6, 5, 0.001), "Canonical surface not reusable.");
        using var brep = surface.ToBrep();
        RequireStoneWireframe(StoneSurfaceBakeGeometry.IsCompatible(brep, 6, 5, 0.001), "Single-face output not reusable.");
        RequireStoneWireframe(!StoneSurfaceBakeGeometry.IsCompatible(surface, 6, 4, 0.001), "Wrong height reused.");
        using var curve = new rhinocommon::Rhino.Geometry.LineCurve(Point3d.Origin, new Point3d(6, 0, 0));
        RequireStoneWireframe(!StoneSurfaceBakeGeometry.IsCompatible(curve, 6, 5, 0.001), "Curve definition reused as surface.");
    }

    private sealed class SurfaceRecordingOperator : ILiveStoneWireframeBakeOperator
    {
        public int Calls { get; private set; }
        public StoneBakeOutputMode Mode { get; private set; }
        public double Height { get; private set; }
        public OperationResponse<StoneWireframeBakeResponse> Preview(string path, IReadOnlyCollection<string> names,
            StoneBakeOutputMode mode = StoneBakeOutputMode.Curve, double height = 5) => Record(mode, height);
        public OperationResponse<StoneWireframeBakeResponse> Apply(string path, IReadOnlyCollection<string> names,
            StoneBakeOutputMode mode = StoneBakeOutputMode.Curve, double height = 5) => Record(mode, height);
        private OperationResponse<StoneWireframeBakeResponse> Record(StoneBakeOutputMode mode, double height)
        {
            Calls++; Mode = mode; Height = height;
            return OperationResponse<StoneWireframeBakeResponse>.Ok(new());
        }
    }
}
