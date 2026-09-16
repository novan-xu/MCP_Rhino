namespace PanelCladdingEditor.Application.Interfaces;

/// <summary>Temporary viewport feedback owned by one editor window.</summary>
public interface IPanelViewportHighlight : IDisposable
{
    void SetTarget(uint documentRuntimeSerialNumber, Guid objectId);
    void SetVisible(bool visible);
}
