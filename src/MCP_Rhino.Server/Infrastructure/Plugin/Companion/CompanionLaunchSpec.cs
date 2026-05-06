namespace MCP_Rhino.Server.Infrastructure.Plugin.Companion;

internal sealed record CompanionLaunchSpec(
    string CompanionExecutablePath,
    string DocumentPath,
    uint RuntimeSerialNumber,
    string PipeName,
    string BridgeExecutablePath,
    string? ModelId = null);
