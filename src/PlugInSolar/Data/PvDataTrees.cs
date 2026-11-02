using System.Linq;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
namespace PlugInSolar {
public static class PvDataTrees {
 public static GH_Structure<GH_Number> Numbers(double[][] values){var tree=new GH_Structure<GH_Number>();for(int i=0;i<values.Length;i++)tree.AppendRange(values[i].Select(x=>new GH_Number(x)),new GH_Path(i));return tree;}
}
}
