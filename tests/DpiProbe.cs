using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
class DpiProbe {
 [DllImport("shcore.dll")] static extern int GetProcessDpiAwareness(IntPtr process,out int awareness);
 [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
 [DllImport("user32.dll")] static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
 static int Main(string[] args){
  using(var p=Process.Start(new ProcessStartInfo(args[0],"--self-test"){UseShellExecute=false})){
   int mode=-1;bool v2=false;bool seen=false;
   for(int i=0;i<150;i++){if(p.HasExited)break;p.Refresh();if(p.MainWindowHandle!=IntPtr.Zero){GetProcessDpiAwareness(p.Handle,out mode);v2=AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(p.MainWindowHandle),new IntPtr(-4));seen=true;break;}Thread.Sleep(50);}
   Console.WriteLine("window="+seen+"; processAwareness="+mode+"; perMonitorV2="+v2);
   if(!p.WaitForExit(20000)){Console.WriteLine("Native self-test timed out");return 2;}
   return mode==2&&v2&&p.ExitCode==0?0:1;
  }
 }
}
