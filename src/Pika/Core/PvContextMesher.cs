using System;
using System.Collections.Generic;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace PlugInSolar {
    public static class PvContextMesher {
        // Append duplicates only. Referenced document objects and caller-owned geometry remain owned by their callers.
        public static void Append(Mesh destination, object item, RhinoDoc document,
            string label, List<string> warnings, int depth = 0) {
            if (depth > 32) throw new ArgumentException(label + " contains excessively nested block references.");
            if (item is GH_ObjectWrapper wrapper) item = wrapper.Value;
            if (item is IGH_GeometricGoo geometric && geometric.IsReferencedGeometry) {
                var reference = document?.Objects.FindId(geometric.ReferenceID);
                if (reference != null) item = reference;
            }
            if (item is IGH_Goo goo) item = goo.ScriptVariable();
            if (item is Guid id) item = document?.Objects.FindId(id);
            if (item is RhinoObject rhinoObject) item = rhinoObject.Geometry;
            if (item == null) throw new ArgumentException(label + " is null or its referenced object is missing.");

            if (item is InstanceReferenceGeometry instance) {
                var definition = document?.InstanceDefinitions.FindId(instance.ParentIdefId);
                if (definition == null) throw new ArgumentException(label + " has an unresolved block definition.");
                using (var blockMesh = new Mesh()) {
                    foreach (var child in definition.GetObjects())
                        Append(blockMesh, child, document, label + "/" + child.Id, warnings, depth + 1);
                    if (!blockMesh.Transform(instance.Xform))
                        throw new ArgumentException(label + " has an invalid block transform.");
                    destination.Append(blockMesh);
                }
                return;
            }
            if (item is Mesh mesh) {
                if (!mesh.IsValid || mesh.Faces.Count == 0)
                    throw new ArgumentException(label + " is an invalid or empty mesh.");
                destination.Append(mesh);
                return;
            }
            if (item is SubD subd) {
                using (var subdMesh = Mesh.CreateFromSubD(subd, 3)) {
                    if (subdMesh == null || !subdMesh.IsValid)
                        throw new ArgumentException(label + " could not be meshed from SubD.");
                    destination.Append(subdMesh);
                }
                return;
            }
            if (item is Curve || item is Point || item is PointCloud || item is AnnotationBase) {
                warnings.Add(label + " (" + item.GetType().Name + ") was ignored because it has no shading surface.");
                return;
            }
            if (!(item is GeometryBase geometry) || !geometry.IsValid)
                throw new ArgumentException(label + " has unsupported or invalid geometry (" + item.GetType().Name + ").");
            Brep brep = Brep.TryConvertBrep(geometry);
            if (brep == null)
                throw new ArgumentException(label + " (" + geometry.GetType().Name + ") cannot be converted to a shading surface.");
            try {
                var parts = Mesh.CreateFromBrep(brep, MeshingParameters.Default);
                if (parts == null || parts.Length == 0)
                    throw new ArgumentException(label + " could not be meshed.");
                foreach (var part in parts) {
                    using (part) {
                        if (!part.IsValid) throw new ArgumentException(label + " generated an invalid mesh.");
                        destination.Append(part);
                    }
                }
            } finally {
                if (!ReferenceEquals(brep, geometry)) brep.Dispose();
            }
        }
    }
}
