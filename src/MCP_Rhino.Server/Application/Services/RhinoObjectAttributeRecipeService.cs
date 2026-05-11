extern alias rhinocommon;

using System.Drawing;
using System.Text.RegularExpressions;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoObjectAttributeRecipeService
{
    private const int PreviewLimit = 20;

    private static readonly Regex TemplateTokenRegex = new(
        @"\{(?<token>objectId|objectName|layerName|layerFullPath|objectType|geometryType|index|user:[^}]+)\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly IEditResultFormatter _formatter;

    public RhinoObjectAttributeRecipeService(
        ILiveRhinoDocumentAccessor documentAccessor,
        IEditResultFormatter formatter)
    {
        _documentAccessor = documentAccessor;
        _formatter = formatter;
    }

    public OperationResponse<ObjectEditPreviewResponse> Preview(
        PreviewBulkObjectAttributeRecipeRequest request,
        RhinoObjectFilterResult selection)
    {
        OperationResponse<PreparedObjectAttributeRecipe> recipe = PrepareRecipe(request);
        if (!recipe.Success || recipe.Data is null)
        {
            return OperationResponse<ObjectEditPreviewResponse>.Fail(recipe.Message);
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            OperationResponse<PreparedObjectAttributeRecipe> documentValidation = ValidateRecipeAgainstDocument(document, recipe.Data);
            if (!documentValidation.Success || documentValidation.Data is null)
            {
                return OperationResponse<ObjectEditPreviewResponse>.Fail(documentValidation.Message);
            }

            PreparedObjectAttributeRecipe prepared = documentValidation.Data;
            var warnings = new List<ObjectEditWarning>();
            if (selection.Objects.Count == 0)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "NO_MATCHED_OBJECTS",
                    Message = "No objects matched the selection."
                });
            }

            List<ObjectEditOperationResult> previewResults = selection.Objects
                .Take(PreviewLimit)
                .Select((objectInfo, index) => BuildPreviewResult(document, objectInfo, prepared, index + 1))
                .ToList();

            if (selection.Objects.Count > PreviewLimit)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "PREVIEW_TRUNCATED",
                    Message = $"Preview only shows the first {PreviewLimit} objects."
                });
            }

            var response = new ObjectEditPreviewResponse
            {
                FilePath = request.FilePath,
                CriteriaSummary = BuildCriteriaSummary(selection, prepared),
                MatchedObjectCount = selection.MatchedCount,
                PreviewObjectCount = previewResults.Count,
                OperationCount = prepared.OperationCount,
                Warnings = warnings,
                ObjectResults = previewResults
            };

            return OperationResponse<ObjectEditPreviewResponse>.Ok(response, "Bulk object attribute recipe preview generated.");
        });
    }

    public OperationResponse<ObjectEditExecutionResponse> Apply(
        ApplyBulkObjectAttributeRecipeRequest request,
        RhinoObjectFilterResult selection)
    {
        OperationResponse<PreparedObjectAttributeRecipe> recipe = PrepareRecipe(request);
        if (!recipe.Success || recipe.Data is null)
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail(recipe.Message);
        }

        if (selection.Objects.Count == 0)
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail("No objects matched the selection.");
        }

        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: ApplyBulkObjectAttributeRecipe", document =>
        {
            OperationResponse<PreparedObjectAttributeRecipe> documentValidation = ValidateRecipeAgainstDocument(document, recipe.Data);
            if (!documentValidation.Success || documentValidation.Data is null)
            {
                return OperationResponse<(bool Mutated, ObjectEditExecutionResponse Result)>.Fail(documentValidation.Message);
            }

            PreparedObjectAttributeRecipe prepared = documentValidation.Data;
            var operationResults = new List<ObjectEditOperationResult>(selection.Objects.Count);
            bool mutated = false;

            for (int i = 0; i < selection.Objects.Count; i++)
            {
                RhinoObjectInfo objectInfo = selection.Objects[i];
                OperationResponse<ObjectEditOperationResult> result = ApplyToObject(document, objectInfo, prepared, i + 1);
                operationResults.Add(ToOperationResult(document, objectInfo, result));
                mutated = mutated || result.Success;
            }

            if (mutated)
            {
                document.Views.Redraw();
            }

            var warnings = new List<ObjectEditWarning>();
            if (operationResults.Count > PreviewLimit)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "RESULT_TRUNCATED",
                    Message = $"Result list only shows the first {PreviewLimit} objects."
                });
            }

            var response = new ObjectEditExecutionResponse
            {
                FilePath = request.FilePath,
                CriteriaSummary = BuildCriteriaSummary(selection, prepared),
                MatchedObjectCount = selection.MatchedCount,
                UpdatedObjectCount = operationResults.Count(result => result.Success),
                FailedObjectCount = operationResults.Count(result => !result.Success),
                OperationCount = prepared.OperationCount,
                Warnings = warnings,
                ObjectResults = operationResults.Take(PreviewLimit).ToList()
            };

            return OperationResponse<(bool Mutated, ObjectEditExecutionResponse Result)>.Ok(
                (mutated, response),
                "Bulk object attribute recipe applied.");
        });
    }

    public string FormatPreview(ObjectEditPreviewResponse response)
    {
        return _formatter.FormatPreview(response);
    }

    public string FormatExecution(ObjectEditExecutionResponse response)
    {
        return _formatter.FormatExecution(response);
    }

    private static OperationResponse<PreparedObjectAttributeRecipe> PrepareRecipe(ObjectAttributeRecipeRequestBase request)
    {
        var userTextWrites = request.UserTextWrites
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .Select(entry => new PreparedUserTextWrite(entry.Key.Trim(), entry.ValueTemplate ?? string.Empty))
            .ToList();

        var removeUserTextKeys = request.RemoveUserTextKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var duplicateWriteKeys = userTextWrites
            .GroupBy(write => write.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateWriteKeys.Count > 0)
        {
            return OperationResponse<PreparedObjectAttributeRecipe>.Fail(
                $"Duplicate user text write keys are not allowed: {string.Join(", ", duplicateWriteKeys)}");
        }

        var writeKeyLookup = new HashSet<string>(userTextWrites.Select(write => write.Key), StringComparer.OrdinalIgnoreCase);
        var removeAndWriteKeys = removeUserTextKeys.Where(writeKeyLookup.Contains).ToList();
        if (removeAndWriteKeys.Count > 0)
        {
            return OperationResponse<PreparedObjectAttributeRecipe>.Fail(
                $"The same user text key cannot be removed and written in one recipe: {string.Join(", ", removeAndWriteKeys)}");
        }

        if (request.DisplayColor is not null
            && (request.DisplayColor.R is < 0 or > 255
                || request.DisplayColor.G is < 0 or > 255
                || request.DisplayColor.B is < 0 or > 255))
        {
            return OperationResponse<PreparedObjectAttributeRecipe>.Fail("Display color values must be in range 0-255.");
        }

        string targetLayerFullPath = request.TargetLayerFullPath?.Trim() ?? string.Empty;
        string objectNameTemplate = request.ObjectNameTemplate ?? string.Empty;
        int operationCount = userTextWrites.Count
            + removeUserTextKeys.Count
            + (string.IsNullOrWhiteSpace(targetLayerFullPath) ? 0 : 1)
            + (request.DisplayColor is null ? 0 : 1)
            + (string.IsNullOrWhiteSpace(objectNameTemplate) ? 0 : 1);

        if (operationCount == 0)
        {
            return OperationResponse<PreparedObjectAttributeRecipe>.Fail("At least one bulk object attribute recipe operation is required.");
        }

        return OperationResponse<PreparedObjectAttributeRecipe>.Ok(new PreparedObjectAttributeRecipe(
            userTextWrites,
            removeUserTextKeys,
            targetLayerFullPath,
            request.DisplayColor,
            objectNameTemplate,
            TargetLayerIndex: null,
            operationCount));
    }

    private static OperationResponse<PreparedObjectAttributeRecipe> ValidateRecipeAgainstDocument(
        RhinoDoc document,
        PreparedObjectAttributeRecipe recipe)
    {
        int? targetLayerIndex = null;
        if (!string.IsNullOrWhiteSpace(recipe.TargetLayerFullPath))
        {
            int resolvedLayerIndex = document.Layers.FindByFullPath(recipe.TargetLayerFullPath, -1);
            if (resolvedLayerIndex < 0)
            {
                return OperationResponse<PreparedObjectAttributeRecipe>.Fail($"Target layer not found: {recipe.TargetLayerFullPath}");
            }

            targetLayerIndex = resolvedLayerIndex;
        }

        return OperationResponse<PreparedObjectAttributeRecipe>.Ok(recipe with { TargetLayerIndex = targetLayerIndex });
    }

    private static ObjectEditOperationResult BuildPreviewResult(
        RhinoDoc document,
        RhinoObjectInfo objectInfo,
        PreparedObjectAttributeRecipe recipe,
        int index)
    {
        RhinoObject? currentObject = document.Objects.FindId(objectInfo.ObjectId);
        if (currentObject is null)
        {
            return new ObjectEditOperationResult
            {
                ObjectId = objectInfo.ObjectId,
                LayerFullPath = objectInfo.LayerFullPath,
                Success = false,
                Messages = new[] { "Object was not found in the active document." }
            };
        }

        var messages = new List<string>();
        AddPreviewMessages(messages, currentObject, objectInfo, recipe, index);

        return new ObjectEditOperationResult
        {
            ObjectId = objectInfo.ObjectId,
            LayerFullPath = string.IsNullOrWhiteSpace(recipe.TargetLayerFullPath) ? objectInfo.LayerFullPath : recipe.TargetLayerFullPath,
            Success = true,
            Messages = messages
        };
    }

    private static OperationResponse<ObjectEditOperationResult> ApplyToObject(
        RhinoDoc document,
        RhinoObjectInfo objectInfo,
        PreparedObjectAttributeRecipe recipe,
        int index)
    {
        RhinoObject? currentObject = document.Objects.FindId(objectInfo.ObjectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Object not found: {objectInfo.ObjectId}");
        }

        ObjectAttributes attributes = currentObject.Attributes.Duplicate();
        var messages = new List<string>();

        AddPreviewMessages(messages, currentObject, objectInfo, recipe, index);

        if (recipe.TargetLayerIndex.HasValue)
        {
            attributes.LayerIndex = recipe.TargetLayerIndex.Value;
        }

        if (recipe.DisplayColor is not null)
        {
            attributes.ObjectColor = Color.FromArgb(recipe.DisplayColor.R, recipe.DisplayColor.G, recipe.DisplayColor.B);
            attributes.ColorSource = ObjectColorSource.ColorFromObject;
        }

        if (!string.IsNullOrWhiteSpace(recipe.ObjectNameTemplate))
        {
            attributes.Name = ResolveTemplate(recipe.ObjectNameTemplate, objectInfo, currentObject, index);
        }

        foreach (string key in recipe.RemoveUserTextKeys)
        {
            attributes.DeleteUserString(key);
        }

        foreach (PreparedUserTextWrite write in recipe.UserTextWrites)
        {
            attributes.SetUserString(write.Key, ResolveTemplate(write.ValueTemplate, objectInfo, currentObject, index));
        }

        if (!document.Objects.ModifyAttributes(objectInfo.ObjectId, attributes, true))
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"ModifyAttributes failed: {objectInfo.ObjectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = objectInfo.ObjectId,
            LayerFullPath = string.IsNullOrWhiteSpace(recipe.TargetLayerFullPath) ? objectInfo.LayerFullPath : recipe.TargetLayerFullPath,
            Success = true,
            Messages = messages
        });
    }

    private static void AddPreviewMessages(
        List<string> messages,
        RhinoObject currentObject,
        RhinoObjectInfo objectInfo,
        PreparedObjectAttributeRecipe recipe,
        int index)
    {
        if (!string.IsNullOrWhiteSpace(recipe.TargetLayerFullPath))
        {
            messages.Add($"SetLayer: [{objectInfo.LayerFullPath}] -> [{recipe.TargetLayerFullPath}]");
        }

        if (recipe.DisplayColor is not null)
        {
            string currentColor = FormatCurrentColor(currentObject);
            string targetColor = $"({recipe.DisplayColor.R},{recipe.DisplayColor.G},{recipe.DisplayColor.B})/ColorFromObject";
            messages.Add($"SetDisplayColor: {currentColor} -> {targetColor}");
        }

        if (!string.IsNullOrWhiteSpace(recipe.ObjectNameTemplate))
        {
            string currentName = currentObject.Attributes.Name ?? string.Empty;
            string nextName = ResolveTemplate(recipe.ObjectNameTemplate, objectInfo, currentObject, index);
            messages.Add($"SetName: [{currentName}] -> [{nextName}]");
        }

        foreach (string key in recipe.RemoveUserTextKeys)
        {
            string fromValue = currentObject.Attributes.GetUserString(key) ?? "<missing>";
            messages.Add($"RemoveUserText: {key} [{fromValue}] -> <removed>");
        }

        foreach (PreparedUserTextWrite write in recipe.UserTextWrites)
        {
            string fromValue = currentObject.Attributes.GetUserString(write.Key) ?? "<missing>";
            string nextValue = ResolveTemplate(write.ValueTemplate, objectInfo, currentObject, index);
            messages.Add($"SetUserText: {write.Key} [{fromValue}] -> [{nextValue}]");
        }
    }

    private static string ResolveTemplate(
        string template,
        RhinoObjectInfo objectInfo,
        RhinoObject currentObject,
        int index)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return TemplateTokenRegex.Replace(template, match =>
        {
            string token = match.Groups["token"].Value;
            if (token.StartsWith("user:", StringComparison.OrdinalIgnoreCase))
            {
                string key = token["user:".Length..].Trim();
                return string.IsNullOrWhiteSpace(key)
                    ? string.Empty
                    : currentObject.Attributes.GetUserString(key) ?? objectInfo.GetUserAttributeValue(key) ?? string.Empty;
            }

            return token.ToLowerInvariant() switch
            {
                "objectid" => objectInfo.ObjectId.ToString(),
                "objectname" => currentObject.Attributes.Name ?? objectInfo.Name,
                "layername" => objectInfo.LayerName,
                "layerfullpath" => objectInfo.LayerFullPath,
                "objecttype" => objectInfo.NormalizedObjectType.ToString(),
                "geometrytype" => objectInfo.GeometryTypeName,
                "index" => index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => match.Value
            };
        });
    }

    private static string FormatCurrentColor(RhinoObject currentObject)
    {
        Color currentColor = currentObject.Attributes.ObjectColor;
        return $"({currentColor.R},{currentColor.G},{currentColor.B})/{currentObject.Attributes.ColorSource}";
    }

    private static string BuildCriteriaSummary(
        RhinoObjectFilterResult selection,
        PreparedObjectAttributeRecipe recipe)
    {
        return $"{selection.CriteriaSummary}; AttributeRecipe=[{DescribeRecipe(recipe)}]";
    }

    private static string DescribeRecipe(PreparedObjectAttributeRecipe recipe)
    {
        var fragments = new List<string>();
        if (recipe.UserTextWrites.Count > 0)
        {
            fragments.Add($"UserTextWrites={recipe.UserTextWrites.Count}");
        }

        if (recipe.RemoveUserTextKeys.Count > 0)
        {
            fragments.Add($"RemoveUserTextKeys={recipe.RemoveUserTextKeys.Count}");
        }

        if (!string.IsNullOrWhiteSpace(recipe.TargetLayerFullPath))
        {
            fragments.Add($"TargetLayerFullPath={recipe.TargetLayerFullPath}");
        }

        if (recipe.DisplayColor is not null)
        {
            fragments.Add($"DisplayColor=({recipe.DisplayColor.R},{recipe.DisplayColor.G},{recipe.DisplayColor.B})");
        }

        if (!string.IsNullOrWhiteSpace(recipe.ObjectNameTemplate))
        {
            fragments.Add("ObjectNameTemplate");
        }

        return string.Join(", ", fragments);
    }

    private static ObjectEditOperationResult ToOperationResult(
        RhinoDoc document,
        RhinoObjectInfo objectInfo,
        OperationResponse<ObjectEditOperationResult> result)
    {
        if (result.Success && result.Data is not null)
        {
            return result.Data;
        }

        RhinoObject? currentObject = document.Objects.FindId(objectInfo.ObjectId);
        return new ObjectEditOperationResult
        {
            ObjectId = objectInfo.ObjectId,
            LayerFullPath = currentObject is null
                ? objectInfo.LayerFullPath
                : document.Layers.FindIndex(currentObject.Attributes.LayerIndex)?.FullPath ?? objectInfo.LayerFullPath,
            Success = false,
            Messages = new[] { result.Message }
        };
    }

    private sealed record PreparedUserTextWrite(string Key, string ValueTemplate);

    private sealed record PreparedObjectAttributeRecipe(
        IReadOnlyList<PreparedUserTextWrite> UserTextWrites,
        IReadOnlyList<string> RemoveUserTextKeys,
        string TargetLayerFullPath,
        ObjectColorRequest? DisplayColor,
        string ObjectNameTemplate,
        int? TargetLayerIndex,
        int OperationCount);
}
