namespace PanelCladdingEditor.Application.Services.PanelCladding;

public static class PanelCladdingCidService
{
    public const string UnitTypeUserTextKey = "CW_1.06_UNIT_TYPE";

    public static string ShortName(string cid)
    {
        string value = (cid ?? string.Empty).Trim();
        int prefixLength = value.StartsWith("CID_BKT_", StringComparison.OrdinalIgnoreCase) ? 8 :
            value.StartsWith("CID_", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
        return value.Length > prefixLength ? value[prefixLength..] : value;
    }

    public static string? PanelNameWrite(IReadOnlyDictionary<string, string> userText)
    {
        string cid = PanelCidWrite(userText) ??
            Get(userText, PanelCladdingSpawnPlanningService.CidUserTextKey).Trim();
        return cid.Length > 0 ? ShortName(cid) : null;
    }

    public static string FromPanelId(string panelId)
    {
        string pid = (panelId ?? string.Empty).Trim();
        return pid.StartsWith("PID_", StringComparison.OrdinalIgnoreCase) ? "CID_" + pid[4..] : pid;
    }

    public static string RoleSuffix(IReadOnlyDictionary<string, string> userText) =>
        UnitTypeSuffix(userText) ?? string.Empty;

    // Null means unspecified/unsupported; an empty suffix is an explicit flat panel.
    private static string? UnitTypeSuffix(IReadOnlyDictionary<string, string> userText) =>
        Get(userText, UnitTypeUserTextKey).Trim().ToLowerInvariant() switch
        {
            "flat" => string.Empty,
            "corner_parent" => "-P",
            "corner_child" => "-C",
            _ => null
        };

    public static string ResolvePanelCid(
        string panelId,
        IReadOnlyDictionary<string, string> userText,
        string? existingCid = null)
    {
        string canonical = FromPanelId(panelId);
        string? suffix = UnitTypeSuffix(userText);
        string stored = (existingCid ?? Get(userText, PanelCladdingSpawnPlanningService.CidUserTextKey)).Trim();
        if (suffix is not null)
        {
            return canonical + suffix;
        }
        return stored.Length == 0 ? canonical : stored;
    }

    public static string SurfaceCid(
        string panelId,
        string cellLabel,
        IReadOnlyDictionary<string, string> userText) =>
        $"{FromPanelId(panelId)}{RoleSuffix(userText)}-{cellLabel.Trim()}";

    // Explicit unit types own the canonical CID, including removing stale corner suffixes
    // for flat panels. Missing/unsupported unit types and missing PIDs are not rewritten.
    public static string? PanelCidWrite(IReadOnlyDictionary<string, string> userText)
    {
        string pid = Get(userText, PanelCladdingSpawnPlanningService.PanelIdUserTextKey).Trim();
        if (pid.Length == 0 || UnitTypeSuffix(userText) is null)
        {
            return null;
        }
        return ResolvePanelCid(pid, userText);
    }

    public static void AddPanelCidWrite(
        IDictionary<string, string> writes,
        IReadOnlyDictionary<string, string> userText)
    {
        if (PanelCidWrite(userText) is string cid)
        {
            writes[PanelCladdingSpawnPlanningService.CidUserTextKey] = cid;
        }
    }

    public static bool IncludesDependency(string panelId, string panelCid, string dependencyCid)
    {
        string canonical = FromPanelId(panelId);
        foreach (string suffix in new[] { "-P", "-C" })
        {
            if (dependencyCid.StartsWith(canonical + suffix + "-", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(panelCid, canonical + suffix, StringComparison.OrdinalIgnoreCase);
            }
        }
        return true; // Existing unsuffixed dependencies can be migrated by PCUpdate.
    }

    private static string Get(IReadOnlyDictionary<string, string> userText, string key) =>
        userText.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            .Value ?? string.Empty;
}
