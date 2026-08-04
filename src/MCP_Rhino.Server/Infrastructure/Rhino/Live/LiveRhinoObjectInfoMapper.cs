extern alias rhinocommon;

using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using LiveLayer = rhinocommon::Rhino.DocObjects.Layer;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

internal static class LiveRhinoObjectInfoMapper
{
    public static List<RhinoObjectInfo> BuildObjectInfos(RhinoDoc document)
    {
        Dictionary<int, LiveLayer> layerLookup = BuildLayerLookup(document);
        var objectInfos = new List<RhinoObjectInfo>();

        foreach (RhinoObject rhinoObject in document.Objects)
        {
            if (rhinoObject.IsDeleted)
            {
                continue;
            }

            objectInfos.Add(BuildObjectInfo(rhinoObject, layerLookup));
        }

        return objectInfos;
    }

    public static RhinoObjectInfo BuildObjectInfo(RhinoObject rhinoObject, IReadOnlyDictionary<int, LiveLayer> layerLookup)
    {
        layerLookup.TryGetValue(rhinoObject.Attributes.LayerIndex, out LiveLayer? layer);

        var userAttributes = new List<RhinoObjectUserAttributeEntry>();
        var userStrings = rhinoObject.Attributes.GetUserStrings();
        if (userStrings is not null)
        {
            foreach (string? key in userStrings.AllKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                userAttributes.Add(new RhinoObjectUserAttributeEntry
                {
                    Key = key,
                    Value = userStrings[key] ?? string.Empty
                });
            }
        }

        string rawObjectType = rhinoObject.Geometry?.ObjectType.ToString() ?? "Unknown";
        string geometryTypeName = rhinoObject.Geometry?.GetType().Name ?? rawObjectType;

        return new RhinoObjectInfo
        {
            ObjectId = rhinoObject.Attributes.ObjectId,
            ObjectTypeName = rawObjectType,
            NormalizedObjectType = NormalizeObjectType(rawObjectType, geometryTypeName),
            GeometryTypeName = geometryTypeName,
            LayerIndex = rhinoObject.Attributes.LayerIndex,
            LayerName = layer?.Name ?? "Unknown",
            LayerFullPath = layer?.FullPath ?? "Unknown",
            Name = rhinoObject.Attributes.Name ?? string.Empty,
            UserAttributes = userAttributes
        };
    }

    public static Dictionary<int, LiveLayer> BuildLayerLookup(RhinoDoc document)
    {
        var layerLookup = new Dictionary<int, LiveLayer>();
        for (int i = 0; i < document.Layers.Count; i++)
        {
            LiveLayer layer = document.Layers[i];
            if (!layer.IsDeleted)
            {
                layerLookup[layer.Index] = layer;
            }
        }

        return layerLookup;
    }

    private static RhinoObjectType NormalizeObjectType(string rawObjectType, string geometryTypeName)
    {
        if (geometryTypeName.Contains("TextDot", StringComparison.OrdinalIgnoreCase)
            || geometryTypeName.Contains("AnnotationDot", StringComparison.OrdinalIgnoreCase))
        {
            return RhinoObjectType.AnnotationDot;
        }

        return rawObjectType switch
        {
            "Point" => RhinoObjectType.Point,
            "Curve" => RhinoObjectType.Curve,
            "Surface" => RhinoObjectType.Surface,
            "Brep" => RhinoObjectType.Brep,
            "Mesh" => RhinoObjectType.Mesh,
            "InstanceReference" => RhinoObjectType.BlockInstance,
            "Annotation" => RhinoObjectType.Annotation,
            _ => RhinoObjectType.Unknown
        };
    }
}
