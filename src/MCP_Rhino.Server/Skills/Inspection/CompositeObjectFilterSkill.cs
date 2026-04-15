using System.Text;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Skills.Inspection;

public sealed class CompositeObjectFilterSkill
{
    private readonly RhinoObjectFilterService _filterService;

    public CompositeObjectFilterSkill(RhinoObjectFilterService filterService)
    {
        _filterService = filterService;
    }

    public string Filter(FilterObjectsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return "错误：filePath 不能为空。";
        }

        var resolvedRequest = new FilterObjectsRequest
        {
            FilePath = request.FilePath,
            LayerQueries = new List<string>(request.LayerQueries),
            ConfirmedLayerFullPaths = new List<string>(request.ConfirmedLayerFullPaths),
            ObjectTypes = new List<string>(request.ObjectTypes),
            UserAttributeConditions = new List<UserAttributeConditionRequest>(request.UserAttributeConditions),
            MatchMode = request.MatchMode,
            UserAttributeMatchMode = request.UserAttributeMatchMode
        };

        if (resolvedRequest.LayerQueries.Count > 0)
        {
            var resolvedLayers = new List<string>(resolvedRequest.ConfirmedLayerFullPaths);
            var ambiguityMessages = new List<string>();

            foreach (string layerQuery in resolvedRequest.LayerQueries)
            {
                var candidateResult = _filterService.FindLayerCandidates(new FindLayerCandidatesRequest
                {
                    FilePath = resolvedRequest.FilePath,
                    LayerQuery = layerQuery,
                    ExactMatch = false
                });

                if (!candidateResult.Success || candidateResult.Data is null)
                {
                    return candidateResult.Message;
                }

                if (candidateResult.Data.Count == 0)
                {
                    ambiguityMessages.Add($"未找到与 [{layerQuery}] 匹配的图层。");
                    continue;
                }

                if (candidateResult.Data.Count == 1)
                {
                    resolvedLayers.Add(candidateResult.Data[0].FullPath);
                    continue;
                }

                bool alreadyConfirmed = candidateResult.Data.Any(candidate =>
                    resolvedRequest.ConfirmedLayerFullPaths.Any(confirmed =>
                        string.Equals(confirmed, candidate.FullPath, StringComparison.OrdinalIgnoreCase)));

                if (!alreadyConfirmed)
                {
                    ambiguityMessages.Add(_filterService.FormatLayerCandidates(layerQuery, candidateResult.Data, candidateResult.Message));
                }
            }

            if (ambiguityMessages.Count > 0)
            {
                var builder = new StringBuilder();
                builder.AppendLine("检测到 layer 条件存在歧义或缺失确认，请先确认目标图层：");
                foreach (string message in ambiguityMessages)
                {
                    builder.AppendLine(message);
                }
                builder.AppendLine("请提供 confirmedLayerFullPaths 后重新执行组合筛查。\n");
                return builder.ToString();
            }

            resolvedRequest.ConfirmedLayerFullPaths = resolvedLayers
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            resolvedRequest.LayerQueries.Clear();
        }

        var result = _filterService.Filter(resolvedRequest);
        return result.Success && result.Data is not null
            ? _filterService.FormatFilterResult(result.Data)
            : result.Message;
    }
}