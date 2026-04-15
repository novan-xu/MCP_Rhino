using MCP_Rhino.Server.Application.Interfaces;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class RhinoDocumentRepository : IRhinoDocumentRepository
{
    public bool Exists(string filePath)
    {
        return File.Exists(filePath);
    }

    public File3dm Read(string filePath)
    {
        return File3dm.Read(filePath);
    }

    public bool Write(File3dm model, string filePath)
    {
        return model.Write(filePath, 0);
    }
}