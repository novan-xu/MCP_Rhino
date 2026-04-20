using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoGeometryModificationService
{
    private const int PreviewLimit = 20;

    private readonly IRhinoDocumentRepository _repository;
    private readonly IGeometryValidator _validator;
    private readonly IGeometryMutator _mutator;
    private readonly IFileMutationSafeguard _fileMutationSafeguard;
    private readonly IEditResultFormatter _formatter;

    public RhinoGeometryModificationService(
        IRhinoDocumentRepository repository,
        IGeometryValidator validator,
        IGeometryMutator mutator,
        IFileMutationSafeguard fileMutationSafeguard,
        IEditResultFormatter formatter)
    {
        _repository = repository;
        _validator = validator;
        _mutator = mutator;
        _fileMutationSafeguard = fileMutationSafeguard;
        _formatter = formatter;
    }

    public OperationResponse<GeometryModificationPreviewResponse> Preview(
        PreviewTransformObjectsRequest request,
        RhinoObjectFilterResult selection)
    {
        return PreviewTransformInternal(
            request.FilePath,
            request.Transform,
            selection,
            request.ConfirmedObjectIds.Count > 0 && HasAnySelectionCriteria(
                request.LayerQueries,
                request.ConfirmedLayerFullPaths,
                request.ObjectTypes,
                request.UserAttributeConditions));
    }

    public OperationResponse<GeometryModificationResponse> Apply(
        TransformObjectsRequest request,
        RhinoObjectFilterResult selection)
    {
        return ApplyTransformInternal(
            request.FilePath,
            request.Transform,
            selection,
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
        return PreviewDeleteInternal(
            request.FilePath,
            selection,
            request.ConfirmedObjectIds.Count > 0 && HasAnySelectionCriteria(
                request.LayerQueries,
                request.ConfirmedLayerFullPaths,
                request.ObjectTypes,
                request.UserAttributeConditions));
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
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        if (specs.Count == 0)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail("错误：至少需要提供一个 ReplaceGeometry entry。");
        }

        if (HasDuplicateObjectIds(specs.Select(spec => spec.ObjectId)))
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail("错误：ReplaceGeometry 不允许对同一个 ObjectId 提供多个 entry。");
        }

        Dictionary<Guid, RhinoObjectInfo> targetLookup = targets.ToDictionary(target => target.ObjectId);
        var warnings = CreateBatchWarnings(specs.Count);

        foreach (GeometryReplacementSpec spec in specs)
        {
            if (!targetLookup.TryGetValue(spec.ObjectId, out RhinoObjectInfo? target))
            {
                return OperationResponse<GeometryModificationPreviewResponse>.Fail($"错误：对象不存在 [{spec.ObjectId}]。");
            }

            OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _validator.Validate(spec, target);
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
        }, "ReplaceGeometry 预览生成完成。");
    }

    public OperationResponse<GeometryModificationResponse> ApplyReplace(
        string filePath,
        IReadOnlyList<GeometryReplacementSpec> specs,
        IReadOnlyList<RhinoObjectInfo> targets)
    {
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<GeometryModificationResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        if (specs.Count == 0)
        {
            return OperationResponse<GeometryModificationResponse>.Fail("错误：至少需要提供一个 ReplaceGeometry entry。");
        }

        if (HasDuplicateObjectIds(specs.Select(spec => spec.ObjectId)))
        {
            return OperationResponse<GeometryModificationResponse>.Fail("错误：ReplaceGeometry 不允许对同一个 ObjectId 提供多个 entry。");
        }

        Dictionary<Guid, RhinoObjectInfo> targetLookup = targets.ToDictionary(target => target.ObjectId);

        try
        {
            using var model = _repository.Read(filePath);
            var warnings = CreateBatchWarnings(specs.Count);

            foreach (GeometryReplacementSpec spec in specs)
            {
                if (!targetLookup.TryGetValue(spec.ObjectId, out RhinoObjectInfo? target))
                {
                    return OperationResponse<GeometryModificationResponse>.Fail($"错误：对象不存在 [{spec.ObjectId}]。");
                }

                OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _validator.Validate(spec, target);
                if (!validation.Success)
                {
                    return OperationResponse<GeometryModificationResponse>.Fail(validation.Message);
                }

                warnings.AddRange(validation.Data ?? Array.Empty<ObjectEditWarning>());
            }

            OperationResponse<FileMutationPreflightResponse> safeguard = _fileMutationSafeguard.BeforeOverwrite(filePath, specs.Count);
            if (!safeguard.Success)
            {
                return OperationResponse<GeometryModificationResponse>.Fail(safeguard.Message);
            }

            if (safeguard.Data is not null)
            {
                warnings.AddRange(safeguard.Data.Warnings.Select(message => new ObjectEditWarning
                {
                    Code = "FILE_MUTATION_PREFLIGHT",
                    Message = message
                }));
            }

            var results = new List<ObjectEditOperationResult>(specs.Count);
            bool writeSucceeded = false;

            try
            {
                foreach (GeometryReplacementSpec spec in specs)
                {
                    RhinoObjectInfo target = targetLookup[spec.ObjectId];
                    OperationResponse<ObjectEditOperationResult> mutateResult = _mutator.Replace(model, target, spec);
                    results.Add(ToOperationResult(target, mutateResult));
                }

                writeSucceeded = results.All(result => result.Success) && _repository.Write(model, filePath);
                if (!writeSucceeded)
                {
                    return OperationResponse<GeometryModificationResponse>.Fail("ReplaceGeometry 写回失败。请检查文件是否可写。");
                }
            }
            finally
            {
                _fileMutationSafeguard.AfterOverwrite(filePath, writeSucceeded);
            }

            AddResultTruncationWarning(results.Count, warnings);

            return OperationResponse<GeometryModificationResponse>.Ok(new GeometryModificationResponse
            {
                FilePath = filePath,
                CriteriaSummary = SummarizeEntryTargets("ReplaceGeometry", specs.Select(spec => spec.ObjectId)),
                MatchedObjectCount = specs.Count,
                UpdatedObjectCount = results.Count(result => result.Success),
                FailedObjectCount = results.Count(result => !result.Success),
                OperationCount = specs.Count,
                Warnings = warnings,
                ObjectResults = results.Take(PreviewLimit).ToList()
            }, "ReplaceGeometry 执行完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryModificationResponse>.Fail($"ReplaceGeometry 执行失败: {ex.Message}");
        }
    }

    public OperationResponse<GeometryModificationPreviewResponse> PreviewEditControlPoints(
        string filePath,
        IReadOnlyList<ControlPointEditSpec> specs,
        IReadOnlyList<RhinoObjectInfo> targets)
    {
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        if (specs.Count == 0)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail("错误：至少需要提供一个 EditControlPoints entry。");
        }

        Dictionary<Guid, RhinoObjectInfo> targetLookup = targets.ToDictionary(target => target.ObjectId);

        try
        {
            using var model = _repository.Read(filePath);
            var warnings = CreateBatchWarnings(specs.Count);

            foreach (ControlPointEditSpec spec in specs)
            {
                if (!targetLookup.TryGetValue(spec.ObjectId, out RhinoObjectInfo? target))
                {
                    return OperationResponse<GeometryModificationPreviewResponse>.Fail($"错误：对象不存在 [{spec.ObjectId}]。");
                }

                OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _validator.Validate(spec, target);
                if (!validation.Success)
                {
                    return OperationResponse<GeometryModificationPreviewResponse>.Fail(validation.Message);
                }

                warnings.AddRange(validation.Data ?? Array.Empty<ObjectEditWarning>());
                OperationResponse controlPointValidation = ValidateControlPointIndex(model, spec);
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
            }, "EditControlPoints 预览生成完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail($"EditControlPoints 预览失败: {ex.Message}");
        }
    }

    public OperationResponse<GeometryModificationResponse> ApplyEditControlPoints(
        string filePath,
        IReadOnlyList<ControlPointEditSpec> specs,
        IReadOnlyList<RhinoObjectInfo> targets)
    {
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<GeometryModificationResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        if (specs.Count == 0)
        {
            return OperationResponse<GeometryModificationResponse>.Fail("错误：至少需要提供一个 EditControlPoints entry。");
        }

        Dictionary<Guid, RhinoObjectInfo> targetLookup = targets.ToDictionary(target => target.ObjectId);

        try
        {
            using var model = _repository.Read(filePath);
            var warnings = CreateBatchWarnings(specs.Count);

            foreach (ControlPointEditSpec spec in specs)
            {
                if (!targetLookup.TryGetValue(spec.ObjectId, out RhinoObjectInfo? target))
                {
                    return OperationResponse<GeometryModificationResponse>.Fail($"错误：对象不存在 [{spec.ObjectId}]。");
                }

                OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _validator.Validate(spec, target);
                if (!validation.Success)
                {
                    return OperationResponse<GeometryModificationResponse>.Fail(validation.Message);
                }

                warnings.AddRange(validation.Data ?? Array.Empty<ObjectEditWarning>());
                OperationResponse controlPointValidation = ValidateControlPointIndex(model, spec);
                if (!controlPointValidation.Success)
                {
                    return OperationResponse<GeometryModificationResponse>.Fail(controlPointValidation.Message);
                }
            }

            Dictionary<Guid, List<ControlPointEditSpec>> groupedSpecs = specs
                .GroupBy(spec => spec.ObjectId)
                .ToDictionary(group => group.Key, group => group.ToList());

            OperationResponse<FileMutationPreflightResponse> safeguard = _fileMutationSafeguard.BeforeOverwrite(filePath, groupedSpecs.Count);
            if (!safeguard.Success)
            {
                return OperationResponse<GeometryModificationResponse>.Fail(safeguard.Message);
            }

            if (safeguard.Data is not null)
            {
                warnings.AddRange(safeguard.Data.Warnings.Select(message => new ObjectEditWarning
                {
                    Code = "FILE_MUTATION_PREFLIGHT",
                    Message = message
                }));
            }

            var results = new List<ObjectEditOperationResult>(groupedSpecs.Count);
            bool writeSucceeded = false;

            try
            {
                foreach ((Guid objectId, List<ControlPointEditSpec> objectSpecs) in groupedSpecs)
                {
                    RhinoObjectInfo target = targetLookup[objectId];
                    OperationResponse<ObjectEditOperationResult> mutateResult = _mutator.EditControlPoints(model, target, objectSpecs);
                    results.Add(ToOperationResult(target, mutateResult));
                }

                writeSucceeded = results.All(result => result.Success) && _repository.Write(model, filePath);
                if (!writeSucceeded)
                {
                    return OperationResponse<GeometryModificationResponse>.Fail("EditControlPoints 写回失败。请检查文件是否可写。");
                }
            }
            finally
            {
                _fileMutationSafeguard.AfterOverwrite(filePath, writeSucceeded);
            }

            AddResultTruncationWarning(results.Count, warnings);

            return OperationResponse<GeometryModificationResponse>.Ok(new GeometryModificationResponse
            {
                FilePath = filePath,
                CriteriaSummary = SummarizeEntryTargets("EditControlPoints", specs.Select(spec => spec.ObjectId)),
                MatchedObjectCount = groupedSpecs.Count,
                UpdatedObjectCount = results.Count(result => result.Success),
                FailedObjectCount = results.Count(result => !result.Success),
                OperationCount = specs.Count,
                Warnings = warnings,
                ObjectResults = results.Take(PreviewLimit).ToList()
            }, "EditControlPoints 执行完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryModificationResponse>.Fail($"EditControlPoints 执行失败: {ex.Message}");
        }
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
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _validator.Validate(transform, selection.Objects);
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
                Message = "没有匹配对象，预览为空。"
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
        }, "TransformObjects 预览生成完成。");
    }

    private OperationResponse<GeometryModificationResponse> ApplyTransformInternal(
        string filePath,
        GeometryTransformSpec transform,
        RhinoObjectFilterResult selection,
        bool ignoredFilters)
    {
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<GeometryModificationResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        if (selection.Objects.Count == 0)
        {
            return OperationResponse<GeometryModificationResponse>.Fail("错误：没有匹配对象，未执行任何修改。");
        }

        try
        {
            using var model = _repository.Read(filePath);
            OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _validator.Validate(transform, selection.Objects);
            if (!validation.Success)
            {
                return OperationResponse<GeometryModificationResponse>.Fail(validation.Message);
            }

            var warnings = CreateWarnings(validation.Data ?? Array.Empty<ObjectEditWarning>(), ignoredFilters, selection.Objects.Count);
            OperationResponse<FileMutationPreflightResponse> safeguard = _fileMutationSafeguard.BeforeOverwrite(filePath, selection.Objects.Count);
            if (!safeguard.Success)
            {
                return OperationResponse<GeometryModificationResponse>.Fail(safeguard.Message);
            }

            if (safeguard.Data is not null)
            {
                warnings.AddRange(safeguard.Data.Warnings.Select(message => new ObjectEditWarning
                {
                    Code = "FILE_MUTATION_PREFLIGHT",
                    Message = message
                }));
            }

            var results = new List<ObjectEditOperationResult>();
            bool writeSucceeded = false;

            try
            {
                foreach (RhinoObjectInfo objectInfo in selection.Objects)
                {
                    OperationResponse<ObjectEditOperationResult> mutateResult = _mutator.Transform(model, objectInfo, transform);
                    results.Add(ToOperationResult(objectInfo, mutateResult));
                }

                writeSucceeded = results.All(result => result.Success) && _repository.Write(model, filePath);
                if (!writeSucceeded)
                {
                    return OperationResponse<GeometryModificationResponse>.Fail("TransformObjects 写回失败。请检查文件是否可写。");
                }
            }
            finally
            {
                _fileMutationSafeguard.AfterOverwrite(filePath, writeSucceeded);
            }

            AddResultTruncationWarning(results.Count, warnings);

            return OperationResponse<GeometryModificationResponse>.Ok(new GeometryModificationResponse
            {
                FilePath = filePath,
                CriteriaSummary = selection.CriteriaSummary,
                MatchedObjectCount = selection.MatchedCount,
                UpdatedObjectCount = results.Count(result => result.Success),
                FailedObjectCount = results.Count(result => !result.Success),
                OperationCount = 1,
                Warnings = warnings,
                ObjectResults = results.Take(PreviewLimit).ToList()
            }, "TransformObjects 执行完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryModificationResponse>.Fail($"TransformObjects 执行失败: {ex.Message}");
        }
    }

    private OperationResponse<GeometryModificationPreviewResponse> PreviewDeleteInternal(
        string filePath,
        RhinoObjectFilterResult selection,
        bool ignoredFilters)
    {
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        var warnings = CreateWarnings(Array.Empty<ObjectEditWarning>(), ignoredFilters, selection.Objects.Count);
        if (selection.Objects.Count == 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "NO_MATCHED_OBJECTS",
                Message = "没有匹配对象，预览为空。"
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
        }, "DeleteObjects 预览生成完成。");
    }

    private OperationResponse<GeometryModificationResponse> ApplyDeleteInternal(
        string filePath,
        RhinoObjectFilterResult selection,
        bool ignoredFilters)
    {
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<GeometryModificationResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        if (selection.Objects.Count == 0)
        {
            return OperationResponse<GeometryModificationResponse>.Fail("错误：没有匹配对象，未执行任何删除。");
        }

        try
        {
            using var model = _repository.Read(filePath);
            var warnings = CreateWarnings(Array.Empty<ObjectEditWarning>(), ignoredFilters, selection.Objects.Count);
            OperationResponse<FileMutationPreflightResponse> safeguard = _fileMutationSafeguard.BeforeOverwrite(filePath, selection.Objects.Count);
            if (!safeguard.Success)
            {
                return OperationResponse<GeometryModificationResponse>.Fail(safeguard.Message);
            }

            if (safeguard.Data is not null)
            {
                warnings.AddRange(safeguard.Data.Warnings.Select(message => new ObjectEditWarning
                {
                    Code = "FILE_MUTATION_PREFLIGHT",
                    Message = message
                }));
            }

            var results = new List<ObjectEditOperationResult>();
            bool writeSucceeded = false;

            try
            {
                foreach (RhinoObjectInfo objectInfo in selection.Objects)
                {
                    OperationResponse<ObjectEditOperationResult> mutateResult = _mutator.Delete(model, objectInfo);
                    results.Add(ToOperationResult(objectInfo, mutateResult));
                }

                writeSucceeded = results.All(result => result.Success) && _repository.Write(model, filePath);
                if (!writeSucceeded)
                {
                    return OperationResponse<GeometryModificationResponse>.Fail("DeleteObjects 写回失败。请检查文件是否可写。");
                }
            }
            finally
            {
                _fileMutationSafeguard.AfterOverwrite(filePath, writeSucceeded);
            }

            AddResultTruncationWarning(results.Count, warnings);

            return OperationResponse<GeometryModificationResponse>.Ok(new GeometryModificationResponse
            {
                FilePath = filePath,
                CriteriaSummary = selection.CriteriaSummary,
                MatchedObjectCount = selection.MatchedCount,
                UpdatedObjectCount = results.Count(result => result.Success),
                FailedObjectCount = results.Count(result => !result.Success),
                OperationCount = 1,
                Warnings = warnings,
                ObjectResults = results.Take(PreviewLimit).ToList()
            }, "DeleteObjects 执行完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryModificationResponse>.Fail($"DeleteObjects 执行失败: {ex.Message}");
        }
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
                Message = "已提供 ConfirmedObjectIds，筛查条件已被忽略。"
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
                Message = "批量条目超过 10000，请确认调用规模。"
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
                Message = $"预览仅展示前 {PreviewLimit} 个对象。"
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
                Message = $"执行结果仅展示前 {PreviewLimit} 个对象。"
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

    private static OperationResponse ValidateControlPointIndex(File3dm model, ControlPointEditSpec spec)
    {
        File3dmObject? modelObject = FindModelObject(model, spec.ObjectId);
        if (modelObject?.Geometry is null)
        {
            return OperationResponse.Fail($"错误：对象不存在或几何为空 [{spec.ObjectId}]。");
        }

        switch (spec.TargetMode)
        {
            case Domain.Enums.ControlPointTargetMode.CurveIndex:
                if (modelObject.Geometry is not Rhino.Geometry.NurbsCurve curve)
                {
                    return OperationResponse.Fail($"错误：对象 [{spec.ObjectId}] 不是 NurbsCurve。");
                }

                if (spec.PointIndex is null || spec.PointIndex < 0 || spec.PointIndex >= curve.Points.Count)
                {
                    return OperationResponse.Fail(
                        $"错误：PointIndex 越界。ObjectId={spec.ObjectId}, PointIndex={spec.PointIndex}");
                }
                break;

            case Domain.Enums.ControlPointTargetMode.SurfaceUV:
                if (modelObject.Geometry is not Rhino.Geometry.NurbsSurface surface)
                {
                    return OperationResponse.Fail($"错误：对象 [{spec.ObjectId}] 不是 NurbsSurface。");
                }

                if (spec.UIndex is null || spec.VIndex is null
                    || spec.UIndex < 0 || spec.VIndex < 0
                    || spec.UIndex >= surface.Points.CountU || spec.VIndex >= surface.Points.CountV)
                {
                    return OperationResponse.Fail(
                        $"错误：UIndex / VIndex 越界。ObjectId={spec.ObjectId}, UIndex={spec.UIndex}, VIndex={spec.VIndex}");
                }
                break;
        }

        return OperationResponse.Ok();
    }
}
