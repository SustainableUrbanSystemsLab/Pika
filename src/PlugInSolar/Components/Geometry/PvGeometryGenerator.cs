using System;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using System.Linq;
using Rhino;
using Rhino.Geometry;

namespace PlugInSolar {
/// <summary>Creates a rectangular panel; the reference plane is its front center.</summary>
public class PvGeometryGeneratorComponent : GH_Component {
    public PvGeometryGeneratorComponent() : base("PV Geometry Generator", "PV Geometry",
        "Create a rectangular PV panel. Dimensions are in meters; geometry uses Rhino document units. The reference plane marks the front center, with thickness behind it.",
        "Plug-In Solar", "01 Geometry") { }

    public override Guid ComponentGuid => new Guid("ea5781f2-b109-4bf3-a1aa-06f788ba2acb");
    protected override Bitmap Icon => Assets.Icon("panel_orientation");

    protected override void RegisterInputParams(GH_InputParamManager p) {
        p.AddNumberParameter("Width", "W", "Panel width in meters, along the reference plane X axis.", GH_ParamAccess.item, 1.1);
        p.AddNumberParameter("Height", "H", "Panel height in meters, along the reference plane Y axis.", GH_ParamAccess.item, 1.7);
        p.AddNumberParameter("Thickness", "T", "Panel thickness in meters, extending behind the front plane (-Z). Must be positive.", GH_ParamAccess.item, 0.035);
        p.AddPlaneParameter("Reference Plane", "P", "Front-face center and orientation. +Z is the active front direction. Defaults to World XY.", GH_ParamAccess.item, Plane.WorldXY);
        int i=p.AddGenericParameter("Panel Dimensions", "Dims", "Optional grouped Panel Dimensions from Select PV Panel. Overrides Width, Height, and Thickness together; Reference Plane still applies.", GH_ParamAccess.item);
        p[i].Optional=true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p) {
        p.AddBrepParameter("Panel", "Panel", "Closed rectangular panel solid in document units.", GH_ParamAccess.item);
        p.AddBrepParameter("Active Face", "Face", "Single planar front face for solar analysis. Full rectangular face; no frame or cell gaps modeled.", GH_ParamAccess.item);
        p.AddVectorParameter("Normal", "N", "Unit outward normal of the active front face.", GH_ParamAccess.item);
        p.AddNumberParameter("Active Area", "A", "Full front-face area in square meters. Does not assign rated electrical power.", GH_ParamAccess.item);
        p.AddPlaneParameter("Panel Frame", "Frame", "Front-center plane for PV Layout Generator → Panel Frame. Connect alongside Panel to preserve its width/height axes and front side.", GH_ParamAccess.item);
        p.AddGenericParameter("Panel Template", "Template", "Panel and front frame together. Connect to PV Candidate Layout.", GH_ParamAccess.item);
    }

    public static void Generate(double widthM, double heightM, double thicknessM,
        Plane plane, UnitSystem units, double tolerance,
        out Brep panel, out Brep activeFace, out Vector3d normal, out double areaM2) {
        panel = null; activeFace = null; normal = Vector3d.Unset; areaM2 = 0;
        Positive(widthM, "Width"); Positive(heightM, "Height"); Positive(thicknessM, "Thickness");
        if (!plane.IsValid) throw new ArgumentException("Reference Plane must be valid.");
        if (units == UnitSystem.None || units == UnitSystem.Unset || units == UnitSystem.CustomUnits)
            throw new ArgumentException("Set Rhino document units to a standard physical unit, such as meters or millimeters.");
        Positive(tolerance, "Document tolerance");
        double scale = RhinoMath.UnitScale(UnitSystem.Meters, units);
        Positive(scale, "Unit conversion");
        double width = widthM * scale, height = heightM * scale, thickness = thicknessM * scale;
        Positive(width, "Converted width"); Positive(height, "Converted height"); Positive(thickness, "Converted thickness");
        if (Math.Min(width, Math.Min(height, thickness)) <= tolerance)
            throw new ArgumentException("Each panel dimension must exceed the Rhino document absolute tolerance. Increase the dimension or use a suitable document tolerance.");
        areaM2 = widthM * heightM;
        Positive(areaM2, "Active area");
        var x = new Interval(-width / 2, width / 2);
        var y = new Interval(-height / 2, height / 2);
        panel = new Box(plane, x, y, new Interval(-thickness, 0)).ToBrep();
        using (var surface = new PlaneSurface(plane, x, y)) activeFace = surface.ToBrep();
        if (panel == null || !panel.IsValid || !panel.IsSolid || activeFace == null || !activeFace.IsValid) {
            panel?.Dispose(); activeFace?.Dispose(); panel = null; activeFace = null;
            throw new ArgumentException("Could not generate valid panel geometry. Check dimensions, reference plane, and model scale.");
        }
        normal = plane.ZAxis;
        normal.Unitize();
    }

    static void Positive(double value, string name) {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            throw new ArgumentException(name + " must be a finite number greater than zero.");
    }

    protected override void SolveInstance(IGH_DataAccess da) {
        double w = 0, h = 0, t = 0; Plane plane = Plane.WorldXY;
        if (!da.GetData(3, ref plane)) return;
        try {
            IGH_Goo reference=null;
            if (da.GetData(4, ref reference)) {
                var dims=PvPanelCatalog.Unwrap<PvPanelDimensionsData>(reference).Data;
                w=dims.WidthM; h=dims.HeightM; t=dims.ThicknessM;
                if(Params.Input.Take(3).Any(p=>p.SourceCount>0)) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,"Panel Dimensions overrides connected Width, Height, and Thickness inputs.");
            } else if(Params.Input[4].SourceCount>0) {
                throw new ArgumentException("Connected Panel Dimensions contains no usable data.");
            } else if(!da.GetData(0, ref w)||!da.GetData(1, ref h)||!da.GetData(2, ref t)) return;
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) throw new ArgumentException("Open a Rhino document to determine geometry units and tolerance.");
            Generate(w, h, t, plane, doc.ModelUnitSystem, doc.ModelAbsoluteTolerance,
                out var panel, out var face, out var normal, out var area);
            da.SetData(0, panel); da.SetData(1, face); da.SetData(2, normal); da.SetData(3, area); da.SetData(4, plane); da.SetData(5, new PvPanelTemplateData(panel, plane));
        } catch (Exception e) { foreach(var output in Params.Output)output.ClearData(); AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message); }
    }
}
}
