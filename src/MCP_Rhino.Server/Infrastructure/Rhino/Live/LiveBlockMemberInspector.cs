extern alias rhinocommon;

using System.Collections.Specialized;
using System.Diagnostics;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using rhinocommon::Rhino;
using rhinocommon::Rhino.DocObjects;
using rhinocommon::Rhino.Geometry;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveBlockMemberInspector : ILiveBlockMemberInspector
{
    private readonly ILiveRhinoDocumentAccessor _accessor;
    public LiveBlockMemberInspector(ILiveRhinoDocumentAccessor accessor) => _accessor = accessor;

    public OperationResponse<BlockMemberInspectionResponse> Inspect(BlockMemberInspectionRequest request)
        => _accessor.Execute(request.FilePath, doc => InspectDocument(doc, request));

    internal static OperationResponse<BlockMemberInspectionResponse> InspectDocument(RhinoDoc doc, BlockMemberInspectionRequest request)
    {
        var members = new List<BlockMemberResponse>();
        var clock = Stopwatch.StartNew();
        try
        {
            var roots = new List<InstanceObject>();
            foreach (Guid id in request.ObjectIds)
            {
                if (doc.Objects.FindId(id) is not InstanceObject root || root.IsDeleted || root.Attributes.IsInstanceDefinitionObject)
                    throw new InvalidOperationException($"Not a live top-level block instance: {id}");
                roots.Add(root);
            }
            foreach (InstanceObject root in roots)
                Visit(root, root.Id, Transform.Identity, new List<BlockMemberAncestorResponse>(), new HashSet<Guid>());
            CheckTime();
            return OperationResponse<BlockMemberInspectionResponse>.Ok(new BlockMemberInspectionResponse
            {
                FilePath = request.FilePath, UnitSystem = doc.ModelUnitSystem.ToString(), AbsoluteTolerance = doc.ModelAbsoluteTolerance,
                RootInstanceCount = roots.Count, Members = members
            }, "Block members inspected from live document without mutation.");
        }
        catch (Exception ex)
        {
            return OperationResponse<BlockMemberInspectionResponse>.Fail("BLOCK_MEMBER_INSPECTION_FAILED: " + ex.Message);
        }

        void Visit(InstanceObject instance, Guid rootId, Transform parent,
            List<BlockMemberAncestorResponse> ancestry, HashSet<Guid> activeDefinitions)
        {
            CheckTime();
            InstanceDefinition definition = instance.InstanceDefinition
                ?? throw new InvalidOperationException("Missing block definition.");
            if (definition.IsDeleted || ancestry.Count >= 32 || !activeDefinitions.Add(definition.Id))
                throw new InvalidOperationException("Missing/cyclic definition or nesting exceeds 32 levels.");
            Transform world = parent * instance.InstanceXform;
            if (!world.IsValid || Math.Abs(world.Determinant) < 1e-15
                || world.M30 != 0 || world.M31 != 0 || world.M32 != 0 || world.M33 != 1)
                throw new InvalidOperationException("Invalid, singular or non-affine block transform.");
            ancestry.Add(new BlockMemberAncestorResponse
            {
                InstanceObjectId = instance.Id, DefinitionId = definition.Id, DefinitionName = definition.Name,
                UserText = ReadText(instance.Attributes.GetUserStrings())
            });
            foreach (RhinoObject member in definition.GetObjects())
            {
                CheckTime();
                if (member.IsDeleted) throw new InvalidOperationException("Deleted member in active definition.");
                if (member is InstanceObject nested) Visit(nested, rootId, world, ancestry, activeDefinitions);
                else
                {
                    if (members.Count >= request.MaxLeaves)
                        throw new InvalidOperationException("Leaf limit exceeded; retry fewer roots or increase MaxLeaves.");
                    members.Add(new BlockMemberResponse
                    {
                        RootInstanceId = rootId, MemberObjectId = member.Id,
                        Name = member.Attributes.Name ?? string.Empty,
                        LayerFullPath = doc.Layers.FindIndex(member.Attributes.LayerIndex)?.FullPath ?? string.Empty,
                        GeometryType = member.Geometry.GetType().Name,
                        UserText = ReadText(member.Attributes.GetUserStrings()), Ancestry = ancestry.ToArray(),
                        WorldTransform = Enumerable.Range(0, 16).Select(i => world[i / 4, i % 4]).ToArray(),
                        Geometry = BlockMemberGeometryReader.Read(member.Geometry, world, doc.ModelAbsoluteTolerance)
                    });
                }
            }
            ancestry.RemoveAt(ancestry.Count - 1);
            activeDefinitions.Remove(definition.Id);
        }

        void CheckTime()
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(5))
                throw new InvalidOperationException("Inspection time limit exceeded; retry smaller batches.");
        }
    }

    private static Dictionary<string, string> ReadText(NameValueCollection text)
        => text.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => text[k!] ?? string.Empty, StringComparer.Ordinal);
}
