using System.Collections.Concurrent;
using Grasshopper;
using Grasshopper.Kernel;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live.Grasshopper;

public sealed class GrasshopperDefinitionRegistry
{
    private readonly ConcurrentDictionary<string, DefinitionEntry> _entries = new(StringComparer.Ordinal);

    public string GetOrCreate(GH_Document document, uint rhinoRuntimeSerialNumber)
    {
        foreach ((string id, DefinitionEntry entry) in _entries)
        {
            if (entry.RhinoRuntimeSerialNumber == rhinoRuntimeSerialNumber
                && entry.DocumentId == document.DocumentID
                && entry.Document.TryGetTarget(out GH_Document? current)
                && ReferenceEquals(current, document))
            {
                return id;
            }
        }

        string sessionId = $"gh1-{Guid.NewGuid():N}";
        _entries[sessionId] = new DefinitionEntry(
            document.DocumentID,
            rhinoRuntimeSerialNumber,
            new WeakReference<GH_Document>(document));
        Prune();
        return sessionId;
    }

    public bool TryResolve(string sessionId, uint rhinoRuntimeSerialNumber, out GH_Document? document)
    {
        document = null;
        if (!_entries.TryGetValue(sessionId, out DefinitionEntry? entry)
            || entry.RhinoRuntimeSerialNumber != rhinoRuntimeSerialNumber
            || !entry.Document.TryGetTarget(out GH_Document? candidate)
            || !Instances.DocumentServer.Contains(candidate)
            || candidate.RhinoDocument is null
            || candidate.RhinoDocument.RuntimeSerialNumber != rhinoRuntimeSerialNumber)
        {
            _entries.TryRemove(sessionId, out _);
            return false;
        }

        document = candidate;
        return true;
    }

    private void Prune()
    {
        foreach ((string key, DefinitionEntry entry) in _entries)
        {
            if (!entry.Document.TryGetTarget(out GH_Document? document) || !Instances.DocumentServer.Contains(document))
            {
                _entries.TryRemove(key, out _);
            }
        }
    }

    private sealed record DefinitionEntry(
        Guid DocumentId,
        uint RhinoRuntimeSerialNumber,
        WeakReference<GH_Document> Document);
}
