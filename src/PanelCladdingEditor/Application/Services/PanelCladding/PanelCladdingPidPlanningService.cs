using System.Globalization;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingPidPlanningService
{
    public const string ElevationKey = "CW_1.03_ELEVATION";
    public const string LevelKey = "CW_1.04_LEVEL";
    public const string PanelLayerRoot = PanelCladdingExtrusionPlanningService.PanelSurfaceLayerRootPath;

    public static bool IsPanelLayerPath(string layerPath) =>
        string.Equals(layerPath, PanelLayerRoot, StringComparison.OrdinalIgnoreCase) ||
        layerPath.StartsWith(PanelLayerRoot + "::", StringComparison.OrdinalIgnoreCase);

    public static bool IsProjectCode(string code) =>
        code.Trim().Length == 3 && code.Trim().All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    public OperationResponse<PanelCladdingPidPlan> CreatePlan(
        PanelCladdingPidRequest request,
        IReadOnlyList<PanelCladdingPidPanel> panels,
        double tolerance,
        double angleTolerance)
    {
        if (!IsProjectCode(request.ProjectCode)) return Fail("Enter exactly three letters for the project code.");
        if (!double.IsFinite(tolerance) || tolerance <= 0 || !double.IsFinite(angleTolerance) ||
            angleTolerance <= 0 || angleTolerance >= Math.PI / 4)
            return Fail("Invalid document linear/angular tolerance.");
        var ids = request.PanelObjectIds.ToHashSet();
        var contextIds = panels.Select(p => p.ObjectId).ToHashSet();
        if (ids.Count == 0 || ids.Contains(Guid.Empty) || contextIds.Contains(Guid.Empty) ||
            contextIds.Count != panels.Count || !ids.IsSubsetOf(contextIds))
            return Fail("The selected panels could not all be read from the panel-layer scope.");
        if (!contextIds.Contains(request.NorthPanelObjectId) || !contextIds.Contains(request.FirstFloorPanelObjectId))
            return Fail("The north and first-floor references must belong to the panel-layer scope.");

        var measured = new List<Measured>();
        foreach (var panel in panels)
        {
            double length = Math.Sqrt(Dot(panel.Normal, panel.Normal));
            if (!Finite(panel.Normal) || !double.IsFinite(length) || length < 1e-12 ||
                panel.Bounds.Count < 4 || panel.Bounds.Any(p => !Finite(p)))
                return Fail($"Invalid panel geometry: {panel.ObjectId}.");
            var normal = Scale(panel.Normal, 1 / length);
            if (Math.Abs(normal.Z) > Math.Sin(angleTolerance))
                return Fail($"Panel {panel.ObjectId} is not vertical. PCpid requires vertical facade panels.");
            var origin = panel.Bounds[0];
            if (panel.Bounds.Any(p => Math.Abs(Dot(Subtract(p, origin), normal)) > tolerance))
                return Fail($"Panel {panel.ObjectId} is not planar within the document tolerance.");
            var horizontal = Scale(new PanelPoint3(normal.X, normal.Y, 0),
                1 / Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y));
            var right = new PanelPoint3(-horizontal.Y, horizontal.X, 0);
            if (panel.Bounds.Max(p => p.Z) - panel.Bounds.Min(p => p.Z) <= tolerance ||
                panel.Bounds.Max(p => Dot(p, right)) - panel.Bounds.Min(p => Dot(p, right)) <= tolerance)
                return Fail($"Panel {panel.ObjectId} has degenerate bounds.");
            measured.Add(new(panel, normal, horizontal, origin, panel.Bounds.Min(p => p.Z)));
        }

        var north = measured.Single(p => p.Panel.ObjectId == request.NorthPanelObjectId).Horizontal;
        var east = new PanelPoint3(north.Y, -north.X, 0);
        var directions = new[] { (Code: "N", Normal: north), (Code: "E", Normal: east),
            (Code: "S", Normal: Scale(north, -1)), (Code: "W", Normal: Scale(east, -1)) };
        var facing = new Dictionary<Guid, string>();
        foreach (var panel in measured)
        {
            var ranked = directions.OrderByDescending(d => Dot(panel.Horizontal, d.Normal)).ToArray();
            if (Dot(panel.Horizontal, ranked[0].Normal) - Dot(panel.Horizontal, ranked[1].Normal) <= Math.Sin(angleTolerance))
                return Fail($"Panel {panel.Panel.ObjectId} faces between two cardinal elevations; its direction is ambiguous.");
            facing.Add(panel.Panel.ObjectId, ranked[0].Code);
        }

        var rows = Cluster(measured, p => p.Bottom, tolerance);
        int firstFloor = rows.FindIndex(row => row.Any(p => p.Panel.ObjectId == request.FirstFloorPanelObjectId));
        var levels = rows.SelectMany((row, index) => row.Select(p => (p.Panel.ObjectId, Level: Number(index - firstFloor + 1))))
            .ToDictionary(p => p.ObjectId, p => p.Level);
        var assignments = new List<PanelCladdingPidAssignment>();
        foreach (var direction in directions)
        {
            var viewRight = new PanelPoint3(-direction.Normal.Y, direction.Normal.X, 0);
            double Left(Measured p) => p.Panel.Bounds.Min(point => Dot(point, viewRight));
            var planes = new List<List<Measured>>();
            // Geometric ordering makes the representative independent of selection order.
            foreach (var panel in measured.Where(p => facing[p.Panel.ObjectId] == direction.Code)
                .OrderBy(Left).ThenBy(p => p.Bottom).ThenBy(p => Dot(p.Origin, direction.Normal))
                .ThenBy(p => p.Panel.ObjectId))
            {
                var matches = planes.Where(group => SamePlane(group[0], panel, tolerance, angleTolerance)).ToArray();
                if (matches.Length > 1) return Fail($"Ambiguous elevation plane for panel {panel.Panel.ObjectId}.");
                if (matches.Length == 1) matches[0].Add(panel);
                else planes.Add([panel]);
            }
            // Tolerance-aware left ties are ordered bottom-to-top, then by plane depth.
            var orderedPlanes = Cluster(planes, group => group.Min(Left), tolerance)
                .SelectMany(column => Cluster(column, group => group.Min(p => p.Bottom), tolerance)
                    .SelectMany(row => row.OrderBy(group => Dot(group[0].Origin, direction.Normal))))
                .ToArray();
            for (int planeIndex = 0; planeIndex < orderedPlanes.Length; planeIndex++)
            {
                var plane = orderedPlanes[planeIndex];
                string elevation = direction.Code + (planeIndex + 1).ToString(CultureInfo.InvariantCulture);
                var planeRight = new PanelPoint3(-plane[0].Horizontal.Y, plane[0].Horizontal.X, 0);
                var bays = Cluster(plane, p => p.Panel.Bounds.Min(point => Dot(point, planeRight)), tolerance);
                for (int bayIndex = 0; bayIndex < bays.Count; bayIndex++)
                foreach (var panel in bays[bayIndex])
                {
                    string level = levels[panel.Panel.ObjectId];
                    string bay = Number(bayIndex + 1);
                    string pid = $"PID_{request.ProjectCode.Trim().ToUpperInvariant()}_{elevation}_{level}_{bay}";
                    string cid = PanelCladdingCidService.FromPanelId(pid) + PanelCladdingCidService.RoleSuffix(panel.Panel.UserText);
                    assignments.Add(new(panel.Panel.ObjectId, elevation, level, bay, pid, cid));
                }
            }
        }
        var duplicate = assignments.GroupBy(p => p.Pid, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            return Fail($"Multiple panels occupy {duplicate.Key}: {string.Join(", ", duplicate.Select(p => p.ObjectId))}. No IDs were assigned.");
        // This is also the write boundary: context-only objects never enter the plan.
        return OperationResponse<PanelCladdingPidPlan>.Ok(new(assignments.Where(p => ids.Contains(p.ObjectId))
            .OrderBy(p => p.Pid, StringComparer.Ordinal).ToArray()) { ContextPanelCount = panels.Count });
    }

    public OperationResponse ValidateDocumentIdentities(
        PanelCladdingPidPlan plan,
        IReadOnlyList<PanelCladdingPidPanel> sources,
        IReadOnlyList<PanelCladdingPidExistingIdentity> existing)
    {
        var selectedIds = plan.Panels.Select(p => p.ObjectId).ToHashSet();
        var byId = sources.ToDictionary(p => p.ObjectId);
        var writes = plan.Panels.ToDictionary(p => p.ObjectId);
        // Validate the state that will actually remain in Rhino, not hypothetical
        // assignments to unselected panels. This also catches unselected pairs.
        var duplicate = sources.Select(p => (p.ObjectId, Pid: writes.TryGetValue(p.ObjectId, out var write)
                ? write.Pid : Get(p.UserText, PanelCladdingSpawnPlanningService.PanelIdUserTextKey)))
            .Where(p => !string.IsNullOrWhiteSpace(p.Pid))
            .GroupBy(p => p.Pid.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            return OperationResponse.Fail($"PCpid: duplicate PID {duplicate.Key} in {PanelLayerRoot}: " +
                $"{string.Join(", ", duplicate.Select(p => p.ObjectId))}. No IDs were assigned; select these panels for correction.");
        var otherObjects = existing.Where(p => !selectedIds.Contains(p.ObjectId)).ToArray();
        var pidLookup = otherObjects.Where(p => p.Pid.Trim().Length > 0).ToLookup(p => p.Pid.Trim(), StringComparer.OrdinalIgnoreCase);
        var cidLookup = otherObjects.Where(p => p.Cid.Trim().Length > 0).ToLookup(p => p.Cid.Trim(), StringComparer.OrdinalIgnoreCase);
        foreach (var assignment in plan.Panels)
        {
            string oldPid = Get(byId[assignment.ObjectId].UserText, PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
            string oldCid = Get(byId[assignment.ObjectId].UserText, PanelCladdingSpawnPlanningService.CidUserTextKey);
            var collision = pidLookup[assignment.Pid].FirstOrDefault(p => !p.IsManagedDependency || !Equal(oldPid, assignment.Pid))
                ?? cidLookup[assignment.Cid].FirstOrDefault();
            if (collision is not null)
                return OperationResponse.Fail($"PCpid: {assignment.Pid} / {assignment.Cid} conflicts with unselected object {collision.ObjectId}. Resolve the conflict or include that panel in the update selection.");
            if (!Equal(oldPid, assignment.Pid) || !Equal(oldCid, assignment.Cid))
            {
                var dependency = otherObjects.FirstOrDefault(p => p.IsManagedDependency &&
                    ((oldPid.Length > 0 && Equal(p.Pid, oldPid)) ||
                     (oldCid.Length > 0 && p.Cid.StartsWith(oldCid + "-", StringComparison.OrdinalIgnoreCase))));
                if (dependency is not null)
                    return OperationResponse.Fail($"PCpid: panel {assignment.ObjectId} already has generated dependencies ({dependency.ObjectId}). Clear those dependencies before initial ID setup.");
            }
        }
        return OperationResponse.Ok();
    }

    private static bool SamePlane(Measured first, Measured second, double tolerance, double angleTolerance) =>
        Dot(first.Normal, second.Normal) >= Math.Cos(angleTolerance) &&
        second.Panel.Bounds.All(p => Math.Abs(Dot(Subtract(p, first.Origin), first.Normal)) <= tolerance) &&
        first.Panel.Bounds.All(p => Math.Abs(Dot(Subtract(p, second.Origin), second.Normal)) <= tolerance);

    // Compare with the first value, not the previous value: near-equal chains must
    // never merge rows/columns whose endpoints are outside the model tolerance.
    private static List<List<T>> Cluster<T>(IEnumerable<T> items, Func<T, double> coordinate, double tolerance)
    {
        var groups = new List<List<T>>();
        double start = 0;
        foreach (T item in items.OrderBy(coordinate))
        {
            double value = coordinate(item);
            if (groups.Count == 0 || value - start > tolerance)
            {
                groups.Add([]);
                start = value;
            }
            groups[^1].Add(item);
        }
        return groups;
    }

    private static string Number(int value) => value.ToString("D2", CultureInfo.InvariantCulture);
    private static bool Equal(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string Get(IReadOnlyDictionary<string, string> values, string key) =>
        (values.FirstOrDefault(p => Equal(p.Key, key)).Value ?? string.Empty).Trim();
    private static bool Finite(PanelPoint3 p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
    private static double Dot(PanelPoint3 a, PanelPoint3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static PanelPoint3 Scale(PanelPoint3 p, double scale) => new(p.X * scale, p.Y * scale, p.Z * scale);
    private static PanelPoint3 Subtract(PanelPoint3 a, PanelPoint3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static OperationResponse<PanelCladdingPidPlan> Fail(string message) => OperationResponse<PanelCladdingPidPlan>.Fail("PCpid: " + message);
    private sealed record Measured(PanelCladdingPidPanel Panel, PanelPoint3 Normal, PanelPoint3 Horizontal, PanelPoint3 Origin, double Bottom);
}
