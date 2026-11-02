using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace PlugInSolar {
public class PvModuleSpecificationsComponent : GH_Component {
    public PvModuleSpecificationsComponent():base("PV Module Specifications","PV Specs","Use grouped product specifications or manual STC inputs. Grouped data overrides all manual electrical values. Includes fractional gamma for the existing PV power components.","Plug-In Solar","01 Inputs") { }
    public override Guid ComponentGuid=>new Guid("633fb763-741a-4560-b88a-f76f3a10ccbb");
    protected override Bitmap Icon=>Assets.Icon("pv_power");
    protected override void RegisterInputParams(GH_InputParamManager p) {
        p.AddNumberParameter("Rated DC Power","Pmax","STC power per module in Wp.",GH_ParamAccess.item,450);
        p.AddNumberParameter("Module Efficiency","Eff","Module efficiency in percent, e.g. 22.5.",GH_ParamAccess.item,22.5);
        p.AddNumberParameter("Pmax Temperature Coefficient","Gamma%","Percent per degree C, e.g. -0.29. Not a decimal fraction.",GH_ParamAccess.item,-.29);
        p.AddNumberParameter("Vmp","Vmp","Voltage at maximum power, V.",GH_ParamAccess.item,44.6);
        p.AddNumberParameter("Imp","Imp","Current at maximum power, A.",GH_ParamAccess.item,10.09);
        p.AddNumberParameter("Voc","Voc","Open circuit voltage, V.",GH_ParamAccess.item,52.9);
        p.AddNumberParameter("Isc","Isc","Short circuit current, A.",GH_ParamAccess.item,10.74);
        int i=p.AddGenericParameter("Reference Specifications","Specs","Optional grouped Module Specifications from Select PV Panel. Overrides all manual inputs together.",GH_ParamAccess.item);p[i].Optional=true;
    }
    static void RegisterValues(GH_OutputParamManager p) {
        p.AddNumberParameter("Rated DC Power","Pmax","STC rated power per module, Wp. Multiply by panel count for array capacity.",GH_ParamAccess.item);
        p.AddNumberParameter("Module Efficiency","Eff","Module efficiency, percent.",GH_ParamAccess.item);
        p.AddNumberParameter("Pmax Temperature Coefficient","Gamma%","Pmax temperature coefficient, percent per degree C.",GH_ParamAccess.item);
        p.AddNumberParameter("Vmp","Vmp","Maximum-power voltage, V.",GH_ParamAccess.item);
        p.AddNumberParameter("Imp","Imp","Maximum-power current, A.",GH_ParamAccess.item);
        p.AddNumberParameter("Voc","Voc","Open circuit voltage, V.",GH_ParamAccess.item);
        p.AddNumberParameter("Isc","Isc","Short circuit current, A.",GH_ParamAccess.item);
        p.AddNumberParameter("Fractional Temperature Coefficient","gamma_pdc","Fraction per degree C. Connect to gamma_pdc on PV Power / PV Power Weather. -0.29 percent becomes -0.0029.",GH_ParamAccess.item);
    }
    public static void SetValues(IGH_DataAccess da,int first,PvModuleSpecs s) {
        double[] values={s.PowerW,s.EfficiencyPct,s.GammaPmaxPctPerC,s.Vmp,s.Imp,s.Voc,s.Isc,s.GammaPmaxPctPerC/100};
        for(int i=0;i<values.Length;i++)da.SetData(first+i,values[i]);
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p) {
        p.AddGenericParameter("Module Specifications","Specs","Validated grouped module specifications.",GH_ParamAccess.item);RegisterValues(p);
    }
    protected override void SolveInstance(IGH_DataAccess da) {
        try {
            PvModuleSpecs specs;IGH_Goo reference=null;
            if(da.GetData(7,ref reference)) {
                specs=PvPanelCatalog.Unwrap<PvModuleSpecsData>(reference).Data;
                if(Params.Input.Take(7).Any(p=>p.SourceCount>0))AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,"Reference Specifications overrides connected manual electrical inputs.");
            } else if(Params.Input[7].SourceCount>0) {
                throw new ArgumentException("Connected Reference Specifications contains no usable data.");
            } else {
                var v=new double[7];for(int i=0;i<7;i++)if(!da.GetData(i,ref v[i]))return;
                specs=new PvModuleSpecs{Model="Custom manual module",Manufacturer="User defined",PowerW=v[0],EfficiencyPct=v[1],GammaPmaxPctPerC=v[2],Vmp=v[3],Imp=v[4],Voc=v[5],Isc=v[6]};
            }
            specs.Validate();da.SetData(0,new PvModuleSpecsData(specs));SetValues(da,1,specs);Message=specs.Model;
        } catch(Exception e){foreach(var output in Params.Output)output.ClearData();AddRuntimeMessage(GH_RuntimeMessageLevel.Error,e.Message);}
    }
}
}
