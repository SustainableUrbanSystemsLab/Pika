"""Run current C# checks in Rhino ScriptEditor after opening Grasshopper."""
from pathlib import Path
import System
root=Path(__file__).resolve().parents[1]
scope={'__name__':'solar_builder'}
exec((root/'scripts/build_in_rhino.py').read_text(),scope)
# Unique names allow rerunning without replacing an assembly already loaded by Rhino.
name='PlugInSolarChecks_'+System.Guid.NewGuid().ToString('N')
binary=scope['build'](str(root),name,True)
assembly=System.Reflection.Assembly.LoadFile(binary)
for check in ['GeometryChecks','ProductChecks','DropdownChecks','CandidateLayoutChecks','SolarWorkflowChecks']:
    args=System.Array[System.Object]([str(root)]) if check=='SolarWorkflowChecks' else None
    print(str(assembly.GetType(check).GetMethod('Run').Invoke(None,args)).split('\n')[0])
