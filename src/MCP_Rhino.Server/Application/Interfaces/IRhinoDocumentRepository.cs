using Rhino.FileIO;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IRhinoDocumentRepository
{
    bool Exists(string filePath);
    File3dm Read(string filePath);
    bool Write(File3dm model, string filePath);
}