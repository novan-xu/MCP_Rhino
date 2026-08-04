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

public sealed class PanelAttributeCommitRequest
{
    public string FilePath { get; init; } = string.Empty;
    public Guid ObjectId { get; init; }
    public string ExpectedGeometryFingerprint { get; init; } = string.Empty;
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

