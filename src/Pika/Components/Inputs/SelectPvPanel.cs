using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Grasshopper.Kernel;

namespace PlugInSolar {
public class SelectPvPanelComponent : GH_Component {
    string selectedModel=PvPanelCatalog.DefaultModel;
    public SelectPvPanelComponent():base("Select PV Panel","Select PV","Select a catalog module or enter custom module specifications. Required inputs: none for the built-in catalog selection. Custom mode requires all seven electrical fields and three dimensions.","Pika","01 Inputs") { }
    public override Guid ComponentGuid=>new Guid("a442f374-5e0d-4674-94dc-d3101f20130e");
    protected override Bitmap Icon=>Assets.Icon("panel_orientation");
    protected override void RegisterInputParams(GH_InputParamManager p) {
        int i=p.AddTextParameter("Product ID","ID","Optional catalog model ID. Overrides the built-in dropdown when supplied.",GH_ParamAccess.item);p[i].Optional=true;
        p.AddBooleanParameter("Custom Module","Custom","True uses the custom electrical and dimension inputs instead of the catalog.",GH_ParamAccess.item,false);
        string[] names={"Rated DC Power","Module Efficiency","Pmax Temperature Coefficient","Vmp","Imp","Voc","Isc","Width","Height","Thickness"};
        string[] units={"STC power per module, Wp.","Module efficiency, percent.","Power temperature coefficient, percent per °C.","Maximum-power voltage, V.","Maximum-power current, A.","Open-circuit voltage, V.","Short-circuit current, A.","Module width, m.","Module height, m.","Module thickness, m."};
        for(int j=0;j<names.Length;j++) {
            int index=p.AddNumberParameter(names[j],names[j],units[j]+" Required in custom mode.",GH_ParamAccess.item);
            p[index].Optional=true;
        }
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p) {
        p.AddGenericParameter("Panel Dimensions","Dims","Grouped dimensions in meters. Connect to PV Geometry Generator → Panel Dimensions.",GH_ParamAccess.item);
        p.AddGenericParameter("Module Specifications","Specs","Grouped STC electrical properties. Connect to PV Energy Calculator or a Panel to inspect electrical ratings.",GH_ParamAccess.item);
        p.AddTextParameter("Product","Product","Manufacturer and exact model ID.",GH_ParamAccess.item);
        p.AddTextParameter("Datasheet","Source","Manufacturer source and revision used for this catalog snapshot.",GH_ParamAccess.item);
        p.AddTextParameter("Available Products","Catalog","Supported product IDs for a Value List or text input.",GH_ParamAccess.list);
    }
    protected override void SolveInstance(IGH_DataAccess da) {
        try {
            string id="";bool supplied=da.GetData(0,ref id);

            bool custom=false;da.GetData(1,ref custom);
            PvModuleSpecs specs;
            PvPanelDimensions dims;
            if(custom) {
                var v=new double[10];
                for(int i=0;i<v.Length;i++)
                    if(!da.GetData(i+2,ref v[i]))throw new ArgumentException("Custom Module requires "+Params.Input[i+2].Name+".");
                specs=new PvModuleSpecs{Model="Custom module",Manufacturer="User defined",PowerW=v[0],EfficiencyPct=v[1],GammaPmaxPctPerC=v[2],Vmp=v[3],Imp=v[4],Voc=v[5],Isc=v[6]};
                dims=new PvPanelDimensions{Model=specs.Model,WidthM=v[7],HeightM=v[8],ThicknessM=v[9]};
            } else {
                if(supplied&&string.IsNullOrWhiteSpace(id))throw new ArgumentException("Product ID cannot be blank.");
                specs=PvPanelCatalog.Specifications(supplied?id:selectedModel);
                dims=PvPanelCatalog.Dimensions(specs.Model);
            }
            da.SetData(0,new PvPanelDimensionsData(dims));
            da.SetData(1,new PvModuleSpecsData(specs));
            da.SetData(2,specs.Manufacturer+" "+specs.Model);da.SetData(3,custom?"User supplied":specs.SourceUrl+" | "+specs.Revision);da.SetDataList(4,PvPanelCatalog.ModelIds);
            Message=null;
        }catch(Exception e){foreach(var output in Params.Output)output.ClearData();Message=null;AddRuntimeMessage(GH_RuntimeMessageLevel.Error,e.Message);}
    }
    public override void CreateAttributes() { m_attributes=new SelectPvPanelAttributes(this); }
    public string SelectedProduct => selectedModel;
    public bool HasExternalProduct => Params.Input[0].SourceCount>0 || ((Grasshopper.Kernel.Parameters.Param_String)Params.Input[0]).PersistentDataCount>0;
    public string CompactProductLabel => selectedModel.StartsWith("Heliene") ? selectedModel.Replace("Heliene 60M G1 All Black ","Heliene ") : selectedModel.Replace("NEG9R.28", "").Replace("TSM-", "Trina ")+"W";
    public void SelectProduct(string id) {
        PvPanelCatalog.Specifications(id); // Reject unsupported IDs before changing state.
        if(HasExternalProduct)return;
        if(selectedModel==id)return;
        RecordUndoEvent("Select PV panel");selectedModel=id;
        Attributes?.ExpireLayout();ExpireSolution(true);
    }
    public ContextMenuStrip CreateProductMenu() {
        var menu=new ContextMenuStrip();
        if(HasExternalProduct) {
            var hint=new ToolStripMenuItem("Product ID is supplied externally; disconnect or clear it to choose here.");hint.Enabled=false;menu.Items.Add(hint);
        }
        foreach(string id in PvPanelCatalog.ModelIds) {
            string choice=id;
            var item=new ToolStripMenuItem(id.StartsWith("Heliene")?id:"Trina Solar "+id);
            item.Checked=selectedModel==choice;item.Enabled=!HasExternalProduct;
            item.Click+=(s,e)=>SelectProduct(choice);menu.Items.Add(item);
        }
        return menu;
    }
    protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu) {
        base.AppendAdditionalComponentMenuItems(menu);Menu_AppendSeparator(menu);
        var products=Menu_AppendItem(menu,"Panel product");
        foreach(string id in PvPanelCatalog.ModelIds) {
            string choice=id;
            Menu_AppendItem(products.DropDown,id.StartsWith("Heliene")?id:"Trina Solar "+id,(s,e)=>SelectProduct(choice),!HasExternalProduct,selectedModel==choice);
        }
    }
    public override bool Write(GH_IO.Serialization.GH_IWriter w){w.SetString("SelectedProduct",selectedModel);return base.Write(w);}
    public override bool Read(GH_IO.Serialization.GH_IReader r){if(r.ItemExists("SelectedProduct"))selectedModel=r.GetString("SelectedProduct");return base.Read(r);}
}
}
