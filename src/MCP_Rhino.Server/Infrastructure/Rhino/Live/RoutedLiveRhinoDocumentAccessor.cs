extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class RoutedLiveRhinoDocumentAccessor : LiveRhinoDocumentAccessorBase
{
    private readonly uint _boundRuntimeSerialNumber;

    public RoutedLiveRhinoDocumentAccessor(uint boundRuntimeSerialNumber)
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

            matched = PathsEqual(document.Path, filePath);
            modified = matched && document.Modified;
            return OperationResponse<bool>.Ok(matched);
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

        if (!PathsEqual(document.Path, filePath))
        {
            return OperationResponse<RhinoDoc>.Fail("DOCUMENT_TARGET_CONFLICT");
        }

        return OperationResponse<RhinoDoc>.Ok(document);
    }
}
