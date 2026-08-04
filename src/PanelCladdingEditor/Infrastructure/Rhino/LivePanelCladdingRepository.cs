extern alias rhinocommon;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Mesh = rhinocommon::Rhino.Geometry.Mesh;
using MeshingParameters = rhinocommon::Rhino.Geometry.MeshingParameters;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PlaneFitResult = rhinocommon::Rhino.Geometry.PlaneFitResult;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoMath = rhinocommon::Rhino.RhinoMath;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using UnitSystem = rhinocommon::Rhino.UnitSystem;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed partial class LivePanelCladdingRepository : ILivePanelCladdingRepository
{
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelAxonometricProjectionService _projection;

    public LivePanelCladdingRepository(
        PanelCladdingKeyService keys,
        PanelAxonometricProjectionService projection)
    {
        _keys = keys;
        _projection = projection;
    }

    public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        return !resolved.Success || resolved.Data is null
            ? OperationResponse<PanelCladdingLayout>.Fail(resolved.Message)
            : ReadLayoutOnMainThread(resolved.Data, objectId);
    }

    public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
        PanelAttributeCommitRequest request,
        Func<OperationResponse> finalizeExternalCommit)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(request.FilePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelAttributeCommitResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;
        {
            RhinoObject? rhinoObject = document.Objects.FindId(request.ObjectId);
            if (rhinoObject?.Geometry is not Brep brep)
            {
                return OperationResponse<PanelAttributeCommitResult>.Fail("PANEL_CLADDING_BREP_NOT_FOUND");
            }

            string currentFingerprint = BuildFingerprint(rhinoObject, brep);
            if (!string.Equals(currentFingerprint, request.ExpectedGeometryFingerprint, StringComparison.Ordinal))
            {
                return OperationResponse<PanelAttributeCommitResult>.Fail(
                    "PANEL_CLADDING_STALE_EDITOR: panel geometry or attributes changed; reload before saving.");
            }

            var originalAttributes = rhinoObject.Attributes.Duplicate();
            var proposedAttributes = rhinoObject.Attributes.Duplicate();
            bool changed = false;
            foreach ((string key, string value) in request.UserTextWrites)
            {
                if (!string.Equals(proposedAttributes.GetUserString(key), value, StringComparison.Ordinal))
                {
                    proposedAttributes.SetUserString(key, value);
                    changed = true;
                }
            }

            string? oldWorkbookPath = document.Strings.GetValue(PanelCladdingKeyService.WorkbookPathDocumentKey);
            bool workbookPathChanged = !string.IsNullOrWhiteSpace(request.WorkbookPath) &&
                !string.Equals(oldWorkbookPath, request.WorkbookPath, StringComparison.OrdinalIgnoreCase);

            if (!changed && !workbookPathChanged)
            {
                OperationResponse externalOnly = finalizeExternalCommit();
                return externalOnly.Success
                    ? OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
                    {
                        ObjectId = request.ObjectId,
                        Mutated = false
                    })
                    : OperationResponse<PanelAttributeCommitResult>.Fail(externalOnly.Message);
            }

            uint undoRecord = document.BeginUndoRecord("Assign Panel Cladding Type");
            bool objectModified = false;
            try
            {
                if (changed)
                {
                    objectModified = document.Objects.ModifyAttributes(rhinoObject, proposedAttributes, quiet: true);
                    if (!objectModified)
                    {
                        return OperationResponse<PanelAttributeCommitResult>.Fail("PANEL_CLADDING_ATTRIBUTE_COMMIT_FAILED");
                    }
                }

                if (workbookPathChanged)
                {
                    document.Strings.SetString(PanelCladdingKeyService.WorkbookPathDocumentKey, request.WorkbookPath!);
                }

                OperationResponse external = finalizeExternalCommit();
                if (!external.Success)
                {
                    if (objectModified)
                    {
                        RhinoObject? changedObject = document.Objects.FindId(request.ObjectId);
                        if (changedObject is not null)
                        {
                            document.Objects.ModifyAttributes(changedObject, originalAttributes, quiet: true);
                        }
                    }
                    RestoreDocumentString(document, oldWorkbookPath);
                    return OperationResponse<PanelAttributeCommitResult>.Fail(external.Message);
                }

                document.Views.Redraw();
                return OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
                {
                    ObjectId = request.ObjectId,
                    Mutated = changed || workbookPathChanged
                });
            }
            finally
            {
                if (undoRecord != 0U)
                {
                    document.EndUndoRecord(undoRecord);
                }
            }
        }
    }

    public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath)
    {
        if (string.IsNullOrWhiteSpace(workbookPath))
        {
            return OperationResponse<string>.Fail("PANEL_CLADDING_WORKBOOK_PATH_REQUIRED");
        }
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(workbookPath);
        }
        catch (Exception ex)
        {
            return OperationResponse<string>.Fail($"PANEL_CLADDING_WORKBOOK_PATH_INVALID: {ex.Message}");
        }
        if (!string.Equals(Path.GetExtension(fullPath), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResponse<string>.Fail("PANEL_CLADDING_WORKBOOK_XLSX_REQUIRED");
        }

        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<string>.Fail(resolved.Message);
        }
        resolved.Data.Strings.SetString(PanelCladdingKeyService.WorkbookPathDocumentKey, fullPath);
        return OperationResponse<string>.Ok(fullPath);
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

    private OperationResponse<PanelCladdingLayout> ReadLayoutOnMainThread(RhinoDoc document, Guid objectId)
    {
        RhinoObject? rhinoObject = document.Objects.FindId(objectId);
        if (rhinoObject?.Geometry is not Brep brep)
        {
            return OperationResponse<PanelCladdingLayout>.Fail("PANEL_CLADDING_BREP_NOT_FOUND");
        }

        OperationResponse<LocalMesh> localMeshResponse = BuildLocalMesh(brep, document.ModelAbsoluteTolerance);
        if (!localMeshResponse.Success || localMeshResponse.Data is null)
        {
            return OperationResponse<PanelCladdingLayout>.Fail(localMeshResponse.Message);
        }
        LocalMesh local = localMeshResponse.Data;
        double width = local.XMax - local.XMin;
        double height = local.YMax - local.YMin;
        if (width <= document.ModelAbsoluteTolerance || height <= document.ModelAbsoluteTolerance)
        {
            return OperationResponse<PanelCladdingLayout>.Fail("PANEL_CLADDING_PANEL_EXTENT_INVALID");
        }

        IReadOnlyDictionary<string, string> userText = ReadUserText(rhinoObject);
        OperationResponse<PanelCladdingKeySet> keys = _keys.Parse(
            userText,
            width,
            height,
            document.ModelAbsoluteTolerance);
        if (!keys.Success || keys.Data is null)
        {
            return OperationResponse<PanelCladdingLayout>.Fail(keys.Message);
        }

        OperationResponse<(PanelGeometryClass Classification, string Diagnostic, PanelPreviewGeometry Preview)> preview =
            _projection.Build(
                local.Vertices,
                local.Triangles,
                local.XMin,
                local.XMax,
                local.YMin,
                local.YMax,
                keys.Data.HorizontalOffsets,
                keys.Data.VerticalOffsets,
                keys.Data.Cells,
                document.ModelAbsoluteTolerance);
        if (!preview.Success)
        {
            return OperationResponse<PanelCladdingLayout>.Fail(preview.Message);
        }

        var previewData = preview.Data;
        string layerPath = rhinoObject.Attributes.LayerIndex >= 0
            ? document.Layers[rhinoObject.Attributes.LayerIndex]?.FullPath ?? string.Empty
            : string.Empty;
        string systemCode = layerPath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? "PANEL";
        return OperationResponse<PanelCladdingLayout>.Ok(new PanelCladdingLayout
        {
            ObjectId = objectId,
            DocumentRuntimeSerialNumber = document.RuntimeSerialNumber,
            DocumentPath = document.Path,
            ObjectName = rhinoObject.Attributes.Name ?? string.Empty,
            LayerFullPath = layerPath,
            SystemCode = systemCode,
            GeometryFingerprint = BuildFingerprint(rhinoObject, brep),
            GeometryClass = previewData.Classification,
            GeometryDiagnostic = $"{local.FrameDiagnostic} {previewData.Diagnostic}".Trim(),
            Width = width,
            Height = height,
            ModelTolerance = document.ModelAbsoluteTolerance,
            ModelUnitScaleToMillimeters = RhinoMath.UnitScale(document.ModelUnitSystem, UnitSystem.Millimeters),
            HorizontalOffsets = keys.Data.HorizontalOffsets,
            VerticalOffsets = keys.Data.VerticalOffsets,
            Cells = keys.Data.Cells,
            Preview = previewData.Preview,
            WorkbookPath = document.Strings.GetValue(PanelCladdingKeyService.WorkbookPathDocumentKey) ?? string.Empty
        });
    }

    private static OperationResponse<LocalMesh> BuildLocalMesh(Brep brep, double tolerance)
    {
        Mesh[] pieces = Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh);
        if (pieces.Length == 0)
        {
            pieces = Mesh.CreateFromBrep(brep, MeshingParameters.Default);
        }
        if (pieces.Length == 0)
        {
            return OperationResponse<LocalMesh>.Fail("PANEL_CLADDING_PREVIEW_MESH_FAILED");
        }

        var combined = new Mesh();
        combined.Append(pieces);
        combined.Compact();
        if (combined.Vertices.Count < 3 || combined.Faces.Count == 0)
        {
            return OperationResponse<LocalMesh>.Fail("PANEL_CLADDING_PREVIEW_MESH_EMPTY");
        }
        if (combined.Faces.Count > 100_000)
        {
            return OperationResponse<LocalMesh>.Fail("PANEL_CLADDING_PREVIEW_MESH_TOO_DENSE");
        }

        (Plane Frame, string Diagnostic) frame = ResolveFrame(brep, combined, tolerance);
        var vertices = new List<PanelPoint3>(combined.Vertices.Count);
        foreach (var vertex in combined.Vertices)
        {
            var point = new Point3d(vertex);
            Vector3d delta = point - frame.Frame.Origin;
            vertices.Add(new PanelPoint3(
                delta * frame.Frame.XAxis,
                delta * frame.Frame.YAxis,
                delta * frame.Frame.ZAxis));
        }

        var triangles = new List<PanelTriangle>(combined.Faces.Count * 2);
        foreach (var face in combined.Faces)
        {
            triangles.Add(new PanelTriangle(face.A, face.B, face.C));
            if (face.IsQuad)
            {
                triangles.Add(new PanelTriangle(face.A, face.C, face.D));
            }
        }
        return OperationResponse<LocalMesh>.Ok(new LocalMesh
        {
            Vertices = vertices,
            Triangles = triangles,
            XMin = vertices.Min(point => point.X),
            XMax = vertices.Max(point => point.X),
            YMin = vertices.Min(point => point.Y),
            YMax = vertices.Max(point => point.Y),
            FrameDiagnostic = frame.Diagnostic
        });
    }

    private static (Plane Frame, string Diagnostic) ResolveFrame(Brep brep, Mesh mesh, double tolerance)
    {
        if (TryParseStoredPlane(brep.GetUserString("Plane"), out Plane stored))
        {
            return (OrientFrame(stored), "Frame: stored Plane user string.");
        }
        if (brep.Faces.Count > 0 && brep.Faces[0].TryGetPlane(out Plane planar, tolerance))
        {
            return (OrientFrame(planar), "Frame: planar Brep face.");
        }

        Point3d[] samples = mesh.Vertices.Select(vertex => new Point3d(vertex)).ToArray();
        PlaneFitResult fit = Plane.FitPlaneToPoints(samples, out Plane fitted);
        if (fit == PlaneFitResult.Failure || !fitted.IsValid)
        {
            throw new InvalidOperationException("PANEL_CLADDING_REFERENCE_PLANE_FAILED");
        }
        return (OrientFrame(fitted), "Frame: best-fit curved-panel plane.");
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
        Vector3d referenceRight = source.XAxis - (source.XAxis * normal) * normal - (source.XAxis * up) * up;
        if (referenceRight.Unitize() && right * referenceRight < 0d)
        {
            right.Reverse();
        }
        normal = Vector3d.CrossProduct(right, up);
        normal.Unitize();
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

    private static string BuildFingerprint(RhinoObject rhinoObject, Brep brep)
    {
        var payload = new StringBuilder();
        payload.Append(brep.DataCRC(0U).ToString(CultureInfo.InvariantCulture));
        payload.Append('|').Append(rhinoObject.Attributes.LayerIndex.ToString(CultureInfo.InvariantCulture));
        var strings = ReadUserText(rhinoObject);
        foreach ((string key, string value) in strings.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (key.StartsWith("CW_2.", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("CW_4.", StringComparison.OrdinalIgnoreCase))
            {
                payload.Append('|').Append(key).Append('=').Append(value);
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString()))).ToLowerInvariant();
    }

    private static void RestoreDocumentString(RhinoDoc document, string? oldValue)
    {
        if (oldValue is null)
        {
            document.Strings.Delete(PanelCladdingKeyService.WorkbookPathDocumentKey);
        }
        else
        {
            document.Strings.SetString(PanelCladdingKeyService.WorkbookPathDocumentKey, oldValue);
        }
    }

    [GeneratedRegex(@"[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    private sealed class LocalMesh
    {
        public IReadOnlyList<PanelPoint3> Vertices { get; init; } = Array.Empty<PanelPoint3>();
        public IReadOnlyList<PanelTriangle> Triangles { get; init; } = Array.Empty<PanelTriangle>();
        public double XMin { get; init; }
        public double XMax { get; init; }
        public double YMin { get; init; }
        public double YMax { get; init; }
        public string FrameDiagnostic { get; init; } = string.Empty;
    }
}

