using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Rules;

public static class SurfaceDirectionTweakDefaults
{
    public static IReadOnlyList<SurfaceDirectionTweakKind> StandardPostRebuildOperations()
    {
        return new[]
        {
            SurfaceDirectionTweakKind.SwapUV
        };
    }

    public static IReadOnlyList<SurfaceDirectionTweakKind> NormalizeOperations(
        IReadOnlyList<SurfaceDirectionTweakKind>? operations)
    {
        return operations is null || operations.Count == 0
            ? StandardPostRebuildOperations()
            : operations.ToList();
    }
}
