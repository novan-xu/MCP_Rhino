extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Geometry.Edit;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using LineCurve = rhinocommon::Rhino.Geometry.LineCurve;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterGeometryEditDerivedRoutingHandlers()
    {
        _extensionHandlers["geometry-edit-derived-routing-smoke-test"] = HandleGeometryEditDerivedRoutingSmokeTest;
    }

    private bool HandleGeometryEditDerivedRoutingSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- geometry-edit-derived-routing-smoke-test <3dm-file-path>");
            Environment.ExitCode = 1;
            return true;
        }

        try
        {
            string filePath = Path.GetFullPath(args[1]);
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"Smoke test source file was not found: {filePath}");
                Environment.ExitCode = 1;
                return true;
            }

            if (MCP_Rhino.Server.Infrastructure.Plugin.McpRhinoPlugin.Instance is null)
            {
                RunCliFallbackGeometryEditDerivedRoutingSmoke(filePath);
            }
            else
            {
                RunLiveGeometryEditDerivedRoutingSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Geometry edit derived routing smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackGeometryEditDerivedRoutingSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        Guid objectId = Guid.NewGuid();
        CurveEditSpec spec = CreateTranslateDerivedSpec(PointSelectorKind.All, 1d, 0d, 0d);
        var previewTool = new PreviewEditCurveGeometryTool(_curveEditOrchestrator);
        var applyTool = new ApplyEditCurveGeometryTool(_curveEditOrchestrator);

        RequireGeometryEditDerivedRoutingFailureWithMessage(
            previewTool.PreviewEditCurveGeometry(filePath, objectId, spec),
            "LIVE_RHINO_REQUIRED",
            "PreviewEditCurveGeometry derived route should require live Rhino.");
        checkpoints.Add("PreviewEditCurveGeometry derived route rejected in CLI fallback");

        RequireGeometryEditDerivedRoutingFailureWithMessage(
            applyTool.ApplyEditCurveGeometry(filePath, objectId, spec),
            "LIVE_RHINO_REQUIRED",
            "ApplyEditCurveGeometry derived route should require live Rhino.");
        checkpoints.Add("ApplyEditCurveGeometry derived route rejected in CLI fallback");

        Console.WriteLine("Geometry edit derived routing smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private void RunLiveGeometryEditDerivedRoutingSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        var descriptorTool = new GetEditableGeometryDescriptorTool(_editableGeometryDescriptorService);
        var previewTool = new PreviewEditCurveGeometryTool(_curveEditOrchestrator);
        var applyTool = new ApplyEditCurveGeometryTool(_curveEditOrchestrator);

        GeometryEditDerivedRoutingSnapshot before = CaptureGeometryEditDerivedRoutingSnapshot(filePath);
        IReadOnlyList<Guid> curveIds = CreateGeometryEditDerivedRoutingSmokeCurves(filePath);
        Guid nurbsId = curveIds[0];
        Guid lineId = curveIds[1];

        try
        {
            EditableGeometryDescriptor nurbsDescriptor = RequireGeometryEditDerivedRoutingDescriptor(
                descriptorTool.GetEditableGeometryDescriptor(filePath, nurbsId),
                "nurbs descriptor");

            GeometryEditPreviewResponse translateAllPreview = RequireGeometryEditDerivedRoutingSuccess(
                previewTool.PreviewEditCurveGeometry(
                    filePath,
                    nurbsId,
                    CreateTranslateDerivedSpec(PointSelectorKind.All, 1d, 0d, 0d),
                    GeometryEditStrategyKind.ExactTransform),
                "Translate All preview");
            RequireGeometryEditDerivedRouting(translateAllPreview.Strategy == GeometryEditStrategyKind.ExactTransform, "Translate All should route to ExactTransform.");
            RequireGeometryEditDerivedRouting(translateAllPreview.ReconstructedCurveSummary is null, "ExactTransform preview should not return a reconstructed curve summary.");
            checkpoints.Add("Translate All -> ExactTransform preview ok");

            GeometryEditApplyResponse translateAllApply = RequireGeometryEditDerivedRoutingSuccess(
                applyTool.ApplyEditCurveGeometry(
                    filePath,
                    nurbsId,
                    CreateTranslateDerivedSpec(PointSelectorKind.All, 0d, 1d, 0d),
                    GeometryEditStrategyKind.ExactTransform),
                "Translate All apply");
            RequireGeometryEditDerivedRouting(translateAllApply.Strategy == GeometryEditStrategyKind.ExactTransform, "Translate All apply should route to ExactTransform.");
            RequireGeometryEditDerivedRouting(string.Equals(translateAllApply.UndoRecordName, "MCP:EditCurveGeometry", StringComparison.Ordinal), "ExactTransform apply should report the curve edit undo name.");
            checkpoints.Add("Translate All -> ExactTransform apply ok");

            GeometryEditPreviewResponse uniformScalePreview = RequireGeometryEditDerivedRoutingSuccess(
                previewTool.PreviewEditCurveGeometry(
                    filePath,
                    nurbsId,
                    CreateScaleDerivedSpec(PointSelectorKind.All, 1.1d, 1.1d, 1.1d),
                    GeometryEditStrategyKind.ExactTransform),
                "Uniform scale preview");
            RequireGeometryEditDerivedRouting(uniformScalePreview.Strategy == GeometryEditStrategyKind.ExactTransform, "Uniform Scale All should route to ExactTransform.");
            checkpoints.Add("Uniform scale All -> ExactTransform ok");

            GeometryEditPreviewResponse nonUniformScalePreview = RequireGeometryEditDerivedRoutingSuccess(
                previewTool.PreviewEditCurveGeometry(
                    filePath,
                    nurbsId,
                    CreateScaleDerivedSpec(PointSelectorKind.All, 1.2d, 1d, 1d)),
                "Non-uniform scale preview");
            RequireGeometryEditDerivedRouting(nonUniformScalePreview.Strategy == GeometryEditStrategyKind.ReconstructFromControlPoints, "Non-uniform scale should route to reconstruction.");
            RequireGeometryEditDerivedRouting(HasWarning(nonUniformScalePreview.Warnings, "STRATEGY_FORCED_RECONSTRUCTION"), "Non-uniform scale should warn about reconstruction routing.");
            checkpoints.Add("Non-uniform scale -> reconstruction ok");

            GeometryEditPreviewResponse translateIndicesPreview = RequireGeometryEditDerivedRoutingSuccess(
                previewTool.PreviewEditCurveGeometry(
                    filePath,
                    nurbsId,
                    CreateTranslateIndicesSpec(new[] { 1, 2 }, 0d, 0d, 1d)),
                "Translate indices preview");
            RequireGeometryEditDerivedRouting(translateIndicesPreview.Strategy == GeometryEditStrategyKind.ReconstructFromControlPoints, "Translate Indices should route to reconstruction.");
            RequireGeometryEditDerivedRouting(translateIndicesPreview.ResolvedPointIndices.SequenceEqual(new[] { 1, 2 }), "Translate Indices should report resolved point indices.");
            checkpoints.Add("Translate Indices -> reconstruction ok");

            GeometryEditPreviewResponse offsetPreview = RequireGeometryEditDerivedRoutingSuccess(
                previewTool.PreviewEditCurveGeometry(
                    filePath,
                    nurbsId,
                    CreateOffsetDerivedSpec(PointSelectorKind.EndpointsOnly, 0.25d)),
                "Offset endpoints preview");
            RequireGeometryEditDerivedRouting(offsetPreview.Strategy == GeometryEditStrategyKind.ReconstructFromControlPoints, "Offset endpoints should route to reconstruction.");
            RequireGeometryEditDerivedRouting(offsetPreview.DerivedOperationApplied?.PerPointOffsets.Count == 2, "Offset endpoints should report two per-point offsets.");
            checkpoints.Add("Offset EndpointsOnly -> reconstruction ok");

            RequireGeometryEditDerivedRoutingFailure(
                previewTool.PreviewEditCurveGeometry(filePath, nurbsId, CreateTranslateIndicesSpec(new[] { 999 }, 1d, 0d, 0d)),
                "POINT_SELECTOR_INVALID",
                "Out-of-range selector should fail.");
            checkpoints.Add("POINT_SELECTOR_INVALID guard ok");

            RequireGeometryEditDerivedRoutingFailure(
                previewTool.PreviewEditCurveGeometry(filePath, nurbsId, CreateTranslateDerivedSpec(PointSelectorKind.All, 0d, 0d, 0d)),
                "TRANSLATE_VECTOR_ZERO",
                "Zero translate vector should fail.");
            checkpoints.Add("TRANSLATE_VECTOR_ZERO guard ok");

            RequireGeometryEditDerivedRoutingFailure(
                previewTool.PreviewEditCurveGeometry(filePath, nurbsId, CreateScaleDerivedSpec(PointSelectorKind.All, 0d, 1d, 1d)),
                "SCALE_FACTOR_INVALID",
                "Zero scale factor should fail.");
            checkpoints.Add("SCALE_FACTOR_INVALID guard ok");

            RequireGeometryEditDerivedRoutingFailure(
                previewTool.PreviewEditCurveGeometry(filePath, lineId, CreateOffsetDerivedSpec(PointSelectorKind.EndpointsOnly, 0.25d)),
                "OFFSET_ALONG_NORMAL_REQUIRES_CURVE_WITH_FRAME",
                "Line offset along normal should fail.");
            checkpoints.Add("OFFSET_ALONG_NORMAL_REQUIRES_CURVE_WITH_FRAME guard ok");

            RequireGeometryEditDerivedRoutingFailure(
                previewTool.PreviewEditCurveGeometry(
                    filePath,
                    nurbsId,
                    CreateTranslateIndicesSpec(new[] { 0 }, 1d, 0d, 0d),
                    GeometryEditStrategyKind.ExactTransform),
                "STRATEGY_EXPECTATION_MISMATCH",
                "Expected strategy mismatch should fail.");
            checkpoints.Add("STRATEGY_EXPECTATION_MISMATCH guard ok");

            EditableGeometryDescriptor afterApplyDescriptor = RequireGeometryEditDerivedRoutingDescriptor(
                descriptorTool.GetEditableGeometryDescriptor(filePath, nurbsId),
                "post-apply descriptor");
            RequireGeometryEditDerivedRouting(afterApplyDescriptor.MetadataSummary.Name.StartsWith("mcp-derived-routing-", StringComparison.Ordinal), "Apply should preserve metadata name.");
            RequireGeometryEditDerivedRouting(afterApplyDescriptor.MetadataSummary.UserStringCount > 0, "Apply should preserve user strings.");
        }
        finally
        {
            DeleteGeometryEditDerivedRoutingSmokeCurves(filePath, curveIds);
        }

        GeometryEditDerivedRoutingSnapshot after = CaptureGeometryEditDerivedRoutingSnapshot(filePath);
        RequireGeometryEditDerivedRouting(after.ObjectCount == before.ObjectCount, "Smoke should not leave temporary objects.");
        RequireGeometryEditDerivedRouting(after.DocumentStringCount == before.DocumentStringCount, "Smoke should not change document strings.");
        checkpoints.Add("Temporary objects cleaned up");

        Console.WriteLine("Geometry edit derived routing smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private GeometryEditDerivedRoutingSnapshot CaptureGeometryEditDerivedRoutingSnapshot(string filePath)
    {
        return RequireGeometryEditDerivedRoutingSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                return OperationResponse<GeometryEditDerivedRoutingSnapshot>.Ok(new GeometryEditDerivedRoutingSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    DocumentStringCount = document.Strings.Count
                });
            }),
            "CaptureGeometryEditDerivedRoutingSnapshot");
    }

    private IReadOnlyList<Guid> CreateGeometryEditDerivedRoutingSmokeCurves(string filePath)
    {
        return RequireGeometryEditDerivedRoutingSuccess(
            _liveRhinoDocumentAccessor.ExecuteWithUndo(filePath, "MCP:GeometryEditDerivedRoutingSmokeSetup", document =>
            {
                Curve? nurbsCurve = Curve.CreateControlPointCurve(new[]
                {
                    new Point3d(0d, 0d, 0d),
                    new Point3d(1d, 1.5d, 0d),
                    new Point3d(3d, -1d, 0d),
                    new Point3d(4d, 0d, 0d)
                }, 3);
                if (nurbsCurve is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to create derived routing NurbsCurve.");
                }

                var ids = new List<Guid>
                {
                    document.Objects.AddCurve(nurbsCurve, CreateGeometryEditDerivedRoutingAttributes("nurbs")),
                    document.Objects.AddCurve(new LineCurve(new Point3d(0d, 3d, 0d), new Point3d(4d, 3d, 0d)), CreateGeometryEditDerivedRoutingAttributes("line"))
                };

                if (ids.Any(id => id == Guid.Empty))
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to add derived routing smoke curves.");
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Ok((true, ids));
            }),
            "CreateGeometryEditDerivedRoutingSmokeCurves");
    }

    private void DeleteGeometryEditDerivedRoutingSmokeCurves(string filePath, IReadOnlyList<Guid> curveIds)
    {
        OperationResponse<bool> delete = _liveRhinoDocumentAccessor.ExecuteWithUndo(
            filePath,
            "MCP:GeometryEditDerivedRoutingSmokeCleanup",
            document =>
            {
                bool mutated = false;
                foreach (Guid curveId in curveIds)
                {
                    RhinoObject? rhinoObject = document.Objects.FindId(curveId);
                    if (rhinoObject is not null && !rhinoObject.IsDeleted)
                    {
                        mutated |= document.Objects.Delete(curveId, true);
                    }
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, bool Result)>.Ok((mutated, true));
            });

        RequireGeometryEditDerivedRoutingSuccess(delete, "DeleteGeometryEditDerivedRoutingSmokeCurves");
    }

    private static ObjectAttributes CreateGeometryEditDerivedRoutingAttributes(string suffix)
    {
        var attributes = new ObjectAttributes
        {
            Name = $"mcp-derived-routing-{suffix}",
            ObjectColor = Color.FromArgb(255, 80, 128, 40),
            ColorSource = ObjectColorSource.ColorFromObject
        };
        attributes.SetUserString("mcp-smoke", "geometry-edit-derived-routing");
        return attributes;
    }

    private static CurveEditSpec CreateTranslateDerivedSpec(PointSelectorKind selectorKind, double x, double y, double z)
    {
        return new CurveEditSpec
        {
            Operation = new GeometryEditOperationSpec
            {
                Kind = GeometryEditOperationKind.DerivedOperation,
                DerivedKind = DerivedPointOperationKind.TranslateByVector,
                DerivedParameters = new DerivedPointOperationParameters
                {
                    VectorX = x,
                    VectorY = y,
                    VectorZ = z
                }
            },
            PointSelector = new PointSelectorSpec { Kind = selectorKind }
        };
    }

    private static CurveEditSpec CreateTranslateIndicesSpec(IReadOnlyList<int> indices, double x, double y, double z)
    {
        CurveEditSpec spec = CreateTranslateDerivedSpec(PointSelectorKind.Indices, x, y, z);
        spec.PointSelector = new PointSelectorSpec
        {
            Kind = PointSelectorKind.Indices,
            Indices = indices.ToList()
        };
        return spec;
    }

    private static CurveEditSpec CreateScaleDerivedSpec(PointSelectorKind selectorKind, double scaleX, double scaleY, double scaleZ)
    {
        return new CurveEditSpec
        {
            Operation = new GeometryEditOperationSpec
            {
                Kind = GeometryEditOperationKind.DerivedOperation,
                DerivedKind = DerivedPointOperationKind.ScaleAboutCentroid,
                DerivedParameters = new DerivedPointOperationParameters
                {
                    ScaleX = scaleX,
                    ScaleY = scaleY,
                    ScaleZ = scaleZ
                }
            },
            PointSelector = new PointSelectorSpec { Kind = selectorKind }
        };
    }

    private static CurveEditSpec CreateOffsetDerivedSpec(PointSelectorKind selectorKind, double distance)
    {
        return new CurveEditSpec
        {
            Operation = new GeometryEditOperationSpec
            {
                Kind = GeometryEditOperationKind.DerivedOperation,
                DerivedKind = DerivedPointOperationKind.OffsetAlongNormal,
                DerivedParameters = new DerivedPointOperationParameters
                {
                    Distance = distance
                }
            },
            PointSelector = new PointSelectorSpec { Kind = selectorKind }
        };
    }

    private static EditableGeometryDescriptor RequireGeometryEditDerivedRoutingDescriptor(
        OperationResponse<EditableGeometryDescriptorResponse> response,
        string operationName)
    {
        EditableGeometryDescriptorResponse data = RequireGeometryEditDerivedRoutingSuccess(response, operationName);
        if (data.Descriptor is null)
        {
            throw new InvalidOperationException($"{operationName} returned no descriptor.");
        }

        return data.Descriptor;
    }

    private static T RequireGeometryEditDerivedRoutingSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireGeometryEditDerivedRoutingFailureWithMessage<T>(
        OperationResponse<T> response,
        string expectedMessage,
        string message)
    {
        if (response.Success)
        {
            throw new InvalidOperationException($"{message} Expected failure but got success.");
        }

        if (!string.Equals(response.Message, expectedMessage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Expected [{expectedMessage}], actual [{response.Message}].");
        }
    }

    private static void RequireGeometryEditDerivedRoutingFailure<T>(
        OperationResponse<T> response,
        string expectedMessageFragment,
        string message)
    {
        if (response.Success)
        {
            throw new InvalidOperationException($"{message} Expected failure but got success.");
        }

        if (!response.Message.Contains(expectedMessageFragment, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Expected message containing [{expectedMessageFragment}], actual [{response.Message}].");
        }
    }

    private static void RequireGeometryEditDerivedRouting(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool HasWarning(IEnumerable<ObjectEditWarning> warnings, string code)
    {
        return warnings.Any(warning => string.Equals(warning.Code, code, StringComparison.Ordinal));
    }

    private sealed class GeometryEditDerivedRoutingSnapshot
    {
        public int ObjectCount { get; set; }
        public int DocumentStringCount { get; set; }
    }
}
