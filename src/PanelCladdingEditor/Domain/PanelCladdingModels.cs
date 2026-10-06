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
    public PanelCladdingTopologyState Topology { get; init; } = new();
    public PanelFrameAssignmentState FrameAssignments { get; init; } = new();
    public IReadOnlyDictionary<string, string> SourceUserText { get; init; } =
        new Dictionary<string, string>();
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
    public PanelCladdingTopologyState Topology { get; init; } = new();
    public PanelFrameAssignmentState FrameAssignments { get; init; } = new();
}

public enum PanelCladdingTopologyAxis
{
    Horizontal,
    Vertical
}

public enum PanelCladdingObjectScope
{
    Surfaces,
    Curves
}

public enum PanelCladdingSaveScope
{
    Both,
    Extrusions,
    Cladding
}

public enum PanelCladdingCurveTemplatePriority
{
    Horizontal,
    Vertical
}

public readonly record struct PanelCladdingSegmentCoordinate(
    PanelCladdingTopologyAxis Axis,
    int Track,
    int Bay);

public sealed record PanelCladdingMergeRun(
    PanelCladdingTopologyAxis Axis,
    int Track,
    int StartBay,
    int EndBay);

public sealed class PanelCladdingTopologyState
{
    public IReadOnlyList<PanelCladdingSegmentCoordinate> MissingSegments { get; init; } =
        Array.Empty<PanelCladdingSegmentCoordinate>();
    public IReadOnlyList<PanelCladdingSegmentCoordinate> HiddenSegments { get; init; } =
        Array.Empty<PanelCladdingSegmentCoordinate>();
    public IReadOnlyList<PanelCladdingMergeRun> MergeRuns { get; init; } =
        Array.Empty<PanelCladdingMergeRun>();
}

public sealed class PanelCladdingTopologyPayloads
{
    public string SegmentMask { get; init; } = string.Empty;
    public string MergeMask { get; init; } = string.Empty;
    public string HideMask { get; init; } = string.Empty;
}

