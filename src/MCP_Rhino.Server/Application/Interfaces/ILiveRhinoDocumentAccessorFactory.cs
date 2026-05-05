namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveRhinoDocumentAccessorFactory
{
    ILiveRhinoDocumentAccessor For(uint runtimeSerialNumber);
}
