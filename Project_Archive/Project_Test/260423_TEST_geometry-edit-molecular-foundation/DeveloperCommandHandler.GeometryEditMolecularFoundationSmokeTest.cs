extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Analysis;
using MCP_Rhino.Server.Tools.Geometry.Edit;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Surface = rhinocommon::Rhino.Geometry.Surface;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterGeometryEditMolecularFoundationHandlers()
    {
        _extensionHandlers["geometry-edit-molecular-foundation-smoke-test"] = HandleGeometryEditMolecularFoundationSmokeTest;
    }

    private bool HandleGeometryEditMolecularFoundationSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- geometry-edit-molecular-foundation-smoke-test <3dm-file-path>");
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
                RunCliFallbackGeometryEditMolecularFoundationSmoke(filePath);
            }
            else
            {
                RunLiveGeometryEditMolecularFoundationSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Geometry edit molecular foundation smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackGeometryEditMolecularFoundationSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        Guid objectId = Guid.NewGuid();

        var descriptorTool = new GetEditableGeometryDescriptorTool(_editableGeometryDescriptorService);
        var framesTool = new GetGeometryFramesInLiveTool(_geometryMetricsService);

        RequireGeometryEditFoundationFailureWithMessage(
            descriptorTool.GetEditableGeometryDescriptor(filePath, objectId),
            "LIVE_RHINO_REQUIRED",
            "GetEditableGeometryDescriptor should require live Rhino.");
        checkpoints.Add("GetEditableGeometryDescriptor rejected in CLI fallback");

        RequireGeometryEditFoundationFailureWithMessage(
            framesTool.GetGeometryFramesInLive(filePath, new List<GeometryFrameEntryRequest>
            {
                new()
                {
                    EntryId = "frame-cli",
                    ObjectId = objectId,
                    ParameterSpec = new GeometryFrameParameterSpecRequest
                    {
                        Kind = GeometryFrameParameterSpecKind.CurveExplicit,
                        Parameters = new List<double> { 0d }
                    }
                }
            }),
            "LIVE_RHINO_REQUIRED",
            "GetGeometryFramesInLive sampled entry should require live Rhino.");
        checkpoints.Add("GetGeometryFramesInLive sampled entry rejected in CLI fallback");

        Console.WriteLine("Geometry edit molecular foundation smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private void RunLiveGeometryEditMolecularFoundationSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        var descriptorTool = new GetEditableGeometryDescriptorTool(_editableGeometryDescriptorService);
        var framesTool = new GetGeometryFramesInLiveTool(_geometryMetricsService);

        GeometryEditFoundationDocumentSnapshot before = CaptureGeometryEditFoundationSnapshot(filePath);
        GeometryEditFoundationFixtureContext fixture = ReadGeometryEditFoundationFixtureContext(filePath);

        EditableGeometryDescriptor curveDescriptor = RequireGeometryEditFoundationDescriptor(
            descriptorTool.GetEditableGeometryDescriptor(filePath, fixture.CurveId),
            "curve descriptor");
        RequireGeometryEditFoundation(curveDescriptor.Kind == EditableGeometryKind.Curve, "Curve descriptor should report Kind=Curve.");
        RequireGeometryEditFoundation(curveDescriptor.Points.Count == curveDescriptor.ControlPointCount, "Curve default descriptor should be Full.");
        RequireGeometryEditFoundation(curveDescriptor.ControlPointCount > 0, "Curve descriptor should expose control points.");
        RequireGeometryEditFoundation(curveDescriptor.MetadataSummary.LayerIndex >= 0, "Curve metadata summary should include layer index.");
        checkpoints.Add("Curve descriptor ok");

        EditableGeometryDescriptor surfaceSummary = RequireGeometryEditFoundationDescriptor(
            descriptorTool.GetEditableGeometryDescriptor(filePath, fixture.SurfaceId),
            "surface summary descriptor");
        RequireGeometryEditFoundation(surfaceSummary.Kind == EditableGeometryKind.Surface, "Surface descriptor should report Kind=Surface.");
        RequireGeometryEditFoundation(surfaceSummary.Points.Count == 0, "Surface default descriptor should be Summary.");
        RequireGeometryEditFoundation(surfaceSummary.ControlPointCount > 0, "Surface summary should still expose control point count.");

        EditableGeometryDescriptor surfaceFull = RequireGeometryEditFoundationDescriptor(
            descriptorTool.GetEditableGeometryDescriptor(filePath, fixture.SurfaceId, DescriptorDetail.Full),
            "surface full descriptor");
        RequireGeometryEditFoundation(surfaceFull.Points.Count == surfaceFull.ControlPointCount, "Surface Full descriptor should include all control points.");
        RequireGeometryEditFoundation(PointsClose(surfaceSummary.ControlPointCentroidWorld, surfaceFull.ControlPointCentroidWorld, 1e-8), "Surface Summary and Full centroids should match.");
        checkpoints.Add("Surface descriptor ok");

        EditableGeometryDescriptor brepDescriptor = RequireGeometryEditFoundationDescriptor(
            descriptorTool.GetEditableGeometryDescriptor(filePath, fixture.UntrimmedSingleFaceBrepId),
            "untrimmed single-face Brep descriptor");
        RequireGeometryEditFoundation(brepDescriptor.Kind == EditableGeometryKind.Surface, "Untrimmed single-face Brep should downgrade to Surface descriptor.");
        RequireGeometryEditFoundation(brepDescriptor.Warnings.Contains("UNDERLYING_SURFACE_FALLBACK"), "Brep descriptor should include UNDERLYING_SURFACE_FALLBACK.");
        checkpoints.Add("Untrimmed single-face Brep downgrade ok");

        GetGeometryFramesInLiveResponse curveFrames = RequireGeometryEditFoundationSuccess(
            framesTool.GetGeometryFramesInLive(filePath, new List<GeometryFrameEntryRequest>
            {
                new()
                {
                    EntryId = "curve-fractions",
                    ObjectId = fixture.CurveId,
                    ParameterSpec = new GeometryFrameParameterSpecRequest
                    {
                        Kind = GeometryFrameParameterSpecKind.AtParameterFractions,
                        Fractions = new List<double> { 0d, 0.5d, 1d }
                    }
                }
            }),
            "curve frame sampling");
        RequireGeometryEditFoundation(curveFrames.SucceededCount == 1 && curveFrames.Results[0].Samples.Count == 3, "Curve fraction sampling should return three samples.");
        checkpoints.Add("Curve frame sampling ok");

        GetGeometryFramesInLiveResponse surfaceFrames = RequireGeometryEditFoundationSuccess(
            framesTool.GetGeometryFramesInLive(filePath, new List<GeometryFrameEntryRequest>
            {
                new()
                {
                    EntryId = "surface-grid",
                    ObjectId = fixture.SurfaceId,
                    ParameterSpec = new GeometryFrameParameterSpecRequest
                    {
                        Kind = GeometryFrameParameterSpecKind.AtControlPointGrevilles
                    }
                }
            }),
            "surface frame sampling");
        RequireGeometryEditFoundation(surfaceFrames.SucceededCount == 1, "Surface frame sampling should succeed.");
        RequireGeometryEditFoundation(surfaceFrames.Results[0].Samples.Count == surfaceSummary.ControlPointCount, "Surface frame grid sampling should match control point count.");
        checkpoints.Add("Surface frame sampling ok");

        GetGeometryFramesInLiveResponse outOfRangeFrames = RequireGeometryEditFoundationSuccess(
            framesTool.GetGeometryFramesInLive(filePath, new List<GeometryFrameEntryRequest>
            {
                new()
                {
                    EntryId = "curve-out-of-range",
                    ObjectId = fixture.CurveId,
                    ParameterSpec = new GeometryFrameParameterSpecRequest
                    {
                        Kind = GeometryFrameParameterSpecKind.CurveExplicit,
                        Parameters = new List<double> { double.MaxValue }
                    }
                }
            }),
            "curve out-of-range frame sampling");
        RequireGeometryEditFoundation(outOfRangeFrames.FailedCount == 1, "Out-of-range frame sampling should fail per entry.");
        RequireGeometryEditFoundation(outOfRangeFrames.Results[0].Message.Contains("FRAME_PARAMETER_OUT_OF_RANGE", StringComparison.Ordinal), "Out-of-range frame sampling should report FRAME_PARAMETER_OUT_OF_RANGE.");
        checkpoints.Add("Frame out-of-range guard ok");

        RunGeometryEditFoundationMetadataReplaySmoke(filePath);
        checkpoints.Add("Metadata snapshot/replay ok");

        GeometryEditFoundationDocumentSnapshot after = CaptureGeometryEditFoundationSnapshot(filePath);
        RequireGeometryEditFoundation(after.ObjectCount == before.ObjectCount, "Smoke should not leave extra objects.");
        RequireGeometryEditFoundation(after.LayerCount == before.LayerCount, "Smoke should not change layer count.");
        RequireGeometryEditFoundation(after.DocumentStringCount == before.DocumentStringCount, "Smoke should not change document user strings.");
        RequireGeometryEditFoundation(after.ObjectUserTextKeyCount == before.ObjectUserTextKeyCount, "Smoke should not leave object user strings.");
        checkpoints.Add("Document state preserved");

        Console.WriteLine("Geometry edit molecular foundation smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        Console.WriteLine($"CurveId: {fixture.CurveId}");
        Console.WriteLine($"SurfaceId: {fixture.SurfaceId}");
        Console.WriteLine($"UntrimmedSingleFaceBrepId: {fixture.UntrimmedSingleFaceBrepId}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private GeometryEditFoundationDocumentSnapshot CaptureGeometryEditFoundationSnapshot(string filePath)
    {
        return RequireGeometryEditFoundationSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                int objectUserTextKeyCount = 0;
                foreach (RhinoObject rhinoObject in document.Objects)
                {
                    if (rhinoObject.IsDeleted)
                    {
                        continue;
                    }

                    var userStrings = rhinoObject.Attributes.GetUserStrings();
                    if (userStrings is not null)
                    {
                        objectUserTextKeyCount += userStrings.Count;
                    }
                }

                return OperationResponse<GeometryEditFoundationDocumentSnapshot>.Ok(new GeometryEditFoundationDocumentSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    LayerCount = Enumerable.Range(0, document.Layers.Count).Count(index => !document.Layers[index].IsDeleted),
                    DocumentStringCount = document.Strings.Count,
                    ObjectUserTextKeyCount = objectUserTextKeyCount
                });
            }),
            "CaptureGeometryEditFoundationSnapshot");
    }

    private GeometryEditFoundationFixtureContext ReadGeometryEditFoundationFixtureContext(string filePath)
    {
        return RequireGeometryEditFoundationSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                Guid? curveId = null;
                Guid? surfaceId = null;
                Guid? untrimmedSingleFaceBrepId = null;

                foreach (RhinoObject rhinoObject in document.Objects)
                {
                    if (rhinoObject.IsDeleted)
                    {
                        continue;
                    }

                    if (!curveId.HasValue && rhinoObject.Geometry is Curve)
                    {
                        curveId = rhinoObject.Id;
                    }

                    if (!surfaceId.HasValue && rhinoObject.Geometry is Surface)
                    {
                        surfaceId = rhinoObject.Id;
                    }

                    if (!untrimmedSingleFaceBrepId.HasValue
                        && rhinoObject.Geometry is Brep brep
                        && brep.Faces.Count == 1
                        && brep.Faces[0].IsSurface)
                    {
                        untrimmedSingleFaceBrepId = rhinoObject.Id;
                    }
                }

                if (!curveId.HasValue)
                {
                    return OperationResponse<GeometryEditFoundationFixtureContext>.Fail("The live fixture does not contain a curve object.");
                }

                if (!surfaceId.HasValue)
                {
                    return OperationResponse<GeometryEditFoundationFixtureContext>.Fail("The live fixture does not contain a surface object.");
                }

                if (!untrimmedSingleFaceBrepId.HasValue)
                {
                    return OperationResponse<GeometryEditFoundationFixtureContext>.Fail("The live fixture does not contain an untrimmed single-face Brep object.");
                }

                return OperationResponse<GeometryEditFoundationFixtureContext>.Ok(new GeometryEditFoundationFixtureContext
                {
                    CurveId = curveId.Value,
                    SurfaceId = surfaceId.Value,
                    UntrimmedSingleFaceBrepId = untrimmedSingleFaceBrepId.Value
                });
            }),
            "ReadGeometryEditFoundationFixtureContext");
    }

    private void RunGeometryEditFoundationMetadataReplaySmoke(string filePath)
    {
        OperationResponse<bool> result = _liveRhinoDocumentAccessor.ExecuteWithUndo(
            filePath,
            "MCP:GeometryEditMolecularFoundationMetadataSmoke",
            document =>
            {
                Guid sourceId = Guid.Empty;
                Guid targetId = Guid.Empty;

                try
                {
                    sourceId = document.Objects.AddPoint(new Point3d(0d, 0d, 0d));
                    targetId = document.Objects.AddPoint(new Point3d(1d, 0d, 0d));
                    if (sourceId == Guid.Empty || targetId == Guid.Empty)
                    {
                        return OperationResponse<(bool Mutated, bool Result)>.Fail("Failed to create metadata smoke point objects.");
                    }

                    RhinoObject? sourceObject = document.Objects.FindId(sourceId);
                    if (sourceObject is null)
                    {
                        return OperationResponse<(bool Mutated, bool Result)>.Fail("Failed to resolve metadata smoke source object.");
                    }

                    ObjectAttributes sourceAttributes = sourceObject.Attributes.Duplicate();
                    sourceAttributes.Name = "mcp-molecular-foundation-source";
                    sourceAttributes.ObjectColor = Color.FromArgb(255, 24, 96, 160);
                    sourceAttributes.ColorSource = ObjectColorSource.ColorFromObject;
                    sourceAttributes.SetUserString("mcp-smoke", "geometry-edit-molecular-foundation");
                    if (!document.Objects.ModifyAttributes(sourceId, sourceAttributes, true))
                    {
                        return OperationResponse<(bool Mutated, bool Result)>.Fail("Failed to modify metadata smoke source attributes.");
                    }

                    RhinoObject? refreshedSourceObject = document.Objects.FindId(sourceId);
                    if (refreshedSourceObject is null)
                    {
                        return OperationResponse<(bool Mutated, bool Result)>.Fail("Failed to resolve refreshed metadata smoke source object.");
                    }

                    GeometryMetadataSnapshot sourceSnapshot = _geometryMetadataOperator.Snapshot(refreshedSourceObject);
                    OperationResponse<IReadOnlyList<ObjectEditWarning>> replay = _geometryMetadataOperator.Replay(document, targetId, sourceSnapshot);
                    if (!replay.Success)
                    {
                        return OperationResponse<(bool Mutated, bool Result)>.Fail(replay.Message);
                    }

                    RhinoObject? targetObject = document.Objects.FindId(targetId);
                    if (targetObject is null)
                    {
                        return OperationResponse<(bool Mutated, bool Result)>.Fail("Failed to resolve metadata smoke target object.");
                    }

                    GeometryMetadataSnapshot targetSnapshot = _geometryMetadataOperator.Snapshot(targetObject);
                    if (!MetadataSnapshotsMatch(sourceSnapshot, targetSnapshot))
                    {
                        return OperationResponse<(bool Mutated, bool Result)>.Fail("Metadata replay did not reproduce the whitelisted fields.");
                    }

                    return OperationResponse<(bool Mutated, bool Result)>.Ok((true, true), "Metadata snapshot/replay completed.");
                }
                finally
                {
                    if (sourceId != Guid.Empty)
                    {
                        document.Objects.Delete(sourceId, true);
                    }

                    if (targetId != Guid.Empty)
                    {
                        document.Objects.Delete(targetId, true);
                    }
                }
            });

        RequireGeometryEditFoundationSuccess(result, "metadata snapshot/replay");
    }

    private static EditableGeometryDescriptor RequireGeometryEditFoundationDescriptor(
        OperationResponse<EditableGeometryDescriptorResponse> response,
        string operationName)
    {
        EditableGeometryDescriptorResponse data = RequireGeometryEditFoundationSuccess(response, operationName);
        if (data.Descriptor is null)
        {
            throw new InvalidOperationException($"{operationName} returned no descriptor.");
        }

        return data.Descriptor;
    }

    private static T RequireGeometryEditFoundationSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireGeometryEditFoundationFailureWithMessage<T>(
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

    private static void RequireGeometryEditFoundation(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool PointsClose(GeometryPointData left, GeometryPointData right, double tolerance)
    {
        return Math.Abs(left.X - right.X) <= tolerance
            && Math.Abs(left.Y - right.Y) <= tolerance
            && Math.Abs(left.Z - right.Z) <= tolerance;
    }

    private static bool MetadataSnapshotsMatch(GeometryMetadataSnapshot left, GeometryMetadataSnapshot right)
    {
        return left.LayerIndex == right.LayerIndex
            && left.ColorSource == right.ColorSource
            && left.ObjectColor.A == right.ObjectColor.A
            && left.ObjectColor.R == right.ObjectColor.R
            && left.ObjectColor.G == right.ObjectColor.G
            && left.ObjectColor.B == right.ObjectColor.B
            && left.Name == right.Name
            && left.Visible == right.Visible
            && left.UserStrings.Count == right.UserStrings.Count
            && left.UserStrings.All(entry => right.UserStrings.TryGetValue(entry.Key, out string? value) && value == entry.Value);
    }

    private sealed class GeometryEditFoundationDocumentSnapshot
    {
        public int ObjectCount { get; set; }
        public int LayerCount { get; set; }
        public int DocumentStringCount { get; set; }
        public int ObjectUserTextKeyCount { get; set; }
    }

    private sealed class GeometryEditFoundationFixtureContext
    {
        public Guid CurveId { get; set; }
        public Guid SurfaceId { get; set; }
        public Guid UntrimmedSingleFaceBrepId { get; set; }
    }
}
