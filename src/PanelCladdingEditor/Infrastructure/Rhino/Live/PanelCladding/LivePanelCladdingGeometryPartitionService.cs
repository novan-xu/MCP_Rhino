extern alias rhinocommon;

using System.Globalization;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using AreaMassProperties = rhinocommon::Rhino.Geometry.AreaMassProperties;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using BrepEdge = rhinocommon::Rhino.Geometry.BrepEdge;
using EdgeAdjacency = rhinocommon::Rhino.Geometry.EdgeAdjacency;
using Mesh = rhinocommon::Rhino.Geometry.Mesh;
using MeshingParameters = rhinocommon::Rhino.Geometry.MeshingParameters;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PlaneFitResult = rhinocommon::Rhino.Geometry.PlaneFitResult;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

internal static partial class LivePanelCladdingGeometryPartitionService
{
    public static OperationResponse<PanelCladdingInferredOffsets> InferOffsets(
        Brep source,
        IReadOnlyList<Brep> claddingSurfaces,
        double tolerance)
    {
        if (claddingSurfaces is null || claddingSurfaces.Count == 0)
        {
            return OperationResponse<PanelCladdingInferredOffsets>.Fail(
                "PANEL_CLADDING_SURFACE_SYNC_SURFACE_MISSING");
        }

        OperationResponse<LocalPanelFrame> frameResponse = BuildLocalFrame(source, tolerance);
        if (!frameResponse.Success || frameResponse.Data is null)
        {
            return OperationResponse<PanelCladdingInferredOffsets>.Fail(frameResponse.Message);
        }
        LocalPanelFrame local = frameResponse.Data;
        double width = local.XMax - local.XMin;
        double height = local.YMax - local.YMin;
        double axisTolerance = Math.Max(
            tolerance * 10d,
            Math.Max(width, height) * 1e-7d);
        var verticalCandidates = new List<double>();
        var horizontalCandidates = new List<double>();

        foreach (Brep surface in claddingSurfaces)
        {
            foreach (BrepEdge edge in surface.Edges)
            {
                if (edge.Valence != EdgeAdjacency.Naked)
                {
                    continue;
                }

                const int sampleCount = 8;
                var samples = new List<(double X, double Y)>(sampleCount + 1);
                for (int index = 0; index <= sampleCount; index++)
                {
                    double parameter = edge.Domain.ParameterAt((double)index / sampleCount);
                    Point3d point = edge.PointAt(parameter);
                    Vector3d delta = point - local.Frame.Origin;
                    samples.Add((delta * local.Frame.XAxis, delta * local.Frame.YAxis));
                }

                double xMin = samples.Min(point => point.X);
                double xMax = samples.Max(point => point.X);
                double yMin = samples.Min(point => point.Y);
                double yMax = samples.Max(point => point.Y);
                bool constantX = xMax - xMin <= axisTolerance;
                bool constantY = yMax - yMin <= axisTolerance;
                if (constantX && yMax - yMin > axisTolerance)
                {
                    double relative = samples.Average(point => point.X) - local.XMin;
                    if (relative > axisTolerance && relative < width - axisTolerance)
                    {
                        verticalCandidates.Add(relative);
                    }
                }
                else if (constantY && xMax - xMin > axisTolerance)
                {
                    double relative = samples.Average(point => point.Y) - local.YMin;
                    if (relative > axisTolerance && relative < height - axisTolerance)
                    {
                        horizontalCandidates.Add(relative);
                    }
                }
            }
        }

        return OperationResponse<PanelCladdingInferredOffsets>.Ok(
            new PanelCladdingInferredOffsets
            {
                HorizontalOffsets = ClusterOffsets(horizontalCandidates, axisTolerance),
                VerticalOffsets = ClusterOffsets(verticalCandidates, axisTolerance)
            });
    }

