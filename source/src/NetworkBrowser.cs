using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CommanderCZ
{
    internal static class NetworkBrowser
    {
        internal const string Root = "network:";
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
            var result = new TaskCompletionSource<List<Entry>>();
            var thread = new Thread(delegate() { try { result.TrySetResult(TestProvider != null ? TestProvider(path) : path == Root ? Devices() : Shares(path)); } catch (Exception ex) { result.TrySetException(ex); } });
            thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start(); return result.Task;
        }
        static Entry Folder(string name, string path) { return new Entry { Name = name, FullPath = path, IsDirectory = true, Icon = DesktopIcons.ToolbarDrawing("network", ToolbarTheme.Classic) }; }
        static object Call(object obj, string name, BindingFlags flags, params object[] args) { return obj.GetType().InvokeMember(name, flags, null, obj, args); }
        static void Release(object obj) { if (obj != null && Marshal.IsComObject(obj)) Marshal.FinalReleaseComObject(obj); }
        static List<Entry> Devices()
        {
            var found = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            object shell = null, folder = null, items = null;
            try {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true));
                folder = Call(shell, "NameSpace", BindingFlags.InvokeMethod, 18);
                if (folder != null) {
                    items = Call(folder, "Items", BindingFlags.InvokeMethod);
                    int count = Convert.ToInt32(Call(items, "Count", BindingFlags.GetProperty));
                    for (int i = 0; i < count; i++) {
                        object item = null;
                        try { item = Call(items, "Item", BindingFlags.InvokeMethod, i); string path = Convert.ToString(Call(item, "Path", BindingFlags.GetProperty));
                            if (IsServer(path)) found[path.TrimEnd('\\')] = Folder(Convert.ToString(Call(item, "Name", BindingFlags.GetProperty)), path.TrimEnd('\\'));
                        } finally { Release(item); }
                    }
                }
            } finally { Release(items); Release(folder); Release(shell); }
            string own = "\\\\" + Environment.MachineName; if (!found.ContainsKey(own)) found[own] = Folder(Environment.MachineName, own);
            return found.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
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
        internal bool IsNetworkBrowser { get { return NetworkBrowser.IsLocation(CurrentPath); } }
        async Task NavigateNetwork(string path, bool remember)
        {
            CancelNavigation(); int version = navigationVersion; LocalLoading = true; List.IsEnabled = false;
            status.Text = L.Text("networkBrowser.discovering");
            Exception failure = null;
            try {
                var read = NetworkBrowser.Read(path);
                if (await Task.WhenAny(read, Task.Delay(15000)) != read) { var observe = read.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); throw new IOException(L.Text("networkBrowser.notRespondingHelp")); }
                var rows = await read.ConfigureAwait(false);
                await Dispatcher.InvokeAsync(delegate {
                if (version != navigationVersion) return;
                if (IsArchive) CloseArchiveContext(); if (IsRemote) { RememberRemote(); Remote = null; }
                if (remember && CurrentPath != null && CurrentPath != path) { history.Push(CurrentPath); forwardHistory.Clear(); }
                CurrentPath = path;
                if (path != NetworkBrowser.Root) rows.Insert(0, new Entry { Name = "[..]", FullPath = NetworkBrowser.Root, IsParent = true, IsDirectory = true, Icon = DesktopIcons.FileIcon("", true) });
                List.ItemsSource = rows; Sort(); if (rows.Count > 0) List.SelectedIndex = 0;
                address.Text = path == NetworkBrowser.Root ? L.Text("network.shortTitle") : path; capacity.Text = L.Text("networkBrowser.devicesAndSharesTitle");
                selectingDrive = true; driveSelector.SelectedIndex = -1; selectingDrive = false;
                foreach (var button in driveButtons.Values) { button.Background = System.Windows.Media.Brushes.Transparent; button.BorderBrush = CompactTheme.Line; }
                UpdateStatus(); UpdateConnectionButtons(); if (PathChanged != null) PathChanged(this, EventArgs.Empty);
                if (path == NetworkBrowser.Root) { status.Text = rows.Count + L.Text("networkBrowser.deviceCountHelp"); status.ToolTip = L.Text("networkBrowser.discoverySettingsHelp"); }
                });
            } catch (Exception ex) { failure = ex; }
            await Dispatcher.InvokeAsync(delegate { if (version == navigationVersion) {
                if (failure != null) { status.Text = L.Text("networkBrowser.loadFailedPrefix") + failure.Message; status.ToolTip = status.Text; }
                LocalLoading = false; List.IsEnabled = true;
            } });
        }
    }
}

