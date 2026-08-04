using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class ReferenceImageProductGeometryPlanningService
{
    private readonly ReferenceImagePrimitiveDecompositionService _decompositionService;

    public ReferenceImageProductGeometryPlanningService(
        ReferenceImagePrimitiveDecompositionService decompositionService)
    {
        _decompositionService = decompositionService;
    }

    public OperationResponse<ReferenceImageProductGeometryPlanResponse> Plan(
        PlanReferenceImageProductGeometryRequest request)
    {
        OperationResponse<ReferenceImagePrimitiveDecompositionResponse> decomposition =
            _decompositionService.Decompose(new DecomposeReferenceImagePrimitivesRequest
            {
                Brief = request.Brief,
                Scale = request.Scale
            });
        if (!decomposition.Success || decomposition.Data is null)
        {
            return OperationResponse<ReferenceImageProductGeometryPlanResponse>.Fail(decomposition.Message);
        }

        var plan = new ReferenceImageProductGeometryPlan
        {
            ReferenceImageLabel = request.Brief.ReferenceImageLabel,
            ObjectType = request.Brief.ObjectType
        };

        var warnings = new List<ObjectEditWarning>();
        warnings.AddRange(decomposition.Data.Warnings);

        foreach (ReferenceImagePrimitivePart part in decomposition.Data.Decomposition.Parts)
        {
            ReferenceImageProductGeometryPlanPart planPart = BuildPlanPart(part);
            plan.Parts.Add(planPart);
            if (planPart.Strategy == ReferenceImageProductGeometryStrategyKind.Unsupported)
            {
                plan.CapabilityGaps.Add($"{part.PartName}: no safe geometry strategy for {part.PreferredPrimitive}.");
            }
        }

        foreach (ReferenceImageMaterialCue cue in request.Brief.MaterialCues)
        {
            plan.MaterialOnlyCues.Add(DescribeCue(cue.PartName, cue.Description));
        }

        foreach (ReferenceImageVisibleDetailCue cue in request.Brief.DetailCues)
        {
            string description = DescribeCue(cue.PartName, cue.Description);
            if (cue.Role == ReferenceImagePartRole.MaterialOnly || IsMaterialLikeCue(cue))
            {
                plan.MaterialOnlyCues.Add(description);
                continue;
            }

            if (cue.Role == ReferenceImagePartRole.Reference || IsReferenceOnlyCue(cue))
            {
                plan.ReferenceOnlyCues.Add(description);
            }
        }

        plan.PlannedObjectCount = plan.Parts.Count(part =>
            part.Strategy is not ReferenceImageProductGeometryStrategyKind.MaterialOnly
                and not ReferenceImageProductGeometryStrategyKind.ReferenceOnly
                and not ReferenceImageProductGeometryStrategyKind.Unsupported);
        plan.BlockingGapCount = plan.CapabilityGaps.Count;

        if (plan.MaterialOnlyCues.Count > 0 || plan.ReferenceOnlyCues.Count > 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "NON_GEOMETRY_CUES_PLANNED_OUT",
                Message = "Material, texture, label, lighting, shadow, and room-context cues were classified out of geometry before mutation."
            });
        }

        return OperationResponse<ReferenceImageProductGeometryPlanResponse>.Ok(
            new ReferenceImageProductGeometryPlanResponse
            {
                Plan = plan,
                Warnings = warnings
            },
            "Reference image product geometry preview plan completed.");
    }

    private static ReferenceImageProductGeometryPlanPart BuildPlanPart(ReferenceImagePrimitivePart part)
    {
        (ReferenceImageProductGeometryStrategyKind strategy, string toolName) = ResolveStrategy(part);
        var notes = new List<string>(part.Notes);
        if (part.HasLocalFrame)
        {
            notes.Add("local frame supplied; creation should honor normal and x-axis.");
        }

        if (part.HasTaper)
        {
            notes.Add("taper supplied; start and end section sizes should be preserved.");
        }

        return new ReferenceImageProductGeometryPlanPart
        {
            PartName = part.PartName,
            Role = part.Role,
            Primitive = part.PreferredPrimitive,
            Strategy = strategy,
            ToolName = toolName,
            HasLocalFrame = part.HasLocalFrame,
            HasTaper = part.HasTaper,
            HasProfile = part.ProfilePoints.Count > 0,
            CenterX = part.CenterX,
            CenterY = part.CenterY,
            CenterZ = part.CenterZ,
            SizeX = part.SizeX,
            SizeY = part.SizeY,
            SizeZ = part.SizeZ,
            Notes = notes
        };
    }

    private static (ReferenceImageProductGeometryStrategyKind Strategy, string ToolName) ResolveStrategy(
        ReferenceImagePrimitivePart part)
    {
        return part.PreferredPrimitive switch
        {
            ReferenceImagePrimitiveVocabularyKind.MaterialOnly => (ReferenceImageProductGeometryStrategyKind.MaterialOnly, "material-plan"),
            ReferenceImagePrimitiveVocabularyKind.DecalPlane => (ReferenceImageProductGeometryStrategyKind.ReferenceOnly, "material/decal-plan"),
            ReferenceImagePrimitiveVocabularyKind.TaperedBox => (ReferenceImageProductGeometryStrategyKind.TaperedBox, "CreateTaperedBoxes"),
            ReferenceImagePrimitiveVocabularyKind.ProfileExtrusion => (ReferenceImageProductGeometryStrategyKind.ProfileExtrusion, "CreateProfileExtrusionsFromPoints"),
            ReferenceImagePrimitiveVocabularyKind.Loft => (ReferenceImageProductGeometryStrategyKind.Loft, "CreateLoftsFromProfiles"),
            ReferenceImagePrimitiveVocabularyKind.Pipe => (ReferenceImageProductGeometryStrategyKind.Pipe, "CreatePipesFromPoints"),
            ReferenceImagePrimitiveVocabularyKind.SubD => (ReferenceImageProductGeometryStrategyKind.SubD, "CreateSubDCushions"),
            ReferenceImagePrimitiveVocabularyKind.Capsule => (ReferenceImageProductGeometryStrategyKind.Pipe, "CreateCapsules/CreatePipesFromPoints"),
            ReferenceImagePrimitiveVocabularyKind.RoundedBox
                or ReferenceImagePrimitiveVocabularyKind.Box => (ReferenceImageProductGeometryStrategyKind.RoundedBox, "CreateRoundedBoxes"),
            ReferenceImagePrimitiveVocabularyKind.Ellipsoid
                or ReferenceImagePrimitiveVocabularyKind.Sphere
                or ReferenceImagePrimitiveVocabularyKind.Cylinder
                or ReferenceImagePrimitiveVocabularyKind.Cone
                or ReferenceImagePrimitiveVocabularyKind.Torus
                or ReferenceImagePrimitiveVocabularyKind.RaisedStrip => (ReferenceImageProductGeometryStrategyKind.GeneralPrimitive, "existing general primitive tools"),
            _ => (ReferenceImageProductGeometryStrategyKind.Unsupported, string.Empty)
        };
    }

    private static bool IsMaterialLikeCue(ReferenceImageVisibleDetailCue cue)
    {
        string text = $"{cue.Kind} {cue.Description}".ToLowerInvariant();
        return ContainsAny(text, "material", "texture", "fabric", "woven", "weave", "grain", "highlight", "gloss", "color");
    }

    private static bool IsReferenceOnlyCue(ReferenceImageVisibleDetailCue cue)
    {
        string text = $"{cue.Kind} {cue.Description}".ToLowerInvariant();
        return ContainsAny(text, "shadow", "cast shadow", "lighting", "label", "dimension", "ruler", "room", "rug", "floor", "background");
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeCue(string partName, string description)
    {
        string target = string.IsNullOrWhiteSpace(partName) ? "reference image" : partName.Trim();
        return string.IsNullOrWhiteSpace(description) ? target : $"{target}: {description.Trim()}";
    }
}
