using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PCSyncSrfParentPersistenceSmoke;

internal static class Program
{
    private static readonly Guid PanelId =
        Guid.Parse("54000000-0000-0000-0000-000000000001");
    private const string Pid = "PID_PARENT_PERSISTENCE_01";
    private const string Material = "MPL-001";

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingLayout layout = BuildLayout(keys);
        PanelCladdingSurfaceSyncSnapshot snapshot = BuildSnapshot(layout);
        var live = new CapturingLiveRepository(snapshot);
        var service = new PanelCladdingSurfaceSyncService(
            live,
            new PanelCladdingTypeSignatureService(keys),
            new PanelCladdingSurfaceSyncPlanningService(keys));

        RequireData(service.Sync(
            snapshot.DocumentPath,
            new[] { PanelId },
            string.Empty,
            allowCreateWorkbook: false,
            PanelCladdingObjectScope.Surfaces),
            "Run parent persistence sync");
        PanelCladdingSurfaceSyncPanelWrite write = live.LastCommitRequest?.PanelWrites.Single() ??
            throw new InvalidOperationException("PCSyncSrf did not produce one panel write.");
        string ownerKey = PanelCladdingKeyService.GetCellKey(0, "A");
        string childKey = PanelCladdingKeyService.GetCellKey(1, "A");
        Require(write.CellValues[ownerKey] == Material && write.CellValues[childKey] == "0A",
            "The final commit payload must preserve 0A=MPL-001 and 1A=0A.");
        Console.WriteLine("[OK] final PCSyncSrf commit payload preserves 1A=0A");

        OperationResponse<IReadOnlyDictionary<string, string>> blanks =
            PanelCladdingSurfaceSyncService.BuildPersistedCellValues(
                layout,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [ownerKey] = Material,
                    [childKey] = string.Empty
                });
        IReadOnlyDictionary<string, string> stored = RequireData(blanks, "Build persisted blanks");
        Require(stored[childKey] == PanelCladdingKeyService.PersistedBlankCellValue,
            "A blank logical cell must remain present through the persisted-blank sentinel.");
        OperationResponse<IReadOnlyDictionary<string, string>> incomplete =
            PanelCladdingSurfaceSyncService.BuildPersistedCellValues(
                layout,
                new Dictionary<string, string> { [ownerKey] = Material });
        Require(!incomplete.Success && incomplete.Message.Contains(
                "CELL_VALUE_MISSING",
                StringComparison.Ordinal),
            "An incomplete logical graph must fail before Rhino mutation.");
        Console.WriteLine("[OK] every logical cell is required and blank keys remain persisted");

        var exactCommitted = new Dictionary<string, string>(
            write.CellValues,
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.CladdingLogicKey] = write.CladdingLogic
        };
        Require(PanelCladdingSurfaceSyncService.ValidateAppliedPanelCellValues(
                exactCommitted,
                write).Success,
            "The exact committed graph must validate.");
        var missingParent = new Dictionary<string, string>(exactCommitted, StringComparer.OrdinalIgnoreCase);
        missingParent.Remove(childKey);
        Require(!PanelCladdingSurfaceSyncService.ValidateAppliedPanelCellValues(
                missingParent,
                write).Success,
            "Read-back validation must reject a missing 1A parent key.");
        missingParent[childKey] = Material;
        Require(!PanelCladdingSurfaceSyncService.ValidateAppliedPanelCellValues(
                missingParent,
                write).Success,
            "Read-back validation must reject a replaced 1A parent value.");
        Console.WriteLine("[OK] postcondition validation rejects missing or replaced parent values");
    }

    private static PanelCladdingLayout BuildLayout(PanelCladdingKeyService keys)
    {
        PanelCladdingKeySet keySet = RequireData(keys.CreateKeySet(
            Array.Empty<double>(),
            new[] { 45d },
            new Dictionary<string, string>(),
            90d,
            180d,
            0.001d),
            "Create two-column layout");
        return new PanelCladdingLayout
        {
            ObjectId = PanelId,
            DocumentPath = "C:\\tests\\pcsync-parent-persistence.3dm",
            SystemCode = "WT01",
            GeometryFingerprint = "parent-persistence-fingerprint",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 90d,
            Height = 180d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 1d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = keySet.Topology
        };
    }

    private static PanelCladdingSurfaceSyncSnapshot BuildSnapshot(PanelCladdingLayout layout)
    {
        string layer = $"{PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer}::" +
            $"{PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(Material)}::{Material}";
        return new PanelCladdingSurfaceSyncSnapshot
        {
            Scope = PanelCladdingObjectScope.Surfaces,
            DocumentPath = layout.DocumentPath,
            SelectedPanelIds = new[] { PanelId },
            Panels = new[]
            {
                new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = PanelId,
                    PanelId = Pid,
                    PanelCid = "CID_PARENT_PERSISTENCE_01",
                    Layout = layout,
                    GridChanged = true
                }
            },
            Surfaces = new[]
            {
                new PanelCladdingSurfaceSyncSurfaceSnapshot
                {
                    ObjectId = Guid.Parse("54000000-0000-0000-0000-000000000002"),
                    PanelObjectId = PanelId,
                    PanelId = Pid,
                    Cid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(Pid, "0A"),
                    LayerPath = layer,
                    CladdingValue = Material,
                    CoveredCellLabels = new[] { "0A", "1A" }
                }
            }
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingLiveRepository : ILivePanelCladdingSurfaceSyncRepository
    {
        private readonly PanelCladdingSurfaceSyncSnapshot _snapshot;

        public CapturingLiveRepository(PanelCladdingSurfaceSyncSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public PanelCladdingSurfaceSyncCommitRequest? LastCommitRequest { get; private set; }

        public OperationResponse<PanelCladdingSurfaceSyncSnapshot> Read(
            string filePath,
            IReadOnlyList<Guid> panelObjectIds,
            PanelCladdingObjectScope scope) =>
            OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Ok(_snapshot);

        public OperationResponse<PanelCladdingSurfaceSyncResult> Commit(
            PanelCladdingSurfaceSyncCommitRequest request,
            Func<OperationResponse> finalizeWorkbook,
            IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
            int matchedSurfaceCount,
            int matchedCurveCount)
        {
            LastCommitRequest = request;
            OperationResponse finalized = finalizeWorkbook();
            return finalized.Success
                ? OperationResponse<PanelCladdingSurfaceSyncResult>.Ok(
                    new PanelCladdingSurfaceSyncResult
                    {
                        Scope = request.Scope,
                        SelectedPanelIds = request.SelectedPanelIds,
                        ChangedPanelIds = request.PanelWrites.Select(write => write.ObjectId).ToArray(),
                        Types = types,
                        MatchedSurfaceCount = matchedSurfaceCount,
                        MatchedCurveCount = matchedCurveCount
                    })
                : OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(finalized.Message);
        }
    }
}
