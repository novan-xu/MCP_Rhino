using MCP_Rhino.Offline.Contracts;

namespace MCP_Rhino.Offline.Application.Interfaces;

public interface IOfflineRhinoSource : IDisposable
{
    string SourcePath { get; }
    string SourceSha256 { get; }
    byte[] Snapshot { get; }
}
public interface IOfflineRhinoStore
{
    IOfflineRhinoSource OpenReadOnly(string filePath);
    (string Source, string Hash) ReadLibrary(string libraryPath);
    void ValidateOutput(byte[] bytes, RhinoWorkerResult result);
    string PublishCopy(string sourcePath, byte[] bytes, CancellationToken cancellationToken);
}
public sealed record RhinoJobOutput(byte[] Bytes, RhinoWorkerResult Report);
public interface IRhinoJobExecutor
{
    Task<RhinoJobOutput> ExecuteAsync(byte[] source, string extension, string script,
        string parametersJson, string? librarySource, string? libraryType, string? modelUnits,
        int timeoutSeconds, CancellationToken cancellationToken);
}
