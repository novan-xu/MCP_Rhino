using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingBlankCellPersistenceSmoke;

internal static class Program
{
    private static int Main()
    {
        AllUnassignedLogicalCellsRemainInWrites();
        MixedAssignmentsKeepTheirDomainValues();
        MergedPhysicalMemberRemainsObsolete();
        Console.WriteLine("[OK] every surviving unassigned logical cell is written with Rhino's retained blank representation.");
        Console.WriteLine("[OK] stored blanks parse back to empty domain values without changing assigned or parent values.");
        Console.WriteLine("[OK] hidden physical members of merged cells remain obsolete and are not recreated.");
        return 0;
    }

    private static void AllUnassignedLogicalCellsRemainInWrites()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingLayout source = Layout(keys, EmptyValues(2, 3));
        PanelAttributeCommitRequest commit = Save(source, EmptyValues(2, 3));

        string[] expectedKeys = CellKeys(2, 3).ToArray();
        Require(expectedKeys.All(key => commit.UserTextWrites.TryGetValue(key, out string? value) &&
                value == PanelCladdingKeyService.PersistedBlankCellValue),
            "Save did not retain all six unassigned logical cell keys.");
        Require(expectedKeys.All(key => !commit.UserTextDeletes.Contains(key, StringComparer.OrdinalIgnoreCase)),
            "A surviving blank logical cell was incorrectly included in the delete set.");

        PanelCladdingKeySet parsed = Required(keys.Parse(
            commit.UserTextWrites,
            source.Width,
            source.Height,
            source.ModelTolerance), "Parse retained blank cells");
        Require(parsed.Cells.Count == 6 && parsed.Cells.All(cell => cell.Value.Length == 0),
            "The retained blank representation did not parse back to empty domain values.");

        PanelCladdingTypeIdentity emptyIdentity = Required(
            new PanelCladdingTypeSignatureService(keys).Create(source, EmptyValues(2, 3), source.SystemCode),
            "Create empty identity");
        PanelCladdingTypeIdentity storedIdentity = Required(
            new PanelCladdingTypeSignatureService(keys).Create(source, commit.UserTextWrites, source.SystemCode),
            "Create stored-blank identity");
        Require(emptyIdentity.StoredSignature == storedIdentity.StoredSignature,
            "The persisted blank representation changed the panel type signature.");
    }

    private static void MixedAssignmentsKeepTheirDomainValues()
    {
        var keys = new PanelCladdingKeyService();
        Dictionary<string, string> values = EmptyValues(2, 3);
        values[Key(0, "A")] = "MPL-001";
        values[Key(1, "A")] = "0A";
        PanelCladdingLayout source = Layout(keys, values);
        PanelAttributeCommitRequest commit = Save(source, values);

        Require(commit.UserTextWrites[Key(0, "A")] == "MPL-001",
            "Assigned material value changed during blank persistence.");
        Require(commit.UserTextWrites[Key(1, "A")] == "0A",
            "Parent-cell value changed during blank persistence.");
        Require(CellKeys(2, 3)
                .Except([Key(0, "A"), Key(1, "A")], StringComparer.OrdinalIgnoreCase)
                .All(key => commit.UserTextWrites[key] == PanelCladdingKeyService.PersistedBlankCellValue),
            "Unassigned cells in a mixed layout were not retained as blanks.");
    }

    private static void MergedPhysicalMemberRemainsObsolete()
    {
        var keys = new PanelCladdingKeyService();
        Dictionary<string, string> values = EmptyValues(2, 2);
        PanelCladdingTopologyState topology = new()
        {
            MissingSegments = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0)]
        };
        PanelCladdingLayout source = Layout(keys, values, horizontalOffsets: [10d], topology: topology);
        PanelAttributeCommitRequest commit = Save(source, values, topology);

        Require(commit.UserTextWrites[Key(0, "A")] == PanelCladdingKeyService.PersistedBlankCellValue,
            "Merged logical representative was not retained as blank.");
        Require(!commit.UserTextWrites.ContainsKey(Key(0, "B")) &&
                commit.UserTextDeletes.Contains(Key(0, "B"), StringComparer.OrdinalIgnoreCase),
            "Hidden physical member 0B was recreated instead of remaining obsolete.");
        Require(commit.UserTextWrites.ContainsKey(Key(1, "A")) && commit.UserTextWrites.ContainsKey(Key(1, "B")),
            "Unmerged logical cells were not retained.");
    }

    private static PanelAttributeCommitRequest Save(
        PanelCladdingLayout source,
        IReadOnlyDictionary<string, string> values,
        PanelCladdingTopologyState? topology = null)
    {
        var keys = new PanelCladdingKeyService();
        var repository = new CapturingRepository(source);
        var service = new PanelCladdingSaveService(repository, new PanelCladdingTypeSignatureService(keys));
        Required(service.Save(new PanelCladdingSaveRequest
        {
            FilePath = source.DocumentPath,
            ObjectId = source.ObjectId,
            ExpectedGeometryFingerprint = source.GeometryFingerprint,
            SystemCode = source.SystemCode,
            HorizontalOffsets = source.HorizontalOffsets,
            VerticalOffsets = source.VerticalOffsets,
            Topology = topology ?? source.Topology,
            CellValues = values
        }), "Save blank cells");
        return repository.LastCommit ?? throw new InvalidOperationException("Save did not prepare a commit.");
    }

    private static PanelCladdingLayout Layout(
        PanelCladdingKeyService keys,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<double>? horizontalOffsets = null,
        PanelCladdingTopologyState? topology = null)
    {
        horizontalOffsets ??= [10d, 20d];
        IReadOnlyList<double> verticalOffsets = [15d];
        PanelCladdingKeySet keySet = Required(keys.CreateKeySet(
            horizontalOffsets,
            verticalOffsets,
            values,
            30d,
            30d,
            0.001d), "Create key set");
        var sourceText = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < horizontalOffsets.Count; index++)
        {
            sourceText[PanelCladdingKeyService.GetHorizontalOffsetKey(index)] =
                PanelCladdingKeyService.FormatOffset(horizontalOffsets[index]);
        }
        sourceText[PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "15.00000";
        return new PanelCladdingLayout
        {
            ObjectId = Guid.NewGuid(),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/test/blank-cell-persistence.3dm",
            ObjectName = "BLANK_CELL_TEST",
            LayerFullPath = "01_CW Panels::Surfaces-PNL::WT-01",
            SystemCode = "WT01",
            GeometryFingerprint = "blank-cell-persistence-fingerprint",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar fixture.",
            Width = 30d,
            Height = 30d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = topology ?? new PanelCladdingTopologyState(),
            SourceUserText = sourceText,
            Preview = new PanelPreviewGeometry()
        };
    }

    private static Dictionary<string, string> EmptyValues(int columns, int rows) =>
        CellKeys(columns, rows).ToDictionary(key => key, _ => string.Empty, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> CellKeys(int columns, int rows)
    {
        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                yield return Key(column, PanelCladdingKeyService.GetRowLabel(row));
            }
        }
    }

    private static string Key(int column, string row) => PanelCladdingKeyService.GetCellKey(column, row);

    private static T Required<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static OperationResponse Required(OperationResponse response, string operation)
    {
        if (!response.Success)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public PanelAttributeCommitRequest? LastCommit { get; private set; }

        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used.");

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
            OperationResponse<string>.Fail("Not used.");
    }
}
