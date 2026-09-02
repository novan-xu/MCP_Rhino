using System.Reflection;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Rhino.Geometry;

namespace PanelCladdingOffsetSyncSmoke;

internal static class Program
{
    private static readonly Guid PanelId = Guid.Parse("A1000000-0000-0000-0000-000000000001");
    private const string Pid = "PID_OFFSET_SYNC_01";

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        VerifyStepLayerContract(keys);
        VerifyCanonicalGrid(keys);
        VerifyOffsetPrecision(keys);
        VerifyGridChangePlanning(keys);
        try
        {
            VerifyGeometryInference();
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("[SKIP] Rhino Brep offset inference requires a running Rhino native host");
        }
    }

    private static void VerifyStepLayerContract(PanelCladdingKeyService keys)
    {
        const string expectedRoot = "04_STEP Surfaces";
        Require(PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer == expectedRoot,
            "Spawn root must use the STEP layer name.");
        Require(PanelCladdingSpawnPlanningService.IsSupportedMaterialSurfaceRoot(expectedRoot),
            "STEP root must be accepted.");
        Require(PanelCladdingSpawnPlanningService.IsSupportedMaterialSurfaceRoot(
                "03_Material Surfaces (STEP)"),
            "The exact legacy STEP root must remain readable for migration.");
        Require(!PanelCladdingSpawnPlanningService.IsSupportedMaterialSurfaceRoot("02_Material Surfaces") &&
                !PanelCladdingSpawnPlanningService.IsSupportedMaterialSurfaceRoot("03_Material Surfaces (STP)"),
            "Unrecognized material roots must not be accepted.");

        PanelCladdingKeySet keySet = RequireData(keys.CreateKeySet(
            Array.Empty<double>(),
            new[] { 60d },
            new Dictionary<string, string>
            {
                [PanelCladdingKeyService.GetCellKey(0, "A")] = "GLS-001",
                [PanelCladdingKeyService.GetCellKey(1, "A")] = "GLS-002"
            },
            120d,
            80d,
            0.001d), "Create spawn key set");
        PanelCladdingSpawnPlan plan = RequireData(
            new PanelCladdingSpawnPlanningService(keys).CreatePlan(
                new Dictionary<string, string>
                {
                    [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = Pid,
                    [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "R1",
                    [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01"
                },
                keySet),
            "Create STEP spawn plan");
        Require(plan.Regions.All(region => region.LayerPath.StartsWith(
                expectedRoot + "::",
                StringComparison.Ordinal)),
            "Every spawned cladding region must be under the STEP root.");
        Console.WriteLine("[OK] 04_STEP Surfaces is the spawn root and the exact legacy root remains readable");
    }

    private static void VerifyCanonicalGrid(PanelCladdingKeyService keys)
    {
        var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "GLS-001",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "0A",
            [PanelCladdingKeyService.GetCellKey(2, "A")] = "GLS-002"
        };
        PanelCladdingKeySet keySet = RequireData(keys.CreateKeySet(
            new[] { 35d },
            new[] { 40d, 90d },
            existing,
            150d,
            100d,
            0.001d), "Create inferred grid");
        Require(keySet.HorizontalOffsets.SequenceEqual(new[] { 35d }) &&
                keySet.VerticalOffsets.SequenceEqual(new[] { 40d, 90d }) &&
                keySet.Cells.Count == 6,
            "Inferred offsets must create the expected canonical grid.");
        Require(PanelCladdingKeyService.GetHorizontalOffsetKey(1) == "CW_2.03_OFFSET_H1" &&
                PanelCladdingKeyService.GetVerticalOffsetKey(2) == "CW_2.04_OFFSET_V2" &&
                keys.IsOffsetKey("cw_2.04_offset_v7") &&
                keys.IsCladdingCellKey("CW_4.02_CLADDING_2B"),
            "Canonical offset/cell key helpers are inconsistent.");
        Console.WriteLine("[OK] inferred offsets produce contiguous H/V and cell keys");
    }

    private static void VerifyGridChangePlanning(PanelCladdingKeyService keys)
    {
        PanelCladdingKeySet keySet = RequireData(keys.CreateKeySet(
            Array.Empty<double>(),
            new[] { 90d },
            new Dictionary<string, string>
            {
                [PanelCladdingKeyService.GetCellKey(0, "A")] = "GLS-001",
                [PanelCladdingKeyService.GetCellKey(1, "A")] = "GLS-002"
            },
            150d,
            100d,
            0.001d), "Create changed grid");
        var layout = new PanelCladdingLayout
        {
            ObjectId = PanelId,
            DocumentPath = "C:\\tests\\offset-sync.3dm",
            SystemCode = "WT01",
            GeometryFingerprint = "offset-sync-fingerprint",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 150d,
            Height = 100d,
            ModelTolerance = 0.001d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells
        };
        PanelCladdingSurfaceSyncPlan plan = RequireData(
            new PanelCladdingSurfaceSyncPlanningService(keys).CreatePlan(
                new PanelCladdingSurfaceSyncSnapshot
                {
                    SelectedPanelIds = new[] { PanelId },
                    Panels = new[]
                    {
                        new PanelCladdingSurfaceSyncPanelSnapshot
                        {
                            ObjectId = PanelId,
                            PanelId = Pid,
                            Layout = layout,
                            GridChanged = true
                        }
                    },
                    Surfaces = new[]
                    {
                        Surface("0A", "GLS-001", "0A"),
                        Surface("1A", "GLS-002", "1A")
                    }
                }),
            "Plan changed-grid sync");
        Require(plan.Panels.Count == 1 && plan.Panels[0].CladdingChanged,
            "An offset-only grid change must force a panel/workbook write.");
        Require(plan.Panels[0].Layout.VerticalOffsets.SequenceEqual(new[] { 90d }),
            "The sync plan must retain the inferred offset layout.");
        Console.WriteLine("[OK] offset-only changes force panel and workbook synchronization");
    }

    private static void VerifyOffsetPrecision(PanelCladdingKeyService keys)
    {
        PanelCladdingKeySet normalized = RequireData(keys.CreateKeySet(
            new[] { 35.123456d },
            new[] { 40.123454d, 90.000004d },
            new Dictionary<string, string>(),
            150d,
            100d,
            0.000001d), "Normalize offset precision");
        Require(normalized.HorizontalOffsets.SequenceEqual(new[] { 35.12346d }) &&
                normalized.VerticalOffsets.SequenceEqual(new[] { 40.12345d, 90d }),
            "Offsets must round to five decimal places before entering the layout.");
        Require(PanelCladdingKeyService.FormatOffset(35.123456d) == "35.12346" &&
                PanelCladdingKeyService.FormatOffset(90.000004d) == "90",
            "Offset serialization must use at most five fractional digits.");
        Require(keys.AreOffsetsCanonicallyStored(
                new Dictionary<string, string>
                {
                    [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "35.12346",
                    [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "40.12345",
                    [PanelCladdingKeyService.GetVerticalOffsetKey(1)] = "90"
                },
                normalized.HorizontalOffsets,
                normalized.VerticalOffsets) &&
                !keys.AreOffsetsCanonicallyStored(
                    new Dictionary<string, string>
                    {
                        [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "35.123460",
                        [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "40.12345",
                        [PanelCladdingKeyService.GetVerticalOffsetKey(1)] = "90.000004"
                    },
                    normalized.HorizontalOffsets,
                    normalized.VerticalOffsets),
            "Sync must rewrite numerically equal offsets that are not stored canonically.");

        OperationResponse<PanelCladdingKeySet> collapsed = keys.CreateKeySet(
            Array.Empty<double>(),
            new[] { 40.123451d, 40.123454d },
            new Dictionary<string, string>(),
            150d,
            100d,
            0.000001d);
        Require(!collapsed.Success && collapsed.Message.Contains("NON_MONOTONIC_V", StringComparison.Ordinal),
            "Offsets that collapse at five-decimal precision must fail closed.");

        var signatureLayout = new PanelCladdingLayout
        {
            ObjectId = PanelId,
            SystemCode = "WT01",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 150d,
            Height = 100d,
            ModelTolerance = 0.000001d,
            ModelUnitScaleToMillimeters = 1d,
            HorizontalOffsets = normalized.HorizontalOffsets,
            VerticalOffsets = normalized.VerticalOffsets,
            Cells = normalized.Cells
        };
        IReadOnlyDictionary<string, string> signatureValues = normalized.Cells.ToDictionary(
            cell => cell.UserTextKey,
            _ => "GLS-001",
            StringComparer.OrdinalIgnoreCase);
        PanelCladdingTypeIdentity identity = RequireData(
            new PanelCladdingTypeSignatureService(keys).Create(
                signatureLayout,
                signatureValues,
                "WT01"),
            "Create five-decimal signature");
        Require(identity.CanonicalPayload.Contains(
                "|h=35.12346;|v=40.12345;90;|",
                StringComparison.Ordinal),
            "Type signatures must use the same five-decimal offset serialization.");
        Console.WriteLine("[OK] offsets round, serialize, and validate at five-decimal precision");
    }

    private static void VerifyGeometryInference()
    {
        using Brep panel = Rectangle(0d, 0d, 150d, 100d);
        using Brep merged = Rectangle(0d, 0d, 90d, 100d);
        using Brep right = Rectangle(90d, 0d, 150d, 100d);
        PanelCladdingInferredOffsets mergedOffsets = Infer(panel, merged, right);
        Require(mergedOffsets.HorizontalOffsets.Count == 0 &&
                mergedOffsets.VerticalOffsets.Count == 1 &&
                Math.Abs(mergedOffsets.VerticalOffsets[0] - 90d) < 0.001d,
            "A boundary crossed by a merged surface must disappear from inferred offsets.");

        using Brep lowerLeft = Rectangle(0d, 0d, 40d, 50d);
        using Brep lowerRight = Rectangle(40d, 0d, 150d, 50d);
        using Brep upper = Rectangle(0d, 50d, 150d, 100d);
        PanelCladdingInferredOffsets partial = Infer(panel, lowerLeft, lowerRight, upper);
        Require(partial.HorizontalOffsets.Count == 1 &&
                partial.VerticalOffsets.Count == 1 &&
                Math.Abs(partial.HorizontalOffsets[0] - 50d) < 0.001d &&
                Math.Abs(partial.VerticalOffsets[0] - 40d) < 0.001d,
            "A partial region boundary must become a global atomic H/V candidate.");
        Console.WriteLine("[OK] naked cladding boundaries reconstruct merged and partial offset grids");
    }

    private static PanelCladdingInferredOffsets Infer(Brep panel, params Brep[] surfaces)
    {
        Type service = typeof(PanelCladdingSpawnPlanningService).Assembly.GetType(
            "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingGeometryPartitionService") ??
            throw new InvalidOperationException("Live geometry partition service is missing.");
        MethodInfo method = service.GetMethod("InferOffsets", BindingFlags.Public | BindingFlags.Static) ??
            throw new InvalidOperationException("InferOffsets is missing.");
        object response = method.Invoke(null, new object[] { panel, surfaces, 0.001d }) ??
            throw new InvalidOperationException("InferOffsets returned null.");
        bool success = (bool)(response.GetType().GetProperty("Success")?.GetValue(response) ?? false);
        string message = response.GetType().GetProperty("Message")?.GetValue(response)?.ToString() ?? string.Empty;
        object? data = response.GetType().GetProperty("Data")?.GetValue(response);
        if (!success || data is not PanelCladdingInferredOffsets offsets)
        {
            throw new InvalidOperationException($"Infer offsets failed: {message}");
        }
        return offsets;
    }

    private static Brep Rectangle(double left, double bottom, double right, double top) =>
        Brep.CreateFromCornerPoints(
            new Point3d(left, bottom, 0d),
            new Point3d(right, bottom, 0d),
            new Point3d(right, top, 0d),
            new Point3d(left, top, 0d),
            0.001d);

    private static PanelCladdingSurfaceSyncSurfaceSnapshot Surface(
        string suffix,
        string material,
        params string[] cells) => new()
        {
            ObjectId = Guid.NewGuid(),
            PanelId = Pid,
            Cid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(Pid, suffix),
            LayerPath = $"{PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer}::" +
                $"{PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(material)}::{material}",
            CladdingValue = material,
            CoveredCellLabels = cells
        };

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
