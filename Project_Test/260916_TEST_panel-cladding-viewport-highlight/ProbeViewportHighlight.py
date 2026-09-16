"""Run inside Rhino with PCEditor open. Reads state; never edits document objects."""
import System
import Rhino
from System.Reflection import BindingFlags
from System.IO import Directory, File, Path

lines = []
flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
static_flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public


def record(key, value):
    lines.append(str(key) + "=" + str(value))


def field(instance, name):
    member = instance.GetType().GetField(name, flags)
    return member.GetValue(instance) if member is not None else None


def describe_doc(prefix, doc):
    record(prefix + ".Exists", doc is not None)
    if doc is not None:
        record(prefix + ".Serial", doc.RuntimeSerialNumber)
        record(prefix + ".Saved", bool(doc.Path))


def main():
    assembly = next((item for item in System.AppDomain.CurrentDomain.GetAssemblies()
                     if item.GetName().Name == "PanelCladdingEditor"), None)
    if assembly is None:
        record("Error", "PanelCladdingEditor assembly is not loaded")
        return
    record("Assembly", assembly.Location)
    command = assembly.GetType("PanelCladdingEditor.UI.PanelCladdingEditorCommand")
    window = command.GetField("_window", static_flags).GetValue(None)
    if window is None:
        record("Error", "PCEditor has no window")
        return
    record("Window.Visible", window.IsVisible)
    record("Window.State", window.WindowState)
    record("Window.Active", window.IsActive)
    layout = field(window, "_sourceLayout")
    record("Layout.Exists", layout is not None)
    if layout is not None:
        record("Layout.Serial", layout.DocumentRuntimeSerialNumber)
        record("Layout.ObjectId", layout.ObjectId)
    highlight = field(window, "_viewportHighlight")
    record("Highlight.Exists", highlight is not None)
    if highlight is None:
        return
    record("Highlight.Enabled", highlight.Enabled)
    for name in ["_documentSerial", "_objectId", "_visible", "_disposed"]:
        record("Highlight." + name, field(highlight, name))
    serial = field(highlight, "_documentSerial")
    target = Rhino.RhinoDoc.FromRuntimeSerialNumber(serial)
    describe_doc("TargetDocument", target)
    describe_doc("ActiveDocument", Rhino.RhinoDoc.ActiveDoc)
    if target is None:
        return
    panel = target.Objects.FindId(field(highlight, "_objectId"))
    record("Panel.Exists", panel is not None)
    if panel is not None:
        record("Panel.Deleted", panel.IsDeleted)
        record("Panel.Visible", panel.Visible)
        record("Panel.Hidden", panel.IsHidden)
        record("Panel.Geometry", panel.Geometry.GetType().FullName)
        record("Panel.Selected", panel.IsSelected(False))
    frames = [0]

    def on_draw(sender, e):
        frames[0] += 1
        if frames[0] > 8:
            return
        prefix = "Frame" + str(frames[0])
        try:
            describe_doc(prefix + ".Document", e.RhinoDoc)
            record(prefix + ".Viewport", e.Viewport.Name)
            if panel is not None:
                record(prefix + ".PanelActive", panel.IsActiveInViewport(e.Viewport))
                # Invoke the installed callback once in its normal draw context to
                # surface exceptions that Rhino may otherwise catch internally.
                method = highlight.GetType().GetMethod("DrawForeground", flags)
                if method.DeclaringType == highlight.GetType():
                    record(prefix + ".Callback", "foreground override installed")
                else:
                    method = highlight.GetType().GetMethod("PostDrawObjects", flags)
                    method.Invoke(highlight, System.Array[System.Object]([e]))
                    record(prefix + ".Callback", "returned")
        except Exception as error:
            record(prefix + ".Error", str(error))

    Rhino.Display.DisplayPipeline.PostDrawObjects += on_draw
    try:
        target.Views.Redraw()
    finally:
        Rhino.Display.DisplayPipeline.PostDrawObjects -= on_draw
    record("ObservedFrames", frames[0])


try:
    main()
except Exception as error:
    record("ProbeError", str(error))
finally:
    output_dir = Path.Combine(System.Environment.GetFolderPath(
        System.Environment.SpecialFolder.LocalApplicationData), "PanelCladdingEditor", "diagnostics")
    Directory.CreateDirectory(output_dir)
    output = Path.Combine(output_dir, "viewport-highlight.txt")
    File.WriteAllLines(output, System.Array[System.String](lines))
    for line in lines:
        Rhino.RhinoApp.WriteLine(line)
    Rhino.RhinoApp.WriteLine("Diagnostic saved: " + output)
