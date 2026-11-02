using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace PlugInSolar {
/// <summary>On-component product dropdown; no separate Value List is required.</summary>
public class SelectPvPanelAttributes : GH_ComponentAttributes {
    RectangleF componentBounds;
    RectangleF dropdownBounds;
    SelectPvPanelComponent Selector => (SelectPvPanelComponent)Owner;
    public SelectPvPanelAttributes(SelectPvPanelComponent owner):base(owner) { }
    public RectangleF DropdownBounds => dropdownBounds;

    protected override void Layout() {
        base.Layout();
        componentBounds=Bounds;
        dropdownBounds=new RectangleF(componentBounds.Left,componentBounds.Bottom+4,
            System.Math.Max(210,componentBounds.Width),25);
        Bounds=RectangleF.Union(componentBounds,dropdownBounds);
    }

    protected override void Render(GH_Canvas canvas,Graphics graphics,GH_CanvasChannel channel) {
        // Preserve standard component/port layout. The additional capsule has its own bounds.
        var allBounds=Bounds;
        try {Bounds=componentBounds;base.Render(canvas,graphics,channel);}
        finally {Bounds=allBounds;}
        if(channel!=GH_CanvasChannel.Objects)return;
        bool external=Selector.HasExternalProduct;
        var palette=external||Owner.Locked?GH_Palette.Grey:GH_Palette.Normal;
        using(var capsule=GH_Capsule.CreateCapsule(dropdownBounds,palette))
            capsule.Render(graphics,Selected,Owner.Locked,false);
        var textBounds=dropdownBounds;textBounds.Inflate(-7,-2);textBounds.Width-=15;
        string text=external?"Product ID: connected input":"Product ID: "+Selector.CompactProductLabel;
        using(var format=new StringFormat {Alignment=StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})
            graphics.DrawString(text,GH_FontServer.Standard,external||Owner.Locked?Brushes.Gray:Brushes.Black,textBounds,format);
        float x=dropdownBounds.Right-11,y=dropdownBounds.Top+11;
        graphics.FillPolygon(external||Owner.Locked?Brushes.Gray:Brushes.Black,
            new[]{new PointF(x-4,y),new PointF(x+4,y),new PointF(x,y+4)});
    }

    public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender,GH_CanvasMouseEvent e) {
        if(e.Button==MouseButtons.Left&&dropdownBounds.Contains(e.CanvasLocation)) {
            if(!Owner.Locked) {
                var menu=Selector.CreateProductMenu();
                menu.Closed+=(s,args)=>menu.Dispose();
                menu.Show(sender,e.ControlLocation);
            }
            return GH_ObjectResponse.Handled;
        }
        return base.RespondToMouseDown(sender,e);
    }
    public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender,GH_CanvasMouseEvent e) {
        if(dropdownBounds.Contains(e.CanvasLocation))return GH_ObjectResponse.Handled;
        return base.RespondToMouseDoubleClick(sender,e);
    }
}
}
