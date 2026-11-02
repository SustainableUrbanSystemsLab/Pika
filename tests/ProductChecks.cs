using System;
using System.Linq;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Parameters;
using PlugInSolar;

public static class ProductChecks {
    public static string Run() {
        var passed=new List<string>();
        void Check(bool ok,string name){if(!ok)throw new Exception("FAILED: "+name);passed.Add(name);}
        double Number(GH_Component c,int output)=>((GH_Number)c.Params.Output[output].VolatileData.AllData(true).First()).Value;
        bool HasError(GH_Component c)=>c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any();
        foreach(string id in PvPanelCatalog.ModelIds) {
            var selector=new SelectPvPanelComponent();
            var expectedDims=PvPanelCatalog.Dimensions(id);
            ((Param_String)selector.Params.Input[0]).PersistentData.Append(new GH_String(id));
            var geometry=new PvGeometryGeneratorComponent();var specs=new PvModuleSpecificationsComponent();
            using(var doc=new GH_Document()) {
                doc.Enabled=true;GH_Document.EnableSolutions=true;
                doc.AddObject(selector,false);doc.AddObject(geometry,false);doc.AddObject(specs,false);
                geometry.Params.Input[4].AddSource(selector.Params.Output[0]);specs.Params.Input[7].AddSource(selector.Params.Output[4]);doc.NewSolution(true);
                Check(!HasError(selector)&&!HasError(geometry)&&!HasError(specs),id+" wired solve");
                Check(selector.Params.Output.Count==16,id+" all selector outputs");
                Check(Math.Abs(Number(geometry,3)-expectedDims.WidthM*expectedDims.HeightM)<1e-9,id+" product geometry area");
                var product=PvPanelCatalog.Specifications(id);
                Check(Number(selector,5)==product.PowerW,id+" separate power");
                Check(Number(specs,1)==product.PowerW,id+" grouped power");
                Check(Math.Abs(Number(specs,8)-product.GammaPmaxPctPerC/100)<1e-12,id+" percent-to-fraction gamma");
                Check(Number(selector,1)==expectedDims.WidthM&&Number(selector,2)==expectedDims.HeightM&&Number(selector,3)==expectedDims.ThicknessM,id+" separate dimensions");
                var grouped=PvPanelCatalog.Unwrap<PvModuleSpecsData>(selector.Params.Output[4].VolatileData.AllData(true).First());
                Check(grouped.Data.Model==id&&grouped.Data.SourceUrl==product.SourceUrl,id+" product provenance");
                Check(grouped.Duplicate() is PvModuleSpecsData,id+" typed duplicate");
                var dims=PvPanelCatalog.Unwrap<PvPanelDimensionsData>(selector.Params.Output[0].VolatileData.AllData(true).First());
                var a=new GH_IO.Serialization.GH_Archive();Check(a.AppendObject(dims,"Dims"),id+" write grouped dimensions");
                var restoredDims=new PvPanelDimensionsData();Check(a.ExtractObject(restoredDims,"Dims")&&restoredDims.Data.WidthM==expectedDims.WidthM,id+" read grouped dimensions");
                var b=new GH_IO.Serialization.GH_Archive();Check(b.AppendObject(grouped,"Specs"),id+" write grouped specs");
                var restoredSpecs=new PvModuleSpecsData();Check(b.ExtractObject(restoredSpecs,"Specs")&&restoredSpecs.Data.PowerW==product.PowerW,id+" read grouped specs");
                var manual=new Param_Number();manual.PersistentData.Append(new GH_Number(-1));doc.AddObject(manual,false);geometry.Params.Input[0].AddSource(manual);specs.Params.Input[0].AddSource(manual);doc.NewSolution(true);
                Check(!HasError(geometry)&&!HasError(specs),id+" grouped data overrides invalid manual values");
                Check(geometry.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any()&&specs.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any(),id+" override warnings");
            }
        }
        using(var doc=new GH_Document()) {
            doc.Enabled=true;var bad=new SelectPvPanelComponent();((Param_String)bad.Params.Input[0]).PersistentData.Append(new GH_String("unknown"));doc.AddObject(bad,false);doc.NewSolution(true);
            Check(HasError(bad)&&!bad.Params.Output[0].VolatileData.AllData(true).Any(g=>g!=null&&g.IsValid),"unknown product rejected without fallback: errors="+HasError(bad)+", outputs="+bad.Params.Output[0].VolatileDataCount+", message="+bad.Message);
        }
        using(var doc=new GH_Document()) {
            doc.Enabled=true;var selector=new SelectPvPanelComponent();var geometry=new PvGeometryGeneratorComponent();doc.AddObject(selector,false);doc.AddObject(geometry,false);geometry.Params.Input[4].AddSource(selector.Params.Output[4]);doc.NewSolution(true);
            Check(HasError(geometry),"wrong grouped type rejected");
        }
        using(var doc=new GH_Document()) {
            doc.Enabled=true;var specs=new PvModuleSpecificationsComponent();doc.AddObject(specs,false);doc.NewSolution(true);
            Check(!HasError(specs)&&Number(specs,1)==450,"manual specifications default solve");
            var value=new Param_Number();value.PersistentData.Append(new GH_Number(-10));doc.AddObject(value,false);specs.Params.Input[0].AddSource(value);doc.NewSolution(true);Check(HasError(specs),"invalid manual power rejected");
        }
        return passed.Count+" product checks passed\n"+string.Join("\n",passed);
    }
}
