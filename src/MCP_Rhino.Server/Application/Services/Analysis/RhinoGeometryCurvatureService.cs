extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services.Analysis;

public sealed class RhinoGeometryCurvatureService
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveGeometryCurvatureCalculator _calculator;

    public RhinoGeometryCurvatureService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveGeometryCurvatureCalculator calculator)
    {
        _documentAccessor = documentAccessor;
        _calculator = calculator;
    }

    public OperationResponse<GetCurvatureSamplesInLiveResponse> GetCurvatureSamplesInLive(GetCurvatureSamplesInLiveRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<GetCurvatureSamplesInLiveResponse>.Fail("At least one curvature entry is required.");
        }

        List<Guid> distinctObjectIds = request.Entries
            .Select(entry => entry.ObjectId)
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();
        if (distinctObjectIds.Count > LiveGeometryAnalysisHelpers.MaxObjectIdsPerRequest)
        {
            return OperationResponse<GetCurvatureSamplesInLiveResponse>.Fail("ObjectId count exceeds 5000. Filter by layer or split the request into smaller batches.");
        }

        var warnings = new List<ObjectEditWarning>();
        foreach (CurvatureSampleEntryRequest entry in request.Entries)
        {
            int sampleBudget = entry.Mode switch
            {
                Domain.Enums.GeometryCurvatureSamplingMode.EvenByCount => entry.SampleCount,
                Domain.Enums.GeometryCurvatureSamplingMode.AtParameters => entry.Parameters.Count,
                Domain.Enums.GeometryCurvatureSamplingMode.AtUVList => entry.UvSamples.Count,
                _ => 0
            };

            if (sampleBudget > LiveGeometryAnalysisHelpers.HardSampleThreshold)
            {
                return OperationResponse<GetCurvatureSamplesInLiveResponse>.Fail("Curvature sample count exceeds 10000. Split the request into smaller batches.");
            }

            if (sampleBudget > LiveGeometryAnalysisHelpers.WarningSampleThreshold)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "HIGH_SAMPLE_COUNT",
                    Message = $"Curvature entry [{entry.EntryId}] requests more than 1000 samples."
                });
            }
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var results = new List<GeometryCurvatureSample>();
            int succeededEntries = 0;
            int failedEntries = 0;

            for (int i = 0; i < request.Entries.Count; i++)
            {
                CurvatureSampleEntryRequest entry = request.Entries[i];
                string entryId = LiveGeometryAnalysisHelpers.NormalizeEntryId(entry.EntryId, "curvature", i);
                OperationResponse<ResolvedGeometryReference> resolved = LiveGeometryAnalysisHelpers.ResolveDocumentObject(document, entry.ObjectId);
                if (!resolved.Success || resolved.Data is null)
                {
                    results.Add(new GeometryCurvatureSample
                    {
                        EntryId = entryId,
                        ObjectId = entry.ObjectId,
                        Success = false,
                        Message = resolved.Message
                    });
                    failedEntries++;
                    continue;
                }

                entry.EntryId = entryId;
                OperationResponse<IReadOnlyList<GeometryCurvatureSample>> sampleResult = _calculator.GetCurvatureSamples(
                    entry,
                    entry.ObjectId,
                    resolved.Data.GeometryTypeName,
                    resolved.Data.Geometry);
                if (!sampleResult.Success || sampleResult.Data is null)
                {
                    results.Add(new GeometryCurvatureSample
                    {
                        EntryId = entryId,
                        ObjectId = entry.ObjectId,
                        GeometryTypeName = resolved.Data.GeometryTypeName,
                        Success = false,
                        Message = sampleResult.Message
                    });
                    failedEntries++;
                    continue;
                }

                results.AddRange(sampleResult.Data);
                succeededEntries++;
            }

            var response = new GetCurvatureSamplesInLiveResponse
            {
                FilePath = request.FilePath,
                RequestedCount = request.Entries.Count,
                SucceededCount = succeededEntries,
                FailedCount = failedEntries,
                Warnings = warnings,
                Results = results
            };

            return OperationResponse<GetCurvatureSamplesInLiveResponse>.Ok(response, "Live curvature sampling completed.");
        });
    }

    public OperationResponse<CheckContinuityInLiveResponse> CheckContinuityInLive(CheckContinuityInLiveRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<CheckContinuityInLiveResponse>.Fail("At least one continuity entry is required.");
        }

        List<Guid> distinctObjectIds = request.Entries
            .SelectMany(entry => new[] { entry.FirstObjectId, entry.SecondObjectId })
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();
        if (distinctObjectIds.Count > LiveGeometryAnalysisHelpers.MaxObjectIdsPerRequest)
        {
            return OperationResponse<CheckContinuityInLiveResponse>.Fail("ObjectId count exceeds 5000. Filter by layer or split the request into smaller batches.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var results = new List<GeometryContinuityResult>(request.Entries.Count);
            for (int i = 0; i < request.Entries.Count; i++)
            {
                ContinuityCheckEntryRequest entry = request.Entries[i];
                string entryId = LiveGeometryAnalysisHelpers.NormalizeEntryId(entry.EntryId, "continuity", i);
                OperationResponse<ResolvedGeometryReference> first = LiveGeometryAnalysisHelpers.ResolveDocumentObject(document, entry.FirstObjectId);
                OperationResponse<ResolvedGeometryReference> second = LiveGeometryAnalysisHelpers.ResolveDocumentObject(document, entry.SecondObjectId);
                if (!first.Success || first.Data is null)
                {
                    results.Add(new GeometryContinuityResult
                    {
                        EntryId = entryId,
                        FirstObjectId = entry.FirstObjectId,
                        SecondObjectId = entry.SecondObjectId,
                        TargetKind = entry.TargetKind,
                        Success = false,
                        Message = first.Message
                    });
                    continue;
                }

                if (!second.Success || second.Data is null)
                {
                    results.Add(new GeometryContinuityResult
                    {
                        EntryId = entryId,
                        FirstObjectId = entry.FirstObjectId,
                        SecondObjectId = entry.SecondObjectId,
                        TargetKind = entry.TargetKind,
                        Success = false,
                        Message = second.Message
                    });
                    continue;
                }

                entry.EntryId = entryId;
                OperationResponse<GeometryContinuityResult> continuity = _calculator.CheckContinuity(
                    entry,
                    first.Data.GeometryTypeName,
                    first.Data.Geometry,
                    second.Data.GeometryTypeName,
                    second.Data.Geometry);
                results.Add(continuity.Success && continuity.Data is not null
                    ? continuity.Data
                    : new GeometryContinuityResult
                    {
                        EntryId = entryId,
                        FirstObjectId = entry.FirstObjectId,
                        SecondObjectId = entry.SecondObjectId,
                        TargetKind = entry.TargetKind,
                        Success = false,
                        Message = continuity.Message
                    });
            }

            var response = new CheckContinuityInLiveResponse
            {
                FilePath = request.FilePath,
                RequestedCount = request.Entries.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            };

            return OperationResponse<CheckContinuityInLiveResponse>.Ok(response, "Live continuity check completed.");
        });
    }
}
