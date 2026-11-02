using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace PlugInSolar {
public sealed class PvLayoutResult {
    public readonly List<Brep> Panels=new List<Brep>();
    public readonly List<Brep> Faces=new List<Brep>();
    public readonly List<Point3d> Positions=new List<Point3d>();
    public readonly List<Plane> Frames=new List<Plane>();
    public readonly List<Vector3d> Normals=new List<Vector3d>();
    public readonly List<string> Ids=new List<string>();
    public readonly List<string> Notes=new List<string>();
    public double AreaM2;
    public Vector3d MountNormal;
    public string SurfaceType;
    public int Tried, BoundaryRejected, ContextRejected;
    public bool Limited;
    public string Report => $"{Panels.Count} panels; {AreaM2:0.###} m² front area. {Tried} candidates checked; {BoundaryRejected} outside mounting region/clearance; {ContextRejected} blocked by host/context."+
        (Limited?" Maximum Panels reached; remaining candidates were not checked.":"")+" "+string.Join(" ",Notes);
}

public static class PvLayoutEngine {
    static void Finite(double v,string name,double min,double max=double.MaxValue) {
        if(double.IsNaN(v)||double.IsInfinity(v)||v<min||v>max)throw new ArgumentException(name+" must be finite and between "+min+" and "+max+".");
    }
    static Point3d InteriorPoint(BrepFace face) {
        // A trimmed face centroid can lie in a hole; use a verified UV interior sample.
        double[] fractions={.5,.25,.75,.125,.375,.625,.875,.05,.95};
        foreach(double u in fractions)foreach(double v in fractions) {
            double a=face.Domain(0).ParameterAt(u),b=face.Domain(1).ParameterAt(v);
            if(face.IsPointOnFace(a,b)==PointFaceRelation.Interior)return face.PointAt(a,b);
        }
        // Very narrow trims may evade the regular samples: try edge-adjacent candidates.
        foreach(var loop in face.Loops)foreach(var trim in loop.Trims) {
            var edge=trim.Edge;if(edge==null)continue;
            var q=edge.PointAtNormalizedLength(.5);
            if(face.ClosestPoint(q,out double u,out double v)) {
                for(int i=0;i<8;i++) {
                    double angle=i*Math.PI/4;
                    double a=u+face.Domain(0).Length*.001*Math.Cos(angle),b=v+face.Domain(1).Length*.001*Math.Sin(angle);
                    if(face.IsPointOnFace(a,b)==PointFaceRelation.Interior)return face.PointAt(a,b);
                }
            }
        }
        throw new ArgumentException("Could not identify an interior point on the mounting face. Split a very narrow/complex region into simpler planar faces.");
    }
    static bool FootprintFits(Plane grid,double minX,double minY,double maxX,double maxY,double clearance,double tolerance,Curve outer,List<Curve> holes) {
        // Shrink only by tolerance to permit boundary contact at zero clearance.
        double e=tolerance*2;
        double a=minX-clearance+e,b=maxX+clearance-e,c=minY-clearance+e,d=maxY+clearance-e;
        if(b<=a||d<=c)return false;
        using(var footprint=new Rectangle3d(grid,new Interval(a,b),new Interval(c,d)).ToNurbsCurve()) {
            if(Curve.PlanarClosedCurveRelationship(footprint,outer,grid,tolerance)!=RegionContainment.AInsideB)return false;
            foreach(var hole in holes)
                if(Curve.PlanarClosedCurveRelationship(footprint,hole,grid,tolerance)!=RegionContainment.Disjoint)return false;
        }
        return true;
    }
    static bool Overlaps(BoundingBox a,BoundingBox b,double tol) =>
        a.Min.X<=b.Max.X+tol&&a.Max.X>=b.Min.X-tol&&a.Min.Y<=b.Max.Y+tol&&a.Max.Y>=b.Min.Y-tol&&a.Min.Z<=b.Max.Z+tol&&a.Max.Z>=b.Min.Z-tol;
    static bool Collides(Brep panel,Brep obstacle,double tolerance,Plane? allowedHostContact=null) {
        if(!Overlaps(panel.GetBoundingBox(true),obstacle.GetBoundingBox(true),tolerance))return false;
        bool strict=allowedHostContact.HasValue;
        if(obstacle.IsSolid&&obstacle.IsManifold&&panel.Vertices.Any(v=>obstacle.IsPointInside(v.Location,tolerance,strict)))return true;
        if(panel.IsSolid&&obstacle.Vertices.Any(v=>panel.IsPointInside(v.Location,tolerance,strict)))return true;
        Intersection.BrepBrep(panel,obstacle,tolerance,out Curve[] curves,out Point3d[] points);
        try {
            bool crossing=(curves!=null&&curves.Length>0)||(points!=null&&points.Length>0);
            if(!crossing)return false;
            if(!allowedHostContact.HasValue)return true;
            // Permit flush contact with this mounting plane, but reject intersections elsewhere.
            var plane=allowedHostContact.Value;
            if(points!=null&&points.Any(q=>Math.Abs(plane.DistanceTo(q))>tolerance*5))return true;
            var transform=Transform.PlaneToPlane(plane,Plane.WorldXY);
            if(curves!=null)foreach(var c in curves) {
                var box=c.GetBoundingBox(transform);
                if(Math.Max(Math.Abs(box.Min.Z),Math.Abs(box.Max.Z))>tolerance*5)return true;
            }
            return false;
        } finally {if(curves!=null)foreach(var c in curves)c.Dispose();}
    }

    public static PvLayoutResult Generate(Brep panel,Plane panelFrame,Brep surface,
        double spacingXM,double spacingYM,double tiltDeg,bool landscape,double offsetM,double clearanceM,
        Brep host,IEnumerable<Brep> context,bool flip,double rotationDeg,string surfaceId,int maxPanels,
        UnitSystem units,double tolerance,bool verifyHost=true) {
        var result=new PvLayoutResult();
        if(panel==null||!panel.IsValid||!panel.IsSolid||!panel.IsBox(tolerance))throw new ArgumentException("Panel must be a valid rectangular solid from PV Geometry Generator.");
        if(surface==null||!surface.IsValid||surface.Faces.Count!=1||!surface.Faces[0].IsPlanar(tolerance))throw new ArgumentException("Mounting Surface must be one valid planar face. Deconstruct the building Brep and choose one face; curved surfaces are not supported in this version.");
        if(!panelFrame.IsValid)throw new ArgumentException("Panel Frame is invalid.");
        if(units==UnitSystem.None||units==UnitSystem.Unset||units==UnitSystem.CustomUnits)throw new ArgumentException("Use standard physical Rhino units, such as meters or millimeters.");
        Finite(tolerance,"Tolerance",double.Epsilon);Finite(spacingXM,"Horizontal Spacing",0);Finite(spacingYM,"Vertical Spacing",0);
        Finite(tiltDeg,"Tilt",-80,80);Finite(offsetM,"Surface Offset",0);Finite(clearanceM,"Edge Clearance",0);Finite(rotationDeg,"Grid Rotation",-360000,360000);
        if(maxPanels<1||maxPanels>5000)throw new ArgumentException("Maximum Panels must be between 1 and 5000.");
        double scale=RhinoMath.UnitScale(UnitSystem.Meters,units);
        double sx=spacingXM*scale,sy=spacingYM*scale,offset=offsetM*scale,clearance=clearanceM*scale;
        if(!PvPanelCatalog.Finite(scale)||scale<=0||!PvPanelCatalog.Finite(sx)||!PvPanelCatalog.Finite(sy)||!PvPanelCatalog.Finite(offset)||!PvPanelCatalog.Finite(clearance))throw new ArgumentException("Invalid or overflowing converted units.");
        var local=panel.GetBoundingBox(Transform.PlaneToPlane(panelFrame,Plane.WorldXY));
        double width=local.Max.X-local.Min.X,height=local.Max.Y-local.Min.Y,thickness=local.Max.Z-local.Min.Z;
        if(Math.Min(width,Math.Min(height,thickness))<=tolerance||Math.Abs(local.Max.Z)>tolerance*5||Math.Abs(local.Min.X+local.Max.X)>tolerance*5||Math.Abs(local.Min.Y+local.Max.Y)>tolerance*5)
            throw new ArgumentException("Panel Frame must be the centered front plane of the source panel, with thickness behind it. Connect PV Geometry Generator → Panel Frame.");
        // Confirm that this is an aligned box, not merely a box with a mismatched frame.
        using(var mass=VolumeMassProperties.Compute(panel))
            if(mass==null||Math.Abs(mass.Volume-width*height*thickness)>Math.Max(tolerance*width*height*10,mass.Volume*1e-7))throw new ArgumentException("Panel axes do not match Panel Frame. Connect the generator Panel Frame output.");

        var face=surface.Faces[0];
        if(!face.TryGetPlane(out Plane mountingPlane,tolerance))throw new ArgumentException("Mounting surface must be planar.");
        var sample=verifyHost?InteriorPoint(face):mountingPlane.Origin;
        face.ClosestPoint(sample,out double u0,out double v0);
        Vector3d normal=face.UnderlyingSurface().NormalAt(u0,v0);if(face.OrientationIsReversed)normal.Reverse();
        if(!normal.Unitize())throw new ArgumentException("Could not calculate the mounting normal.");
        var obstacles=new List<Brep>();
        try {
            if(host!=null && verifyHost) {
                if(!host.IsValid||!host.IsSolid||!host.IsManifold)throw new ArgumentException("Host Building must be a valid closed manifold Brep.");
                var normalizedHost=host.DuplicateBrep();if(normalizedHost.SolidOrientation==BrepSolidOrientation.Inward)normalizedHost.Flip();obstacles.Add(normalizedHost);
                if(normalizedHost.ClosestPoint(sample).DistanceTo(sample)>tolerance*10)throw new ArgumentException("Mounting Surface does not lie on Host Building at the tested interior point.");
                double probe=Math.Max(tolerance*20,Math.Min(.001*scale,thickness/10));
                bool plus=normalizedHost.IsPointInside(sample+normal*probe,tolerance,true),minus=normalizedHost.IsPointInside(sample-normal*probe,tolerance,true);
                if(plus&&!minus)normal.Reverse();
                else if(plus==minus)throw new ArgumentException("Host Building could not determine an outside direction at this face. Check that the selected face belongs to the host and is away from edges.");
                result.Notes.Add("Outward direction verified against Host Building.");
            } else result.Notes.Add("Candidate layout only; shading is evaluated by PV Solar Exposure. Uses the supplied face orientation; verify Mounting Normal or use Flip when no Host Building is connected.");
            if(flip)normal.Reverse();result.MountNormal=normal;
            bool roofLike=Math.Abs(normal.Z)>=Math.Sqrt(.5);
            result.SurfaceType=Math.Abs(normal.Z)>.999?"Roof-like":Math.Abs(normal.Z)<.001?"Facade-like":"Sloped";
            Vector3d y=roofLike?Vector3d.YAxis:Vector3d.ZAxis;y-=normal*(y*normal);
            if(!y.Unitize()){y=Vector3d.XAxis-normal*(Vector3d.XAxis*normal);y.Unitize();}
            var x=Vector3d.CrossProduct(y,normal);x.Unitize();
            var grid=new Plane(sample,x,y);grid.Rotate(rotationDeg*Math.PI/180,normal);
            x=grid.XAxis;y=grid.YAxis;
            var box=surface.GetBoundingBox(Transform.PlaneToPlane(grid,Plane.WorldXY));
            double w=landscape?height:width,h=landscape?width:height;
            double theta=tiltDeg*Math.PI/180,cos=Math.Cos(theta),sin=Math.Sin(theta)*(roofLike?1:-1);
            var tiltedY=y*cos+normal*sin;var panelNormal=Vector3d.CrossProduct(x,tiltedY);panelNormal.Unitize();
            double footprintW=w,footprintH=h*cos+thickness*Math.Abs(sin);
            double availableW=box.Max.X-box.Min.X-2*clearance,availableH=box.Max.Y-box.Min.Y-2*clearance;
            long columns=availableW+tolerance<footprintW?0:(long)Math.Floor((availableW+sx+tolerance)/(footprintW+sx));
            long rows=availableH+tolerance<footprintH?0:(long)Math.Floor((availableH+sy+tolerance)/(footprintH+sy));
            if(columns>20000||rows>20000||columns*rows>20000)throw new ArgumentException("More than 20,000 candidate positions. Use a smaller mounting face, larger panels, or larger spacing.");
            if(context!=null)foreach(var obstacle in context) {
                if(obstacle==null||!obstacle.IsValid)throw new ArgumentException("Context contains an invalid Brep.");
                var copy=obstacle.DuplicateBrep();if(copy.IsSolid&&copy.SolidOrientation==BrepSolidOrientation.Inward)copy.Flip();obstacles.Add(copy);
            }
            if(obstacles.Any(b=>!b.IsSolid||!b.IsManifold))result.Notes.Add("Open/non-manifold context is checked for surface intersections only, not solid containment.");
            var loops=new List<Curve>();
            try {
                Curve outer=face.OuterLoop.To3dCurve();loops.Add(outer);
                foreach(var loop in face.Loops)if(loop.LoopType==BrepLoopType.Inner)loops.Add(loop.To3dCurve());
                var holes=loops.Skip(1).ToList();
                double usedW=columns*footprintW+Math.Max(0,columns-1)*sx,usedH=rows*footprintH+Math.Max(0,rows-1)*sy;
                double startX=box.Min.X+clearance+(availableW-usedW)/2,startY=box.Min.Y+clearance+(availableH-usedH)/2;
                double frontOffset=offset+thickness*cos+h*.5*Math.Abs(sin);
                for(int row=0;row<rows;row++)for(int column=0;column<columns;column++) {
                    if(result.Panels.Count>=maxPanels){result.Limited=true;goto Finished;}
                    result.Tried++;
                    double minX=startX+column*(footprintW+sx),minY=startY+row*(footprintH+sy);
                    if(!FootprintFits(grid,minX,minY,minX+footprintW,minY+footprintH,clearance,tolerance,outer,holes)){result.BoundaryRejected++;continue;}
                    double centerX=minX+w/2,centerY=minY+h*cos/2-Math.Min(0,thickness*sin);
                    var center=grid.PointAt(centerX,centerY)+normal*frontOffset;
                    var target=landscape?new Plane(center,-tiltedY,x):new Plane(center,x,tiltedY);
                    var placed=panel.DuplicateBrep();
                    if(!placed.Transform(Transform.PlaneToPlane(panelFrame,target))){placed.Dispose();throw new ArgumentException("Panel placement transformation failed.");}
                    bool blocked=false;
                    for(int i=0;i<obstacles.Count;i++)if(Collides(placed,obstacles[i],tolerance,(!verifyHost || i==0&&host!=null)?(Plane?)new Plane(sample,normal):null)){blocked=true;break;}
                    if(blocked){placed.Dispose();result.ContextRejected++;continue;}
                    using(var front=new PlaneSurface(target,new Interval(-width/2,width/2),new Interval(-height/2,height/2)))result.Faces.Add(front.ToBrep());
                    result.Panels.Add(placed);result.Positions.Add(center);result.Frames.Add(target);result.Normals.Add(panelNormal);
                    result.Ids.Add((string.IsNullOrWhiteSpace(surfaceId)?"Surface1":surfaceId)+"/R"+(row+1)+"/C"+(column+1));
                }
                Finished: result.AreaM2=result.Panels.Count*(width/scale)*(height/scale);
                if(result.Panels.Count==0)result.Notes.Add("No panels fit. Review panel size, spacing, clearance, Flip, and obstacles.");
                return result;
            } finally {foreach(var loop in loops)loop?.Dispose();}
        } catch {
            foreach(var b in result.Panels)b.Dispose();foreach(var b in result.Faces)b.Dispose();throw;
        } finally {foreach(var b in obstacles)b.Dispose();}
    }

}
}
