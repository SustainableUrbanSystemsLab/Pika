using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using PlugInSolar;

public static class DropdownChecks {
    public static string Run() {
        int count=0;
        void Check(bool ok,string name) {if(!ok)throw new Exception("FAILED: "+name);count++;}
        var selector=new SelectPvPanelComponent();selector.CreateAttributes();
        selector.Attributes.Pivot=new PointF(300,200);selector.Attributes.PerformLayout();
        var attrs=(SelectPvPanelAttributes)selector.Attributes;
        Check(attrs.DropdownBounds.Width>=210&&selector.Attributes.Bounds.Contains(attrs.DropdownBounds),"dropdown is inside component picking bounds");
        using(var menu=selector.CreateProductMenu()) {
            Check(menu.Items.Count==7,"all seven options in built-in menu");
            Check(menu.Items.OfType<ToolStripMenuItem>().Count(i=>i.Checked)==1,"one checked selection");
            menu.Items.OfType<ToolStripMenuItem>().First(i=>i.Text.Contains("Heliene")&&i.Text.Contains("305W")).PerformClick();
            Check(selector.SelectedProduct=="Heliene 60M G1 All Black 305W","built-in menu changes product");
        }
        using(var doc=new GH_Document()) {
            doc.Enabled=true;GH_Document.EnableSolutions=true;
            var geometry=new PvGeometryGeneratorComponent();doc.AddObject(selector,false);doc.AddObject(geometry,false);geometry.Params.Input[4].AddSource(selector.Params.Output[0]);doc.NewSolution(true);
            Check(selector.Params.Output[5].VolatileData.AllData(true).First().ToString()=="305","dropdown updates output without ID wire");
            Check(!geometry.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(),"dropdown drives geometry");
            var archive=new GH_IO.Serialization.GH_Archive();archive.AppendObject(selector,"Selector");
            var restored=new SelectPvPanelComponent();archive.ExtractObject(restored,"Selector");
            Check(restored.SelectedProduct==selector.SelectedProduct,"dropdown selection restored");
            var text=new GH_Panel();text.SetUserText("TSM-430NEG9R.28");doc.AddObject(text,false);selector.Params.Input[0].AddSource(text);doc.NewSolution(true);
            Check(selector.HasExternalProduct,"external input detected");
            using(var menu=selector.CreateProductMenu())Check(menu.Items.OfType<ToolStripMenuItem>().All(i=>!i.Enabled),"dropdown disabled while externally controlled");
            selector.SelectProduct("Heliene 60M G1 All Black 320W");
            Check(selector.SelectedProduct=="Heliene 60M G1 All Black 305W","external control protects internal selection");
            Check(selector.Params.Output[5].VolatileData.AllData(true).First().ToString()=="430","wire still overrides dropdown");
            selector.Params.Input[0].RemoveAllSources();doc.NewSolution(true);
            Check(selector.Params.Output[5].VolatileData.AllData(true).First().ToString()=="305","disconnect restores dropdown choice");
        }
        return count+" dropdown checks passed";
    }
}
