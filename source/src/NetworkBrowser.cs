using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FileCompass
{
    internal static class NetworkBrowser
    {
        internal const string Root = "network:";
        static readonly object discoveryGate=new object(); static Task<List<Entry>> discovery; static List<Entry> discovered=new List<Entry>(); static int generation;
        internal static bool DiscoveryRunning { get { lock(discoveryGate)return discovery!=null&&!discovery.IsCompleted; } }
        internal static List<Entry> Snapshot(){lock(discoveryGate)return discovered.ToList();}
        internal static Func<string, List<Entry>> TestProvider;
        internal static bool IsServer(string path) { return path != null && path.StartsWith("\\\\") && path.TrimEnd('\\').Length > 2 && path.TrimEnd('\\').Substring(2).IndexOf('\\') < 0; }
        internal static bool IsLocation(string path) { return path == Root || IsServer(path); }
        internal static bool CanMap(Entry item) { return item != null && item.IsDirectory && !item.IsParent && item.FullPath != null && item.FullPath.StartsWith("\\\\") && !IsServer(item.FullPath); }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct Share { [MarshalAs(UnmanagedType.LPWStr)] public string Name; public uint Type; [MarshalAs(UnmanagedType.LPWStr)] public string Remark; }
        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)] static extern int NetShareEnum(string server, int level, out IntPtr data, int preferred, out int read, out int total, ref int resume);
        [DllImport("netapi32.dll")] static extern int NetApiBufferFree(IntPtr data);
        internal static Task<List<Entry>> Read(string path)
        {
            if(path==Root&&TestProvider==null)return ReadDevices();
            var result = new TaskCompletionSource<List<Entry>>();
            var thread = new Thread(delegate() { try { result.TrySetResult(TestProvider != null ? TestProvider(path) : path == Root ? Devices() : Shares(path)); } catch (Exception ex) { result.TrySetException(ex); } });
            thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start(); return result.Task;
        }
        static Entry Folder(string name, string path) { return new Entry { Name = name, FullPath = path, IsDirectory = true, Icon = DesktopIcons.ToolbarDrawing("network", ToolbarTheme.Classic) }; }
        static object Call(object obj, string name, BindingFlags flags, params object[] args) { return obj.GetType().InvokeMember(name, flags, null, obj, args); }
        static void Release(object obj) { if (obj != null && Marshal.IsComObject(obj)) Marshal.FinalReleaseComObject(obj); }
        static Task<List<Entry>> ReadDevices() {
            lock(discoveryGate){
                if(discovery!=null&&!discovery.IsCompleted)return BoundedDiscovery(discovery);
                string own="\\\\"+Environment.MachineName;
                discovered=new List<Entry>{Folder(Environment.MachineName,own)};int scan=++generation;
                Action<Entry> publish=entry=>{if(entry==null)return;lock(discoveryGate){if(scan!=generation)return;int index=discovered.FindIndex(x=>String.Equals(x.FullPath,entry.FullPath,StringComparison.OrdinalIgnoreCase));if(index<0)discovered.Add(entry);else discovered[index]=entry;discovered=discovered.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();}};
                var shell=new TaskCompletionSource<List<Entry>>();
                var thread=new Thread(delegate(){try{shell.TrySetResult(Devices(publish));}catch(Exception ex){shell.TrySetException(ex);}}){IsBackground=true,Name="File Compass network discovery"};
                thread.SetApartmentState(ApartmentState.STA);thread.Start();
                discovery=DiscoverAll(shell.Task,publish);return BoundedDiscovery(discovery);
            }
        }
        static async Task<List<Entry>> DiscoverAll(Task<List<Entry>> shell,Action<Entry> publish) {
            var services=Task.Run(()=>NetworkDiscovery.Discover(publish));
            if(await Task.WhenAny(shell,Task.Delay(9000)).ConfigureAwait(false)==shell){try{await shell.ConfigureAwait(false);}catch(COMException){}catch(TargetInvocationException){}}
            else{var observe=shell.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
            await services.ConfigureAwait(false);return Snapshot();
        }
        static async Task<List<Entry>> BoundedDiscovery(Task<List<Entry>> task){
            if(await Task.WhenAny(task,Task.Delay(10000)).ConfigureAwait(false)==task)return await task.ConfigureAwait(false);
            var observe=task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);return Snapshot();
        }
        internal static Entry Device(string name,string path,string parsing) {
            if(String.IsNullOrWhiteSpace(path))return null;
            string server=IsServer(parsing)?parsing:IsServer(path)?path:null;
            if(server==null){int unc=path.LastIndexOf("\\\\",StringComparison.Ordinal);if(unc>=0&&IsServer(path.Substring(unc)))server=path.Substring(unc);}
            if(server!=null)return Folder(name,server.TrimEnd('\\'));
            if(!path.StartsWith("::{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}\\",StringComparison.OrdinalIgnoreCase))return null;
            return new Entry{Name=name,FullPath=path,IsDirectory=true,NetworkDeviceTarget=path,Icon=DesktopIcons.StockIcon(17)};
        }
        static List<Entry> Devices(Action<Entry> publish=null) {
            var found=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);foreach(var entry in Snapshot())found[entry.FullPath]=entry;
            object shell=null,folder=null;
            try{
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application",true));folder=Call(shell,"NameSpace",BindingFlags.InvokeMethod,18);
                var until=DateTime.UtcNow.AddSeconds(7);
                do{
                    object items=null;
                    try{
                        if(folder==null)break;items=Call(folder,"Items",BindingFlags.InvokeMethod);int count=Convert.ToInt32(Call(items,"Count",BindingFlags.GetProperty));
                        for(int i=0;i<count;i++){
                            object item=null;
                            try{item=Call(items,"Item",BindingFlags.InvokeMethod,i);string path=Convert.ToString(Call(item,"Path",BindingFlags.GetProperty));string name=Convert.ToString(Call(item,"Name",BindingFlags.GetProperty));string parsing=null;
                                try{parsing=Convert.ToString(Call(item,"ExtendedProperty",BindingFlags.InvokeMethod,"System.ParsingPath"));}catch(COMException){}catch(TargetInvocationException){}
                                var entry=Device(name,path,parsing);if(entry!=null){found[entry.FullPath]=entry;if(publish!=null)publish(entry);}
                            }catch(COMException){}catch(TargetInvocationException){}finally{Release(item);}
                        }
                    }finally{Release(items);}
                    var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
                }while(DateTime.UtcNow<until);
            }finally{Release(folder);Release(shell);}
            return Snapshot();
        }
        static List<Entry> Shares(string server)
        {
            var result = new List<Entry>(); int resume = 0, error;
            do {
                IntPtr buffer; int read, total;
                error = NetShareEnum(server.TrimEnd('\\'), 1, out buffer, 65536, out read, out total, ref resume);
                try {
                    if (error != 0 && error != 234) throw new Win32Exception(error);
                    for (int i = 0; i < read; i++) { var share = (Share)Marshal.PtrToStructure(IntPtr.Add(buffer, i * Marshal.SizeOf(typeof(Share))), typeof(Share));
                        if ((share.Type & 0xffff) == 0 && !share.Name.EndsWith("$", StringComparison.Ordinal)) result.Add(Folder(share.Name, server.TrimEnd('\\') + "\\" + share.Name)); }
                } finally { if (buffer != IntPtr.Zero) NetApiBufferFree(buffer); }
            } while (error == 234);
            return result.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
    public partial class FilePanel
    {
        void ShowNetworkRows(string path,bool remember,List<Entry> rows) {
                if (IsArchive) CloseArchiveContext(); if (IsRemote) { RememberRemote(); Remote = null; }
                if (remember && CurrentPath != null && CurrentPath != path) { history.Push(CurrentPath); forwardHistory.Clear(); }
                bool locationChanged=CurrentPath!=path;CurrentPath = path;
                if (path != NetworkBrowser.Root) rows.Insert(0, new Entry { Name = "[..]", FullPath = NetworkBrowser.Root, IsParent = true, IsDirectory = true, Icon = DesktopIcons.FileIcon("", true) });
                var selected=List.SelectedItem as Entry;
                bool changed=List.ItemsSource==null||!List.Items.Cast<Entry>().Select(x=>x.FullPath).SequenceEqual(rows.Select(x=>x.FullPath));
                if(changed){List.ItemsSource=rows;Sort();List.SelectedItem=selected==null?rows.FirstOrDefault():rows.FirstOrDefault(x=>x.FullPath==selected.FullPath)??rows.FirstOrDefault();} List.IsEnabled=true;
                address.Text = path == NetworkBrowser.Root ? L.Text("network.shortTitle") : path; capacity.Text = L.Text("networkBrowser.devicesAndSharesTitle");
                selectingDrive = true; driveSelector.SelectedIndex = -1; selectingDrive = false;
                foreach (var button in driveButtons.Values) { button.Background = System.Windows.Media.Brushes.Transparent; button.BorderBrush = CompactTheme.Line; }
                UpdateStatus(); UpdateConnectionButtons(); if ((changed||locationChanged)&&PathChanged != null) PathChanged(this, EventArgs.Empty);
                if (path == NetworkBrowser.Root) { status.Text = rows.Count + L.Text("networkBrowser.deviceCountHelp"); status.ToolTip = L.Text("networkBrowser.discoverySettingsHelp")+(NetworkDiscovery.AccessDeniedCount>0?"\n\n"+L.Text("networkBrowser.scanRestricted"):""); }
        }
        internal bool IsNetworkBrowser { get { return NetworkBrowser.IsLocation(CurrentPath); } }
        async Task NavigateNetwork(string path, bool remember)
        {
            CancelNavigation(); int version = navigationVersion; LocalLoading = true; List.IsEnabled = false;
            status.Text = L.Text("networkBrowser.discovering");
            Exception failure = null;
            try {
                var read = NetworkBrowser.Read(path);
                if(path==NetworkBrowser.Root&&NetworkBrowser.TestProvider==null){
                    var deadline=DateTime.UtcNow.AddSeconds(45);while((!read.IsCompleted||NetworkBrowser.DiscoveryRunning)&&DateTime.UtcNow<deadline){var snapshot=NetworkBrowser.Snapshot();await Dispatcher.InvokeAsync(delegate{if(version==navigationVersion)ShowNetworkRows(path,remember,snapshot);});if(version!=navigationVersion)return;await Task.Delay(350);}
                }
                if (await Task.WhenAny(read, Task.Delay(15000)) != read) { var observe = read.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); throw new IOException(L.Text("networkBrowser.notRespondingHelp")); }
                var rows = path==NetworkBrowser.Root&&NetworkBrowser.TestProvider==null?NetworkBrowser.Snapshot():await read.ConfigureAwait(false);
                await Dispatcher.InvokeAsync(delegate {
                if (version != navigationVersion) return;
                ShowNetworkRows(path,remember,rows);
                });
            } catch (Exception ex) { failure = ex; }
            await Dispatcher.InvokeAsync(delegate { if (version == navigationVersion) {
                if (failure != null) { status.Text = L.Text("networkBrowser.loadFailedPrefix") + failure.Message; status.ToolTip = status.Text; }
                LocalLoading = false; List.IsEnabled = true;
            } });
        }
    }
}

