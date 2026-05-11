using System.ComponentModel;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public abstract class ObjectAttributeRecipeRequestBase
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> LayerQueries { get; set; } = new();
    public List<string> ConfirmedLayerFullPaths { get; set; } = new();
    public List<string> ObjectTypes { get; set; } = new();
    public List<UserAttributeConditionRequest> UserAttributeConditions { get; set; } = new();
    public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.All;
    public FilterMatchMode UserAttributeMatchMode { get; set; } = FilterMatchMode.All;
    public List<ObjectAttributeUserTextRecipeRequest> UserTextWrites { get; set; } = new();
    public List<string> RemoveUserTextKeys { get; set; } = new();
    public string TargetLayerFullPath { get; set; } = string.Empty;
    public ObjectColorRequest? DisplayColor { get; set; }
    public string ObjectNameTemplate { get; set; } = string.Empty;
}

public sealed class PreviewBulkObjectAttributeRecipeRequest : ObjectAttributeRecipeRequestBase
{
}

public sealed class ApplyBulkObjectAttributeRecipeRequest : ObjectAttributeRecipeRequestBase
{
}

public sealed class ObjectAttributeUserTextRecipeRequest
{
    [Description("User text key to write on every matched object.")]
    public string Key { get; set; } = string.Empty;

    [Description("Value template resolved per matched object. Supports {objectId}, {objectName}, {layerName}, {layerFullPath}, {objectType}, {geometryType}, {index}, and {user:<key>}. Literal values without tokens are allowed.")]
    public string ValueTemplate { get; set; } = string.Empty;
}
