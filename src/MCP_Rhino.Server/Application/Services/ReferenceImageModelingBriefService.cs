using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class ReferenceImageModelingBriefService
{
    public OperationResponse<ReferenceImageModelBriefResponse> Build(BuildReferenceImageModelBriefRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReferenceImagePath)
            && string.IsNullOrWhiteSpace(request.ReferenceImageLabel))
        {
            return OperationResponse<ReferenceImageModelBriefResponse>.Fail(
                "ReferenceImagePath or ReferenceImageLabel is required.");
        }

        string objectType = string.IsNullOrWhiteSpace(request.ObjectType)
            ? "unknown-object"
            : request.ObjectType.Trim();
        string label = !string.IsNullOrWhiteSpace(request.ReferenceImageLabel)
            ? request.ReferenceImageLabel.Trim()
            : Path.GetFileName(request.ReferenceImagePath);
        var warnings = new List<ObjectEditWarning>();

        double width = PositiveOrDefault(request.WidthRatio, 1d);
        double depth = PositiveOrDefault(request.DepthRatio, 1d);
        double height = PositiveOrDefault(request.HeightRatio, 1d);
        NormalizeRatios(ref width, ref depth, ref height);

        List<ReferenceImageBriefPart> parts = request.Parts
            .Select(MapPart)
            .ToList();
        if (parts.Count == 0)
        {
            parts.Add(new ReferenceImageBriefPart
            {
                Name = string.IsNullOrWhiteSpace(request.PrimaryTargetObject) ? "primary body" : request.PrimaryTargetObject.Trim(),
                Role = ReferenceImagePartRole.PrimaryMass,
                EdgeCharacter = request.OverallEdgeCharacter,
                RelativeWidth = width,
                RelativeDepth = depth,
                RelativeHeight = height,
                MaterialKey = "default"
            });
            warnings.Add(new ObjectEditWarning
            {
                Code = "SYNTHESIZED_PRIMARY_PART",
                Message = "No part list was supplied, so the brief synthesized one primary body part."
            });
        }

        var brief = new ReferenceImageModelBrief
        {
            ReferenceImagePath = request.ReferenceImagePath.Trim(),
            ReferenceImageLabel = label,
            ObjectType = objectType,
            ObjectTypeConfidence = Clamp01(request.ObjectTypeConfidence),
            VisibleObjectCount = Math.Max(1, request.VisibleObjectCount),
            PrimaryTargetObject = string.IsNullOrWhiteSpace(request.PrimaryTargetObject)
                ? objectType
                : request.PrimaryTargetObject.Trim(),
            ViewpointAssumption = string.IsNullOrWhiteSpace(request.ViewpointAssumption)
                ? "single reference image; infer hidden depth conservatively"
                : request.ViewpointAssumption.Trim(),
            WidthRatio = width,
            DepthRatio = depth,
            HeightRatio = height,
            OverallEdgeCharacter = request.OverallEdgeCharacter,
            SymmetryAssumptions = CleanStrings(request.SymmetryAssumptions),
            Parts = parts,
            DetailCues = request.DetailCues.Select(MapDetailCue).ToList(),
            MaterialCues = request.MaterialCues.Select(MapMaterialCue).ToList(),
            Unknowns = CleanStrings(request.Unknowns),
            RiskyAssumptions = CleanStrings(request.RiskyAssumptions)
        };

        if (brief.MaterialCues.Count == 0)
        {
            brief.MaterialCues.Add(new ReferenceImageMaterialCue
            {
                Key = "default",
                PartName = brief.PrimaryTargetObject,
                Description = "neutral fallback material because no material cue was supplied",
                BaseColor = new RhinoDisplayColor { R = 160, G = 160, B = 160 },
                Intent = ReferenceImageMaterialIntent.Matte,
                Roughness = 0.75d
            });
            warnings.Add(new ObjectEditWarning
            {
                Code = "SYNTHESIZED_DEFAULT_MATERIAL",
                Message = "No material cues were supplied, so a neutral default material was added."
            });
        }

        return OperationResponse<ReferenceImageModelBriefResponse>.Ok(
            new ReferenceImageModelBriefResponse
            {
                Brief = brief,
                Warnings = warnings
            },
            "Reference image model brief built from structured observations.");
    }

    private static ReferenceImageBriefPart MapPart(ReferenceImageBriefPartRequest request)
    {
        return new ReferenceImageBriefPart
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? "unnamed part" : request.Name.Trim(),
            ParentName = request.ParentName.Trim(),
            Role = request.Role,
            EdgeCharacter = request.EdgeCharacter,
            PreferredPrimitiveHint = request.PreferredPrimitiveHint,
            RelativeCenterX = request.RelativeCenterX,
            RelativeCenterY = request.RelativeCenterY,
            RelativeCenterZ = request.RelativeCenterZ,
            RelativeWidth = PositiveOrDefault(request.RelativeWidth, 1d),
            RelativeDepth = PositiveOrDefault(request.RelativeDepth, 1d),
            RelativeHeight = PositiveOrDefault(request.RelativeHeight, 1d),
            HasLocalFrame = request.HasLocalFrame,
            NormalX = request.NormalX,
            NormalY = request.NormalY,
            NormalZ = request.NormalZ,
            XAxisX = request.XAxisX,
            XAxisY = request.XAxisY,
            XAxisZ = request.XAxisZ,
            HasTaper = request.HasTaper,
            StartWidth = PositiveOrDefault(request.StartWidth, 0d),
            StartDepth = PositiveOrDefault(request.StartDepth, 0d),
            EndWidth = PositiveOrDefault(request.EndWidth, 0d),
            EndDepth = PositiveOrDefault(request.EndDepth, 0d),
            MaterialKey = request.MaterialKey.Trim(),
            Anchors = request.Anchors
                .Where(anchor => !string.IsNullOrWhiteSpace(anchor.Name))
                .Select(anchor => new ReferenceImageBriefAnchor
                {
                    Name = anchor.Name.Trim(),
                    X = anchor.X,
                    Y = anchor.Y,
                    Z = anchor.Z
                })
                .ToList(),
            ProfilePoints = request.ProfilePoints
                .Select(point => new ReferenceImageBriefProfilePoint
                {
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z
                })
                .ToList(),
            Notes = CleanStrings(request.Notes)
        };
    }

    private static ReferenceImageVisibleDetailCue MapDetailCue(ReferenceImageVisibleDetailCueRequest request)
    {
        return new ReferenceImageVisibleDetailCue
        {
            PartName = request.PartName.Trim(),
            Kind = request.Kind.Trim(),
            Description = request.Description.Trim(),
            Role = request.Role,
            PreferredRepresentation = request.PreferredRepresentation
        };
    }

    private static ReferenceImageMaterialCue MapMaterialCue(ReferenceImageMaterialCueRequest request)
    {
        ObjectColorRequest color = request.BaseColor ?? new ObjectColorRequest { R = 160, G = 160, B = 160 };
        return new ReferenceImageMaterialCue
        {
            Key = string.IsNullOrWhiteSpace(request.Key) ? "default" : request.Key.Trim(),
            PartName = request.PartName.Trim(),
            Description = request.Description.Trim(),
            BaseColor = new RhinoDisplayColor
            {
                R = Math.Clamp(color.R, 0, 255),
                G = Math.Clamp(color.G, 0, 255),
                B = Math.Clamp(color.B, 0, 255)
            },
            Intent = request.Intent,
            Roughness = Clamp01(request.Roughness),
            Transparency = Clamp01(request.Transparency),
            TextureLikely = request.TextureLikely
        };
    }

    private static List<string> CleanStrings(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void NormalizeRatios(ref double width, ref double depth, ref double height)
    {
        double max = Math.Max(width, Math.Max(depth, height));
        if (max <= 0d)
        {
            width = 1d;
            depth = 1d;
            height = 1d;
            return;
        }

        width /= max;
        depth /= max;
        height /= max;
    }

    private static double PositiveOrDefault(double value, double fallback)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d ? value : fallback;
    }

    private static double Clamp01(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value) ? 0d : Math.Clamp(value, 0d, 1d);
    }
}
