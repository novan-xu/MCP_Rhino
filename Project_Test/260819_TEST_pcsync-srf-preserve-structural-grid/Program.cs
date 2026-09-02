using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PCSyncSrfPreserveStructuralGridSmoke;

internal static class Program
{
    private static readonly Guid PanelId =
        Guid.Parse("55000000-0000-0000-0000-000000000001");

    private static void Main()
    {
        VerifyCoverageBlindSurfaceScopeClearsStructuralOffsets();
        Console.WriteLine("[OK] coverage-blind surface planning clears geometry-missing stored offsets");

        VerifyCoverageBlindSurfaceScopeUsesCurrentBoundaries();
        Console.WriteLine("[OK] coverage-blind surface planning uses current boundaries exactly");

        VerifyCurveScopeCanRemoveOffsets();
        Console.WriteLine("[OK] PCSyncCrv remains authoritative and can remove stored offsets");

        VerifySpanningSurfaceReconstructsParent();
        Console.WriteLine("[OK] preserved V0 keeps 1A in the grid and reconstructs 1A=0A");

        VerifyLiveRepositoryUsesEffectiveOffsets();
        Console.WriteLine("[OK] live PCSyncSrf grid construction uses the scope-aware effective offsets");
    }

    private static void VerifyCoverageBlindSurfaceScopeClearsStructuralOffsets()
    {
        PanelCladdingInferredOffsets result = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveEffectiveOffsets(
                PanelCladdingObjectScope.Surfaces,
                new[] { 152.20455d, 163.875d },
                new[] { 22.5d },
                new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = new[] { 152.20455d, 163.875d },
                    VerticalOffsets = Array.Empty<double>()
                },
                45d,
                279.125d,
                0.001d),
            "Resolve BKT structural-grid offsets");
        Require(result.HorizontalOffsets.SequenceEqual(new[] { 152.20455d, 163.875d }) &&
                result.VerticalOffsets.Count == 0,
            "Coverage-blind surface scope must clear V0=22.5 when geometry reports no boundary.");
    }

    private static void VerifyCoverageBlindSurfaceScopeUsesCurrentBoundaries()
    {
        PanelCladdingInferredOffsets result = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveEffectiveOffsets(
                PanelCladdingObjectScope.Surfaces,
                new[] { 50d },
                new[] { 22.5d },
                new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = new[] { 50.0005d, 100d },
                    VerticalOffsets = new[] { 22.5005d, 35d }
                },
                90d,
                180d,
                0.001d),
            "Resolve current surface boundaries");
        Require(result.HorizontalOffsets.SequenceEqual(new[] { 50.0005d, 100d }) &&
                result.VerticalOffsets.SequenceEqual(new[] { 22.5005d, 35d }),
            "Current surface boundaries must replace stored values even within tolerance.");
    }

    private static void VerifyCurveScopeCanRemoveOffsets()
    {
        var inferred = new PanelCladdingInferredOffsets
        {
            HorizontalOffsets = new[] { 100d },
            VerticalOffsets = Array.Empty<double>()
        };
        PanelCladdingInferredOffsets result = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveEffectiveOffsets(
                PanelCladdingObjectScope.Curves,
                new[] { 50d, 100d },
                new[] { 22.5d },
                inferred,
                90d,
                180d,
                0.001d),
            "Resolve curve-scope offsets");
        Require(ReferenceEquals(result, inferred) ||
                (result.HorizontalOffsets.SequenceEqual(new[] { 100d }) &&
                 result.VerticalOffsets.Count == 0),
            "PCSyncCrv must continue using only curve-inferred tracks.");
    }

    private static void VerifySpanningSurfaceReconstructsParent()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingKeySet keySet = RequireData(keys.CreateKeySet(
            Array.Empty<double>(),
            new[] { 22.5d },
            new Dictionary<string, string>(),
            45d,
            279.125d,
            0.001d),
            "Create preserved two-column grid");
        var layout = new PanelCladdingLayout
        {
            ObjectId = PanelId,
            DocumentPath = "C:\\tests\\pcsync-preserve-structural-grid.3dm",
            SystemCode = "WT04",
            GeometryFingerprint = "preserved-structural-grid",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 45d,
            Height = 279.125d,
            ModelTolerance = 0.001d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = keySet.Topology
        };
        const string pid = "PID_BKT_N1_01_11";
        const string material = "MPL-001";
        string layer = $"{PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer}::" +
            $"{PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(material)}::{material}";
        var snapshot = new PanelCladdingSurfaceSyncSnapshot
        {
            Scope = PanelCladdingObjectScope.Surfaces,
            SelectedPanelIds = new[] { PanelId },
            Panels = new[]
            {
                new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = PanelId,
                    PanelId = pid,
                    Layout = layout,
                    GridChanged = true
                }
            },
            Surfaces = new[]
            {
                new PanelCladdingSurfaceSyncSurfaceSnapshot
                {
                    ObjectId = Guid.Parse("55000000-0000-0000-0000-000000000002"),
                    PanelObjectId = PanelId,
                    PanelId = pid,
                    Cid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(pid, "0A"),
                    LayerPath = layer,
                    CladdingValue = material,
                    CoveredCellLabels = new[] { "0A", "1A" }
                }
            }
        };
        PanelCladdingSurfaceSyncPlan plan = RequireData(
            new PanelCladdingSurfaceSyncPlanningService(keys).CreatePlan(snapshot),
            "Plan spanning surface on preserved grid");
        PanelCladdingSurfaceSyncPanelPlan panel = plan.Panels.Single();
        Require(panel.CellValues[PanelCladdingKeyService.GetCellKey(0, "A")] == material &&
                panel.CellValues[PanelCladdingKeyService.GetCellKey(1, "A")] == "0A",
            "The preserved two-column grid must produce 1A=0A.");
    }

    private static void VerifyLiveRepositoryUsesEffectiveOffsets()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "PanelCladdingEditor",
            "Infrastructure",
            "Rhino",
            "Live",
            "PanelCladding",
            "LivePanelCladdingSurfaceSyncRepository.cs"));
        Require(source.Contains(
                "PanelCladdingSurfaceSyncPlanningService.ResolveEffectiveOffsets(",
                StringComparison.Ordinal) &&
                source.Contains(
                    "IReadOnlyList<double> verticalOffsets = effectiveOffsets.Data.VerticalOffsets;",
                    StringComparison.Ordinal),
            "The live repository must use effective offsets before key-set and coverage construction.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static T RequireData<T>(OperationResponse<T> response, string operation)
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
