extern alias rhinocommon;

using System.Collections;
using System.Drawing;
using System.Reflection;
using PlugIn = rhinocommon::Rhino.PlugIns.PlugIn;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using PanelType = rhinocommon::Rhino.UI.PanelType;
using Panels = rhinocommon::Rhino.UI.Panels;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Panel;

public sealed class PerDocumentPanelDispatcher : IDisposable
{
    private readonly RhinoChatPanelHost _host;
    private bool _started;

    public PerDocumentPanelDispatcher(RhinoChatPanelHost host)
    {
        _host = host;
    }

    public static void RegisterPanel(PlugIn plugin, Guid pluginId)
    {
        if (plugin.Id != Guid.Empty)
        {
            Panels.RegisterPanel(plugin, typeof(RhinoChatPanel), "Claude Code Chat", null, PanelType.PerDoc);
            return;
        }

        RegisterPanelByPluginId(pluginId);
    }

    private static void RegisterPanelByPluginId(Guid pluginId)
    {
        Type panelSystemType = typeof(Panels).Assembly.GetType("Rhino.UI.PanelSystem", throwOnError: true)
            ?? throw new InvalidOperationException("Rhino.UI.PanelSystem type was not found.");
        MethodInfo registerMethod = panelSystemType.GetMethod(
            "Register",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(Guid), typeof(Type), typeof(string), typeof(Icon), typeof(PanelType) },
            modifiers: null)
            ?? throw new InvalidOperationException("Rhino.UI.PanelSystem.Register(Guid, Type, string, Icon, PanelType) was not found.");

        registerMethod.Invoke(null, new object?[] { pluginId, typeof(RhinoChatPanel), "Claude Code Chat", null, PanelType.PerDoc });
    }

    public void Start(bool startOpenDocuments = true)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        RhinoDoc.NewDocument += OnDocumentEvent;
        RhinoDoc.EndOpenDocument += OnDocumentEvent;
        RhinoDoc.EndSaveDocument += OnEndSaveDocument;
        RhinoDoc.CloseDocument += OnCloseDocument;

        if (!startOpenDocuments)
        {
            return;
        }

        foreach (RhinoDoc document in GetOpenDocuments())
        {
            TryStartDocument(document);
        }
    }

    public bool TryShowDocument(RhinoDoc document)
    {
        return TryStartDocument(document);
    }

    public void Dispose()
    {
        if (_started)
        {
            RhinoDoc.NewDocument -= OnDocumentEvent;
            RhinoDoc.EndOpenDocument -= OnDocumentEvent;
            RhinoDoc.EndSaveDocument -= OnEndSaveDocument;
            RhinoDoc.CloseDocument -= OnCloseDocument;
            _started = false;
        }

        _host.Dispose();
    }

    private void OnDocumentEvent(object? sender, EventArgs e)
    {
        TryStartDocument(GetDocument(sender, e));
    }

    private void OnEndSaveDocument(object? sender, EventArgs e)
    {
        RhinoDoc? document = GetDocument(sender, e);
        if (document is null)
        {
            return;
        }

        _host.UpdateDocumentPath(document);
    }

    private void OnCloseDocument(object? sender, EventArgs e)
    {
        RhinoDoc? document = GetDocument(sender, e);
        if (document is null)
        {
            return;
        }

        _host.StopDocument(document.RuntimeSerialNumber, closePanel: true);
    }

    private bool TryStartDocument(RhinoDoc? document)
    {
        if (document is null || string.IsNullOrWhiteSpace(document.Path))
        {
            return false;
        }

        try
        {
            _host.StartDocument(document);
            return true;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP_Rhino panel startup failed for document {document.RuntimeSerialNumber}: {ex}");
            return false;
        }
    }

    private static IEnumerable<RhinoDoc> GetOpenDocuments()
    {
        object? openDocuments = typeof(RhinoDoc).GetProperty("OpenDocuments")?.GetValue(null);
        if (openDocuments is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                if (item is RhinoDoc document)
                {
                    yield return document;
                }
            }
        }
    }

    private static RhinoDoc? GetDocument(object? sender, EventArgs e)
    {
        if (sender is RhinoDoc senderDocument)
        {
            return senderDocument;
        }

        object? document = e.GetType().GetProperty("Document")?.GetValue(e);
        return document as RhinoDoc;
    }
}