public sealed class PanelFrameAssignmentState
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FrameAssignments { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<PanelFrameSegmentAssignment> SegmentAssignments { get; init; } =
        Array.Empty<PanelFrameSegmentAssignment>();
    public IReadOnlyDictionary<string, PanelFrameProfileDefinition> Definitions { get; init; } =
        new Dictionary<string, PanelFrameProfileDefinition>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, double> CurveModifiers { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    public bool IsEmpty => FrameAssignments.Count == 0 && SegmentAssignments.Count == 0;
}

public sealed record PanelFrameSegmentAssignment(
    PanelCladdingSegmentCoordinate Segment,
    IReadOnlyList<string> Codes);

public enum PanelFrameProfileDimension
{
    OneDimensional,
    ZeroDimensional
}

public enum PanelFrameProfileCalculation
{
    Length,
    FixedQuantity,
    Spacing
}

public sealed class PanelFrameProfileDefinition
{
    public string Code { get; init; } = string.Empty;
    public string BaseCode { get; init; } = string.Empty;
    public string SourceCode { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public PanelFrameProfileDimension Dimension { get; init; }
    public PanelFrameProfileCalculation Calculation { get; init; } = PanelFrameProfileCalculation.Length;
    public double? CalculationValue { get; init; }
    public string ParentCode { get; init; } = string.Empty;
}

public sealed class PanelFrameTypologyIdentity
{
    public int SchemaVersion { get; init; } = 1;
    public string TypologyCode { get; init; } = string.Empty;
    public string FullDigest { get; init; } = string.Empty;
    public string CanonicalPayload { get; init; } = string.Empty;
}

public sealed class PanelCladdingAxisCorrespondence
{
    public IReadOnlyList<int?> OldToNewTracks { get; init; } = Array.Empty<int?>();
    public IReadOnlyList<int?> NewToOldTracks { get; init; } = Array.Empty<int?>();
    public IReadOnlyList<int> NewBayToOldBay { get; init; } = Array.Empty<int>();
}

public sealed class PanelCladdingLayoutReconciliationResult
{
    public PanelCladdingAxisCorrespondence Horizontal { get; init; } = new();
    public PanelCladdingAxisCorrespondence Vertical { get; init; } = new();
    public PanelCladdingTopologyState Topology { get; init; } = new();
}

public sealed class PanelCladdingMatchMapping
{
    public IReadOnlyDictionary<string, string> TargetCellValues { get; init; } =
        new Dictionary<string, string>();
    public string CladdingLogic { get; init; } = string.Empty;
}

public sealed class PanelCladdingInferredOffsets
{
    public IReadOnlyList<double> HorizontalOffsets { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> VerticalOffsets { get; init; } = Array.Empty<double>();
}

public sealed class PanelCladdingRegion
{
    public string OwnerCellLabel { get; init; } = string.Empty;
    public string MaterialCode { get; init; } = string.Empty;
    public IReadOnlyList<PanelCladdingCell> Cells { get; init; } = Array.Empty<PanelCladdingCell>();
}

public sealed class PanelCladdingRegionSet
{
    public IReadOnlyList<PanelCladdingRegion> Regions { get; init; } = Array.Empty<PanelCladdingRegion>();
    public IReadOnlyDictionary<string, string> NormalizedCellValues { get; init; } =
        new Dictionary<string, string>();
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

public sealed class PanelCladdingInferredCurveGeometry
{
    public int SourceIndex { get; init; }
    public string FrameCode { get; init; } = string.Empty;
    public PanelCladdingTopologyAxis Axis { get; init; }
    public IReadOnlyList<PanelCladdingSegmentCoordinate> AtomicSegments { get; init; } =
        Array.Empty<PanelCladdingSegmentCoordinate>();
}

public sealed class PanelCladdingInferredExtrusionLayout
{
    public PanelCladdingTopologyState Topology { get; init; } = new();
    public IReadOnlyList<PanelCladdingInferredCurveGeometry> Curves { get; init; } =
        Array.Empty<PanelCladdingInferredCurveGeometry>();
}

public sealed class PanelCladdingMaterialCatalogItem
{
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string ColorHex { get; init; } = string.Empty;
}

public sealed class PanelCladdingMaterialCatalog
{
    public string WorkbookPath { get; init; } = string.Empty;
    public IReadOnlyList<PanelCladdingMaterialCatalogItem> Materials { get; init; } =
        Array.Empty<PanelCladdingMaterialCatalogItem>();
    public bool UsesLegacyTypeFallback { get; init; }
}

public sealed class PanelCladdingMaterialCatalogSaveRequest
{
    public string WorkbookPath { get; init; } = string.Empty;
    public bool AllowCreate { get; init; }
    public bool RemoveLegacyTypeSheets { get; init; } = true;
    public IReadOnlyList<PanelCladdingMaterialCatalogItem> Materials { get; init; } =
        Array.Empty<PanelCladdingMaterialCatalogItem>();
}

public sealed class PanelCladdingMaterialCatalogSaveResult
{
    public string WorkbookPath { get; init; } = string.Empty;
    public int MaterialCount { get; init; }
    public int RemovedLegacyTypeSheetCount { get; init; }
}

public sealed class PanelFrameExtrusionCatalogItem
{
    public string Code { get; init; } = string.Empty;
    public string BaseCode { get; init; } = string.Empty;
    public string SourceCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public PanelFrameProfileDimension? Dimension { get; init; }
    public PanelFrameProfileCalculation Calculation { get; init; } = PanelFrameProfileCalculation.Length;
    public double? CalculationValue { get; init; }
    public string ParentCode { get; init; } = string.Empty;
    public string SourcePdfPath { get; init; } = string.Empty;
    public int SourcePageNumber { get; init; }
    public byte[] ThumbnailPng { get; init; } = Array.Empty<byte>();
}

public sealed class PanelFrameExtrusionCatalog
{
    public string WorkbookPath { get; init; } = string.Empty;
    public IReadOnlyList<PanelFrameExtrusionCatalogItem> Extrusions { get; init; } =
        Array.Empty<PanelFrameExtrusionCatalogItem>();
}

public sealed class PanelFrameExtrusionCatalogSaveRequest
{
    public string WorkbookPath { get; init; } = string.Empty;
    public bool AllowCreate { get; init; }
    public IReadOnlyList<PanelFrameExtrusionCatalogItem> Extrusions { get; init; } =
        Array.Empty<PanelFrameExtrusionCatalogItem>();
}

public sealed class PanelFrameExtrusionCatalogSaveResult
{
    public string WorkbookPath { get; init; } = string.Empty;
    public int ExtrusionCount { get; init; }
}

public sealed class PanelFrameExtrusionScheduleImportRequest
{
    public string PdfPath { get; init; } = string.Empty;
}

public sealed class PanelFrameExtrusionScheduleImportResult
{
    public string PdfPath { get; init; } = string.Empty;
    public IReadOnlyList<PanelFrameExtrusionCatalogItem> Extrusions { get; init; } =
        Array.Empty<PanelFrameExtrusionCatalogItem>();
    public IReadOnlyList<int> ImportedPageNumbers { get; init; } = Array.Empty<int>();
    public bool UsedFramingPageFilter { get; init; }
}

public sealed class PanelCladdingSaveRequest
{
    public string FilePath { get; init; } = string.Empty;
    public Guid ObjectId { get; init; }
    public string ExpectedGeometryFingerprint { get; init; } = string.Empty;
    public string WorkbookPath { get; init; } = string.Empty;
    public bool AllowCreateWorkbook { get; init; }
    public string SystemCode { get; init; } = string.Empty;
    public IReadOnlyList<double>? HorizontalOffsets { get; init; }
    public IReadOnlyList<double>? VerticalOffsets { get; init; }
    public PanelCladdingTopologyState? Topology { get; init; }
    public PanelFrameAssignmentState? FrameAssignments { get; init; }
    public IReadOnlyDictionary<string, string> CellValues { get; init; } = new Dictionary<string, string>();
    public PanelCladdingSaveScope Scope { get; init; } = PanelCladdingSaveScope.Both;
}

public sealed class PanelCladdingSaveResult
{
    public Guid ObjectId { get; init; }
    public string TypeCode { get; init; } = string.Empty;
    public string FrameTypology { get; init; } = string.Empty;
    public string StoredSignature { get; init; } = string.Empty;
    public string WorkbookPath { get; init; } = string.Empty;
    public string SheetName { get; init; } = string.Empty;
    public bool ReusedExistingType { get; init; }
}

public sealed class PanelCladdingSpawnRegionPlan
{
    public string OwnerCellLabel { get; init; } = string.Empty;
    public IReadOnlyList<PanelCladdingCell> Cells { get; init; } = Array.Empty<PanelCladdingCell>();
    public string CladdingCode { get; init; } = string.Empty;
    public string Cid { get; init; } = string.Empty;
    public string LayerPath { get; init; } = string.Empty;
    public PanelColorRgb LayerColor { get; init; }
    public IReadOnlyDictionary<string, string> UserTextWrites { get; init; } =
        new Dictionary<string, string>();
}

public enum PanelCladdingExtrusionCurveKind
{
    Frame,
    Segment,
    Merged
}

public sealed class PanelCladdingExtrusionCurvePlan
{
    public string Code { get; init; } = string.Empty;
    public PanelCladdingExtrusionCurveKind Kind { get; init; }
    public PanelCladdingTopologyAxis Axis { get; init; }
    public double Offset { get; init; }
    public double Start { get; init; }
    public double End { get; init; }
    public IReadOnlyList<PanelCladdingSegmentCoordinate> AtomicSegments { get; init; } =
        Array.Empty<PanelCladdingSegmentCoordinate>();
    public IReadOnlyList<string> AssignedExtrusionCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> AssignedExtrusionValues { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string Cid { get; init; } = string.Empty;
    public string LayerPath { get; init; } = string.Empty;
    public PanelColorRgb ObjectColor { get; init; }
    public IReadOnlyDictionary<string, string> UserTextWrites { get; init; } =
        new Dictionary<string, string>();
}

public sealed class PanelCladdingSpawnPlan
{
    public string PanelId { get; init; } = string.Empty;
    public IReadOnlyList<PanelCladdingSpawnRegionPlan> Regions { get; init; } =
        Array.Empty<PanelCladdingSpawnRegionPlan>();
    public IReadOnlyList<PanelCladdingExtrusionCurvePlan> Curves { get; init; } =
        Array.Empty<PanelCladdingExtrusionCurvePlan>();
}

public sealed class PanelCladdingSpawnResult
{
    public IReadOnlyList<Guid> SourcePanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> CreatedObjectIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> CreatedSurfaceIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> CreatedCurveIds { get; init; } = Array.Empty<Guid>();
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

public sealed class PanelCladdingCreateGuideSnapshot
{
    public Guid ObjectId { get; init; }
    public bool IsOnPanel { get; init; } = true;
    public IReadOnlyList<PanelPoint3> Samples { get; init; } = Array.Empty<PanelPoint3>();
}

public sealed class PanelCladdingCreatePanelSnapshot
{
    public Guid ObjectId { get; init; }
    public double XMinimum { get; init; }
    public double XMaximum { get; init; }
    public double YMinimum { get; init; }
    public double YMaximum { get; init; }
    public double ZMinimum { get; init; }
    public double ZMaximum { get; init; }
    public double Tolerance { get; init; }
    public IReadOnlyList<PanelCladdingCreateGuideSnapshot> Guides { get; init; } =
        Array.Empty<PanelCladdingCreateGuideSnapshot>();
    public IReadOnlyDictionary<string, string> UserText { get; init; } =
        new Dictionary<string, string>();
}

public sealed class PanelCladdingCreatePanelPlan
{
    public Guid ObjectId { get; init; }
    public IReadOnlyList<double> HorizontalOffsets { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> VerticalOffsets { get; init; } = Array.Empty<double>();
    public IReadOnlyList<string> UserTextDeletes { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> UserTextWrites { get; init; } =
        new Dictionary<string, string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public int CellCount => (HorizontalOffsets.Count + 1) * (VerticalOffsets.Count + 1);
}

public sealed class PanelCladdingCreatePlan
{
    public IReadOnlyList<PanelCladdingCreatePanelPlan> Panels { get; init; } =
        Array.Empty<PanelCladdingCreatePanelPlan>();
}

public sealed class PanelCladdingCreateResult
{
    public IReadOnlyList<Guid> SelectedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> UpdatedPanelIds { get; init; } = Array.Empty<Guid>();
    public int HorizontalOffsetCount { get; init; }
    public int VerticalOffsetCount { get; init; }
    public int CellCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class PanelCladdingCurveTemplatePanelSnapshot
{
    public Guid ObjectId { get; init; }
    public int HorizontalTrackCount { get; init; }
    public int VerticalTrackCount { get; init; }
    public bool HasMergeMask { get; init; }
    public PanelCladdingTopologyState Topology { get; init; } = new();
}

public sealed class PanelCladdingCurveTemplatePanelPlan
{
    public Guid ObjectId { get; init; }
    public string MergeMask { get; init; } = string.Empty;
    public IReadOnlyList<string> UserTextDeletes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PanelCladdingMergeRun> MergeRuns { get; init; } =
        Array.Empty<PanelCladdingMergeRun>();
}

public sealed class PanelCladdingCurveTemplatePlan
{
    public PanelCladdingCurveTemplatePriority Priority { get; init; }
    public IReadOnlyList<PanelCladdingCurveTemplatePanelPlan> Panels { get; init; } =
        Array.Empty<PanelCladdingCurveTemplatePanelPlan>();
    public IReadOnlyList<Guid> SkippedPanelIds { get; init; } = Array.Empty<Guid>();
}

public sealed class PanelCladdingCurveTemplateResult
{
    public PanelCladdingCurveTemplatePriority Priority { get; init; }
    public IReadOnlyList<Guid> SelectedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> UpdatedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> SkippedPanelIds { get; init; } = Array.Empty<Guid>();
    public int MergeRunCount { get; init; }
}

public sealed class PanelCladdingSurfaceSyncPanelSnapshot
{
    public Guid ObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public string PanelCid { get; init; } = string.Empty;
    public PanelCladdingLayout Layout { get; init; } = new();
    public bool GridChanged { get; init; }
}

public sealed class PanelCladdingSurfaceSyncSurfaceSnapshot
{
    public Guid ObjectId { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public Guid PanelObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public string Cid { get; init; } = string.Empty;
    public string ReleaseNumber { get; init; } = string.Empty;
    public string LayerPath { get; init; } = string.Empty;
    public string CladdingValue { get; init; } = string.Empty;
    public string CoverageValue { get; init; } = string.Empty;
    public string LegacyCoverageValue { get; init; } = string.Empty;
    public IReadOnlyList<string> CoveredCellLabels { get; init; } = Array.Empty<string>();
}

public sealed class PanelCladdingSurfaceSyncCurveSnapshot
{
    public Guid ObjectId { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public Guid PanelObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public string Cid { get; init; } = string.Empty;
    public string ReleaseNumber { get; init; } = string.Empty;
    public string CurveCode { get; init; } = string.Empty;
    public string DesiredCode { get; init; } = string.Empty;
    public string LayerPath { get; init; } = string.Empty;
    public string AssignedExtrusions { get; init; } = string.Empty;
    public string DesiredAssignedExtrusions { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> AssignedExtrusionValues { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> DesiredAssignedExtrusionValues { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public bool UsesObjectColor { get; init; }
    public PanelColorRgb ObjectColor { get; init; }
    public PanelColorRgb DesiredObjectColor { get; init; }
    public bool ObjectColorChanged => !UsesObjectColor || ObjectColor != DesiredObjectColor;
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
    public PanelCladdingObjectScope Scope { get; init; } = PanelCladdingObjectScope.Surfaces;
    public string DocumentPath { get; init; } = string.Empty;
    public string WorkbookPath { get; init; } = string.Empty;
    public IReadOnlyList<Guid> SelectedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<PanelCladdingSurfaceSyncPanelSnapshot> Panels { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncPanelSnapshot>();
    public IReadOnlyList<PanelCladdingSurfaceSyncSurfaceSnapshot> Surfaces { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncSurfaceSnapshot>();
    public IReadOnlyList<PanelCladdingSurfaceSyncCurveSnapshot> Curves { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncCurveSnapshot>();
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
    public string ExpectedCid { get; init; } = string.Empty;
    public string DesiredCid { get; init; } = string.Empty;
    public string DesiredReleaseNumber { get; init; } = string.Empty;
    public string CellKey { get; init; } = string.Empty;
    public IReadOnlyList<string> CoveredCellLabels { get; init; } = Array.Empty<string>();
    public string ExpectedLayerPath { get; init; } = string.Empty;
    public string MaterialCode { get; init; } = string.Empty;
    public string DesiredCoverageValue { get; init; } = string.Empty;
    public bool CladdingKeyChanged { get; init; }
    public bool CoverageChanged { get; init; }
    public bool CidChanged { get; init; }
    public bool PidChanged { get; init; }
    public bool ReleaseChanged { get; init; }
    public bool NameChanged { get; init; }
    public bool MetadataChanged => CladdingKeyChanged || CoverageChanged || CidChanged || PidChanged || ReleaseChanged || NameChanged;
}

public sealed class PanelCladdingSurfaceSyncPanelPlan
{
    public Guid ObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public PanelCladdingLayout Layout { get; init; } = new();
    public IReadOnlyDictionary<string, string> CellValues { get; init; } =
        new Dictionary<string, string>();
    public string CladdingLogic { get; init; } = string.Empty;
    public bool CladdingChanged { get; init; }
    public bool NameChanged { get; init; }
}

public sealed class PanelCladdingSurfaceSyncCurvePlan
{
    public Guid ObjectId { get; init; }
    public Guid PanelObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public string ExpectedLayerPath { get; init; } = string.Empty;
    public string DesiredCode { get; init; } = string.Empty;
    public string DesiredCid { get; init; } = string.Empty;
    public string DesiredReleaseNumber { get; init; } = string.Empty;
    public string DesiredAssignedExtrusions { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> DesiredAssignedExtrusionValues { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public PanelColorRgb DesiredObjectColor { get; init; }
    public bool MetadataChanged { get; init; }
}

public sealed class PanelCladdingSurfaceSyncPlan
{
    public PanelCladdingObjectScope Scope { get; init; } = PanelCladdingObjectScope.Surfaces;
    public IReadOnlyList<Guid> SelectedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<PanelCladdingSurfaceSyncPanelPlan> Panels { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncPanelPlan>();
    public IReadOnlyList<PanelCladdingSurfaceSyncSurfacePlan> Surfaces { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncSurfacePlan>();
    public IReadOnlyList<PanelCladdingSurfaceSyncCurvePlan> Curves { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncCurvePlan>();
    public IReadOnlyList<PanelCladdingSurfaceSyncIssue> Issues { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncIssue>();
}

public sealed class PanelCladdingSurfaceSyncPanelWrite
{
    public Guid ObjectId { get; init; }
    public string ExpectedGeometryFingerprint { get; init; } = string.Empty;
    public IReadOnlyList<double> HorizontalOffsets { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> VerticalOffsets { get; init; } = Array.Empty<double>();
    public PanelCladdingTopologyState Topology { get; init; } = new();
    public IReadOnlyDictionary<string, string> CellValues { get; init; } =
        new Dictionary<string, string>();
    public string CladdingLogic { get; init; } = string.Empty;
    public string TypeCode { get; init; } = string.Empty;
    public string StoredSignature { get; init; } = string.Empty;
    public string UnitWidth { get; init; } = string.Empty;
    public string UnitHeight { get; init; } = string.Empty;
    public string UnitDimension { get; init; } = string.Empty;
}

public sealed class PanelCladdingSurfaceSyncCommitRequest
{
    public PanelCladdingObjectScope Scope { get; init; } = PanelCladdingObjectScope.Surfaces;
    public string FilePath { get; init; } = string.Empty;
    public string WorkbookPath { get; init; } = string.Empty;
    public IReadOnlyList<Guid> SelectedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> SkippedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<PanelCladdingSurfaceSyncIssue> Issues { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncIssue>();
    public IReadOnlyList<string> RemovedWorkbookTypeCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PanelCladdingSurfaceSyncSurfacePlan> SurfaceWrites { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncSurfacePlan>();
    public IReadOnlyList<PanelCladdingSurfaceSyncCurvePlan> CurveWrites { get; init; } =
        Array.Empty<PanelCladdingSurfaceSyncCurvePlan>();
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
    public PanelCladdingObjectScope Scope { get; init; } = PanelCladdingObjectScope.Surfaces;
    public IReadOnlyList<Guid> SelectedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> SkippedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> ChangedPanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> RefreshedSurfaceIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> RefreshedCurveIds { get; init; } = Array.Empty<Guid>();
    public int MatchedSurfaceCount { get; init; }
    public int MatchedCurveCount { get; init; }
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

