extern alias rhinocommon;

using System.Globalization;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using AreaMassProperties = rhinocommon::Rhino.Geometry.AreaMassProperties;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using BrepEdge = rhinocommon::Rhino.Geometry.BrepEdge;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using CurveSimplifyOptions = rhinocommon::Rhino.Geometry.CurveSimplifyOptions;
using EdgeAdjacency = rhinocommon::Rhino.Geometry.EdgeAdjacency;
using Mesh = rhinocommon::Rhino.Geometry.Mesh;
using MeshingParameters = rhinocommon::Rhino.Geometry.MeshingParameters;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PlaneFitResult = rhinocommon::Rhino.Geometry.PlaneFitResult;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Polyline = rhinocommon::Rhino.Geometry.Polyline;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

internal static partial class LivePanelCladdingGeometryPartitionService
{
    public static OperationResponse<PanelCladdingCreatePanelSnapshot> BuildCreateSnapshot(
        Guid objectId,
        Brep source,
        IReadOnlyList<LivePanelCladdingGuideCurve> guides,
        IReadOnlyDictionary<string, string> userText,
        double tolerance)
    {
        OperationResponse<LocalPanelFrame> frameResponse = BuildLocalFrame(source, tolerance);
        if (!frameResponse.Success || frameResponse.Data is null)
        {
            return OperationResponse<PanelCladdingCreatePanelSnapshot>.Fail(frameResponse.Message);
        }
        LocalPanelFrame local = frameResponse.Data;
        var guideSnapshots = new List<PanelCladdingCreateGuideSnapshot>(guides.Count);
        foreach (LivePanelCladdingGuideCurve guide in guides)
        {
            Point3d[] points = SampleGuideCurve(guide.Curve);
            guideSnapshots.Add(new PanelCladdingCreateGuideSnapshot
            {
                ObjectId = guide.ObjectId,
                IsOnPanel = HasMeaningfulPanelOverlap(source, guide.Curve, local, tolerance),
                Samples = points.Select(point =>
                {
                    Vector3d delta = point - local.Frame.Origin;
                    return new PanelPoint3(
                        delta * local.Frame.XAxis,
                        delta * local.Frame.YAxis,
                        delta * local.Frame.ZAxis);
                }).ToArray()
            });
        }
        return OperationResponse<PanelCladdingCreatePanelSnapshot>.Ok(
            new PanelCladdingCreatePanelSnapshot
            {
                ObjectId = objectId,
                XMinimum = local.XMin,
                XMaximum = local.XMax,
                YMinimum = local.YMin,
                YMaximum = local.YMax,
                ZMinimum = local.ZMin,
                ZMaximum = local.ZMax,
                Tolerance = tolerance,
                Guides = guideSnapshots,
                UserText = new Dictionary<string, string>(userText, StringComparer.OrdinalIgnoreCase)
            });
    }

    public static OperationResponse<PanelCladdingInferredOffsets> InferOffsets(
        Brep source,
        IReadOnlyList<Brep> claddingSurfaces,
        double tolerance)
    {
        return InferOffsets(source, claddingSurfaces, Array.Empty<Curve>(), tolerance);
    }

