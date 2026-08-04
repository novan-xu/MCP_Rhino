extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Point = rhinocommon::Rhino.Geometry.Point;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Application.Services.Analysis;

internal static class LiveGeometryAnalysisHelpers
{
    internal const int MaxObjectIdsPerRequest = 5000;
    internal const int WarningSampleThreshold = 1000;
    internal const int HardSampleThreshold = 10000;
    internal const int HardGeneratedCurveThreshold = 10000;
    private const string TemporaryLayerPlaceholder = "__analysis__";

    internal static string NormalizeEntryId(string? entryId, string prefix, int index)
    {
        return string.IsNullOrWhiteSpace(entryId)
            ? $"{prefix}-{index + 1}"
            : entryId.Trim();
    }

    internal static GeometryPointData ToPointData(Point3d point)
    {
        return new GeometryPointData
        {
            X = point.X,
            Y = point.Y,
            Z = point.Z
        };
    }

    internal static GeometryVectorData ToVectorData(Vector3d vector)
    {
        return new GeometryVectorData
        {
            X = vector.X,
            Y = vector.Y,
            Z = vector.Z
        };
    }

    internal static string GetGeometryTypeName(GeometryBase geometry)
    {
        return geometry.GetType().Name;
    }

    internal static OperationResponse<ResolvedGeometryReference> ResolveDocumentObject(RhinoDoc document, Guid objectId)
    {
        if (objectId == Guid.Empty)
        {
            return OperationResponse<ResolvedGeometryReference>.Fail("ObjectId cannot be empty.");
        }

        var rhinoObject = document.Objects.FindId(objectId);
        if (rhinoObject?.Geometry is null)
        {
            return OperationResponse<ResolvedGeometryReference>.Fail($"Object was not found in active document: {objectId}");
        }

        GeometryBase? duplicate = rhinoObject.Geometry.Duplicate();
        if (duplicate is null)
        {
            return OperationResponse<ResolvedGeometryReference>.Fail($"Failed to duplicate geometry for ObjectId: {objectId}");
        }

        return OperationResponse<ResolvedGeometryReference>.Ok(new ResolvedGeometryReference
        {
            ObjectId = objectId,
            GeometryTypeName = GetGeometryTypeName(duplicate),
            Geometry = duplicate,
            IsTemporary = false
        });
    }

    internal static OperationResponse<ResolvedGeometryReference> ResolveReference(
        RhinoDoc document,
        GeometryAnalysisReferenceRequest request,
        ILiveGeometryValidator validator,
        ILiveGeometryBuilder builder,
        bool allowTemporary)
    {
        if (request.ObjectId != Guid.Empty)
        {
            OperationResponse<ResolvedGeometryReference> resolved = ResolveDocumentObject(document, request.ObjectId);
            if (!resolved.Success || resolved.Data is null)
            {
                return resolved;
            }

            resolved.Data.Parameter = request.Parameter;
            resolved.Data.U = request.U;
            resolved.Data.V = request.V;
            return resolved;
        }

        if (!allowTemporary)
        {
            return OperationResponse<ResolvedGeometryReference>.Fail("This live analysis entry only accepts ObjectId inputs.");
        }

        if (request.Geometry is null)
        {
            return OperationResponse<ResolvedGeometryReference>.Fail("Each entry requires either ObjectId or temporary Geometry spec.");
        }

        OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = validator.Validate(
            request.Geometry,
            new GeometryObjectAttributesSpec
            {
                LayerFullPath = TemporaryLayerPlaceholder
            });
        if (!validation.Success)
        {
            return OperationResponse<ResolvedGeometryReference>.Fail(validation.Message);
        }

        OperationResponse<GeometryBase> build = builder.Build(request.Geometry);
        if (!build.Success || build.Data is null)
        {
            return OperationResponse<ResolvedGeometryReference>.Fail(build.Message);
        }

        return OperationResponse<ResolvedGeometryReference>.Ok(new ResolvedGeometryReference
        {
            ObjectId = Guid.Empty,
            GeometryTypeName = GetGeometryTypeName(build.Data),
            Geometry = build.Data,
            Parameter = request.Parameter,
            U = request.U,
            V = request.V,
            IsTemporary = true
        });
    }

    internal static bool TryGetExplicitPoint(ResolvedGeometryReference reference, out Point3d point)
    {
        switch (reference.Geometry)
        {
            case Point pointGeometry:
                point = pointGeometry.Location;
                return true;
            case Curve curve when reference.Parameter.HasValue:
                point = curve.PointAt(reference.Parameter.Value);
                return true;
            case Surface surface when reference.U.HasValue && reference.V.HasValue:
                point = surface.PointAt(reference.U.Value, reference.V.Value);
                return true;
            default:
                point = Point3d.Unset;
                return false;
        }
    }

    internal static bool TryGetCurveWithParameter(ResolvedGeometryReference reference, out Curve? curve, out double parameter)
    {
        if (reference.Geometry is Curve curveGeometry)
        {
            curve = curveGeometry;
            parameter = reference.Parameter ?? curveGeometry.Domain.Mid;
            return true;
        }

        curve = null;
        parameter = 0d;
        return false;
    }
}
