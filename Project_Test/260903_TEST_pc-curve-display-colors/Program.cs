using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PCCurveDisplayColorsSmoke;

internal static class Program
{
    private static readonly PanelColorRgb Blue = new(0, 0, 255);
    private static readonly PanelColorRgb Purple = new(128, 0, 128);
    private static readonly PanelColorRgb DarkGreen = new(0, 100, 0);

    private static void Main()
    {
        VerifyColorResolver();
        VerifyPlannedCurveColors();
        VerifyExistingColorMismatchDetection();
        VerifyRhinoAttributeWriteContracts();
        VerifyPackageContract();
    }

    private static void VerifyColorResolver()
    {
        Require(PanelCladdingExtrusionPlanningService.ResolveObjectColor(
                PanelCladdingExtrusionCurveKind.Frame,
                PanelCladdingTopologyAxis.Horizontal) == Blue,
            "Horizontal main frame was not Blue.");
        Require(PanelCladdingExtrusionPlanningService.ResolveObjectColor(
                PanelCladdingExtrusionCurveKind.Frame,
                PanelCladdingTopologyAxis.Vertical) == Blue,
            "Vertical main frame was not Blue.");
        Require(PanelCladdingExtrusionPlanningService.ResolveObjectColor(
                PanelCladdingExtrusionCurveKind.Segment,
                PanelCladdingTopologyAxis.Horizontal) == Purple &&
            PanelCladdingExtrusionPlanningService.ResolveObjectColor(
                PanelCladdingExtrusionCurveKind.Merged,
                PanelCladdingTopologyAxis.Horizontal) == Purple,
            "Horizontal intermediate curves were not Purple.");
        Require(PanelCladdingExtrusionPlanningService.ResolveObjectColor(
                PanelCladdingExtrusionCurveKind.Segment,
                PanelCladdingTopologyAxis.Vertical) == DarkGreen &&
            PanelCladdingExtrusionPlanningService.ResolveObjectColor(
                PanelCladdingExtrusionCurveKind.Merged,
                PanelCladdingTopologyAxis.Vertical) == DarkGreen,
            "Vertical intermediate curves were not DarkGreen.");
        Console.WriteLine("[OK] curve kind/axis maps to Blue, Purple, and DarkGreen");
    }

    private static void VerifyPlannedCurveColors()
    {
        PanelCladdingKeySet keySet = Required(
            new PanelCladdingKeyService().CreateKeySet(
                [20d],
                [30d],
                new Dictionary<string, string>(),
                100d,
                80d,
                0.001d),
            "create curve-color grid");
        IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves = Required(
            new PanelCladdingExtrusionPlanningService().CreatePlan(
                "PID_COLOR",
                "CID_COLOR",
                100d,
                80d,
                keySet,
                "01_CW Panels::Surfaces-PNL::WT-COLOR"),
            "plan colored curves");

        Require(curves.Count(curve => curve.Kind == PanelCladdingExtrusionCurveKind.Frame) == 4 &&
                curves.Where(curve => curve.Kind == PanelCladdingExtrusionCurveKind.Frame)
                    .All(curve => curve.ObjectColor == Blue),
            "Not all four planned main-frame curves were Blue.");
        Require(curves.Any(curve => curve.Kind != PanelCladdingExtrusionCurveKind.Frame &&
                                    curve.Axis == PanelCladdingTopologyAxis.Horizontal &&
                                    curve.ObjectColor == Purple),
            "No planned horizontal intermediate curve was Purple.");
        Require(curves.Any(curve => curve.Kind != PanelCladdingExtrusionCurveKind.Frame &&
                                    curve.Axis == PanelCladdingTopologyAxis.Vertical &&
                                    curve.ObjectColor == DarkGreen),
            "No planned vertical intermediate curve was DarkGreen.");
        Console.WriteLine("[OK] extrusion plans carry authoritative object colors");
    }

    private static void VerifyExistingColorMismatchDetection()
    {
        var layerColored = new PanelCladdingSurfaceSyncCurveSnapshot
        {
            UsesObjectColor = false,
            ObjectColor = Blue,
            DesiredObjectColor = Blue
        };
        var wrongRgb = new PanelCladdingSurfaceSyncCurveSnapshot
        {
            UsesObjectColor = true,
            ObjectColor = new PanelColorRgb(1, 2, 3),
            DesiredObjectColor = Purple
        };
        var current = new PanelCladdingSurfaceSyncCurveSnapshot
        {
            UsesObjectColor = true,
            ObjectColor = DarkGreen,
            DesiredObjectColor = DarkGreen
        };
        Require(layerColored.ObjectColorChanged,
            "PCSyncCrv must repair a curve that still inherits layer color.");
        Require(wrongRgb.ObjectColorChanged,
            "PCSyncCrv must repair a wrong existing object RGB value.");
        Require(!current.ObjectColorChanged,
            "PCSyncCrv must not report an already-correct object color as changed.");
        Console.WriteLine("[OK] existing curve color source/RGB mismatch is detectable");
    }

    private static void VerifyRhinoAttributeWriteContracts()
    {
        string root = FindRepositoryRoot();
        string spawn = File.ReadAllText(Path.Combine(
            root, "src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live",
            "PanelCladding", "LivePanelCladdingSpawnService.cs"));
        string update = File.ReadAllText(Path.Combine(
            root, "src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live",
            "PanelCladding", "LivePanelCladdingUpdateService.cs"));
        string sync = File.ReadAllText(Path.Combine(
            root, "src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live",
            "PanelCladding", "LivePanelCladdingSurfaceSyncRepository.cs"));
        string planning = File.ReadAllText(Path.Combine(
            root, "src", "PanelCladdingEditor", "Application", "Services", "PanelCladding",
            "PanelCladdingSurfaceSyncPlanningService.cs"));

        Require(spawn.Contains("item.CurvePlan.ObjectColor.Red", StringComparison.Ordinal) &&
                spawn.Contains("ColorSource = ObjectColorSource.ColorFromObject", StringComparison.Ordinal),
            "PCSpawnCrv does not write planned object color.");
        Require(update.Contains("dependency.CurvePlan.ObjectColor.Red", StringComparison.Ordinal) &&
                update.Contains("ObjectColorSource.ColorFromObject", StringComparison.Ordinal) &&
                update.Contains("ObjectColorSource.ColorFromLayer", StringComparison.Ordinal),
            "PCUpdate does not branch curve/surface color sources correctly.");
        Require(sync.Contains("DesiredObjectColor.Red", StringComparison.Ordinal) &&
                sync.Contains("proposed.ColorSource = ObjectColorSource.ColorFromObject", StringComparison.Ordinal),
            "PCSyncCrv does not commit desired existing-curve color.");
        Require(planning.Contains("curve.ObjectColorChanged", StringComparison.Ordinal),
            "PCSyncCrv planning does not include existing color drift.");
        Console.WriteLine("[OK] spawn, update, and sync write/repair curve object colors");
    }

    private static void VerifyPackageContract()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Packaging",
            "PanelCladdingEditor",
            "package-manifest.json")));
        Require(manifest.RootElement.GetProperty("version").GetString() == "1.0.73",
            "The curve display-color behavior must ship as package 1.0.73.");
        Console.WriteLine("[OK] package version is 1.0.73");
    }

    private static T Required<T>(
        PanelCladdingEditor.Contracts.Responses.OperationResponse<T> response,
        string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
