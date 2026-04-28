extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Application.Services.Analysis;

public sealed class RhinoGeometryMetricsService
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly IGeometryValidator _validator;
    private readonly ILiveGeometryBuilder _builder;
    private readonly ILiveGeometryMetricsCalculator _calculator;
    private readonly IGeometryFrameSampler _frameSampler;

    public RhinoGeometryMetricsService(
        ILiveRhinoDocumentAccessor documentAccessor,
        IGeometryValidator validator,
        ILiveGeometryBuilder builder,
        ILiveGeometryMetricsCalculator calculator,
        IGeometryFrameSampler frameSampler)
    {
        _documentAccessor = documentAccessor;
        _validator = validator;
        _builder = builder;
        _calculator = calculator;
        _frameSampler = frameSampler;
    }

    public OperationResponse<GetObjectMetricsInLiveResponse> GetObjectMetricsInLive(GetObjectMetricsInLiveRequest request)
    {
        List<Guid> objectIds = request.ObjectIds
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();
        if (objectIds.Count == 0)
        {
            return OperationResponse<GetObjectMetricsInLiveResponse>.Fail("At least one non-empty ObjectId is required.");
        }

        if (objectIds.Count > LiveGeometryAnalysisHelpers.MaxObjectIdsPerRequest)
        {
            return OperationResponse<GetObjectMetricsInLiveResponse>.Fail("ObjectId count exceeds 5000. Filter by layer or split the request into smaller batches.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var results = new List<GeometryMetricsResult>(objectIds.Count);
            foreach (Guid objectId in objectIds)
            {
                OperationResponse<ResolvedGeometryReference> resolved = LiveGeometryAnalysisHelpers.ResolveDocumentObject(document, objectId);
                if (!resolved.Success || resolved.Data is null)
                {
                    results.Add(new GeometryMetricsResult
                    {
                        ObjectId = objectId,
                        Success = false,
                        Message = resolved.Message
                    });
                    continue;
                }

                OperationResponse<GeometryMetricsResult> metrics = _calculator.GetObjectMetrics(
                    objectId,
                    resolved.Data.GeometryTypeName,
                    resolved.Data.Geometry);
                results.Add(metrics.Success && metrics.Data is not null
                    ? metrics.Data
                    : new GeometryMetricsResult
                    {
                        ObjectId = objectId,
                        GeometryTypeName = resolved.Data.GeometryTypeName,
                        Success = false,
                        Message = metrics.Message
                    });
            }

            var response = new GetObjectMetricsInLiveResponse
            {
                FilePath = request.FilePath,
                RequestedCount = objectIds.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            };

            return OperationResponse<GetObjectMetricsInLiveResponse>.Ok(response, "Live object metrics completed.");
        });
    }

    public OperationResponse<MeasureDistancesInLiveResponse> MeasureDistancesInLive(MeasureDistancesInLiveRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<MeasureDistancesInLiveResponse>.Fail("At least one distance entry is required.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var results = new List<GeometryDistanceResult>(request.Entries.Count);
            for (int i = 0; i < request.Entries.Count; i++)
            {
                MeasureDistanceEntryRequest entry = request.Entries[i];
                string entryId = LiveGeometryAnalysisHelpers.NormalizeEntryId(entry.EntryId, "distance", i);

                OperationResponse<ResolvedGeometryReference> fromResolved = LiveGeometryAnalysisHelpers.ResolveReference(
                    document,
                    entry.From,
                    _validator,
                    _builder,
                    allowTemporary: true);
                if (!fromResolved.Success || fromResolved.Data is null)
                {
                    results.Add(new GeometryDistanceResult
                    {
                        EntryId = entryId,
                        Success = false,
                        Message = fromResolved.Message
                    });
                    continue;
                }

                OperationResponse<ResolvedGeometryReference> toResolved = LiveGeometryAnalysisHelpers.ResolveReference(
                    document,
                    entry.To,
                    _validator,
                    _builder,
                    allowTemporary: true);
                if (!toResolved.Success || toResolved.Data is null)
                {
                    results.Add(new GeometryDistanceResult
                    {
                        EntryId = entryId,
                        FromGeometryTypeName = fromResolved.Data.GeometryTypeName,
                        Success = false,
                        Message = toResolved.Message
                    });
                    continue;
                }

                double tolerance = entry.Tolerance ?? document.ModelAbsoluteTolerance;
                OperationResponse<GeometryDistanceResult> distance = _calculator.MeasureDistance(
                    entryId,
                    fromResolved.Data,
                    toResolved.Data,
                    tolerance);

                results.Add(distance.Success && distance.Data is not null
                    ? distance.Data
                    : new GeometryDistanceResult
                    {
                        EntryId = entryId,
                        FromGeometryTypeName = fromResolved.Data.GeometryTypeName,
                        ToGeometryTypeName = toResolved.Data.GeometryTypeName,
                        Success = false,
                        Message = distance.Message
                    });
            }

            var response = new MeasureDistancesInLiveResponse
            {
                FilePath = request.FilePath,
                RequestedCount = request.Entries.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            };

            return OperationResponse<MeasureDistancesInLiveResponse>.Ok(response, "Live distance measurement completed.");
        });
    }

    public OperationResponse<MeasureAnglesInLiveResponse> MeasureAnglesInLive(MeasureAnglesInLiveRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<MeasureAnglesInLiveResponse>.Fail("At least one angle entry is required.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var results = new List<GeometryAngleResult>(request.Entries.Count);
            for (int i = 0; i < request.Entries.Count; i++)
            {
                MeasureAngleEntryRequest entry = request.Entries[i];
                string entryId = LiveGeometryAnalysisHelpers.NormalizeEntryId(entry.EntryId, "angle", i);

                OperationResponse<GeometryAngleResult> result = entry.Mode switch
                {
                    GeometryAngleMeasurementMode.ThreePoints => MeasureThreePointAngle(document, entryId, entry),
                    GeometryAngleMeasurementMode.TwoVectors => MeasureVectorAngle(entryId, entry),
                    GeometryAngleMeasurementMode.CurveTangents => MeasureCurveTangentAngle(document, entryId, entry),
                    _ => OperationResponse<GeometryAngleResult>.Fail($"Unsupported angle measurement mode: {entry.Mode}")
                };

                results.Add(result.Success && result.Data is not null
                    ? result.Data
                    : new GeometryAngleResult
                    {
                        EntryId = entryId,
                        Mode = entry.Mode,
                        Success = false,
                        Message = result.Message
                    });
            }

            var response = new MeasureAnglesInLiveResponse
            {
                FilePath = request.FilePath,
                RequestedCount = request.Entries.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            };

            return OperationResponse<MeasureAnglesInLiveResponse>.Ok(response, "Live angle measurement completed.");
        });
    }

    public OperationResponse<GetGeometryFramesInLiveResponse> GetGeometryFramesInLive(GetGeometryFramesInLiveRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<GetGeometryFramesInLiveResponse>.Fail("At least one frame entry is required.");
        }

        List<Guid> distinctObjectIds = request.Entries
            .Select(entry => entry.ObjectId)
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();
        if (distinctObjectIds.Count > LiveGeometryAnalysisHelpers.MaxObjectIdsPerRequest)
        {
            return OperationResponse<GetGeometryFramesInLiveResponse>.Fail("ObjectId count exceeds 5000. Filter by layer or split the request into smaller batches.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var results = new List<GeometryFrameResult>(request.Entries.Count);
            for (int i = 0; i < request.Entries.Count; i++)
            {
                GeometryFrameEntryRequest entry = request.Entries[i];
                string entryId = LiveGeometryAnalysisHelpers.NormalizeEntryId(entry.EntryId, "frame", i);
                OperationResponse<ResolvedGeometryReference> resolved = LiveGeometryAnalysisHelpers.ResolveDocumentObject(document, entry.ObjectId);
                if (!resolved.Success || resolved.Data is null)
                {
                    results.Add(new GeometryFrameResult
                    {
                        EntryId = entryId,
                        ObjectId = entry.ObjectId,
                        Kind = entry.Kind,
                        Success = false,
                        Message = resolved.Message
                    });
                    continue;
                }

                OperationResponse<GeometryFrameResult> frame = entry.ParameterSpec is not null
                    ? GetSampledFrameResult(entryId, entry, resolved.Data)
                    : _calculator.GetFrame(
                        entryId,
                        entry.ObjectId,
                        resolved.Data.GeometryTypeName,
                        resolved.Data.Geometry,
                        entry.Kind,
                        entry.Parameter,
                        entry.U,
                        entry.V);

                results.Add(frame.Success && frame.Data is not null
                    ? EnsureFrameSamples(frame.Data)
                    : new GeometryFrameResult
                    {
                        EntryId = entryId,
                        ObjectId = entry.ObjectId,
                        GeometryTypeName = resolved.Data.GeometryTypeName,
                        Kind = entry.Kind,
                        Success = false,
                        Message = frame.Message
                    });
            }

            var response = new GetGeometryFramesInLiveResponse
            {
                FilePath = request.FilePath,
                RequestedCount = request.Entries.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            };

            return OperationResponse<GetGeometryFramesInLiveResponse>.Ok(response, "Live geometry frame inspection completed.");
        });
    }

    private OperationResponse<GeometryFrameResult> GetSampledFrameResult(
        string entryId,
        GeometryFrameEntryRequest entry,
        ResolvedGeometryReference resolved)
    {
        if (entry.ParameterSpec is null)
        {
            return OperationResponse<GeometryFrameResult>.Fail("ParameterSpec is required.");
        }

        OperationResponse<IReadOnlyList<FrameSample>> samples = _frameSampler.Sample(resolved.Geometry, entry.ParameterSpec);
        if (!samples.Success || samples.Data is null)
        {
            return OperationResponse<GeometryFrameResult>.Fail(samples.Message);
        }

        FrameSample? first = samples.Data.FirstOrDefault();
        return OperationResponse<GeometryFrameResult>.Ok(new GeometryFrameResult
        {
            EntryId = entryId,
            ObjectId = entry.ObjectId,
            GeometryTypeName = resolved.GeometryTypeName,
            Kind = entry.Kind,
            Success = true,
            Message = samples.Data.Any(sample => sample.IsDegenerate) ? "FRAME_DEGENERATE" : string.Empty,
            Parameter = first?.Parameter,
            U = first?.U,
            V = first?.V,
            Origin = first?.Origin,
            Tangent = first?.Tangent,
            Normal = first?.Normal,
            XAxis = first?.XAxis,
            YAxis = first?.YAxis,
            ZAxis = first?.ZAxis,
            Samples = samples.Data
        });
    }

    private static GeometryFrameResult EnsureFrameSamples(GeometryFrameResult result)
    {
        if (result.Samples.Count > 0)
        {
            return result;
        }

        result.Samples = new[]
        {
            new FrameSample
            {
                Index = 0,
                Parameter = result.Parameter,
                U = result.U,
                V = result.V,
                Origin = result.Origin,
                Tangent = result.Tangent,
                Normal = result.Normal,
                XAxis = result.XAxis,
                YAxis = result.YAxis,
                ZAxis = result.ZAxis,
                IsDegenerate = false,
                Message = result.Message
            }
        };

        return result;
    }

    private OperationResponse<GeometryAngleResult> MeasureThreePointAngle(
        rhinocommon::Rhino.RhinoDoc document,
        string entryId,
        MeasureAngleEntryRequest entry)
    {
        if (entry.PointA is null || entry.Vertex is null || entry.PointB is null)
        {
            return OperationResponse<GeometryAngleResult>.Fail("ThreePoints mode requires PointA, Vertex, and PointB.");
        }

        OperationResponse<ResolvedGeometryReference> first = LiveGeometryAnalysisHelpers.ResolveReference(document, entry.PointA, _validator, _builder, allowTemporary: true);
        OperationResponse<ResolvedGeometryReference> vertex = LiveGeometryAnalysisHelpers.ResolveReference(document, entry.Vertex, _validator, _builder, allowTemporary: true);
        OperationResponse<ResolvedGeometryReference> second = LiveGeometryAnalysisHelpers.ResolveReference(document, entry.PointB, _validator, _builder, allowTemporary: true);
        if (!first.Success || first.Data is null)
        {
            return OperationResponse<GeometryAngleResult>.Fail(first.Message);
        }

        if (!vertex.Success || vertex.Data is null)
        {
            return OperationResponse<GeometryAngleResult>.Fail(vertex.Message);
        }

        if (!second.Success || second.Data is null)
        {
            return OperationResponse<GeometryAngleResult>.Fail(second.Message);
        }

        if (!LiveGeometryAnalysisHelpers.TryGetExplicitPoint(first.Data, out Point3d pointA)
            || !LiveGeometryAnalysisHelpers.TryGetExplicitPoint(vertex.Data, out Point3d pointVertex)
            || !LiveGeometryAnalysisHelpers.TryGetExplicitPoint(second.Data, out Point3d pointB))
        {
            return OperationResponse<GeometryAngleResult>.Fail("ThreePoints mode requires point-like references or explicit curve/surface parameters.");
        }

        return _calculator.MeasureThreePointAngle(entryId, pointA, pointVertex, pointB);
    }

    private OperationResponse<GeometryAngleResult> MeasureVectorAngle(string entryId, MeasureAngleEntryRequest entry)
    {
        if (entry.FirstVector is null || entry.SecondVector is null)
        {
            return OperationResponse<GeometryAngleResult>.Fail("TwoVectors mode requires FirstVector and SecondVector.");
        }

        return _calculator.MeasureVectorAngle(
            entryId,
            new Vector3d(entry.FirstVector.X, entry.FirstVector.Y, entry.FirstVector.Z),
            new Vector3d(entry.SecondVector.X, entry.SecondVector.Y, entry.SecondVector.Z));
    }

    private OperationResponse<GeometryAngleResult> MeasureCurveTangentAngle(
        rhinocommon::Rhino.RhinoDoc document,
        string entryId,
        MeasureAngleEntryRequest entry)
    {
        if (entry.First is null || entry.Second is null)
        {
            return OperationResponse<GeometryAngleResult>.Fail("CurveTangents mode requires First and Second references.");
        }

        OperationResponse<ResolvedGeometryReference> first = LiveGeometryAnalysisHelpers.ResolveReference(document, entry.First, _validator, _builder, allowTemporary: true);
        OperationResponse<ResolvedGeometryReference> second = LiveGeometryAnalysisHelpers.ResolveReference(document, entry.Second, _validator, _builder, allowTemporary: true);
        if (!first.Success || first.Data is null)
        {
            return OperationResponse<GeometryAngleResult>.Fail(first.Message);
        }

        if (!second.Success || second.Data is null)
        {
            return OperationResponse<GeometryAngleResult>.Fail(second.Message);
        }

        if (!LiveGeometryAnalysisHelpers.TryGetCurveWithParameter(first.Data, out var firstCurve, out double firstParameter) || firstCurve is null)
        {
            return OperationResponse<GeometryAngleResult>.Fail("First reference must resolve to a curve for CurveTangents mode.");
        }

        if (!LiveGeometryAnalysisHelpers.TryGetCurveWithParameter(second.Data, out var secondCurve, out double secondParameter) || secondCurve is null)
        {
            return OperationResponse<GeometryAngleResult>.Fail("Second reference must resolve to a curve for CurveTangents mode.");
        }

        Vector3d firstTangent = firstCurve.TangentAt(firstParameter);
        Vector3d secondTangent = secondCurve.TangentAt(secondParameter);
        return _calculator.MeasureVectorAngle(entryId, firstTangent, secondTangent);
    }
}
