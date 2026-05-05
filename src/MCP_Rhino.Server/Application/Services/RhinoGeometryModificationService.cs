using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoGeometryModificationService
{
    private const int PreviewLimit = 20;

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveGeometryValidator _liveGeometryValidator;
    private readonly IGeometryMutator _mutator;
    private readonly IEditResultFormatter _formatter;

    public RhinoGeometryModificationService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveGeometryValidator liveGeometryValidator,
        IGeometryMutator mutator,
        IEditResultFormatter formatter)
    {
        _documentAccessor = documentAccessor;
        _liveGeometryValidator = liveGeometryValidator;
        _mutator = mutator;
        _formatter = formatter;
    }

    public OperationResponse<GeometryModificationPreviewResponse> Preview(
        PreviewTransformObjectsRequest request,
        RhinoObjectFilterResult selection)
    {
        return _documentAccessor.Execute(
            request.FilePath,
            _ => PreviewTransformInternal(
                request.FilePath,
                request.Transform,
                selection,
                request.ConfirmedObjectIds.Count > 0 && HasAnySelectionCriteria(
                    request.LayerQueries,
                    request.ConfirmedLayerFullPaths,
                    request.ObjectTypes,
                    request.UserAttributeConditions)));
    }

    public OperationResponse<GeometryModificationResponse> Apply(
        TransformObjectsRequest request,
        RhinoObjectFilterResult selection)
    {
        return Apply(request, selection, "MCP: TransformObjects");
    }

    public OperationResponse<GeometryModificationResponse> Apply(
        TransformObjectsRequest request,
        RhinoObjectFilterResult selection,
        string undoDescription)
    {
        return ApplyTransformInternal(
            request.FilePath,
            request.Transform,
            selection,
            undoDescription,
            request.ConfirmedObjectIds.Count > 0 && HasAnySelectionCriteria(
                request.LayerQueries,
                request.ConfirmedLayerFullPaths,
                request.ObjectTypes,
                request.UserAttributeConditions));
    }

    public OperationResponse<GeometryModificationPreviewResponse> Preview(
        PreviewDeleteObjectsRequest request,
        RhinoObjectFilterResult selection)
    {
        return _documentAccessor.Execute(
            request.FilePath,
            _ => PreviewDeleteInternal(
                request.FilePath,
                selection,
                request.ConfirmedObjectIds.Count > 0 && HasAnySelectionCriteria(
                    request.LayerQueries,
                    request.ConfirmedLayerFullPaths,
                    request.ObjectTypes,
                    request.UserAttributeConditions)));
    }

    public OperationResponse<GeometryModificationResponse> Apply(
        DeleteObjectsRequest request,
        RhinoObjectFilterResult selection)
    {
        return ApplyDeleteInternal(
            request.FilePath,
            selection,
            request.ConfirmedObjectIds.Count > 0 && HasAnySelectionCriteria(
                request.LayerQueries,
                request.ConfirmedLayerFullPaths,
                request.ObjectTypes,
                request.UserAttributeConditions));
    }

    public OperationResponse<GeometryModificationPreviewResponse> PreviewReplace(
        string filePath,
        IReadOnlyList<GeometryReplacementSpec> specs,
        IReadOnlyList<RhinoObjectInfo> targets)
    {
        if (specs.Count == 0)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail("At least one ReplaceGeometry entry is required.");
        }

        if (HasDuplicateObjectIds(specs.Select(spec => spec.ObjectId)))
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail("ReplaceGeometry does not allow duplicate ObjectId entries.");
        }

        return _documentAccessor.Execute(filePath, _ =>
        {
            Dictionary<Guid, RhinoObjectInfo> targetLookup = targets.ToDictionary(target => target.ObjectId);
            var warnings = CreateBatchWarnings(specs.Count);

            foreach (GeometryReplacementSpec spec in specs)
            {
                if (!targetLookup.TryGetValue(spec.ObjectId, out RhinoObjectInfo? target))
                {
                    return OperationResponse<GeometryModificationPreviewResponse>.Fail($"Object not found: {spec.ObjectId}");
                }

                OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _liveGeometryValidator.Validate(spec, target);
                if (!validation.Success)
                {
                    return OperationResponse<GeometryModificationPreviewResponse>.Fail(validation.Message);
                }

                warnings.AddRange(validation.Data ?? Array.Empty<ObjectEditWarning>());
            }

            List<ObjectEditOperationResult> results = specs
                .Take(PreviewLimit)
                .Select(spec =>
                {
                    RhinoObjectInfo target = targetLookup[spec.ObjectId];
                    return new ObjectEditOperationResult
                    {
                        ObjectId = target.ObjectId,
                        LayerFullPath = target.LayerFullPath,
                        Success = true,
                        Messages = new[] { $"ReplaceGeometry: {target.GeometryTypeName} -> {spec.Geometry.Primitive}" }
                    };
                })
                .ToList();

            AddPreviewTruncationWarning(results.Count, specs.Count, warnings);

            return OperationResponse<GeometryModificationPreviewResponse>.Ok(new GeometryModificationPreviewResponse
            {
                FilePath = filePath,
                CriteriaSummary = SummarizeEntryTargets("ReplaceGeometry", specs.Select(spec => spec.ObjectId)),
                MatchedObjectCount = specs.Count,
                PreviewObjectCount = results.Count,
                OperationCount = specs.Count,
                Warnings = warnings,
                ObjectResults = results
            }, "ReplaceGeometry preview generated.");
        });
    }

    public OperationResponse<GeometryModificationResponse> ApplyReplace(
        string filePath,
        IReadOnlyList<GeometryReplacementSpec> specs,
        IReadOnlyList<RhinoObjectInfo> targets)
    {
        if (specs.Count == 0)
        {
            return OperationResponse<GeometryModificationResponse>.Fail("At least one ReplaceGeometry entry is required.");
        }

        if (HasDuplicateObjectIds(specs.Select(spec => spec.ObjectId)))
        {
            return OperationResponse<GeometryModificationResponse>.Fail("ReplaceGeometry does not allow duplicate ObjectId entries.");
        }

        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: ReplaceGeometry", document =>
        {
            Dictionary<Guid, RhinoObjectInfo> targetLookup = targets.ToDictionary(target => target.ObjectId);
            var warnings = CreateBatchWarnings(specs.Count);

            foreach (GeometryReplacementSpec spec in specs)
            {
                if (!targetLookup.TryGetValue(spec.ObjectId, out RhinoObjectInfo? target))
                {
                    return OperationResponse<(bool Mutated, GeometryModificationResponse Result)>.Fail($"Object not found: {spec.ObjectId}");
                }

                OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _liveGeometryValidator.Validate(spec, target);
                if (!validation.Success)
                {
                    return OperationResponse<(bool Mutated, GeometryModificationResponse Result)>.Fail(validation.Message);
                }

                warnings.AddRange(validation.Data ?? Array.Empty<ObjectEditWarning>());
            }

            var results = new List<ObjectEditOperationResult>(specs.Count);
            foreach (GeometryReplacementSpec spec in specs)
            {
                RhinoObjectInfo target = targetLookup[spec.ObjectId];
                OperationResponse<ObjectEditOperationResult> mutateResult = _mutator.Replace(target, spec);
                results.Add(ToOperationResult(target, mutateResult));
            }

            if (results.Any(result => result.Success))
            {
                document.Views.Redraw();
            }

            AddResultTruncationWarning(results.Count, warnings);

            return OperationResponse<(bool Mutated, GeometryModificationResponse Result)>.Ok(
                (results.Any(result => result.Success), new GeometryModificationResponse
                {
                    FilePath = filePath,
                    CriteriaSummary = SummarizeEntryTargets("ReplaceGeometry", specs.Select(spec => spec.ObjectId)),
                    MatchedObjectCount = specs.Count,
                    UpdatedObjectCount = results.Count(result => result.Success),
                    FailedObjectCount = results.Count(result => !result.Success),
                    OperationCount = specs.Count,
                    Warnings = warnings,
                    ObjectResults = results.Take(PreviewLimit).ToList()
                }),
                "ReplaceGeometry completed.");
        });
    }

    public OperationResponse<GeometryModificationPreviewResponse> PreviewEditControlPoints(
        string filePath,
        IReadOnlyList<ControlPointEditSpec> specs,
        IReadOnlyList<RhinoObjectInfo> targets)
    {
        if (specs.Count == 0)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail("At least one EditControlPoints entry is required.");
        }

        return _documentAccessor.Execute(filePath, document =>
        {
            Dictionary<Guid, RhinoObjectInfo> targetLookup = targets.ToDictionary(target => target.ObjectId);
            var warnings = CreateBatchWarnings(specs.Count);

            foreach (ControlPointEditSpec spec in specs)
            {
                if (!targetLookup.TryGetValue(spec.ObjectId, out RhinoObjectInfo? target))
                {
                    return OperationResponse<GeometryModificationPreviewResponse>.Fail($"Object not found: {spec.ObjectId}");
                }

                OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _liveGeometryValidator.Validate(spec, target);
                if (!validation.Success)
                {
                    return OperationResponse<GeometryModificationPreviewResponse>.Fail(validation.Message);
                }

                warnings.AddRange(validation.Data ?? Array.Empty<ObjectEditWarning>());

                OperationResponse controlPointValidation = _liveGeometryValidator.ValidateAgainstDocument(document, spec);
                if (!controlPointValidation.Success)
                {
                    return OperationResponse<GeometryModificationPreviewResponse>.Fail(controlPointValidation.Message);
                }
            }

            Dictionary<Guid, List<ControlPointEditSpec>> groupedSpecs = specs
                .GroupBy(spec => spec.ObjectId)
                .ToDictionary(group => group.Key, group => group.ToList());

            List<ObjectEditOperationResult> results = groupedSpecs
                .Take(PreviewLimit)
                .Select(group =>
                {
                    RhinoObjectInfo target = targetLookup[group.Key];
                    return new ObjectEditOperationResult
                    {
                        ObjectId = group.Key,
                        LayerFullPath = target.LayerFullPath,
                        Success = true,
                        Messages = group.Value.Select(DescribeControlPointEdit).ToList()
                    };
                })
                .ToList();

            AddPreviewTruncationWarning(results.Count, groupedSpecs.Count, warnings);

            return OperationResponse<GeometryModificationPreviewResponse>.Ok(new GeometryModificationPreviewResponse
            {
                FilePath = filePath,
                CriteriaSummary = SummarizeEntryTargets("EditControlPoints", specs.Select(spec => spec.ObjectId)),
                MatchedObjectCount = groupedSpecs.Count,
                PreviewObjectCount = results.Count,
                OperationCount = specs.Count,
                Warnings = warnings,
                ObjectResults = results
            }, "EditControlPoints preview generated.");
        });
    }

    public OperationResponse<GeometryModificationResponse> ApplyEditControlPoints(
        string filePath,
        IReadOnlyList<ControlPointEditSpec> specs,
        IReadOnlyList<RhinoObjectInfo> targets)
    {
        if (specs.Count == 0)
        {
            return OperationResponse<GeometryModificationResponse>.Fail("At least one EditControlPoints entry is required.");
        }

        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: EditControlPoints", document =>
        {
            Dictionary<Guid, RhinoObjectInfo> targetLookup = targets.ToDictionary(target => target.ObjectId);
            var warnings = CreateBatchWarnings(specs.Count);

            foreach (ControlPointEditSpec spec in specs)
            {
                if (!targetLookup.TryGetValue(spec.ObjectId, out RhinoObjectInfo? target))
                {
                    return OperationResponse<(bool Mutated, GeometryModificationResponse Result)>.Fail($"Object not found: {spec.ObjectId}");
                }

                OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _liveGeometryValidator.Validate(spec, target);
                if (!validation.Success)
                {
                    return OperationResponse<(bool Mutated, GeometryModificationResponse Result)>.Fail(validation.Message);
                }

                warnings.AddRange(validation.Data ?? Array.Empty<ObjectEditWarning>());

                OperationResponse controlPointValidation = _liveGeometryValidator.ValidateAgainstDocument(document, spec);
                if (!controlPointValidation.Success)
                {
                    return OperationResponse<(bool Mutated, GeometryModificationResponse Result)>.Fail(controlPointValidation.Message);
                }
            }

            Dictionary<Guid, List<ControlPointEditSpec>> groupedSpecs = specs
                .GroupBy(spec => spec.ObjectId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var results = new List<ObjectEditOperationResult>(groupedSpecs.Count);
            foreach ((Guid objectId, List<ControlPointEditSpec> objectSpecs) in groupedSpecs)
            {
                RhinoObjectInfo target = targetLookup[objectId];
                OperationResponse<ObjectEditOperationResult> mutateResult = _mutator.EditControlPoints(target, objectSpecs);
                results.Add(ToOperationResult(target, mutateResult));
            }

            if (results.Any(result => result.Success))
            {
                document.Views.Redraw();
            }

            AddResultTruncationWarning(results.Count, warnings);

            return OperationResponse<(bool Mutated, GeometryModificationResponse Result)>.Ok(
                (results.Any(result => result.Success), new GeometryModificationResponse
                {
                    FilePath = filePath,
                    CriteriaSummary = SummarizeEntryTargets("EditControlPoints", specs.Select(spec => spec.ObjectId)),
                    MatchedObjectCount = groupedSpecs.Count,
                    UpdatedObjectCount = results.Count(result => result.Success),
                    FailedObjectCount = results.Count(result => !result.Success),
                    OperationCount = specs.Count,
                    Warnings = warnings,
                    ObjectResults = results.Take(PreviewLimit).ToList()
                }),
                "EditControlPoints completed.");
        });
    }

    public string Format(GeometryModificationResponse response)
    {
        return _formatter.FormatGeometryModification(response);
    }

    public string FormatPreview(GeometryModificationPreviewResponse response)
    {
        return _formatter.FormatGeometryModificationPreview(response);
    }

    private OperationResponse<GeometryModificationPreviewResponse> PreviewTransformInternal(
        string filePath,
        GeometryTransformSpec transform,
        RhinoObjectFilterResult selection,
        bool ignoredFilters)
    {
        OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _liveGeometryValidator.Validate(transform, selection.Objects);
        if (!validation.Success)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail(validation.Message);
        }

        var warnings = CreateWarnings(validation.Data ?? Array.Empty<ObjectEditWarning>(), ignoredFilters, selection.Objects.Count);
        if (selection.Objects.Count == 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "NO_MATCHED_OBJECTS",
                Message = "No objects matched the selection."
            });
        }

        List<ObjectEditOperationResult> results = selection.Objects
            .Take(PreviewLimit)
            .Select(objectInfo => new ObjectEditOperationResult
            {
                ObjectId = objectInfo.ObjectId,
                LayerFullPath = objectInfo.LayerFullPath,
                Success = true,
                Messages = new[] { DescribeTransform(transform) }
            })
            .ToList();

        AddPreviewTruncationWarning(results.Count, selection.Objects.Count, warnings);

        return OperationResponse<GeometryModificationPreviewResponse>.Ok(new GeometryModificationPreviewResponse
        {
            FilePath = filePath,
            CriteriaSummary = selection.CriteriaSummary,
            MatchedObjectCount = selection.MatchedCount,
            PreviewObjectCount = results.Count,
            OperationCount = 1,
            Warnings = warnings,
            ObjectResults = results
        }, "TransformObjects preview generated.");
    }

    private OperationResponse<GeometryModificationResponse> ApplyTransformInternal(
        string filePath,
        GeometryTransformSpec transform,
        RhinoObjectFilterResult selection,
        string undoDescription,
        bool ignoredFilters)
    {
        if (selection.Objects.Count == 0)
        {
            return OperationResponse<GeometryModificationResponse>.Fail("No objects matched the selection.");
        }

        return _documentAccessor.ExecuteWithUndo(filePath, undoDescription, document =>
        {
            OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _liveGeometryValidator.Validate(transform, selection.Objects);
            if (!validation.Success)
            {
                return OperationResponse<(bool Mutated, GeometryModificationResponse Result)>.Fail(validation.Message);
            }

            var warnings = CreateWarnings(validation.Data ?? Array.Empty<ObjectEditWarning>(), ignoredFilters, selection.Objects.Count);
            var results = new List<ObjectEditOperationResult>();

            foreach (RhinoObjectInfo objectInfo in selection.Objects)
            {
                OperationResponse<ObjectEditOperationResult> mutateResult = _mutator.Transform(objectInfo, transform);
                results.Add(ToOperationResult(objectInfo, mutateResult));
            }

            if (results.Any(result => result.Success))
            {
                document.Views.Redraw();
            }

            AddResultTruncationWarning(results.Count, warnings);

            return OperationResponse<(bool Mutated, GeometryModificationResponse Result)>.Ok(
                (results.Any(result => result.Success), new GeometryModificationResponse
                {
                    FilePath = filePath,
                    CriteriaSummary = selection.CriteriaSummary,
                    MatchedObjectCount = selection.MatchedCount,
                    UpdatedObjectCount = results.Count(result => result.Success),
                    FailedObjectCount = results.Count(result => !result.Success),
                    OperationCount = 1,
                    Warnings = warnings,
                    ObjectResults = results.Take(PreviewLimit).ToList()
                }),
                "TransformObjects completed.");
        });
    }

    private OperationResponse<GeometryModificationPreviewResponse> PreviewDeleteInternal(
        string filePath,
        RhinoObjectFilterResult selection,
        bool ignoredFilters)
    {
        var warnings = CreateWarnings(Array.Empty<ObjectEditWarning>(), ignoredFilters, selection.Objects.Count);
        if (selection.Objects.Count == 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "NO_MATCHED_OBJECTS",
                Message = "No objects matched the selection."
            });
        }

        List<ObjectEditOperationResult> results = selection.Objects
            .Take(PreviewLimit)
            .Select(objectInfo => new ObjectEditOperationResult
            {
                ObjectId = objectInfo.ObjectId,
                LayerFullPath = objectInfo.LayerFullPath,
                Success = true,
                Messages = new[] { "DeleteObject" }
            })
            .ToList();

        AddPreviewTruncationWarning(results.Count, selection.Objects.Count, warnings);

        return OperationResponse<GeometryModificationPreviewResponse>.Ok(new GeometryModificationPreviewResponse
        {
            FilePath = filePath,
            CriteriaSummary = selection.CriteriaSummary,
            MatchedObjectCount = selection.MatchedCount,
            PreviewObjectCount = results.Count,
            OperationCount = 1,
            Warnings = warnings,
            ObjectResults = results
        }, "DeleteObjects preview generated.");
    }

    private OperationResponse<GeometryModificationResponse> ApplyDeleteInternal(
        string filePath,
        RhinoObjectFilterResult selection,
        bool ignoredFilters)
    {
        if (selection.Objects.Count == 0)
        {
            return OperationResponse<GeometryModificationResponse>.Fail("No objects matched the selection.");
        }

        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: DeleteObjects", document =>
        {
            var warnings = CreateWarnings(Array.Empty<ObjectEditWarning>(), ignoredFilters, selection.Objects.Count);
            var results = new List<ObjectEditOperationResult>();

            foreach (RhinoObjectInfo objectInfo in selection.Objects)
            {
                OperationResponse<ObjectEditOperationResult> mutateResult = _mutator.Delete(objectInfo);
                results.Add(ToOperationResult(objectInfo, mutateResult));
            }

            if (results.Any(result => result.Success))
            {
                document.Views.Redraw();
            }

            AddResultTruncationWarning(results.Count, warnings);

            return OperationResponse<(bool Mutated, GeometryModificationResponse Result)>.Ok(
                (results.Any(result => result.Success), new GeometryModificationResponse
                {
                    FilePath = filePath,
                    CriteriaSummary = selection.CriteriaSummary,
                    MatchedObjectCount = selection.MatchedCount,
                    UpdatedObjectCount = results.Count(result => result.Success),
                    FailedObjectCount = results.Count(result => !result.Success),
                    OperationCount = 1,
                    Warnings = warnings,
                    ObjectResults = results.Take(PreviewLimit).ToList()
                }),
                "DeleteObjects completed.");
        });
    }

    private static bool HasAnySelectionCriteria(
        IReadOnlyList<string> layerQueries,
        IReadOnlyList<string> confirmedLayerFullPaths,
        IReadOnlyList<string> objectTypes,
        IReadOnlyList<UserAttributeConditionRequest> userAttributeConditions)
    {
        return layerQueries.Count > 0
            || confirmedLayerFullPaths.Count > 0
            || objectTypes.Count > 0
            || userAttributeConditions.Count > 0;
    }

    private static bool HasDuplicateObjectIds(IEnumerable<Guid> objectIds)
    {
        return objectIds.GroupBy(objectId => objectId).Any(group => group.Count() > 1);
    }

    private static List<ObjectEditWarning> CreateWarnings(
        IEnumerable<ObjectEditWarning> validatorWarnings,
        bool ignoredFilters,
        int batchCount)
    {
        var warnings = validatorWarnings.ToList();
        warnings.AddRange(CreateBatchWarnings(batchCount));

        if (ignoredFilters)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "FILTERS_IGNORED",
                Message = "ConfirmedObjectIds were provided, so filter criteria were ignored."
            });
        }

        return warnings;
    }

    private static List<ObjectEditWarning> CreateBatchWarnings(int batchCount)
    {
        var warnings = new List<ObjectEditWarning>();
        if (batchCount > 10000)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "LARGE_BATCH",
                Message = "Batch size exceeds 10000 items."
            });
        }

        return warnings;
    }

    private static void AddPreviewTruncationWarning(int previewCount, int totalCount, ICollection<ObjectEditWarning> warnings)
    {
        if (totalCount > PreviewLimit && previewCount <= PreviewLimit)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "PREVIEW_TRUNCATED",
                Message = $"Preview only shows the first {PreviewLimit} objects."
            });
        }
    }

    private static void AddResultTruncationWarning(int resultCount, ICollection<ObjectEditWarning> warnings)
    {
        if (resultCount > PreviewLimit)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "RESULT_TRUNCATED",
                Message = $"Result list only shows the first {PreviewLimit} objects."
            });
        }
    }

    private static ObjectEditOperationResult ToOperationResult(
        RhinoObjectInfo target,
        OperationResponse<ObjectEditOperationResult> mutateResult)
    {
        if (mutateResult.Success && mutateResult.Data is not null)
        {
            return mutateResult.Data;
        }

        return new ObjectEditOperationResult
        {
            ObjectId = target.ObjectId,
            LayerFullPath = target.LayerFullPath,
            Success = false,
            Messages = new[] { mutateResult.Message }
        };
    }

    private static string DescribeTransform(GeometryTransformSpec spec)
    {
        return spec.Kind switch
        {
            Domain.Enums.GeometryTransformKind.Translate =>
                $"Transform: Translate ({spec.VectorX}, {spec.VectorY}, {spec.VectorZ})",
            Domain.Enums.GeometryTransformKind.Rotate =>
                $"Transform: Rotate Axis=({spec.AxisX}, {spec.AxisY}, {spec.AxisZ}) Angle={spec.AngleRadians}",
            Domain.Enums.GeometryTransformKind.UniformScale =>
                $"Transform: UniformScale Center=({spec.CenterX}, {spec.CenterY}, {spec.CenterZ}) Scale={spec.ScaleFactor}",
            _ => $"Transform: {spec.Kind}"
        };
    }

    private static string DescribeControlPointEdit(ControlPointEditSpec spec)
    {
        return spec.TargetMode switch
        {
            Domain.Enums.ControlPointTargetMode.CurveIndex => $"EditControlPoint: CurveIndex={spec.PointIndex}",
            Domain.Enums.ControlPointTargetMode.SurfaceUV => $"EditControlPoint: SurfaceUV=({spec.UIndex},{spec.VIndex})",
            _ => "EditControlPoint"
        };
    }

    private static string SummarizeEntryTargets(string operationName, IEnumerable<Guid> objectIds)
    {
        List<Guid> distinctIds = objectIds.Distinct().ToList();
        return $"{operationName}; Targets={distinctIds.Count}";
    }
}
