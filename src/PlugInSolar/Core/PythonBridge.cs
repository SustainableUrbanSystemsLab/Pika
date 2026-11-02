using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
namespace PlugInSolar {
public static class PythonBridge {
    public static string RunPython(string request) {
        string folder=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string root=Directory.GetParent(Directory.GetParent(folder).FullName).FullName;
        string python=Path.Combine(root,".venv","bin","python");
        if(!File.Exists(python))throw new FileNotFoundException("Python environment missing. Run scripts/build_plugin.sh in the repository.");
        var info=new ProcessStartInfo(python){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add(Path.Combine(folder,"bridge.py"));
        using(var process=Process.Start(info)) {
            var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
            process.StandardInput.Write(request);process.StandardInput.Close();
            if(!process.WaitForExit(240000)){process.Kill(true);throw new TimeoutException("Solar preparation exceeded 240 seconds. Reduce the weather period.");}
            string text=output.GetAwaiter().GetResult();string error=errors.GetAwaiter().GetResult();
            if(string.IsNullOrWhiteSpace(text))throw new Exception("Python calculation failed: "+error);
            return text;
        }
    }
}
}
