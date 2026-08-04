using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class ReferenceImageMaterialPlanningService
{
    public OperationResponse<ReferenceImageMaterialPlanningResponse> Plan(
        PlanReferenceImageMaterialsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Brief.ObjectType))
        {
            return OperationResponse<ReferenceImageMaterialPlanningResponse>.Fail("Brief.ObjectType is required.");
        }

        var plan = new ReferenceImageMaterialPlan
        {
            ReferenceImageLabel = request.Brief.ReferenceImageLabel,
            ObjectType = request.Brief.ObjectType
        };

        foreach (ReferenceImageMaterialCue cue in request.Brief.MaterialCues)
        {
            string key = string.IsNullOrWhiteSpace(cue.Key) ? "default" : cue.Key;
            plan.Materials.Add(new ReferenceImagePlannedMaterial
            {
                Key = key,
                Name = BuildMaterialName(request.Brief.ReferenceImageLabel, request.Brief.ObjectType, key),
                BaseColor = cue.BaseColor,
                Intent = cue.Intent,
                Roughness = cue.Roughness,
                Transparency = cue.Transparency,
                TextureLikely = cue.TextureLikely,
                TextureFallback = cue.TextureLikely
                    ? "Use color-only material until texture creation and mapping tools are available."
                    : string.Empty
            });
        }

        if (plan.Materials.Count == 0)
        {
            plan.Materials.Add(new ReferenceImagePlannedMaterial
            {
                Key = "default",
                Name = BuildMaterialName(request.Brief.ReferenceImageLabel, request.Brief.ObjectType, "default")
            });
            plan.Fallbacks.Add("Added neutral default material because the brief contained no material cues.");
        }

        foreach (ReferenceImagePlannedMaterial material in plan.Materials)
        {
            List<string> partNames = request.Decomposition.Parts
                .Where(part => string.Equals(ResolveMaterialKey(part.MaterialKey), material.Key, StringComparison.OrdinalIgnoreCase))
                .Select(part => part.PartName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (partNames.Count == 0 && material.Key == "default")
            {
                partNames = request.Decomposition.Parts
                    .Where(part => string.IsNullOrWhiteSpace(part.MaterialKey))
                    .Select(part => part.PartName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var objectIds = new List<Guid>();
            foreach (string partName in partNames)
            {
                if (request.ObjectIdsByPartName.TryGetValue(partName, out List<Guid>? ids))
                {
                    objectIds.AddRange(ids.Where(id => id != Guid.Empty));
                }
            }

            plan.Assignments.Add(new ReferenceImageMaterialAssignmentPlan
            {
                MaterialKey = material.Key,
                MaterialName = material.Name,
                PartNames = partNames,
                ObjectIds = objectIds.Distinct().ToList()
            });
        }

        foreach (ReferenceImagePlannedMaterial material in plan.Materials.Where(item => item.TextureLikely))
        {
            plan.Fallbacks.Add($"{material.Name}: texture cue will use base color until texture tools are added.");
        }

        return OperationResponse<ReferenceImageMaterialPlanningResponse>.Ok(
            new ReferenceImageMaterialPlanningResponse
            {
                Plan = plan,
                Warnings = plan.Warnings
                    .Select(message => new ObjectEditWarning { Code = "MATERIAL_PLAN_WARNING", Message = message })
                    .ToList()
            },
            "Reference image material planning completed.");
    }

    private static string BuildMaterialName(string referenceImageLabel, string objectType, string key)
    {
        string stem = FirstNonEmpty(referenceImageLabel, objectType, "reference-object");
        string cleanStem = string.Join("-", stem.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return $"refimg {cleanStem} {key}".Trim();
    }

    private static string ResolveMaterialKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "default" : value.Trim();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "reference-object";
    }
}
