extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Surface = rhinocommon::Rhino.Geometry.Surface;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveBrepSurfaceDowngrader : IBrepSurfaceDowngrader
{
    public OperationResponse<(BrepDowngradeResult Result, Surface? Surface)> Evaluate(RhinoObject rhinoObject, double tolerance)
    {
        if (rhinoObject.Geometry is not Brep brep)
        {
            return Create(false, "not a brep", tolerance, null);
        }

        if (brep.Faces.Count != 1)
        {
            return Create(false, $"polysurface has {brep.Faces.Count} faces", tolerance, null);
        }

        var face = brep.Faces[0];
        if (!face.IsSurface)
        {
            return Create(false, "brep face is trimmed", tolerance, null);
        }

        Surface? underlyingSurface = face.UnderlyingSurface();
        if (underlyingSurface is null)
        {
            return Create(false, "underlying surface is missing", tolerance, null);
        }

        return Create(true, string.Empty, tolerance, underlyingSurface);
    }

    private static OperationResponse<(BrepDowngradeResult Result, Surface? Surface)> Create(
        bool isUntrimmedSingleFaceBrep,
        string reason,
        double tolerance,
        Surface? surface)
    {
        var result = new BrepDowngradeResult
        {
            IsUntrimmedSingleFaceBrep = isUntrimmedSingleFaceBrep,
            Reason = reason,
            Tolerance = tolerance,
            SurfaceTypeName = surface?.GetType().Name ?? string.Empty
        };

        return OperationResponse<(BrepDowngradeResult Result, Surface? Surface)>.Ok((result, surface));
    }
}
