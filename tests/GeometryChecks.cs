using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using PlugInSolar;

public static class GeometryChecks {
    static Vector3d FrontNormal(Brep b) { var f=b.Faces[0]; var n=f.UnderlyingSurface().NormalAt(f.Domain(0).Mid,f.Domain(1).Mid); if(f.OrientationIsReversed)n.Reverse();n.Unitize();return n; }
    public static string Run() {
        var passed = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new Exception("FAILED: " + name); passed.Add(name); }
        void Near(double actual, double expected, string name) { Check(Math.Abs(actual-expected) < 1e-7 * Math.Max(1, Math.Abs(expected)), name); }
        void Reject(double w, double h, double t, Plane p, UnitSystem u, double tol, string name) {
            try { PvGeometryGeneratorComponent.Generate(w,h,t,p,u,tol,out var a,out var b,out var c,out var d); a.Dispose();b.Dispose(); }
            catch (ArgumentException) { passed.Add(name); return; }
            throw new Exception("FAILED: " + name);
        }
        foreach (var units in new[] { UnitSystem.Meters, UnitSystem.Millimeters, UnitSystem.Feet }) {
            double scale = RhinoMath.UnitScale(UnitSystem.Meters, units);
            PvGeometryGeneratorComponent.Generate(1.1,1.7,.035,Plane.WorldXY,units,1e-6,
                out var panel,out var face,out var normal,out var area);
            using(panel) using(face) {
                Check(panel.IsSolid && panel.IsValid, units + " valid closed solid");
                var box = panel.GetBoundingBox(true);
                Near(box.Max.X-box.Min.X,1.1*scale,units + " width");
                Near(box.Max.Y-box.Min.Y,1.7*scale,units + " height");
                Near(box.Min.Z,-.035*scale,units + " thickness behind front");
                Near(box.Max.Z,0,units + " front stays at reference plane");
                Near(area,1.87,units + " area remains m2");
                using(var mass=AreaMassProperties.Compute(face)) Near(mass.Area/(scale*scale),area,units + " actual face area");
                Check(face.Faces.Count==1,units + " single analysis face");
                var active=FrontNormal(face);
                Near(active*normal,1,units + " front face agrees with output normal");
            }
        }
        var plane = new Plane(new Point3d(23,41,57),Vector3d.XAxis,Vector3d.ZAxis);
        PvGeometryGeneratorComponent.Generate(1.1,1.7,.035,plane,UnitSystem.Meters,1e-6,
            out var tiltedPanel,out var tiltedFace,out var tiltedNormal,out var tiltedArea);
        using(tiltedPanel) using(tiltedFace) using(var mass=AreaMassProperties.Compute(tiltedFace)) {
            Near(mass.Centroid.DistanceTo(plane.Origin),0,"translated front center");
            Near(tiltedNormal*plane.ZAxis,1,"rotated outward normal");
            Near(FrontNormal(tiltedFace)*plane.ZAxis,1,"rotated face orientation");
        }
        Reject(0,1,.03,Plane.WorldXY,UnitSystem.Meters,1e-6,"zero width rejected");
        Reject(1,-1,.03,Plane.WorldXY,UnitSystem.Meters,1e-6,"negative height rejected");
        Reject(1,1,0,Plane.WorldXY,UnitSystem.Meters,1e-6,"zero thickness rejected");
        Reject(double.NaN,1,.03,Plane.WorldXY,UnitSystem.Meters,1e-6,"NaN rejected");
        Reject(1,1,double.PositiveInfinity,Plane.WorldXY,UnitSystem.Meters,1e-6,"infinite thickness rejected");
        Reject(1,1,.03,Plane.Unset,UnitSystem.Meters,1e-6,"invalid plane rejected");
        Reject(1,1,.03,Plane.WorldXY,UnitSystem.None,1e-6,"unitless model rejected");
        Reject(1,1,.03,Plane.WorldXY,UnitSystem.Meters,.04,"sub-tolerance dimension rejected");
        var component = new PvGeometryGeneratorComponent();
        using(var doc=new Grasshopper.Kernel.GH_Document()) {
            doc.Enabled=true; Grasshopper.Kernel.GH_Document.EnableSolutions=true; doc.AddObject(component,false); component.ExpireSolution(false); doc.NewSolution(true);
            Check(!component.RuntimeMessages(Grasshopper.Kernel.GH_RuntimeMessageLevel.Error).GetEnumerator().MoveNext(),"component default solve has no errors");
            for(int i=0;i<4;i++) Check(component.Params.Output[i].VolatileDataCount==1,"component output " + i);
        }
        return passed.Count + " checks passed\n" + string.Join("\n",passed);
    }
}
