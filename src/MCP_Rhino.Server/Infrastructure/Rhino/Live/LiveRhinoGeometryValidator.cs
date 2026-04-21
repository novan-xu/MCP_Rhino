extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using NurbsCurve = rhinocommon::Rhino.Geometry.NurbsCurve;
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoGeometryValidator : ILiveGeometryValidator
{
    public OperationResponse ValidateAgainstDocument(RhinoDoc document, ControlPointEditSpec spec)
    {
        RhinoObject? currentObject = document.Objects.FindId(spec.ObjectId);
        if (currentObject?.Geometry is null)
        {
            return OperationResponse.Fail($"Object does not exist or has empty geometry: {spec.ObjectId}");
        }

        switch (spec.TargetMode)
        {
            case Domain.Enums.ControlPointTargetMode.CurveIndex:
                if (currentObject.Geometry is not NurbsCurve curve)
                {
                    return OperationResponse.Fail($"Object is not NurbsCurve: {spec.ObjectId}");
                }

                if (spec.PointIndex is null || spec.PointIndex < 0 || spec.PointIndex >= curve.Points.Count)
                {
                    return OperationResponse.Fail($"PointIndex out of range. ObjectId={spec.ObjectId}, PointIndex={spec.PointIndex}");
                }
                break;

            case Domain.Enums.ControlPointTargetMode.SurfaceUV:
                if (currentObject.Geometry is not NurbsSurface surface)
                {
                    return OperationResponse.Fail($"Object is not NurbsSurface: {spec.ObjectId}");
                }

                if (spec.UIndex is null || spec.VIndex is null
                    || spec.UIndex < 0 || spec.VIndex < 0
                    || spec.UIndex >= surface.Points.CountU || spec.VIndex >= surface.Points.CountV)
                {
                    return OperationResponse.Fail(
                        $"UIndex / VIndex out of range. ObjectId={spec.ObjectId}, UIndex={spec.UIndex}, VIndex={spec.VIndex}");
                }
                break;
        }

        return OperationResponse.Ok();
    }
}
