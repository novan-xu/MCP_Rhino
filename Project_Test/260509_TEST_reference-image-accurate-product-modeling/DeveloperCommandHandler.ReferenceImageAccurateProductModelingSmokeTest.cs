using System.ComponentModel;
using System.Reflection;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Geometry;
using MCP_Rhino.Server.Tools.Geometry.CurveOps;
using MCP_Rhino.Server.Tools.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string ReferenceImageAccurateProductModelingSlug = "reference-image-accurate-product-modeling-smoke-test";

    partial void RegisterReferenceImageAccurateProductModelingHandlers()
    {
        _extensionHandlers[ReferenceImageAccurateProductModelingSlug] = HandleReferenceImageAccurateProductModelingSmokeTest;
    }

    private bool HandleReferenceImageAccurateProductModelingSmokeTest(string[] args)
    {
        try
        {
            RunReferenceImageAccurateProductModelingSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Reference image accurate product modeling smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunReferenceImageAccurateProductModelingSmoke()
    {
        RequireProductToolMetadata();

        ReferenceImageModelBrief brief = RequireProductSmokeSuccess(
            _referenceImageModelBriefSkill.Build(ProductChairBrief()),
            "Build product chair brief").Brief;
        ReferenceImagePrimitiveDecomposition decomposition = RequireProductSmokeSuccess(
            _referenceImagePrimitiveDecompositionSkill.Decompose(new DecomposeReferenceImagePrimitivesRequest
            {
                Brief = brief,
                Scale = 1d
            }),
            "Decompose product chair").Decomposition;

        ReferenceImagePrimitivePart back = RequirePart(decomposition, "sloped back cushion");
        RequireProductSmoke(back.HasLocalFrame, "Back cushion local frame should be preserved.");
        RequireProductSmoke(Math.Abs(back.NormalY + 0.5d) < 0.01d, "Back cushion normal should preserve the supplied slope.");

        ReferenceImagePrimitivePart leg = RequirePart(decomposition, "front left tapered leg");
        RequireProductSmoke(leg.PreferredPrimitive == ReferenceImagePrimitiveVocabularyKind.TaperedBox, "Tapered leg should resolve to TaperedBox.");
        RequireProductSmoke(leg.HasTaper && leg.StartWidth > leg.EndWidth, "Tapered leg should preserve start/end section sizes.");

        var planningService = new ReferenceImageProductGeometryPlanningService(new ReferenceImagePrimitiveDecompositionService());
        ReferenceImageProductGeometryPlan productPlan = RequireProductSmokeSuccess(
            planningService.Plan(new PlanReferenceImageProductGeometryRequest
            {
                Brief = brief,
                Scale = 1d
            }),
            "Preview product geometry plan").Plan;

        ReferenceImageProductGeometryPlanPart legPlan = productPlan.Parts.First(part => part.PartName == "front left tapered leg");
        RequireProductSmoke(legPlan.Strategy == ReferenceImageProductGeometryStrategyKind.TaperedBox, "Product plan should select TaperedBox for tapered leg.");
        RequireProductSmoke(productPlan.MaterialOnlyCues.Any(cue => cue.Contains("woven", StringComparison.OrdinalIgnoreCase)), "Woven fabric should be material-only.");
        RequireProductSmoke(productPlan.ReferenceOnlyCues.Any(cue => cue.Contains("shadow", StringComparison.OrdinalIgnoreCase)), "Cast shadow should be reference-only.");

        OperationResponse<GeneralPrimitiveCreationResponse> taperedFallback = _geometryCreationSkill.Create(new CreateTaperedBoxesRequest
        {
            FilePath = "C:/mcp-rhino/reference-image-product-smoke.3dm",
            Common = new GeometryCreationCommonOptions { LayerFullPath = "MCP::PRODUCT_SMOKE" },
            Items = new List<TaperedBoxItemRequest>
            {
                new()
                {
                    StartX = -0.4,
                    StartZ = 0,
                    EndX = -0.25,
                    EndY = 0.1,
                    EndZ = 1,
                    StartWidth = 0.12,
                    StartDepth = 0.12,
                    EndWidth = 0.08,
                    EndDepth = 0.08,
                    UpZ = 1
                }
            }
        });
        RequireLiveBoundary(taperedFallback.Message, "CreateTaperedBoxes");

        OperationResponse<CurveDerivedGeometryResponse> extrusionFallback =
            _curveDerivedGeometryService.CreateProfileExtrusionsFromPoints(new CreateProfileExtrusionsFromPointsRequest
            {
                FilePath = "C:/mcp-rhino/reference-image-product-smoke.3dm",
                Common = new GeometryCreationCommonOptions { LayerFullPath = "MCP::PRODUCT_SMOKE" },
                Entries = new List<ProfileExtrusionFromPointsEntryRequest>
                {
                    new()
                    {
                        ProfilePoints = RectangleProfile(),
                        VectorZ = 0.2
                    }
                }
            });
        RequireLiveBoundary(extrusionFallback.Message, "CreateProfileExtrusionsFromPoints");

        Console.WriteLine("[OK] reference-image product brief preserves local frames, taper, and cue classification.");
        Console.WriteLine("[OK] product geometry preview selects tapered/profile-capable strategies and excludes material/shadow cues from geometry.");
        Console.WriteLine("[OK] new product geometry creation tools route to live Rhino in CLI fallback mode.");
    }

    private static void RequireProductToolMetadata()
    {
        RequireTool<CreateTaperedBoxesTool>(nameof(CreateTaperedBoxesTool.CreateTaperedBoxes), readOnly: false);
        RequireTool<CreateProfileExtrusionsFromPointsTool>(nameof(CreateProfileExtrusionsFromPointsTool.CreateProfileExtrusionsFromPoints), readOnly: false);
        RequireTool<CreateLoftsFromProfilesTool>(nameof(CreateLoftsFromProfilesTool.CreateLoftsFromProfiles), readOnly: false);
        RequireTool<CreatePipesFromPointsTool>(nameof(CreatePipesFromPointsTool.CreatePipesFromPoints), readOnly: false);
        RequireTool<PreviewReferenceImageProductGeometryPlanTool>(nameof(PreviewReferenceImageProductGeometryPlanTool.PreviewReferenceImageProductGeometryPlan), readOnly: true);
    }

    private static void RequireTool<TTool>(string methodName, bool readOnly)
    {
        MethodInfo method = typeof(TTool).GetMethod(methodName)
            ?? throw new InvalidOperationException($"Tool method was not found: {typeof(TTool).Name}.{methodName}");
        McpServerToolAttribute attribute = method.GetCustomAttribute<McpServerToolAttribute>()
            ?? throw new InvalidOperationException($"Missing MCP attribute: {methodName}");
        RequireProductSmoke(attribute.ReadOnly == readOnly && !attribute.Destructive && !attribute.OpenWorld, $"Unexpected safety metadata for {methodName}.");
        RequireProductSmoke(!string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description), $"Missing description for {methodName}.");
    }

    private static ReferenceImagePrimitivePart RequirePart(
        ReferenceImagePrimitiveDecomposition decomposition,
        string partName)
    {
        return decomposition.Parts.FirstOrDefault(part => string.Equals(part.PartName, partName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Missing decomposed part: {partName}");
    }

    private static BuildReferenceImageModelBriefRequest ProductChairBrief()
    {
        return new BuildReferenceImageModelBriefRequest
        {
            ReferenceImageLabel = "kyajah-armchair-product-smoke",
            ObjectType = "wide upholstered armchair with solid wood legs",
            ObjectTypeConfidence = 0.95d,
            PrimaryTargetObject = "armchair",
            WidthRatio = 27.55,
            DepthRatio = 30.31,
            HeightRatio = 29.92,
            OverallEdgeCharacter = ReferenceImageObjectEdgeCharacter.Mixed,
            Parts = new List<ReferenceImageBriefPartRequest>
            {
                new()
                {
                    Name = "seat cushion",
                    Role = ReferenceImagePartRole.PrimaryMass,
                    EdgeCharacter = ReferenceImageObjectEdgeCharacter.Soft,
                    RelativeWidth = 27.55,
                    RelativeDepth = 22,
                    RelativeHeight = 5,
                    RelativeCenterZ = 15,
                    MaterialKey = "woven-fabric"
                },
                new()
                {
                    Name = "sloped back cushion",
                    Role = ReferenceImagePartRole.StructuralMass,
                    EdgeCharacter = ReferenceImageObjectEdgeCharacter.Soft,
                    RelativeWidth = 27.55,
                    RelativeDepth = 5,
                    RelativeHeight = 18,
                    RelativeCenterY = 10,
                    RelativeCenterZ = 22,
                    HasLocalFrame = true,
                    NormalY = -0.5,
                    NormalZ = 0.8660254,
                    XAxisX = 1,
                    MaterialKey = "woven-fabric"
                },
                new()
                {
                    Name = "front left tapered leg",
                    Role = ReferenceImagePartRole.StructuralMass,
                    EdgeCharacter = ReferenceImageObjectEdgeCharacter.Hard,
                    PreferredPrimitiveHint = ReferenceImagePrimitiveVocabularyKind.TaperedBox,
                    HasTaper = true,
                    StartWidth = 1.4,
                    StartDepth = 1.4,
                    EndWidth = 0.85,
                    EndDepth = 0.85,
                    RelativeWidth = 1,
                    RelativeDepth = 1,
                    RelativeHeight = 13,
                    MaterialKey = "wood",
                    Anchors = new List<ReferenceImageBriefAnchorRequest>
                    {
                        new() { Name = "bottom start", X = -11, Y = -9, Z = 0 },
                        new() { Name = "top end", X = -9.5, Y = -6.5, Z = 13 }
                    }
                }
            },
            DetailCues = new List<ReferenceImageVisibleDetailCueRequest>
            {
                new()
                {
                    PartName = "seat cushion",
                    Kind = "texture",
                    Description = "warm orange woven fabric texture",
                    Role = ReferenceImagePartRole.MaterialOnly
                },
                new()
                {
                    PartName = "floor",
                    Kind = "shadow",
                    Description = "dark horizontal cast shadow under the chair",
                    Role = ReferenceImagePartRole.Reference
                }
            },
            MaterialCues = new List<ReferenceImageMaterialCueRequest>
            {
                new()
                {
                    Key = "woven-fabric",
                    PartName = "seat cushion",
                    Description = "orange woven upholstery",
                    BaseColor = new ObjectColorRequest { R = 180, G = 112, B = 70 },
                    Intent = ReferenceImageMaterialIntent.Fabric,
                    TextureLikely = true
                },
                new()
                {
                    Key = "wood",
                    PartName = "front left tapered leg",
                    Description = "warm solid wood",
                    BaseColor = new ObjectColorRequest { R = 126, G = 76, B = 42 },
                    Intent = ReferenceImageMaterialIntent.Wood
                }
            }
        };
    }

    private static List<GeneralPrimitivePointRequest> RectangleProfile()
    {
        return new List<GeneralPrimitivePointRequest>
        {
            new() { X = -0.5, Y = -0.2, Z = 0 },
            new() { X = 0.5, Y = -0.2, Z = 0 },
            new() { X = 0.5, Y = 0.2, Z = 0 },
            new() { X = -0.5, Y = 0.2, Z = 0 }
        };
    }

    private static T RequireProductSmokeSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireLiveBoundary(string message, string operation)
    {
        RequireProductSmoke(message.Contains("LIVE_RHINO_REQUIRED", StringComparison.Ordinal),
            $"{operation} should reach the live-Rhino boundary in CLI fallback mode; got {message}");
    }

    private static void RequireProductSmoke(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
