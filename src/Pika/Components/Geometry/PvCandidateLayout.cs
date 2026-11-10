using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
namespace PlugInSolar {
public sealed class PvCandidateLayoutComponent : GH_Component {
    bool flip;double rotation;
    public PvCandidateLayoutComponent():base("PV Candidate Layout","PV Candidate","Generate a geometrically feasible candidate layout. Solar Exposure evaluates shading next. Uses the selected surface normal; reverse it in the component menu when needed. Required inputs: Panel Template and Mounting Surface.","Pika","01 Geometry"){}
    public override Guid ComponentGuid=>new Guid("0f9e4421-f274-42e6-822e-9eb9b9a1d503");
    protected override Bitmap Icon=>Assets.Icon("panel_orientation");
    protected override void RegisterInputParams(GH_InputParamManager p){
        p.AddGenericParameter("Panel Template","Template","PV Geometry Generator → Panel Template. Contains panel geometry and front frame.",GH_ParamAccess.item);
        p.AddBrepParameter("Mounting Surface","S","One selected planar face, including any modeled holes. Verify its outward direction in the preview.",GH_ParamAccess.item);
        p.AddNumberParameter("Tilt from Surface","Tilt","Degrees from mounting surface (-80 to 80). Zero is parallel. Positive facade tilt projects the bottom outward; positive roof tilt faces model -Y at default grid rotation.",GH_ParamAccess.item,0);
        p.AddNumberParameter("Column Gap","XGap","Gap between projected panel footprints across columns, meters.",GH_ParamAccess.item,.1);
        p.AddNumberParameter("Row Gap","YGap","Gap between projected footprints across rows, meters. Shading is calculated downstream.",GH_ParamAccess.item,.1);
        p.AddBooleanParameter("Landscape","L","Rotate source panel 90 degrees in its own plane.",GH_ParamAccess.item,false);
        p.AddNumberParameter("Mounting Offset","Offset","Minimum normal clearance behind full panel solid, meters.",GH_ParamAccess.item,.05);
        p.AddNumberParameter("Edge Setback","Edge","Gap from projected footprints to mounting boundary and holes, meters.",GH_ParamAccess.item,.1);
        int c=p.AddBrepParameter("Building and Obstacles","Context","Building, balcony slabs, railings and nearby objects. Used for collisions here and passed automatically to Solar Exposure for shading.",GH_ParamAccess.list);p[c].Optional=true;
        p.AddIntegerParameter("Maximum Panel Count","Max","Maximum accepted panels, 1–5000. Defaults to 5000.",GH_ParamAccess.item,5000);
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p){
        p.AddGenericParameter("Candidate Layout","Layout","Grouped panel geometry, IDs, frames and context. Connect to PV Energy Calculator or PV Solar Exposure.",GH_ParamAccess.item);
        p.AddBrepParameter("Candidate Panels","Panels","Panel solids in document units.",GH_ParamAccess.list);
        p.AddBrepParameter("Analysis Faces","Faces","Full rectangular fronts; not measured cell area.",GH_ParamAccess.list);
        p.AddIntegerParameter("Panel Count","Count","Accepted candidate count.",GH_ParamAccess.item);
        p.AddNumberParameter("Total Analysis Area","Area","Full front analysis area, m².",GH_ParamAccess.item);
        p.AddTextParameter("Panel IDs","IDs","Stable surface/row/column labels.",GH_ParamAccess.list);
        p.AddVectorParameter("Mounting Direction","Normal","Verify this points away from the building. Reverse using the component menu.",GH_ParamAccess.item);
        p.AddNumberParameter("Tilt from Horizontal","TiltH","Actual panel front tilt from horizontal, degrees. Solar Exposure also reports azimuth using its North input.",GH_ParamAccess.list);
        p.AddTextParameter("Report","Report","Geometric acceptance/rejection counts. No energy optimization.",GH_ParamAccess.item);
    }
    protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu){base.AppendAdditionalComponentMenuItems(menu);Menu_AppendItem(menu,"Flip mounting direction",(s,e)=>{RecordUndoEvent("Flip mounting direction");flip=!flip;ExpireSolution(true);},true,flip);Menu_AppendItem(menu,"Rotate grid +90°",(s,e)=>{RecordUndoEvent("Rotate grid");rotation=(rotation+90)%360;ExpireSolution(true);});Menu_AppendItem(menu,"Reset grid rotation",(s,e)=>{RecordUndoEvent("Reset grid");rotation=0;ExpireSolution(true);});}
    public override bool Write(GH_IO.Serialization.GH_IWriter w){w.SetBoolean("flip",flip);w.SetDouble("rotation",rotation);return base.Write(w);}
    public override bool Read(GH_IO.Serialization.GH_IReader r){if(r.ItemExists("flip"))flip=r.GetBoolean("flip");if(r.ItemExists("rotation"))rotation=r.GetDouble("rotation");return base.Read(r);}
    protected override void SolveInstance(IGH_DataAccess da){try{
        IGH_Goo input=null;Brep surface=null;double tilt=0,x=.1,y=.1,offset=.05,edge=.1;bool landscape=false;
        if(!da.GetData(0,ref input)||!da.GetData(1,ref surface)||!da.GetData(2,ref tilt)||!da.GetData(3,ref x)||!da.GetData(4,ref y)||!da.GetData(5,ref landscape)||!da.GetData(6,ref offset)||!da.GetData(7,ref edge))return;
        var template=PvPanelCatalog.Unwrap<PvPanelTemplateData>(input);if(!template.IsValid)throw new ArgumentException("Invalid Panel Template.");var context=new List<Brep>();da.GetDataList(8,context);var doc=RhinoDoc.ActiveDoc;if(doc==null)throw new ArgumentException("Open a Rhino document.");
        int maximum=5000;da.GetData(9,ref maximum);
        var r=PvLayoutEngine.Generate(template.Value.Item1,template.Value.Item2,surface,x,y,tilt,landscape,offset,edge,null,context,flip,rotation,"Surface1",maximum,doc.ModelUnitSystem,doc.ModelAbsoluteTolerance,false);
        da.SetData(0,new PvCandidateLayoutData(new PvLayoutBundle{Layout=r,Units=doc.ModelUnitSystem,Tolerance=doc.ModelAbsoluteTolerance,Context=context.Select(b=>b.DuplicateBrep()).ToList()}));da.SetDataList(1,r.Panels);da.SetDataList(2,r.Faces);da.SetData(3,r.Panels.Count);da.SetData(4,r.AreaM2);da.SetDataList(5,r.Ids);da.SetData(6,r.MountNormal);da.SetDataList(7,r.Normals.Select(n=>Math.Acos(Math.Max(-1,Math.Min(1,n.Z)))*180/Math.PI));da.SetData(8,r.Report);Message="Candidate only";
        if(r.Panels.Count==0)AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,"No panels fit; inspect Report and mounting direction.");
    }catch(Exception e){foreach(var p in Params.Output)p.ClearData();AddRuntimeMessage(GH_RuntimeMessageLevel.Error,e.Message);}}
}
}