    public static OperationResponse<LivePanelCladdingGeometryGrid> CreateGrid(
        Brep source,
        PanelCladdingKeySet keySet,
        double tolerance)
    {
        OperationResponse<LocalPanelFrame> frameResponse = BuildLocalFrame(source, tolerance);
        if (!frameResponse.Success || frameResponse.Data is null)
        {
            return OperationResponse<LivePanelCladdingGeometryGrid>.Fail(frameResponse.Message);
        }
        LocalPanelFrame local = frameResponse.Data;
        double[] xBoundaries = new[] { local.XMin }
            .Concat(keySet.VerticalOffsets.Select(offset => local.XMin + offset))
            .Concat(new[] { local.XMax })
            .ToArray();
        double[] yBoundaries = new[] { local.YMin }
            .Concat(keySet.HorizontalOffsets.Select(offset => local.YMin + offset))
            .Concat(new[] { local.YMax })
            .ToArray();

        var cells = new List<LivePanelCladdingGeometryCell>(keySet.Cells.Count);
        foreach (PanelCladdingCell cell in keySet.Cells)
        {
            OperationResponse<Brep> geometry = CreateCellGeometry(
                source,
                local.Frame,
                xBoundaries[cell.Column],
                xBoundaries[cell.Column + 1],
                yBoundaries[cell.Row],
                yBoundaries[cell.Row + 1],
                cell.ShortLabel,
                tolerance,
                cell.Column > 0,
                cell.Column < keySet.VerticalOffsets.Count,
                cell.Row > 0,
                cell.Row < keySet.HorizontalOffsets.Count);
            if (!geometry.Success || geometry.Data is null)
            {
                DisposeCells(cells);
                return OperationResponse<LivePanelCladdingGeometryGrid>.Fail(geometry.Message);
            }

            OperationResponse<LivePanelCladdingGeometryCell> prepared = PrepareCell(cell, geometry.Data);
            if (!prepared.Success || prepared.Data is null)
            {
                geometry.Data.Dispose();
                DisposeCells(cells);
                return OperationResponse<LivePanelCladdingGeometryGrid>.Fail(prepared.Message);
            }
            cells.Add(prepared.Data);
        }

        return OperationResponse<LivePanelCladdingGeometryGrid>.Ok(
            new LivePanelCladdingGeometryGrid(cells));
    }

    public static OperationResponse<Brep> JoinRegion(
        LivePanelCladdingGeometryGrid grid,
        IReadOnlyList<PanelCladdingCell> regionCells,
        double tolerance,
        string cid)
    {
        var pieces = new List<Brep>(regionCells.Count);
        try
        {
            foreach (PanelCladdingCell cell in regionCells)
            {
                LivePanelCladdingGeometryCell? geometryCell = grid.Cells.FirstOrDefault(item =>
                    string.Equals(item.Cell.ShortLabel, cell.ShortLabel, StringComparison.OrdinalIgnoreCase));
                if (geometryCell is null)
                {
                    return OperationResponse<Brep>.Fail(
                        $"PANEL_CLADDING_REGION_CELL_GEOMETRY_MISSING: {cid}: {cell.ShortLabel}");
                }
                pieces.Add(geometryCell.Geometry.DuplicateBrep());
            }
            if (pieces.Count == 1)
            {
                Brep result = pieces[0];
                pieces.Clear();
                return OperationResponse<Brep>.Ok(result);
            }

            Brep[] joined = Brep.JoinBreps(pieces, tolerance);
            if (joined.Length != 1)
            {
                foreach (Brep result in joined)
                {
                    result.Dispose();
                }
                return OperationResponse<Brep>.Fail(
                    $"PANEL_CLADDING_REGION_JOIN_FAILED: {cid} produced {joined.Length} Breps.");
            }
            return OperationResponse<Brep>.Ok(joined[0]);
        }
        finally
        {
            foreach (Brep piece in pieces)
            {
                piece.Dispose();
            }
        }
    }

