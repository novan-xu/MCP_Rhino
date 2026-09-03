extern alias rhinocommon;

using System.Reflection;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoCore = rhinocommon::Rhino.Runtime.InProcess.RhinoCore;
using WindowStyle = rhinocommon::Rhino.Runtime.InProcess.WindowStyle;

namespace PCSpawnCrvCurveSimplificationSmoke;

internal static class Program
{
    private const double FitTolerance = 0.01d;

    private static void Main(string[] args)
    {
        VerifyLayerRouting();
        VerifyPlanLayerAssignment();
        if (args.Contains("--rhino-geometry", StringComparer.OrdinalIgnoreCase))
        {
            using var core = new RhinoCore(Environment.GetCommandLineArgs(), WindowStyle.NoWindow);
            VerifyControlPointReduction();
        }
        else
        {
            Console.WriteLine(
                "[SKIP] native curve reduction requires an initialized Rhino runtime; " +
                "pass --rhino-geometry in a supported Rhino test host");
        }
    }

    private static void VerifyLayerRouting()
    {
        string wallTypeLayer = Required(
            PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(
                "01_CW Panels::Surfaces-PNL::WT-04"),
            "resolve WT-04 curve layer");
        Require(
            wallTypeLayer == "02_CW Extrusions::Curves-PNL::WT-04",
            $"Unexpected WT-04 curve layer: {wallTypeLayer}");

        string nestedLayer = Required(
            PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(
                "01_CW Panels::Surfaces-PNL::Tower A::WT-09"),
            "resolve nested curve layer");
        Require(
            nestedLayer == "02_CW Extrusions::Curves-PNL::Tower A::WT-09",
            $"Nested panel layer suffix was not preserved: {nestedLayer}");

        Require(
            !PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(
                "01_CW Panels::Surfaces-PNL").Success,
            "The root-only panel layer must not silently become an untyped curve layer.");
        Require(
            !PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(
                "01_CW Panels::Other::WT-04").Success,
            "An unrelated panel layer must not be accepted.");
        Console.WriteLine("[OK] panel Surfaces-PNL suffix maps to the corresponding Curves-PNL suffix");
    }

    private static void VerifyPlanLayerAssignment()
    {
        var keySet = new PanelCladdingKeySet
        {
            HorizontalOffsets = Array.Empty<double>(),
            VerticalOffsets = Array.Empty<double>(),
            Cells = Array.Empty<PanelCladdingCell>(),
            Topology = new PanelCladdingTopologyState()
        };
        IReadOnlyList<PanelCladdingExtrusionCurvePlan> plan = Required(
            new PanelCladdingExtrusionPlanningService().CreatePlan(
                "PID_TEST",
                "CID_TEST",
                100d,
                80d,
                keySet,
                "01_CW Panels::Surfaces-PNL::WT-04"),
            "plan WT-04 curves");
        Require(plan.Count == 4, $"Expected four frame curves, got {plan.Count}.");
        Require(
            plan.All(item => item.LayerPath == "02_CW Extrusions::Curves-PNL::WT-04"),
            "Not every planned curve received the source panel's corresponding type layer.");
        Console.WriteLine("[OK] every planned curve carries its corresponding Curves-PNL type layer");
    }

    private static void VerifyControlPointReduction()
    {
        Point3d[] points = Enumerable.Range(0, 41)
            .Select(index => new Point3d(
                index * 2.5d,
                Math.Sin(index * Math.PI / 4d) * 0.001d,
                0d))
            .ToArray();
        using Curve source = Curve.CreateInterpolatedCurve(points, 3);
        int sourceCount = ControlPointCount(source);

        Type service = typeof(PanelCladdingExtrusionPlanningService).Assembly.GetType(
            "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingGeometryPartitionService") ??
            throw new InvalidOperationException("Live curve geometry service is missing.");
        MethodInfo simplify = service.GetMethod(
            "SimplifyExtrusionCurve",
            BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Curve simplification entry point is missing.");
        using Curve simplified = (Curve?)simplify.Invoke(null, new object[] { source, FitTolerance }) ??
            throw new InvalidOperationException("Curve simplification returned no geometry.");

        int simplifiedCount = ControlPointCount(simplified);
        Require(
            simplifiedCount < sourceCount,
            $"Control points were not reduced: source={sourceCount}, simplified={simplifiedCount}.");

        double maximumDeviation = 0d;
        for (int index = 0; index <= 100; index++)
        {
            Point3d sample = source.PointAtNormalizedLength(index / 100d);
            Require(simplified.ClosestPoint(sample, out double parameter),
                $"Simplified curve rejected source sample {index}.");
            maximumDeviation = Math.Max(maximumDeviation, sample.DistanceTo(simplified.PointAt(parameter)));
        }
        Require(
            maximumDeviation <= FitTolerance,
            $"Simplified curve exceeded fit tolerance: {maximumDeviation:R} > {FitTolerance:R}.");
        Console.WriteLine(
            $"[OK] curve control points reduced {sourceCount} -> {simplifiedCount}; " +
            $"sampled max deviation={maximumDeviation:R}");
    }

    private static int ControlPointCount(Curve curve)
    {
        using var nurbs = curve.ToNurbsCurve();
        return nurbs.Points.Count;
    }

    private static T Required<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
