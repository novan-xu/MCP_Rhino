using System.Drawing;
using System.Text.Json;
using MCP_Rhino.Offline.Contracts;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;

namespace MCP_Rhino.Offline.Infrastructure;

internal sealed class OfflineModelBridge(File3dm model)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private int _calls;
    public int MutationCount { get; private set; }
    public bool Failed { get; private set; }
    public List<string> Log { get; } = [];

    public string Call(string operation, string arguments)
    {
        try
        {
            if (++_calls > 10_000 || arguments.Length > 1_000_000)
                throw new InvalidOperationException("Model call/input limit exceeded.");
            using JsonDocument document = JsonDocument.Parse(arguments);
            return JsonSerializer.Serialize(new { ok = true, value = Dispatch(operation, document.RootElement) }, Json);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Failed = true;
            return JsonSerializer.Serialize(new { ok = false, error = ex.Message }, Json);
        }
    }

    public object Inspect(int offset, int limit) => new { archiveVersion = model.ArchiveVersion,
        units = model.Settings.ModelUnitSystem.ToString(), objectCount = model.Objects.Count,
        layers = Layers(), documentUserText = DocumentText(), objects = Objects(offset, limit) };

    private object Dispatch(string operation, JsonElement args)
    {
        switch (operation)
        {
            case "objects": return Objects(args.GetProperty("offset").GetInt32(), args.GetProperty("limit").GetInt32());
            case "layers": return Layers();
            case "documentUserText": return DocumentText();
            case "log":
                if (Log.Count >= 100) throw new InvalidOperationException("At most 100 log messages are allowed.");
                string message = Text(args.GetProperty("message"));
                Log.Add(message); return true;
            case "setDocumentUserText":
                string key = Text(args.GetProperty("key"), allowEmpty: false);
                JsonElement value = args.GetProperty("value");
                if (value.ValueKind == JsonValueKind.Null) model.Strings.Delete(key);
                else model.Strings.SetString(key, Text(value));
                MutationCount++; return true;
            case "setAttributes":
                foreach (File3dmObject obj in Targets(args))
                {
                    SetAttributes(obj.Attributes, args.GetProperty("attributes"));
                    MutationCount++;
                }
                return true;
            case "transform":
                double[] values = Numbers(args.GetProperty("matrix"), 16);
                if (values[12] != 0 || values[13] != 0 || values[14] != 0 || values[15] != 1)
                    throw new InvalidOperationException("Transform must be a row-major 4x4 affine matrix.");
                var transform = Transform.Identity;
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++) transform[row, column] = values[row * 4 + column];
                if (!transform.IsValid || !transform.TryGetInverse(out _))
                    throw new InvalidOperationException("Transform must be finite and invertible.");
                foreach (File3dmObject obj in Targets(args))
                {
                    // Duplicate before touching the model: a failed transform invalidates the whole preview.
                    using GeometryBase geometry = obj.Geometry.Duplicate();
                    if (!geometry.Transform(transform) || !geometry.IsValid)
                        throw new InvalidOperationException($"Cannot transform object {obj.Id}.");
                    using ObjectAttributes attributes = obj.Attributes.Duplicate();
                    Guid id = obj.Id;
                    model.Objects.Delete(id);
                    if (model.Objects.Add(geometry, attributes) != id)
                        throw new InvalidOperationException("Transform did not preserve the object id.");
                    MutationCount++;
                }
                return true;
            case "deleteObjects":
                foreach (File3dmObject obj in Targets(args))
                {
                    if (!model.Objects.Delete(obj.Id)) throw new InvalidOperationException("Cannot delete object.");
                    MutationCount++;
                }
                return true;
            case "addLayer":
                string name = LayerName(args.GetProperty("name"));
                Guid parent = args.GetProperty("parentId").ValueKind == JsonValueKind.Null ? Guid.Empty : Id(args.GetProperty("parentId"));
                if (parent != Guid.Empty) _ = LayerById(parent);
                if (model.AllLayers.Any(layer => layer.ParentLayerId == parent && layer.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Layer name already exists under this parent.");
                int index = model.AllLayers.AddLayer(name, ColorValue(args.GetProperty("color")), parent);
                if (index < 0) throw new InvalidOperationException("Cannot create layer.");
                MutationCount++; return model.AllLayers.FindIndex(index).Id.ToString();
            case "editLayer":
                Layer layer = LayerById(Id(args.GetProperty("id")));
                JsonElement patch = args.GetProperty("attributes");
                Only(patch, "name", "color", "visible", "locked");
                if (patch.TryGetProperty("name", out JsonElement layerName))
                {
                    string newName = LayerName(layerName);
                    if (model.AllLayers.Any(other => other.Id != layer.Id && other.ParentLayerId == layer.ParentLayerId && other.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("Layer name already exists under this parent.");
                    layer.Name = newName;
                }
                if (patch.TryGetProperty("color", out JsonElement color)) layer.Color = ColorValue(color);
                if (patch.TryGetProperty("visible", out JsonElement visible)) layer.IsVisible = visible.GetBoolean();
                if (patch.TryGetProperty("locked", out JsonElement locked)) layer.IsLocked = locked.GetBoolean();
                MutationCount++; return true;
            case "addPoint": return Add(new Rhino.Geometry.Point(Point(args.GetProperty("point"))), args);
            case "addLine": return Add(new LineCurve(Point(args.GetProperty("start")), Point(args.GetProperty("end"))), args);
            case "addPolyline":
                JsonElement points = args.GetProperty("points");
                if (points.GetArrayLength() is < 2 or > 10_000) throw new InvalidOperationException("Use 2–10000 polyline points.");
                return Add(new PolylineCurve(points.EnumerateArray().Select(Point)), args);
            case "addSphere":
                double radius = args.GetProperty("radius").GetDouble();
                if (!double.IsFinite(radius) || radius <= 0) throw new InvalidOperationException("Radius must be finite and positive.");
                return Add(new Sphere(Point(args.GetProperty("center")), radius).ToBrep(), args);
            case "addBox":
                Point3d min = Point(args.GetProperty("min")), max = Point(args.GetProperty("max"));
                if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z)
                    throw new InvalidOperationException("Box min must be strictly below max on each axis.");
                return Add(new BoundingBox(min, max).ToBrep(), args);
            default: throw new InvalidOperationException("Unsupported model operation.");
        }
    }

    private string Add(GeometryBase geometry, JsonElement args)
    {
        using (geometry)
        using (var attributes = new ObjectAttributes())
        {
            if (MutationCount >= 10_000 || !geometry.IsValid) throw new InvalidOperationException("Invalid geometry or mutation limit exceeded.");
            if (model.AllLayers.Count == 0) model.AllLayers.AddLayer("Default", Color.Black);
            attributes.LayerIndex = model.AllLayers.First().Index;
            SetAttributes(attributes, args.GetProperty("attributes"));
            Guid id = model.Objects.Add(geometry, attributes);
            if (id == Guid.Empty) throw new InvalidOperationException("Cannot add geometry.");
            MutationCount++; return id.ToString();
        }
    }

    private File3dmObject[] Targets(JsonElement args)
    {
        Guid[] ids = args.GetProperty("ids").EnumerateArray().Select(Id).ToArray();
        if (ids.Length is < 1 or > 10_000 || ids.Distinct().Count() != ids.Length || MutationCount + ids.Length > 10_000)
            throw new InvalidOperationException("Supply 1–10000 unique object ids within the 10000 mutation limit.");
        return ids.Select(id =>
        {
            File3dmObject obj = model.Objects.FindId(id) ?? throw new InvalidOperationException($"Unknown object id {id}.");
            if (obj.Attributes.IsInstanceDefinitionObject)
                throw new InvalidOperationException("Block definition geometry requires live Rhino tools; edit block instances only.");
            return obj;
        }).ToArray();
    }

    private void SetAttributes(ObjectAttributes attributes, JsonElement patch)
    {
        Only(patch, "name", "layerId", "color", "visible", "userText");
        if (patch.TryGetProperty("name", out JsonElement name)) attributes.Name = Text(name);
        if (patch.TryGetProperty("layerId", out JsonElement layerId)) attributes.LayerIndex = LayerById(Id(layerId)).Index;
        if (patch.TryGetProperty("color", out JsonElement color))
        {
            attributes.ObjectColor = ColorValue(color);
            attributes.ColorSource = ObjectColorSource.ColorFromObject;
        }
        if (patch.TryGetProperty("visible", out JsonElement visible)) attributes.Visible = visible.GetBoolean();
        if (patch.TryGetProperty("userText", out JsonElement text))
        {
            foreach (JsonProperty item in text.EnumerateObject())
            {
                if (string.IsNullOrEmpty(item.Name) || item.Name.Length > 4096) throw new InvalidOperationException("Invalid user text key.");
                if (!attributes.SetUserString(item.Name, item.Value.ValueKind == JsonValueKind.Null ? null : Text(item.Value)))
                    throw new InvalidOperationException("Cannot update object user text.");
            }
        }
    }

    private object Objects(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 1000) throw new OfflineEditException("INVALID_PAGE", "offset >= 0 and limit 1–1000 are required.");
        return new { total = model.Objects.Count, offset, items = EnumerateObjects().Skip(offset).Take(limit).Select(ObjectInfo).ToArray() };
    }

    private IEnumerable<File3dmObject> EnumerateObjects()
    {
        foreach (File3dmObject obj in model.Objects) yield return obj;
    }

    private static object ObjectInfo(File3dmObject obj)
    {
        BoundingBox box = obj.Geometry.GetBoundingBox(true);
        return new { id = obj.Id, type = obj.Geometry.ObjectType.ToString(), name = obj.Attributes.Name ?? "",
            layerIndex = obj.Attributes.LayerIndex, visible = obj.Attributes.Visible,
            isDefinitionGeometry = obj.Attributes.IsInstanceDefinitionObject,
            color = ColorText(obj.Attributes.ObjectColor), colorSource = obj.Attributes.ColorSource.ToString(),
            userText = (obj.Attributes.GetUserStrings()?.AllKeys ?? []).Where(key => key is not null).ToDictionary(key => key!, key => obj.Attributes.GetUserString(key!)),
            bounds = box.IsValid ? new[] { box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z } : null };
    }

    private object[] Layers() => model.AllLayers.Select(layer => (object)new { id = layer.Id, index = layer.Index,
        name = layer.Name, path = layer.FullPath, parentId = layer.ParentLayerId, color = ColorText(layer.Color),
        visible = layer.IsVisible, locked = layer.IsLocked }).ToArray();
    private Dictionary<string, string> DocumentText() => Enumerable.Range(0, model.Strings.Count)
        .ToDictionary(model.Strings.GetKey, model.Strings.GetValue);
    private Layer LayerById(Guid id) => model.AllLayers.FirstOrDefault(layer => layer.Id == id)
        ?? throw new InvalidOperationException($"Unknown layer id {id}.");

    public byte[] SerializeVerified()
    {
        int version = model.ArchiveVersion >= 50 ? model.ArchiveVersion / 10 : model.ArchiveVersion;
        byte[] bytes = model.ToByteArray(new File3dmWriteOptions { Version = version, SaveUserData = true });
        if (bytes is null || bytes.Length == 0) throw new OfflineEditException("SERIALIZATION_FAILED", "No valid output bytes produced.");
        using File3dm reread = File3dm.FromByteArray(bytes) ?? throw new OfflineEditException("SERIALIZATION_FAILED", "Cannot reread generated .3dm.");
        var other = new OfflineModelBridge(reread);
        if (Signature() != other.Signature())
            throw new OfflineEditException("ROUND_TRIP_FAILED", "Object/layer/metadata verification failed; no file was written.");
        return bytes;
    }

    private string Signature() => JsonSerializer.Serialize(new { objects = EnumerateObjects().OrderBy(x => x.Id).Select(ObjectInfo).ToArray(),
        layers = Layers(), text = DocumentText(), units = model.Settings.ModelUnitSystem, materials = model.AllMaterials.Count,
        definitions = model.AllInstanceDefinitions.Count }, Json);
    private static string ColorText(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    private static Color ColorValue(JsonElement value)
    {
        string text = Text(value);
        if (text.Length != 7 || text[0] != '#' || !int.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            throw new InvalidOperationException("Color must be #RRGGBB.");
        return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    }
    private static Guid Id(JsonElement value) => Guid.TryParse(value.GetString(), out Guid id) && id != Guid.Empty
        ? id : throw new InvalidOperationException("Expected a nonempty UUID.");
    private static string Text(JsonElement value, bool allowEmpty = true)
    {
        string text = value.GetString() ?? throw new InvalidOperationException("Expected a string.");
        if (text.Length > 4096 || (!allowEmpty && string.IsNullOrWhiteSpace(text))) throw new InvalidOperationException("Invalid string length.");
        return text;
    }
    private static string LayerName(JsonElement value)
    {
        string name = Text(value, false);
        if (name.Contains("::") || name.Any(char.IsControl)) throw new InvalidOperationException("Supply a single layer name; parentId sets hierarchy.");
        return name;
    }
    private static double[] Numbers(JsonElement value, int count)
    {
        double[] values = value.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        if (values.Length != count || values.Any(x => !double.IsFinite(x))) throw new InvalidOperationException($"Expected {count} finite numbers.");
        return values;
    }
    private static Point3d Point(JsonElement value) { double[] p = Numbers(value, 3); return new(p[0], p[1], p[2]); }
    private static void Only(JsonElement value, params string[] allowed)
    {
        foreach (JsonProperty property in value.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal)) throw new InvalidOperationException($"Unsupported attribute '{property.Name}'.");
    }
}
