using System.Reflection;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Rhino.Geometry;

namespace PanelCladdingSurfaceSyncStructuralGridSmoke;

internal static class Program
{
    private static readonly Guid PanelObjectId =
        Guid.Parse("51000000-0000-0000-0000-000000000001");
    private const string Pid = "PID_STRUCTURAL_GRID_01";
    private const string Material = "MPL-001";

    private static void Main()
    {
        VerifySurfaceScopeUsesAssociatedCurves();
        Console.WriteLine("[OK] PCSyncSrf supplies associated curves to offset inference");

        VerifyLogicalParentReconstruction();
        Console.WriteLine("[OK] spanning cladding reconstructs 0A material plus 1A=0A parent");

        try
        {
            VerifyCurvePreservesStructuralOffset();
            Console.WriteLine("[OK] associated structural curve preserves V0 without splitting cladding");
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("[SKIP] Rhino Brep inference requires a Rhino native host");
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is DllNotFoundException)
        {
            Console.WriteLine("[SKIP] Rhino Brep inference requires a Rhino native host");
        }
    }

    private static void VerifySurfaceScopeUsesAssociatedCurves()
    {
        string root = FindRepositoryRoot();
        string repositoryPath = Path.Combine(
            root,
            "src",
            "PanelCladdingEditor",
            "Infrastructure",
            "Rhino",
            "Live",
            "PanelCladding",
            "LivePanelCladdingSurfaceSyncRepository.cs");
        string source = File.ReadAllText(repositoryPath);
        Require(source.Contains(
                "PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(",
                StringComparison.Ordinal) &&
                source.Contains(
                    "panelCurves.Select(curve => curve.Geometry).ToArray()",
                    StringComparison.Ordinal) &&
                source.Contains("curveInferredData", StringComparison.Ordinal),
            "PCSyncSrf must reconcile associated curve tracks with cladding-surface boundaries.");
        Require(!source.Contains(
                "scope == PanelCladdingObjectScope.Curves\r\n                        ? panelCurves.Select",
                StringComparison.Ordinal) &&
                !source.Contains(
                    "scope == PanelCladdingObjectScope.Curves\n                        ? panelCurves.Select",
                    StringComparison.Ordinal),
            "Associated curves must not be restricted to PCSyncCrv scope.");
        Require(!source.Contains(
                "scope == PanelCladdingObjectScope.Curves &&\r\n                rhinoObject.Geometry is Curve",
                StringComparison.Ordinal) &&
                !source.Contains(
                    "scope == PanelCladdingObjectScope.Curves &&\n                rhinoObject.Geometry is Curve",
                    StringComparison.Ordinal),
            "PCSyncSrf must discover associated structural curves before offset inference.");
        Require(source.Contains(
                "if (rhinoObject.Geometry is Curve curveGeometry &&",
                StringComparison.Ordinal) && source.Contains(
                "PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(layerPath)", StringComparison.Ordinal),
            "The shared sync-object scan must collect extrusion curves in surface scope.");
    }

