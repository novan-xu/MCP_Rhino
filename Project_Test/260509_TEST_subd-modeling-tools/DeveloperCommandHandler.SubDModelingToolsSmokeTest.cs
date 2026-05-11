using System.ComponentModel;
using System.Reflection;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;
using MCP_Rhino.Server.Tools.Geometry.SubD;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string SubDModelingToolsSlug = "subd-modeling-tools-smoke-test";

    partial void RegisterSubDModelingToolsHandlers()
    {
        _extensionHandlers[SubDModelingToolsSlug] = HandleSubDModelingToolsSmokeTest;
    }

    private bool HandleSubDModelingToolsSmokeTest(string[] args)
    {
        try
        {
            RunSubDModelingToolsSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SubD modeling tools smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunSubDModelingToolsSmoke()
    {
        RequireSubDToolMetadata();

        var service = new RhinoSubDModelingService(new LiveSubDModelingOperator(_liveRhinoDocumentAccessor));
        SubDCagePreviewResponse preview = RequireSubDSmokeSuccess(
            service.PreviewCage(new PreviewSubDCageRequest
            {
                Vertices = CubeVertices(),
                Faces = CubeFaces()
            }),
            "Preview valid SubD cage");
        RequireSubDSmoke(preview.VertexCount == 8 && preview.FaceCount == 6, "Valid cube cage preview should report 8 vertices and 6 faces.");
        RequireSubDSmoke(preview.NonManifoldEdgeCount == 0, "Valid cube cage should not have non-manifold edges.");

        OperationResponse<SubDCagePreviewResponse> invalidPreview = service.PreviewCage(new PreviewSubDCageRequest
        {
            Vertices = CubeVertices(),
            Faces = new List<SubDFaceRequest> { new() { VertexIndices = new List<int> { 0, 1, 99 } } }
        });
        RequireSubDSmoke(!invalidPreview.Success && invalidPreview.Message.Contains("out-of-range", StringComparison.OrdinalIgnoreCase),
            "Invalid SubD cage preview should reject out-of-range face indices.");

        OperationResponse<SubDCreationResponse> createFallback = service.CreateCage(new CreateSubDCageRequest
        {
            FilePath = "C:/mcp-rhino/subd-smoke.3dm",
            Common = new GeometryCreationCommonOptions { LayerFullPath = "MCP::SUBD_SMOKE" },
            Items = new List<SubDCageItemRequest>
            {
                new()
                {
                    Name = "cube subd smoke",
                    Vertices = CubeVertices(),
                    Faces = CubeFaces()
                }
            }
        });
        RequireSubDLiveBoundary(createFallback.Message, "CreateSubDCage");

        OperationResponse<SubDInspectionResponse> inspectFallback = service.Inspect(new InspectSubDObjectsRequest
        {
            FilePath = "C:/mcp-rhino/subd-smoke.3dm",
            ObjectIds = new List<Guid> { Guid.NewGuid() }
        });
        RequireSubDLiveBoundary(inspectFallback.Message, "InspectSubDObjects");

        var productPlanningService = new ReferenceImageProductGeometryPlanningService(new ReferenceImagePrimitiveDecompositionService());
        ReferenceImageProductGeometryPlan plan = RequireSubDSmokeSuccess(
            productPlanningService.Plan(new PlanReferenceImageProductGeometryRequest
            {
                Brief = RequireSubDSmokeSuccess(_referenceImageModelBriefSkill.Build(SubDCushionBrief()), "Build SubD cushion brief").Brief,
                Scale = 1d
            }),
            "Plan SubD cushion strategy").Plan;
        ReferenceImageProductGeometryPlanPart cushion = plan.Parts.First(part => part.PartName == "crowned seat cushion");
        RequireSubDSmoke(cushion.Strategy == ReferenceImageProductGeometryStrategyKind.SubD, "Product plan should select SubD for explicit SubD cushion parts.");

        Console.WriteLine("[OK] SubD cage preview accepts valid topology and rejects invalid face indices.");
        Console.WriteLine("[OK] SubD create/inspect tools route to live Rhino in CLI fallback mode.");
        Console.WriteLine("[OK] reference-image product planning can select SubD for soft cushion geometry without using SubD for materials or shadows.");
    }

    private static void RequireSubDToolMetadata()
    {
        RequireSubDTool<PreviewSubDCageTool>(nameof(PreviewSubDCageTool.PreviewSubDCage), readOnly: true);
        RequireSubDTool<CreateSubDCageTool>(nameof(CreateSubDCageTool.CreateSubDCage), readOnly: false);
        RequireSubDTool<CreateSubDBoxTool>(nameof(CreateSubDBoxTool.CreateSubDBox), readOnly: false);
        RequireSubDTool<CreateSubDCushionsTool>(nameof(CreateSubDCushionsTool.CreateSubDCushions), readOnly: false);
        RequireSubDTool<InspectSubDObjectsTool>(nameof(InspectSubDObjectsTool.InspectSubDObjects), readOnly: true);
    }

    private static void RequireSubDTool<TTool>(string methodName, bool readOnly)
    {
        MethodInfo method = typeof(TTool).GetMethod(methodName)
            ?? throw new InvalidOperationException($"Tool method was not found: {typeof(TTool).Name}.{methodName}");
        McpServerToolAttribute attribute = method.GetCustomAttribute<McpServerToolAttribute>()
            ?? throw new InvalidOperationException($"Missing MCP attribute: {methodName}");
        RequireSubDSmoke(attribute.ReadOnly == readOnly && !attribute.Destructive && !attribute.OpenWorld, $"Unexpected safety metadata for {methodName}.");
        RequireSubDSmoke(!string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description), $"Missing description for {methodName}.");
    }

    private static BuildReferenceImageModelBriefRequest SubDCushionBrief()
    {
        return new BuildReferenceImageModelBriefRequest
        {
            ReferenceImageLabel = "subd-cushion-smoke",
            ObjectType = "upholstered chair cushion",
            OverallEdgeCharacter = ReferenceImageObjectEdgeCharacter.Soft,
            Parts = new List<ReferenceImageBriefPartRequest>
            {
                new()
                {
                    Name = "crowned seat cushion",
                    Role = ReferenceImagePartRole.PrimaryMass,
                    PreferredPrimitiveHint = ReferenceImagePrimitiveVocabularyKind.SubD,
                    RelativeWidth = 2,
                    RelativeDepth = 1.6,
                    RelativeHeight = 0.28,
                    Notes = new List<string> { "soft crowned cushion with continuous upholstery bulge" },
                    MaterialKey = "fabric"
                }
            },
            DetailCues = new List<ReferenceImageVisibleDetailCueRequest>
            {
                new()
                {
                    PartName = "crowned seat cushion",
                    Kind = "texture",
                    Description = "woven fabric texture",
                    Role = ReferenceImagePartRole.MaterialOnly
                },
                new()
                {
                    PartName = "crowned seat cushion",
                    Kind = "shadow",
                    Description = "cast shadow along lower edge",
                    Role = ReferenceImagePartRole.Reference
                }
            },
            MaterialCues = new List<ReferenceImageMaterialCueRequest>
            {
                new()
                {
                    Key = "fabric",
                    PartName = "crowned seat cushion",
                    Description = "woven upholstery",
                    Intent = ReferenceImageMaterialIntent.Fabric,
                    TextureLikely = true
                }
            }
        };
    }

    private static List<SubDVertexRequest> CubeVertices()
    {
        return new List<SubDVertexRequest>
        {
            new() { X = -0.5, Y = -0.5, Z = -0.5 },
            new() { X = 0.5, Y = -0.5, Z = -0.5 },
            new() { X = 0.5, Y = 0.5, Z = -0.5 },
            new() { X = -0.5, Y = 0.5, Z = -0.5 },
            new() { X = -0.5, Y = -0.5, Z = 0.5 },
            new() { X = 0.5, Y = -0.5, Z = 0.5 },
            new() { X = 0.5, Y = 0.5, Z = 0.5 },
            new() { X = -0.5, Y = 0.5, Z = 0.5 }
        };
    }

    private static List<SubDFaceRequest> CubeFaces()
    {
        return new List<SubDFaceRequest>
        {
            FaceRequest(0, 3, 2, 1),
            FaceRequest(4, 5, 6, 7),
            FaceRequest(0, 1, 5, 4),
            FaceRequest(1, 2, 6, 5),
            FaceRequest(2, 3, 7, 6),
            FaceRequest(3, 0, 4, 7)
        };
    }

    private static SubDFaceRequest FaceRequest(params int[] indices)
    {
        return new SubDFaceRequest { VertexIndices = indices.ToList() };
    }

    private static T RequireSubDSmokeSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireSubDLiveBoundary(string message, string operation)
    {
        RequireSubDSmoke(message.Contains("LIVE_RHINO_REQUIRED", StringComparison.Ordinal),
            $"{operation} should reach the live-Rhino boundary in CLI fallback mode; got {message}");
    }

    private static void RequireSubDSmoke(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
