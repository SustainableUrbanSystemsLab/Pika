using System;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
namespace PlugInSolar {
public sealed class PvLossAssumptionsComponent:GH_Component {
 public PvLossAssumptionsComponent():base("PV Loss Assumptions","PV Losses","Connect the same Losses output to both calculators. PV Energy Calculator applies soiling. PV AC Calculator applies DC and AC wiring losses. Required inputs: none. Defaults are zero loss.","Pika","01 Inputs"){}
 public override Guid ComponentGuid=>new Guid("99258bbe-cc6d-4d78-802a-1c5574ccf155");
 protected override Bitmap Icon=>Assets.Icon("pv_power");
 protected override void RegisterInputParams(GH_InputParamManager p){p.AddNumberParameter("Soiling","Soiling","Soiling loss, percent, 0–100. Applied once at its own conversion stage.",GH_ParamAccess.item,0);p.AddNumberParameter("DC Wiring","DC Wiring","DC Wiring loss, percent, 0–100. Applied once at its own conversion stage.",GH_ParamAccess.item,0);p.AddNumberParameter("AC Wiring","AC Wiring","AC Wiring loss, percent, 0–100. Applied once at its own conversion stage.",GH_ParamAccess.item,0);}
 protected override void RegisterOutputParams(GH_OutputParamManager p){p.AddGenericParameter("Losses","Losses","Soiling, DC wiring and AC wiring percentages.",GH_ParamAccess.item);}
 protected override void SolveInstance(IGH_DataAccess da){try{double a=0,b=0,c=0;da.GetData(0,ref a);da.GetData(1,ref b);da.GetData(2,ref c);da.SetData(0,new PvLossData(new PvLossSettings{SoilingPct=a,DcWiringPct=b,AcWiringPct=c}));}catch(Exception e){foreach(var output in Params.Output)output.ClearData();AddRuntimeMessage(GH_RuntimeMessageLevel.Error,e.Message);}}
}
}