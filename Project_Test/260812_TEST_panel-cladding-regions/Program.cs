using System.Reflection;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Rhino.Geometry;

namespace PanelCladdingRegionsSmoke;

internal static class Program
{
    private static readonly Guid PanelId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string Pid = "PID_BKT_REGION_01";

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        VerifyRegionResolution(keys);
        VerifySpawnPlanning(keys);
        try
        {
            VerifyGeometryPartition();
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("[SKIP] Rhino Brep geometry probe requires a running Rhino native host");
        }
        VerifyTypeIdentity(keys);
        VerifySurfaceSync(keys);
    }

    private static void VerifyGeometryPartition()
    {
        PanelCladdingKeySet keySet = BuildKeySet("GLS-001", "0A", "GLS-002");
        using Brep panel = Brep.CreateFromCornerPoints(
            new Point3d(0d, 0d, 0d),
            new Point3d(150d, 0d, 0d),
            new Point3d(150d, 100d, 0d),
            new Point3d(0d, 100d, 0d),
            0.001d);
        Assembly assembly = typeof(PanelCladdingSpawnPlanningService).Assembly;
        Type service = assembly.GetType(
            "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingGeometryPartitionService") ??
            throw new InvalidOperationException("Live geometry partition service is missing.");
        MethodInfo createGrid = service.GetMethod("CreateGrid", BindingFlags.Public | BindingFlags.Static) ??
            throw new InvalidOperationException("CreateGrid is missing.");
        object gridResponse = createGrid.Invoke(null, new object[] { panel, keySet, 0.001d }) ??
            throw new InvalidOperationException("CreateGrid returned null.");
        object grid = RequireReflectedData(gridResponse, "Create planar geometry grid");
        using IDisposable disposableGrid = (IDisposable)grid;

        MethodInfo createRegionSurface = service.GetMethod(
            "CreateRegionSurface",
            BindingFlags.Public | BindingFlags.Static) ??
            throw new InvalidOperationException("CreateRegionSurface is missing.");
        object joinResponse = createRegionSurface.Invoke(null, new object[]
        {
            grid,
            keySet.Cells.Take(2).ToArray(),
            0.001d,
            "CID_BKT_REGION_01-0A"
        }) ?? throw new InvalidOperationException("CreateRegionSurface returned null.");
        using Brep joined = (Brep)RequireReflectedData(joinResponse, "Join two atomic cells");
        using AreaMassProperties? joinedArea = AreaMassProperties.Compute(joined);
        Require(joinedArea is not null && Math.Abs(joinedArea.Area - 10_000d) < 0.01d,
            "Joined 0A/1A Brep area is incorrect.");
        Require(joined.Faces.Count == 1,
            $"Parent-linked 0A/1A must be one trimmed face, not {joined.Faces.Count} joined faces.");

        MethodInfo resolveCoverage = service.GetMethod(
            "ResolveCoveredCellLabels",
            BindingFlags.Public | BindingFlags.Static) ??
            throw new InvalidOperationException("ResolveCoveredCellLabels is missing.");
        object coverageResponse = resolveCoverage.Invoke(null, new object[]
        {
            joined,
            grid,
            0.001d,
            "CID_BKT_REGION_01-0A"
        }) ?? throw new InvalidOperationException("ResolveCoveredCellLabels returned null.");
        string[] covered = ((IEnumerable<string>)RequireReflectedData(
            coverageResponse,
            "Resolve joined coverage")).ToArray();
        Require(covered.SequenceEqual(new[] { "0A", "1A" }),
            "Joined Brep footprint did not resolve to 0A and 1A.");

        using Brep partial = Brep.CreateFromCornerPoints(
            new Point3d(0d, 0d, 0d),
            new Point3d(25d, 0d, 0d),
            new Point3d(25d, 100d, 0d),
            new Point3d(0d, 100d, 0d),
            0.001d);
        object partialResponse = resolveCoverage.Invoke(null, new object[]
        {
            partial,
            grid,
            0.001d,
            "partial-surface"
        }) ?? throw new InvalidOperationException("Partial coverage response returned null.");
        bool partialSuccess = (bool)(partialResponse.GetType().GetProperty("Success")?.GetValue(partialResponse) ?? true);
        string partialMessage = partialResponse.GetType().GetProperty("Message")?.GetValue(partialResponse)?.ToString() ?? string.Empty;
        Require(!partialSuccess && (partialMessage.Contains("PARTIAL_CELL", StringComparison.Ordinal) ||
                partialMessage.Contains("AREA_MISMATCH", StringComparison.Ordinal)),
            $"A partial atomic-cell surface must fail coverage validation: {partialMessage}");
        Console.WriteLine("[OK] Rhino Brep partition, single-face region creation, footprint recovery, and partial-cell rejection");
    }

    private static void VerifyRegionResolution(PanelCladdingKeyService keys)
    {
        var regions = new PanelCladdingRegionService(keys);
        PanelCladdingKeySet merged = BuildKeySet("GLS-001", "0A", "GLS-002");
        PanelCladdingRegionSet result = RequireData(
            regions.Resolve(merged.Cells, requirePopulatedCells: true),
            "Resolve merged cells");
        Require(result.Regions.Count == 2, "The three-cell example must resolve to two regions.");
        Require(result.Regions[0].OwnerCellLabel == "0A" &&
                result.Regions[0].MaterialCode == "GLS-001" &&
                result.Regions[0].Cells.Select(cell => cell.ShortLabel).SequenceEqual(new[] { "0A", "1A" }),
            "0A and 1A did not resolve to one GLS-001 region.");
        Require(Value(result, 0) == "GLS-001" && Value(result, 1) == "0A" && Value(result, 2) == "GLS-002",
            "Normalized panel values do not preserve material-owner/reference semantics.");

        PanelCladdingRegionSet separate = RequireData(
            regions.Resolve(BuildKeySet("GLS-001", "GLS-001", "GLS-002").Cells, requirePopulatedCells: true),
            "Resolve separate equal materials");
        Require(separate.Regions.Count == 3,
            "Adjacent equal material values must remain separate regions without a reference.");

        PanelCladdingRegionSet reversedOwner = RequireData(
            regions.Resolve(BuildKeySet("1A", "GLS-001", "GLS-002").Cells, requirePopulatedCells: true),
            "Normalize reversed owner");
        Require(Value(reversedOwner, 0) == "GLS-001" && Value(reversedOwner, 1) == "0A",
            "Equivalent regions must normalize to the lowest-row, lowest-column owner.");

        PanelCladdingRegionSet chain = RequireData(
            regions.Resolve(BuildKeySet("GLS-001", "0A", "1A").Cells, requirePopulatedCells: true),
            "Normalize reference chain");
        Require(chain.Regions.Count == 1 && Value(chain, 1) == "0A" && Value(chain, 2) == "0A",
            "Reference chains must normalize to direct owner references.");

        RequireFailure(regions.Resolve(BuildKeySet("1A", "0A", "GLS-002").Cells), "REFERENCE_CYCLE");
        RequireFailure(regions.Resolve(BuildKeySet("9A", "GLS-001", "GLS-002").Cells), "TARGET_NOT_FOUND");
        RequireFailure(regions.Resolve(BuildKeySet("GLS-001", "STN-001", "0A").Cells), "REGION_DISCONNECTED");
        RequireFailure(regions.Resolve(BuildKeySet(string.Empty, "0A", "GLS-002").Cells), "TARGET_BLANK");
        Console.WriteLine("[OK] owner/reference resolution, canonicalization, and fail-closed validation");
    }

    private static void VerifySpawnPlanning(PanelCladdingKeyService keys)
    {
        var planner = new PanelCladdingSpawnPlanningService(keys);
        PanelCladdingKeySet keySet = BuildKeySet("GLS-001", "0A", "GLS-002");
        PanelCladdingSpawnPlan plan = RequireData(
            planner.CreatePlan(BuildPanelText(keySet), keySet),
            "Create merged spawn plan");
        Require(plan.Regions.Count == 2,
            "GLS-001 / 0A / GLS-002 must plan two Rhino cladding objects.");
        Require(plan.Regions.Select(region => region.Cid).SequenceEqual(new[]
            { "CID_BKT_REGION_01-0A", "CID_BKT_REGION_01-2A" }),
            "Spawn CIDs must use canonical owner suffixes.");
        Require(plan.Regions[0].Cells.Select(cell => cell.ShortLabel).SequenceEqual(new[] { "0A", "1A" }),
            "The first spawn region must include both referenced atomic cells.");

        PanelCladdingKeySet separateKeySet = BuildKeySet("GLS-001", "GLS-001", "GLS-002");
        PanelCladdingSpawnPlan separatePlan = RequireData(
            planner.CreatePlan(BuildPanelText(separateKeySet), separateKeySet),
            "Create separate spawn plan");
        Require(separatePlan.Regions.Count == 3,
            "Equal materials without references must plan separate Rhino objects.");
        Console.WriteLine("[OK] spawn plans one object per normalized region");
    }

    private static void VerifyTypeIdentity(PanelCladdingKeyService keys)
    {
        var signatures = new PanelCladdingTypeSignatureService(keys);
        PanelCladdingLayout layout = BuildLayout(BuildKeySet("GLS-001", "0A", "GLS-002"), 50d, 100d);
        PanelCladdingTypeIdentity merged = RequireData(
            signatures.Create(layout, Values(layout), "WT01"),
            "Create merged identity");
        Require(merged.SchemaVersion == 4 &&
                merged.StoredSignature.StartsWith("v4:sha256:", StringComparison.Ordinal) &&
                merged.CanonicalPayload.Contains("1A:0A;", StringComparison.Ordinal),
            "v4 identity must serialize normalized owner references.");

        PanelCladdingLayout separateLayout = BuildLayout(
            BuildKeySet("GLS-001", "GLS-001", "GLS-002"),
            50d,
            100d);
        PanelCladdingTypeIdentity separate = RequireData(
            signatures.Create(separateLayout, Values(separateLayout), "WT01"),
            "Create separate identity");
        Require(merged.FullDigest != separate.FullDigest,
            "Merged and separate boundaries must not reuse one type identity.");

        PanelCladdingLayout shiftedLayout = BuildLayout(
            BuildKeySet("GLS-001", "0A", "GLS-002", firstOffset: 40d),
            40d,
            100d);
        PanelCladdingTypeIdentity shifted = RequireData(
            signatures.Create(shiftedLayout, Values(shiftedLayout), "WT01"),
            "Create shifted-offset identity");
        Require(merged.FullDigest != shifted.FullDigest,
            "Different candidate-boundary offsets must not reuse one type identity.");
        Console.WriteLine("[OK] v4 signatures distinguish region, extrusion topology, and offset geometry");
    }

    private static void VerifySurfaceSync(PanelCladdingKeyService keys)
    {
        var planner = new PanelCladdingSurfaceSyncPlanningService(keys);
        PanelCladdingLayout layout = BuildLayout(
            BuildKeySet("GLS-001", "GLS-001", "GLS-002"),
            50d,
            100d);
        PanelCladdingSurfaceSyncPlan merged = RequireData(
            planner.CreatePlan(Snapshot(layout, new[]
            {
                SurfaceAtRoot(
                    PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer,
                    "1A",
                    "GLS-001",
                    "0A",
                    "1A"),
                Surface("2A", "GLS-002", "2A")
            })),
            "Plan merged surface sync");
        Require(merged.Panels.Count == 1 && merged.Surfaces.Count == 2,
            "One merged object plus one separate object must produce two surface plans.");
        PanelCladdingSurfaceSyncPanelPlan panel = merged.Panels[0];
        Require(panel.CellValues[CellKey(0)] == "GLS-001" &&
                panel.CellValues[CellKey(1)] == "0A" &&
                panel.CellValues[CellKey(2)] == "GLS-002",
            "Surface coverage did not reconstruct owner/reference panel values.");
        PanelCladdingSurfaceSyncSurfacePlan normalizedSurface = merged.Surfaces[0];
        Require(normalizedSurface.ExpectedCid.EndsWith("-1A", StringComparison.Ordinal) &&
                normalizedSurface.DesiredCid.EndsWith("-0A", StringComparison.Ordinal) &&
                normalizedSurface.CidChanged,
            "Merged surface CID must normalize to its canonical owner.");

        PanelCladdingSurfaceSyncPlan separate = RequireData(
            planner.CreatePlan(Snapshot(layout, new[]
            {
                Surface("0A", "GLS-001", "0A"),
                Surface("1A", "GLS-001", "1A"),
                Surface("2A", "GLS-002", "2A")
            })),
            "Plan separate surface sync");
        Require(separate.Panels[0].CellValues[CellKey(0)] == "GLS-001" &&
                separate.Panels[0].CellValues[CellKey(1)] == "GLS-001",
            "Separate same-material objects must remain separate material owners.");

        PanelCladdingSurfaceSyncPlan exploded = RequireData(
            planner.CreatePlan(Snapshot(layout, new[]
            {
                Surface("0A", "GLS-001", "0A"),
                Surface("0A", "GLS-001", "1A"),
                Surface("2A", "GLS-002", "2A")
            })),
            "Plan exploded duplicate-CID surface sync");
        Require(exploded.Surfaces.Count == 3 && exploded.Surfaces[1].DesiredCid.EndsWith("-1A", StringComparison.Ordinal),
            "Geometry coverage must repair duplicate CIDs inherited by separated surfaces.");

        PanelCladdingSurfaceSyncPlan missingCid = RequireData(
            planner.CreatePlan(Snapshot(layout, new[]
            {
                Surface(string.Empty, "GLS-001", "0A", "1A"),
                Surface("2A", "GLS-002", "2A")
            })),
            "Plan missing-CID normalization");
        Require(missingCid.Surfaces[0].ExpectedCid.Length == 0 &&
                missingCid.Surfaces[0].DesiredCid.EndsWith("-0A", StringComparison.Ordinal) &&
                missingCid.Surfaces[0].CidChanged,
            "Geometry coverage must recover and normalize a missing surface CID.");

        RequireIssue(planner.CreatePlan(Snapshot(layout, new[]
        {
            Surface("0A", "GLS-001", "0A", "1A"),
            Surface("1A", "GLS-002", "1A", "2A")
        })), "CELL_OVERLAP");
        RequireIssue(planner.CreatePlan(Snapshot(layout, new[]
        {
            Surface("0A", "GLS-001", "0A", "2A"),
            Surface("1A", "GLS-002", "1A")
        })), "REGION_DISCONNECTED");
        RequireIssue(planner.CreatePlan(Snapshot(layout, new[]
        {
            Surface("0A", "GLS-001", "0A", "1A")
        })), "SURFACE_MISSING");
        Console.WriteLine("[OK] surface-object coverage reconstructs merges and isolates invalid mappings");
    }

    private static PanelCladdingKeySet BuildKeySet(
        string first,
        string second,
        string third,
        double firstOffset = 50d)
    {
        string[] values = { first, second, third };
        var cells = new List<PanelCladdingCell>();
        for (int column = 0; column < 3; column++)
        {
            cells.Add(new PanelCladdingCell
            {
                Column = column,
                Row = 0,
                RowLabel = "A",
                ShortLabel = $"{column}A",
                UserTextKey = CellKey(column),
                Value = values[column]
            });
        }
        return new PanelCladdingKeySet
        {
            HorizontalOffsets = Array.Empty<double>(),
            VerticalOffsets = new[] { firstOffset, 100d },
            Cells = cells
        };
    }

    private static PanelCladdingLayout BuildLayout(
        PanelCladdingKeySet keySet,
        double firstOffset,
        double secondOffset)
    {
        return new PanelCladdingLayout
        {
            ObjectId = PanelId,
            DocumentPath = "C:\\tests\\panel-cladding-regions.3dm",
            SystemCode = "WT01",
            GeometryFingerprint = "region-fingerprint",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 150d,
            Height = 100d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 1d,
            HorizontalOffsets = Array.Empty<double>(),
            VerticalOffsets = new[] { firstOffset, secondOffset },
            Cells = keySet.Cells
        };
    }

    private static Dictionary<string, string> BuildPanelText(PanelCladdingKeySet keySet)
    {
        var values = new Dictionary<string, string>(Values(keySet.Cells), StringComparer.OrdinalIgnoreCase)
        {
            ["CW_2.04_OFFSET_V0"] = keySet.VerticalOffsets[0].ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["CW_2.04_OFFSET_V1"] = keySet.VerticalOffsets[1].ToString(System.Globalization.CultureInfo.InvariantCulture),
            [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = Pid,
            [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "R1",
            [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01"
        };
        return values;
    }

    private static IReadOnlyDictionary<string, string> Values(PanelCladdingLayout layout) =>
        Values(layout.Cells);

    private static Dictionary<string, string> Values(IReadOnlyList<PanelCladdingCell> cells) =>
        cells.ToDictionary(cell => cell.UserTextKey, cell => cell.Value, StringComparer.OrdinalIgnoreCase);

    private static string Value(PanelCladdingRegionSet set, int column) =>
        set.NormalizedCellValues[CellKey(column)];

    private static string CellKey(int column) => PanelCladdingKeyService.GetCellKey(column, "A");

    private static PanelCladdingSurfaceSyncSnapshot Snapshot(
        PanelCladdingLayout layout,
        IReadOnlyList<PanelCladdingSurfaceSyncSurfaceSnapshot> surfaces)
    {
        return new PanelCladdingSurfaceSyncSnapshot
        {
            DocumentPath = layout.DocumentPath,
            SelectedPanelIds = new[] { layout.ObjectId },
            Panels = new[]
            {
                new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = layout.ObjectId,
                    PanelId = Pid,
                    Layout = layout
                }
            },
            Surfaces = surfaces
        };
    }

    private static PanelCladdingSurfaceSyncSurfaceSnapshot Surface(
        string cidSuffix,
        string material,
        params string[] coveredCells)
    {
        return SurfaceAtRoot(
            PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer,
            cidSuffix,
            material,
            coveredCells);
    }

    private static PanelCladdingSurfaceSyncSurfaceSnapshot SurfaceAtRoot(
        string rootLayer,
        string cidSuffix,
        string material,
        params string[] coveredCells)
    {
        return new PanelCladdingSurfaceSyncSurfaceSnapshot
        {
            ObjectId = Guid.NewGuid(),
            PanelId = Pid,
            Cid = cidSuffix.Length == 0
                ? string.Empty
                : PanelCladdingSpawnPlanningService.BuildSurfaceCid(Pid, cidSuffix),
            LayerPath = $"{rootLayer}::" +
                $"{PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(material)}::{material}",
            CladdingValue = material,
            CoveredCellLabels = coveredCells
        };
    }

    private static T RequireData<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static object RequireReflectedData(object response, string operation)
    {
        bool success = (bool)(response.GetType().GetProperty("Success")?.GetValue(response) ?? false);
        string message = response.GetType().GetProperty("Message")?.GetValue(response)?.ToString() ?? string.Empty;
        object? data = response.GetType().GetProperty("Data")?.GetValue(response);
        if (!success || data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {message}");
        }
        return data;
    }

    private static void RequireFailure<T>(OperationResponse<T> response, string token)
    {
        Require(!response.Success && response.Message.Contains(token, StringComparison.Ordinal),
            $"Expected failure containing {token}, got: {response.Message}");
    }

    private static void RequireIssue(
        OperationResponse<PanelCladdingSurfaceSyncPlan> response,
        string token)
    {
        PanelCladdingSurfaceSyncPlan plan = RequireData(response, $"Create {token} plan");
        Require(plan.Panels.Count == 0 && plan.Issues.Any(issue =>
                issue.Message.Contains(token, StringComparison.Ordinal)),
            $"Expected an isolated panel issue containing {token}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
