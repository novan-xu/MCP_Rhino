using System.Text.Json;

namespace MCP_Rhino.Offline.Contracts;

// JSON-only contract shared with the isolated RhinoCommon process; no Rhino types cross the boundary.
public sealed record RhinoWorkerRequest(string InputPath, string OutputPath, string Script,
    JsonElement Parameters, string? LibrarySource, string? LibraryType, string? ModelUnits);
public sealed record RhinoWorkerResult(bool Success, string? Error, int ObjectCountBefore,
    int ObjectCountAfter, int DefinitionCount, int LayerCount, string ModelUnits,
    string RhinoVersion, IReadOnlyList<string> Log, JsonElement Result);
public sealed record RhinoScriptPreviewResponse(string PreviewToken, string SourcePath, string SourceSha256,
    string ScriptSha256, string? LibrarySha256, DateTimeOffset ExpiresUtc, RhinoWorkerResult Document,
    string OutputNaming = "<source-stem>_<local-yyyyMMdd_HHmmss>.3dm");