    public static OperationResponse<IReadOnlyList<string>> ResolveCoveredCellLabels(
        Brep surface,
        LivePanelCladdingGeometryGrid grid,
        double tolerance,
        string surfaceIdentity)
    {
        double distanceTolerance = Math.Max(tolerance * 5d, 1e-6d);
        var covered = new List<LivePanelCladdingGeometryCell>();
        foreach (LivePanelCladdingGeometryCell cell in grid.Cells)
        {
            int matchingSamples = cell.Samples.Count(sample =>
            {
                Point3d closest = surface.ClosestPoint(sample);
                return closest.IsValid && closest.DistanceTo(sample) <= distanceTolerance;
            });
            if (matchingSamples > 0 && matchingSamples < cell.Samples.Count)
            {
                return OperationResponse<IReadOnlyList<string>>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_PARTIAL_CELL: {surfaceIdentity}: {cell.Cell.ShortLabel}");
            }
            if (matchingSamples == cell.Samples.Count)
            {
                covered.Add(cell);
            }
        }
        if (covered.Count == 0)
        {
            return OperationResponse<IReadOnlyList<string>>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_COVERAGE_NOT_FOUND: {surfaceIdentity}");
        }

        using AreaMassProperties? surfaceArea = AreaMassProperties.Compute(surface);
        if (surfaceArea is null)
        {
            return OperationResponse<IReadOnlyList<string>>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_AREA_FAILED: {surfaceIdentity}");
        }
        double coveredArea = covered.Sum(cell => cell.Area);
        double areaTolerance = Math.Max(
            tolerance * tolerance * 100d,
            Math.Max(surfaceArea.Area, coveredArea) * 1e-4d);
        if (Math.Abs(surfaceArea.Area - coveredArea) > areaTolerance)
        {
            return OperationResponse<IReadOnlyList<string>>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_COVERAGE_AREA_MISMATCH: {surfaceIdentity}: " +
                $"surface={surfaceArea.Area.ToString("G17", CultureInfo.InvariantCulture)}, " +
                $"cells={coveredArea.ToString("G17", CultureInfo.InvariantCulture)}");
        }

