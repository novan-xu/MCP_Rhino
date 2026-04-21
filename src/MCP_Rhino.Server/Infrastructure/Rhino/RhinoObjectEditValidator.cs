using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class RhinoObjectEditValidator : IObjectEditSpecValidator
{
    public OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(
        IReadOnlyList<RhinoObjectEditOperation> operations,
        IReadOnlyList<RhinoObjectInfo> matchedObjects)
    {
        if (operations.Count == 0)
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("At least one object edit operation is required.");
        }

        var warnings = new List<ObjectEditWarning>();

        foreach (RhinoObjectEditOperation operation in operations)
        {
            switch (operation.OperationType)
            {
                case ObjectEditOperationType.SetUserText:
                    if (string.IsNullOrWhiteSpace(operation.Key))
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("SetUserText requires a non-empty key.");
                    }
                    break;

                case ObjectEditOperationType.RemoveUserText:
                    if (string.IsNullOrWhiteSpace(operation.Key))
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("RemoveUserText requires a non-empty key.");
                    }

                    if (matchedObjects.Count > 0 && matchedObjects.All(obj => obj.GetUserAttributeValue(operation.Key) is null))
                    {
                        warnings.Add(new ObjectEditWarning
                        {
                            Code = "USER_TEXT_NOT_FOUND",
                            Message = $"No matched object contains user text key [{operation.Key}]."
                        });
                    }
                    break;

                case ObjectEditOperationType.SetLayer:
                    if (string.IsNullOrWhiteSpace(operation.TargetLayerFullPath))
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("SetLayer requires TargetLayerFullPath.");
                    }
                    break;

                case ObjectEditOperationType.SetDisplayColor:
                    if (operation.Color is null)
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("SetDisplayColor requires a color.");
                    }

                    if (operation.Color.R is < 0 or > 255
                        || operation.Color.G is < 0 or > 255
                        || operation.Color.B is < 0 or > 255)
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Display color values must be in range 0-255.");
                    }
                    break;
            }
        }

        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(warnings);
    }
}
