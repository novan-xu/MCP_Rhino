using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.IO;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;

string tempRoot = Path.Combine(Path.GetTempPath(), "panel-cladding-material-catalog-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);
try
{
    string workbookPath = Path.Combine(tempRoot, "materials.xlsx");
    PanelCladdingMaterialCatalogItem[] materials =
    [
        Item("GLS-003", "Glazing panel", "Glass", "#71A0AE"),
        Item("GLS-004", "Glazing panel", "Glass", "#344C54"),
        Item("GLS-005", "Glazing panel", "Glass", "#507681"),
        Item("STN-001", "Stone finish", "Stone", "#A59582"),
        Item("STN-002", "Stone finish", "Stone", "#82725D"),
        Item("STN-003", "Stone finish", "Stone", "#BCB1A2"),
        Item("TER-001", "Terracotta finish", "Terracotta", "#B86641")
    ];

    var repository = new OpenXmlPanelCladdingWorkbookRepository();
    OperationResponse<IPreparedPanelCladdingMaterialCatalogUpdate> prepared = repository.PrepareMaterialCatalog(
        new PanelCladdingMaterialCatalogSaveRequest
        {
            WorkbookPath = workbookPath,
            AllowCreate = true,
            Materials = materials
        });
    Assert(prepared.Success && prepared.Data is not null, prepared.Message);
    using (IPreparedPanelCladdingMaterialCatalogUpdate update = prepared.Data!)
    {
        OperationResponse committed = update.Commit();
        Assert(committed.Success, committed.Message);
        Assert(update.Result.MaterialCount == 7, "Catalog result did not report seven materials.");
    }

    OperationResponse<PanelCladdingMaterialCatalog> read = repository.ReadMaterialCatalog(workbookPath);
    Assert(read.Success && read.Data is not null, read.Message);
    PanelCladdingMaterialCatalog catalog = read.Data!;
    Assert(catalog.Materials.Count == 7, "Catalog round trip did not preserve seven materials.");
    Assert(catalog.Materials.Single(item => item.Code == "GLS-004").ColorHex == "#344C54",
        "GLS-004 color did not round trip.");
    using (SpreadsheetDocument document = SpreadsheetDocument.Open(workbookPath, false))
    {
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidOperationException("WorkbookPart missing.");
        Workbook workbook = workbookPart.Workbook
            ?? throw new InvalidOperationException("Workbook missing.");
        Sheet[] sheets = workbook.Sheets!.Elements<Sheet>().ToArray();
        Assert(sheets.Length == 1 && sheets[0].Name?.Value == "Materials",
            "A new catalog workbook must contain only the Materials sheet.");
        WorksheetPart part = (WorksheetPart)workbookPart.GetPartById(sheets[0].Id!);
        Worksheet worksheet = part.Worksheet
            ?? throw new InvalidOperationException("Materials worksheet missing.");
        Assert(worksheet.Descendants<AutoFilter>().Single().Reference?.Value == "A1:E8",
            "Materials table filter range is incorrect.");
        Assert(worksheet.Descendants<Pane>().Single().State?.Value == PaneStateValues.Frozen,
            "Materials header is not frozen.");
    }

    Guid objectId = Guid.NewGuid();
    var layout = new PanelCladdingLayout
    {
        ObjectId = objectId,
        DocumentPath = "panel.3dm",
        SystemCode = "WT01",
        GeometryFingerprint = "geometry-v1",
        GeometryClass = PanelGeometryClass.Planar,
        Width = 90,
        Height = 180,
        HorizontalOffsets = [90],
        VerticalOffsets = [45],
        Cells =
        [
            Cell(0, 0, "0A", "TER-001"), Cell(0, 1, "0B", "TER-001"),
            Cell(1, 0, "1A", "STN-003"), Cell(1, 1, "1B", "1A")
        ]
    };
    var live = new LiveStub(layout);
    var keys = new PanelCladdingKeyService();
    var save = new PanelCladdingSaveService(live, new PanelCladdingTypeSignatureService(keys));
    OperationResponse<PanelCladdingSaveResult> saved = save.Save(new PanelCladdingSaveRequest
    {
        FilePath = layout.DocumentPath,
        ObjectId = objectId,
        ExpectedGeometryFingerprint = layout.GeometryFingerprint,
        WorkbookPath = string.Empty,
        SystemCode = layout.SystemCode,
        CellValues = layout.Cells.ToDictionary(cell => cell.UserTextKey, cell => cell.Value)
    });
    Assert(saved.Success && saved.Data is not null, saved.Message);
    Assert(saved.Data!.SheetName == string.Empty, "Panel save must not produce a workbook type sheet.");
    Assert(live.LastCommit?.UserTextWrites.ContainsKey(PanelCladdingKeyService.TypeCodeKey) == true,
        "Panel save did not write the calculated cladding type key.");
    Assert(live.LastCommit?.UserTextWrites.ContainsKey(PanelCladdingKeyService.SignatureKey) == false &&
           live.LastCommit.UserTextDeletes.Contains(
               PanelCladdingKeyService.SignatureKey,
               StringComparer.OrdinalIgnoreCase),
        "Panel save did not retire the legacy cladding signature key.");

    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    string xaml = File.ReadAllText(Path.Combine(root, "src", "PanelCladdingEditor", "UI", "PanelCladdingEditorWindow.xaml"));
    Assert(xaml.IndexOf("x:Name=\"CladdingAssignmentSection\"", StringComparison.Ordinal) <
           xaml.IndexOf("x:Name=\"MaterialLegendSection\"", StringComparison.Ordinal),
        "Drag Materials must follow Cell Assignment.");
    Assert(xaml.IndexOf("x:Name=\"MaterialLegendSection\"", StringComparison.Ordinal) <
           xaml.IndexOf("Text=\"CLADDING TYPE\"", StringComparison.Ordinal),
        "Drag Materials must precede Cladding Type.");
    Assert(!xaml.Contains("EXCEL TYPOLOGY RECORD", StringComparison.Ordinal),
        "Legacy Excel Typology Record section remains in the editor.");
    string canvas = File.ReadAllText(Path.Combine(root, "src", "PanelCladdingEditor", "UI", "PanelCladdingGridCanvas.cs"));
    Assert(canvas.Contains("#FFEA00", StringComparison.Ordinal), "Bright yellow selection overwrite is missing.");
    Assert(canvas.Contains("new DashStyle([3d, 3d], 0d)", StringComparison.Ordinal),
        "Centered dashed parent boundary style is missing.");

    Console.WriteLine("PASS: project material catalog round trip, Rhino-only cladding save, and UI source checks.");
}
finally
{
    Directory.Delete(tempRoot, recursive: true);
}

static PanelCladdingMaterialCatalogItem Item(string code, string description, string category, string color) => new()
{
    Code = code,
    Description = description,
    Category = category,
    ColorHex = color
};

static PanelCladdingCell Cell(int column, int row, string label, string value) => new()
{
    Column = column,
    Row = row,
    RowLabel = ((char)('A' + row)).ToString(),
    ShortLabel = label,
    UserTextKey = PanelCladdingKeyService.GetCellKey(column, ((char)('A' + row)).ToString()),
    Value = value
};

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class LiveStub(PanelCladdingLayout layout) : ILivePanelCladdingRepository
{
    public PanelAttributeCommitRequest? LastCommit { get; private set; }

    public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
        OperationResponse<PanelCladdingLayout>.Ok(layout);

    public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
        OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not required.");

    public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
        PanelAttributeCommitRequest request,
        Func<OperationResponse> finalizeExternalCommit)
    {
        OperationResponse finalized = finalizeExternalCommit();
        if (!finalized.Success)
        {
            return OperationResponse<PanelAttributeCommitResult>.Fail(finalized.Message);
        }
        LastCommit = request;
        return OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
        {
            ObjectId = request.ObjectId,
            Mutated = true
        });
    }

    public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
        OperationResponse<string>.Ok(workbookPath);
}
