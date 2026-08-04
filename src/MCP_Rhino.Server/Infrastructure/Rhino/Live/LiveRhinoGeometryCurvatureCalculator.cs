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
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using SurfaceCurvature = rhinocommon::Rhino.Geometry.SurfaceCurvature;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoGeometryCurvatureCalculator : ILiveGeometryCurvatureCalculator
{
    public OperationResponse<IReadOnlyList<GeometryCurvatureSample>> GetCurvatureSamples(
        CurvatureSampleEntryRequest entry,
        Guid objectId,
        string geometryTypeName,
        GeometryBase geometry)
    {
        try
        {
            return geometry switch
            {
                Curve curve => OperationResponse<IReadOnlyList<GeometryCurvatureSample>>.Ok(
                    SampleCurve(entry, objectId, geometryTypeName, curve)),
                Surface surface => OperationResponse<IReadOnlyList<GeometryCurvatureSample>>.Ok(
                    SampleSurface(entry, objectId, geometryTypeName, surface)),
                _ => OperationResponse<IReadOnlyList<GeometryCurvatureSample>>.Fail(
                    $"Unsupported geometry type for curvature sampling: {geometryTypeName}")
            };
        }
        catch (Exception ex)
        {
            return OperationResponse<IReadOnlyList<GeometryCurvatureSample>>.Fail($"Curvature sampling failed: {ex.Message}");
        }
    }

    public OperationResponse<GeometryContinuityResult> CheckContinuity(
        ContinuityCheckEntryRequest entry,
        string firstGeometryTypeName,
        GeometryBase firstGeometry,
        string secondGeometryTypeName,
        GeometryBase secondGeometry)
    {
        try
        {
            OperationResponse<Curve> firstCurve = GetContinuityCurve(firstGeometry, entry.FirstEdgeIndex);
            if (!firstCurve.Success || firstCurve.Data is null)
            {
                return OperationResponse<GeometryContinuityResult>.Fail(firstCurve.Message);
            }

            OperationResponse<Curve> secondCurve = GetContinuityCurve(secondGeometry, entry.SecondEdgeIndex);
            if (!secondCurve.Success || secondCurve.Data is null)
            {
                return OperationResponse<GeometryContinuityResult>.Fail(secondCurve.Message);
            }

            Curve curveA = firstCurve.Data;
            Curve curveB = secondCurve.Data;
            (bool useStartA, bool useStartB, double gap) = FindBestEndpointPair(curveA, curveB);
            Vector3d tangentA = useStartA ? curveA.TangentAtStart : curveA.TangentAtEnd;
            Vector3d tangentB = useStartB ? curveB.TangentAtStart : curveB.TangentAtEnd;
            double tangentAngle = ComputeUnsignedAngle(tangentA, tangentB);

            double curvatureRatio = double.NaN;
            if (entry.TargetKind == GeometryContinuityKind.G2)
            {
                Vector3d curvatureA = useStartA ? curveA.CurvatureAt(curveA.Domain.T0) : curveA.CurvatureAt(curveA.Domain.T1);
                Vector3d curvatureB = useStartB ? curveB.CurvatureAt(curveB.Domain.T0) : curveB.CurvatureAt(curveB.Domain.T1);
                curvatureRatio = ComputeCurvatureRatioDifference(curvatureA.Length, curvatureB.Length);
            }

            bool isContinuous = entry.TargetKind switch
            {
                GeometryContinuityKind.G0 => gap <= entry.PositionTolerance,
                GeometryContinuityKind.G1 => gap <= entry.PositionTolerance && tangentAngle <= entry.AngleToleranceRadians,
                GeometryContinuityKind.G2 => gap <= entry.PositionTolerance
                    && tangentAngle <= entry.AngleToleranceRadians
                    && curvatureRatio <= entry.CurvatureToleranceRatio,
                _ => false
            };

            return OperationResponse<GeometryContinuityResult>.Ok(new GeometryContinuityResult
            {
                EntryId = entry.EntryId,
                FirstObjectId = entry.FirstObjectId,
                SecondObjectId = entry.SecondObjectId,
                TargetKind = entry.TargetKind,
                Success = true,
                IsContinuous = isContinuous,
                GapDistance = gap,
                TangentAngleRadians = tangentAngle,
                CurvatureDifferenceRatio = double.IsNaN(curvatureRatio) ? null : curvatureRatio,
                Message = $"Continuity proxy evaluated from [{firstGeometryTypeName}] to [{secondGeometryTypeName}]."
            });
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryContinuityResult>.Fail($"Continuity check failed: {ex.Message}");
        }
    }

    private static IReadOnlyList<GeometryCurvatureSample> SampleCurve(
        CurvatureSampleEntryRequest entry,
        Guid objectId,
        string geometryTypeName,
        Curve curve)
    {
        IReadOnlyList<double> parameters = entry.Mode switch
        {
            GeometryCurvatureSamplingMode.EvenByCount => CreateEvenCountParameters(curve, entry.SampleCount),
            GeometryCurvatureSamplingMode.EvenByLength => CreateEvenLengthParameters(curve, entry.StepLength),
            GeometryCurvatureSamplingMode.AtParameters => entry.Parameters,
            _ => throw new InvalidOperationException("Curve curvature sampling does not support UV lists.")
        };

        return parameters.Select(parameter =>
        {
            Point3d point = curve.PointAt(parameter);
            Vector3d curvature = curve.CurvatureAt(parameter);
            return new GeometryCurvatureSample
            {
                EntryId = entry.EntryId,
                ObjectId = objectId,
                GeometryTypeName = geometryTypeName,
                Success = true,
                Parameter = parameter,
                Point = LiveGeometryAnalysisHelpers.ToPointData(point),
                CurvatureVector = LiveGeometryAnalysisHelpers.ToVectorData(curvature),
                CurvatureMagnitude = curvature.Length
            };
        }).ToList();
    }

    private static IReadOnlyList<GeometryCurvatureSample> SampleSurface(
        CurvatureSampleEntryRequest entry,
        Guid objectId,
        string geometryTypeName,
        Surface surface)
    {
        IReadOnlyList<(double U, double V)> uvPairs = entry.Mode switch
        {
            GeometryCurvatureSamplingMode.AtUVList => entry.UvSamples.Select(sample => (sample.U, sample.V)).ToList(),
            GeometryCurvatureSamplingMode.EvenByCount => CreateEvenSurfaceUvPairs(surface, entry.SampleCount),
            _ => throw new InvalidOperationException("Surface curvature sampling supports EvenByCount or AtUVList.")
        };

        return uvPairs.Select(uv =>
        {
            Point3d point = surface.PointAt(uv.U, uv.V);
            SurfaceCurvature curvature = surface.CurvatureAt(uv.U, uv.V);
            Vector3d curvatureVector = curvature.Normal * curvature.Gaussian;
            return new GeometryCurvatureSample
            {
                EntryId = entry.EntryId,
                ObjectId = objectId,
                GeometryTypeName = geometryTypeName,
                Success = true,
                U = uv.U,
                V = uv.V,
                Point = LiveGeometryAnalysisHelpers.ToPointData(point),
                CurvatureVector = LiveGeometryAnalysisHelpers.ToVectorData(curvatureVector),
                CurvatureMagnitude = curvatureVector.Length,
                GaussianCurvature = curvature.Gaussian,
                MeanCurvature = curvature.Mean
            };
        }).ToList();
    }

    private static IReadOnlyList<double> CreateEvenCountParameters(Curve curve, int sampleCount)
    {
        if (sampleCount <= 0)
        {
            throw new InvalidOperationException("SampleCount must be greater than zero.");
        }

        if (sampleCount == 1)
        {
            return new[] { curve.Domain.Mid };
        }

        var parameters = new List<double>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            double normalized = (double)i / (sampleCount - 1);
            if (!curve.NormalizedLengthParameter(normalized, out double parameter))
            {
                parameter = curve.Domain.ParameterAt(normalized);
            }

            parameters.Add(parameter);
        }

        return parameters;
    }

    private static IReadOnlyList<double> CreateEvenLengthParameters(Curve curve, double stepLength)
    {
        if (stepLength <= 0d)
        {
            throw new InvalidOperationException("StepLength must be greater than zero.");
        }

        double totalLength = curve.GetLength();
        int sampleCount = Math.Max(2, (int)Math.Floor(totalLength / stepLength) + 1);
        if (sampleCount > LiveGeometryAnalysisHelpers.HardSampleThreshold)
        {
            throw new InvalidOperationException("Curvature sample count exceeds 10000. Split the request into smaller batches.");
        }

        return CreateEvenCountParameters(curve, sampleCount);
    }

    private static IReadOnlyList<(double U, double V)> CreateEvenSurfaceUvPairs(Surface surface, int sampleCount)
    {
        if (sampleCount <= 0)
        {
            throw new InvalidOperationException("SampleCount must be greater than zero.");
        }

        double side = Math.Ceiling(Math.Sqrt(sampleCount));
        var result = new List<(double U, double V)>(sampleCount);
        for (int uIndex = 0; uIndex < side && result.Count < sampleCount; uIndex++)
        {
            for (int vIndex = 0; vIndex < side && result.Count < sampleCount; vIndex++)
            {
                double u = surface.Domain(0).ParameterAt(side <= 1 ? 0.5 : (double)uIndex / (side - 1));
                double v = surface.Domain(1).ParameterAt(side <= 1 ? 0.5 : (double)vIndex / (side - 1));
                result.Add((u, v));
            }
        }

        return result;
    }

    private static OperationResponse<Curve> GetContinuityCurve(GeometryBase geometry, int? edgeIndex)
    {
        if (geometry is Curve curve)
        {
            return OperationResponse<Curve>.Ok(curve);
        }

        Brep? brep = geometry switch
        {
            Surface surface => surface.ToBrep(),
            Brep brepGeometry => brepGeometry,
            _ => null
        };
        if (brep is null)
        {
            return OperationResponse<Curve>.Fail("Continuity supports only curves or surfaces/breps with edge indices.");
        }

        if (!edgeIndex.HasValue || edgeIndex.Value < 0 || edgeIndex.Value >= brep.Edges.Count)
        {
            return OperationResponse<Curve>.Fail("Surface/Brep continuity requires valid FirstEdgeIndex and SecondEdgeIndex.");
        }

        return OperationResponse<Curve>.Ok(brep.Edges[edgeIndex.Value].DuplicateCurve());
    }

    private static (bool UseStartA, bool UseStartB, double Gap) FindBestEndpointPair(Curve curveA, Curve curveB)
    {
        var pairs = new[]
        {
            (UseStartA: true, UseStartB: true, Gap: curveA.PointAtStart.DistanceTo(curveB.PointAtStart)),
            (UseStartA: true, UseStartB: false, Gap: curveA.PointAtStart.DistanceTo(curveB.PointAtEnd)),
            (UseStartA: false, UseStartB: true, Gap: curveA.PointAtEnd.DistanceTo(curveB.PointAtStart)),
            (UseStartA: false, UseStartB: false, Gap: curveA.PointAtEnd.DistanceTo(curveB.PointAtEnd))
        };

        return pairs.OrderBy(pair => pair.Gap).First();
    }

    private static double ComputeUnsignedAngle(Vector3d first, Vector3d second)
    {
        if (!first.Unitize() || !second.Unitize())
        {
            return double.PositiveInfinity;
        }

        double angle = Vector3d.VectorAngle(first, second);
        return Math.Min(angle, Math.Abs(Math.PI - angle));
    }

    private static double ComputeCurvatureRatioDifference(double first, double second)
    {
        double scale = Math.Max(Math.Max(Math.Abs(first), Math.Abs(second)), 1e-12);
        return Math.Abs(first - second) / scale;
    }
}
