using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using MCP_Rhino.Server.Infrastructure.Plugin;
using MCP_Rhino.Server.Tools.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string GrasshopperAuthoringToolsSlug = "grasshopper-authoring-tools-smoke-test";

    partial void RegisterGrasshopperAuthoringToolsHandlers()
    {
        _extensionHandlers[GrasshopperAuthoringToolsSlug] = HandleGrasshopperAuthoringToolsSmokeTest;
    }

    private bool HandleGrasshopperAuthoringToolsSmokeTest(string[] args)
    {
        try
        {
            RunGrasshopperAuthoringContractSmoke();
            if (McpRhinoPlugin.Instance is not null)
            {
                string filePath = args.Length > 1 ? args[1] : string.Empty;
                RunGrasshopperAuthoringLiveReadSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Grasshopper authoring tools smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunGrasshopperAuthoringContractSmoke()
    {
        VerifyGrasshopperTools();
        VerifyGrasshopperContractsAndValidation();
        Console.WriteLine("[OK] Grasshopper authoring tool surface and contract validation passed.");
    }

    private static void VerifyGrasshopperTools()
    {
        var expected = new Dictionary<Type, (string Method, bool ReadOnly, bool Destructive, bool OpenWorld)>
        {
            [typeof(ListGrasshopperDefinitionsTool)] = ("ListGrasshopperDefinitions", true, false, false),
            [typeof(StartGrasshopperTool)] = ("StartGrasshopper", false, false, false),
            [typeof(SearchGrasshopperComponentsTool)] = ("SearchGrasshopperComponents", true, false, false),
            [typeof(DescribeGrasshopperComponentTool)] = ("DescribeGrasshopperComponent", true, false, true),
            [typeof(GetGrasshopperGraphTool)] = ("GetGrasshopperGraph", true, false, false),
            [typeof(PreviewApplyGrasshopperGraphTool)] = ("PreviewApplyGrasshopperGraph", true, false, true),
            [typeof(ApplyGrasshopperGraphTool)] = ("ApplyGrasshopperGraph", false, false, true),
            [typeof(SolveGrasshopperDefinitionTool)] = ("SolveGrasshopperDefinition", false, false, true),
            [typeof(PreviewClearGrasshopperDefinitionTool)] = ("PreviewClearGrasshopperDefinition", true, false, false),
            [typeof(ApplyClearGrasshopperDefinitionTool)] = ("ApplyClearGrasshopperDefinition", false, true, false)
        };

        foreach ((Type type, var expectation) in expected)
        {
            RequireGrasshopper(type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null, $"{type.Name} is missing [McpServerToolType].");
            MethodInfo method = type.GetMethod(expectation.Method)
                ?? throw new InvalidOperationException($"{expectation.Method} was not found.");
            McpServerToolAttribute tool = method.GetCustomAttribute<McpServerToolAttribute>()
                ?? throw new InvalidOperationException($"{expectation.Method} is missing [McpServerTool].");
            RequireGrasshopper(tool.ReadOnly == expectation.ReadOnly, $"{expectation.Method} ReadOnly mismatch.");
            RequireGrasshopper(tool.Destructive == expectation.Destructive, $"{expectation.Method} Destructive mismatch.");
            RequireGrasshopper(tool.OpenWorld == expectation.OpenWorld, $"{expectation.Method} OpenWorld mismatch.");
            string description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
            RequireGrasshopper(!string.IsNullOrWhiteSpace(description), $"{expectation.Method} description is empty.");
        }

        string[] names = expected.Values.Select(value => value.Method).ToArray();
        RequireGrasshopper(names.Distinct(StringComparer.Ordinal).Count() == 10, "Grasshopper tool names are not unique.");
    }

    private static void VerifyGrasshopperContractsAndValidation()
    {
        var fake = new FakeGrasshopperOperator();
        var service = new RhinoGrasshopperAuthoringService(fake);

        OperationResponse<GrasshopperDefinitionListResponse> gh2 = service.ListDefinitions(new GrasshopperEngineRequest
        {
            FilePath = "C:/test/model.3dm",
            Engine = GrasshopperEngine.Gh2
        });
        RequireGrasshopper(!gh2.Success && gh2.Message.StartsWith("GRASSHOPPER_ENGINE_UNAVAILABLE", StringComparison.Ordinal), "GH2 should be gated.");

        var duplicate = new PreviewApplyGrasshopperGraphRequest
        {
            FilePath = "C:/test/model.3dm",
            DefinitionSessionId = "definition-1",
            Graph = new GrasshopperGraphSpec
            {
                Nodes =
                [
                    Slider("same", 0, 10, 2),
                    Slider("same", 0, 10, 3)
                ]
            }
        };
        OperationResponse<GrasshopperGraphPreviewResponse> duplicateResult = service.PreviewGraph(duplicate);
        RequireGrasshopper(!duplicateResult.Success, "Duplicate client keys should fail before the adapter.");

        var invalidSlider = new PreviewApplyGrasshopperGraphRequest
        {
            FilePath = "C:/test/model.3dm",
            DefinitionSessionId = "definition-1",
            Graph = new GrasshopperGraphSpec { Nodes = [Slider("bad", 5, 1, 2)] }
        };
        RequireGrasshopper(!service.PreviewGraph(invalidSlider).Success, "Invalid slider bounds should fail.");

        GrasshopperGraphSpec graph = new()
        {
            Nodes =
            [
                new GrasshopperNodeSpec
                {
                    ClientKey = "script",
                    Kind = GrasshopperNodeKind.Component,
                    ComponentGuid = FakeGrasshopperOperator.ScriptComponentGuid
                }
            ]
        };
        var previewRequest = new PreviewApplyGrasshopperGraphRequest
        {
            FilePath = "C:/test/model.3dm",
            DefinitionSessionId = "definition-1",
            Graph = graph
        };
        OperationResponse<GrasshopperGraphPreviewResponse> preview = service.PreviewGraph(previewRequest);
        RequireGrasshopper(preview.Success && preview.Data is not null, "Script/code component preview should be allowed.");
        GrasshopperGraphPreviewResponse previewData = preview.Data
            ?? throw new InvalidOperationException("Preview data is missing.");
        RequireGrasshopper(previewData.ResolvedNodes.Single().IsExecutableCodeComponent, "Script/code component should be classified.");
        RequireGrasshopper(previewData.Warnings.Count == 1, "Script/code component preview should warn.");

        OperationResponse<GrasshopperGraphApplyResponse> apply = service.ApplyGraph(new ApplyGrasshopperGraphRequest
        {
            FilePath = previewRequest.FilePath,
            DefinitionSessionId = previewRequest.DefinitionSessionId,
            Graph = graph,
            PreviewToken = previewData.PreviewToken
        });
        RequireGrasshopper(apply.Success, "A matching script/code component preview should apply.");

        OperationResponse<GrasshopperGraphPreviewResponse> stalePreview = service.PreviewGraph(previewRequest);
        RequireGrasshopper(stalePreview.Success && stalePreview.Data is not null, "Second preview should succeed.");
        GrasshopperGraphPreviewResponse stalePreviewData = stalePreview.Data
            ?? throw new InvalidOperationException("Stale-preview data is missing.");
        OperationResponse<GrasshopperGraphApplyResponse> staleApply = service.ApplyGraph(new ApplyGrasshopperGraphRequest
        {
            FilePath = previewRequest.FilePath,
            DefinitionSessionId = previewRequest.DefinitionSessionId,
            Graph = new GrasshopperGraphSpec { Nodes = [Slider("changed", 0, 1, 0.5m)] },
            PreviewToken = stalePreviewData.PreviewToken
        });
        RequireGrasshopper(!staleApply.Success && staleApply.Message.StartsWith("GRASSHOPPER_GRAPH_CHANGED", StringComparison.Ordinal), "Changed graph must invalidate its token.");

        string json = JsonSerializer.Serialize(new ApplyGrasshopperGraphRequest { Graph = graph });
        RequireGrasshopper(!json.Contains("scriptSource", StringComparison.OrdinalIgnoreCase), "Graph contract must not expose arbitrary script source injection.");
    }

    private void RunGrasshopperAuthoringLiveReadSmoke(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Live Grasshopper smoke requires a saved Rhino document path.");
        }

        OperationResponse<GrasshopperDefinitionListResponse> started = _grasshopperAuthoringService.Start(new GrasshopperEngineRequest
        {
            FilePath = filePath,
            Engine = GrasshopperEngine.Gh1
        });
        RequireGrasshopper(started.Success, started.Message);

        OperationResponse<GrasshopperComponentSearchResponse> search = _grasshopperAuthoringService.SearchComponents(new SearchGrasshopperComponentsRequest
        {
            FilePath = filePath,
            Query = "Addition",
            MaxResults = 25
        });
        RequireGrasshopper(search.Success && search.Data is not null && search.Data.Components.Count > 0, search.Message);
        GrasshopperComponentSearchResponse searchData = search.Data
            ?? throw new InvalidOperationException("Component search data is missing.");
        Console.WriteLine($"[OK] Live GH1 runtime started; definitions={started.Data?.Definitions.Count ?? 0}; Addition matches={searchData.Components.Count}.");
    }

    private static GrasshopperNodeSpec Slider(string key, decimal minimum, decimal maximum, decimal value) => new()
    {
        ClientKey = key,
        Kind = GrasshopperNodeKind.NumberSlider,
        SliderMinimum = minimum,
        SliderMaximum = maximum,
        SliderValue = value,
        SliderDecimalPlaces = 2
    };

    private static void RequireGrasshopper(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FakeGrasshopperOperator : ILiveGrasshopperOperator
    {
        public static readonly Guid ScriptComponentGuid = new("11111111-2222-3333-4444-555555555555");

        public OperationResponse<GrasshopperDefinitionListResponse> Start(GrasshopperEngineRequest request) =>
            OperationResponse<GrasshopperDefinitionListResponse>.Ok(new GrasshopperDefinitionListResponse { RuntimeLoaded = true });
        public OperationResponse<GrasshopperDefinitionListResponse> ListDefinitions(GrasshopperEngineRequest request) => Start(request);
        public OperationResponse<GrasshopperComponentSearchResponse> SearchComponents(SearchGrasshopperComponentsRequest request) =>
            OperationResponse<GrasshopperComponentSearchResponse>.Ok(new GrasshopperComponentSearchResponse());
        public OperationResponse<GrasshopperComponentDescriptionResponse> DescribeComponent(DescribeGrasshopperComponentRequest request) =>
            OperationResponse<GrasshopperComponentDescriptionResponse>.Ok(new GrasshopperComponentDescriptionResponse());
        public OperationResponse<GrasshopperGraphResponse> GetGraph(GetGrasshopperGraphRequest request) =>
            OperationResponse<GrasshopperGraphResponse>.Ok(new GrasshopperGraphResponse());
        public OperationResponse<GrasshopperGraphPreviewResponse> PreviewGraph(PreviewApplyGrasshopperGraphRequest request) =>
            OperationResponse<GrasshopperGraphPreviewResponse>.Ok(new GrasshopperGraphPreviewResponse
            {
                DefinitionRevision = "revision-1",
                PredictedNodeAdditions = request.Graph.Nodes.Count,
                PredictedWireAdditions = request.Graph.Wires.Count,
                ResolvedNodes = request.Graph.Nodes.Select(node => new ResolvedGrasshopperNode
                {
                    ClientKey = node.ClientKey,
                    Kind = node.Kind,
                    ComponentGuid = node.ComponentGuid,
                    IsExecutableCodeComponent = node.ComponentGuid == ScriptComponentGuid
                }).ToList(),
                Warnings = request.Graph.Nodes.Any(node => node.ComponentGuid == ScriptComponentGuid)
                    ? ["Executable code component will be placed."]
                    : []
            });
        public OperationResponse<GrasshopperGraphApplyResponse> ApplyGraph(ApplyGrasshopperGraphRequest request, GrasshopperGraphPreviewResponse preview) =>
            OperationResponse<GrasshopperGraphApplyResponse>.Ok(new GrasshopperGraphApplyResponse { DefinitionRevision = "revision-2", RollbackCompleted = true });
        public OperationResponse<GrasshopperSolveResponse> Solve(SolveGrasshopperDefinitionRequest request) =>
            OperationResponse<GrasshopperSolveResponse>.Ok(new GrasshopperSolveResponse { Solved = true });
        public OperationResponse<GrasshopperClearPreviewResponse> PreviewClear(GrasshopperDefinitionRequest request) =>
            OperationResponse<GrasshopperClearPreviewResponse>.Ok(new GrasshopperClearPreviewResponse { DefinitionRevision = "revision-1" });
        public OperationResponse<GrasshopperClearApplyResponse> ApplyClear(ApplyClearGrasshopperDefinitionRequest request, GrasshopperClearPreviewResponse preview) =>
            OperationResponse<GrasshopperClearApplyResponse>.Ok(new GrasshopperClearApplyResponse { DefinitionRevision = "revision-2" });
    }
}
