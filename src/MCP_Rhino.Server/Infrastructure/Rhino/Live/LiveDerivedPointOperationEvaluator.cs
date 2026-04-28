extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveDerivedPointOperationEvaluator : IDerivedPointOperationEvaluator
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly IGeometryFrameSampler _frameSampler;

    public LiveDerivedPointOperationEvaluator(
        ILiveRhinoDocumentAccessor documentAccessor,
        IGeometryFrameSampler frameSampler)
    {
        _documentAccessor = documentAccessor;
        _frameSampler = frameSampler;
    }

    public OperationResponse<DerivedPointEvaluationResult> Evaluate(
        string filePath,
        Guid objectId,
        EditableGeometryDescriptor descriptor,
        CurveEditSpec editSpec)
    {
        if (editSpec.Operation.Kind != GeometryEditOperationKind.DerivedOperation || !editSpec.Operation.DerivedKind.HasValue)
        {
            return OperationResponse<DerivedPointEvaluationResult>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        IReadOnlyList<int> indices = ResolveIndices(editSpec.PointSelector, descriptor.ControlPointCount);
        var points = descriptor.Points
            .Select(point => new EditablePointInput
            {
                Index = point.Index,
                X = point.X,
                Y = point.Y,
                Z = point.Z
            })
            .ToList();

        DerivedPointOperationParameters parameters = editSpec.Operation.DerivedParameters ?? new DerivedPointOperationParameters();
        return editSpec.Operation.DerivedKind.Value switch
        {
            DerivedPointOperationKind.ScaleAboutCentroid => EvaluateScale(indices, points, parameters),
            DerivedPointOperationKind.TranslateByVector => EvaluateTranslate(indices, points, parameters),
            DerivedPointOperationKind.OffsetAlongNormal => EvaluateOffset(filePath, objectId, indices, points, parameters),
            _ => OperationResponse<DerivedPointEvaluationResult>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE")
        };
    }

    public OperationResponse<DerivedPointEvaluationResult> EvaluateSurface(
        string filePath,
        Guid objectId,
        EditableGeometryDescriptor descriptor,
        SurfaceEditSpec editSpec)
    {
        if (editSpec.Operation.Kind != GeometryEditOperationKind.DerivedOperation || !editSpec.Operation.DerivedKind.HasValue)
        {
            return OperationResponse<DerivedPointEvaluationResult>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        if (descriptor.SurfaceStructure is null || descriptor.Points.Count != descriptor.ControlPointCount)
        {
            return OperationResponse<DerivedPointEvaluationResult>.Fail("DESCRIPTOR_FULL_REQUIRED");
        }

        IReadOnlyList<int> indices = ResolveSurfaceIndices(editSpec.PointSelector, descriptor.SurfaceStructure);
        var points = descriptor.Points
            .Select(point => new EditablePointInput
            {
                Index = point.Index,
                X = point.X,
                Y = point.Y,
                Z = point.Z
            })
            .ToList();

        SurfaceEditDerivedOperationParameters parameters = editSpec.Operation.DerivedParameters ?? new SurfaceEditDerivedOperationParameters();
        return editSpec.Operation.DerivedKind.Value switch
        {
            DerivedPointOperationKind.ScaleAboutCentroid => EvaluateSurfaceScale(indices, points, parameters),
            DerivedPointOperationKind.TranslateByVector => EvaluateSurfaceTranslate(indices, points, parameters),
            DerivedPointOperationKind.OffsetAlongNormal => EvaluateSurfaceOffset(filePath, objectId, descriptor, indices, points, parameters),
            _ => OperationResponse<DerivedPointEvaluationResult>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE")
        };
    }

    private static OperationResponse<DerivedPointEvaluationResult> EvaluateScale(
        IReadOnlyList<int> indices,
        List<EditablePointInput> points,
        DerivedPointOperationParameters parameters)
    {
        GeometryPointData centroid = ComputeCentroid(indices.Select(index => points[index]).ToList());
        foreach (int index in indices)
        {
            EditablePointInput point = points[index];
            point.X = centroid.X + ((point.X - centroid.X) * parameters.ScaleX);
            point.Y = centroid.Y + ((point.Y - centroid.Y) * parameters.ScaleY);
            point.Z = centroid.Z + ((point.Z - centroid.Z) * parameters.ScaleZ);
        }

        return OperationResponse<DerivedPointEvaluationResult>.Ok(new DerivedPointEvaluationResult
        {
            ResolvedPointIndices = indices,
            NewPoints = points,
            DerivedOperationApplied = new DerivedOperationApplied
            {
                Kind = DerivedPointOperationKind.ScaleAboutCentroid,
                CentroidWorld = centroid,
                ScaleX = parameters.ScaleX,
                ScaleY = parameters.ScaleY,
                ScaleZ = parameters.ScaleZ
            }
        });
    }

    private static OperationResponse<DerivedPointEvaluationResult> EvaluateTranslate(
        IReadOnlyList<int> indices,
        List<EditablePointInput> points,
        DerivedPointOperationParameters parameters)
    {
        foreach (int index in indices)
        {
            EditablePointInput point = points[index];
            point.X += parameters.VectorX;
            point.Y += parameters.VectorY;
            point.Z += parameters.VectorZ;
        }

        return OperationResponse<DerivedPointEvaluationResult>.Ok(new DerivedPointEvaluationResult
        {
            ResolvedPointIndices = indices,
            NewPoints = points,
            DerivedOperationApplied = new DerivedOperationApplied
            {
                Kind = DerivedPointOperationKind.TranslateByVector,
                Vector = new GeometryVectorData
                {
                    X = parameters.VectorX,
                    Y = parameters.VectorY,
                    Z = parameters.VectorZ
                }
            }
        });
    }

    private static OperationResponse<DerivedPointEvaluationResult> EvaluateSurfaceScale(
        IReadOnlyList<int> indices,
        List<EditablePointInput> points,
        SurfaceEditDerivedOperationParameters parameters)
    {
        GeometryPointData centroid = ComputeCentroid(indices.Select(index => points[index]).ToList());
        foreach (int index in indices)
        {
            EditablePointInput point = points[index];
            point.X = centroid.X + ((point.X - centroid.X) * parameters.ScaleX);
            point.Y = centroid.Y + ((point.Y - centroid.Y) * parameters.ScaleY);
            point.Z = centroid.Z + ((point.Z - centroid.Z) * parameters.ScaleZ);
        }

        return OperationResponse<DerivedPointEvaluationResult>.Ok(new DerivedPointEvaluationResult
        {
            ResolvedPointIndices = indices,
            NewPoints = points,
            DerivedOperationApplied = new DerivedOperationApplied
            {
                Kind = DerivedPointOperationKind.ScaleAboutCentroid,
                CentroidWorld = centroid,
                ScaleX = parameters.ScaleX,
                ScaleY = parameters.ScaleY,
                ScaleZ = parameters.ScaleZ
            }
        });
    }

    private static OperationResponse<DerivedPointEvaluationResult> EvaluateSurfaceTranslate(
        IReadOnlyList<int> indices,
        List<EditablePointInput> points,
        SurfaceEditDerivedOperationParameters parameters)
    {
        foreach (int index in indices)
        {
            EditablePointInput point = points[index];
            point.X += parameters.VectorX;
            point.Y += parameters.VectorY;
            point.Z += parameters.VectorZ;
        }

        return OperationResponse<DerivedPointEvaluationResult>.Ok(new DerivedPointEvaluationResult
        {
            ResolvedPointIndices = indices,
            NewPoints = points,
            DerivedOperationApplied = new DerivedOperationApplied
            {
                Kind = DerivedPointOperationKind.TranslateByVector,
                Vector = new GeometryVectorData
                {
                    X = parameters.VectorX,
                    Y = parameters.VectorY,
                    Z = parameters.VectorZ
                }
            }
        });
    }

    private OperationResponse<DerivedPointEvaluationResult> EvaluateOffset(
        string filePath,
        Guid objectId,
        IReadOnlyList<int> indices,
        List<EditablePointInput> points,
        DerivedPointOperationParameters parameters)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            if (document.Objects.FindId(objectId)?.Geometry is not Curve curve)
            {
                return OperationResponse<DerivedPointEvaluationResult>.Fail("EDITABLE_KIND_UNSUPPORTED");
            }

            OperationResponse<IReadOnlyList<FrameSample>> frames = _frameSampler.Sample(curve, new GeometryFrameParameterSpecRequest
            {
                Kind = GeometryFrameParameterSpecKind.AtControlPointGrevilles
            });
            if (!frames.Success || frames.Data is null || frames.Data.Count < points.Count)
            {
                return OperationResponse<DerivedPointEvaluationResult>.Fail("OFFSET_ALONG_NORMAL_REQUIRES_CURVE_WITH_FRAME");
            }

            var warnings = new List<ObjectEditWarning>();
            var degenerateIndices = new List<int>();
            var perPointOffsets = new List<PerPointOffsetData>();

            foreach (int index in indices)
            {
                FrameSample frame = frames.Data[index];
                if (frame.IsDegenerate || frame.YAxis is null)
                {
                    degenerateIndices.Add(index);
                    perPointOffsets.Add(new PerPointOffsetData { Index = index });
                    continue;
                }

                double offsetX = frame.YAxis.X * parameters.Distance;
                double offsetY = frame.YAxis.Y * parameters.Distance;
                double offsetZ = frame.YAxis.Z * parameters.Distance;
                points[index].X += offsetX;
                points[index].Y += offsetY;
                points[index].Z += offsetZ;
                perPointOffsets.Add(new PerPointOffsetData
                {
                    Index = index,
                    Offset = new GeometryVectorData
                    {
                        X = offsetX,
                        Y = offsetY,
                        Z = offsetZ
                    }
                });
            }

            if (degenerateIndices.Count > 0)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "OFFSET_NORMAL_DEGENERATE",
                    Message = $"Degenerate frame indices: {string.Join(",", degenerateIndices)}"
                });
            }

            return OperationResponse<DerivedPointEvaluationResult>.Ok(new DerivedPointEvaluationResult
            {
                ResolvedPointIndices = indices,
                NewPoints = points,
                DerivedOperationApplied = new DerivedOperationApplied
                {
                    Kind = DerivedPointOperationKind.OffsetAlongNormal,
                    Distance = parameters.Distance,
                    PerPointOffsets = perPointOffsets
                },
                Warnings = warnings
            });
        });
    }

    private OperationResponse<DerivedPointEvaluationResult> EvaluateSurfaceOffset(
        string filePath,
        Guid objectId,
        EditableGeometryDescriptor descriptor,
        IReadOnlyList<int> indices,
        List<EditablePointInput> points,
        SurfaceEditDerivedOperationParameters parameters)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            GeometryBase? geometry = document.Objects.FindId(objectId)?.Geometry;
            if (geometry is null)
            {
                return OperationResponse<DerivedPointEvaluationResult>.Fail($"OBJECT_NOT_FOUND: {objectId}");
            }

            OperationResponse<IReadOnlyList<FrameSample>> frames = _frameSampler.Sample(geometry, new GeometryFrameParameterSpecRequest
            {
                Kind = GeometryFrameParameterSpecKind.AtControlPointGrevilles
            });
            if (!frames.Success || frames.Data is null || frames.Data.Count < points.Count)
            {
                return OperationResponse<DerivedPointEvaluationResult>.Fail("OFFSET_NORMAL_DEGENERATE");
            }

            var warnings = new List<ObjectEditWarning>();
            var degenerateIndices = new List<int>();
            var perPointOffsets = new List<PerPointOffsetData>();
            var uvSamples = new List<ResolvedUvSampleData>();

            foreach (int index in indices)
            {
                FrameSample frame = frames.Data[index];
                uvSamples.Add(CreateUvSample(descriptor, frame, index));
                GeometryVectorData? normal = frame.Normal ?? frame.ZAxis;
                if (frame.IsDegenerate || normal is null)
                {
                    degenerateIndices.Add(index);
                    perPointOffsets.Add(new PerPointOffsetData { Index = index });
                    continue;
                }

                double offsetX = normal.X * parameters.Distance;
                double offsetY = normal.Y * parameters.Distance;
                double offsetZ = normal.Z * parameters.Distance;
                points[index].X += offsetX;
                points[index].Y += offsetY;
                points[index].Z += offsetZ;
                perPointOffsets.Add(new PerPointOffsetData
                {
                    Index = index,
                    Offset = new GeometryVectorData
                    {
                        X = offsetX,
                        Y = offsetY,
                        Z = offsetZ
                    }
                });
            }

            if (degenerateIndices.Count > 0)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "OFFSET_NORMAL_DEGENERATE",
                    Message = $"Degenerate surface frame indices: {string.Join(",", degenerateIndices)}"
                });
            }

            return OperationResponse<DerivedPointEvaluationResult>.Ok(new DerivedPointEvaluationResult
            {
                ResolvedPointIndices = indices,
                NewPoints = points,
                DerivedOperationApplied = new DerivedOperationApplied
                {
                    Kind = DerivedPointOperationKind.OffsetAlongNormal,
                    Distance = parameters.Distance,
                    PerPointOffsets = perPointOffsets,
                    ResolvedUvSamples = uvSamples
                },
                Warnings = warnings
            });
        });
    }

    private static IReadOnlyList<int> ResolveIndices(PointSelectorSpec? selector, int pointCount)
    {
        PointSelectorSpec resolved = selector ?? new PointSelectorSpec { Kind = PointSelectorKind.All };
        return resolved.Kind switch
        {
            PointSelectorKind.All => Enumerable.Range(0, pointCount).ToList(),
            PointSelectorKind.Indices => resolved.Indices.ToList(),
            PointSelectorKind.Range => Enumerable.Range(resolved.Start!.Value, resolved.End!.Value - resolved.Start.Value + 1).ToList(),
            PointSelectorKind.EndpointsOnly => new[] { 0, pointCount - 1 },
            _ => Array.Empty<int>()
        };
    }

    private static IReadOnlyList<int> ResolveSurfaceIndices(SurfacePointSelectorSpec? selector, EditableSurfaceStructure structure)
    {
        SurfacePointSelectorSpec resolved = selector ?? new SurfacePointSelectorSpec { Kind = SurfacePointSelectorKind.All };
        int pointCount = structure.CountU * structure.CountV;
        return resolved.Kind switch
        {
            SurfacePointSelectorKind.All => Enumerable.Range(0, pointCount).ToList(),
            SurfacePointSelectorKind.Indices => resolved.Indices.ToList(),
            SurfacePointSelectorKind.UvRange => ResolveSurfaceUvRange(resolved, structure.CountU, structure.CountV),
            SurfacePointSelectorKind.EdgeOnly => ResolveSurfaceEdges(structure.CountU, structure.CountV),
            _ => Array.Empty<int>()
        };
    }

    private static IReadOnlyList<int> ResolveSurfaceUvRange(SurfacePointSelectorSpec selector, int countU, int countV)
    {
        var indices = new List<int>();
        for (int u = selector.UStart!.Value; u <= selector.UEnd!.Value; u++)
        {
            for (int v = selector.VStart!.Value; v <= selector.VEnd!.Value; v++)
            {
                indices.Add((u * countV) + v);
            }
        }

        return indices;
    }

    private static IReadOnlyList<int> ResolveSurfaceEdges(int countU, int countV)
    {
        var indices = new List<int>();
        var seen = new HashSet<int>();
        for (int u = 0; u < countU; u++)
        {
            Add((u * countV));
            Add((u * countV) + countV - 1);
        }

        for (int v = 0; v < countV; v++)
        {
            Add(v);
            Add(((countU - 1) * countV) + v);
        }

        return indices;

        void Add(int index)
        {
            if (seen.Add(index))
            {
                indices.Add(index);
            }
        }
    }

    private static ResolvedUvSampleData CreateUvSample(
        EditableGeometryDescriptor descriptor,
        FrameSample frame,
        int index)
    {
        EditablePointDescriptor? point = descriptor.Points.FirstOrDefault(candidate => candidate.Index == index);
        return new ResolvedUvSampleData
        {
            Index = index,
            UIndex = point?.UIndex,
            VIndex = point?.VIndex,
            U = frame.U ?? 0d,
            V = frame.V ?? 0d
        };
    }

    private static GeometryPointData ComputeCentroid(IReadOnlyList<EditablePointInput> points)
    {
        double x = 0d;
        double y = 0d;
        double z = 0d;
        foreach (EditablePointInput point in points)
        {
            x += point.X;
            y += point.Y;
            z += point.Z;
        }

        return new GeometryPointData
        {
            X = x / points.Count,
            Y = y / points.Count,
            Z = z / points.Count
        };
    }
}
