using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Geometry;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private bool HandleGeometrySmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- geometry-smoke-test <3dm-file-path>");
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

            var checkpoints = new List<string>();
            var createPointsTool = new CreatePointsTool(_geometryCreationSkill);
            var transformObjectsTool = new TransformObjectsTool(_geometryModificationSkill);
            var replaceGeometryTool = new ReplaceGeometryTool(_geometryModificationSkill);
            var deleteObjectsTool = new DeleteObjectsTool(_geometryModificationSkill);

            RequireLiveFallback(
                createPointsTool.CreatePoints(
                    filePath,
                    new List<PointItemRequest> { new() { X = 1d, Y = 2d, Z = 3d } },
                    new GeometryCreationCommonOptions { LayerFullPath = "Default" }),
                "CreatePoints should require live Rhino in CLI fallback.");
            checkpoints.Add("CreatePoints returned LIVE_RHINO_REQUIRED");

            Guid objectId = Guid.NewGuid();
            RequireLiveFallback(
                transformObjectsTool.TransformObjects(
                    filePath,
                    new GeometryTransformSpec
                    {
                        Kind = GeometryTransformKind.Translate,
                        VectorX = 1d,
                        VectorY = 0d,
                        VectorZ = 0d
                    },
                    confirmedObjectIds: new List<Guid> { objectId }),
                "TransformObjects should require live Rhino in CLI fallback.");
            checkpoints.Add("TransformObjects returned LIVE_RHINO_REQUIRED");

            RequireLiveFallback(
                replaceGeometryTool.ReplaceGeometry(
                    filePath,
                    new List<GeometryReplacementEntryRequest>
                    {
                        new()
                        {
                            ObjectId = objectId,
                            Geometry = new GeometryCreationSpec
                            {
                                Primitive = GeometryPrimitiveKind.Line,
                                StartX = 0d,
                                StartY = 0d,
                                StartZ = 0d,
                                EndX = 1d,
                                EndY = 0d,
                                EndZ = 0d
                            }
                        }
                    }),
                "ReplaceGeometry should require live Rhino in CLI fallback.");
            checkpoints.Add("ReplaceGeometry returned LIVE_RHINO_REQUIRED");

            RequireLiveFallback(
                deleteObjectsTool.DeleteObjects(filePath, confirmedObjectIds: new List<Guid> { objectId }),
                "DeleteObjects should require live Rhino in CLI fallback.");
            checkpoints.Add("DeleteObjects returned LIVE_RHINO_REQUIRED");

            Console.WriteLine("Geometry smoke test completed successfully (CLI fallback mode).");
            Console.WriteLine($"File: {filePath}");
            foreach (string checkpoint in checkpoints)
            {
                Console.WriteLine($"- {checkpoint}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Geometry smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RequireLiveFallback<T>(OperationResponse<T> response, string message)
    {
        if (response.Success || !string.Equals(response.Message, "LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Expected LIVE_RHINO_REQUIRED, actual Success={response.Success}, Message=[{response.Message}].");
        }
    }
}
