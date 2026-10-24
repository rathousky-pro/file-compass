using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal sealed class LockingProcess {
        internal int Pid;internal long Started;internal string Name;
        public override string ToString(){return Name+" (PID "+Pid+")";}
        internal void Activate(){using(var process=Process.GetProcessById(Pid)){if(process.StartTime.ToUniversalTime().ToFileTimeUtc()!=Started)throw new IOException(L.Text("tools1.processChanged"));var handle=process.MainWindowHandle;if(handle==IntPtr.Zero)throw new IOException(L.Text("tools1.noProcessWindow"));ShowWindow(handle,9);SetForegroundWindow(handle);}}
        [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
        [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
    }
    internal static class FileLocks {
        [StructLayout(LayoutKind.Sequential)]struct UniqueProcess{internal int Id;internal System.Runtime.InteropServices.ComTypes.FILETIME Started;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct ProcessInfo{
            internal UniqueProcess Process;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]internal string Application;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]internal string Service;
            internal int Type;internal uint Status,Session;
            [MarshalAs(UnmanagedType.Bool)]internal bool Restartable;
        }
        [DllImport("rstrtmgr.dll",CharSet=CharSet.Unicode)]static extern int RmStartSession(out uint handle,int flags,StringBuilder key);
        [DllImport("rstrtmgr.dll",CharSet=CharSet.Unicode)]static extern int RmRegisterResources(uint handle,uint files,string[] paths,uint apps,IntPtr processes,uint services,IntPtr serviceNames);
        [DllImport("rstrtmgr.dll")]static extern int RmGetList(uint handle,out uint needed,ref uint count,[In,Out]ProcessInfo[] info,ref uint reasons);
        [DllImport("rstrtmgr.dll")]static extern int RmEndSession(uint handle);
        internal static List<LockingProcess> Find(IEnumerable<string> paths){
            var files=paths.Where(x=>!String.IsNullOrWhiteSpace(x)&&Path.IsPathRooted(x)&&File.Exists(x)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray();
            if(files.Length==0)return new List<LockingProcess>();uint handle;int code=RmStartSession(out handle,0,new StringBuilder(33));if(code!=0)throw new Win32Exception(code);
            try{code=RmRegisterResources(handle,(uint)files.Length,files,0,IntPtr.Zero,0,IntPtr.Zero);if(code!=0)throw new Win32Exception(code);
                for(int retry=0;retry<4;retry++){uint needed,count=0,reasons=0;code=RmGetList(handle,out needed,ref count,null,ref reasons);if(code==0)return new List<LockingProcess>();if(code!=234||needed>10000)throw new Win32Exception(code);var info=new ProcessInfo[needed];count=needed;code=RmGetList(handle,out needed,ref count,info,ref reasons);if(code==234)continue;if(code!=0)throw new Win32Exception(code);return info.Take((int)count).Select(x=>new LockingProcess{Pid=x.Process.Id,Started=((long)x.Process.Started.dwHighDateTime<<32)|(uint)x.Process.Started.dwLowDateTime,Name=x.Application}).ToList();}
                throw new IOException(L.Text("tools1.locksChanged"));
            }finally{RmEndSession(handle);}
        }
        internal static async void Show(Window owner,IEnumerable<string> paths){
            var window=new Window();CopyUi.Style(window,owner,L.Text("tools1.locks"),600);var panel=new StackPanel();CopyUi.SectionedSurface(window,panel);
            var status=CopyUi.Label(L.Text("state.preparing"));panel.Children.Add(status);var list=new ListBox{MinHeight=100,MaxHeight=260};panel.Children.Add(list);
            var open=CopyUi.Button(L.Text("tools1.activateProcess"),delegate{try{var selected=list.SelectedItem as LockingProcess;if(selected!=null)selected.Activate();}catch(Exception ex){status.Text=ex.Message;}});open.IsEnabled=false;panel.Children.Add(open);panel.Children.Add(CopyUi.Button(L.Text("action.close"),window.Close));bool closed=false;window.Closed+=delegate{closed=true;};window.Show();
            try{var result=await Task.Run(()=>Find(paths));if(closed)return;list.ItemsSource=result;list.SelectedIndex=result.Count>0?0:-1;open.IsEnabled=result.Count>0;status.Text=L.Text(result.Count==0?"tools1.noLocks":"tools1.locksHelp");}catch(Exception ex){if(!closed)status.Text=L.Text("tools1.locksUnavailable")+"\n"+ex.Message;}
        }
    }
}
