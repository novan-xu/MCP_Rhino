using System.Reflection;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using Rhino.Geometry;

namespace PCSpawnSrfBoundaryParentOrderSmoke;

internal static class Program
{
    private const double Tolerance = 0.001d;

    private static void Main()
    {
        VerifyConfiguredBoundaryCancellation();
        VerifyColumnFirstParentOrder();
        VerifyConfiguredMaterialColor();
        VerifySingleFaceNativeGeometryWhenAvailable();
    }

    private static void VerifyConfiguredBoundaryCancellation()
    {
        var boundaryService = new PanelCladdingRegionBoundaryService();
        PanelCladdingCell[] panel = BuildCells(columns: 3, rows: 2);
        PanelCladdingCell[] region = Select(panel, "0A", "1A");
        IReadOnlyList<PanelCladdingRegionBoundarySegment> boundary = RequireData(
            boundaryService.CreateBoundary(panel, region),
            "Create 0A/1A boundary");

        Require(boundary.Count == 6,
            $"Two adjacent cells must expose six atomic boundary segments, found {boundary.Count}.");
        Require(!boundary.Any(segment =>
                (segment.Cell.ShortLabel == "0A" && segment.Side == PanelCladdingCellBoundarySide.Right) ||
                (segment.Cell.ShortLabel == "1A" && segment.Side == PanelCladdingCellBoundarySide.Left)),
            "The shared 0A/1A divider was not cancelled from the configured region boundary.");
        Require(boundary.Count(segment => !segment.IsPanelPerimeter) == 3,
            "The partial 0A/1A region must expose one right and two top internal split segments.");

        PanelCladdingCell[] completePanel = BuildCells(columns: 2, rows: 1);
        IReadOnlyList<PanelCladdingRegionBoundarySegment> completeBoundary = RequireData(
            boundaryService.CreateBoundary(completePanel, completePanel),
            "Create complete-panel boundary");
        Require(completeBoundary.All(segment => segment.IsPanelPerimeter),
            "A complete parent-linked region must require no internal split curves.");
        Console.WriteLine("[OK] configured region boundary cancels shared cell edges and isolates only internal splits");
    }

