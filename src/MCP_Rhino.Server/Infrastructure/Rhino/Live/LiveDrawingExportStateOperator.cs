extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using BackgroundStyle = rhinocommon::Rhino.Display.BackgroundStyle;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveDrawingExportStateOperator : ILiveDrawingExportStateOperator
{
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromMinutes(30);
    private readonly IGeometryMetadataOperator _metadataOperator;

    public LiveDrawingExportStateOperator(IGeometryMetadataOperator metadataOperator)
    {
        _metadataOperator = metadataOperator;
    }

    public DrawingExportSnapshot Capture(RhinoDoc document, string filePath, IReadOnlyList<Guid> objectIds)
    {
        var objectSnapshots = new Dictionary<Guid, GeometryMetadataSnapshot>();
        foreach (Guid objectId in objectIds.Distinct())
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject is null || rhinoObject.IsDeleted)
            {
                continue;
            }

            objectSnapshots[objectId] = _metadataOperator.Snapshot(rhinoObject);
        }

        DateTime createdUtc = DateTime.UtcNow;
        return new DrawingExportSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString("N"),
            FilePath = filePath,
            DocumentRuntimeSerialNumber = document.RuntimeSerialNumber,
            CreatedUtc = createdUtc,
            ExpiresUtc = createdUtc.Add(SnapshotTtl),
            Background = CaptureBackground(document),
            ObjectSnapshots = objectSnapshots
        };
    }

    public OperationResponse ApplyObjectColor(RhinoDoc document, DrawingExportSnapshot snapshot, RhinoDisplayColor? color)
    {
        if (color is null)
        {
            return OperationResponse.Ok("No drawing export object color override requested.");
        }

        foreach (Guid objectId in snapshot.ObjectSnapshots.Keys)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject is null || rhinoObject.IsDeleted)
            {
                return OperationResponse.Fail($"OBJECT_NOT_FOUND: {objectId}");
            }

            ObjectAttributes attributes = rhinoObject.Attributes.Duplicate();
            attributes.ObjectColor = color.ToColor();
            attributes.ColorSource = ObjectColorSource.ColorFromObject;

            if (!document.Objects.ModifyAttributes(objectId, attributes, true))
            {
                return OperationResponse.Fail($"ModifyAttributes failed: {objectId}");
            }
        }

        document.Views.Redraw();
        return OperationResponse.Ok("Drawing export object style applied.");
    }

    public OperationResponse SetBackground(RhinoDoc document, DrawingExportSnapshot snapshot, RhinoDisplayColor color)
    {
        Color resolved = color.ToColor();
        document.RenderSettings.BackgroundStyle = BackgroundStyle.SolidColor;
        document.RenderSettings.BackgroundColorTop = resolved;
        document.RenderSettings.BackgroundColorBottom = resolved;
        document.Views.Redraw();
        return OperationResponse.Ok("Drawing export background set.");
    }

    public OperationResponse<IReadOnlyList<ObjectEditWarning>> Restore(RhinoDoc document, DrawingExportSnapshot snapshot)
    {
        var warnings = new List<ObjectEditWarning>();

        foreach ((Guid objectId, GeometryMetadataSnapshot objectSnapshot) in snapshot.ObjectSnapshots)
        {
            OperationResponse<IReadOnlyList<ObjectEditWarning>> replay = _metadataOperator.Replay(document, objectId, objectSnapshot);
            if (!replay.Success)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "DRAWING_EXPORT_OBJECT_RESTORE_FAILED",
                    Message = replay.Message
                });
                continue;
            }

            if (replay.Data is not null)
            {
                warnings.AddRange(replay.Data);
            }
        }

        RestoreBackground(document, snapshot.Background);
        document.Views.Redraw();

        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(warnings, warnings.Count == 0
            ? "Drawing export state restored."
            : "Drawing export state restored with warnings.");
    }

    private static DrawingBackgroundSnapshot CaptureBackground(RhinoDoc document)
    {
        return new DrawingBackgroundSnapshot
        {
            RenderBackgroundStyle = (int)document.RenderSettings.BackgroundStyle,
            RenderBackgroundTop = ToColorData(document.RenderSettings.BackgroundColorTop),
            RenderBackgroundBottom = ToColorData(document.RenderSettings.BackgroundColorBottom)
        };
    }

    private static void RestoreBackground(RhinoDoc document, DrawingBackgroundSnapshot snapshot)
    {
        document.RenderSettings.BackgroundStyle = (BackgroundStyle)snapshot.RenderBackgroundStyle;
        document.RenderSettings.BackgroundColorTop = FromColorData(snapshot.RenderBackgroundTop);
        document.RenderSettings.BackgroundColorBottom = FromColorData(snapshot.RenderBackgroundBottom);
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
