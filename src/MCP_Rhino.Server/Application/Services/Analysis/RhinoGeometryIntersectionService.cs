extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services.Analysis;

public sealed class RhinoGeometryIntersectionService
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveGeometryValidator _validator;
    private readonly ILiveGeometryBuilder _builder;
    private readonly ILiveGeometryIntersectionCalculator _calculator;

    public RhinoGeometryIntersectionService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveGeometryValidator validator,
        ILiveGeometryBuilder builder,
        ILiveGeometryIntersectionCalculator calculator)
    {
        _documentAccessor = documentAccessor;
        _validator = validator;
        _builder = builder;
        _calculator = calculator;
    }

    public OperationResponse<GetMassPropertiesInLiveResponse> GetMassPropertiesInLive(GetMassPropertiesInLiveRequest request)
    {
        List<Guid> objectIds = request.ObjectIds
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();
        if (objectIds.Count == 0)
        {
            return OperationResponse<GetMassPropertiesInLiveResponse>.Fail("At least one non-empty ObjectId is required.");
        }

        if (objectIds.Count > LiveGeometryAnalysisHelpers.MaxObjectIdsPerRequest)
        {
            return OperationResponse<GetMassPropertiesInLiveResponse>.Fail("ObjectId count exceeds 5000. Filter by layer or split the request into smaller batches.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var results = new List<GeometryMassResult>(objectIds.Count);
            foreach (Guid objectId in objectIds)
            {
                OperationResponse<ResolvedGeometryReference> resolved = LiveGeometryAnalysisHelpers.ResolveDocumentObject(document, objectId);
                if (!resolved.Success || resolved.Data is null)
                {
                    results.Add(new GeometryMassResult
                    {
                        ObjectId = objectId,
                        Kind = request.Kind,
                        Success = false,
                        Message = resolved.Message
                    });
                    continue;
                }

                OperationResponse<GeometryMassResult> mass = _calculator.GetMassProperties(
                    objectId,
                    resolved.Data.GeometryTypeName,
                    resolved.Data.Geometry,
                    request.Kind);
                results.Add(mass.Success && mass.Data is not null
                    ? mass.Data
                    : new GeometryMassResult
                    {
                        ObjectId = objectId,
                        GeometryTypeName = resolved.Data.GeometryTypeName,
                        Kind = request.Kind,
                        Success = false,
                        Message = mass.Message
                    });
            }

            var response = new GetMassPropertiesInLiveResponse
            {
                FilePath = request.FilePath,
                RequestedCount = objectIds.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            };

            return OperationResponse<GetMassPropertiesInLiveResponse>.Ok(response, "Live mass-properties analysis completed.");
        });
    }

    public OperationResponse<IntersectObjectsInLiveResponse> IntersectObjectsInLive(IntersectObjectsInLiveRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<IntersectObjectsInLiveResponse>.Fail("At least one intersection entry is required.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var results = new List<GeometryIntersectionResult>(request.Entries.Count);
            int producedCurveCount = 0;

            for (int i = 0; i < request.Entries.Count; i++)
            {
                GeometryIntersectionEntryRequest entry = request.Entries[i];
                string entryId = LiveGeometryAnalysisHelpers.NormalizeEntryId(entry.EntryId, "intersection", i);
                OperationResponse<ResolvedGeometryReference> first = LiveGeometryAnalysisHelpers.ResolveReference(document, entry.First, _validator, _builder, allowTemporary: true);
                OperationResponse<ResolvedGeometryReference> second = LiveGeometryAnalysisHelpers.ResolveReference(document, entry.Second, _validator, _builder, allowTemporary: true);
                if (!first.Success || first.Data is null)
                {
                    results.Add(new GeometryIntersectionResult
                    {
                        EntryId = entryId,
                        Kind = entry.Kind,
                        Success = false,
                        Message = first.Message
                    });
                    continue;
                }

                if (!second.Success || second.Data is null)
                {
                    results.Add(new GeometryIntersectionResult
                    {
                        EntryId = entryId,
                        Kind = entry.Kind,
                        FirstGeometryTypeName = first.Data.GeometryTypeName,
                        Success = false,
                        Message = second.Message
                    });
                    continue;
                }

                entry.EntryId = entryId;
                double tolerance = entry.Tolerance ?? document.ModelAbsoluteTolerance;
                OperationResponse<GeometryIntersectionResult> intersection = _calculator.Intersect(entry, first.Data, second.Data, tolerance);
                GeometryIntersectionResult payload = intersection.Success && intersection.Data is not null
                    ? intersection.Data
                    : new GeometryIntersectionResult
                    {
                        EntryId = entryId,
                        Kind = entry.Kind,
                        FirstGeometryTypeName = first.Data.GeometryTypeName,
                        SecondGeometryTypeName = second.Data.GeometryTypeName,
                        Success = false,
                        Message = intersection.Message
                    };

                producedCurveCount += payload.Curves.Count + payload.OverlapCurves.Count;
                if (producedCurveCount > LiveGeometryAnalysisHelpers.HardGeneratedCurveThreshold)
                {
                    return OperationResponse<IntersectObjectsInLiveResponse>.Fail("Intersection result count exceeds 10000 curves. Filter by layer or split the request into smaller batches.");
                }

                results.Add(payload);
            }

            var response = new IntersectObjectsInLiveResponse
            {
                FilePath = request.FilePath,
                RequestedCount = request.Entries.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            };

            return OperationResponse<IntersectObjectsInLiveResponse>.Ok(response, "Live intersection analysis completed.");
        });
    }

    public OperationResponse<GetClosestPointsInLiveResponse> GetClosestPointsInLive(GetClosestPointsInLiveRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<GetClosestPointsInLiveResponse>.Fail("At least one closest-point entry is required.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var results = new List<GeometryClosestPointResult>(request.Entries.Count);
            for (int i = 0; i < request.Entries.Count; i++)
            {
                GeometryClosestPointEntryRequest entry = request.Entries[i];
                string entryId = LiveGeometryAnalysisHelpers.NormalizeEntryId(entry.EntryId, "closest", i);
                OperationResponse<ResolvedGeometryReference> source = LiveGeometryAnalysisHelpers.ResolveReference(document, entry.Source, _validator, _builder, allowTemporary: true);
                OperationResponse<ResolvedGeometryReference> target = LiveGeometryAnalysisHelpers.ResolveReference(document, entry.Target, _validator, _builder, allowTemporary: true);
                if (!source.Success || source.Data is null)
                {
                    results.Add(new GeometryClosestPointResult
                    {
                        EntryId = entryId,
                        TargetKind = entry.TargetKind,
                        Success = false,
                        Message = source.Message
                    });
                    continue;
                }

                if (!target.Success || target.Data is null)
                {
                    results.Add(new GeometryClosestPointResult
                    {
                        EntryId = entryId,
                        TargetKind = entry.TargetKind,
                        SourceGeometryTypeName = source.Data.GeometryTypeName,
                        Success = false,
                        Message = target.Message
                    });
                    continue;
                }

                entry.EntryId = entryId;
                OperationResponse<GeometryClosestPointResult> closest = _calculator.GetClosestPoints(entry, source.Data, target.Data);
                results.Add(closest.Success && closest.Data is not null
                    ? closest.Data
                    : new GeometryClosestPointResult
                    {
                        EntryId = entryId,
                        TargetKind = entry.TargetKind,
                        SourceGeometryTypeName = source.Data.GeometryTypeName,
                        TargetGeometryTypeName = target.Data.GeometryTypeName,
                        Success = false,
                        Message = closest.Message
                    });
            }

            var response = new GetClosestPointsInLiveResponse
            {
                FilePath = request.FilePath,
                RequestedCount = request.Entries.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            };

            return OperationResponse<GetClosestPointsInLiveResponse>.Ok(response, "Live closest-point analysis completed.");
        });
    }

    public OperationResponse<GetContourCurvesInLiveResponse> GetContourCurvesInLive(GetContourCurvesInLiveRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<GetContourCurvesInLiveResponse>.Fail("At least one contour entry is required.");
        }

        List<Guid> distinctObjectIds = request.Entries
            .Select(entry => entry.ObjectId)
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();
        if (distinctObjectIds.Count > LiveGeometryAnalysisHelpers.MaxObjectIdsPerRequest)
        {
            return OperationResponse<GetContourCurvesInLiveResponse>.Fail("ObjectId count exceeds 5000. Filter by layer or split the request into smaller batches.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var results = new List<GeometryContourResult>(request.Entries.Count);
            int producedCurveCount = 0;
            for (int i = 0; i < request.Entries.Count; i++)
            {
                GeometryContourEntryRequest entry = request.Entries[i];
                string entryId = LiveGeometryAnalysisHelpers.NormalizeEntryId(entry.EntryId, "contour", i);
                OperationResponse<ResolvedGeometryReference> resolved = LiveGeometryAnalysisHelpers.ResolveDocumentObject(document, entry.ObjectId);
                if (!resolved.Success || resolved.Data is null)
                {
                    results.Add(new GeometryContourResult
                    {
                        EntryId = entryId,
                        ObjectId = entry.ObjectId,
                        Success = false,
                        Message = resolved.Message
                    });
                    continue;
                }

                entry.EntryId = entryId;
                OperationResponse<GeometryContourResult> contour = _calculator.GetContourCurves(
                    entry,
                    entry.ObjectId,
                    resolved.Data.GeometryTypeName,
                    resolved.Data.Geometry);
                GeometryContourResult payload = contour.Success && contour.Data is not null
                    ? contour.Data
                    : new GeometryContourResult
                    {
                        EntryId = entryId,
                        ObjectId = entry.ObjectId,
                        GeometryTypeName = resolved.Data.GeometryTypeName,
                        Success = false,
                        Message = contour.Message
                    };

                producedCurveCount += payload.Curves.Count;
                if (producedCurveCount > LiveGeometryAnalysisHelpers.HardGeneratedCurveThreshold)
                {
                    return OperationResponse<GetContourCurvesInLiveResponse>.Fail("Contour result count exceeds 10000 curves. Filter by layer or split the request into smaller batches.");
                }

                results.Add(payload);
            }

            var response = new GetContourCurvesInLiveResponse
            {
                FilePath = request.FilePath,
                RequestedCount = request.Entries.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            };

            return OperationResponse<GetContourCurvesInLiveResponse>.Ok(response, "Live contour analysis completed.");
        });
    }
}
