extern alias rhinocommon;

using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

internal static class LivePanelCladdingCidService
{
    internal static IReadOnlyDictionary<string, string> Read(ObjectAttributes attributes)
    {
        var text = attributes.GetUserStrings();
        return (text?.AllKeys ?? Array.Empty<string?>())
            .Where(key => key is not null)
            .ToDictionary(key => key!, key => text![key!] ?? string.Empty, StringComparer.Ordinal);
    }

    internal static bool Normalize(ObjectAttributes attributes)
    {
        IReadOnlyDictionary<string, string> text = Read(attributes);
        string? cid = PanelCladdingCidService.PanelCidWrite(text);
        if (cid is null)
        {
            return false;
        }
        string key = PanelCladdingSpawnPlanningService.CidUserTextKey;
        string[] keys = text.Keys.Where(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (keys.Length == 1 && keys[0] == key && text[key] == cid)
        {
            return false;
        }
        foreach (string existing in keys)
        {
            attributes.DeleteUserString(existing);
        }
        attributes.SetUserString(key, cid);
        return true;
    }

    internal static OperationResponse<IReadOnlyList<Change>> Prepare(RhinoDoc document, IEnumerable<Guid> ids)
    {
        var changes = new List<Change>();
        foreach (Guid id in ids)
        {
            RhinoObject? panel = document.Objects.FindId(id);
            if (panel is null)
            {
                return OperationResponse<IReadOnlyList<Change>>.Fail($"PANEL_CLADDING_CID_PANEL_MISSING: {id:D}");
            }
            ObjectAttributes proposed = panel.Attributes.Duplicate();
            if (Normalize(proposed))
            {
                changes.Add(new Change(panel, panel.Attributes.Duplicate(), proposed));
            }
        }
        return OperationResponse<IReadOnlyList<Change>>.Ok(changes);
    }

    internal static OperationResponse Apply(RhinoDoc document, IReadOnlyList<Change> changes)
    {
        try
        {
            foreach (Change change in changes)
            {
                if (!document.Objects.ModifyAttributes(change.Panel, change.Proposed, quiet: true))
                {
                    return OperationResponse.Fail($"PANEL_CLADDING_CID_WRITE_FAILED: {change.Panel.Id:D}");
                }
                change.Applied = true;
            }
            return OperationResponse.Ok();
        }
        catch (Exception ex)
        {
            return OperationResponse.Fail($"PANEL_CLADDING_CID_WRITE_FAILED: {ex.Message}");
        }
    }

    internal static bool Restore(RhinoDoc document, IReadOnlyList<Change> changes)
    {
        bool restored = true;
        foreach (Change change in changes.Reverse().Where(change => change.Applied))
        {
            restored &= document.Objects.ModifyAttributes(change.Panel.Id, change.Original, quiet: true);
        }
        return restored;
    }

    internal sealed record Change(RhinoObject Panel, ObjectAttributes Original, ObjectAttributes Proposed)
    {
        public bool Applied { get; set; }
    }
}
