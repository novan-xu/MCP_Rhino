var sourceObjects = doc.Objects.GetObjectList(new ObjectEnumeratorSettings {
    NormalObjects=true, HiddenObjects=true, LockedObjects=true, ActiveObjects=true,
    ReferenceObjects=false, IdefObjects=false, DeletedObjects=false, IncludeLights=true
}).Concat(doc.InstanceDefinitions.Where(d=>!d.IsDeleted && !d.IsReference).SelectMany(d=>d.GetObjects())).DistinctBy(o=>o.Id).ToArray();
if (sourceObjects.Length==0) throw new Exception("No source objects to preserve.");
string Signature(RhinoObject o) => o.Geometry.DataCRC(0)+"|"+o.Attributes.ToJSON(new Rhino.FileIO.SerializationOptions());
var signatures = sourceObjects.ToDictionary(o=>o.Id,Signature);
double Value(string family,string field) => Parameters.GetProperty(family).GetProperty(field).GetDouble();
var settings = new StoneSurfaceBake.Settings(Value("fin","offset"),Value("fin","height"),
    Value("flat","offset"),Value("flat","height"),Value("lintel","offset"),Value("lintel","height"));
var baked = StoneSurfaceBake.RunHeadless(doc,settings,true);
if (baked.Fins.Count+baked.Flats.Count+baked.Lintels.Count==0) throw new Exception("No new stone surfaces found: "+baked.Report);
double inches = RhinoMath.UnitScale(doc.ModelUnitSystem,UnitSystem.Inches);
object Measure(string family,List<Brep> geometries) {
    var boxes=geometries.Select(b=>b.GetBoundingBox(true)).ToArray();
    double offset=Value(family,"offset"),height=Value(family,"height");
    foreach(var box in boxes)
        if(Math.Abs((box.Max.Z-box.Min.Z)*inches-height)>1e-6 || Math.Abs(box.Min.Z*inches-offset)>1e-6)
            throw new Exception(family+" baseline/height mismatch: base="+(box.Min.Z*inches)+", height="+((box.Max.Z-box.Min.Z)*inches));
    return new {count=boxes.Length,offset,height,measuredBase=boxes.Length==0?(double?)null:boxes.Min(b=>b.Min.Z)*inches,
        minHeight=boxes.Length==0?(double?)null:boxes.Min(b=>b.Max.Z-b.Min.Z)*inches,
        maxHeight=boxes.Length==0?(double?)null:boxes.Max(b=>b.Max.Z-b.Min.Z)*inches};
}
var measurements = new {fin=Measure("fin",baked.Fins),flat=Measure("flat",baked.Flats),lintel=Measure("lintel",baked.Lintels)};
foreach(var original in signatures) {
    var current=doc.Objects.FindId(original.Key);
    if(current==null || Signature(current)!=original.Value) throw new Exception("Imported geometry/attributes changed: "+original.Key);
}
int afterObjects=doc.Objects.Count, afterDefinitions=doc.InstanceDefinitions.Count;
var repeated=StoneSurfaceBake.RunHeadless(doc,settings,true);
if(doc.Objects.Count!=afterObjects || doc.InstanceDefinitions.Count!=afterDefinitions || repeated.Fins.Count+repeated.Flats.Count+repeated.Lintels.Count!=0)
    throw new Exception("Repeated bake added duplicates.");
Log(baked.Report);
Log(repeated.Report);
Result = new {measurements,bakedAssemblyCount=baked.BakedIds.Count,importedObjectsPreserved=signatures.Count,repeatAddedObjects=0,
    sourceObjectsUnchanged=true};
foreach(var g in baked.Fins.Concat(baked.Flats).Concat(baked.Lintels)) g.Dispose();
