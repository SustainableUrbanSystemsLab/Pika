"""Optional source rebuild using Rhino 8's bundled Python and C# compiler.
In Rhino ScriptEditor, open this saved file as Python 3 and Run. Start Grasshopper first.
The repository root is discovered from this script's scripts/ folder.
"""
from pathlib import Path
import os
from datetime import datetime
from uuid import uuid4
import System
from System import Array,Func
from System.IO import File,Stream
# Ensure Rhino's bundled compiler is loaded even in a fresh ScriptEditor session.
rhino_resources=Path(System.Type.GetType('Rhino.RhinoApp, RhinoCommon').Assembly.Location).parent
for compiler_dll in ['Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll']:
    System.Reflection.Assembly.LoadFrom(str(rhino_resources/compiler_dll))
from Microsoft.CodeAnalysis import MetadataReference,SyntaxTree,OutputKind,OptimizationLevel,ResourceDescription
from Microsoft.CodeAnalysis.CSharp import CSharpCompilation,CSharpCompilationOptions,CSharpSyntaxTree

def build(package_root,output_name='Pika',with_tests=False):
    root=Path(package_root);src=root/'src/Pika';out=root/'build'/('pika-'+datetime.now().strftime('%Y%m%d-%H%M%S')+'-'+uuid4().hex[:8]);out.mkdir(parents=True,exist_ok=True)
    paths=set(str(System.AppContext.GetData('TRUSTED_PLATFORM_ASSEMBLIES')).split(os.pathsep))
    for a in System.AppDomain.CurrentDomain.GetAssemblies():
        try:
            if not a.IsDynamic and a.Location and not a.Location.lower().endswith('.gha'):
                paths.add(str(a.Location))
        except System.BadImageFormatException:
            raise RuntimeError('A loaded assembly was overwritten. Save your work and restart Rhino before building.')
    resources_root=Path('/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources')
    for name in ['Grasshopper.dll','GH_IO.dll']:paths.add(str(resources_root/'ManagedPlugIns/GrasshopperPlugin.rhp'/name))
    refs=[MetadataReference.CreateFromFile(p) for p in paths if Path(p).is_file() and not p.endswith('.gha')]
    trees=[CSharpSyntaxTree.ParseText(File.ReadAllText(str(p)),path=str(p)) for p in sorted(src.rglob('*.cs')) if not {'obj','bin'}&set(p.relative_to(src).parts)]  # an SDK build leaves generated .cs under obj/
    if with_tests:
        for check in sorted((root/'tests').glob('*Checks.cs')):
            trees.append(CSharpSyntaxTree.ParseText(File.ReadAllText(str(check))))
    resources=[]
    def resource(path):return ResourceDescription('PlugInSolar.Assets.'+path.name,Func[Stream](lambda:File.OpenRead(str(path))),True)
    for p in sorted((src/'Assets').glob('*-24.png')):resources.append(resource(p))
    comp=CSharpCompilation.Create(output_name).WithOptions(CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithOptimizationLevel(OptimizationLevel.Release)).AddSyntaxTrees(Array[SyntaxTree](trees)).AddReferences(Array[MetadataReference](refs))
    target=out/(output_name+('.dll' if with_tests else '.gha'));staged=target.with_name(target.name+'.new')
    stream=File.Create(str(staged))
    try:result=comp.Emit(stream,manifestResources=Array[ResourceDescription](resources))
    finally:stream.Dispose()
    errors=[str(d) for d in result.Diagnostics if str(d.Severity)=='Error']
    if not result.Success:
        staged.unlink(missing_ok=True);raise RuntimeError('\n'.join(errors))
    os.replace(str(staged),str(target))
    import shutil
    for helper in (root/'python').glob('*.py'):shutil.copy2(str(helper),str(out/helper.name))
    print('Built:',target)
    print('Compiler runtime:',System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription)
    return str(target)

if __name__=='__main__':
    # Rhino ScriptEditor supplies the saved file's path as __file__.
    build(Path(__file__).resolve().parents[1])
