using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveGeometryEditValidator : IGeometryEditValidator
{
    public OperationResponse ValidateRequest(Guid objectId, CurveEditSpec editSpec)
    {
        if (objectId == Guid.Empty)
        {
            return OperationResponse.Fail("ObjectId cannot be empty.");
        }

        if (editSpec.Operation.Kind == GeometryEditOperationKind.DirectOverride)
        {
            if (editSpec.Points.Count == 0)
            {
                return OperationResponse.Fail("EDIT_POINT_COUNT_MISMATCH");
            }

            foreach (EditablePointInput point in editSpec.Points)
            {
                if (!IsFinite(point.X) || !IsFinite(point.Y) || !IsFinite(point.Z))
                {
                    return OperationResponse.Fail("EDIT_POINT_INVALID");
                }
            }

            return OperationResponse.Ok();
        }

        if (editSpec.Operation.Kind != GeometryEditOperationKind.DerivedOperation)
        {
            return OperationResponse.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        if (!editSpec.Operation.DerivedKind.HasValue)
        {
            return OperationResponse.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        DerivedPointOperationParameters parameters = editSpec.Operation.DerivedParameters ?? new DerivedPointOperationParameters();
        return editSpec.Operation.DerivedKind.Value switch
        {
            DerivedPointOperationKind.ScaleAboutCentroid => ValidateScale(parameters),
            DerivedPointOperationKind.TranslateByVector => ValidateTranslate(parameters),
            DerivedPointOperationKind.OffsetAlongNormal => IsFinite(parameters.Distance)
                ? OperationResponse.Ok()
                : OperationResponse.Fail("EDIT_POINT_INVALID"),
            _ => OperationResponse.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE")
        };
    }

    public OperationResponse ValidatePointInputs(CurveEditSpec editSpec, EditableGeometryDescriptor descriptor)
    {
        if (descriptor.Kind != EditableGeometryKind.Curve)
        {
            return OperationResponse.Fail("EDITABLE_KIND_UNSUPPORTED");
        }

        return editSpec.Operation.Kind switch
        {
            GeometryEditOperationKind.DirectOverride => ValidateDirectOverridePointInputs(editSpec, descriptor),
            GeometryEditOperationKind.DerivedOperation => ValidateDerivedOperation(editSpec, descriptor),
            _ => OperationResponse.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE")
        };
    }

    public OperationResponse ValidateSurfaceRequest(Guid objectId, SurfaceEditSpec editSpec)
    {
        if (objectId == Guid.Empty)
        {
            return OperationResponse.Fail("ObjectId cannot be empty.");
        }

        if (editSpec.Operation.Kind == GeometryEditOperationKind.DirectOverride)
        {
            if (editSpec.Grid is null)
            {
                return OperationResponse.Fail("EDIT_POINT_LAYOUT_MISMATCH");
            }

            return ValidateSurfaceGridShapeAndFinite(editSpec.Grid);
        }

        if (editSpec.Operation.Kind != GeometryEditOperationKind.DerivedOperation)
        {
            return OperationResponse.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        if (!editSpec.Operation.DerivedKind.HasValue)
        {
            return OperationResponse.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        SurfaceEditDerivedOperationParameters parameters = editSpec.Operation.DerivedParameters ?? new SurfaceEditDerivedOperationParameters();
        return editSpec.Operation.DerivedKind.Value switch
        {
            DerivedPointOperationKind.ScaleAboutCentroid => ValidateSurfaceScale(parameters),
            DerivedPointOperationKind.TranslateByVector => ValidateSurfaceTranslate(parameters),
            DerivedPointOperationKind.OffsetAlongNormal => IsFinite(parameters.Distance)
                ? OperationResponse.Ok()
                : OperationResponse.Fail("EDIT_POINT_INVALID"),
            _ => OperationResponse.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE")
        };
    }

    public OperationResponse ValidateSurfacePointInputs(SurfaceEditSpec editSpec, EditableGeometryDescriptor descriptor)
    {
        if (descriptor.Kind != EditableGeometryKind.Surface || descriptor.SurfaceStructure is null)
        {
            return OperationResponse.Fail("EDITABLE_KIND_UNSUPPORTED");
        }

        return editSpec.Operation.Kind switch
        {
            GeometryEditOperationKind.DirectOverride => ValidateSurfaceDirectOverridePointInputs(editSpec, descriptor),
            GeometryEditOperationKind.DerivedOperation => ValidateSurfaceDerivedOperation(editSpec, descriptor),
            _ => OperationResponse.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE")
        };
    }

    private static OperationResponse ValidateDirectOverridePointInputs(CurveEditSpec editSpec, EditableGeometryDescriptor descriptor)
    {
        if (editSpec.Points.Count != descriptor.ControlPointCount)
        {
            return OperationResponse.Fail("EDIT_POINT_COUNT_MISMATCH");
        }

        var seen = new HashSet<int>();
        for (int i = 0; i < editSpec.Points.Count; i++)
        {
            int index = editSpec.Points[i].Index;
            if (index != i || index < 0 || index >= descriptor.ControlPointCount || !seen.Add(index))
            {
                return OperationResponse.Fail("EDIT_POINT_INDEX_MISMATCH");
            }
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateDerivedOperation(CurveEditSpec editSpec, EditableGeometryDescriptor descriptor)
    {
        OperationResponse selector = ValidateSelector(editSpec.PointSelector, descriptor.ControlPointCount);
        if (!selector.Success)
        {
            return selector;
        }

        if (editSpec.Operation.DerivedKind == DerivedPointOperationKind.OffsetAlongNormal
            && string.Equals(descriptor.CurveStructure?.CurveKind, "LineCurve", StringComparison.Ordinal))
        {
            return OperationResponse.Fail("OFFSET_ALONG_NORMAL_REQUIRES_CURVE_WITH_FRAME");
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateSurfaceDirectOverridePointInputs(SurfaceEditSpec editSpec, EditableGeometryDescriptor descriptor)
    {
        if (editSpec.Grid is null || descriptor.SurfaceStructure is null)
        {
            return OperationResponse.Fail("EDIT_POINT_LAYOUT_MISMATCH");
        }

        int countU = descriptor.SurfaceStructure.CountU;
        int countV = descriptor.SurfaceStructure.CountV;
        int pointCount = countU * countV;
        return editSpec.Grid.Layout switch
        {
            SurfacePointLayoutKind.Flat => ValidateSurfaceFlatGrid(editSpec.Grid, pointCount),
            SurfacePointLayoutKind.Grid => ValidateSurfaceRowsGrid(editSpec.Grid, countU, countV),
            _ => OperationResponse.Fail("EDIT_POINT_LAYOUT_MISMATCH")
        };
    }

    private static OperationResponse ValidateSurfaceDerivedOperation(SurfaceEditSpec editSpec, EditableGeometryDescriptor descriptor)
    {
        if (descriptor.SurfaceStructure is null)
        {
            return OperationResponse.Fail("EDITABLE_KIND_UNSUPPORTED");
        }

        return ValidateSurfaceSelector(
            editSpec.PointSelector,
            descriptor.SurfaceStructure.CountU,
            descriptor.SurfaceStructure.CountV);
    }

    private static OperationResponse ValidateSurfaceGridShapeAndFinite(EditableSurfacePointGrid grid)
    {
        if (grid.Layout == SurfacePointLayoutKind.Flat)
        {
            if (grid.Points.Count == 0 || grid.Rows.Count > 0)
            {
                return OperationResponse.Fail("EDIT_POINT_LAYOUT_MISMATCH");
            }

            return ValidateSurfaceFinitePoints(grid.Points);
        }

        if (grid.Layout == SurfacePointLayoutKind.Grid)
        {
            if (grid.Rows.Count == 0 || grid.Points.Count > 0)
            {
                return OperationResponse.Fail("EDIT_POINT_LAYOUT_MISMATCH");
            }

            foreach (IReadOnlyList<EditablePointInput> row in grid.Rows)
            {
                OperationResponse finite = ValidateSurfaceFinitePoints(row);
                if (!finite.Success)
                {
                    return finite;
                }
            }

            return OperationResponse.Ok();
        }

        return OperationResponse.Fail("EDIT_POINT_LAYOUT_MISMATCH");
    }

    private static OperationResponse ValidateSurfaceFinitePoints(IReadOnlyList<EditablePointInput> points)
    {
        foreach (EditablePointInput point in points)
        {
            if (!IsFinite(point.X) || !IsFinite(point.Y) || !IsFinite(point.Z))
            {
                return OperationResponse.Fail("EDIT_POINT_INVALID");
            }
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateSurfaceFlatGrid(EditableSurfacePointGrid grid, int pointCount)
    {
        if (grid.Rows.Count > 0)
        {
            return OperationResponse.Fail("EDIT_POINT_LAYOUT_MISMATCH");
        }

        if (grid.Points.Count != pointCount)
        {
            return OperationResponse.Fail("EDIT_POINT_COUNT_MISMATCH");
        }

        var seen = new HashSet<int>();
        for (int i = 0; i < grid.Points.Count; i++)
        {
            int index = grid.Points[i].Index;
            if (index != i || index < 0 || index >= pointCount || !seen.Add(index))
            {
                return OperationResponse.Fail("EDIT_POINT_INDEX_MISMATCH");
            }
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateSurfaceRowsGrid(EditableSurfacePointGrid grid, int countU, int countV)
    {
        if (grid.Points.Count > 0)
        {
            return OperationResponse.Fail("EDIT_POINT_LAYOUT_MISMATCH");
        }

        if (grid.Rows.Count != countU)
        {
            return OperationResponse.Fail("EDIT_POINT_COUNT_MISMATCH");
        }

        foreach (IReadOnlyList<EditablePointInput> row in grid.Rows)
        {
            if (row.Count != countV)
            {
                return OperationResponse.Fail("EDIT_POINT_COUNT_MISMATCH");
            }
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateSelector(PointSelectorSpec? selector, int pointCount)
    {
        PointSelectorSpec resolved = selector ?? new PointSelectorSpec { Kind = PointSelectorKind.All };
        return resolved.Kind switch
        {
            PointSelectorKind.All => OperationResponse.Ok(),
            PointSelectorKind.EndpointsOnly => pointCount >= 2
                ? OperationResponse.Ok()
                : OperationResponse.Fail("POINT_SELECTOR_INVALID"),
            PointSelectorKind.Indices => ValidateIndexList(resolved.Indices, pointCount),
            PointSelectorKind.Range => ValidateRange(resolved.Start, resolved.End, pointCount),
            _ => OperationResponse.Fail("POINT_SELECTOR_INVALID")
        };
    }

    private static OperationResponse ValidateIndexList(IReadOnlyList<int> indices, int pointCount)
    {
        if (indices.Count == 0)
        {
            return OperationResponse.Fail("POINT_SELECTOR_INVALID");
        }

        var seen = new HashSet<int>();
        foreach (int index in indices)
        {
            if (index < 0 || index >= pointCount || !seen.Add(index))
            {
                return OperationResponse.Fail("POINT_SELECTOR_INVALID");
            }
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateSurfaceSelector(SurfacePointSelectorSpec? selector, int countU, int countV)
    {
        SurfacePointSelectorSpec resolved = selector ?? new SurfacePointSelectorSpec { Kind = SurfacePointSelectorKind.All };
        int pointCount = countU * countV;
        return resolved.Kind switch
        {
            SurfacePointSelectorKind.All => OperationResponse.Ok(),
            SurfacePointSelectorKind.EdgeOnly => countU > 0 && countV > 0
                ? OperationResponse.Ok()
                : OperationResponse.Fail("POINT_SELECTOR_INVALID"),
            SurfacePointSelectorKind.Indices => ValidateIndexList(resolved.Indices, pointCount),
            SurfacePointSelectorKind.UvRange => ValidateUvRange(resolved, countU, countV),
            _ => OperationResponse.Fail("POINT_SELECTOR_INVALID")
        };
    }

    private static OperationResponse ValidateRange(int? start, int? end, int pointCount)
    {
        if (!start.HasValue || !end.HasValue || start.Value < 0 || end.Value < start.Value || end.Value >= pointCount)
        {
            return OperationResponse.Fail("POINT_SELECTOR_INVALID");
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateUvRange(SurfacePointSelectorSpec selector, int countU, int countV)
    {
        if (!selector.UStart.HasValue
            || !selector.UEnd.HasValue
            || !selector.VStart.HasValue
            || !selector.VEnd.HasValue
            || selector.UStart.Value < 0
            || selector.VStart.Value < 0
            || selector.UEnd.Value < selector.UStart.Value
            || selector.VEnd.Value < selector.VStart.Value
            || selector.UEnd.Value >= countU
            || selector.VEnd.Value >= countV)
        {
            return OperationResponse.Fail("POINT_SELECTOR_INVALID");
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateScale(DerivedPointOperationParameters parameters)
    {
        if (!IsFinite(parameters.ScaleX) || !IsFinite(parameters.ScaleY) || !IsFinite(parameters.ScaleZ)
            || parameters.ScaleX <= 0d || parameters.ScaleY <= 0d || parameters.ScaleZ <= 0d
            || (IsOne(parameters.ScaleX) && IsOne(parameters.ScaleY) && IsOne(parameters.ScaleZ)))
        {
            return OperationResponse.Fail("SCALE_FACTOR_INVALID");
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateTranslate(DerivedPointOperationParameters parameters)
    {
        if (!IsFinite(parameters.VectorX) || !IsFinite(parameters.VectorY) || !IsFinite(parameters.VectorZ))
        {
            return OperationResponse.Fail("EDIT_POINT_INVALID");
        }

        if (IsZero(parameters.VectorX) && IsZero(parameters.VectorY) && IsZero(parameters.VectorZ))
        {
            return OperationResponse.Fail("TRANSLATE_VECTOR_ZERO");
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateSurfaceScale(SurfaceEditDerivedOperationParameters parameters)
    {
        if (!IsFinite(parameters.ScaleX) || !IsFinite(parameters.ScaleY) || !IsFinite(parameters.ScaleZ)
            || parameters.ScaleX <= 0d || parameters.ScaleY <= 0d || parameters.ScaleZ <= 0d
            || (IsOne(parameters.ScaleX) && IsOne(parameters.ScaleY) && IsOne(parameters.ScaleZ)))
        {
            return OperationResponse.Fail("SCALE_FACTOR_INVALID");
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateSurfaceTranslate(SurfaceEditDerivedOperationParameters parameters)
    {
        if (!IsFinite(parameters.VectorX) || !IsFinite(parameters.VectorY) || !IsFinite(parameters.VectorZ))
        {
            return OperationResponse.Fail("EDIT_POINT_INVALID");
        }

        if (IsZero(parameters.VectorX) && IsZero(parameters.VectorY) && IsZero(parameters.VectorZ))
        {
            return OperationResponse.Fail("TRANSLATE_VECTOR_ZERO");
        }

        return OperationResponse.Ok();
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool IsZero(double value)
    {
        return Math.Abs(value) <= 1e-12;
    }

    private static bool IsOne(double value)
    {
        return Math.Abs(value - 1d) <= 1e-12;
    }
}
