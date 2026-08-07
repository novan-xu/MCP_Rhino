extern alias rhinocommon;

using System.Globalization;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using AreaMassProperties = rhinocommon::Rhino.Geometry.AreaMassProperties;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Layer = rhinocommon::Rhino.DocObjects.Layer;
using Mesh = rhinocommon::Rhino.Geometry.Mesh;
using MeshingParameters = rhinocommon::Rhino.Geometry.MeshingParameters;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PlaneFitResult = rhinocommon::Rhino.Geometry.PlaneFitResult;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed partial class LivePanelCladdingSpawnService : ILivePanelCladdingSpawnService
{
    private readonly ILivePanelCladdingRepository _layoutRepository;
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelCladdingSpawnPlanningService _planning;

    public LivePanelCladdingSpawnService(
        ILivePanelCladdingRepository layoutRepository,
        PanelCladdingKeyService keys,
        PanelCladdingSpawnPlanningService planning)
    {
        _layoutRepository = layoutRepository;
        _keys = keys;
        _planning = planning;
    }

    public OperationResponse<PanelCladdingSpawnResult> Spawn(
        string filePath,
        IReadOnlyList<Guid> objectIds)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelCladdingSpawnResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;
        Guid[] sourcePanelIds = (objectIds ?? Array.Empty<Guid>())
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (sourcePanelIds.Length == 0)
        {
            return OperationResponse<PanelCladdingSpawnResult>.Fail("PANEL_CLADDING_PANEL_SELECTION_REQUIRED");
        }

        var prepared = new List<PreparedCell>();
        try
        {
            foreach (Guid sourcePanelId in sourcePanelIds)
            {
                OperationResponse<IReadOnlyList<PreparedCell>> panel = PreparePanel(
                    document,
                    filePath,
                    sourcePanelId);
                if (!panel.Success || panel.Data is null)
                {
                    return OperationResponse<PanelCladdingSpawnResult>.Fail(
                        $"PANEL_CLADDING_PANEL_PREPARE_FAILED: {sourcePanelId:D}: {panel.Message}");
                }
                prepared.AddRange(panel.Data);
            }

            string? duplicateCid = prepared
                .GroupBy(item => item.Cell.Cid, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;
            if (duplicateCid is not null)
            {
                return OperationResponse<PanelCladdingSpawnResult>.Fail(
                    $"PANEL_CLADDING_DUPLICATE_BATCH_CID: {duplicateCid}");
            }
            foreach (PreparedCell item in prepared)
            {
                RhinoObject[]? existing = document.Objects.FindByUserString(
                    PanelCladdingSpawnPlanningService.CidUserTextKey,
                    item.Cell.Cid,
                    caseSensitive: false);
                if (existing is { Length: > 0 })
                {
                    return OperationResponse<PanelCladdingSpawnResult>.Fail(
                        $"PANEL_CLADDING_CID_ALREADY_EXISTS: {item.Cell.Cid}");
                }
            }

            uint undoRecord = document.BeginUndoRecord("Spawn Panel Cladding");
            var createdIds = new List<Guid>(prepared.Count);
            try
            {
                foreach (PreparedCell item in prepared)
                {
                    OperationResponse<int> layer = EnsureMaterialLayer(document, item.Cell);
                    if (!layer.Success)
                    {
                        RollBackCreatedObjects(document, createdIds);
                        return OperationResponse<PanelCladdingSpawnResult>.Fail(layer.Message);
                    }

                    var attributes = new ObjectAttributes
                    {
                        Name = item.Cell.Cid,
                        LayerIndex = layer.Data,
                        ColorSource = ObjectColorSource.ColorFromLayer
                    };
                    foreach ((string key, string value) in item.Cell.UserTextWrites)
                    {
                        attributes.SetUserString(key, value);
                    }

                    Guid createdId = document.Objects.AddBrep(item.Geometry, attributes);
                    if (createdId == Guid.Empty)
                    {
                        RollBackCreatedObjects(document, createdIds);
                        return OperationResponse<PanelCladdingSpawnResult>.Fail(
                            $"PANEL_CLADDING_OBJECT_CREATE_FAILED: {item.Cell.Cid}");
                    }
                    createdIds.Add(createdId);
                }

                document.Views.Redraw();
                return OperationResponse<PanelCladdingSpawnResult>.Ok(new PanelCladdingSpawnResult
                {
                    SourcePanelIds = sourcePanelIds,
                    CreatedObjectIds = createdIds.ToArray(),
                    Cids = prepared.Select(item => item.Cell.Cid).ToArray()
                });
            }
            catch (Exception ex)
            {
                RollBackCreatedObjects(document, createdIds);
                return OperationResponse<PanelCladdingSpawnResult>.Fail(
                    $"PANEL_CLADDING_SPAWN_FAILED: {ex.Message}");
            }
            finally
            {
                if (undoRecord != 0U)
                {
                    document.EndUndoRecord(undoRecord);
                }
            }
        }
        finally
        {
            foreach (PreparedCell item in prepared)
            {
                item.Geometry.Dispose();
            }
        }
    }

    private OperationResponse<IReadOnlyList<PreparedCell>> PreparePanel(
        RhinoDoc document,
        string filePath,
        Guid objectId)
    {
        RhinoObject? sourceObject = document.Objects.FindId(objectId);
        if (sourceObject?.Geometry is not Brep sourceBrep)
        {
            return OperationResponse<IReadOnlyList<PreparedCell>>.Fail("PANEL_CLADDING_BREP_NOT_FOUND");
        }

        OperationResponse<PanelCladdingLayout> layoutResponse = _layoutRepository.ReadLayout(filePath, objectId);
        if (!layoutResponse.Success || layoutResponse.Data is null)
        {
            return OperationResponse<IReadOnlyList<PreparedCell>>.Fail(layoutResponse.Message);
        }
        PanelCladdingLayout layout = layoutResponse.Data;
        if (!layout.CanSave)
        {
            return OperationResponse<IReadOnlyList<PreparedCell>>.Fail(
                $"PANEL_CLADDING_UNSUPPORTED_PROJECTION: {layout.GeometryDiagnostic}");
        }

        IReadOnlyDictionary<string, string> userText = ReadUserText(sourceObject);
        OperationResponse<PanelCladdingKeySet> keySetResponse = _keys.Parse(
            userText,
            layout.Width,
            layout.Height,
            layout.ModelTolerance);
        if (!keySetResponse.Success || keySetResponse.Data is null)
        {
            return OperationResponse<IReadOnlyList<PreparedCell>>.Fail(keySetResponse.Message);
        }
        PanelCladdingKeySet keySet = keySetResponse.Data;

        OperationResponse<PanelCladdingSpawnPlan> planResponse = _planning.CreatePlan(userText, keySet);
        if (!planResponse.Success || planResponse.Data is null)
        {
            return OperationResponse<IReadOnlyList<PreparedCell>>.Fail(planResponse.Message);
        }
        PanelCladdingSpawnPlan plan = planResponse.Data;

        OperationResponse<LocalPanelFrame> frameResponse = BuildLocalFrame(sourceBrep, layout.ModelTolerance);
        if (!frameResponse.Success || frameResponse.Data is null)
        {
            return OperationResponse<IReadOnlyList<PreparedCell>>.Fail(frameResponse.Message);
        }
        LocalPanelFrame local = frameResponse.Data;

        var xBoundaries = new List<double> { local.XMin };
        xBoundaries.AddRange(keySet.VerticalOffsets.Select(offset => local.XMin + offset));
        xBoundaries.Add(local.XMax);
        var yBoundaries = new List<double> { local.YMin };
        yBoundaries.AddRange(keySet.HorizontalOffsets.Select(offset => local.YMin + offset));
        yBoundaries.Add(local.YMax);

        var prepared = new List<PreparedCell>(plan.Cells.Count);
        foreach (PanelCladdingSpawnCellPlan cell in plan.Cells)
        {
            OperationResponse<Brep> geometry = CreateCellGeometry(
                sourceBrep,
                local.Frame,
                xBoundaries[cell.Column],
                xBoundaries[cell.Column + 1],
                yBoundaries[cell.Row],
                yBoundaries[cell.Row + 1],
                cell,
                layout.ModelTolerance,
                cell.Column > 0,
                cell.Column < keySet.VerticalOffsets.Count,
                cell.Row > 0,
                cell.Row < keySet.HorizontalOffsets.Count);
            if (!geometry.Success || geometry.Data is null)
            {
                foreach (PreparedCell item in prepared)
                {
                    item.Geometry.Dispose();
                }
                return OperationResponse<IReadOnlyList<PreparedCell>>.Fail(geometry.Message);
            }
            prepared.Add(new PreparedCell(objectId, cell, geometry.Data));
        }
        return OperationResponse<IReadOnlyList<PreparedCell>>.Ok(prepared);
    }

    private static OperationResponse<int> EnsureMaterialLayer(
        RhinoDoc document,
        PanelCladdingSpawnCellPlan cell)
    {
        int layerIndex = document.Layers.FindByFullPath(cell.LayerPath, -1);
        if (layerIndex < 0)
        {
            layerIndex = document.Layers.AddPath(cell.LayerPath);
        }
        if (layerIndex < 0)
        {
            return OperationResponse<int>.Fail(
                $"PANEL_CLADDING_LAYER_CREATE_FAILED: {cell.LayerPath}");
        }

        Layer? layer = document.Layers[layerIndex];
        if (layer is null)
        {
            return OperationResponse<int>.Fail(
                $"PANEL_CLADDING_LAYER_NOT_FOUND_AFTER_CREATE: {cell.LayerPath}");
        }
        var expectedColor = System.Drawing.Color.FromArgb(
            cell.LayerColor.Red,
            cell.LayerColor.Green,
            cell.LayerColor.Blue);
        if (layer.Color.ToArgb() != expectedColor.ToArgb())
        {
            layer.Color = expectedColor;
            if (!document.Layers.Modify(layer, layerIndex, quiet: true))
            {
                return OperationResponse<int>.Fail(
                    $"PANEL_CLADDING_LAYER_COLOR_FAILED: {cell.LayerPath}");
            }
        }
        return OperationResponse<int>.Ok(layerIndex);
    }

    private static OperationResponse<Brep> CreateCellGeometry(
        Brep source,
        Plane frame,
        double left,
        double right,
        double bottom,
        double top,
        PanelCladdingSpawnCellPlan cell,
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
                    $"PANEL_CLADDING_CELL_GEOMETRY_AMBIGUOUS: {cell.Cid} produced {meaningful.Count} surface pieces.");
            }

            Brep result = meaningful[0];
            using AreaMassProperties properties = AreaMassProperties.Compute(result);
            if (properties is null)
            {
                return OperationResponse<Brep>.Fail(
                    $"PANEL_CLADDING_CELL_AREA_FAILED: {cell.Cid}");
            }
            Vector3d delta = properties.Centroid - frame.Origin;
            double x = delta * frame.XAxis;
            double y = delta * frame.YAxis;
            if (x < left - tolerance || x > right + tolerance ||
                y < bottom - tolerance || y > top + tolerance)
            {
                return OperationResponse<Brep>.Fail(
                    $"PANEL_CLADDING_CELL_CENTROID_OUTSIDE: {cell.Cid}");
            }

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

    private static IReadOnlyDictionary<string, string> ReadUserText(RhinoObject rhinoObject)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var strings = rhinoObject.Attributes.GetUserStrings();
        if (strings?.AllKeys is null)
        {
            return result;
        }
        foreach (string? key in strings.AllKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = strings[key] ?? string.Empty;
            }
        }
        return result;
    }

    private static OperationResponse<RhinoDoc> ResolveDocument(string filePath)
    {
        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null)
        {
            return OperationResponse<RhinoDoc>.Fail("NO_ACTIVE_DOCUMENT");
        }
        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return OperationResponse<RhinoDoc>.Fail("ACTIVE_DOC_UNSAVED");
        }
        try
        {
            if (!string.Equals(
                Path.GetFullPath(document.Path).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<RhinoDoc>.Fail("FILE_NOT_ACTIVE");
            }
        }
        catch (Exception ex)
        {
            return OperationResponse<RhinoDoc>.Fail($"DOCUMENT_PATH_INVALID: {ex.Message}");
        }
        return OperationResponse<RhinoDoc>.Ok(document);
    }

    private static void RollBackCreatedObjects(RhinoDoc document, IEnumerable<Guid> objectIds)
    {
        foreach (Guid objectId in objectIds.Reverse())
        {
            document.Objects.Delete(objectId, quiet: true);
        }
    }

    [GeneratedRegex(@"[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    private sealed record PreparedCell(
        Guid SourcePanelId,
        PanelCladdingSpawnCellPlan Cell,
        Brep Geometry);

    private sealed class LocalPanelFrame
    {
        public Plane Frame { get; init; } = Plane.Unset;
        public double XMin { get; init; }
        public double XMax { get; init; }
        public double YMin { get; init; }
        public double YMax { get; init; }
    }
}
