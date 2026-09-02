using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingLogicalCellCleanupSmoke;

internal static class Program
{
    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        Dictionary<string, string> values = BuildValues();
        PanelCladdingLayout source = BuildLayout(keys, values);
        var repository = new CapturingRepository(source);
        var save = new PanelCladdingSaveService(
            repository,
            new PanelCladdingTypeSignatureService(keys));
        var topology = new PanelCladdingTopologyState
        {
            MissingSegments =
            [
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 2, 0),
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 2, 1)
            ]
        };

        Required(save.Save(new PanelCladdingSaveRequest
        {
            FilePath = source.DocumentPath,
            ObjectId = source.ObjectId,
            ExpectedGeometryFingerprint = source.GeometryFingerprint,
            SystemCode = source.SystemCode,
            HorizontalOffsets = source.HorizontalOffsets,
            VerticalOffsets = source.VerticalOffsets,
            Topology = topology,
            CellValues = values
        }), "Save logical topology");

        PanelAttributeCommitRequest commit = repository.LastCommit ??
            throw new InvalidOperationException("Save did not prepare an attribute commit.");
        string[] expectedLogicalKeys =
        [
            PanelCladdingKeyService.GetCellKey(0, "A"),
            PanelCladdingKeyService.GetCellKey(0, "B"),
            PanelCladdingKeyService.GetCellKey(0, "C"),
            PanelCladdingKeyService.GetCellKey(1, "A"),
            PanelCladdingKeyService.GetCellKey(1, "B"),
            PanelCladdingKeyService.GetCellKey(1, "C")
        ];
        string[] actualLogicalKeys = commit.UserTextWrites.Keys
            .Where(keys.IsCladdingCellKey)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Require(actualLogicalKeys.SequenceEqual(
                expectedLogicalKeys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase),
            $"Expected six logical cell writes, got {string.Join(",", actualLogicalKeys)}.");

        string zeroD = PanelCladdingKeyService.GetCellKey(0, "D");
        string oneD = PanelCladdingKeyService.GetCellKey(1, "D");
        Require(!commit.UserTextWrites.ContainsKey(zeroD) && !commit.UserTextWrites.ContainsKey(oneD),
            "Hidden D cells were still written.");
        Require(commit.UserTextDeletes.Contains(zeroD, StringComparer.OrdinalIgnoreCase) &&
                commit.UserTextDeletes.Contains(oneD, StringComparer.OrdinalIgnoreCase),
            "Hidden D cells were not included in the delete set.");
        Require(commit.UserTextWrites[PanelCladdingKeyService.GetCellKey(0, "C")] == "MPL-002",
            "0C did not retain the merged cell material.");
        Require(commit.UserTextWrites[PanelCladdingKeyService.GetCellKey(1, "C")] == "0B",
            "1C did not retain its parent-cell assignment.");

        var normalized = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "0D"
        };
        IReadOnlyDictionary<string, string> remapped = new PanelCladdingLogicalCellService().Collapse(
            source.Cells,
            topology,
            normalized);
        Require(remapped[PanelCladdingKeyService.GetCellKey(1, "A")] == "0C",
            "A parent label targeting hidden 0D was not remapped to logical representative 0C.");

        Console.WriteLine("[OK] Structural Save writes only 0A-0C and 1A-1C for the merged 2x4 lattice.");
        Console.WriteLine("[OK] Stale 0D/1D keys are deleted and representative assignments are preserved.");
        Console.WriteLine("[OK] Parent references to hidden physical cells resolve to logical representatives.");
    }

    private static Dictionary<string, string> BuildValues() => new(StringComparer.OrdinalIgnoreCase)
    {
        [PanelCladdingKeyService.GetCellKey(0, "A")] = "MPL-001",
        [PanelCladdingKeyService.GetCellKey(0, "B")] = "MPL-001",
        [PanelCladdingKeyService.GetCellKey(0, "C")] = "MPL-002",
        [PanelCladdingKeyService.GetCellKey(0, "D")] = "MPL-002",
        [PanelCladdingKeyService.GetCellKey(1, "A")] = "0A",
        [PanelCladdingKeyService.GetCellKey(1, "B")] = "0B",
        [PanelCladdingKeyService.GetCellKey(1, "C")] = "0B",
        [PanelCladdingKeyService.GetCellKey(1, "D")] = "0B"
    };

    private static PanelCladdingLayout BuildLayout(
        PanelCladdingKeyService keys,
        IReadOnlyDictionary<string, string> values)
    {
        double[] horizontal = [152.20455d, 163.875d, 204.625d];
        double[] vertical = [22.5d];
        PanelCladdingKeySet keySet = Required(keys.CreateKeySet(
            horizontal,
            vertical,
            values,
            panelWidth: 45d,
            panelHeight: 279.125d,
            tolerance: 0.001d), "Build 2x4 fixture");
        var sourceUserText = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < horizontal.Length; index++)
        {
            sourceUserText[PanelCladdingKeyService.GetHorizontalOffsetKey(index)] =
                PanelCladdingKeyService.FormatOffset(horizontal[index]);
        }
        sourceUserText[PanelCladdingKeyService.GetVerticalOffsetKey(0)] =
            PanelCladdingKeyService.FormatOffset(vertical[0]);
        return new PanelCladdingLayout
        {
            ObjectId = Guid.NewGuid(),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/test/logical-cell-cleanup.3dm",
            ObjectName = "PID_BKT_N1_01_11",
            LayerFullPath = "01_CW Panels::Surfaces-PNL::WT-04",
            SystemCode = "WT04",
            GeometryFingerprint = "logical-cell-cleanup-fingerprint",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar fixture.",
            Width = 45d,
            Height = 279.125d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = new PanelCladdingTopologyState(),
            SourceUserText = sourceUserText,
            Preview = new PanelPreviewGeometry()
        };
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

    private sealed class CapturingRepository : ILivePanelCladdingRepository
    {
        private readonly PanelCladdingLayout _layout;

        public CapturingRepository(PanelCladdingLayout layout)
        {
            _layout = layout;
        }

        public PanelAttributeCommitRequest? LastCommit { get; private set; }

        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(_layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used by logical-cell cleanup smoke.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit)
        {
            LastCommit = request;
            OperationResponse finalized = finalizeExternalCommit();
            return finalized.Success
                ? OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
                {
                    ObjectId = request.ObjectId,
                    Mutated = true
                })
                : OperationResponse<PanelAttributeCommitResult>.Fail(finalized.Message);
        }

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used by logical-cell cleanup smoke.");
    }
}
