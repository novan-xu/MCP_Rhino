using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveGeometryEditStrategyResolver : IGeometryEditStrategyResolver
{
    public OperationResponse<StrategyResolutionResult> Resolve(CurveEditSpec editSpec, EditableGeometryDescriptor descriptor)
    {
        var steps = new List<StrategyResolutionStep>();
        var warnings = new List<ObjectEditWarning>();
        GeometryEditStrategyKind strategy;

        if (editSpec.Operation.Kind == GeometryEditOperationKind.DirectOverride)
        {
            bool polyline = string.Equals(descriptor.CurveStructure?.CurveKind, "PolylineCurve", StringComparison.Ordinal);
            strategy = polyline
                ? GeometryEditStrategyKind.ReconstructFromPoints
                : GeometryEditStrategyKind.ReconstructFromControlPoints;
            steps.Add(new StrategyResolutionStep
            {
                RuleName = "DirectOverride",
                Matched = true,
                Reason = polyline ? "PolylineCurve reconstructs from points." : "Curve reconstructs from control points."
            });
            return Create(strategy, steps, warnings);
        }

        if (editSpec.Operation.Kind != GeometryEditOperationKind.DerivedOperation || !editSpec.Operation.DerivedKind.HasValue)
        {
            return OperationResponse<StrategyResolutionResult>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        PointSelectorKind selectorKind = editSpec.PointSelector?.Kind ?? PointSelectorKind.All;
        bool appliesToAll = selectorKind == PointSelectorKind.All;
        DerivedPointOperationKind derivedKind = editSpec.Operation.DerivedKind.Value;
        DerivedPointOperationParameters parameters = editSpec.Operation.DerivedParameters ?? new DerivedPointOperationParameters();

        if (appliesToAll && derivedKind == DerivedPointOperationKind.TranslateByVector)
        {
            steps.Add(new StrategyResolutionStep
            {
                RuleName = "DerivedTranslateAll",
                Matched = true,
                Reason = "TranslateByVector applied to all points maps to an exact transform."
            });
            return Create(GeometryEditStrategyKind.ExactTransform, steps, warnings);
        }

        if (appliesToAll && derivedKind == DerivedPointOperationKind.ScaleAboutCentroid && IsUniformScale(parameters))
        {
            steps.Add(new StrategyResolutionStep
            {
                RuleName = "DerivedUniformScaleAll",
                Matched = true,
                Reason = "Uniform ScaleAboutCentroid applied to all points maps to an exact transform."
            });
            return Create(GeometryEditStrategyKind.ExactTransform, steps, warnings);
        }

        steps.Add(new StrategyResolutionStep
        {
            RuleName = "DerivedReconstructionFallback",
            Matched = true,
            Reason = appliesToAll
                ? "Derived operation cannot be represented by an exact transform."
                : "Point selector is not All, so the edit must reconstruct selected control points."
        });
        warnings.Add(new ObjectEditWarning
        {
            Code = "STRATEGY_FORCED_RECONSTRUCTION",
            Message = "The derived operation was routed to reconstruction."
        });

        return Create(GeometryEditStrategyKind.ReconstructFromControlPoints, steps, warnings);
    }

    public OperationResponse<StrategyResolutionResult> ResolveSurface(SurfaceEditSpec editSpec, EditableGeometryDescriptor descriptor)
    {
        var steps = new List<StrategyResolutionStep>();
        var warnings = new List<ObjectEditWarning>();

        if (descriptor.Kind != EditableGeometryKind.Surface || descriptor.SurfaceStructure is null)
        {
            return OperationResponse<StrategyResolutionResult>.Fail("EDITABLE_KIND_UNSUPPORTED");
        }

        if (editSpec.Operation.Kind == GeometryEditOperationKind.DirectOverride)
        {
            steps.Add(new StrategyResolutionStep
            {
                RuleName = "SurfaceDirectOverride",
                Matched = true,
                Reason = "Surface DirectOverride reconstructs from the provided control-point grid."
            });
            return Create(GeometryEditStrategyKind.ReconstructFromControlPoints, steps, warnings);
        }

        if (editSpec.Operation.Kind != GeometryEditOperationKind.DerivedOperation || !editSpec.Operation.DerivedKind.HasValue)
        {
            return OperationResponse<StrategyResolutionResult>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        SurfacePointSelectorKind selectorKind = editSpec.PointSelector?.Kind ?? SurfacePointSelectorKind.All;
        bool appliesToAll = selectorKind == SurfacePointSelectorKind.All;
        DerivedPointOperationKind derivedKind = editSpec.Operation.DerivedKind.Value;
        SurfaceEditDerivedOperationParameters parameters = editSpec.Operation.DerivedParameters ?? new SurfaceEditDerivedOperationParameters();

        if (appliesToAll && derivedKind == DerivedPointOperationKind.TranslateByVector)
        {
            steps.Add(new StrategyResolutionStep
            {
                RuleName = "SurfaceDerivedTranslateAll",
                Matched = true,
                Reason = "TranslateByVector applied to all surface points maps to an exact transform."
            });
            return Create(GeometryEditStrategyKind.ExactTransform, steps, warnings);
        }

        if (appliesToAll && derivedKind == DerivedPointOperationKind.ScaleAboutCentroid && IsUniformScale(parameters))
        {
            steps.Add(new StrategyResolutionStep
            {
                RuleName = "SurfaceDerivedUniformScaleAll",
                Matched = true,
                Reason = "Uniform ScaleAboutCentroid applied to all surface points maps to an exact transform."
            });
            return Create(GeometryEditStrategyKind.ExactTransform, steps, warnings);
        }

        steps.Add(new StrategyResolutionStep
        {
            RuleName = "SurfaceDerivedReconstructionFallback",
            Matched = true,
            Reason = appliesToAll
                ? "Surface derived operation cannot be represented by an exact transform."
                : "Surface point selector is not All, so the edit must reconstruct selected control points."
        });
        warnings.Add(new ObjectEditWarning
        {
            Code = "STRATEGY_FORCED_RECONSTRUCTION",
            Message = "The surface derived operation was routed to reconstruction."
        });

        return Create(GeometryEditStrategyKind.ReconstructFromControlPoints, steps, warnings);
    }

    private static OperationResponse<StrategyResolutionResult> Create(
        GeometryEditStrategyKind strategy,
        IReadOnlyList<StrategyResolutionStep> steps,
        IReadOnlyList<ObjectEditWarning> warnings)
    {
        return OperationResponse<StrategyResolutionResult>.Ok(new StrategyResolutionResult
        {
            Strategy = strategy,
            Trace = new StrategyResolutionTrace { Steps = steps },
            Warnings = warnings
        });
    }

    private static bool IsUniformScale(DerivedPointOperationParameters parameters)
    {
        return Math.Abs(parameters.ScaleX - parameters.ScaleY) <= 1e-12
            && Math.Abs(parameters.ScaleX - parameters.ScaleZ) <= 1e-12;
    }

    private static bool IsUniformScale(SurfaceEditDerivedOperationParameters parameters)
    {
        return Math.Abs(parameters.ScaleX - parameters.ScaleY) <= 1e-12
            && Math.Abs(parameters.ScaleX - parameters.ScaleZ) <= 1e-12;
    }
}
