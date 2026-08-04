extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using ObjectLinetypeSource = rhinocommon::Rhino.DocObjects.ObjectLinetypeSource;
using ObjectPlotColorSource = rhinocommon::Rhino.DocObjects.ObjectPlotColorSource;
using ObjectPlotWeightSource = rhinocommon::Rhino.DocObjects.ObjectPlotWeightSource;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveGeometryMetadataOperator : IGeometryMetadataOperator
{
    public GeometryMetadataSnapshot Snapshot(RhinoObject rhinoObject)
    {
        ObjectAttributes attributes = rhinoObject.Attributes;
        return new GeometryMetadataSnapshot
        {
            LayerIndex = attributes.LayerIndex,
            ObjectColor = ToColorData(attributes.ObjectColor),
            ColorSource = (int)attributes.ColorSource,
            PlotColor = ToColorData(attributes.PlotColor),
            PlotColorSource = (int)attributes.PlotColorSource,
            PlotWeight = attributes.PlotWeight,
            PlotWeightSource = (int)attributes.PlotWeightSource,
            LinetypeIndex = attributes.LinetypeIndex,
            LinetypeSource = (int)attributes.LinetypeSource,
            Name = attributes.Name ?? string.Empty,
            Visible = attributes.Visible,
            UserStrings = ReadUserStrings(attributes)
        };
    }

    public GeometryMetadataSummary Summarize(RhinoDoc document, RhinoObject rhinoObject)
    {
        ObjectAttributes attributes = rhinoObject.Attributes;
        return new GeometryMetadataSummary
        {
            LayerIndex = attributes.LayerIndex,
            LayerFullPath = document.Layers.FindIndex(attributes.LayerIndex)?.FullPath ?? string.Empty,
            Name = attributes.Name ?? string.Empty,
            ColorSource = attributes.ColorSource.ToString(),
            ObjectColor = ToColorData(attributes.ObjectColor),
            UserStringCount = ReadUserStrings(attributes).Count
        };
    }

    public OperationResponse<IReadOnlyList<ObjectEditWarning>> Replay(RhinoDoc document, Guid objectId, GeometryMetadataSnapshot snapshot)
    {
        RhinoObject? rhinoObject = document.Objects.FindId(objectId);
        if (rhinoObject is null)
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail($"OBJECT_NOT_FOUND: {objectId}");
        }

        ObjectAttributes attributes = rhinoObject.Attributes.Duplicate();
        attributes.LayerIndex = snapshot.LayerIndex;
        attributes.ObjectColor = FromColorData(snapshot.ObjectColor);
        attributes.ColorSource = (ObjectColorSource)snapshot.ColorSource;
        attributes.PlotColor = FromColorData(snapshot.PlotColor);
        attributes.PlotColorSource = (ObjectPlotColorSource)snapshot.PlotColorSource;
        attributes.PlotWeight = snapshot.PlotWeight;
        attributes.PlotWeightSource = (ObjectPlotWeightSource)snapshot.PlotWeightSource;
        attributes.LinetypeIndex = snapshot.LinetypeIndex;
        attributes.LinetypeSource = (ObjectLinetypeSource)snapshot.LinetypeSource;
        attributes.Name = snapshot.Name;
        attributes.Visible = snapshot.Visible;

        var currentUserStrings = attributes.GetUserStrings();
        if (currentUserStrings is not null)
        {
            foreach (string? key in currentUserStrings.AllKeys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    attributes.DeleteUserString(key);
                }
            }
        }

        foreach ((string key, string value) in snapshot.UserStrings)
        {
            attributes.SetUserString(key, value);
        }

        if (!document.Objects.ModifyAttributes(objectId, attributes, true))
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail($"ModifyAttributes failed: {objectId}");
        }

        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(Array.Empty<ObjectEditWarning>());
    }

    private static IReadOnlyDictionary<string, string> ReadUserStrings(ObjectAttributes attributes)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var userStrings = attributes.GetUserStrings();
        if (userStrings is null)
        {
            return values;
        }

        foreach (string? key in userStrings.AllKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                values[key] = userStrings[key] ?? string.Empty;
            }
        }

        return values;
    }

    private static GeometryColorData ToColorData(Color color)
    {
        return new GeometryColorData
        {
            A = color.A,
            R = color.R,
            G = color.G,
            B = color.B
        };
    }

    private static Color FromColorData(GeometryColorData color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
