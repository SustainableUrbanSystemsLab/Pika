using System;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
namespace PlugInSolar {
public sealed class SelectInverterComponent:GH_Component {
 public SelectInverterComponent():base("Select Inverter","Inverter","Create custom inverter specifications from manufacturer ratings. Required inputs: all six ratings. Product catalog selection is not available in this version.","Pika","01 Inputs"){}
 public override Guid ComponentGuid=>new Guid("caef615d-056c-4ba5-8d26-22123028da37");
 protected override Bitmap Icon=>Assets.Icon("pv_power");
 protected override void RegisterInputParams(GH_InputParamManager p){p.AddNumberParameter("AC Rated Power","AC W","Manufacturer rated continuous AC output, W. Required.",GH_ParamAccess.item);p.AddNumberParameter("Nominal Efficiency","Eff","Manufacturer nominal conversion efficiency, percent. Required.",GH_ParamAccess.item);p.AddNumberParameter("Maximum DC Voltage","Max V","Absolute maximum DC input voltage, V. Required.",GH_ParamAccess.item);p.AddNumberParameter("MPPT Minimum Voltage","Min V","Minimum tracking voltage, V. Required.",GH_ParamAccess.item);p.AddNumberParameter("MPPT Maximum Voltage","MPPT V","Maximum tracking voltage, V. Required.",GH_ParamAccess.item);p.AddNumberParameter("Maximum Operating Current","Max A","Maximum operating DC input current per channel, A. Required.",GH_ParamAccess.item);}
 protected override void RegisterOutputParams(GH_OutputParamManager p){p.AddGenericParameter("Inverter Specifications","Inverter","Manufacturer supplied ratings. Connect to PV Electrical Config.",GH_ParamAccess.item);}
 protected override void SolveInstance(IGH_DataAccess da){try{var v=new double[6];for(int i=0;i<6;i++)if(!da.GetData(i,ref v[i]))return;da.SetData(0,new PvInverterData(new PvInverterSpec{AcPowerW=v[0],EfficiencyPct=v[1],MaximumVoltage=v[2],MpptMinimum=v[3],MpptMaximum=v[4],MaximumCurrent=v[5]}));}catch(Exception e){foreach(var output in Params.Output)output.ClearData();AddRuntimeMessage(GH_RuntimeMessageLevel.Error,e.Message);}}
}
}