    public static OperationResponse<PanelCladdingInferredOffsets> InferOffsets(
        Brep source,
        IReadOnlyList<Brep> claddingSurfaces,
        IReadOnlyList<Curve> extrusionCurves,
        double tolerance)
    {
        if ((claddingSurfaces is null || claddingSurfaces.Count == 0) &&
            (extrusionCurves is null || extrusionCurves.Count == 0))
        {
            return OperationResponse<PanelCladdingInferredOffsets>.Fail(
                "PANEL_CLADDING_SYNC_GEOMETRY_MISSING");
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

        foreach (Brep surface in claddingSurfaces ?? Array.Empty<Brep>())
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

        foreach (Curve curve in extrusionCurves ?? Array.Empty<Curve>())
        {
            AddDividerCandidate(
                curve,
                local.Frame,
                local.XMin,
                local.YMin,
                width,
                height,
                axisTolerance,
                horizontalCandidates,
                verticalCandidates);
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
            new LivePanelCladdingGeometryGrid(
                source,
                cells,
                local.Frame,
                local.XMin,
                local.XMax,
                local.YMin,
                local.YMax,
                xBoundaries,
                yBoundaries,
                tolerance));
    }

    public static OperationResponse<Curve> CreateExtrusionCurve(
        LivePanelCladdingGeometryGrid grid,
        PanelCladdingExtrusionCurvePlan plan)
    {
        IEnumerable<LivePanelCladdingGeometryCell> sourceCells;
        double target;
        if (plan.Kind == PanelCladdingExtrusionCurveKind.Frame)
        {
            sourceCells = plan.Code switch
            {
                "FRM_0" => grid.Cells.Where(cell => cell.Cell.Row == 0),
                "FRM_1" => grid.Cells.Where(cell => cell.Cell.Row == grid.Cells.Max(item => item.Cell.Row)),
                "FRM_2" => grid.Cells.Where(cell => cell.Cell.Column == 0),
                "FRM_3" => grid.Cells.Where(cell => cell.Cell.Column == grid.Cells.Max(item => item.Cell.Column)),
                _ => Array.Empty<LivePanelCladdingGeometryCell>()
            };
            target = plan.Code switch
            {
                "FRM_0" => grid.YMin,
                "FRM_1" => grid.YMax,
                "FRM_2" => grid.XMin,
                "FRM_3" => grid.XMax,
                _ => double.NaN
            };
        }

        else
        {
            sourceCells = plan.AtomicSegments.Select(segment => grid.Cells.FirstOrDefault(cell =>
                    segment.Axis == PanelCladdingTopologyAxis.Horizontal
                        ? cell.Cell.Column == segment.Bay && cell.Cell.Row == segment.Track
                        : cell.Cell.Column == segment.Track && cell.Cell.Row == segment.Bay))
                .Where(cell => cell is not null)
                .Cast<LivePanelCladdingGeometryCell>();
            target = plan.Axis == PanelCladdingTopologyAxis.Horizontal
                ? grid.YMin + plan.Offset
                : grid.XMin + plan.Offset;
        }

        LivePanelCladdingGeometryCell[] cells = sourceCells.Distinct().ToArray();
        if (cells.Length == 0 || double.IsNaN(target))
        {
            return OperationResponse<Curve>.Fail(
                $"PANEL_CLADDING_EXTRUSION_SOURCE_CELL_MISSING: {plan.Code}");
        }

        double extent = Math.Max(grid.XMax - grid.XMin, grid.YMax - grid.YMin);
        double axisTolerance = Math.Max(grid.Tolerance * 10d, extent * 1e-7d);
        var pieces = new List<Curve>();
        try
        {
            foreach (LivePanelCladdingGeometryCell cell in cells)
            {
                BrepEdge? best = cell.Geometry.Edges
                    .Where(edge => edge.Valence == EdgeAdjacency.Naked)
                    .Select(edge => new { Edge = edge, Score = ScoreEdge(edge, grid.Frame, plan.Axis, target, axisTolerance) })
                    .Where(item => item.Score >= 0d)
                    .OrderBy(item => item.Score)
                    .Select(item => item.Edge)
                    .FirstOrDefault();
                if (best is null)
                {
                    return OperationResponse<Curve>.Fail(
                        $"PANEL_CLADDING_EXTRUSION_EDGE_NOT_FOUND: {plan.Code}: {cell.Cell.ShortLabel}");
                }
                pieces.Add(best.DuplicateCurve());
            }

            Curve[] joined = Curve.JoinCurves(pieces, grid.Tolerance);
            if (joined.Length != 1)
            {
                foreach (Curve curve in joined)
                {
                    curve.Dispose();
                }
                return OperationResponse<Curve>.Fail(
                    $"PANEL_CLADDING_EXTRUSION_JOIN_FAILED: {plan.Code}: {joined.Length} curves");
            }
            Curve simplified = SimplifyExtrusionCurve(joined[0], grid.Tolerance);
            joined[0].Dispose();
            return OperationResponse<Curve>.Ok(simplified);
        }
        finally
        {
            foreach (Curve piece in pieces)
            {
                piece.Dispose();
            }
        }
    }

    internal static Curve SimplifyExtrusionCurve(Curve source, double tolerance)
    {
        double fitTolerance = Math.Max(tolerance, 1e-9d);
        double angleTolerance = Math.PI / 180d;
        Curve? simplified = source.Simplify(
            CurveSimplifyOptions.All,
            fitTolerance,
            angleTolerance);
        Curve candidate = simplified is null || ReferenceEquals(simplified, source)
            ? source.DuplicateCurve()
            : simplified;
        Curve? fitted = candidate.Fit(3, fitTolerance, angleTolerance);
        if (fitted is not null &&
            CountNurbsControlPoints(fitted) < CountNurbsControlPoints(candidate))
        {
            candidate.Dispose();
            return fitted;
        }

        fitted?.Dispose();
        return candidate;
    }

    private static int CountNurbsControlPoints(Curve curve)
    {
        using var nurbs = curve.ToNurbsCurve();
        return nurbs?.Points.Count ?? int.MaxValue;
    }

    public static bool IsAssociated(Brep source, Brep candidate, double tolerance)
    {
        Point3d[] points = candidate.Vertices.Select(vertex => vertex.Location).ToArray();
        if (points.Length == 0)
        {
            using AreaMassProperties? area = AreaMassProperties.Compute(candidate);
            points = area is null ? Array.Empty<Point3d>() : [area.Centroid];
        }
        return PointsLieOnPanel(source, points, tolerance);
    }

    public static bool IsAssociated(Brep source, Curve candidate, double tolerance) =>
        PointsLieOnPanel(source, SampleCurve(candidate, 12), tolerance);

    public static OperationResponse<PanelCladdingInferredExtrusionLayout> InferExtrusionTopology(
        Brep source,
        IReadOnlyList<Curve> curves,
        IReadOnlyList<double> horizontalOffsets,
        IReadOnlyList<double> verticalOffsets,
        double tolerance)
    {
        if (curves is null || curves.Count == 0)
        {
            return OperationResponse<PanelCladdingInferredExtrusionLayout>.Fail(
                "PANEL_CLADDING_SYNC_CURVES_MISSING");
        }
        OperationResponse<LocalPanelFrame> frameResponse = BuildLocalFrame(source, tolerance);
        if (!frameResponse.Success || frameResponse.Data is null)
        {
            return OperationResponse<PanelCladdingInferredExtrusionLayout>.Fail(frameResponse.Message);
        }
        LocalPanelFrame local = frameResponse.Data;
        double width = local.XMax - local.XMin;
        double height = local.YMax - local.YMin;
        double axisTolerance = Math.Max(tolerance * 10d, Math.Max(width, height) * 1e-7d);
        double[] xCuts = [0d, .. verticalOffsets, width];
        double[] yCuts = [0d, .. horizontalOffsets, height];
        var inferredCurves = new List<PanelCladdingInferredCurveGeometry>(curves.Count);
        var present = new HashSet<PanelCladdingSegmentCoordinate>();
        var mergeRuns = new List<PanelCladdingMergeRun>();
        var frameCodes = new HashSet<string>(StringComparer.Ordinal);

        for (int sourceIndex = 0; sourceIndex < curves.Count; sourceIndex++)
        {
            Curve curve = curves[sourceIndex];
            Point3d[] samples = SampleCurve(curve, 16);
            var localPoints = samples.Select(point =>
            {
                Vector3d delta = point - local.Frame.Origin;
                return (X: delta * local.Frame.XAxis - local.XMin,
                    Y: delta * local.Frame.YAxis - local.YMin);
            }).ToArray();
            double xSpan = localPoints.Max(point => point.X) - localPoints.Min(point => point.X);
            double ySpan = localPoints.Max(point => point.Y) - localPoints.Min(point => point.Y);
            bool horizontal = ySpan <= axisTolerance && xSpan > axisTolerance;
            bool vertical = xSpan <= axisTolerance && ySpan > axisTolerance;
            if (!horizontal && !vertical)
            {
                return OperationResponse<PanelCladdingInferredExtrusionLayout>.Fail(
                    $"PANEL_CLADDING_SYNC_CURVE_NOT_GRID_ALIGNED: {sourceIndex}");
            }

            PanelCladdingTopologyAxis axis = horizontal
                ? PanelCladdingTopologyAxis.Horizontal
                : PanelCladdingTopologyAxis.Vertical;
            double primary = horizontal
                ? localPoints.Average(point => point.Y)
                : localPoints.Average(point => point.X);
            double secondaryMin = horizontal
                ? localPoints.Min(point => point.X)
                : localPoints.Min(point => point.Y);
            double secondaryMax = horizontal
                ? localPoints.Max(point => point.X)
                : localPoints.Max(point => point.Y);
            double primaryExtent = horizontal ? height : width;
            string frameCode = string.Empty;
            if (Math.Abs(primary) <= axisTolerance)
            {
                frameCode = horizontal ? "FRM_0" : "FRM_2";
            }
            else if (Math.Abs(primary - primaryExtent) <= axisTolerance)
            {
                frameCode = horizontal ? "FRM_1" : "FRM_3";
            }
            if (frameCode.Length > 0)
            {
                if (!frameCodes.Add(frameCode))
                {
                    return OperationResponse<PanelCladdingInferredExtrusionLayout>.Fail(
                        $"PANEL_CLADDING_SYNC_DUPLICATE_FRAME_CURVE: {frameCode}");
                }
                inferredCurves.Add(new PanelCladdingInferredCurveGeometry
                {
                    SourceIndex = sourceIndex,
                    FrameCode = frameCode,
                    Axis = axis
                });
                continue;
            }

            IReadOnlyList<double> offsets = horizontal ? horizontalOffsets : verticalOffsets;
            int track = FindNearestIndex(offsets, primary, axisTolerance);
            if (track < 0)
            {
                return OperationResponse<PanelCladdingInferredExtrusionLayout>.Fail(
                    $"PANEL_CLADDING_SYNC_CURVE_TRACK_NOT_FOUND: {sourceIndex}");
            }
            double[] cuts = horizontal ? xCuts : yCuts;
            var atoms = new List<PanelCladdingSegmentCoordinate>();
            for (int bay = 0; bay < cuts.Length - 1; bay++)
            {
                if (secondaryMin <= cuts[bay] + axisTolerance &&
                    secondaryMax >= cuts[bay + 1] - axisTolerance)
                {
                    atoms.Add(new PanelCladdingSegmentCoordinate(axis, track, bay));
                }
            }
            if (atoms.Count == 0 || atoms.Select(atom => atom.Bay).Zip(
                atoms.Select(atom => atom.Bay).Skip(1),
                (left, right) => right == left + 1).Any(consecutive => !consecutive))
            {
                return OperationResponse<PanelCladdingInferredExtrusionLayout>.Fail(
                    $"PANEL_CLADDING_SYNC_CURVE_SPAN_INVALID: {sourceIndex}");
            }
            foreach (PanelCladdingSegmentCoordinate atom in atoms)
            {
                if (!present.Add(atom))
                {
                    return OperationResponse<PanelCladdingInferredExtrusionLayout>.Fail(
                        $"PANEL_CLADDING_SYNC_OVERLAPPING_CURVES: {axis}:{track}:{atom.Bay}");
                }
            }
            if (atoms.Count > 1)
            {
                mergeRuns.Add(new PanelCladdingMergeRun(
                    axis,
                    track,
                    atoms[0].Bay,
                    atoms[^1].Bay));
            }
            inferredCurves.Add(new PanelCladdingInferredCurveGeometry
            {
                SourceIndex = sourceIndex,
                Axis = axis,
                AtomicSegments = atoms
            });
        }

        string[] missingFrames = new[] { "FRM_0", "FRM_1", "FRM_2", "FRM_3" }
            .Where(code => !frameCodes.Contains(code))
            .ToArray();
        if (missingFrames.Length > 0)
        {
            return OperationResponse<PanelCladdingInferredExtrusionLayout>.Fail(
                $"PANEL_CLADDING_SYNC_FRAME_CURVES_MISSING: {string.Join(",", missingFrames)}");
        }

        var missing = new List<PanelCladdingSegmentCoordinate>();
        for (int track = 0; track < horizontalOffsets.Count; track++)
        {
            for (int bay = 0; bay < xCuts.Length - 1; bay++)
            {
                var atom = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, track, bay);
                if (!present.Contains(atom)) missing.Add(atom);
            }
        }
        for (int track = 0; track < verticalOffsets.Count; track++)
        {
            for (int bay = 0; bay < yCuts.Length - 1; bay++)
            {
                var atom = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, track, bay);
                if (!present.Contains(atom)) missing.Add(atom);
            }
        }
        return OperationResponse<PanelCladdingInferredExtrusionLayout>.Ok(
            new PanelCladdingInferredExtrusionLayout
            {
                Topology = new PanelCladdingTopologyState
                {
                    MissingSegments = missing,
                    MergeRuns = mergeRuns
                },
                Curves = inferredCurves
            });
    }

    private static void AddDividerCandidate(
        Curve curve,
        Plane frame,
        double xMin,
        double yMin,
        double width,
        double height,
        double tolerance,
        ICollection<double> horizontal,
        ICollection<double> vertical)
    {
        Point3d[] points = SampleCurve(curve, 12);
        var local = points.Select(point =>
        {
            Vector3d delta = point - frame.Origin;
            return (X: delta * frame.XAxis, Y: delta * frame.YAxis);
        }).ToArray();
        double lxMin = local.Min(point => point.X);
        double lxMax = local.Max(point => point.X);
        double lyMin = local.Min(point => point.Y);
        double lyMax = local.Max(point => point.Y);
        if (lxMax - lxMin <= tolerance && lyMax - lyMin > tolerance)
        {
            double relative = local.Average(point => point.X) - xMin;
            if (relative > tolerance && relative < width - tolerance) vertical.Add(relative);
        }
        else if (lyMax - lyMin <= tolerance && lxMax - lxMin > tolerance)
        {
            double relative = local.Average(point => point.Y) - yMin;
            if (relative > tolerance && relative < height - tolerance) horizontal.Add(relative);
        }
    }

    private static int FindNearestIndex(IReadOnlyList<double> values, double target, double tolerance)
    {
        int result = -1;
        double best = double.MaxValue;
        for (int index = 0; index < values.Count; index++)
        {
            double distance = Math.Abs(values[index] - target);
            if (distance <= tolerance && distance < best)
            {
                result = index;
                best = distance;
            }
        }
        return result;
    }

    private static bool PointsLieOnPanel(Brep source, IReadOnlyList<Point3d> points, double tolerance)
    {
        if (points.Count == 0) return false;
        double distanceTolerance = Math.Max(tolerance * 10d, 1e-5d);
        return points.All(point =>
        {
            Point3d closest = source.ClosestPoint(point);
            return closest.IsValid && closest.DistanceTo(point) <= distanceTolerance;
        });
    }

    private static bool HasMeaningfulPanelOverlap(
        Brep source,
        Curve curve,
        LocalPanelFrame panel,
        double tolerance)
    {
        Point3d[] samples = SampleGuideCurve(curve);
        if (samples.Length < 2)
        {
            return false;
        }
        var local = samples.Select(point =>
        {
            Vector3d delta = point - panel.Frame.Origin;
            return (X: delta * panel.Frame.XAxis, Y: delta * panel.Frame.YAxis);
        }).ToArray();
        double xMinimum = local.Min(point => point.X);
        double xMaximum = local.Max(point => point.X);
        double yMinimum = local.Min(point => point.Y);
        double yMaximum = local.Max(point => point.Y);
        double xSpan = xMaximum - xMinimum;
        double ySpan = yMaximum - yMinimum;
        double extent = Math.Max(panel.XMax - panel.XMin, panel.YMax - panel.YMin);
        double probeTolerance = Math.Max(tolerance * 10d, extent * 1e-7d);

        bool vertical = ySpan >= Math.Max(xSpan * 2d, probeTolerance);
        bool horizontal = xSpan >= Math.Max(ySpan * 2d, probeTolerance);
        if (!vertical && !horizontal)
        {
            return false;
        }

        double primary = vertical
            ? local.Select(point => point.X).OrderBy(value => value).ElementAt(local.Length / 2)
            : local.Select(point => point.Y).OrderBy(value => value).ElementAt(local.Length / 2);
        double primaryMinimum = vertical ? panel.XMin : panel.YMin;
        double primaryMaximum = vertical ? panel.XMax : panel.YMax;
        if (primary <= primaryMinimum + probeTolerance ||
            primary >= primaryMaximum - probeTolerance)
        {
            return false;
        }

        double secondaryMinimum = vertical ? yMinimum : xMinimum;
        double secondaryMaximum = vertical ? yMaximum : xMaximum;
        double panelSecondaryMinimum = vertical ? panel.YMin : panel.XMin;
        double panelSecondaryMaximum = vertical ? panel.YMax : panel.XMax;
        double overlapStart = Math.Max(secondaryMinimum, panelSecondaryMinimum);
        double overlapEnd = Math.Min(secondaryMaximum, panelSecondaryMaximum);
        if (overlapEnd - overlapStart <= probeTolerance)
        {
            return false;
        }

        double distanceTolerance = Math.Max(tolerance * 10d, 1e-5d);
        foreach (double fraction in new[] { 0.1d, 0.5d, 0.9d })
        {
            double secondary = overlapStart + (overlapEnd - overlapStart) * fraction;
            Point3d target = vertical
                ? panel.Frame.PointAt(primary, secondary)
                : panel.Frame.PointAt(secondary, primary);
            if (!curve.ClosestPoint(target, out double parameter))
            {
                return false;
            }
            Point3d point = curve.PointAt(parameter);
            Vector3d delta = point - panel.Frame.Origin;
            double actualPrimary = vertical
                ? delta * panel.Frame.XAxis
                : delta * panel.Frame.YAxis;
            double actualSecondary = vertical
                ? delta * panel.Frame.YAxis
                : delta * panel.Frame.XAxis;
            if (Math.Abs(actualPrimary - primary) > probeTolerance ||
                Math.Abs(actualSecondary - secondary) > probeTolerance)
            {
                return false;
            }
            Point3d closest = source.ClosestPoint(point);
            if (!closest.IsValid || closest.DistanceTo(point) > distanceTolerance)
            {
                return false;
            }
        }
        return true;
    }

    private static Point3d[] SampleCurve(Curve curve, int segmentCount)
    {
        return Enumerable.Range(0, segmentCount + 1)
            .Select(index => curve.PointAt(curve.Domain.ParameterAt((double)index / segmentCount)))
            .ToArray();
    }

    private static double ScoreEdge(
        BrepEdge edge,
        Plane frame,
        PanelCladdingTopologyAxis axis,
        double target,
        double tolerance)
    {
        const int sampleCount = 8;
        var primary = new double[sampleCount + 1];
        var secondary = new double[sampleCount + 1];
        for (int index = 0; index <= sampleCount; index++)
        {
            Point3d point = edge.PointAt(edge.Domain.ParameterAt((double)index / sampleCount));
            Vector3d delta = point - frame.Origin;
            double x = delta * frame.XAxis;
            double y = delta * frame.YAxis;
            primary[index] = axis == PanelCladdingTopologyAxis.Horizontal ? y : x;
            secondary[index] = axis == PanelCladdingTopologyAxis.Horizontal ? x : y;
        }
        if (primary.Max() - primary.Min() > tolerance || secondary.Max() - secondary.Min() <= tolerance)
        {
            return -1d;
        }
        double distance = Math.Abs(primary.Average() - target);
        return distance <= tolerance ? distance : -1d;
    }

    public static OperationResponse<Brep> CreateRegionSurface(
        LivePanelCladdingGeometryGrid grid,
        IReadOnlyList<PanelCladdingCell> regionCells,
        double tolerance,
        string cid)
    {
        if (grid.Source.Faces.Count != 1)
        {
            return OperationResponse<Brep>.Fail(
                $"PANEL_CLADDING_REGION_SINGLE_FACE_SOURCE_REQUIRED: {cid}: " +
                $"source has {grid.Source.Faces.Count} faces.");
        }

        var boundaryService = new PanelCladdingRegionBoundaryService();
        OperationResponse<IReadOnlyList<PanelCladdingRegionBoundarySegment>> boundary =
            boundaryService.CreateBoundary(
                grid.Cells.Select(cell => cell.Cell).ToArray(),
                regionCells);
        if (!boundary.Success || boundary.Data is null)
        {
            return OperationResponse<Brep>.Fail(
                $"PANEL_CLADDING_REGION_BOUNDARY_FAILED: {cid}: {boundary.Message}");
        }

        var expectedLabels = regionCells
            .Select(cell => cell.ShortLabel)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (expectedLabels.Count == grid.Cells.Count &&
            grid.Cells.All(cell => expectedLabels.Contains(cell.Cell.ShortLabel)))
        {
            Brep complete = grid.Source.DuplicateBrep();
            return complete.Faces.Count == 1
                ? OperationResponse<Brep>.Ok(complete)
                : DisposeAndFail(
                    complete,
                    $"PANEL_CLADDING_REGION_SINGLE_FACE_CREATE_FAILED: {cid}");
        }

        PanelCladdingRegionBoundarySegment[] internalSegments = boundary.Data
            .Where(segment => !segment.IsPanelPerimeter)
            .ToArray();
        if (internalSegments.Length == 0)
        {
            return OperationResponse<Brep>.Fail(
                $"PANEL_CLADDING_REGION_INTERNAL_BOUNDARY_MISSING: {cid}");
        }

        var atomicCurves = new List<Curve>(internalSegments.Length);
        var splitCurves = new List<Curve>();
        try
        {
            foreach (PanelCladdingRegionBoundarySegment segment in internalSegments)
            {
                OperationResponse<Curve> curve = CreateRegionBoundaryCurve(grid, segment, cid);
                if (!curve.Success || curve.Data is null)
                {
                    return OperationResponse<Brep>.Fail(
                        curve.Message);
                }
                atomicCurves.Add(curve.Data);
            }

            splitCurves.AddRange(Curve.JoinCurves(atomicCurves, tolerance));
            if (splitCurves.Count == 0)
            {
                return OperationResponse<Brep>.Fail(
                    $"PANEL_CLADDING_REGION_BOUNDARY_JOIN_FAILED: {cid}");
            }

            using Brep? split = grid.Source.Faces[0].Split(splitCurves, tolerance);
            if (split is null)
            {
                return OperationResponse<Brep>.Fail(
                    $"PANEL_CLADDING_REGION_FACE_SPLIT_FAILED: {cid}");
            }

            var matches = new List<Brep>();
            try
            {
                foreach (var face in split.Faces)
                {
                    Brep candidate = face.DuplicateFace(true);
                    OperationResponse<IReadOnlyList<string>> coverage = ResolveCoveredCellLabels(
                        candidate,
                        grid,
                        tolerance,
                        cid);
                    bool exactCoverage = coverage.Success && coverage.Data is not null &&
                        coverage.Data.Count == expectedLabels.Count &&
                        coverage.Data.All(expectedLabels.Contains);
                    if (exactCoverage && candidate.Faces.Count == 1)
                    {
                        matches.Add(candidate);
                    }
                    else
                    {
                        candidate.Dispose();
                    }
                }

                if (matches.Count != 1)
                {
                    return OperationResponse<Brep>.Fail(
                        $"PANEL_CLADDING_REGION_FACE_SELECTION_FAILED: {cid}: " +
                        $"expected one face, found {matches.Count}.");
                }

                Brep result = matches[0];
                matches.Clear();
                return OperationResponse<Brep>.Ok(result);
            }
            finally
            {
                foreach (Brep match in matches)
                {
                    match.Dispose();
                }
            }
        }
        finally
        {
            foreach (Curve curve in splitCurves)
            {
                curve.Dispose();
            }
            foreach (Curve curve in atomicCurves)
            {
                curve.Dispose();
            }
        }
    }

    private static OperationResponse<Curve> CreateRegionBoundaryCurve(
        LivePanelCladdingGeometryGrid grid,
        PanelCladdingRegionBoundarySegment segment,
        string cid)
    {
        LivePanelCladdingGeometryCell? geometryCell = grid.Cells.FirstOrDefault(item =>
            string.Equals(
                item.Cell.ShortLabel,
                segment.Cell.ShortLabel,
                StringComparison.OrdinalIgnoreCase));
        if (geometryCell is null)
        {
            return OperationResponse<Curve>.Fail(
                $"PANEL_CLADDING_REGION_CELL_GEOMETRY_MISSING: {cid}: {segment.Cell.ShortLabel}");
        }

        bool vertical = segment.Side is PanelCladdingCellBoundarySide.Left or
            PanelCladdingCellBoundarySide.Right;
        double primary = segment.Side switch
        {
            PanelCladdingCellBoundarySide.Left => grid.XBoundaries[segment.Cell.Column],
            PanelCladdingCellBoundarySide.Right => grid.XBoundaries[segment.Cell.Column + 1],
            PanelCladdingCellBoundarySide.Bottom => grid.YBoundaries[segment.Cell.Row],
            PanelCladdingCellBoundarySide.Top => grid.YBoundaries[segment.Cell.Row + 1],
            _ => double.NaN
        };
        double secondaryMinimum = vertical
            ? grid.YBoundaries[segment.Cell.Row]
            : grid.XBoundaries[segment.Cell.Column];
        double secondaryMaximum = vertical
            ? grid.YBoundaries[segment.Cell.Row + 1]
            : grid.XBoundaries[segment.Cell.Column + 1];
        double extent = Math.Max(grid.XMax - grid.XMin, grid.YMax - grid.YMin);
        double axisTolerance = Math.Max(grid.Tolerance * 10d, extent * 1e-7d);

        BrepEdge? edge = geometryCell.Geometry.Edges
            .Where(candidate => candidate.Valence == EdgeAdjacency.Naked)
            .Select(candidate => new
            {
                Edge = candidate,
                Score = ScoreRegionBoundaryEdge(
                    candidate,
                    grid.Frame,
                    vertical,
                    primary,
                    secondaryMinimum,
                    secondaryMaximum,
                    axisTolerance)
            })
            .Where(candidate => candidate.Score >= 0d)
            .OrderBy(candidate => candidate.Score)
            .Select(candidate => candidate.Edge)
            .FirstOrDefault();
        return edge is null
            ? OperationResponse<Curve>.Fail(
                $"PANEL_CLADDING_REGION_BOUNDARY_CURVE_MISSING: {cid}: " +
                $"{segment.Cell.ShortLabel}:{segment.Side}")
            : OperationResponse<Curve>.Ok(edge.DuplicateCurve());
    }

    private static double ScoreRegionBoundaryEdge(
        BrepEdge edge,
        Plane frame,
        bool vertical,
        double expectedPrimary,
        double expectedSecondaryMinimum,
        double expectedSecondaryMaximum,
        double tolerance)
    {
        const int sampleCount = 12;
        var primary = new double[sampleCount + 1];
        var secondary = new double[sampleCount + 1];
        for (int index = 0; index <= sampleCount; index++)
        {
            Point3d point = edge.PointAt(edge.Domain.ParameterAt((double)index / sampleCount));
            Vector3d delta = point - frame.Origin;
            double x = delta * frame.XAxis;
            double y = delta * frame.YAxis;
            primary[index] = vertical ? x : y;
            secondary[index] = vertical ? y : x;
        }

        double primarySpan = primary.Max() - primary.Min();
        double primaryDistance = Math.Abs(primary.Average() - expectedPrimary);
        double secondaryStartDistance = Math.Abs(secondary.Min() - expectedSecondaryMinimum);
        double secondaryEndDistance = Math.Abs(secondary.Max() - expectedSecondaryMaximum);
        if (primarySpan > tolerance ||
            primaryDistance > tolerance ||
            secondaryStartDistance > tolerance ||
            secondaryEndDistance > tolerance)
        {
            return -1d;
        }
        return primarySpan + primaryDistance + secondaryStartDistance + secondaryEndDistance;
    }

    private static OperationResponse<Brep> DisposeAndFail(Brep brep, string message)
    {
        brep.Dispose();
        return OperationResponse<Brep>.Fail(message);
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

        var localPoints = new List<(double X, double Y, double Z)>(combined.Vertices.Count);
        foreach (var vertex in combined.Vertices)
        {
            Vector3d delta = new Point3d(vertex) - frame.Origin;
            localPoints.Add((
                delta * frame.XAxis,
                delta * frame.YAxis,
                delta * frame.ZAxis));
        }
        return OperationResponse<LocalPanelFrame>.Ok(new LocalPanelFrame
        {
            Frame = frame,
            XMin = localPoints.Min(point => point.X),
            XMax = localPoints.Max(point => point.X),
            YMin = localPoints.Min(point => point.Y),
            YMax = localPoints.Max(point => point.Y),
            ZMin = localPoints.Min(point => point.Z),
            ZMax = localPoints.Max(point => point.Z)
        });
    }

    private static Point3d[] SampleGuideCurve(Curve curve)
    {
        if (curve.TryGetPolyline(out Polyline polyline) && polyline.Count >= 2)
        {
            return Enumerable.Range(0, polyline.Count)
                .Select(index => polyline[index])
                .ToArray();
        }
        return new[] { 0d, 0.25d, 0.5d, 0.75d, 1d }
            .Select(curve.PointAtNormalizedLength)
            .ToArray();
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
        public double ZMin { get; init; }
        public double ZMax { get; init; }
    }
}

internal sealed record LivePanelCladdingGuideCurve(Guid ObjectId, Curve Curve);

internal sealed class LivePanelCladdingGeometryGrid : IDisposable
{
    public LivePanelCladdingGeometryGrid(
        Brep source,
        IReadOnlyList<LivePanelCladdingGeometryCell> cells,
        Plane frame,
        double xMin,
        double xMax,
        double yMin,
        double yMax,
        IReadOnlyList<double> xBoundaries,
        IReadOnlyList<double> yBoundaries,
        double tolerance)
    {
        Source = source;
        Cells = cells;
        Frame = frame;
        XMin = xMin;
        XMax = xMax;
        YMin = yMin;
        YMax = yMax;
        XBoundaries = xBoundaries;
        YBoundaries = yBoundaries;
        Tolerance = tolerance;
    }

    public Brep Source { get; }
    public IReadOnlyList<LivePanelCladdingGeometryCell> Cells { get; }
    public Plane Frame { get; }
    public double XMin { get; }
    public double XMax { get; }
    public double YMin { get; }
    public double YMax { get; }
    public IReadOnlyList<double> XBoundaries { get; }
    public IReadOnlyList<double> YBoundaries { get; }
    public double Tolerance { get; }

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
