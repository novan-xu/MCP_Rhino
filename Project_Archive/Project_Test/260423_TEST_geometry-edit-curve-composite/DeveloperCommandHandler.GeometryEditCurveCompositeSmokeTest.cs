extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Geometry.Edit;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using LineCurve = rhinocommon::Rhino.Geometry.LineCurve;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using PolylineCurve = rhinocommon::Rhino.Geometry.PolylineCurve;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterGeometryEditCurveCompositeHandlers()
    {
        _extensionHandlers["geometry-edit-curve-composite-smoke-test"] = HandleGeometryEditCurveCompositeSmokeTest;
    }

    private bool HandleGeometryEditCurveCompositeSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- geometry-edit-curve-composite-smoke-test <3dm-file-path>");
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
                RunCliFallbackGeometryEditCurveCompositeSmoke(filePath);
            }
            else
            {
                RunLiveGeometryEditCurveCompositeSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Geometry edit curve composite smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackGeometryEditCurveCompositeSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        Guid objectId = Guid.NewGuid();
        CurveEditSpec editSpec = CreateTwoPointCurveEditSpec();

        var previewTool = new PreviewEditCurveGeometryTool(_curveEditOrchestrator);
        var applyTool = new ApplyEditCurveGeometryTool(_curveEditOrchestrator);

        RequireGeometryEditCurveCompositeFailureWithMessage(
            previewTool.PreviewEditCurveGeometry(filePath, objectId, editSpec),
            "LIVE_RHINO_REQUIRED",
            "PreviewEditCurveGeometry should require live Rhino.");
        checkpoints.Add("PreviewEditCurveGeometry rejected in CLI fallback");

        RequireGeometryEditCurveCompositeFailureWithMessage(
            applyTool.ApplyEditCurveGeometry(filePath, objectId, editSpec),
            "LIVE_RHINO_REQUIRED",
            "ApplyEditCurveGeometry should require live Rhino.");
        checkpoints.Add("ApplyEditCurveGeometry rejected in CLI fallback");

        Console.WriteLine("Geometry edit curve composite smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private void RunLiveGeometryEditCurveCompositeSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        var descriptorTool = new GetEditableGeometryDescriptorTool(_editableGeometryDescriptorService);
        var previewTool = new PreviewEditCurveGeometryTool(_curveEditOrchestrator);
        var applyTool = new ApplyEditCurveGeometryTool(_curveEditOrchestrator);

        GeometryEditCurveCompositeDocumentSnapshot before = CaptureGeometryEditCurveCompositeSnapshot(filePath);
        IReadOnlyList<Guid> curveIds = CreateGeometryEditCurveCompositeSmokeCurves(filePath);

        try
        {
            foreach (Guid curveId in curveIds)
            {
                EditableGeometryDescriptor descriptor = RequireGeometryEditCurveCompositeDescriptor(
                    descriptorTool.GetEditableGeometryDescriptor(filePath, curveId),
                    "curve descriptor");
                CurveEditSpec editSpec = BuildOffsetCurveEditSpec(descriptor, 0.25d);

                GeometryEditCurveCompositeDocumentSnapshot beforePreview = CaptureGeometryEditCurveCompositeSnapshot(filePath);
                GeometryEditPreviewResponse preview = RequireGeometryEditCurveCompositeSuccess(
                    previewTool.PreviewEditCurveGeometry(filePath, curveId, editSpec),
                    "PreviewEditCurveGeometry");
                GeometryEditCurveCompositeDocumentSnapshot afterPreview = CaptureGeometryEditCurveCompositeSnapshot(filePath);
                RequireGeometryEditCurveComposite(preview.ObjectId == curveId, "Preview should echo the target ObjectId.");
                RequireGeometryEditCurveComposite(preview.ReconstructedCurveSummary is not null && preview.ReconstructedCurveSummary.PointCount == descriptor.ControlPointCount, "Preview point count should match descriptor point count.");
                RequireGeometryEditCurveComposite(beforePreview.ObjectCount == afterPreview.ObjectCount, "Preview should not change object count.");
                RequireGeometryEditCurveComposite(beforePreview.DocumentStringCount == afterPreview.DocumentStringCount, "Preview should not change document strings.");

                GeometryEditApplyResponse apply = RequireGeometryEditCurveCompositeSuccess(
                    applyTool.ApplyEditCurveGeometry(filePath, curveId, editSpec),
                    "ApplyEditCurveGeometry");
                RequireGeometryEditCurveComposite(apply.ObjectId == curveId, "Apply should preserve ObjectId.");
                RequireGeometryEditCurveComposite(string.Equals(apply.UndoRecordName, "MCP:EditCurveGeometry", StringComparison.Ordinal), "Apply should report the curve edit undo record name.");

                EditableGeometryDescriptor afterApplyDescriptor = RequireGeometryEditCurveCompositeDescriptor(
                    descriptorTool.GetEditableGeometryDescriptor(filePath, curveId),
                    "post-apply descriptor");
                RequireGeometryEditCurveComposite(afterApplyDescriptor.MetadataSummary.Name.StartsWith("mcp-curve-composite-", StringComparison.Ordinal), "Apply should preserve object name.");
                RequireGeometryEditCurveComposite(afterApplyDescriptor.MetadataSummary.UserStringCount > 0, "Apply should preserve object user strings.");
            }

            checkpoints.Add("Line/Polyline/Nurbs preview+apply ok");

            Guid invalidObjectId = Guid.NewGuid();
            RequireGeometryEditCurveCompositeFailure(
                previewTool.PreviewEditCurveGeometry(filePath, invalidObjectId, CreateTwoPointCurveEditSpec()),
                "OBJECT_NOT_FOUND",
                "Missing ObjectId should fail.");
            checkpoints.Add("Missing ObjectId guard ok");

            EditableGeometryDescriptor firstDescriptor = RequireGeometryEditCurveCompositeDescriptor(
                descriptorTool.GetEditableGeometryDescriptor(filePath, curveIds[0]),
                "first curve descriptor");
            CurveEditSpec countMismatchSpec = BuildOffsetCurveEditSpec(firstDescriptor, 0.1d);
            countMismatchSpec.Points.RemoveAt(countMismatchSpec.Points.Count - 1);
            RequireGeometryEditCurveCompositeFailure(
                previewTool.PreviewEditCurveGeometry(filePath, curveIds[0], countMismatchSpec),
                "EDIT_POINT_COUNT_MISMATCH",
                "Point count mismatch should fail.");
            checkpoints.Add("Point count guard ok");
        }
        finally
        {
            DeleteGeometryEditCurveCompositeSmokeCurves(filePath, curveIds);
        }

        GeometryEditCurveCompositeDocumentSnapshot after = CaptureGeometryEditCurveCompositeSnapshot(filePath);
        RequireGeometryEditCurveComposite(after.ObjectCount == before.ObjectCount, "Smoke should not leave temporary objects.");
        RequireGeometryEditCurveComposite(after.DocumentStringCount == before.DocumentStringCount, "Smoke should not change document strings.");
        checkpoints.Add("Temporary objects cleaned up");

        Console.WriteLine("Geometry edit curve composite smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        foreach (Guid curveId in curveIds)
        {
            Console.WriteLine($"CurveId: {curveId}");
        }

        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private GeometryEditCurveCompositeDocumentSnapshot CaptureGeometryEditCurveCompositeSnapshot(string filePath)
    {
        return RequireGeometryEditCurveCompositeSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                return OperationResponse<GeometryEditCurveCompositeDocumentSnapshot>.Ok(new GeometryEditCurveCompositeDocumentSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    DocumentStringCount = document.Strings.Count
                });
            }),
            "CaptureGeometryEditCurveCompositeSnapshot");
    }

    private IReadOnlyList<Guid> CreateGeometryEditCurveCompositeSmokeCurves(string filePath)
    {
        return RequireGeometryEditCurveCompositeSuccess(
            _liveRhinoDocumentAccessor.ExecuteWithUndo(filePath, "MCP:GeometryEditCurveCompositeSmokeSetup", document =>
            {
                var ids = new List<Guid>();

                Guid lineId = document.Objects.AddCurve(
                    new LineCurve(new Point3d(0d, 0d, 0d), new Point3d(4d, 0d, 0d)),
                    CreateGeometryEditCurveCompositeAttributes("line"));
                ids.Add(lineId);

                Guid polylineId = document.Objects.AddCurve(
                    new PolylineCurve(new[]
                    {
                        new Point3d(0d, 2d, 0d),
                        new Point3d(2d, 3d, 0d),
                        new Point3d(4d, 2d, 0d)
                    }),
                    CreateGeometryEditCurveCompositeAttributes("polyline"));
                ids.Add(polylineId);

                Curve? nurbsCurve = Curve.CreateControlPointCurve(new[]
                {
                    new Point3d(0d, 5d, 0d),
                    new Point3d(1.5d, 6d, 0d),
                    new Point3d(3d, 4d, 0d),
                    new Point3d(4.5d, 5d, 0d)
                }, 3);
                if (nurbsCurve is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to create smoke NurbsCurve.");
                }

                Guid nurbsId = document.Objects.AddCurve(
                    nurbsCurve,
                    CreateGeometryEditCurveCompositeAttributes("nurbs"));
                ids.Add(nurbsId);

                if (ids.Any(id => id == Guid.Empty))
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to add one or more smoke curves.");
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Ok((true, ids));
            }),
            "CreateGeometryEditCurveCompositeSmokeCurves");
    }

    private void DeleteGeometryEditCurveCompositeSmokeCurves(string filePath, IReadOnlyList<Guid> curveIds)
    {
        OperationResponse<bool> delete = _liveRhinoDocumentAccessor.ExecuteWithUndo(
            filePath,
            "MCP:GeometryEditCurveCompositeSmokeCleanup",
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

        RequireGeometryEditCurveCompositeSuccess(delete, "DeleteGeometryEditCurveCompositeSmokeCurves");
    }

    private static ObjectAttributes CreateGeometryEditCurveCompositeAttributes(string suffix)
    {
        var attributes = new ObjectAttributes
        {
            Name = $"mcp-curve-composite-{suffix}",
            ObjectColor = Color.FromArgb(255, 32, 128, 192),
            ColorSource = ObjectColorSource.ColorFromObject
        };
        attributes.SetUserString("mcp-smoke", "geometry-edit-curve-composite");
        return attributes;
    }

    private static CurveEditSpec BuildOffsetCurveEditSpec(EditableGeometryDescriptor descriptor, double zOffset)
    {
        return new CurveEditSpec
        {
            Operation = new GeometryEditOperationSpec
            {
                Kind = GeometryEditOperationKind.DirectOverride
            },
            Points = descriptor.Points
                .Select(point => new EditablePointInput
                {
                    Index = point.Index,
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z + zOffset
                })
                .ToList()
        };
    }

    private static CurveEditSpec CreateTwoPointCurveEditSpec()
    {
        return new CurveEditSpec
        {
            Operation = new GeometryEditOperationSpec
            {
                Kind = GeometryEditOperationKind.DirectOverride
            },
            Points = new List<EditablePointInput>
            {
                new() { Index = 0, X = 0d, Y = 0d, Z = 0d },
                new() { Index = 1, X = 1d, Y = 0d, Z = 0d }
            }
        };
    }

    private static EditableGeometryDescriptor RequireGeometryEditCurveCompositeDescriptor(
        OperationResponse<EditableGeometryDescriptorResponse> response,
        string operationName)
    {
        EditableGeometryDescriptorResponse data = RequireGeometryEditCurveCompositeSuccess(response, operationName);
        if (data.Descriptor is null)
        {
            throw new InvalidOperationException($"{operationName} returned no descriptor.");
        }

        return data.Descriptor;
    }

    private static T RequireGeometryEditCurveCompositeSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireGeometryEditCurveCompositeFailureWithMessage<T>(
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

    private static void RequireGeometryEditCurveCompositeFailure<T>(
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

    private static void RequireGeometryEditCurveComposite(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class GeometryEditCurveCompositeDocumentSnapshot
    {
        public int ObjectCount { get; set; }
        public int DocumentStringCount { get; set; }
    }
}
