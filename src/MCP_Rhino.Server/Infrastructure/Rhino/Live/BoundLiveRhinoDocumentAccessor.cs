extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class BoundLiveRhinoDocumentAccessor : LiveRhinoDocumentAccessorBase
{
    private readonly uint _boundRuntimeSerialNumber;

    public BoundLiveRhinoDocumentAccessor(uint boundRuntimeSerialNumber)
    {
        _boundRuntimeSerialNumber = boundRuntimeSerialNumber;
    }

    public override bool TryGetActiveDocumentState(string filePath, out bool hasUnsavedChanges)
    {
        bool matched = false;
        bool modified = false;

        InvokeOnMainThread(() =>
        {
            RhinoDoc? document = RhinoDoc.FromRuntimeSerialNumber(_boundRuntimeSerialNumber);
            if (document is null || string.IsNullOrWhiteSpace(document.Path))
            {
                return OperationResponse<bool>.Ok(false);
            }

            matched = true;
            modified = document.Modified;
            return OperationResponse<bool>.Ok(true);
        });

        hasUnsavedChanges = modified;
        return matched;
    }

    protected override OperationResponse<RhinoDoc> ResolveDocument(string filePath)
    {
        RhinoDoc? document = RhinoDoc.FromRuntimeSerialNumber(_boundRuntimeSerialNumber);
        if (document is null)
        {
            return OperationResponse<RhinoDoc>.Fail("DOCUMENT_CLOSED");
        }

        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return OperationResponse<RhinoDoc>.Fail("ACTIVE_DOC_UNSAVED");
        }

        return OperationResponse<RhinoDoc>.Ok(document);
    }
}
