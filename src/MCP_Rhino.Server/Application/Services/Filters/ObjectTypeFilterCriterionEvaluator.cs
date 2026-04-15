using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services.Filters;

public sealed class ObjectTypeFilterCriterionEvaluator : IObjectFilterCriterionEvaluator
{
    public string CriterionName => "ObjectType";

    public bool CanEvaluate(RhinoObjectFilterCriteria criteria)
    {
        return criteria.ObjectTypes.Count > 0;
    }

    public bool Evaluate(RhinoObjectInfo objectInfo, RhinoObjectFilterCriteria criteria)
    {
        return criteria.ObjectTypes.Any(requestedType => IsMatch(objectInfo, requestedType));
    }

    private static bool IsMatch(RhinoObjectInfo objectInfo, string requestedType)
    {
        string normalizedRequest = Normalize(requestedType);
        string normalizedEnum = Normalize(objectInfo.NormalizedObjectType.ToString());
        string normalizedGeometryType = Normalize(objectInfo.GeometryTypeName);
        string normalizedRawType = Normalize(objectInfo.ObjectTypeName);

        return normalizedRequest switch
        {
            "point" or "points" => objectInfo.NormalizedObjectType == RhinoObjectType.Point || normalizedGeometryType.Contains("point"),
            "curve" or "curves" => objectInfo.NormalizedObjectType == RhinoObjectType.Curve || normalizedRawType == "curve",
            "surface" or "surfaces" => objectInfo.NormalizedObjectType == RhinoObjectType.Surface || normalizedRawType == "surface",
            "brep" or "breps" or "polysurface" or "polysurfaces" => objectInfo.NormalizedObjectType == RhinoObjectType.Brep || normalizedRawType == "brep",
            "mesh" or "meshes" => objectInfo.NormalizedObjectType == RhinoObjectType.Mesh || normalizedRawType == "mesh",
            "block" or "blocks" or "blockinstance" or "blockinstances" or "instancereference" or "instancereferences" =>
                objectInfo.NormalizedObjectType == RhinoObjectType.BlockInstance || normalizedRawType == "instancereference",
            "annotationdot" or "annotationdots" or "dot" or "dots" => objectInfo.NormalizedObjectType == RhinoObjectType.AnnotationDot,
            "annotation" or "annotations" => objectInfo.NormalizedObjectType == RhinoObjectType.Annotation || objectInfo.NormalizedObjectType == RhinoObjectType.AnnotationDot,
            _ => normalizedRequest == normalizedEnum || normalizedRequest == normalizedGeometryType || normalizedRequest == normalizedRawType
        };
    }

    private static string Normalize(string value)
    {
        return value
            .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToLowerInvariant();
    }
}