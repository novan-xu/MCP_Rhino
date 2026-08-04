extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoDocumentAccessor : LiveRhinoDocumentAccessorBase
{
    public override bool TryGetActiveDocumentState(string filePath, out bool hasUnsavedChanges)
    {
        bool matched = false;
        bool modified = false;

        InvokeOnMainThread(() =>
        {
            RhinoDoc? document = RhinoDoc.ActiveDoc;
            if (document is null || string.IsNullOrWhiteSpace(document.Path))
            {
                return OperationResponse<bool>.Ok(false);
            }

            if (PathsEqual(document.Path, filePath))
            {
                matched = true;
                modified = document.Modified;
            }

            return OperationResponse<bool>.Ok(true);
        });

        hasUnsavedChanges = modified;
        return matched;
    }

    protected override OperationResponse<RhinoDoc> ResolveDocument(string filePath)
    {
        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null)
        {
            return OperationResponse<RhinoDoc>.Fail("NO_ACTIVE_DOCUMENT");
        }

        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return OperationResponse<RhinoDoc>.Fail("ACTIVE_DOC_UNSAVED");
        }

        if (!PathsEqual(document.Path, filePath))
        {
            return OperationResponse<RhinoDoc>.Fail("FILE_NOT_ACTIVE");
        }

        return OperationResponse<RhinoDoc>.Ok(document);
    }
}
