using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class RhinoGeometryMutator : IGeometryMutator
{
    private readonly IGeometryBuilder _builder;

    public RhinoGeometryMutator(IGeometryBuilder builder)
    {
        _builder = builder;
    }

    public OperationResponse<ObjectEditOperationResult> Transform(
        File3dm model,
        RhinoObjectInfo target,
        GeometryTransformSpec spec)
    {
        File3dmObject? currentObject = FindModelObject(model, target.ObjectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象不存在: {target.ObjectId}");
        }

        GeometryBase? geometry = currentObject.Geometry?.Duplicate();
        if (geometry is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象几何为空，无法执行 Transform: {target.ObjectId}");
        }

        Transform transform = BuildTransform(spec);
        bool transformed = geometry.Transform(transform);
        if (!transformed)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Transform 执行失败: {target.ObjectId}");
        }

        return ReplaceObjectGeometry(model, currentObject, geometry, target.LayerFullPath, $"Transform: {spec.Kind}");
    }

    public OperationResponse<ObjectEditOperationResult> Replace(
        File3dm model,
        RhinoObjectInfo target,
        GeometryReplacementSpec spec)
    {
        File3dmObject? currentObject = FindModelObject(model, target.ObjectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象不存在: {target.ObjectId}");
        }

        OperationResponse<GeometryBase> buildResult = _builder.Build(spec.Geometry);
        if (!buildResult.Success || buildResult.Data is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail(buildResult.Message);
        }

        return ReplaceObjectGeometry(
            model,
            currentObject,
            buildResult.Data,
            target.LayerFullPath,
            $"ReplaceGeometry: {target.GeometryTypeName} -> {spec.Geometry.Primitive}");
    }

    public OperationResponse<ObjectEditOperationResult> Delete(File3dm model, RhinoObjectInfo target)
    {
        File3dmObject? currentObject = FindModelObject(model, target.ObjectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象不存在: {target.ObjectId}");
        }

        bool deleted = model.Objects.Delete(currentObject.Attributes.ObjectId);
        if (!deleted)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"删除对象失败: {target.ObjectId}");
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
        File3dm model,
        RhinoObjectInfo target,
        IReadOnlyList<ControlPointEditSpec> specs)
    {
        File3dmObject? currentObject = FindModelObject(model, target.ObjectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象不存在: {target.ObjectId}");
        }

        GeometryBase? geometry = currentObject.Geometry?.Duplicate();
        if (geometry is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象几何为空，无法执行控制点编辑: {target.ObjectId}");
        }

        var messages = new List<string>();
        switch (geometry)
        {
            case NurbsCurve curve:
                foreach (ControlPointEditSpec spec in specs)
                {
                    if (spec.PointIndex is null || spec.PointIndex < 0 || spec.PointIndex >= curve.Points.Count)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail(
                            $"控制点索引越界: ObjectId={target.ObjectId}, PointIndex={spec.PointIndex}");
                    }

                    bool updated = spec.Weight.HasValue
                        ? curve.Points.SetPoint(spec.PointIndex.Value, new Point3d(spec.X, spec.Y, spec.Z), spec.Weight.Value)
                        : curve.Points.SetPoint(spec.PointIndex.Value, new Point3d(spec.X, spec.Y, spec.Z));

                    if (!updated)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail(
                            $"更新 NurbsCurve 控制点失败: ObjectId={target.ObjectId}, PointIndex={spec.PointIndex}");
                    }

                    messages.Add($"EditControlPoint: CurveIndex={spec.PointIndex}");
                }
                break;

            case NurbsSurface surface:
                foreach (ControlPointEditSpec spec in specs)
                {
                    if (spec.UIndex is null || spec.VIndex is null
                        || spec.UIndex < 0 || spec.VIndex < 0
                        || spec.UIndex >= surface.Points.CountU
                        || spec.VIndex >= surface.Points.CountV)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail(
                            $"控制点索引越界: ObjectId={target.ObjectId}, UIndex={spec.UIndex}, VIndex={spec.VIndex}");
                    }

                    bool updated = spec.Weight.HasValue
                        ? surface.Points.SetPoint(spec.UIndex.Value, spec.VIndex.Value, new Point3d(spec.X, spec.Y, spec.Z), spec.Weight.Value)
                        : surface.Points.SetPoint(spec.UIndex.Value, spec.VIndex.Value, new Point3d(spec.X, spec.Y, spec.Z));

                    if (!updated)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail(
                            $"更新 NurbsSurface 控制点失败: ObjectId={target.ObjectId}, UIndex={spec.UIndex}, VIndex={spec.VIndex}");
                    }

                    messages.Add($"EditControlPoint: SurfaceUV=({spec.UIndex},{spec.VIndex})");
                }
                break;

            default:
                return OperationResponse<ObjectEditOperationResult>.Fail(
                    $"对象不是可编辑的 NurbsCurve / NurbsSurface: {target.ObjectId}");
        }

        return ReplaceObjectGeometry(model, currentObject, geometry, target.LayerFullPath, messages);
    }

    private static OperationResponse<ObjectEditOperationResult> ReplaceObjectGeometry(
        File3dm model,
        File3dmObject currentObject,
        GeometryBase geometry,
        string layerFullPath,
        string message)
    {
        return ReplaceObjectGeometry(model, currentObject, geometry, layerFullPath, new[] { message });
    }

    private static OperationResponse<ObjectEditOperationResult> ReplaceObjectGeometry(
        File3dm model,
        File3dmObject currentObject,
        GeometryBase geometry,
        string layerFullPath,
        IReadOnlyList<string> messages)
    {
        ObjectAttributes attributes = currentObject.Attributes.Duplicate();
        attributes.ObjectId = currentObject.Attributes.ObjectId;

        bool deleted = model.Objects.Delete(currentObject.Attributes.ObjectId);
        if (!deleted)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"删除原对象失败: {currentObject.Attributes.ObjectId}");
        }

        Guid newObjectId = model.Objects.Add(geometry, attributes);
        if (newObjectId == Guid.Empty)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"重新写入对象失败: {currentObject.Attributes.ObjectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = attributes.ObjectId != Guid.Empty ? attributes.ObjectId : newObjectId,
            LayerFullPath = layerFullPath,
            Success = true,
            Messages = messages
        });
    }

    private static File3dmObject? FindModelObject(File3dm model, Guid objectId)
    {
        foreach (File3dmObject modelObject in model.Objects)
        {
            if (modelObject.Attributes.ObjectId == objectId)
            {
                return modelObject;
            }
        }

        return null;
    }

    private static Transform BuildTransform(GeometryTransformSpec spec)
    {
        return spec.Kind switch
        {
            GeometryTransformKind.Translate => global::Rhino.Geometry.Transform.Translation(spec.VectorX, spec.VectorY, spec.VectorZ),
            GeometryTransformKind.Rotate => global::Rhino.Geometry.Transform.Rotation(
                spec.AngleRadians,
                new Vector3d(spec.AxisX, spec.AxisY, spec.AxisZ),
                new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ)),
            GeometryTransformKind.UniformScale => global::Rhino.Geometry.Transform.Scale(
                new Plane(new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ), Vector3d.ZAxis),
                spec.ScaleFactor,
                spec.ScaleFactor,
                spec.ScaleFactor),
            _ => throw new InvalidOperationException($"不支持的变换类型: {spec.Kind}")
        };
    }
}
