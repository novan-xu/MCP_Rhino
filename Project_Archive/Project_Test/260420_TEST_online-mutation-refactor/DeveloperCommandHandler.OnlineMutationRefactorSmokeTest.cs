using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Tools.Editing;
using MCP_Rhino.Server.Tools.File;
using MCP_Rhino.Server.Tools.Geometry;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private bool HandleOnlineMutationRefactorSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- online-mutation-refactor-smoke-test <3dm-file-path>");
            System.Environment.ExitCode = 1;
            return true;
        }

        try
        {
            string sourceFilePath = Path.GetFullPath(args[1]);
            if (!File.Exists(sourceFilePath))
            {
                Console.Error.WriteLine($"Smoke test source file was not found: {sourceFilePath}");
                System.Environment.ExitCode = 1;
                return true;
            }

            string workingFilePath = sourceFilePath;
            string layerFullPath = "Default";
            Guid objectId = Guid.NewGuid();
            var checkpoints = new List<string>();

            var readDocumentUserStringsTool = new GetDocumentUserStringsTool(_documentUserStringService);
            var previewObjectUserTextWritesTool = new PreviewObjectUserTextWritesTool(_userTextService);
            var setDocumentUserStringsTool = new SetDocumentUserStringsTool(_documentUserStringService);
            var createPointsTool = new CreatePointsTool(_geometryCreationSkill);
            var applyObjectUserTextWritesTool = new ApplyObjectUserTextWritesTool(_userTextService);

            OperationResponse<DocumentUserStringReadResponse> readResult = readDocumentUserStringsTool.GetDocumentUserStrings(workingFilePath);
            RequireOnlineMutationFailureWithMessage(readResult, "LIVE_RHINO_REQUIRED", "GetDocumentUserStrings should require live Rhino.");
            checkpoints.Add("GetDocumentUserStrings rejected in CLI fallback");

            OperationResponse<ObjectEditPreviewResponse> previewResult = previewObjectUserTextWritesTool.PreviewObjectUserTextWrites(
                workingFilePath,
                new List<ObjectScopedUserTextEntryRequest>
                {
                    new()
                    {
                        ObjectId = objectId,
                        Key = "online_mutation_preview",
                        Value = "preview-only"
                    }
                });
            RequireOnlineMutationFailureWithMessage(previewResult, "LIVE_RHINO_REQUIRED", "PreviewObjectUserTextWrites should require live Rhino.");
            checkpoints.Add("PreviewObjectUserTextWrites rejected in CLI fallback");

            OperationResponse<DocumentUserStringMutationResponse> setDocumentResult = setDocumentUserStringsTool.SetDocumentUserStrings(
                workingFilePath,
                new List<DocumentUserStringEntryRequest>
                {
                    new()
                    {
                        Key = "online_mutation_smoke",
                        Value = "should-fail-with-live-required"
                    }
                });
            RequireOnlineMutationFailureWithMessage(setDocumentResult, "LIVE_RHINO_REQUIRED", "SetDocumentUserStrings should require live Rhino.");
            checkpoints.Add("SetDocumentUserStrings rejected in CLI fallback");

            OperationResponse<GeometryCreationResponse> createPointResult = createPointsTool.CreatePoints(
                workingFilePath,
                new List<PointItemRequest>
                {
                    new()
                    {
                        X = 1d,
                        Y = 2d,
                        Z = 3d
                    }
                },
                new GeometryCreationCommonOptions
                {
                    LayerFullPath = layerFullPath
                });
            RequireOnlineMutationFailureWithMessage(createPointResult, "LIVE_RHINO_REQUIRED", "CreatePoints should require live Rhino.");
            checkpoints.Add("CreatePoints rejected in CLI fallback");

            OperationResponse<ObjectEditExecutionResponse> applyUserTextResult = applyObjectUserTextWritesTool.ApplyObjectUserTextWrites(
                workingFilePath,
                new List<ObjectScopedUserTextEntryRequest>
                {
                    new()
                    {
                        ObjectId = objectId,
                        Key = "online_mutation_apply",
                        Value = "should-fail-with-live-required"
                    }
                });
            RequireOnlineMutationFailureWithMessage(applyUserTextResult, "LIVE_RHINO_REQUIRED", "ApplyObjectUserTextWrites should require live Rhino.");
            checkpoints.Add("ApplyObjectUserTextWrites rejected in CLI fallback");

            Console.WriteLine("Online mutation refactor smoke test completed successfully.");
            Console.WriteLine($"Source: {sourceFilePath}");

            foreach (string checkpoint in checkpoints)
            {
                Console.WriteLine($"- {checkpoint}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Online mutation refactor smoke test failed: {ex}");
            System.Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RequireOnlineMutationFailureWithMessage<T>(OperationResponse<T> response, string expectedMessage, string message)
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

}