        return OperationResponse<IReadOnlyList<string>>.Ok(covered
            .OrderBy(cell => cell.Cell.Row)
            .ThenBy(cell => cell.Cell.Column)
            .Select(cell => cell.Cell.ShortLabel)
            .ToArray());
    }

    private static OperationResponse<LivePanelCladdingGeometryCell> PrepareCell(
        PanelCladdingCell cell,
        Brep geometry)
    {
        using AreaMassProperties? properties = AreaMassProperties.Compute(geometry);
        if (properties is null)
        {
            return OperationResponse<LivePanelCladdingGeometryCell>.Fail(
                $"PANEL_CLADDING_CELL_AREA_FAILED: {cell.ShortLabel}");
        }

        var samples = new List<Point3d> { properties.Centroid };
        Mesh[] meshes = Mesh.CreateFromBrep(geometry, MeshingParameters.FastRenderMesh);
        if (meshes.Length == 0)
        {
            meshes = Mesh.CreateFromBrep(geometry, MeshingParameters.Default);
        }
        try
        {
            int totalFaceCount = meshes.Sum(mesh => mesh.Faces.Count);
            int stride = Math.Max(1, totalFaceCount / 64);
            int faceNumber = 0;
            foreach (Mesh mesh in meshes)
            {
                for (int index = 0; index < mesh.Faces.Count; index++, faceNumber++)
                {
                    if (faceNumber % stride != 0)
                    {
                        continue;
                    }
                    var face = mesh.Faces[index];
                    Point3d a = new(mesh.Vertices[face.A]);
                    Point3d b = new(mesh.Vertices[face.B]);
                    Point3d c = new(mesh.Vertices[face.C]);
                    if (face.IsQuad)
                    {
                        Point3d d = new(mesh.Vertices[face.D]);
                        samples.Add(new Point3d(
                            (a.X + b.X + c.X + d.X) / 4d,
                            (a.Y + b.Y + c.Y + d.Y) / 4d,
                            (a.Z + b.Z + c.Z + d.Z) / 4d));
                    }
                    else
                    {
                        samples.Add(new Point3d(
                            (a.X + b.X + c.X) / 3d,
                            (a.Y + b.Y + c.Y) / 3d,
                            (a.Z + b.Z + c.Z) / 3d));
                    }
                }
            }
        }
        finally
        {
            foreach (Mesh mesh in meshes)
            {
                mesh.Dispose();
            }
        }

        return OperationResponse<LivePanelCladdingGeometryCell>.Ok(
            new LivePanelCladdingGeometryCell(cell, geometry, properties.Area, samples));
    }

    private static IReadOnlyList<double> ClusterOffsets(
        IReadOnlyList<double> candidates,
        double tolerance)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<double>();
        }

        double[] ordered = candidates.OrderBy(value => value).ToArray();
        var clusters = new List<List<double>> { new() { ordered[0] } };
        for (int index = 1; index < ordered.Length; index++)
        {
            List<double> current = clusters[^1];
            if (ordered[index] <= current.Average() + tolerance)
            {
                current.Add(ordered[index]);
            }
            else
            {
                clusters.Add(new List<double> { ordered[index] });
            }
        }
        return clusters.Select(cluster => cluster.Average()).ToArray();
    }

    private static OperationResponse<Brep> CreateCellGeometry(
        Brep source,
        Plane frame,
        double left,
        double right,
        double bottom,
        double top,
        string cellLabel,
        double tolerance,
        bool trimLeft,
        bool trimRight,
        bool trimBottom,
        bool trimTop)
    {
        var pieces = new List<Brep> { source.DuplicateBrep() };
        try
        {
            if (trimLeft)
            {
                TrimPieces(pieces, new Plane(frame.PointAt(left, bottom), -frame.XAxis), tolerance);
            }
            if (trimRight)
            {
                TrimPieces(pieces, new Plane(frame.PointAt(right, bottom), frame.XAxis), tolerance);
            }
            if (trimBottom)
            {
                TrimPieces(pieces, new Plane(frame.PointAt(left, bottom), -frame.YAxis), tolerance);
            }
            if (trimTop)
            {
                TrimPieces(pieces, new Plane(frame.PointAt(left, top), frame.YAxis), tolerance);
            }

            var meaningful = new List<Brep>();
            foreach (Brep piece in pieces)
            {
                using AreaMassProperties? area = AreaMassProperties.Compute(piece);
                if (area is not null && area.Area > tolerance * tolerance)
                {
                    meaningful.Add(piece);
                }
            }
            if (meaningful.Count != 1)
            {
                return OperationResponse<Brep>.Fail(
                    $"PANEL_CLADDING_CELL_GEOMETRY_AMBIGUOUS: {cellLabel} produced {meaningful.Count} surface pieces.");
            }

            Brep result = meaningful[0];
            foreach (Brep piece in pieces.Where(piece => !ReferenceEquals(piece, result)))
            {
                piece.Dispose();
            }
            pieces.Clear();
            return OperationResponse<Brep>.Ok(result);
        }
        finally
        {
            foreach (Brep piece in pieces)
            {
                piece.Dispose();
            }
        }
    }

    private static void TrimPieces(List<Brep> pieces, Plane cutter, double tolerance)
    {
        var trimmed = new List<Brep>();
        foreach (Brep piece in pieces)
        {
            Brep[] results = piece.Trim(cutter, tolerance);
            if (results.Length > 0)
            {
                trimmed.AddRange(results);
            }
            piece.Dispose();
        }
        pieces.Clear();
        pieces.AddRange(trimmed);
    }

    private static OperationResponse<LocalPanelFrame> BuildLocalFrame(Brep brep, double tolerance)
    {
        Mesh[] pieces = Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh);
        if (pieces.Length == 0)
        {
            pieces = Mesh.CreateFromBrep(brep, MeshingParameters.Default);
        }
        if (pieces.Length == 0)
        {
            return OperationResponse<LocalPanelFrame>.Fail("PANEL_CLADDING_SPAWN_MESH_FAILED");
        }

        using var combined = new Mesh();
        foreach (Mesh piece in pieces)
        {
            combined.Append(piece);
            piece.Dispose();
        }
        combined.Compact();
        if (combined.Vertices.Count < 3 || combined.Faces.Count == 0)
        {
            return OperationResponse<LocalPanelFrame>.Fail("PANEL_CLADDING_SPAWN_MESH_EMPTY");
        }

        Plane frame;
        if (TryParseStoredPlane(brep.GetUserString("Plane"), out Plane stored))
        {
            frame = OrientFrame(stored);
        }
        else if (brep.Faces.Count > 0 && brep.Faces[0].TryGetPlane(out Plane planar, tolerance))
        {
            frame = OrientFrame(planar);
        }
        else
        {
            Point3d[] samples = combined.Vertices.Select(vertex => new Point3d(vertex)).ToArray();
            PlaneFitResult fit = Plane.FitPlaneToPoints(samples, out Plane fitted);
            if (fit == PlaneFitResult.Failure || !fitted.IsValid)
            {
                return OperationResponse<LocalPanelFrame>.Fail("PANEL_CLADDING_REFERENCE_PLANE_FAILED");
            }
            frame = OrientFrame(fitted);
        }

        var localPoints = new List<(double X, double Y)>(combined.Vertices.Count);
        foreach (var vertex in combined.Vertices)
        {
            Vector3d delta = new Point3d(vertex) - frame.Origin;
            localPoints.Add((delta * frame.XAxis, delta * frame.YAxis));
        }
        return OperationResponse<LocalPanelFrame>.Ok(new LocalPanelFrame
        {
            Frame = frame,
            XMin = localPoints.Min(point => point.X),
            XMax = localPoints.Max(point => point.X),
            YMin = localPoints.Min(point => point.Y),
            YMax = localPoints.Max(point => point.Y)
        });
    }

    private static Plane OrientFrame(Plane source)
    {
        Vector3d normal = source.ZAxis;
        normal.Unitize();
        Vector3d up = Vector3d.ZAxis - (Vector3d.ZAxis * normal) * normal;
        if (!up.Unitize())
        {
            up = source.YAxis;
            up.Unitize();
        }
        if (up * Vector3d.ZAxis < 0d)
        {
            up.Reverse();
        }
        Vector3d right = Vector3d.CrossProduct(up, normal);
        right.Unitize();
        Vector3d referenceRight = source.XAxis - (source.XAxis * normal) * normal -
            (source.XAxis * up) * up;
        if (referenceRight.Unitize() && right * referenceRight < 0d)
        {
            right.Reverse();
        }
        return new Plane(source.Origin, right, up);
    }

    private static bool TryParseStoredPlane(string? raw, out Plane plane)
    {
        plane = Plane.Unset;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }
        double[] values = NumberRegex().Matches(raw)
            .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))
            .ToArray();
        if (values.Length < 9)
        {
            return false;
        }
        var origin = new Point3d(values[0], values[1], values[2]);
        var right = new Vector3d(values[3], values[4], values[5]);
        var up = new Vector3d(values[6], values[7], values[8]);
        if (!right.Unitize() || !up.Unitize())
        {
            return false;
        }
        plane = new Plane(origin, right, up);
        return plane.IsValid;
    }

    private static void DisposeCells(IEnumerable<LivePanelCladdingGeometryCell> cells)
    {
        foreach (LivePanelCladdingGeometryCell cell in cells)
        {
            cell.Geometry.Dispose();
        }
    }

    [GeneratedRegex(@"[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    private sealed class LocalPanelFrame
    {
        public Plane Frame { get; init; } = Plane.Unset;
        public double XMin { get; init; }
        public double XMax { get; init; }
        public double YMin { get; init; }
        public double YMax { get; init; }
    }
}

internal sealed class LivePanelCladdingGeometryGrid : IDisposable
{
    public LivePanelCladdingGeometryGrid(IReadOnlyList<LivePanelCladdingGeometryCell> cells)
    {
        Cells = cells;
    }

    public IReadOnlyList<LivePanelCladdingGeometryCell> Cells { get; }

    public void Dispose()
    {
        foreach (LivePanelCladdingGeometryCell cell in Cells)
        {
            cell.Geometry.Dispose();
        }
    }
}

internal sealed record LivePanelCladdingGeometryCell(
    PanelCladdingCell Cell,
    Brep Geometry,
    double Area,
    IReadOnlyList<Point3d> Samples);
