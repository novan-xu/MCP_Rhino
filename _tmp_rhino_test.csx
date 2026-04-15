using System;
using Rhino.FileIO;
using Rhino.DocObjects;
using System.Drawing;

var path = @"c:\Projects\MCP_Rhino\test-files\MCP_rhino_test.3dm";
var model = File3dm.Read(path);
var first = model.Objects.GetEnumerator();
first.MoveNext();
var obj = first.Current;
var attrs = obj.Attributes.Duplicate();
attrs.SetUserString("CLINE_TEST", "1");
attrs.ObjectColor = Color.Red;
attrs.ColorSource = ObjectColorSource.ColorFromObject;
attrs.ObjectId = obj.Attributes.ObjectId;
var geometry = obj.Geometry;
var deleted = model.Objects.Delete(obj.Attributes.ObjectId);
var added = model.Objects.Add(geometry, attrs);
Console.WriteLine($"deleted={deleted}; added={added}; id={attrs.ObjectId}");
var writePath = @"c:\Projects\MCP_Rhino\test-files\_tmp_edit_test.3dm";
Console.WriteLine(model.Write(writePath, 0));
var reread = File3dm.Read(writePath);
foreach (var reObj in reread.Objects)
{
    if (reObj.Attributes.ObjectId == attrs.ObjectId)
    {
        Console.WriteLine(reObj.Attributes.GetUserString("CLINE_TEST"));
        Console.WriteLine(reObj.Attributes.ObjectColor);
        Console.WriteLine(reObj.Attributes.ColorSource);
        break;
    }
}
