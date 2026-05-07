extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Geometry.Edit;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Interval = rhinocommon::Rhino.Geometry.Interval;
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PlaneSurface = rhinocommon::Rhino.Geometry.PlaneSurface;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterGeometryEditSurfaceCompositeHandlers()
    {
        _extensionHandlers["geometry-edit-surface-composite-smoke-test"] = HandleGeometryEditSurfaceCompositeSmokeTest;
    }

    private bool HandleGeometryEditSurfaceCompositeSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- geometry-edit-surface-composite-smoke-test <3dm-file-path>");
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
                RunCliFallbackGeometryEditSurfaceCompositeSmoke(filePath);
            }
            else
            {
                RunLiveGeometryEditSurfaceCompositeSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Geometry edit surface composite smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackGeometryEditSurfaceCompositeSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        Guid objectId = Guid.NewGuid();
        SurfaceEditSpec spec = CreateSurfaceTranslateSpec(SurfacePointSelectorKind.All, 1d, 0d, 0d);
        var previewTool = new PreviewEditSurfaceGeometryTool(_surfaceEditOrchestrator);
        var applyTool = new ApplyEditSurfaceGeometryTool(_surfaceEditOrchestrator);

        RequireGeometryEditSurfaceCompositeFailureWithMessage(
            previewTool.PreviewEditSurfaceGeometry(filePath, objectId, spec),
            "LIVE_RHINO_REQUIRED",
            "PreviewEditSurfaceGeometry should require live Rhino.");
        checkpoints.Add("PreviewEditSurfaceGeometry rejected in CLI fallback");

        RequireGeometryEditSurfaceCompositeFailureWithMessage(
            applyTool.ApplyEditSurfaceGeometry(filePath, objectId, spec),
            "LIVE_RHINO_REQUIRED",
            "ApplyEditSurfaceGeometry should require live Rhino.");
        checkpoints.Add("ApplyEditSurfaceGeometry rejected in CLI fallback");

        Console.WriteLine("Geometry edit surface composite smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private void RunLiveGeometryEditSurfaceCompositeSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        var descriptorTool = new GetEditableGeometryDescriptorTool(_editableGeometryDescriptorService);
        var previewTool = new PreviewEditSurfaceGeometryTool(_surfaceEditOrchestrator);
        var applyTool = new ApplyEditSurfaceGeometryTool(_surfaceEditOrchestrator);

        GeometryEditSurfaceCompositeSnapshot before = CaptureGeometryEditSurfaceCompositeSnapshot(filePath);
        IReadOnlyList<Guid> surfaceIds = CreateGeometryEditSurfaceCompositeSmokeSurfaces(filePath);
        Guid planeId = surfaceIds[0];
        Guid nurbsId = surfaceIds[1];
        Guid brepId = surfaceIds[2];

        try
        {
            EditableGeometryDescriptor planeDescriptor = RequireGeometryEditSurfaceCompositeDescriptor(
                descriptorTool.GetEditableGeometryDescriptor(filePath, planeId, DescriptorDetail.Full),
                "plane surface descriptor");
            RequireGeometryEditSurfaceComposite(planeDescriptor.Kind == EditableGeometryKind.Surface, "Plane descriptor should be editable as a surface.");

            GeometryEditPreviewResponse flatPreview = RequireGeometryEditSurfaceCompositeSuccess(
                previewTool.PreviewEditSurfaceGeometry(
                    filePath,
                    planeId,
                    CreateDirectFlatSurfaceSpec(planeDescriptor, 0.15d),
                    GeometryEditStrategyKind.ReconstructFromControlPoints),
                "Plane flat DirectOverride preview");
            RequireGeometryEditSurfaceComposite(flatPreview.SurfaceControlPointGridSnapshot?.CountU == planeDescriptor.SurfaceStructure?.CountU, "Flat preview should return CountU.");
            RequireGeometryEditSurfaceComposite(flatPreview.SurfaceControlPointGridSnapshot?.CountV == planeDescriptor.SurfaceStructure?.CountV, "Flat preview should return CountV.");
            checkpoints.Add("Plane DirectOverride Flat preview ok");

            GeometryEditApplyResponse flatApply = RequireGeometryEditSurfaceCompositeSuccess(
                applyTool.ApplyEditSurfaceGeometry(
                    filePath,
                    planeId,
                    CreateDirectFlatSurfaceSpec(planeDescriptor, 0.15d),
                    GeometryEditStrategyKind.ReconstructFromControlPoints),
                "Plane flat DirectOverride apply");
            RequireGeometryEditSurfaceComposite(string.Equals(flatApply.UndoRecordName, "MCP:EditSurfaceGeometry", StringComparison.Ordinal), "Surface apply should report surface edit undo name.");
            checkpoints.Add("Plane DirectOverride Flat apply ok");

            EditableGeometryDescriptor nurbsDescriptor = RequireGeometryEditSurfaceCompositeDescriptor(
                descriptorTool.GetEditableGeometryDescriptor(filePath, nurbsId, DescriptorDetail.Full),
                "nurbs surface descriptor");
            GeometryEditApplyResponse gridApply = RequireGeometryEditSurfaceCompositeSuccess(
                applyTool.ApplyEditSurfaceGeometry(
                    filePath,
                    nurbsId,
                    CreateDirectGridSurfaceSpec(nurbsDescriptor, 0.2d),
                    GeometryEditStrategyKind.ReconstructFromControlPoints),
                "Nurbs Grid DirectOverride apply");
            RequireGeometryEditSurfaceComposite(gridApply.SurfaceControlPointGridSnapshot?.CountU == nurbsDescriptor.SurfaceStructure?.CountU, "Grid apply should return CountU.");
            checkpoints.Add("Nurbs DirectOverride Grid apply ok");

            GeometryEditPreviewResponse translateAllPreview = RequireGeometryEditSurfaceCompositeSuccess(
                previewTool.PreviewEditSurfaceGeometry(
                    filePath,
                    brepId,
                    CreateSurfaceTranslateSpec(SurfacePointSelectorKind.All, 0d, 0d, 0.5d),
                    GeometryEditStrategyKind.ExactTransform),
                "Brep Translate All preview");
            RequireGeometryEditSurfaceComposite(translateAllPreview.Strategy == GeometryEditStrategyKind.ExactTransform, "Translate All should route to ExactTransform.");
            RequireGeometryEditSurfaceComposite(HasGeometryEditSurfaceCompositeWarning(translateAllPreview.Warnings, "UNDERLYING_SURFACE_FALLBACK"), "Brep surface descriptor warning should be preserved.");
            checkpoints.Add("Brep Translate All -> ExactTransform preview ok");

            GeometryEditApplyResponse translateAllApply = RequireGeometryEditSurfaceCompositeSuccess(
                applyTool.ApplyEditSurfaceGeometry(
                    filePath,
                    brepId,
                    CreateSurfaceTranslateSpec(SurfacePointSelectorKind.All, 0d, 0d, 0.5d),
                    GeometryEditStrategyKind.ExactTransform),
                "Brep Translate All apply");
            RequireGeometryEditSurfaceComposite(translateAllApply.Strategy == GeometryEditStrategyKind.ExactTransform, "Translate All apply should route to ExactTransform.");
            RequireGeometryEditSurfaceComposite(IsGeometryEditSurfaceCompositeBrep(filePath, brepId), "ExactTransform should keep untrimmed single-face Brep as Brep.");
            checkpoints.Add("Brep Translate All -> ExactTransform apply ok");

            EditableGeometryDescriptor brepDescriptor = RequireGeometryEditSurfaceCompositeDescriptor(
                descriptorTool.GetEditableGeometryDescriptor(filePath, brepId, DescriptorDetail.Full),
                "brep surface descriptor");
            GeometryEditApplyResponse brepDirectApply = RequireGeometryEditSurfaceCompositeSuccess(
                applyTool.ApplyEditSurfaceGeometry(
                    filePath,
                    brepId,
                    CreateDirectFlatSurfaceSpec(brepDescriptor, 0.05d),
                    GeometryEditStrategyKind.ReconstructFromControlPoints),
                "Brep DirectOverride reconstruction apply");
            RequireGeometryEditSurfaceComposite(brepDirectApply.SurfaceControlPointGridSnapshot is not null, "Brep DirectOverride should return a surface grid snapshot.");
            RequireGeometryEditSurfaceComposite(IsGeometryEditSurfaceCompositeBrep(filePath, brepId), "Reconstruction should keep untrimmed single-face Brep as Brep.");
            checkpoints.Add("Brep DirectOverride reconstruction keeps Brep ok");

            EditableGeometryDescriptor postGridDescriptor = RequireGeometryEditSurfaceCompositeDescriptor(
                descriptorTool.GetEditableGeometryDescriptor(filePath, nurbsId, DescriptorDetail.Full),
                "post-grid nurbs descriptor");
            GeometryEditPreviewResponse offsetPreview = RequireGeometryEditSurfaceCompositeSuccess(
                previewTool.PreviewEditSurfaceGeometry(
                    filePath,
                    nurbsId,
                    CreateSurfaceOffsetUvRangeSpec(postGridDescriptor, 0.25d),
                    GeometryEditStrategyKind.ReconstructFromControlPoints),
                "Nurbs Offset UvRange preview");
            RequireGeometryEditSurfaceComposite(offsetPreview.Strategy == GeometryEditStrategyKind.ReconstructFromControlPoints, "Offset UvRange should route to reconstruction.");
            RequireGeometryEditSurfaceComposite(offsetPreview.DerivedOperationApplied?.ResolvedUvSamples.Count > 0, "Offset UvRange should report resolved UV samples.");
            checkpoints.Add("Offset UvRange -> reconstruction preview ok");

            GeometryEditApplyResponse offsetApply = RequireGeometryEditSurfaceCompositeSuccess(
                applyTool.ApplyEditSurfaceGeometry(
                    filePath,
                    nurbsId,
                    CreateSurfaceOffsetUvRangeSpec(postGridDescriptor, 0.1d),
                    GeometryEditStrategyKind.ReconstructFromControlPoints),
                "Nurbs Offset UvRange apply");
            RequireGeometryEditSurfaceComposite(offsetApply.SurfaceControlPointGridSnapshot is not null, "Offset apply should return a surface grid snapshot.");
            checkpoints.Add("Offset UvRange -> reconstruction apply ok");

            GeometryEditPreviewResponse edgePreview = RequireGeometryEditSurfaceCompositeSuccess(
                previewTool.PreviewEditSurfaceGeometry(
                    filePath,
                    nurbsId,
                    CreateSurfaceTranslateSpec(SurfacePointSelectorKind.EdgeOnly, 0d, 0.1d, 0d),
                    GeometryEditStrategyKind.ReconstructFromControlPoints),
                "Nurbs EdgeOnly translate preview");
            RequireGeometryEditSurfaceComposite(edgePreview.Strategy == GeometryEditStrategyKind.ReconstructFromControlPoints, "EdgeOnly translate should route to reconstruction.");
            RequireGeometryEditSurfaceComposite(HasGeometryEditSurfaceCompositeWarning(edgePreview.Warnings, "STRATEGY_FORCED_RECONSTRUCTION"), "EdgeOnly translate should warn about forced reconstruction.");
            checkpoints.Add("EdgeOnly translate -> reconstruction preview ok");

            RequireGeometryEditSurfaceCompositeFailure(
                previewTool.PreviewEditSurfaceGeometry(filePath, nurbsId, CreateSurfaceIndicesTranslateSpec(new[] { 999 }, 1d, 0d, 0d)),
                "POINT_SELECTOR_INVALID",
                "Out-of-range surface selector should fail.");
            checkpoints.Add("POINT_SELECTOR_INVALID guard ok");

            RequireGeometryEditSurfaceCompositeFailure(
                previewTool.PreviewEditSurfaceGeometry(filePath, nurbsId, CreateSurfaceScaleSpec(SurfacePointSelectorKind.All, 0d, 1d, 1d)),
                "SCALE_FACTOR_INVALID",
                "Zero scale factor should fail.");
            checkpoints.Add("SCALE_FACTOR_INVALID guard ok");

            RequireGeometryEditSurfaceCompositeFailure(
                previewTool.PreviewEditSurfaceGeometry(
                    filePath,
                    nurbsId,
                    CreateSurfaceTranslateSpec(SurfacePointSelectorKind.EdgeOnly, 0d, 0.1d, 0d),
                    GeometryEditStrategyKind.ExactTransform),
                "STRATEGY_EXPECTATION_MISMATCH",
                "Expected strategy mismatch should fail.");
            checkpoints.Add("STRATEGY_EXPECTATION_MISMATCH guard ok");

            EditableGeometryDescriptor afterApplyDescriptor = RequireGeometryEditSurfaceCompositeDescriptor(
                descriptorTool.GetEditableGeometryDescriptor(filePath, nurbsId, DescriptorDetail.Summary),
                "post-apply nurbs descriptor");
            RequireGeometryEditSurfaceComposite(afterApplyDescriptor.MetadataSummary.Name.StartsWith("mcp-surface-composite-", StringComparison.Ordinal), "Apply should preserve object name.");
            RequireGeometryEditSurfaceComposite(afterApplyDescriptor.MetadataSummary.UserStringCount > 0, "Apply should preserve object user strings.");
        }
        finally
        {
            DeleteGeometryEditSurfaceCompositeSmokeSurfaces(filePath, surfaceIds);
        }

        GeometryEditSurfaceCompositeSnapshot after = CaptureGeometryEditSurfaceCompositeSnapshot(filePath);
        RequireGeometryEditSurfaceComposite(after.ObjectCount == before.ObjectCount, "Smoke should not leave temporary objects.");
        RequireGeometryEditSurfaceComposite(after.DocumentStringCount == before.DocumentStringCount, "Smoke should not change document strings.");
        checkpoints.Add("Temporary objects cleaned up");

        Console.WriteLine("Geometry edit surface composite smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private GeometryEditSurfaceCompositeSnapshot CaptureGeometryEditSurfaceCompositeSnapshot(string filePath)
    {
        return RequireGeometryEditSurfaceCompositeSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                return OperationResponse<GeometryEditSurfaceCompositeSnapshot>.Ok(new GeometryEditSurfaceCompositeSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    DocumentStringCount = document.Strings.Count
                });
            }),
            "CaptureGeometryEditSurfaceCompositeSnapshot");
    }

    private IReadOnlyList<Guid> CreateGeometryEditSurfaceCompositeSmokeSurfaces(string filePath)
    {
        return RequireGeometryEditSurfaceCompositeSuccess(
            _liveRhinoDocumentAccessor.ExecuteWithUndo(filePath, "MCP:GeometryEditSurfaceCompositeSmokeSetup", document =>
            {
                var ids = new List<Guid>();
                var plane = new PlaneSurface(Plane.WorldXY, new Interval(0d, 2d), new Interval(0d, 2d));
                ids.Add(document.Objects.AddSurface(plane, CreateGeometryEditSurfaceCompositeAttributes("plane")));

                var nurbsSourcePlane = new PlaneSurface(
                    new Plane(new Point3d(3d, 0d, 0d), rhinocommon::Rhino.Geometry.Vector3d.ZAxis),
                    new Interval(0d, 2d),
                    new Interval(0d, 2d));
                NurbsSurface? nurbsSurface = nurbsSourcePlane.ToNurbsSurface();
                if (nurbsSurface is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to create smoke NurbsSurface.");
                }

                ids.Add(document.Objects.AddSurface(nurbsSurface, CreateGeometryEditSurfaceCompositeAttributes("nurbs")));

                var brepSurface = new PlaneSurface(
                    new Plane(new Point3d(6d, 0d, 0d), rhinocommon::Rhino.Geometry.Vector3d.ZAxis),
                    new Interval(0d, 2d),
                    new Interval(0d, 2d));
                Brep? brep = Brep.CreateFromSurface(brepSurface);
                if (brep is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to create smoke Brep.");
                }

                ids.Add(document.Objects.AddBrep(brep, CreateGeometryEditSurfaceCompositeAttributes("brep")));

                if (ids.Any(id => id == Guid.Empty))
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to add one or more smoke surfaces.");
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Ok((true, ids));
            }),
            "CreateGeometryEditSurfaceCompositeSmokeSurfaces");
    }

    private void DeleteGeometryEditSurfaceCompositeSmokeSurfaces(string filePath, IReadOnlyList<Guid> surfaceIds)
    {
        OperationResponse<bool> delete = _liveRhinoDocumentAccessor.ExecuteWithUndo(
            filePath,
            "MCP:GeometryEditSurfaceCompositeSmokeCleanup",
            document =>
            {
                bool mutated = false;
                foreach (Guid surfaceId in surfaceIds)
                {
                    RhinoObject? rhinoObject = document.Objects.FindId(surfaceId);
                    if (rhinoObject is not null && !rhinoObject.IsDeleted)
                    {
                        mutated |= document.Objects.Delete(surfaceId, true);
                    }
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, bool Result)>.Ok((mutated, true));
            });

        RequireGeometryEditSurfaceCompositeSuccess(delete, "DeleteGeometryEditSurfaceCompositeSmokeSurfaces");
    }

    private bool IsGeometryEditSurfaceCompositeBrep(string filePath, Guid objectId)
    {
        return RequireGeometryEditSurfaceCompositeSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                return OperationResponse<bool>.Ok(document.Objects.FindId(objectId)?.Geometry is Brep);
            }),
            "IsGeometryEditSurfaceCompositeBrep");
    }

    private static ObjectAttributes CreateGeometryEditSurfaceCompositeAttributes(string suffix)
    {
        var attributes = new ObjectAttributes
        {
            Name = $"mcp-surface-composite-{suffix}",
            ObjectColor = Color.FromArgb(255, 144, 88, 32),
            ColorSource = ObjectColorSource.ColorFromObject
        };
        attributes.SetUserString("mcp-smoke", "geometry-edit-surface-composite");
        return attributes;
    }

    private static SurfaceEditSpec CreateDirectFlatSurfaceSpec(EditableGeometryDescriptor descriptor, double zOffset)
    {
        return new SurfaceEditSpec
        {
            Operation = new SurfaceEditOperationSpec
            {
                Kind = GeometryEditOperationKind.DirectOverride
            },
            Grid = new EditableSurfacePointGrid
            {
                Layout = SurfacePointLayoutKind.Flat,
                Points = descriptor.Points
                    .Select(point => new EditablePointInput
                    {
                        Index = point.Index,
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z + zOffset
                    })
                    .ToList()
            }
        };
    }

    private static SurfaceEditSpec CreateDirectGridSurfaceSpec(EditableGeometryDescriptor descriptor, double zOffset)
    {
        if (descriptor.SurfaceStructure is null)
        {
            throw new InvalidOperationException("Surface descriptor is missing structure.");
        }

        var rows = new List<List<EditablePointInput>>();
        for (int u = 0; u < descriptor.SurfaceStructure.CountU; u++)
        {
            var row = new List<EditablePointInput>();
            for (int v = 0; v < descriptor.SurfaceStructure.CountV; v++)
            {
                EditablePointDescriptor point = descriptor.Points[(u * descriptor.SurfaceStructure.CountV) + v];
                row.Add(new EditablePointInput
                {
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z + zOffset
                });
            }

            rows.Add(row);
        }

        return new SurfaceEditSpec
        {
            Operation = new SurfaceEditOperationSpec
            {
                Kind = GeometryEditOperationKind.DirectOverride
            },
            Grid = new EditableSurfacePointGrid
            {
                Layout = SurfacePointLayoutKind.Grid,
                Rows = rows
            }
        };
    }

    private static SurfaceEditSpec CreateSurfaceTranslateSpec(SurfacePointSelectorKind selectorKind, double x, double y, double z)
    {
        return new SurfaceEditSpec
        {
            Operation = new SurfaceEditOperationSpec
            {
                Kind = GeometryEditOperationKind.DerivedOperation,
                DerivedKind = DerivedPointOperationKind.TranslateByVector,
                DerivedParameters = new SurfaceEditDerivedOperationParameters
                {
                    VectorX = x,
                    VectorY = y,
                    VectorZ = z
                }
            },
            PointSelector = new SurfacePointSelectorSpec { Kind = selectorKind }
        };
    }

    private static SurfaceEditSpec CreateSurfaceIndicesTranslateSpec(IReadOnlyList<int> indices, double x, double y, double z)
    {
        SurfaceEditSpec spec = CreateSurfaceTranslateSpec(SurfacePointSelectorKind.Indices, x, y, z);
        spec.PointSelector = new SurfacePointSelectorSpec
        {
            Kind = SurfacePointSelectorKind.Indices,
            Indices = indices.ToList()
        };
        return spec;
    }

    private static SurfaceEditSpec CreateSurfaceScaleSpec(SurfacePointSelectorKind selectorKind, double scaleX, double scaleY, double scaleZ)
    {
        return new SurfaceEditSpec
        {
            Operation = new SurfaceEditOperationSpec
            {
                Kind = GeometryEditOperationKind.DerivedOperation,
                DerivedKind = DerivedPointOperationKind.ScaleAboutCentroid,
                DerivedParameters = new SurfaceEditDerivedOperationParameters
                {
                    ScaleX = scaleX,
                    ScaleY = scaleY,
                    ScaleZ = scaleZ
                }
            },
            PointSelector = new SurfacePointSelectorSpec { Kind = selectorKind }
        };
    }

    private static SurfaceEditSpec CreateSurfaceOffsetUvRangeSpec(EditableGeometryDescriptor descriptor, double distance)
    {
        if (descriptor.SurfaceStructure is null)
        {
            throw new InvalidOperationException("Surface descriptor is missing structure.");
        }

        int uEnd = Math.Min(1, descriptor.SurfaceStructure.CountU - 1);
        int vEnd = Math.Min(1, descriptor.SurfaceStructure.CountV - 1);
        return new SurfaceEditSpec
        {
            Operation = new SurfaceEditOperationSpec
            {
                Kind = GeometryEditOperationKind.DerivedOperation,
                DerivedKind = DerivedPointOperationKind.OffsetAlongNormal,
                DerivedParameters = new SurfaceEditDerivedOperationParameters
                {
                    Distance = distance
                }
            },
            PointSelector = new SurfacePointSelectorSpec
            {
                Kind = SurfacePointSelectorKind.UvRange,
                UStart = 0,
                UEnd = uEnd,
                VStart = 0,
                VEnd = vEnd
            }
        };
    }

    private static EditableGeometryDescriptor RequireGeometryEditSurfaceCompositeDescriptor(
        OperationResponse<EditableGeometryDescriptorResponse> response,
        string operationName)
    {
        EditableGeometryDescriptorResponse data = RequireGeometryEditSurfaceCompositeSuccess(response, operationName);
        if (data.Descriptor is null)
        {
            throw new InvalidOperationException($"{operationName} returned no descriptor.");
        }

        return data.Descriptor;
    }

    private static T RequireGeometryEditSurfaceCompositeSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireGeometryEditSurfaceCompositeFailureWithMessage<T>(
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

    private static void RequireGeometryEditSurfaceCompositeFailure<T>(
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

    private static void RequireGeometryEditSurfaceComposite(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool HasGeometryEditSurfaceCompositeWarning(IEnumerable<ObjectEditWarning> warnings, string code)
    {
        return warnings.Any(warning => string.Equals(warning.Code, code, StringComparison.Ordinal));
    }

    private sealed class GeometryEditSurfaceCompositeSnapshot
    {
        public int ObjectCount { get; set; }
        public int DocumentStringCount { get; set; }
    }
}
