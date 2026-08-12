namespace PanelCladdingEditor.Domain.Models.PanelCladding;

public enum PanelGeometryClass
{
    Planar,
    Curved,
    UnsupportedProjection
}

public readonly record struct PanelPoint3(double X, double Y, double Z);

public readonly record struct PanelPoint2(double X, double Y);

public readonly record struct PanelTriangle(int A, int B, int C);

public readonly record struct PanelColorRgb(byte Red, byte Green, byte Blue);

public sealed class PanelCladdingCell
{
    public int Column { get; init; }
    public int Row { get; init; }
    public string RowLabel { get; init; } = string.Empty;
    public string ShortLabel { get; init; } = string.Empty;
    public string UserTextKey { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class PanelPreviewCell
{
    public string UserTextKey { get; init; } = string.Empty;
    public PanelPoint3 Center { get; init; }
    public IReadOnlyList<PanelPoint3> Boundary { get; init; } = Array.Empty<PanelPoint3>();
}

public sealed class PanelPreviewGeometry
{
    public IReadOnlyList<PanelPoint3> Vertices { get; init; } = Array.Empty<PanelPoint3>();
    public IReadOnlyList<PanelTriangle> Triangles { get; init; } = Array.Empty<PanelTriangle>();
    public IReadOnlyList<IReadOnlyList<PanelPoint3>> GridPolylines { get; init; } = Array.Empty<IReadOnlyList<PanelPoint3>>();
    public IReadOnlyList<PanelPreviewCell> Cells { get; init; } = Array.Empty<PanelPreviewCell>();
    public IReadOnlyList<double> DepthSamples { get; init; } = Array.Empty<double>();
}

public sealed class PanelCladdingLayout
{
    public Guid ObjectId { get; init; }
    public uint DocumentRuntimeSerialNumber { get; init; }
    public string DocumentPath { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string LayerFullPath { get; init; } = string.Empty;
    public string SystemCode { get; init; } = string.Empty;
    public string GeometryFingerprint { get; init; } = string.Empty;
    public PanelGeometryClass GeometryClass { get; init; }
    public string GeometryDiagnostic { get; init; } = string.Empty;
    public double Width { get; init; }
    public double Height { get; init; }
    public double ModelTolerance { get; init; }
    public double ModelUnitScaleToMillimeters { get; init; } = 1.0;
    public IReadOnlyList<double> HorizontalOffsets { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> VerticalOffsets { get; init; } = Array.Empty<double>();
    public IReadOnlyList<PanelCladdingCell> Cells { get; init; } = Array.Empty<PanelCladdingCell>();
    public PanelPreviewGeometry Preview { get; init; } = new();
    public string WorkbookPath { get; init; } = string.Empty;

    public int RowCount => HorizontalOffsets.Count + 1;
    public int ColumnCount => VerticalOffsets.Count + 1;
    public bool CanSave => GeometryClass != PanelGeometryClass.UnsupportedProjection;
}

public sealed class PanelCladdingKeySet
{
    public IReadOnlyList<double> HorizontalOffsets { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> VerticalOffsets { get; init; } = Array.Empty<double>();
    public IReadOnlyList<PanelCladdingCell> Cells { get; init; } = Array.Empty<PanelCladdingCell>();
}

public sealed class PanelCladdingTypeIdentity
{
    public int SchemaVersion { get; init; } = 1;
    public string TypeCode { get; init; } = string.Empty;
    public string FullDigest { get; init; } = string.Empty;
    public string StoredSignature { get; init; } = string.Empty;
    public string CanonicalPayload { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> NormalizedCellValues { get; init; } = new Dictionary<string, string>();
}

public sealed class PanelCladdingWorkbookUpsert
{
    public string WorkbookPath { get; init; } = string.Empty;
    public PanelCladdingLayout Layout { get; init; } = new();
    public PanelCladdingTypeIdentity Identity { get; init; } = new();
    public byte[] PreviewPng { get; init; } = Array.Empty<byte>();
    public bool AllowCreate { get; init; }
}

public sealed class PanelCladdingWorkbookCommitResult
{
    public string WorkbookPath { get; init; } = string.Empty;
    public string SheetName { get; init; } = string.Empty;
    public PanelCladdingTypeIdentity Identity { get; init; } = new();
    public bool ReusedExistingType { get; init; }
}

public sealed class PanelCladdingSaveRequest
{
    public string FilePath { get; init; } = string.Empty;
    public Guid ObjectId { get; init; }
    public string ExpectedGeometryFingerprint { get; init; } = string.Empty;
    public string WorkbookPath { get; init; } = string.Empty;
    public bool AllowCreateWorkbook { get; init; }
    public string SystemCode { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> CellValues { get; init; } = new Dictionary<string, string>();
}

public sealed class PanelCladdingSaveResult
{
    public Guid ObjectId { get; init; }
    public string TypeCode { get; init; } = string.Empty;
    public string StoredSignature { get; init; } = string.Empty;
    public string WorkbookPath { get; init; } = string.Empty;
    public string SheetName { get; init; } = string.Empty;
    public bool ReusedExistingType { get; init; }
}

public sealed class PanelCladdingSpawnCellPlan
{
    public int Column { get; init; }
    public int Row { get; init; }
    public string CellLabel { get; init; } = string.Empty;
    public string CladdingCode { get; init; } = string.Empty;
    public string Cid { get; init; } = string.Empty;
    public string LayerPath { get; init; } = string.Empty;
    public PanelColorRgb LayerColor { get; init; }
    public IReadOnlyDictionary<string, string> UserTextWrites { get; init; } =
        new Dictionary<string, string>();
}

public sealed class PanelCladdingSpawnPlan
{
    public string PanelId { get; init; } = string.Empty;
    public IReadOnlyList<PanelCladdingSpawnCellPlan> Cells { get; init; } =
        Array.Empty<PanelCladdingSpawnCellPlan>();
}

public sealed class PanelCladdingSpawnResult
{
    public IReadOnlyList<Guid> SourcePanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> CreatedObjectIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<string> Cids { get; init; } = Array.Empty<string>();
}

public sealed class PanelCladdingMatchGeometryDescriptor
{
    public PanelGeometryClass GeometryClass { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double ModelTolerance { get; init; }
    public IReadOnlyList<double> DepthSamples { get; init; } = Array.Empty<double>();
}

public sealed class PanelCladdingMatchPanelSnapshot
{
    public Guid ObjectId { get; init; }
    public PanelCladdingMatchGeometryDescriptor Geometry { get; init; } = new();
    public IReadOnlyDictionary<string, string> UserText { get; init; } =
        new Dictionary<string, string>();
}

public sealed class PanelCladdingMatchTargetPlan
{
    public Guid ObjectId { get; init; }
    public IReadOnlyList<string> UserTextDeletes { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> UserTextWrites { get; init; } =
        new Dictionary<string, string>();
}

public sealed class PanelCladdingMatchPlan
{
    public Guid SourceObjectId { get; init; }
    public IReadOnlyList<PanelCladdingMatchTargetPlan> Targets { get; init; } =
        Array.Empty<PanelCladdingMatchTargetPlan>();
}

public sealed class PanelCladdingMatchResult
{
    public Guid SourceObjectId { get; init; }
    public IReadOnlyList<Guid> UpdatedTargetIds { get; init; } = Array.Empty<Guid>();
}

public sealed class PanelCladdingClearPanelSnapshot
{
    public Guid ObjectId { get; init; }
    public IReadOnlyDictionary<string, string> UserText { get; init; } =
        new Dictionary<string, string>();
}

public sealed class PanelCladdingClearPanelPlan
{
    public Guid ObjectId { get; init; }
    public IReadOnlyList<string> UserTextDeletes { get; init; } = Array.Empty<string>();
}

public sealed class PanelCladdingClearPlan
{
    public IReadOnlyList<PanelCladdingClearPanelPlan> Panels { get; init; } =
        Array.Empty<PanelCladdingClearPanelPlan>();

    public int RemovedKeyCount => Panels.Sum(panel => panel.UserTextDeletes.Count);
}

public sealed class PanelCladdingClearResult
{
    public IReadOnlyList<Guid> SelectedObjectIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> UpdatedObjectIds { get; init; } = Array.Empty<Guid>();
    public int RemovedKeyCount { get; init; }
}

public sealed class PanelCladdingSurfaceSyncPanelSnapshot
{
    public Guid ObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public PanelCladdingLayout Layout { get; init; } = new();
}

public sealed class PanelCladdingSurfaceSyncSurfaceSnapshot
{
    public Guid ObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public string Cid { get; init; } = string.Empty;
    public string LayerPath { get; init; } = string.Empty;
    public string CladdingValue { get; init; } = string.Empty;
}

public sealed class PanelCladdingSurfaceSyncIssue
{
    public Guid PanelObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class PanelCladdingWorkbookTypeReference
{
    public Guid ObjectId { get; init; }
    public string TypeCode { get; init; } = string.Empty;
    public string StoredSignature { get; init; } = string.Empty;
}

public sealed class PanelCladdingSurfaceSyncSnapshot
{
    public string DocumentPath { get; init; } = string.Empty;
    public string WorkbookPath { get; init; } = string.Empty;
    public IReadOnlyList<Guid> SelectedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<PanelCladdingSurfaceSyncPanelSnapshot> Panels { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncPanelSnapshot>();
    public IReadOnlyList<PanelCladdingSurfaceSyncSurfaceSnapshot> Surfaces { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncSurfaceSnapshot>();
    public IReadOnlyList<PanelCladdingSurfaceSyncIssue> Issues { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncIssue>();
    public IReadOnlyList<PanelCladdingWorkbookTypeReference> ModelTypeAssignments { get; init; } =
        Array.Empty<PanelCladdingWorkbookTypeReference>();
}

public sealed class PanelCladdingSurfaceSyncSurfacePlan
{
    public Guid ObjectId { get; init; }
    public Guid PanelObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public string Cid { get; init; } = string.Empty;
    public string CellKey { get; init; } = string.Empty;
    public string ExpectedLayerPath { get; init; } = string.Empty;
    public string MaterialCode { get; init; } = string.Empty;
    public bool CladdingKeyChanged { get; init; }
}

public sealed class PanelCladdingSurfaceSyncPanelPlan
{
    public Guid ObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public PanelCladdingLayout Layout { get; init; } = new();
    public IReadOnlyDictionary<string, string> CellValues { get; init; } =
        new Dictionary<string, string>();
    public bool CladdingChanged { get; init; }
}

public sealed class PanelCladdingSurfaceSyncPlan
{
    public IReadOnlyList<Guid> SelectedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<PanelCladdingSurfaceSyncPanelPlan> Panels { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncPanelPlan>();
    public IReadOnlyList<PanelCladdingSurfaceSyncSurfacePlan> Surfaces { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncSurfacePlan>();
    public IReadOnlyList<PanelCladdingSurfaceSyncIssue> Issues { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncIssue>();
}

public sealed class PanelCladdingSurfaceSyncPanelWrite
{
    public Guid ObjectId { get; init; }
    public string ExpectedGeometryFingerprint { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> CellValues { get; init; } =
        new Dictionary<string, string>();
    public string TypeCode { get; init; } = string.Empty;
    public string StoredSignature { get; init; } = string.Empty;
}

public sealed class PanelCladdingSurfaceSyncCommitRequest
{
    public string FilePath { get; init; } = string.Empty;
    public string WorkbookPath { get; init; } = string.Empty;
    public IReadOnlyList<Guid> SelectedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> SkippedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<PanelCladdingSurfaceSyncIssue> Issues { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncIssue>();
    public IReadOnlyList<string> RemovedWorkbookTypeCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PanelCladdingSurfaceSyncSurfacePlan> SurfaceWrites { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncSurfacePlan>();
    public IReadOnlyList<PanelCladdingSurfaceSyncPanelWrite> PanelWrites { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncPanelWrite>();
}

public sealed class PanelCladdingSurfaceSyncTypeResult
{
    public Guid PanelObjectId { get; init; }
    public string TypeCode { get; init; } = string.Empty;
    public string StoredSignature { get; init; } = string.Empty;
    public string SheetName { get; init; } = string.Empty;
    public bool ReusedExistingType { get; init; }
}

public sealed class PanelCladdingSurfaceSyncResult
{
    public IReadOnlyList<Guid> SelectedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> SkippedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> ChangedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> RefreshedSurfaceIds { get; init; } = Array.Empty<Guid>();
    public int MatchedSurfaceCount { get; init; }
    public string WorkbookPath { get; init; } = string.Empty;
    public IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> Types { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncTypeResult>();
    public IReadOnlyList<PanelCladdingSurfaceSyncIssue> Issues { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncIssue>();
    public IReadOnlyList<string> RemovedWorkbookTypeCodes { get; init; } = Array.Empty<string>();
}

public sealed class PanelCladdingWorkbookBatchUpsert
{
    public string WorkbookPath { get; init; } = string.Empty;
    public bool AllowCreate { get; init; }
    public IReadOnlyList<PanelCladdingWorkbookUpsert> Items { get; init; } =
        Array.Empty<PanelCladdingWorkbookUpsert>();
    public bool PruneUnusedTypes { get; init; }
    public IReadOnlyList<PanelCladdingWorkbookTypeReference> RetainedTypes { get; init; } =
        Array.Empty<PanelCladdingWorkbookTypeReference>();
}

public sealed class PanelCladdingWorkbookBatchItemResult
{
    public Guid ObjectId { get; init; }
    public PanelCladdingWorkbookCommitResult Result { get; init; } = new();
}

public sealed class PanelAttributeCommitRequest
{
    public string FilePath { get; init; } = string.Empty;
    public Guid ObjectId { get; init; }
    public string ExpectedGeometryFingerprint { get; init; } = string.Empty;
    public IReadOnlyList<string> UserTextDeletes { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> UserTextWrites { get; init; } = new Dictionary<string, string>();
    public string? WorkbookPath { get; init; }
}

public sealed class PanelAttributeCommitResult
{
    public Guid ObjectId { get; init; }
    public bool Mutated { get; init; }
}

public sealed class PanelProjectedScene
{
    public IReadOnlyList<PanelProjectedTriangle> Triangles { get; init; } = Array.Empty<PanelProjectedTriangle>();
    public IReadOnlyList<IReadOnlyList<PanelPoint2>> GridPolylines { get; init; } = Array.Empty<IReadOnlyList<PanelPoint2>>();
    public IReadOnlyList<PanelProjectedCell> Cells { get; init; } = Array.Empty<PanelProjectedCell>();
    public double MinX { get; init; }
    public double MaxX { get; init; }
    public double MinY { get; init; }
    public double MaxY { get; init; }
}

public sealed class PanelProjectedTriangle
{
    public IReadOnlyList<PanelPoint2> Points { get; init; } = Array.Empty<PanelPoint2>();
    public double Depth { get; init; }
    public byte Shade { get; init; }
}

public sealed class PanelProjectedCell
{
    public string UserTextKey { get; init; } = string.Empty;
    public PanelPoint2 Center { get; init; }
    public IReadOnlyList<PanelPoint2> Boundary { get; init; } = Array.Empty<PanelPoint2>();
}