    private static void VerifyColumnFirstParentOrder()
    {
        PanelCladdingCell[] cells = BuildCells(columns: 2, rows: 4)
            .OrderByDescending(cell => cell.Row)
            .ThenByDescending(cell => cell.Column)
            .ToArray();
        Assembly assembly = typeof(PanelCladdingRegionBoundaryService).Assembly;
        Type windowType = assembly.GetType("PanelCladdingEditor.UI.PanelCladdingEditorWindow") ??
            throw new InvalidOperationException("PanelCladdingEditorWindow is missing.");
        MethodInfo orderMethod = windowType.GetMethod(
            "OrderParentReferenceCells",
            BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException("OrderParentReferenceCells is missing.");
        var ordered = (IEnumerable<PanelCladdingCell>)(orderMethod.Invoke(null, new object[] { cells }) ??
            throw new InvalidOperationException("OrderParentReferenceCells returned null."));
        string[] actual = ordered.Select(cell => cell.ShortLabel).ToArray();
        string[] expected = ["0A", "0B", "0C", "0D", "1A", "1B", "1C", "1D"];
        Require(actual.SequenceEqual(expected),
            $"Parent-reference order was {string.Join(", ", actual)}.");
        Console.WriteLine("[OK] parent-reference choices are column-first: 0A-0D, then 1A-1D");
    }

    private static void VerifyConfiguredMaterialColor()
    {
        var colorService = new PanelCladdingMaterialColorService();
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"pcspawnsrf-material-color-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        string workbookPath = Path.Combine(tempDirectory, "material-setup.xlsx");
        IReadOnlyDictionary<string, PanelColorRgb> colors;
        try
        {
            var repository = new OpenXmlPanelCladdingWorkbookRepository();
            IPreparedPanelCladdingMaterialCatalogUpdate prepared = RequireData(
                repository.PrepareMaterialCatalog(new PanelCladdingMaterialCatalogSaveRequest
                {
                    WorkbookPath = workbookPath,
                    AllowCreate = true,
                    RemoveLegacyTypeSheets = false,
                    Materials =
                    [
                        new PanelCladdingMaterialCatalogItem
                        {
                            Code = "XX",
                            Description = "Configured test material",
                            Category = "Metal",
                            ColorHex = "#123456"
                        }
                    ]
                }),
                "Prepare Material Setup workbook");
            using (prepared)
            {
                Require(prepared.Commit().Success, "Material Setup workbook commit failed.");
            }
            PanelCladdingMaterialCatalog persisted = RequireData(
                repository.ReadMaterialCatalog(workbookPath),
                "Read persisted Material Setup workbook");
            colors = RequireData(
                colorService.CreateColorMap(persisted),
                "Create configured material color map");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
        Require(colors["xx"] == new PanelColorRgb(0x12, 0x34, 0x56),
            "Persisted color parsing must be case-insensitive and preserve exact RGB channels.");

        var keys = new PanelCladdingKeyService();
        PanelCladdingCell[] cells = BuildCells(columns: 2, rows: 1);
        var keySet = new PanelCladdingKeySet
        {
            HorizontalOffsets = Array.Empty<double>(),
            VerticalOffsets = [50d],
            Cells = cells
        };
        PanelCladdingSpawnPlan plan = RequireData(
            new PanelCladdingSpawnPlanningService(keys, colors).CreatePlan(
                new Dictionary<string, string>
                {
                    [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = "PID_COLOR_TEST",
                    [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "R1",
                    [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01"
                },
                keySet),
            "Create configured-color spawn plan");
        Require(plan.Regions.Count == 1 &&
                plan.Regions[0].LayerColor == new PanelColorRgb(0x12, 0x34, 0x56),
            "PCSpawnSrf planning did not use the Material Setup color for XX.");

        PanelColorRgb fallback = PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("XX");
        PanelCladdingSpawnPlan fallbackPlan = RequireData(
            new PanelCladdingSpawnPlanningService(keys).CreatePlan(
                new Dictionary<string, string>
                {
                    [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = "PID_COLOR_FALLBACK",
                    [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "R1",
                    [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01"
                },
                keySet),
            "Create fallback-color spawn plan");
        Require(fallbackPlan.Regions[0].LayerColor == fallback,
            "A material missing from Material Setup must retain the deterministic fallback color.");
        Require(!colorService.ParseHex("#12ZZ56").Success,
            "Invalid Material Setup color text must fail before spawn mutation.");
        Console.WriteLine("[OK] PCSpawnSrf layer colors use exact Material Setup RGB with deterministic fallback");
    }

    private static void VerifySingleFaceNativeGeometryWhenAvailable()
    {
        try
        {
            PanelCladdingCell[] cells = BuildCells(columns: 3, rows: 1);
            var keySet = new PanelCladdingKeySet
            {
                HorizontalOffsets = Array.Empty<double>(),
                VerticalOffsets = [50d, 100d],
                Cells = cells
            };
            using Brep panel = Brep.CreateFromCornerPoints(
                new Point3d(0d, 0d, 0d),
                new Point3d(150d, 0d, 0d),
                new Point3d(150d, 100d, 0d),
                new Point3d(0d, 100d, 0d),
                Tolerance);
            Assembly assembly = typeof(PanelCladdingRegionBoundaryService).Assembly;
            Type service = assembly.GetType(
                "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingGeometryPartitionService") ??
                throw new InvalidOperationException("LivePanelCladdingGeometryPartitionService is missing.");
            MethodInfo createGrid = RequiredMethod(service, "CreateGrid");
            object gridResponse = createGrid.Invoke(null, new object[] { panel, keySet, Tolerance }) ??
                throw new InvalidOperationException("CreateGrid returned null.");
            object grid = RequireReflectedData(gridResponse, "Create geometry grid");
            using IDisposable disposableGrid = (IDisposable)grid;

            MethodInfo createRegion = RequiredMethod(service, "CreateRegionSurface");
            object regionResponse = createRegion.Invoke(null, new object[]
            {
                grid,
                Select(cells, "0A", "1A"),
                Tolerance,
                "CID_BOUNDARY-0A"
            }) ?? throw new InvalidOperationException("CreateRegionSurface returned null.");
            using Brep region = (Brep)RequireReflectedData(regionResponse, "Create 0A/1A surface");
            Require(region.Faces.Count == 1,
                $"0A/1A must be one trimmed face, found {region.Faces.Count} faces.");
            using AreaMassProperties? area = AreaMassProperties.Compute(region);
            Require(area is not null && Math.Abs(area.Area - 10_000d) < 0.01d,
                "0A/1A boundary-trimmed area is incorrect.");

            MethodInfo resolveCoverage = RequiredMethod(service, "ResolveCoveredCellLabels");
            object coverageResponse = resolveCoverage.Invoke(null, new object[]
            {
                region,
                grid,
                Tolerance,
                "CID_BOUNDARY-0A"
            }) ?? throw new InvalidOperationException("ResolveCoveredCellLabels returned null.");
            string[] coverage = ((IEnumerable<string>)RequireReflectedData(
                coverageResponse,
                "Resolve 0A/1A coverage")).ToArray();
            Require(coverage.SequenceEqual(["0A", "1A"]),
                $"Boundary-trimmed surface coverage was {string.Join(", ", coverage)}.");
            Console.WriteLine("[OK] Rhino-native PCSpawnSrf probe returns one exact-coverage face for 0A/1A");
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("[SKIP] Rhino-native single-face probe requires a running Rhino native host");
        }
    }

    private static MethodInfo RequiredMethod(Type type, string name) =>
        type.GetMethod(name, BindingFlags.Public | BindingFlags.Static) ??
        throw new InvalidOperationException($"{name} is missing.");

    private static object RequireReflectedData(object response, string context)
    {
        Type type = response.GetType();
        bool success = (bool)(type.GetProperty("Success")?.GetValue(response) ?? false);
        string message = type.GetProperty("Message")?.GetValue(response)?.ToString() ?? string.Empty;
        object? data = type.GetProperty("Data")?.GetValue(response);
        return success && data is not null
            ? data
            : throw new InvalidOperationException($"{context} failed: {message}");
    }

    private static PanelCladdingCell[] BuildCells(int columns, int rows)
    {
        var cells = new List<PanelCladdingCell>(columns * rows);
        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                string rowLabel = ((char)('A' + row)).ToString();
                cells.Add(new PanelCladdingCell
                {
                    Column = column,
                    Row = row,
                    RowLabel = rowLabel,
                    ShortLabel = $"{column}{rowLabel}",
                    UserTextKey = PanelCladdingKeyService.GetCellKey(column, rowLabel),
                    Value = column == 0 && row == 0 ? "XX" : "0A"
                });
            }
        }
        return cells.ToArray();
    }

    private static PanelCladdingCell[] Select(
        IEnumerable<PanelCladdingCell> cells,
        params string[] labels)
    {
        var requested = labels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        PanelCladdingCell[] selected = cells
            .Where(cell => requested.Contains(cell.ShortLabel))
            .ToArray();
        Require(selected.Length == requested.Count, "A requested fixture cell is missing.");
        return selected;
    }

    private static T RequireData<T>(
        PanelCladdingEditor.Contracts.Responses.OperationResponse<T> response,
        string context) where T : class =>
        response.Success && response.Data is not null
            ? response.Data
            : throw new InvalidOperationException($"{context} failed: {response.Message}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
