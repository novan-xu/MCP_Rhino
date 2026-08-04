extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using NurbsCurve = rhinocommon::Rhino.Geometry.NurbsCurve;
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveGeometryFrameSampler : IGeometryFrameSampler
{
    public OperationResponse<IReadOnlyList<FrameSample>> Sample(GeometryBase geometry, GeometryFrameParameterSpecRequest parameterSpec)
    {
        try
        {
            return parameterSpec.Kind switch
            {
                GeometryFrameParameterSpecKind.CurveExplicit => geometry is Curve curve
                    ? SampleCurve(curve, parameterSpec.Parameters)
                    : OperationResponse<IReadOnlyList<FrameSample>>.Fail("FRAME_PARAMETER_SPEC_MISMATCH: CurveExplicit requires a curve object."),

                GeometryFrameParameterSpecKind.SurfaceExplicit => TryGetSurface(geometry, out Surface? explicitSurface)
                    ? SampleSurface(explicitSurface, parameterSpec.UvParameters)
                    : OperationResponse<IReadOnlyList<FrameSample>>.Fail("FRAME_PARAMETER_SPEC_MISMATCH: SurfaceExplicit requires a surface object."),

                GeometryFrameParameterSpecKind.AtControlPointGrevilles => geometry switch
                {
                    Curve curve => SampleCurve(curve, GetCurveGrevilleParameters(curve)),
                    _ when TryGetSurface(geometry, out Surface? grevilleSurface) => SampleSurface(grevilleSurface, GetSurfaceGrevilleParameters(grevilleSurface)),
                    _ => OperationResponse<IReadOnlyList<FrameSample>>.Fail("FRAME_PARAMETER_SPEC_MISMATCH: AtControlPointGrevilles requires a curve or surface object.")
                },

                GeometryFrameParameterSpecKind.AtParameterFractions => geometry switch
                {
                    Curve curve => SampleCurve(curve, MapCurveFractions(curve, parameterSpec.Fractions)),
                    _ when TryGetSurface(geometry, out Surface? fractionSurface) => SampleSurface(fractionSurface, MapSurfaceFractions(fractionSurface, parameterSpec.Fractions)),
                    _ => OperationResponse<IReadOnlyList<FrameSample>>.Fail("FRAME_PARAMETER_SPEC_MISMATCH: AtParameterFractions requires a curve or surface object.")
                },

                _ => OperationResponse<IReadOnlyList<FrameSample>>.Fail("FRAME_PARAMETER_SPEC_MISMATCH: ParameterSpec.Kind is required for sampled frame requests.")
            };
        }
        catch (Exception ex)
        {
            return OperationResponse<IReadOnlyList<FrameSample>>.Fail($"Frame sampling failed: {ex.Message}");
        }
    }

    private static OperationResponse<IReadOnlyList<FrameSample>> SampleCurve(Curve curve, IReadOnlyList<double> parameters)
    {
        if (parameters.Count == 0)
        {
            return OperationResponse<IReadOnlyList<FrameSample>>.Fail("At least one curve frame parameter is required.");
        }

        for (int i = 0; i < parameters.Count; i++)
        {
            double parameter = parameters[i];
            if (!IsFinite(parameter) || !IsInDomain(curve.Domain.T0, curve.Domain.T1, parameter))
            {
                return OperationResponse<IReadOnlyList<FrameSample>>.Fail($"FRAME_PARAMETER_OUT_OF_RANGE: curve parameter[{i}]={parameter}");
            }
        }

        var samples = new List<FrameSample>(parameters.Count);
        for (int i = 0; i < parameters.Count; i++)
        {
            samples.Add(CreateCurveSample(curve, i, parameters[i]));
        }

        return OperationResponse<IReadOnlyList<FrameSample>>.Ok(samples);
    }

    private static OperationResponse<IReadOnlyList<FrameSample>> SampleSurface(Surface surface, IReadOnlyList<GeometryAnalysisUvSampleRequest> uvParameters)
    {
        if (uvParameters.Count == 0)
        {
            return OperationResponse<IReadOnlyList<FrameSample>>.Fail("At least one surface UV frame parameter is required.");
        }

        for (int i = 0; i < uvParameters.Count; i++)
        {
            GeometryAnalysisUvSampleRequest uv = uvParameters[i];
            if (!IsFinite(uv.U) || !IsFinite(uv.V)
                || !IsInDomain(surface.Domain(0).T0, surface.Domain(0).T1, uv.U)
                || !IsInDomain(surface.Domain(1).T0, surface.Domain(1).T1, uv.V))
            {
                return OperationResponse<IReadOnlyList<FrameSample>>.Fail($"FRAME_PARAMETER_OUT_OF_RANGE: surface uv[{i}]=({uv.U}, {uv.V})");
            }
        }

        var samples = new List<FrameSample>(uvParameters.Count);
        for (int i = 0; i < uvParameters.Count; i++)
        {
            GeometryAnalysisUvSampleRequest uv = uvParameters[i];
            samples.Add(CreateSurfaceSample(surface, i, uv.U, uv.V));
        }

        return OperationResponse<IReadOnlyList<FrameSample>>.Ok(samples);
    }

    private static FrameSample CreateCurveSample(Curve curve, int index, double parameter)
    {
        Point3d point = curve.PointAt(parameter);
        Vector3d tangent = curve.TangentAt(parameter);
        bool degenerate = !tangent.Unitize();
        if (degenerate)
        {
            return new FrameSample
            {
                Index = index,
                Parameter = parameter,
                Origin = LiveGeometryAnalysisHelpers.ToPointData(point),
                IsDegenerate = true,
                Message = "FRAME_DEGENERATE: curve tangent is zero length."
            };
        }

        Vector3d seed = Math.Abs(tangent * Vector3d.ZAxis) > 0.95d ? Vector3d.YAxis : Vector3d.ZAxis;
        Vector3d yAxis = Vector3d.CrossProduct(seed, tangent);
        if (!yAxis.Unitize())
        {
            yAxis = Vector3d.YAxis;
        }

        Vector3d zAxis = Vector3d.CrossProduct(tangent, yAxis);
        zAxis.Unitize();

        return new FrameSample
        {
            Index = index,
            Parameter = parameter,
            Origin = LiveGeometryAnalysisHelpers.ToPointData(point),
            Tangent = LiveGeometryAnalysisHelpers.ToVectorData(tangent),
            Normal = LiveGeometryAnalysisHelpers.ToVectorData(zAxis),
            XAxis = LiveGeometryAnalysisHelpers.ToVectorData(tangent),
            YAxis = LiveGeometryAnalysisHelpers.ToVectorData(yAxis),
            ZAxis = LiveGeometryAnalysisHelpers.ToVectorData(zAxis)
        };
    }

    private static FrameSample CreateSurfaceSample(Surface surface, int index, double u, double v)
    {
        if (!surface.FrameAt(u, v, out Plane frame))
        {
            Point3d point = surface.PointAt(u, v);
            Vector3d normal = surface.NormalAt(u, v);
            bool degenerate = !normal.Unitize();
            return new FrameSample
            {
                Index = index,
                U = u,
                V = v,
                Origin = LiveGeometryAnalysisHelpers.ToPointData(point),
                Normal = degenerate ? null : LiveGeometryAnalysisHelpers.ToVectorData(normal),
                IsDegenerate = true,
                Message = "FRAME_DEGENERATE: surface frame could not be evaluated."
            };
        }

        return new FrameSample
        {
            Index = index,
            U = u,
            V = v,
            Origin = LiveGeometryAnalysisHelpers.ToPointData(frame.Origin),
            XAxis = LiveGeometryAnalysisHelpers.ToVectorData(frame.XAxis),
            YAxis = LiveGeometryAnalysisHelpers.ToVectorData(frame.YAxis),
            ZAxis = LiveGeometryAnalysisHelpers.ToVectorData(frame.ZAxis),
            Normal = LiveGeometryAnalysisHelpers.ToVectorData(frame.ZAxis)
        };
    }

    private static IReadOnlyList<double> GetCurveGrevilleParameters(Curve curve)
    {
        using NurbsCurve? nurbsCurve = curve.ToNurbsCurve();
        if (nurbsCurve is null)
        {
            return Array.Empty<double>();
        }

        var parameters = new List<double>(nurbsCurve.Points.Count);
        for (int i = 0; i < nurbsCurve.Points.Count; i++)
        {
            parameters.Add(nurbsCurve.GrevilleParameter(i));
        }

        return parameters;
    }

    private static IReadOnlyList<GeometryAnalysisUvSampleRequest> GetSurfaceGrevilleParameters(Surface surface)
    {
        using NurbsSurface? nurbsSurface = surface.ToNurbsSurface();
        if (nurbsSurface is null)
        {
            return Array.Empty<GeometryAnalysisUvSampleRequest>();
        }

        var parameters = new List<GeometryAnalysisUvSampleRequest>(nurbsSurface.Points.CountU * nurbsSurface.Points.CountV);
        for (int u = 0; u < nurbsSurface.Points.CountU; u++)
        {
            double fallbackU = MapGridIndexToDomain(surface.Domain(0).T0, surface.Domain(0).T1, nurbsSurface.Points.CountU, u);
            double grevilleU = GetSurfaceGrevilleParameter(nurbsSurface, 0, u, fallbackU);
            for (int v = 0; v < nurbsSurface.Points.CountV; v++)
            {
                double fallbackV = MapGridIndexToDomain(surface.Domain(1).T0, surface.Domain(1).T1, nurbsSurface.Points.CountV, v);
                parameters.Add(new GeometryAnalysisUvSampleRequest
                {
                    U = grevilleU,
                    V = GetSurfaceGrevilleParameter(nurbsSurface, 1, v, fallbackV)
                });
            }
        }

        return parameters;
    }

    private static IReadOnlyList<double> MapCurveFractions(Curve curve, IReadOnlyList<double> fractions)
    {
        var parameters = new List<double>(fractions.Count);
        foreach (double fraction in fractions)
        {
            if (!IsFinite(fraction) || fraction < 0d || fraction > 1d)
            {
                return Array.Empty<double>();
            }

            parameters.Add(curve.Domain.T0 + ((curve.Domain.T1 - curve.Domain.T0) * fraction));
        }

        return parameters;
    }

    private static IReadOnlyList<GeometryAnalysisUvSampleRequest> MapSurfaceFractions(Surface surface, IReadOnlyList<double> fractions)
    {
        var parameters = new List<GeometryAnalysisUvSampleRequest>(fractions.Count);
        foreach (double fraction in fractions)
        {
            if (!IsFinite(fraction) || fraction < 0d || fraction > 1d)
            {
                return Array.Empty<GeometryAnalysisUvSampleRequest>();
            }

            parameters.Add(new GeometryAnalysisUvSampleRequest
            {
                U = surface.Domain(0).T0 + ((surface.Domain(0).T1 - surface.Domain(0).T0) * fraction),
                V = surface.Domain(1).T0 + ((surface.Domain(1).T1 - surface.Domain(1).T0) * fraction)
            });
        }

        return parameters;
    }

    private static bool TryGetSurface(GeometryBase geometry, out Surface surface)
    {
        switch (geometry)
        {
            case Surface directSurface:
                surface = directSurface;
                return true;
            case Brep brep when brep.Faces.Count == 1 && brep.Faces[0].IsSurface:
                Surface? underlyingSurface = brep.Faces[0].UnderlyingSurface();
                if (underlyingSurface is not null)
                {
                    surface = underlyingSurface;
                    return true;
                }

                break;
        }

        surface = null!;
        return false;
    }

    private static bool IsInDomain(double t0, double t1, double value)
    {
        double min = Math.Min(t0, t1);
        double max = Math.Max(t0, t1);
        const double tolerance = 1e-9;
        return value >= min - tolerance && value <= max + tolerance;
    }

    private static double MapGridIndexToDomain(double t0, double t1, int count, int index)
    {
        if (count <= 1)
        {
            return (t0 + t1) * 0.5d;
        }

        double fraction = index / (double)(count - 1);
        return t0 + ((t1 - t0) * fraction);
    }

    private static double GetSurfaceGrevilleParameter(NurbsSurface surface, int direction, int index, double fallback)
    {
        int degree = Math.Max(1, (direction == 0 ? surface.OrderU : surface.OrderV) - 1);
        int knotCount = direction == 0 ? surface.KnotsU.Count : surface.KnotsV.Count;
        int start = index;
        if (start < 0 || start + degree > knotCount)
        {
            return fallback;
        }

        double sum = 0d;
        for (int offset = 0; offset < degree; offset++)
        {
            sum += direction == 0
                ? surface.KnotsU[start + offset]
                : surface.KnotsV[start + offset];
        }

        return sum / degree;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
