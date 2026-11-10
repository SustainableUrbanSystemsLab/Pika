using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

namespace PlugInSolar {
internal sealed class PvExposureSnapshot {
    internal sealed class Panels { public string[] Ids; public Plane[] Frames; public BoundingBox[] Bounds; }
    public Panels Layout; public UnitSystem Units; public double Tolerance;
    public static PvExposureSnapshot Capture(PvLayoutBundle b) => new PvExposureSnapshot {
        Units=b.Units,Tolerance=b.Tolerance,Layout=new Panels {
            Ids=b.Layout.Ids.ToArray(),Frames=b.Layout.Frames.ToArray(),
            Bounds=b.Layout.Faces.Select((f,i)=>f.GetBoundingBox(Transform.PlaneToPlane(b.Layout.Frames[i],Plane.WorldXY))).ToArray()
        }
    };
}
public sealed partial class PvEnergyCalculatorComponent {
    sealed class ExposureJob {
        public string Key; public CancellationTokenSource Cancel=new CancellationTokenSource();
        public Task<PvExposureResult> Work; public DateTime Due;
        public List<string> Warnings=new List<string>();
    }
    readonly Dictionary<int,ExposureJob> jobs=new Dictionary<int,ExposureJob>();
    readonly Dictionary<string,PvExposureResult> cache=new Dictionary<string,PvExposureResult>();
    readonly Queue<string> cacheOrder=new Queue<string>();
    // Limit simultaneous exact calculations across components, avoiding CPU oversubscription.
    static readonly SemaphoreSlim computeGate=new SemaphoreSlim(1,1);
    string contextKey; Mesh contextMesh; List<string> contextWarnings=new List<string>();
    bool internalRefresh;
    bool exposureCacheHit;
    static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    static void GeometryKey(StringBuilder key,object item,RhinoDoc doc,int depth=0) {
        if(depth>32)throw new ArgumentException("Excessively nested context blocks.");
        if(item is GH_ObjectWrapper wrapper)item=wrapper.Value;
        if(item is IGH_GeometricGoo geo&&geo.IsReferencedGeometry) {
            var found=doc?.Objects.FindId(geo.ReferenceID);
            if(found==null)throw new ArgumentException("Missing referenced context geometry.");
            item=found.Geometry;
        }
        if(item is IGH_Goo goo)item=goo.ScriptVariable();
        if(item is Rhino.DocObjects.RhinoObject ro)item=ro.Geometry;
        if(!(item is GeometryBase g))throw new ArgumentException("Invalid context geometry.");
        // Hash actual serialized geometry, not just bounds or object IDs: edits invalidate the cache.
        key.Append(g.ToJSON(new Rhino.FileIO.SerializationOptions()));
        if(g is InstanceReferenceGeometry instance) {
            var definition=doc?.InstanceDefinitions.FindId(instance.ParentIdefId);
            if(definition==null)throw new ArgumentException("Missing context block definition.");
            foreach(var child in definition.GetObjects())GeometryKey(key,child.Geometry,doc,depth+1);
        }
    }
    void CancelJobs() {
        foreach(var j in jobs.Values)j.Cancel.Cancel();
        jobs.Clear();
    }
    public override void ExpireSolution(bool recompute) {
        // Upstream edits cancel immediately, even before a new solution starts.
        if(!internalRefresh)CancelJobs();
        base.ExpireSolution(recompute);
    }
    void Refresh(GH_Document doc,int delay) {
        if(doc==null)return;
        doc.ScheduleSolution(delay,d=> {
            if(OnPingDocument()!=d)return;
            internalRefresh=true;
            try{ExpireSolution(false);}finally{internalRefresh=false;}
        });
    }
    protected override void AfterSolveInstance() {
        base.AfterSolveInstance();
        if(jobs.Values.Any(j=>j.Work==null||!j.Work.IsCompleted))Refresh(OnPingDocument(),100);
    }
    protected override void BeforeSolveInstance() {
        base.BeforeSolveInstance();foreach(var output in Params.Output)output.ClearData();
    }
    public override void DocumentContextChanged(GH_Document document,GH_DocumentContext context) {
        if(context==GH_DocumentContext.Close||context==GH_DocumentContext.Unloaded) {
            CancelJobs();contextMesh?.Dispose();contextMesh=null;contextKey=null;cache.Clear();cacheOrder.Clear();
        }
        base.DocumentContextChanged(document,context);
    }
    public override void RemovedFromDocument(GH_Document document) {
        CancelJobs();contextMesh?.Dispose();contextMesh=null;cache.Clear();cacheOrder.Clear();
        base.RemovedFromDocument(document);
    }
    internal static int WorkerCount(int faces)=>Math.Max(1,Math.Min(Math.Min(8,Math.Max(1,Environment.ProcessorCount/2)),(int)Math.Max(1,2000000L/Math.Max(1,faces))));
    PvExposureResult RequestExposure(int iteration,PvLayoutBundle bundle,PvWeather weather,List<IGH_GeometricGoo> extra,double north,double grid,double albedo,List<string> warnings) {
        exposureCacheHit=false;
        var doc=RhinoDoc.ActiveDoc;
        var contextObjects=bundle.Context.Cast<object>().Concat(extra.Cast<object>()).ToArray();
        var contextText=new StringBuilder();
        contextText.Append((int)bundle.Units).Append('|').Append(bundle.Tolerance.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        foreach(var g in contextObjects)GeometryKey(contextText,g,doc);
        string ck=Hash(contextText.ToString());
        var snapshot=PvExposureSnapshot.Capture(bundle);
        var keyText=new StringBuilder(ck);
        foreach(var g in bundle.Layout.Panels)GeometryKey(keyText,g,doc);
        // Serialize plane/bounds explicitly; JSON does not include these snapshot fields by default.
        foreach(var frame in snapshot.Layout.Frames)keyText.Append(JsonSerializer.Serialize(frame));
        foreach(var bounds in snapshot.Layout.Bounds)keyText.Append(JsonSerializer.Serialize(bounds));
        keyText.Append(JsonSerializer.Serialize(snapshot.Layout.Ids));
        keyText.Append(JsonSerializer.Serialize(weather));
        keyText.Append(JsonSerializer.Serialize(new[]{north,grid,albedo}));
        string key=Hash(keyText.ToString());
        if(cache.TryGetValue(key,out var cached)) {
            if(contextKey==ck)warnings.AddRange(contextWarnings);
            exposureCacheHit=true;Message="Cached exact result";return cached;
        }
        if(!jobs.TryGetValue(iteration,out var job)||job.Key!=key) {
            if(job!=null)job.Cancel.Cancel();
            job=new ExposureJob{Key=key,Due=DateTime.UtcNow.AddMilliseconds(350)};jobs[iteration]=job;
            Message="Waiting for input to settle";return null;
        }
        if(job.Work==null) {
            if(DateTime.UtcNow<job.Due){Message="Waiting for input to settle";return null;}
            if(contextKey!=ck||contextMesh==null) {
                var replacement=new Mesh();var notes=new List<string>();
                try {for(int i=0;i<contextObjects.Length;i++)PvContextMesher.Append(replacement,contextObjects[i],doc,"Context item "+i,notes);replacement.Compact();}
                catch{replacement.Dispose();throw;}
                contextMesh?.Dispose();contextMesh=replacement;contextKey=ck;contextWarnings=notes;
            }
            var mesh=contextMesh.DuplicateMesh();
            try {foreach(var panel in bundle.Layout.Panels)PvContextMesher.Append(mesh,panel,doc,"Panel",job.Warnings);mesh.Compact();}
            catch{mesh.Dispose();throw;}
            job.Warnings.AddRange(contextWarnings);
            var token=job.Cancel.Token;int workers=WorkerCount(mesh.Faces.Count);
            job.Work=Task.Run(async()=> {
                bool acquired=false;
                try {
                    await computeGate.WaitAsync(token);acquired=true;token.ThrowIfCancellationRequested();
                    return CalculateExposure(snapshot,weather,mesh,north,grid,albedo,job.Warnings,token,workers);
                } finally {mesh.Dispose();if(acquired)computeGate.Release();}
            });
            Message="Calculating exact grid…";return null;
        }
        if(!job.Work.IsCompleted){Message="Calculating exact grid…";return null;}
        var result=job.Work.GetAwaiter().GetResult();warnings.AddRange(job.Warnings);
        // Bound retained results. Oversized results remain in the completed job, not the LRU.
        long size=result.CellIrradiation.Sum(x=>(long)x.Length)*128L+result.Poa.Sum(x=>(long)x.Length)*32L;
        if(size<64000000) {
            while(cacheOrder.Count>=2)cache.Remove(cacheOrder.Dequeue());
            cache[key]=result;cacheOrder.Enqueue(key);
        }
        return result;
    }
}
}
