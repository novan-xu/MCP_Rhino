using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Tools.Editing;
using MCP_Rhino.Server.Tools.File;
using MCP_Rhino.Server.Tools.Geometry;
using Rhino.FileIO;

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

            string validationDirectory = ResolveValidationDirectory("online-mutation-refactor");

            string workingFilePath = Path.Combine(validationDirectory, "MCP_METtest.online-mutation-refactor.3dm");
            File.Copy(sourceFilePath, workingFilePath, overwrite: true);

            int initialObjectCount = GetOnlineMutationObjectCount(workingFilePath);
            string layerFullPath = GetOnlineMutationFirstActiveLayerFullPath(workingFilePath);
            Guid objectId = TryGetOnlineMutationFirstObjectId(workingFilePath) ?? Guid.NewGuid();
            var checkpoints = new List<string>();

            var readDocumentUserStringsTool = new GetDocumentUserStringsTool(_documentUserStringService);
            var previewObjectUserTextWritesTool = new PreviewObjectUserTextWritesTool(_userTextService);
            var setDocumentUserStringsTool = new SetDocumentUserStringsTool(_documentUserStringService);
            var createPointsTool = new CreatePointsTool(_geometryCreationSkill);
            var applyObjectUserTextWritesTool = new ApplyObjectUserTextWritesTool(_userTextService);

            OperationResponse<DocumentUserStringReadResponse> readResult = readDocumentUserStringsTool.GetDocumentUserStrings(workingFilePath);
            DocumentUserStringReadResponse readData = RequireOnlineMutationSuccess(readResult, "GetDocumentUserStrings");
            RequireOnlineMutation(readData.Warnings.Count == 0, "CLI fallback read should not emit stale warnings when no live Rhino host is present.");
            checkpoints.Add("GetDocumentUserStrings ok");

            if (TryGetOnlineMutationFirstObjectId(workingFilePath).HasValue)
            {
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

                ObjectEditPreviewResponse previewData = RequireOnlineMutationSuccess(previewResult, "PreviewObjectUserTextWrites");
                RequireOnlineMutation(previewData.PreviewObjectCount == 1, "PreviewObjectUserTextWrites should still work in offline mode.");
                checkpoints.Add("PreviewObjectUserTextWrites ok");
            }
            else
            {
                checkpoints.Add("PreviewObjectUserTextWrites skipped: source file contains no objects");
            }

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

            RequireOnlineMutation(
                GetOnlineMutationObjectCount(workingFilePath) == initialObjectCount,
                $"CLI fallback smoke test should not mutate the working copy. Initial={initialObjectCount}, Current={GetOnlineMutationObjectCount(workingFilePath)}");
            checkpoints.Add("Working copy remained unchanged");

            Console.WriteLine("Online mutation refactor smoke test completed successfully.");
            Console.WriteLine($"Source: {sourceFilePath}");
            Console.WriteLine($"Working copy: {workingFilePath}");
            Console.WriteLine($"Initial objects: {initialObjectCount}");
            Console.WriteLine($"Final objects: {GetOnlineMutationObjectCount(workingFilePath)}");

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

    private static T RequireOnlineMutationSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
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

    private static int GetOnlineMutationObjectCount(string filePath)
    {
        using File3dm model = ReadOnlineMutationModel(filePath);
        return model.Objects.Count;
    }

    private static string GetOnlineMutationFirstActiveLayerFullPath(string filePath)
    {
        using File3dm model = ReadOnlineMutationModel(filePath);
        var layer = model.AllLayers.FirstOrDefault(candidate => !candidate.IsDeleted);
        if (layer is null)
        {
            throw new InvalidOperationException("The smoke test source file does not contain an active layer.");
        }

        return layer.FullPath;
    }

    private static Guid? TryGetOnlineMutationFirstObjectId(string filePath)
    {
        using File3dm model = ReadOnlineMutationModel(filePath);
        foreach (File3dmObject modelObject in model.Objects)
        {
            if (modelObject.Attributes.ObjectId != Guid.Empty)
            {
                return modelObject.Attributes.ObjectId;
            }
        }

        return null;
    }

    private static File3dm ReadOnlineMutationModel(string filePath)
    {
        File3dm? model = File3dm.Read(filePath);
        if (model is null)
        {
            throw new InvalidOperationException($"Failed to read Rhino model: {filePath}");
        }

        return model;
    }

    private static void RequireOnlineMutation(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
