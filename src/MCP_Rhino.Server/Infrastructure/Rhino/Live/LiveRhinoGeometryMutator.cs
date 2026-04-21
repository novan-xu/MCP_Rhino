extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using NurbsCurve = rhinocommon::Rhino.Geometry.NurbsCurve;
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using RhinoTransform = rhinocommon::Rhino.Geometry.Transform;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoGeometryMutator : IGeometryMutator
{
    private readonly ILiveGeometryBuilder _builder;

    public LiveRhinoGeometryMutator(ILiveGeometryBuilder builder)
    {
        _builder = builder;
    }

    public OperationResponse<ObjectEditOperationResult> Transform(RhinoObjectInfo target, GeometryTransformSpec spec)
    {
        RhinoObject? currentObject = FindObject(target.ObjectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Object not found: {target.ObjectId}");
        }

        GeometryBase? geometry = currentObject.Geometry?.Duplicate();
        if (geometry is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Object geometry is empty: {target.ObjectId}");
        }

        if (!geometry.Transform(BuildTransform(spec)))
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Transform failed: {target.ObjectId}");
        }

        if (!RhinoDoc.ActiveDoc!.Objects.Replace(target.ObjectId, geometry, false))
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Replace after transform failed: {target.ObjectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = target.ObjectId,
            LayerFullPath = target.LayerFullPath,
            Success = true,
            Messages = new[] { $"Transform: {spec.Kind}" }
        });
    }

    public OperationResponse<ObjectEditOperationResult> Replace(RhinoObjectInfo target, GeometryReplacementSpec spec)
    {
        OperationResponse<GeometryBase> build = _builder.Build(spec.Geometry);
        if (!build.Success || build.Data is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail(build.Message);
        }

        if (!RhinoDoc.ActiveDoc!.Objects.Replace(target.ObjectId, build.Data, false))
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Replace failed: {target.ObjectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = target.ObjectId,
            LayerFullPath = target.LayerFullPath,
            Success = true,
            Messages = new[] { $"ReplaceGeometry: {target.GeometryTypeName} -> {spec.Geometry.Primitive}" }
        });
    }

    public OperationResponse<ObjectEditOperationResult> Delete(RhinoObjectInfo target)
    {
        if (!RhinoDoc.ActiveDoc!.Objects.Delete(target.ObjectId, true))
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Delete failed: {target.ObjectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = target.ObjectId,
            LayerFullPath = target.LayerFullPath,
            Success = true,
            Messages = new[] { "DeleteObject" }
        });
    }

    public OperationResponse<ObjectEditOperationResult> EditControlPoints(
        RhinoObjectInfo target,
        IReadOnlyList<ControlPointEditSpec> specs)
    {
        RhinoObject? currentObject = FindObject(target.ObjectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Object not found: {target.ObjectId}");
        }

        GeometryBase? geometry = currentObject.Geometry?.Duplicate();
        if (geometry is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Object geometry is empty: {target.ObjectId}");
        }

        var messages = new List<string>();
        switch (geometry)
        {
            case NurbsCurve curve:
                foreach (ControlPointEditSpec spec in specs)
                {
                    if (spec.PointIndex is null || spec.PointIndex < 0 || spec.PointIndex >= curve.Points.Count)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail($"Point index out of range: {spec.PointIndex}");
                    }

                    bool updated = spec.Weight.HasValue
                        ? curve.Points.SetPoint(spec.PointIndex.Value, new Point3d(spec.X, spec.Y, spec.Z), spec.Weight.Value)
                        : curve.Points.SetPoint(spec.PointIndex.Value, new Point3d(spec.X, spec.Y, spec.Z));

                    if (!updated)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail($"Failed to edit curve control point: {target.ObjectId}");
                    }

                    messages.Add($"EditControlPoint: CurveIndex={spec.PointIndex}");
                }
                break;

            case NurbsSurface surface:
                foreach (ControlPointEditSpec spec in specs)
                {
                    if (spec.UIndex is null || spec.VIndex is null
                        || spec.UIndex < 0 || spec.VIndex < 0
                        || spec.UIndex >= surface.Points.CountU || spec.VIndex >= surface.Points.CountV)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail(
                            $"Surface control point out of range: ({spec.UIndex},{spec.VIndex})");
                    }

                    bool updated = spec.Weight.HasValue
                        ? surface.Points.SetPoint(spec.UIndex.Value, spec.VIndex.Value, new Point3d(spec.X, spec.Y, spec.Z), spec.Weight.Value)
                        : surface.Points.SetPoint(spec.UIndex.Value, spec.VIndex.Value, new Point3d(spec.X, spec.Y, spec.Z));

                    if (!updated)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail($"Failed to edit surface control point: {target.ObjectId}");
                    }

                    messages.Add($"EditControlPoint: SurfaceUV=({spec.UIndex},{spec.VIndex})");
                }
                break;

            default:
                return OperationResponse<ObjectEditOperationResult>.Fail($"Object is not editable NURBS geometry: {target.ObjectId}");
        }

        if (!RhinoDoc.ActiveDoc!.Objects.Replace(target.ObjectId, geometry, false))
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Replace after control point edit failed: {target.ObjectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = target.ObjectId,
            LayerFullPath = target.LayerFullPath,
            Success = true,
            Messages = messages
        });
    }

    private static RhinoObject? FindObject(Guid objectId)
    {
        return RhinoDoc.ActiveDoc?.Objects.FindId(objectId);
    }

    private static RhinoTransform BuildTransform(GeometryTransformSpec spec)
    {
        return spec.Kind switch
        {
            GeometryTransformKind.Translate => RhinoTransform.Translation(spec.VectorX, spec.VectorY, spec.VectorZ),
            GeometryTransformKind.Rotate => RhinoTransform.Rotation(
                spec.AngleRadians,
                new Vector3d(spec.AxisX, spec.AxisY, spec.AxisZ),
                new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ)),
            GeometryTransformKind.UniformScale => RhinoTransform.Scale(
                new Plane(new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ), Vector3d.ZAxis),
                spec.ScaleFactor,
                spec.ScaleFactor,
                spec.ScaleFactor),
            _ => throw new InvalidOperationException($"Unsupported transform kind: {spec.Kind}")
        };
    }
}
