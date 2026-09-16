extern alias rhinocommon;

using System.Drawing;
using PanelCladdingEditor.Application.Interfaces;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using DisplayConduit = rhinocommon::Rhino.Display.DisplayConduit;
using DrawEventArgs = rhinocommon::Rhino.Display.DrawEventArgs;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace PanelCladdingEditor.Infrastructure.Rhino;

/// <summary>
/// View-only panel feedback. Called by the modeless editor on Rhino's UI thread;
/// geometry is resolved in Rhino's display callback and is never retained or changed.
/// </summary>
public sealed class LivePanelViewportHighlight : DisplayConduit, IPanelViewportHighlight
{
    private uint _documentSerial;
    private Guid _objectId;
    private bool _visible;
    private bool _disposed;

    public void SetTarget(uint documentRuntimeSerialNumber, Guid objectId)
    {
        if (_disposed) return;
        uint previousDocument = _documentSerial;
        _documentSerial = documentRuntimeSerialNumber;
        _objectId = objectId;
        UpdateEnabled();
        Redraw(previousDocument);
        if (previousDocument != _documentSerial) Redraw(_documentSerial);
    }

    public void SetVisible(bool visible)
    {
        if (_disposed || _visible == visible) return;
        _visible = visible;
        UpdateEnabled();
        Redraw(_documentSerial);
    }

    protected override void DrawForeground(DrawEventArgs e)
    {
        if (_disposed || !_visible || e.RhinoDoc is not { } doc ||
            doc.RuntimeSerialNumber != _documentSerial) return;

        var panel = doc.Objects.FindId(_objectId);
        if (panel is null || panel.IsDeleted || !panel.Visible ||
            !panel.IsActiveInViewport(e.Viewport) || panel.Geometry is not Brep brep) return;

        // DrawForeground has depth testing/writing disabled: this identification
        // overlay must remain legible on coplanar panels and generated cladding.
        // DrawBrepWires' integer is wire density, NOT pixel thickness. Draw actual
        // Brep edges with DrawCurve so trimmed boundaries receive a real halo.
        foreach (var edge in brep.Edges)
        {
            e.Display.DrawCurve(edge, Color.FromArgb(25, 55, 65), 6);
        }
        foreach (var edge in brep.Edges)
        {
            e.Display.DrawCurve(edge, Color.FromArgb(0, 230, 255), 3);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Enabled = false;
        Redraw(_documentSerial);
        _documentSerial = 0;
        _objectId = Guid.Empty;
    }

    private void UpdateEnabled() =>
        Enabled = _visible && _documentSerial != 0 && _objectId != Guid.Empty;

    private static void Redraw(uint serial)
    {
        if (serial != 0) RhinoDoc.FromRuntimeSerialNumber(serial)?.Views.Redraw();
    }
}
