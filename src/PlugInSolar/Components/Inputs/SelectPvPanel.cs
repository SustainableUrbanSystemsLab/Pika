using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Grasshopper.Kernel;

namespace PlugInSolar {
public class SelectPvPanelComponent : GH_Component {
    string selectedModel=PvPanelCatalog.DefaultModel;
    public SelectPvPanelComponent():base("Select PV Panel","Select PV","Choose a catalog product using the on-component Product ID dropdown or an optional exact product ID wire. Outputs grouped dimensions/specifications and separate STC values.","Plug-In Solar","01 Inputs") { }
    public override Guid ComponentGuid=>new Guid("a442f374-5e0d-4674-94dc-d3101f20130e");
    protected override Bitmap Icon=>Assets.Icon("panel_orientation");
    protected override void RegisterInputParams(GH_InputParamManager p) {
        int i=p.AddTextParameter("Product ID","ID","Optional catalog model ID. Overrides the built-in dropdown when supplied.",GH_ParamAccess.item);p[i].Optional=true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p) {
        p.AddGenericParameter("Panel Dimensions","Dims","Grouped dimensions in meters. Connect to PV Geometry Generator → Panel Dimensions.",GH_ParamAccess.item);
        p.AddNumberParameter("Width","W","Panel width in meters.",GH_ParamAccess.item);
        p.AddNumberParameter("Height","H","Panel height in meters.",GH_ParamAccess.item);
        p.AddNumberParameter("Thickness","T","Panel thickness in meters.",GH_ParamAccess.item);
        p.AddGenericParameter("Module Specifications","Specs","Grouped STC electrical properties. Connect to PV Module Specifications → Reference Specifications.",GH_ParamAccess.item);
        RegisterValues(p);
        p.AddTextParameter("Product","Product","Manufacturer and exact model ID.",GH_ParamAccess.item);
        p.AddTextParameter("Datasheet","Source","Manufacturer source and revision used for this catalog snapshot.",GH_ParamAccess.item);
        p.AddTextParameter("Available Products","Catalog","Supported product IDs for a Value List or text input.",GH_ParamAccess.list);
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
    protected override void SolveInstance(IGH_DataAccess da) {
        try {
            string id="";bool supplied=da.GetData(0,ref id);
            if(supplied&&string.IsNullOrWhiteSpace(id))throw new ArgumentException("Product ID cannot be blank. Disconnect it to use the built-in dropdown.");
            var specs=PvPanelCatalog.Specifications(supplied?id:selectedModel);
            var dims=PvPanelCatalog.Dimensions(specs.Model);
            da.SetData(0,new PvPanelDimensionsData(dims));da.SetData(1,dims.WidthM);da.SetData(2,dims.HeightM);da.SetData(3,dims.ThicknessM);
            da.SetData(4,new PvModuleSpecsData(specs));PvModuleSpecificationsComponent.SetValues(da,5,specs);
            da.SetData(13,specs.Manufacturer+" "+specs.Model);da.SetData(14,specs.SourceUrl+" | "+specs.Revision);da.SetDataList(15,PvPanelCatalog.ModelIds);
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
