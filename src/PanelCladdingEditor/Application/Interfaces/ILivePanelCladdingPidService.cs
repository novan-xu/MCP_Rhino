using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Interfaces;

public interface ILivePanelCladdingPidService
{
    OperationResponse<IReadOnlyList<Guid>> GetPanelObjectIds(uint documentSerialNumber);

    OperationResponse<PanelCladdingPidResult> Assign(uint documentSerialNumber, PanelCladdingPidRequest request);
}
