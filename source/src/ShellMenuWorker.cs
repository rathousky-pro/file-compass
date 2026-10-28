using System;using System.IO;using System.Diagnostics;using System.Runtime.InteropServices;using System.Threading;using System.Threading.Tasks;using System.Web.Script.Serialization;using System.Windows;
namespace FileCompass {
 internal static class ShellMenuWorker {
#if APP_TESTS
  internal static string ExecutableForTests;
#endif
  sealed class Request {public long Owner;public string[] Paths;public bool Probe;}
  sealed class Response {public int Count;public string Error;}
  [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(uint process);
  internal static int Serve(string input,string output){var response=new Response();Window host=null;try{if(new FileInfo(input).Length>1024*1024)throw new IOException(L.Text("context.shellFailed"));var request=new JavaScriptSerializer().Deserialize<Request>(File.ReadAllText(input));if(request==null||!ShellFileMenu.Compatible(request.Paths))throw new IOException(L.Text("context.sameFolder"));var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};host=new Window{Width=1,Height=1,Left=-30000,Top=-30000,WindowStyle=WindowStyle.None,AllowsTransparency=true,Opacity=0,ShowInTaskbar=false,ShowActivated=!request.Probe};if(request.Owner!=0)new System.Windows.Interop.WindowInteropHelper(host).Owner=new IntPtr(request.Owner);host.Show();response.Count=ShellFileMenu.Show(host,request.Paths,!request.Probe);host.Close();host=null;}catch(Exception ex){response.Error=ex.Message;}finally{if(host!=null)host.Close();}File.WriteAllText(output+".tmp",new JavaScriptSerializer().Serialize(response));File.Move(output+".tmp",output);return response.Error==null?0:1;}
  // Shell extensions may keep COM apartments alive. The worker exits explicitly after
  // writing its response; it has no job object, so programs launched by a verb survive.
  internal static async Task<int> Show(Window owner,string[] paths,bool probe=false){
   if(!ShellFileMenu.Compatible(paths))throw new IOException(L.Text("context.sameFolder"));
   using(var temp=new ArchiveTemp(Path.Combine(AppPaths.ConfigDirectory,"shell-menu-workers"))){
    string input=Path.Combine(temp.Path,"request.json"),output=Path.Combine(temp.Path,"response.json");
    File.WriteAllText(input,new JavaScriptSerializer().Serialize(new Request{Owner=new System.Windows.Interop.WindowInteropHelper(owner).EnsureHandle().ToInt64(),Paths=paths,Probe=probe}));
    string executable=Process.GetCurrentProcess().MainModule.FileName;
#if APP_TESTS
    if(!String.IsNullOrEmpty(ExecutableForTests))executable=ExecutableForTests;
#endif
    using(var cancelled=new CancellationTokenSource())using(var process=new Process{StartInfo=new ProcessStartInfo(executable,"--shell-menu-worker "+SshTerminal.QuoteWindowsArgument(input)+" "+SshTerminal.QuoteWindowsArgument(output)){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden}}){
     EventHandler closed=delegate{cancelled.Cancel();};owner.Closed+=closed;
     try{process.Start();AllowSetForegroundWindow((uint)process.Id);await Task.Run(delegate{var clock=Stopwatch.StartNew();while(!process.WaitForExit(100)){if(File.Exists(output))break;cancelled.Token.ThrowIfCancellationRequested();if(clock.Elapsed.TotalSeconds>(probe?45:600))throw new IOException(L.Text("context.shellFailed"));}},cancelled.Token);
      if(!File.Exists(output)||new FileInfo(output).Length>65536)throw new IOException(L.Text("context.shellFailed"));var response=new JavaScriptSerializer().Deserialize<Response>(File.ReadAllText(output));if(response==null||response.Error!=null)throw new IOException(response==null?L.Text("context.shellFailed"):response.Error);return response.Count;
     }finally{owner.Closed-=closed;try{if(!process.HasExited){process.Kill();process.WaitForExit(2000);}}catch(InvalidOperationException){}}
    }
   }
  }
 }
}
