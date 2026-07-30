extern alias rhinocommon;

using System.Collections.Specialized;
using System.Drawing;
using System.Text;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Group = rhinocommon::Rhino.DocObjects.Group;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveLotVisualizationOperator : ILiveLotVisualizationOperator
{
    private static readonly string[] PreferredLotKeys =
    {
        "Lot Number", "LotNumber", "Lot_Number", "Lot No", "Lot_No", "Lot", "Element Lot"
    };

    private static readonly (int R, int G, int B)[] Palette =
    {
        (31, 119, 180), (255, 127, 14), (44, 160, 44), (214, 39, 40),
        (148, 103, 189), (140, 86, 75), (227, 119, 194), (55, 79, 180),
        (188, 189, 34), (23, 190, 207), (127, 127, 127), (174, 199, 232),
        (255, 187, 120), (152, 223, 138), (255, 152, 150), (197, 176, 213),
        (196, 156, 148), (247, 182, 210), (219, 219, 141), (158, 218, 229)
    };

    public OperationResponse<LotVisualizationPlan> Preview(RhinoDoc document, LotVisualizationSpec spec)
    {
        List<RhinoObject> targets = ResolveTargets(document, spec.ObjectIds);
        if (targets.Count == 0)
        {
            return OperationResponse<LotVisualizationPlan>.Fail("LOT_VISUALIZATION_TARGET_EMPTY");
        }

        IReadOnlyList<string> keys = ResolveLotKeys(targets, spec.LotNumberKeys);
        var grouped = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        var unassigned = new List<Guid>();
        int conflictCount = 0;

        foreach (RhinoObject target in targets)
        {
            IReadOnlyList<string> values = ResolveEffectiveValues(target, keys, spec.IneffectiveLotValues);
            if (values.Count == 0)
            {
                unassigned.Add(target.Id);
                continue;
            }

            if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            {
                conflictCount++;
            }

            string lot = values[0];
            if (!grouped.TryGetValue(lot, out List<Guid>? objectIds))
            {
                objectIds = new List<Guid>();
                grouped[lot] = objectIds;
            }

            objectIds.Add(target.Id);
        }

        List<LotVisualizationLotPlan> lots = grouped
            .OrderBy(pair => pair.Key, NaturalStringComparer.Instance)
            .Select((pair, index) => new LotVisualizationLotPlan
            {
                LotNumber = pair.Key,
                GroupName = CreateGroupName(spec.GroupPrefix, pair.Key),
                Color = CreateColor(index),
                ObjectIds = pair.Value
            })
            .ToList();

        var warnings = new List<ObjectEditWarning>();
        if (keys.Count == 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "LOT_NUMBER_KEY_NOT_FOUND",
                Message = "No conservative lot-number user-text key was found; all target geometry will remain white."
            });
        }

        if (conflictCount > 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "LOT_NUMBER_VALUE_CONFLICT",
                Message = $"{conflictCount} objects had multiple effective lot values; the first value in resolved key order was used."
            });
        }

        IReadOnlyList<string> layers = targets
            .Select(target => target.Attributes.LayerIndex)
            .Distinct()
            .Select(index => document.Layers.FindIndex(index)?.FullPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return OperationResponse<LotVisualizationPlan>.Ok(new LotVisualizationPlan
        {
            TargetObjectIds = targets.Select(target => target.Id).ToList(),
            LayerFullPaths = layers,
            ResolvedLotNumberKeys = keys,
            Lots = lots,
            UnassignedObjectIds = unassigned,
            Warnings = warnings
        }, "Lot visualization plan resolved.");
    }

    public OperationResponse<LotVisualizationApplyResult> Apply(
        RhinoDoc document,
        LotVisualizationPlan plan,
        string groupPrefix)
    {
        var originalAttributes = new Dictionary<Guid, ObjectAttributes>();
        var createdGroupNames = new List<string>();
        var groupIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int reusedGroupCount = 0;

        try
        {
            foreach (LotVisualizationLotPlan lot in plan.Lots)
            {
                Group? existing = document.Groups.FindName(lot.GroupName);
                if (existing is not null)
                {
                    groupIndexByName[lot.GroupName] = existing.Index;
                    reusedGroupCount++;
                    continue;
                }

                int added = document.Groups.Add(lot.GroupName);
                if (added < 0)
                {
                    return OperationResponse<LotVisualizationApplyResult>.Fail($"LOT_GROUP_CREATE_FAILED: {lot.GroupName}");
                }

                createdGroupNames.Add(lot.GroupName);
            }

            foreach (LotVisualizationLotPlan lot in plan.Lots)
            {
                Group? group = document.Groups.FindName(lot.GroupName);
                if (group is null)
                {
                    return RollBack(document, originalAttributes, createdGroupNames, $"LOT_GROUP_RESOLVE_FAILED: {lot.GroupName}");
                }

                groupIndexByName[lot.GroupName] = group.Index;
            }

            HashSet<int> ownedGroupIndices = FindOwnedGroupIndices(document, groupPrefix);
            var lotByObjectId = plan.Lots
                .SelectMany(lot => lot.ObjectIds.Select(objectId => (ObjectId: objectId, Lot: lot)))
                .ToDictionary(pair => pair.ObjectId, pair => pair.Lot);

            int modifiedCount = 0;
            foreach (Guid objectId in plan.TargetObjectIds)
            {
                RhinoObject? rhinoObject = document.Objects.FindId(objectId);
                if (rhinoObject is null || rhinoObject.IsDeleted)
                {
                    return RollBack(document, originalAttributes, createdGroupNames, $"OBJECT_NOT_FOUND: {objectId}");
                }

                originalAttributes[objectId] = rhinoObject.Attributes.Duplicate();
                ObjectAttributes attributes = rhinoObject.Attributes.Duplicate();
                foreach (int groupIndex in attributes.GetGroupList().Where(ownedGroupIndices.Contains))
                {
                    attributes.RemoveFromGroup(groupIndex);
                }

                if (lotByObjectId.TryGetValue(objectId, out LotVisualizationLotPlan? lot))
                {
                    attributes.ObjectColor = Color.FromArgb(255, lot.Color.R, lot.Color.G, lot.Color.B);
                    attributes.AddToGroup(groupIndexByName[lot.GroupName]);
                }
                else
                {
                    attributes.ObjectColor = Color.White;
                }

                attributes.ColorSource = ObjectColorSource.ColorFromObject;
                if (!document.Objects.ModifyAttributes(objectId, attributes, true))
                {
                    return RollBack(document, originalAttributes, createdGroupNames, $"LOT_OBJECT_ATTRIBUTE_FAILED: {objectId}");
                }

                modifiedCount++;
            }

            document.Views.Redraw();
            return OperationResponse<LotVisualizationApplyResult>.Ok(new LotVisualizationApplyResult
            {
                Plan = plan,
                ModifiedObjectCount = modifiedCount,
                CreatedGroupCount = createdGroupNames.Count,
                ReusedGroupCount = reusedGroupCount
            }, "Lot groups and colors applied.");
        }
        catch (Exception ex)
        {
            return RollBack(document, originalAttributes, createdGroupNames, $"LOT_VISUALIZATION_APPLY_FAILED: {ex.Message}");
        }
    }

    private static List<RhinoObject> ResolveTargets(RhinoDoc document, IReadOnlyList<Guid> objectIds)
    {
        IEnumerable<RhinoObject> candidates = objectIds.Count == 0
            ? document.Objects
            : objectIds.Distinct().Select(document.Objects.FindId).Where(item => item is not null)!;

        return candidates
            .Where(IsVisibleGeometry)
            .OrderBy(item => item.Id)
            .ToList();
    }

    private static bool IsVisibleGeometry(RhinoObject rhinoObject)
    {
        return !rhinoObject.IsDeleted
            && !rhinoObject.IsHidden
            && rhinoObject.Attributes.Visible
            && rhinoObject.Geometry is not null;
    }

    private static IReadOnlyList<string> ResolveLotKeys(
        IReadOnlyList<RhinoObject> targets,
        IReadOnlyList<string> requestedKeys)
    {
        Dictionary<string, string> discovered = targets
            .SelectMany(target => GetUserText(target).Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(key => key, key => key, StringComparer.OrdinalIgnoreCase);

        if (requestedKeys.Count > 0)
        {
            return requestedKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(key => discovered.TryGetValue(key, out string? actual) ? actual : key)
                .ToList();
        }

        var resolved = new List<string>();
        foreach (string preferred in PreferredLotKeys)
        {
            if (discovered.TryGetValue(preferred, out string? actual)
                && !resolved.Contains(actual, StringComparer.OrdinalIgnoreCase))
            {
                resolved.Add(actual);
            }
        }

        resolved.AddRange(discovered.Values
            .Where(IsConservativeLotKey)
            .Where(key => !resolved.Contains(key, StringComparer.OrdinalIgnoreCase))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        return resolved;
    }

    private static IReadOnlyList<string> ResolveEffectiveValues(
        RhinoObject target,
        IReadOnlyList<string> keys,
        IReadOnlySet<string> ineffectiveValues)
    {
        Dictionary<string, string> userText = GetUserText(target);
        var values = new List<string>();
        foreach (string key in keys)
        {
            if (!userText.TryGetValue(key, out string? raw))
            {
                continue;
            }

            string value = raw.Trim();
            if (value.Length == 0 || ineffectiveValues.Contains(value))
            {
                continue;
            }

            values.Add(value);
        }

        return values;
    }

    private static Dictionary<string, string> GetUserText(RhinoObject target)
    {
        NameValueCollection entries = target.Attributes.GetUserStrings();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? key in entries.AllKeys)
        {
            if (!string.IsNullOrWhiteSpace(key) && entries[key] is string value)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static bool IsConservativeLotKey(string key)
    {
        string[] tokens = key
            .ToLowerInvariant()
            .Split(new[] { ' ', '_', '-', '.', ':', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (!tokens.Contains("lot", StringComparer.Ordinal))
        {
            return false;
        }

        return tokens.Length == 1
            || tokens[^1] == "lot"
            || tokens.Any(token => token is "number" or "num" or "no" or "id");
    }

    private static HashSet<int> FindOwnedGroupIndices(RhinoDoc document, string groupPrefix)
    {
        var result = new HashSet<int>();
        for (int index = 0; index < document.Groups.Count; index++)
        {
            Group? group = document.Groups.FindIndex(index);
            if (group is not null
                && !document.Groups.IsDeleted(index)
                && group.Name.StartsWith(groupPrefix, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(index);
            }
        }

        return result;
    }

    private static OperationResponse<LotVisualizationApplyResult> RollBack(
        RhinoDoc document,
        IReadOnlyDictionary<Guid, ObjectAttributes> originals,
        IReadOnlyList<string> createdGroupNames,
        string message)
    {
        foreach ((Guid objectId, ObjectAttributes attributes) in originals)
        {
            document.Objects.ModifyAttributes(objectId, attributes, true);
        }

        foreach (string groupName in createdGroupNames.Reverse())
        {
            Group? group = document.Groups.FindName(groupName);
            if (group is not null && document.Groups.GroupMembers(group.Index).Length == 0)
            {
                document.Groups.Delete(group.Index);
            }
        }

        document.Views.Redraw();
        return OperationResponse<LotVisualizationApplyResult>.Fail(message);
    }

    private static RhinoDisplayColor CreateColor(int index)
    {
        if (index < Palette.Length)
        {
            (int r, int g, int b) = Palette[index];
            return new RhinoDisplayColor { R = r, G = g, B = b };
        }

        double hue = (index * 137.50776405003785d) % 360d;
        Color color = FromHsv(hue, 0.68d, 0.78d);
        return new RhinoDisplayColor { R = color.R, G = color.G, B = color.B };
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        double chroma = value * saturation;
        double segment = hue / 60d;
        double x = chroma * (1d - Math.Abs((segment % 2d) - 1d));
        (double r, double g, double b) = segment switch
        {
            < 1d => (chroma, x, 0d),
            < 2d => (x, chroma, 0d),
            < 3d => (0d, chroma, x),
            < 4d => (0d, x, chroma),
            < 5d => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        double m = value - chroma;
        return Color.FromArgb(
            255,
            (int)Math.Round((r + m) * 255d),
            (int)Math.Round((g + m) * 255d),
            (int)Math.Round((b + m) * 255d));
    }

    private static string CreateGroupName(string prefix, string lotNumber)
    {
        var sanitized = new StringBuilder();
        foreach (char character in lotNumber)
        {
            sanitized.Append(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '_');
        }

        string compact = sanitized.ToString().Trim('_');
        if (compact.Length > 40)
        {
            compact = compact[..40];
        }

        if (compact.Length == 0)
        {
            compact = "lot";
        }

        return $"{prefix}{compact}_{Fnv1a(lotNumber):x8}";
    }

    private static uint Fnv1a(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        uint hash = offset;
        foreach (byte item in Encoding.UTF8.GetBytes(value.ToUpperInvariant()))
        {
            hash ^= item;
            hash *= prime;
        }

        return hash;
    }

    private sealed class NaturalStringComparer : IComparer<string>
    {
        public static NaturalStringComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(left, right);
        }
    }
}
