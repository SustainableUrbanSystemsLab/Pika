using System;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
namespace PlugInSolar {
public sealed class PvElectricalConfigComponent:GH_Component {
 public PvElectricalConfigComponent():base("PV Electrical Config","PV Config","Configure one inverter per module and check STC voltage and operating current. Required inputs: Module Specifications and Inverter Specifications.","Pika","01 Inputs"){}
 public override Guid ComponentGuid=>new Guid("50765ccb-9b5c-4a9d-80e2-158fa1b5f88a");
 protected override Bitmap Icon=>Assets.Icon("pv_power");
 protected override void RegisterInputParams(GH_InputParamManager p){p.AddGenericParameter("Module Specifications","Specs","Select PV Panel output.",GH_ParamAccess.item);p.AddGenericParameter("Inverter Specifications","Inverter","Select Inverter output. One inverter per module.",GH_ParamAccess.item);}
 protected override void RegisterOutputParams(GH_OutputParamManager p){p.AddGenericParameter("Electrical Config","Config","Validated module-level topology and inverter ratings.",GH_ParamAccess.item);p.AddTextParameter("Validation","Report","Checks performed and remaining design checks.",GH_ParamAccess.item);}
 protected override void SolveInstance(IGH_DataAccess da){try{IGH_Goo module=null,inverter=null;if(!da.GetData(0,ref module)||!da.GetData(1,ref inverter))return;
var m=PvPanelCatalog.Unwrap<PvModuleSpecsData>(module).Data;
var inv=PvPanelCatalog.Unwrap<PvInverterData>(inverter).Data;
PvInverterValidation.Check(m,inv);
da.SetData(0,new PvElectricalData(new PvElectricalSettings{Inverter=inv,PanelsPerInverter=1}));
da.SetData(1,"STC checks passed. One independent inverter per module. Cold Voc, hot Vmp, startup voltage and short-circuit limits require additional manufacturer data before installation.");
}catch(Exception e){foreach(var output in Params.Output)output.ClearData();AddRuntimeMessage(GH_RuntimeMessageLevel.Error,e.Message);}}
}
}