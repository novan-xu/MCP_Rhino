using MCP_Rhino.Server.Application.Interfaces;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoDocumentAccessorFactory : ILiveRhinoDocumentAccessorFactory
{
    public ILiveRhinoDocumentAccessor For(uint runtimeSerialNumber)
    {
        return new BoundLiveRhinoDocumentAccessor(runtimeSerialNumber);
    }
}
