using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoGrasshopperAuthoringService
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(10);
    private readonly ILiveGrasshopperOperator _operator;
    private readonly ConcurrentDictionary<string, PreviewEntry> _previews = new(StringComparer.Ordinal);

    public RhinoGrasshopperAuthoringService(ILiveGrasshopperOperator @operator)
    {
        _operator = @operator;
    }

    public OperationResponse<GrasshopperDefinitionListResponse> Start(GrasshopperEngineRequest request) =>
        ValidateEngine(request.Engine) ?? _operator.Start(request);

    public OperationResponse<GrasshopperDefinitionListResponse> ListDefinitions(GrasshopperEngineRequest request) =>
        ValidateEngine(request.Engine) ?? _operator.ListDefinitions(request);

    public OperationResponse<GrasshopperComponentSearchResponse> SearchComponents(SearchGrasshopperComponentsRequest request)
    {
        OperationResponse<GrasshopperComponentSearchResponse>? engine = ValidateEngine<GrasshopperComponentSearchResponse>(request.Engine);
        if (engine is not null)
        {
            return engine;
        }

        if (request.MaxResults is < 1 or > GrasshopperLimits.MaxSearchResults)
        {
            return OperationResponse<GrasshopperComponentSearchResponse>.Fail("GRASSHOPPER_GRAPH_LIMIT_EXCEEDED: MaxResults must be between 1 and 100.");
        }

        return _operator.SearchComponents(request);
    }

    public OperationResponse<GrasshopperComponentDescriptionResponse> DescribeComponent(DescribeGrasshopperComponentRequest request) =>
        ValidateEngine<GrasshopperComponentDescriptionResponse>(request.Engine) ?? _operator.DescribeComponent(request);

    public OperationResponse<GrasshopperGraphResponse> GetGraph(GetGrasshopperGraphRequest request)
    {
        OperationResponse<GrasshopperGraphResponse>? validation = ValidateTarget<GrasshopperGraphResponse>(request);
        if (validation is not null)
        {
            return validation;
        }

        if (request.DataSampleSize is < 0 or > GrasshopperLimits.MaxDataItemsPerParameter)
        {
            return OperationResponse<GrasshopperGraphResponse>.Fail("GRASSHOPPER_GRAPH_LIMIT_EXCEEDED: DataSampleSize must be between 0 and 20.");
        }

        return _operator.GetGraph(request);
    }

    public OperationResponse<GrasshopperGraphPreviewResponse> PreviewGraph(PreviewApplyGrasshopperGraphRequest request)
    {
        OperationResponse<GrasshopperGraphPreviewResponse>? validation = ValidateGraphRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        OperationResponse<GrasshopperGraphPreviewResponse> response = _operator.PreviewGraph(request);
        if (!response.Success || response.Data is null)
        {
            return response;
        }

        string token = CreateToken();
        string fingerprint = Fingerprint(request.Graph);
        var preview = new GrasshopperGraphPreviewResponse
        {
            PreviewToken = token,
            DefinitionRevision = response.Data.DefinitionRevision,
            PredictedNodeAdditions = response.Data.PredictedNodeAdditions,
            PredictedWireAdditions = response.Data.PredictedWireAdditions,
            ResolvedNodes = response.Data.ResolvedNodes,
            ResolvedWires = response.Data.ResolvedWires,
            Warnings = response.Data.Warnings
        };

        _previews[token] = PreviewEntry.ForGraph(request, fingerprint, preview);
        RemoveExpiredPreviews();
        return OperationResponse<GrasshopperGraphPreviewResponse>.Ok(preview);
    }

    public OperationResponse<GrasshopperGraphApplyResponse> ApplyGraph(ApplyGrasshopperGraphRequest request)
    {
        OperationResponse<GrasshopperGraphApplyResponse>? validation = ValidateGraphRequest<GrasshopperGraphApplyResponse>(request);
        if (validation is not null)
        {
            return validation;
        }

        if (!_previews.TryRemove(request.PreviewToken, out PreviewEntry? entry)
            || entry.GraphPreview is null
            || entry.ExpiresUtc < DateTimeOffset.UtcNow
            || !entry.Matches(request, Fingerprint(request.Graph)))
        {
            return OperationResponse<GrasshopperGraphApplyResponse>.Fail("GRASSHOPPER_GRAPH_CHANGED: preview token is missing, expired, or bound to another target/specification.");
        }

        return _operator.ApplyGraph(request, entry.GraphPreview);
    }

    public OperationResponse<GrasshopperSolveResponse> Solve(SolveGrasshopperDefinitionRequest request)
    {
        OperationResponse<GrasshopperSolveResponse>? validation = ValidateTarget<GrasshopperSolveResponse>(request);
        if (validation is not null)
        {
            return validation;
        }

        if (request.DataSampleSize is < 0 or > GrasshopperLimits.MaxDataItemsPerParameter)
        {
            return OperationResponse<GrasshopperSolveResponse>.Fail("GRASSHOPPER_GRAPH_LIMIT_EXCEEDED: DataSampleSize must be between 0 and 20.");
        }

        return _operator.Solve(request);
    }

    public OperationResponse<GrasshopperClearPreviewResponse> PreviewClear(GrasshopperDefinitionRequest request)
    {
        OperationResponse<GrasshopperClearPreviewResponse>? validation = ValidateTarget<GrasshopperClearPreviewResponse>(request);
        if (validation is not null)
        {
            return validation;
        }

        OperationResponse<GrasshopperClearPreviewResponse> response = _operator.PreviewClear(request);
        if (!response.Success || response.Data is null)
        {
            return response;
        }

        string token = CreateToken();
        var preview = new GrasshopperClearPreviewResponse
        {
            PreviewToken = token,
            DefinitionRevision = response.Data.DefinitionRevision,
            ObjectIds = response.Data.ObjectIds,
            WireCount = response.Data.WireCount
        };
        _previews[token] = PreviewEntry.ForClear(request, preview);
        RemoveExpiredPreviews();
        return OperationResponse<GrasshopperClearPreviewResponse>.Ok(preview);
    }

    public OperationResponse<GrasshopperClearApplyResponse> ApplyClear(ApplyClearGrasshopperDefinitionRequest request)
    {
        OperationResponse<GrasshopperClearApplyResponse>? validation = ValidateTarget<GrasshopperClearApplyResponse>(request);
        if (validation is not null)
        {
            return validation;
        }

        if (!_previews.TryRemove(request.PreviewToken, out PreviewEntry? entry)
            || entry.ClearPreview is null
            || entry.ExpiresUtc < DateTimeOffset.UtcNow
            || !entry.Matches(request, string.Empty))
        {
            return OperationResponse<GrasshopperClearApplyResponse>.Fail("GRASSHOPPER_GRAPH_CHANGED: clear preview token is missing, expired, or bound to another target.");
        }

        return _operator.ApplyClear(request, entry.ClearPreview);
    }

    private static OperationResponse<GrasshopperDefinitionListResponse>? ValidateEngine(GrasshopperEngine engine) =>
        ValidateEngine<GrasshopperDefinitionListResponse>(engine);

    private static OperationResponse<T>? ValidateEngine<T>(GrasshopperEngine engine) =>
        engine == GrasshopperEngine.Gh1
            ? null
            : OperationResponse<T>.Fail("GRASSHOPPER_ENGINE_UNAVAILABLE: GH2 requires the future Rhino 9 adapter.");

    private static OperationResponse<T>? ValidateTarget<T>(GrasshopperDefinitionRequest request)
    {
        OperationResponse<T>? engine = ValidateEngine<T>(request.Engine);
        if (engine is not null)
        {
            return engine;
        }

        if (string.IsNullOrWhiteSpace(request.DefinitionSessionId))
        {
            return OperationResponse<T>.Fail("GRASSHOPPER_DEFINITION_NOT_FOUND: DefinitionSessionId is required.");
        }

        return null;
    }

    private static OperationResponse<GrasshopperGraphPreviewResponse>? ValidateGraphRequest(PreviewApplyGrasshopperGraphRequest request) =>
        ValidateGraphRequestCore<GrasshopperGraphPreviewResponse>(request, request.Graph);

    private static OperationResponse<T>? ValidateGraphRequest<T>(ApplyGrasshopperGraphRequest request) =>
        ValidateGraphRequestCore<T>(request, request.Graph);

    private static OperationResponse<T>? ValidateGraphRequestCore<T>(GrasshopperDefinitionRequest request, GrasshopperGraphSpec graph)
    {
        OperationResponse<T>? target = ValidateTarget<T>(request);
        if (target is not null)
        {
            return target;
        }

        if (graph.Nodes.Count > GrasshopperLimits.MaxNewNodes || graph.Wires.Count > GrasshopperLimits.MaxNewWires)
        {
            return OperationResponse<T>.Fail("GRASSHOPPER_GRAPH_LIMIT_EXCEEDED");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (GrasshopperNodeSpec node in graph.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.ClientKey) || !keys.Add(node.ClientKey))
            {
                return OperationResponse<T>.Fail("GRASSHOPPER_APPLY_FAILED: node ClientKey values must be non-empty and unique.");
            }

            if (node.ExistingObjectId is not null)
            {
                continue;
            }

            if (node.Kind == GrasshopperNodeKind.Component && node.ComponentGuid is null && string.IsNullOrWhiteSpace(node.ComponentName))
            {
                return OperationResponse<T>.Fail("GRASSHOPPER_COMPONENT_NOT_FOUND: component nodes require ComponentGuid or ComponentName.");
            }

            if (node.Kind == GrasshopperNodeKind.NumberSlider)
            {
                if (node.SliderMinimum is null || node.SliderMaximum is null || node.SliderValue is null
                    || node.SliderMinimum >= node.SliderMaximum
                    || node.SliderValue < node.SliderMinimum || node.SliderValue > node.SliderMaximum
                    || node.SliderDecimalPlaces is < 0 or > 12)
                {
                    return OperationResponse<T>.Fail("GRASSHOPPER_APPLY_FAILED: invalid number-slider range, value, or decimal places.");
                }
            }
        }

        return null;
    }

    private static string Fingerprint(GrasshopperGraphSpec graph)
    {
        byte[] bytes = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(graph));
        return Convert.ToHexString(bytes);
    }

    private static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    private void RemoveExpiredPreviews()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach ((string key, PreviewEntry value) in _previews)
        {
            if (value.ExpiresUtc < now)
            {
                _previews.TryRemove(key, out _);
            }
        }
    }

    private sealed class PreviewEntry
    {
        public required string FilePath { get; init; }
        public required GrasshopperEngine Engine { get; init; }
        public required string DefinitionSessionId { get; init; }
        public required string Fingerprint { get; init; }
        public required DateTimeOffset ExpiresUtc { get; init; }
        public GrasshopperGraphPreviewResponse? GraphPreview { get; init; }
        public GrasshopperClearPreviewResponse? ClearPreview { get; init; }

        public bool Matches(GrasshopperDefinitionRequest request, string fingerprint) =>
            string.Equals(FilePath, request.FilePath, StringComparison.OrdinalIgnoreCase)
            && Engine == request.Engine
            && string.Equals(DefinitionSessionId, request.DefinitionSessionId, StringComparison.Ordinal)
            && string.Equals(Fingerprint, fingerprint, StringComparison.Ordinal);

        public static PreviewEntry ForGraph(
            PreviewApplyGrasshopperGraphRequest request,
            string fingerprint,
            GrasshopperGraphPreviewResponse preview) => new()
        {
            FilePath = request.FilePath,
            Engine = request.Engine,
            DefinitionSessionId = request.DefinitionSessionId,
            Fingerprint = fingerprint,
            ExpiresUtc = DateTimeOffset.UtcNow + PreviewLifetime,
            GraphPreview = preview
        };

        public static PreviewEntry ForClear(
            GrasshopperDefinitionRequest request,
            GrasshopperClearPreviewResponse preview) => new()
        {
            FilePath = request.FilePath,
            Engine = request.Engine,
            DefinitionSessionId = request.DefinitionSessionId,
            Fingerprint = string.Empty,
            ExpiresUtc = DateTimeOffset.UtcNow + PreviewLifetime,
            ClearPreview = preview
        };
    }
}
