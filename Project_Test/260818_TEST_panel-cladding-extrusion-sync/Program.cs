using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.UI;
using System.Runtime.CompilerServices;

namespace PanelCladdingExtrusionSyncSmoke;

internal static class Program
{
    private static readonly Guid PanelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SurfaceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CurveId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingKeySet keySet = Required(keys.CreateKeySet(
            [20d, 50d], [30d], new Dictionary<string, string>(), 100d, 80d, 0.001d), "Create grid");
        keySet = new PanelCladdingKeySet
        {
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = new PanelCladdingTopologyState
            {
                MissingSegments =
                [
                    new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 1)
                ],
                MergeRuns =
                [
                    new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Horizontal, 1, 0, 1),
                    new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Vertical, 0, 0, 1)
                ]
            }
        };

        IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves = Required(
            new PanelCladdingExtrusionPlanningService().CreatePlan(
                "PID_BKT_N2_06_04", "CID_BKT_N2_06_04", 100d, 80d, keySet,
                "01_CW Panels::Surfaces-PNL::WT-04"),
            "Plan extrusion curves");
        Require(curves.Count == 8, $"Expected 8 curve objects, got {curves.Count}.");
        Require(curves.Select(curve => curve.Code).OrderBy(code => code).SequenceEqual(new[]
        {
            "FRM_0", "FRM_1", "FRM_2", "FRM_3", "INT_0A–B", "INT_0C", "INT_A0", "INT_B"
        }.OrderBy(code => code)), "Curve codes did not reflect deleted/merged topology.");
        Require(curves.All(curve => curve.LayerPath ==
                "02_CW Extrusions::Curves-PNL::WT-04"),
            "A curve was planned on the wrong layer.");
        Require(curves.All(curve => curve.Cid == $"CID_BKT_N2_06_04-{curve.Code}" &&
            curve.UserTextWrites["CRV"] == curve.Code &&
            curve.UserTextWrites["CW_1.01_PID"] == "PID_BKT_N2_06_04"),
            "Curve PID/CRV/CID metadata was not canonical.");
        Require(curves.Where(curve => curve.Kind == PanelCladdingExtrusionCurveKind.Frame)
                .All(curve => curve.ObjectColor == new PanelColorRgb(0, 0, 255)),
            "Main-frame curves were not planned in Blue.");
        Require(curves.Where(curve => curve.Kind != PanelCladdingExtrusionCurveKind.Frame &&
                                      curve.Axis == PanelCladdingTopologyAxis.Horizontal)
                .All(curve => curve.ObjectColor == new PanelColorRgb(128, 0, 128)),
            "Horizontal intermediate curves were not planned in Purple.");
        Require(curves.Where(curve => curve.Kind != PanelCladdingExtrusionCurveKind.Frame &&
                                      curve.Axis == PanelCladdingTopologyAxis.Vertical)
                .All(curve => curve.ObjectColor == new PanelColorRgb(0, 100, 0)),
            "Vertical intermediate curves were not planned in DarkGreen.");
        Console.WriteLine("[OK] topology expands into underscore-named frame/segment/merge curves with canonical metadata.");

        Require(PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer == "04_STEP Surfaces" &&
            PanelCladdingSpawnPlanningService.IsSupportedMaterialSurfaceRoot("04_STEP Surfaces") &&
            PanelCladdingSpawnPlanningService.IsSupportedMaterialSurfaceRoot("03_Material Surfaces (STEP)"),
            "New write root or legacy read compatibility is wrong.");
        var syncSrfCommand = (PanelCladdingSyncSrfCommand)RuntimeHelpers.GetUninitializedObject(
            typeof(PanelCladdingSyncSrfCommand));
        var syncCrvCommand = (PanelCladdingSyncCrvCommand)RuntimeHelpers.GetUninitializedObject(
            typeof(PanelCladdingSyncCrvCommand));
        Require(syncSrfCommand.EnglishName == "PCSyncSrf" && syncCrvCommand.EnglishName == "PCSyncCrv",
            "The visible synchronization commands are not separated by object family.");
        Console.WriteLine("[OK] new surfaces target 04_STEP Surfaces, legacy root remains readable, and synchronization is split by object family.");

        PanelCladdingLayout layout = new()
        {
            ObjectId = PanelId,
            GeometryClass = PanelGeometryClass.Planar,
            GeometryFingerprint = "geometry",
            SystemCode = "WT01",
            Width = 90d,
            Height = 180d,
            ModelTolerance = 0.001d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = keySet.Topology
        };
        var snapshot = new PanelCladdingSurfaceSyncSnapshot
        {
            SelectedPanelIds = [PanelId],
            Panels =
            [
                new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = PanelId,
                    PanelId = "PID_BKT_N2_06_04",
                    PanelCid = "CID_BKT_N2_06_04",
                    Layout = layout,
                    GridChanged = true
                }
            ],
            Surfaces =
            [
                new PanelCladdingSurfaceSyncSurfaceSnapshot
                {
                    ObjectId = SurfaceId,
                    PanelObjectId = PanelId,
                    PanelId = "WRONG_PID",
                    Cid = "WRONG_CID",
                    LayerPath = "04_STEP Surfaces::Surfaces-Metal::MPL-001",
                    CladdingValue = "WRONG",
                    CoveredCellLabels = keySet.Cells.Select(cell => cell.ShortLabel).ToArray()
                }
            ],
            Curves =
            [
                new PanelCladdingSurfaceSyncCurveSnapshot
                {
                    ObjectId = CurveId,
                    PanelObjectId = PanelId,
                    PanelId = "WRONG_PID",
                    Cid = "WRONG_CID",
                    CurveCode = "FRM-0",
                    DesiredCode = "FRM_0",
                    AssignedExtrusions = "1D-ALU-OLD",
                    DesiredAssignedExtrusions = "1D-ALU-H0651;1D-GSK-001",
                    AssignedExtrusionValues = new Dictionary<string, string>
                    {
                        ["1D-ALU-OLD"] = "LL"
                    },
                    DesiredAssignedExtrusionValues = new Dictionary<string, string>
                    {
                        ["1D-ALU-H0651"] = "(LL+4)*2",
                        ["1D-GSK-001"] = "LL+4"
                    },
                    UsesObjectColor = true,
                    ObjectColor = new PanelColorRgb(1, 2, 3),
                    DesiredObjectColor = new PanelColorRgb(0, 0, 255),
                    LayerPath = "02_CW Extrusions::Curves-PNL::WT-04"
                }
            ]
        };
        var surfaceRepository = new CaptureRepository(snapshot);
        OperationResponse<PanelCladdingSurfaceSyncResult> surfaceSynced = new PanelCladdingSurfaceSyncService(
            surfaceRepository,
            new PanelCladdingTypeSignatureService(keys),
            new PanelCladdingSurfaceSyncPlanningService(keys)).Sync(
                "panel.3dm", [PanelId], string.Empty, false, PanelCladdingObjectScope.Surfaces);
        Require(surfaceSynced.Success && surfaceRepository.Request is not null,
            $"Surface sync planning failed: {surfaceSynced.Message}");
        PanelCladdingSurfaceSyncCommitRequest surfaceRequest = surfaceRepository.Request!;
        Require(surfaceRequest.Scope == PanelCladdingObjectScope.Surfaces &&
                surfaceRequest.SurfaceWrites.Count == 1 && surfaceRequest.CurveWrites.Count == 0 &&
                surfaceRequest.PanelWrites.Count == 1,
            "PCSyncSrf must repair surfaces and panel values without writing curves.");
        PanelCladdingSurfaceSyncSurfacePlan surfaceWrite = surfaceRequest.SurfaceWrites.Single();
        PanelCladdingSurfaceSyncPanelWrite panelWrite = surfaceRequest.PanelWrites.Single();
        Require(surfaceWrite.PanelId == "PID_BKT_N2_06_04" && surfaceWrite.PidChanged &&
            surfaceWrite.DesiredCid.EndsWith("-0A", StringComparison.Ordinal),
            "Geometry-associated surface metadata was not repaired independently of its old PID/CID.");
        Require(panelWrite.UnitWidth == "90.00000" && panelWrite.UnitHeight == "180.00000" &&
            panelWrite.UnitDimension == "90.00000x180.00000",
            "Panel unit dimensions were not generated with five decimals.");

        var curveRepository = new CaptureRepository(snapshot);
        OperationResponse<PanelCladdingSurfaceSyncResult> curveSynced = new PanelCladdingSurfaceSyncService(
            curveRepository,
            new PanelCladdingTypeSignatureService(keys),
            new PanelCladdingSurfaceSyncPlanningService(keys)).Sync(
                "panel.3dm", [PanelId], string.Empty, false, PanelCladdingObjectScope.Curves);
        Require(curveSynced.Success && curveRepository.Request is not null,
            $"Curve sync planning failed: {curveSynced.Message}");
        PanelCladdingSurfaceSyncCommitRequest curveRequest = curveRepository.Request!;
        Require(curveRequest.Scope == PanelCladdingObjectScope.Curves &&
                curveRequest.SurfaceWrites.Count == 0 && curveRequest.CurveWrites.Count == 1 &&
                curveRequest.PanelWrites.Count == 1,
            "PCSyncCrv must repair curves and panel topology without writing surfaces.");
        PanelCladdingSurfaceSyncCurvePlan curveWrite = curveRequest.CurveWrites.Single();
        Require(curveWrite.DesiredCode == "FRM_0" &&
            curveWrite.DesiredCid == "CID_BKT_N2_06_04-FRM_0" &&
            curveWrite.DesiredAssignedExtrusions == "1D-ALU-H0651;1D-GSK-001" &&
            curveWrite.DesiredAssignedExtrusionValues["1D-ALU-H0651"] == "(LL+4)*2" &&
            curveWrite.DesiredAssignedExtrusionValues["1D-GSK-001"] == "LL+4" &&
            curveWrite.DesiredObjectColor == new PanelColorRgb(0, 0, 255),
            "Geometry-associated curve metadata/color was not repaired.");
        Console.WriteLine("[OK] scoped sync plans repair only its own object family and retains five-decimal unit dimensions.");
    }

    private static T Required<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        return response.Data;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class CaptureRepository : ILivePanelCladdingSurfaceSyncRepository
    {
        private readonly PanelCladdingSurfaceSyncSnapshot _snapshot;
        public CaptureRepository(PanelCladdingSurfaceSyncSnapshot snapshot) => _snapshot = snapshot;
        public PanelCladdingSurfaceSyncCommitRequest? Request { get; private set; }

        public OperationResponse<PanelCladdingSurfaceSyncSnapshot> Read(
            string filePath,
            IReadOnlyList<Guid> panelObjectIds,
            PanelCladdingObjectScope scope) =>
            OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Ok(new PanelCladdingSurfaceSyncSnapshot
            {
                Scope = scope,
                DocumentPath = _snapshot.DocumentPath,
                WorkbookPath = _snapshot.WorkbookPath,
                SelectedPanelIds = _snapshot.SelectedPanelIds,
                Panels = _snapshot.Panels,
                Surfaces = scope == PanelCladdingObjectScope.Surfaces
                    ? _snapshot.Surfaces
                    : Array.Empty<PanelCladdingSurfaceSyncSurfaceSnapshot>(),
                Curves = scope == PanelCladdingObjectScope.Curves
                    ? _snapshot.Curves
                    : Array.Empty<PanelCladdingSurfaceSyncCurveSnapshot>(),
                Issues = _snapshot.Issues,
                ModelTypeAssignments = _snapshot.ModelTypeAssignments
            });

        public OperationResponse<PanelCladdingSurfaceSyncResult> Commit(
            PanelCladdingSurfaceSyncCommitRequest request,
            Func<OperationResponse> finalizeWorkbook,
            IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
            int matchedSurfaceCount,
            int matchedCurveCount)
        {
            Request = request;
            return OperationResponse<PanelCladdingSurfaceSyncResult>.Ok(new PanelCladdingSurfaceSyncResult
            {
                Scope = request.Scope,
                SelectedPanelIds = request.SelectedPanelIds,
                ChangedPanelIds = request.PanelWrites.Select(write => write.ObjectId).ToArray(),
                RefreshedSurfaceIds = request.SurfaceWrites.Select(write => write.ObjectId).ToArray(),
                RefreshedCurveIds = request.CurveWrites.Select(write => write.ObjectId).ToArray(),
                MatchedSurfaceCount = matchedSurfaceCount,
                MatchedCurveCount = matchedCurveCount
            });
        }
    }
}
