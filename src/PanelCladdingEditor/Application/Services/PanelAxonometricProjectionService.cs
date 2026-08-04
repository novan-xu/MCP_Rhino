using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelAxonometricProjectionService
{
    private const int BoundarySamples = 20;
    private const int DepthGridSize = 5;

    public OperationResponse<(PanelGeometryClass Classification, string Diagnostic, PanelPreviewGeometry Preview)> Build(
        IReadOnlyList<PanelPoint3> vertices,
        IReadOnlyList<PanelTriangle> triangles,
        double xMin,
        double xMax,
        double yMin,
        double yMax,
        IReadOnlyList<double> horizontalOffsets,
        IReadOnlyList<double> verticalOffsets,
        IReadOnlyList<PanelCladdingCell> cells,
        double tolerance)
    {
        if (vertices.Count < 3 || triangles.Count == 0)
        {
            return OperationResponse<(PanelGeometryClass, string, PanelPreviewGeometry)>.Fail(
                "PANEL_CLADDING_PREVIEW_MESH_REQUIRED: panel preview mesh is empty.");
        }

        double zMin = vertices.Min(point => point.Z);
        double zMax = vertices.Max(point => point.Z);
        double depthRange = zMax - zMin;
        double projectionTolerance = Math.Max(tolerance * 10d, Math.Max(xMax - xMin, yMax - yMin) * 1e-7d);
        bool planar = depthRange <= projectionTolerance;
        bool ambiguous = false;
        int missingSamples = 0;
        var gridPolylines = new List<IReadOnlyList<PanelPoint3>>();

        var xBoundaries = new List<double> { xMin };
        xBoundaries.AddRange(verticalOffsets.Select(value => xMin + value));
        xBoundaries.Add(xMax);
        var yBoundaries = new List<double> { yMin };
        yBoundaries.AddRange(horizontalOffsets.Select(value => yMin + value));
        yBoundaries.Add(yMax);

        foreach (double x in xBoundaries)
        {
            IReadOnlyList<PanelPoint3> line = SampleLine(
                fixedValue: x,
                start: yMin,
                end: yMax,
                fixedX: true,
                vertices,
                triangles,
                projectionTolerance,
                ref ambiguous,
                ref missingSamples);
            gridPolylines.Add(line);
        }

        foreach (double y in yBoundaries)
        {
            IReadOnlyList<PanelPoint3> line = SampleLine(
                fixedValue: y,
                start: xMin,
                end: xMax,
                fixedX: false,
                vertices,
                triangles,
                projectionTolerance,
                ref ambiguous,
                ref missingSamples);
            gridPolylines.Add(line);
        }

        var previewCells = new List<PanelPreviewCell>(cells.Count);
        foreach (PanelCladdingCell cell in cells)
        {
            double left = xBoundaries[cell.Column];
            double right = xBoundaries[cell.Column + 1];
            double bottom = yBoundaries[cell.Row];
            double top = yBoundaries[cell.Row + 1];
            var boundary = new List<PanelPoint3>();
            foreach ((double x, double y) in new[]
            {
                (left, bottom), (right, bottom), (right, top), (left, top)
            })
            {
                QueryDepthResult query = QueryDepth(x, y, vertices, triangles, projectionTolerance);
                ambiguous |= query.Ambiguous;
                if (!query.Found)
                {
                    missingSamples++;
                }
                boundary.Add(new PanelPoint3(x, y, query.Found ? query.Z : 0d));
            }

            double centerX = 0.5d * (left + right);
            double centerY = 0.5d * (bottom + top);
            QueryDepthResult center = QueryDepth(centerX, centerY, vertices, triangles, projectionTolerance);
            ambiguous |= center.Ambiguous;
            if (!center.Found)
            {
                missingSamples++;
            }

            previewCells.Add(new PanelPreviewCell
            {
                UserTextKey = cell.UserTextKey,
                Center = new PanelPoint3(centerX, centerY, center.Found ? center.Z : 0d),
                Boundary = boundary
            });
        }

        var depthSamples = new List<double>(DepthGridSize * DepthGridSize);
        for (int row = 0; row < DepthGridSize; row++)
        {
            double fy = (row + 0.5d) / DepthGridSize;
            for (int column = 0; column < DepthGridSize; column++)
            {
                double fx = (column + 0.5d) / DepthGridSize;
                QueryDepthResult query = QueryDepth(
                    xMin + fx * (xMax - xMin),
                    yMin + fy * (yMax - yMin),
                    vertices,
                    triangles,
                    projectionTolerance);
                ambiguous |= query.Ambiguous;
                if (!query.Found)
                {
                    missingSamples++;
                    depthSamples.Add(0d);
                }
                else
                {
                    depthSamples.Add(query.Z);
                }
            }
        }

        int sampleBudget = (xBoundaries.Count + yBoundaries.Count) * (BoundarySamples + 1) + cells.Count * 5 + DepthGridSize * DepthGridSize;
        bool tooManyMissing = missingSamples > Math.Max(2, sampleBudget / 20);
        PanelGeometryClass classification = ambiguous || tooManyMissing
            ? PanelGeometryClass.UnsupportedProjection
            : planar ? PanelGeometryClass.Planar : PanelGeometryClass.Curved;
        string diagnostic = classification switch
        {
            PanelGeometryClass.Planar => $"Planar depth range {depthRange:G6}.",
            PanelGeometryClass.Curved => $"Curved/projectable depth range {depthRange:G6}.",
            _ => $"Unsupported orthographic projection: ambiguous={ambiguous}, missingSamples={missingSamples}."
        };

        return OperationResponse<(PanelGeometryClass, string, PanelPreviewGeometry)>.Ok((
            classification,
            diagnostic,
            new PanelPreviewGeometry
            {
                Vertices = vertices,
                Triangles = triangles,
                GridPolylines = gridPolylines,
                Cells = previewCells,
                DepthSamples = depthSamples
            }));
    }

    private static IReadOnlyList<PanelPoint3> SampleLine(
        double fixedValue,
        double start,
        double end,
        bool fixedX,
        IReadOnlyList<PanelPoint3> vertices,
        IReadOnlyList<PanelTriangle> triangles,
        double tolerance,
        ref bool ambiguous,
        ref int missingSamples)
    {
        var points = new List<PanelPoint3>(BoundarySamples + 1);
        for (int index = 0; index <= BoundarySamples; index++)
        {
            double variable = start + (end - start) * index / BoundarySamples;
            double x = fixedX ? fixedValue : variable;
            double y = fixedX ? variable : fixedValue;
            QueryDepthResult query = QueryDepth(x, y, vertices, triangles, tolerance);
            ambiguous |= query.Ambiguous;
            if (!query.Found)
            {
                missingSamples++;
            }
            points.Add(new PanelPoint3(x, y, query.Found ? query.Z : 0d));
        }
        return points;
    }

    private static QueryDepthResult QueryDepth(
        double x,
        double y,
        IReadOnlyList<PanelPoint3> vertices,
        IReadOnlyList<PanelTriangle> triangles,
        double tolerance)
    {
        var depths = new List<double>();
        foreach (PanelTriangle triangle in triangles)
        {
            PanelPoint3 a = vertices[triangle.A];
            PanelPoint3 b = vertices[triangle.B];
            PanelPoint3 c = vertices[triangle.C];
            double denominator = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(denominator) <= 1e-14d)
            {
                continue;
            }

            double alpha = ((b.Y - c.Y) * (x - c.X) + (c.X - b.X) * (y - c.Y)) / denominator;
            double beta = ((c.Y - a.Y) * (x - c.X) + (a.X - c.X) * (y - c.Y)) / denominator;
            double gamma = 1d - alpha - beta;
            double barycentricTolerance = Math.Max(1e-8d, tolerance * 1e-3d);
            if (alpha < -barycentricTolerance || beta < -barycentricTolerance || gamma < -barycentricTolerance)
            {
                continue;
            }

            depths.Add(alpha * a.Z + beta * b.Z + gamma * c.Z);
        }

        if (depths.Count == 0)
        {
            return new QueryDepthResult(false, false, 0d);
        }

        double min = depths.Min();
        double max = depths.Max();
        return new QueryDepthResult(true, max - min > tolerance, depths.Average());
    }

    private readonly record struct QueryDepthResult(bool Found, bool Ambiguous, double Z);
}

