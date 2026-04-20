using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private static PreviewObjectEditsRequest BuildPreviewObjectEditsRequest(string[] args)
    {
        return new PreviewObjectEditsRequest
        {
            FilePath = args[1],
            Operations = ParseEditOperations(args[2]),
            LayerQueries = ParseNamedCsv(args, "layers="),
            ConfirmedLayerFullPaths = ParseNamedCsv(args, "layerpaths="),
            ObjectTypes = ParseNamedCsv(args, "types="),
            UserAttributeConditions = ParseNamedUserAttributes(args, "attrs="),
            MatchMode = ParseNamedFilterMatchMode(args, "mode="),
            UserAttributeMatchMode = ParseNamedFilterMatchMode(args, "attrmode=")
        };
    }

    private static ApplyObjectEditsRequest BuildApplyObjectEditsRequest(string[] args)
    {
        return new ApplyObjectEditsRequest
        {
            FilePath = args[1],
            Operations = ParseEditOperations(args[2]),
            LayerQueries = ParseNamedCsv(args, "layers="),
            ConfirmedLayerFullPaths = ParseNamedCsv(args, "layerpaths="),
            ObjectTypes = ParseNamedCsv(args, "types="),
            UserAttributeConditions = ParseNamedUserAttributes(args, "attrs="),
            MatchMode = ParseNamedFilterMatchMode(args, "mode="),
            UserAttributeMatchMode = ParseNamedFilterMatchMode(args, "attrmode=")
        };
    }

    private static ObjectUserTextBatchWriteRequest BuildObjectUserTextBatchWriteRequest(string[] args)
    {
        return new ObjectUserTextBatchWriteRequest
        {
            FilePath = args[1],
            Entries = ParseObjectScopedUserTextEntries(args[2])
        };
    }

    private static List<ObjectEditOperationRequest> ParseEditOperations(string input)
    {
        var operations = new List<ObjectEditOperationRequest>();
        foreach (string token in input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separatorIndex = token.IndexOf(':');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException($"无效 editSpec 片段: {token}");
            }

            string operationName = token[..separatorIndex].Trim().ToLowerInvariant();
            string payload = token[(separatorIndex + 1)..].Trim();

            switch (operationName)
            {
                case "set-user":
                case "set-user-text":
                    int equalIndex = payload.IndexOf('=');
                    if (equalIndex <= 0)
                    {
                        throw new InvalidOperationException($"SetUserText 格式应为 set-user:key=value，收到: {token}");
                    }

                    operations.Add(new ObjectEditOperationRequest
                    {
                        OperationType = ObjectEditOperationType.SetUserText,
                        Key = payload[..equalIndex],
                        Value = payload[(equalIndex + 1)..]
                    });
                    break;

                case "remove-user":
                case "remove-user-text":
                    operations.Add(new ObjectEditOperationRequest
                    {
                        OperationType = ObjectEditOperationType.RemoveUserText,
                        Key = payload
                    });
                    break;

                case "set-layer":
                    operations.Add(new ObjectEditOperationRequest
                    {
                        OperationType = ObjectEditOperationType.SetLayer,
                        TargetLayerFullPath = payload
                    });
                    break;

                case "set-color":
                case "set-display-color":
                    string[] colorParts = payload.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (colorParts.Length != 3
                        || !int.TryParse(colorParts[0], out int r)
                        || !int.TryParse(colorParts[1], out int g)
                        || !int.TryParse(colorParts[2], out int b))
                    {
                        throw new InvalidOperationException($"SetDisplayColor 格式应为 set-color:r,g,b，收到: {token}");
                    }

                    operations.Add(new ObjectEditOperationRequest
                    {
                        OperationType = ObjectEditOperationType.SetDisplayColor,
                        Color = new ObjectColorRequest
                        {
                            R = r,
                            G = g,
                            B = b
                        }
                    });
                    break;

                default:
                    throw new InvalidOperationException($"未知编辑操作: {operationName}");
            }
        }

        return operations;
    }

    private static List<ObjectScopedUserTextEntryRequest> ParseObjectScopedUserTextEntries(string input)
    {
        var entries = new List<ObjectScopedUserTextEntryRequest>();
        foreach (string token in input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int pipeIndex = token.IndexOf('|');
            if (pipeIndex <= 0)
            {
                throw new InvalidOperationException($"无效 entrySpec 片段: {token}");
            }

            if (!Guid.TryParse(token[..pipeIndex], out Guid objectId))
            {
                throw new InvalidOperationException($"无效 ObjectId: {token[..pipeIndex]}");
            }

            string payload = token[(pipeIndex + 1)..].Trim();
            int equalIndex = payload.IndexOf('=');
            if (equalIndex <= 0)
            {
                throw new InvalidOperationException($"entrySpec 格式应为 objectId|key=value，收到: {token}");
            }

            entries.Add(new ObjectScopedUserTextEntryRequest
            {
                ObjectId = objectId,
                Key = payload[..equalIndex],
                Value = payload[(equalIndex + 1)..]
            });
        }

        return entries;
    }

    private static List<string> ParseNamedCsv(string[] args, string prefix)
    {
        string? token = args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return token is null ? new List<string>() : ParseCsv(token[prefix.Length..]);
    }

    private static List<UserAttributeConditionRequest> ParseNamedUserAttributes(string[] args, string prefix)
    {
        string? token = args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return token is null ? new List<UserAttributeConditionRequest>() : ParseUserAttributeConditions(token[prefix.Length..]);
    }

    private static FilterMatchMode ParseNamedFilterMatchMode(string[] args, string prefix)
    {
        string? token = args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return token is null ? FilterMatchMode.All : ParseFilterMatchMode(token[prefix.Length..]);
    }

    private static List<string> ParseCsv(string input)
    {
        return input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static List<UserAttributeConditionRequest> ParseUserAttributeConditions(string input)
    {
        var conditions = new List<UserAttributeConditionRequest>();
        foreach (string token in input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int containsIndex = token.IndexOf('~');
            int exactIndex = token.IndexOf('=');

            if (containsIndex > 0)
            {
                conditions.Add(new UserAttributeConditionRequest
                {
                    Key = token[..containsIndex],
                    ExpectedValue = token[(containsIndex + 1)..],
                    ComparisonMode = UserAttributeComparisonMode.Contains
                });
                continue;
            }

            if (exactIndex > 0)
            {
                conditions.Add(new UserAttributeConditionRequest
                {
                    Key = token[..exactIndex],
                    ExpectedValue = token[(exactIndex + 1)..],
                    ComparisonMode = UserAttributeComparisonMode.Exact
                });
                continue;
            }

            conditions.Add(new UserAttributeConditionRequest
            {
                Key = token,
                ComparisonMode = UserAttributeComparisonMode.Exists
            });
        }

        return conditions;
    }

    private static FilterMatchMode ParseFilterMatchMode(string value)
    {
        return Enum.TryParse<FilterMatchMode>(value, true, out var parsed)
            ? parsed
            : FilterMatchMode.All;
    }
}
