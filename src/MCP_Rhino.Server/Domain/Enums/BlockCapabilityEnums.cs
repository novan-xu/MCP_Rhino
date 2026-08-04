namespace MCP_Rhino.Server.Domain.Enums;

public enum BlockSourceObjectPolicy
{
    KeepVisible,
    HideSourceObjects
}

public enum BlockDuplicateDefinitionPolicy
{
    Reject,
    VersionedName
}

public enum BlockLinkStatus
{
    Local,
    Embedded,
    Linked,
    LinkedAndEmbedded,
    Reference,
    Unknown
}

public enum BlockPurgePolicy
{
    UnusedLocalOnly
}