    private static void VerifyLogicalParentReconstruction()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingKeySet keySet = RequireData(
            keys.CreateKeySet(
                Array.Empty<double>(),
                new[] { 45d },
                new Dictionary<string, string>(),
                90d,
                180d,
                0.001d),
            "Create structural grid");
        var layout = new PanelCladdingLayout
        {
            ObjectId = PanelObjectId,
            DocumentPath = "C:\\tests\\surface-sync-structural-grid.3dm",
            SystemCode = "WT01",
            GeometryFingerprint = "structural-grid-fingerprint",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 90d,
            Height = 180d,
            ModelTolerance = 0.001d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells
        };
        string layerPath = $"{PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer}::" +
            $"{PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(Material)}::{Material}";
        var snapshot = new PanelCladdingSurfaceSyncSnapshot
        {
            Scope = PanelCladdingObjectScope.Surfaces,
            SelectedPanelIds = new[] { PanelObjectId },
            Panels = new[]
            {
                new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = PanelObjectId,
                    PanelId = Pid,
                    Layout = layout,
                    GridChanged = true
                }
            },
            Surfaces = new[]
            {
                new PanelCladdingSurfaceSyncSurfaceSnapshot
                {
                    ObjectId = Guid.Parse("52000000-0000-0000-0000-000000000001"),
                    PanelObjectId = PanelObjectId,
                    PanelId = Pid,
                    Cid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(Pid, "0A"),
                    LayerPath = layerPath,
                    CladdingValue = Material,
                    CoveredCellLabels = new[] { "0A", "1A" }
                }
            }
        };

        PanelCladdingSurfaceSyncPlan plan = RequireData(
            new PanelCladdingSurfaceSyncPlanningService(keys).CreatePlan(snapshot),
            "Plan structural-grid surface sync");
        Require(plan.Issues.Count == 0 && plan.Panels.Count == 1 && plan.Surfaces.Count == 1,
            "The spanning surface must produce one valid panel and one surface plan.");
        PanelCladdingSurfaceSyncPanelPlan panel = plan.Panels[0];
        Require(panel.Layout.VerticalOffsets.SequenceEqual(new[] { 45d }),
            "The planned layout must retain V0.");
        Require(panel.CellValues[PanelCladdingKeyService.GetCellKey(0, "A")] == Material,
            "The first logical cell must own the surface material.");
        Require(panel.CellValues[PanelCladdingKeyService.GetCellKey(1, "A")] == "0A",
            "The second logical cell must point to owner 0A.");
        Require(plan.Surfaces[0].CoveredCellLabels.SequenceEqual(new[] { "0A", "1A" }) &&
                plan.Surfaces[0].DesiredCid ==
                PanelCladdingSpawnPlanningService.BuildSurfaceCid(Pid, "0A"),
            "The spanning cladding must remain one owner-0A surface region.");
    }

    private static void VerifyCurvePreservesStructuralOffset()
    {
        using Brep panel = Rectangle(0d, 0d, 90d, 180d);
        using Brep spanningCladding = Rectangle(0d, 0d, 90d, 180d);
        using Curve structuralMullion = new LineCurve(
            new Point3d(45d, 0d, 0d),
            new Point3d(45d, 180d, 0d));

        Type service = typeof(PanelCladdingSpawnPlanningService).Assembly.GetType(
            "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingGeometryPartitionService") ??
            throw new InvalidOperationException("Live geometry partition service is missing.");
        MethodInfo method = service.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == "InferOffsets" &&
                candidate.GetParameters().Length == 4);
        object response = method.Invoke(null, new object[]
        {
            panel,
            new[] { spanningCladding },
            new[] { structuralMullion },
            0.001d
        }) ?? throw new InvalidOperationException("InferOffsets returned null.");
        bool success = (bool)(response.GetType().GetProperty("Success")?.GetValue(response) ?? false);
        string message = response.GetType().GetProperty("Message")?.GetValue(response)?.ToString() ??
            string.Empty;
        object? data = response.GetType().GetProperty("Data")?.GetValue(response);
        Require(success && data is PanelCladdingInferredOffsets,
            $"Structural-grid inference failed: {message}");
        var offsets = (PanelCladdingInferredOffsets)data!;
        Require(offsets.HorizontalOffsets.Count == 0 &&
                offsets.VerticalOffsets.Count == 1 &&
                Math.Abs(offsets.VerticalOffsets[0] - 45d) < 0.001d,
            "A structural center curve must preserve V0 even when the cladding has no boundary there.");
    }

    private static Brep Rectangle(double left, double bottom, double right, double top) =>
        Brep.CreateFromCornerPoints(
            new Point3d(left, bottom, 0d),
            new Point3d(right, bottom, 0d),
            new Point3d(right, top, 0d),
            new Point3d(left, top, 0d),
            0.001d);

    private static T RequireData<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "PanelCladdingEditor")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root could not be located.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
