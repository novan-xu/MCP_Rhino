namespace PanelCladdingEditor.Application.Services.PanelCladding;

public static class PanelCladdingCidService
{
    public static string FromPanelId(string panelId)
    {
        string pid = (panelId ?? string.Empty).Trim();
        return pid.StartsWith("PID_", StringComparison.OrdinalIgnoreCase) ? "CID_" + pid[4..] : pid;
    }

    public static string RoleSuffix(IReadOnlyDictionary<string, string> userText) =>
        Get(userText, "parent").Trim() == "1" ? "-P" :
        Get(userText, "child").Trim() == "1" ? "-C" : string.Empty;

    public static string ResolvePanelCid(
        string panelId,
        IReadOnlyDictionary<string, string> userText,
        string? existingCid = null)
    {
        string canonical = FromPanelId(panelId);
        string suffix = RoleSuffix(userText);
        string stored = (existingCid ?? Get(userText, PanelCladdingSpawnPlanningService.CidUserTextKey)).Trim();
        if (suffix.Length > 0)
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

    // Only role-related panel metadata belongs to this behavior change. Ordinary custom
    // CIDs and panels without PID metadata retain their existing save behavior.
    public static string? PanelCidWrite(IReadOnlyDictionary<string, string> userText)
    {
        string pid = Get(userText, PanelCladdingSpawnPlanningService.PanelIdUserTextKey).Trim();
        if (pid.Length == 0 || RoleSuffix(userText).Length == 0)
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
