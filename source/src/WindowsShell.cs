using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    internal sealed class ShellLocation
    {
        internal string Path, Name;
        internal ImageSource Icon;
        internal bool Directory, Hidden, System;
        internal long Size;
    }

    // Shell objects stay on a background STA. Only strings and frozen images cross to WPF.
    internal static class WindowsShell
    {
        internal const string Computer = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
        internal const string Home = "::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}";
        const string QuickAccess = "::{679F85CB-0220-4080-B29B-5540CC05AAB6}";
        internal const string DesktopNamespace = "::{00021400-0000-0000-C000-000000000046}";
        const string NetworkNamespace = "::{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}";
        static readonly ConcurrentDictionary<string, Task<ShellLocation[]>> pending = new ConcurrentDictionary<string, Task<ShellLocation[]>>(StringComparer.OrdinalIgnoreCase);
        static readonly ConcurrentDictionary<string, string> names = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal static string Name(string path) { string value; return names.TryGetValue(path, out value) ? value : null; }
        static readonly ConcurrentDictionary<string, ImageSource> icons = new ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ShellInfo {
            internal IntPtr Icon; internal int Index; internal uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] internal string Type;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint requested, out uint attributes);
        [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(IntPtr pidl, uint attributes, out ShellInfo info, uint size, uint flags);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        internal static bool IsLocation(string path) { return path != null && path.StartsWith("::{", StringComparison.Ordinal); }
        static object Call(object obj, string name, BindingFlags flags, params object[] args) { return obj.GetType().InvokeMember(name, flags, null, obj, args); }
        static object Get(object obj, string name) { return Call(obj, name, BindingFlags.GetProperty); }
        static void Release(object obj) { if (obj != null && Marshal.IsComObject(obj)) Marshal.FinalReleaseComObject(obj); }
        internal static ImageSource Icon(string path)
        {
            ImageSource cached; if (icons.TryGetValue(path, out cached)) return cached;
            IntPtr pidl = IntPtr.Zero; ShellInfo info = new ShellInfo(); uint attributes;
            try {
                if (SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out attributes) == 0)
                    SHGetFileInfo(pidl, 0, out info, (uint)Marshal.SizeOf(typeof(ShellInfo)), 0x100 | 0x1 | 0x8);
                if (info.Icon != IntPtr.Zero) {
                    var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); image.Freeze();
                    if (icons.Count > 512) icons.Clear(); icons[path] = image; return image;
                }
            } catch (COMException) { }
            finally { if (info.Icon != IntPtr.Zero) DestroyIcon(info.Icon); if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
            return DesktopIcons.StockIcon(path == Computer ? 15 : path == Home ? 4 : 3);
        }
        internal static Task<ShellLocation[]> Roots() { return Run("@roots", ReadRoots); }
        internal static Task<ShellLocation[]> Read(string path, bool foldersOnly = false) { return Run(path + (foldersOnly ? "|folders" : "|all"), delegate { return ReadItems(path, foldersOnly); }); }
        internal static Task<ShellLocation[]> Folders(string path, bool hidden, bool system)
        {
            return Run(path + "|directories|" + hidden + "|" + system, delegate {
                return Directory.EnumerateDirectories(path).Select(x => new { Path = x, Attributes = File.GetAttributes(x) })
                    .Where(x => (hidden || (x.Attributes & FileAttributes.Hidden) == 0) && (system || (x.Attributes & FileAttributes.System) == 0))
                    .Select(x => new ShellLocation { Path = x.Path, Name = FolderName(x.Path), Directory = true, Icon = Icon(x.Path) }).ToArray();
            });
        }
        [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFolderInfo(string path, uint attributes, out ShellInfo info, uint size, uint flags);
        static string FolderName(string path)
        {
            ShellInfo info; SHGetFolderInfo(path, 0, out info, (uint)Marshal.SizeOf(typeof(ShellInfo)), 0x200);
            return String.IsNullOrEmpty(info.Name) ? System.IO.Path.GetFileName(path.TrimEnd('\\')) : info.Name;
        }
        static Task<ShellLocation[]> Run(string key, Func<ShellLocation[]> read)
        {
            // A slow provider retains its in-flight request; repeated clicks do not spawn more probes.
            lock (pending) {
                Task<ShellLocation[]> task; if (pending.TryGetValue(key, out task)) return task;
                var completion = new TaskCompletionSource<ShellLocation[]>(); task = completion.Task; pending[key] = task;
                var thread = new Thread(delegate() {
                    try { completion.TrySetResult(read()); } catch (Exception ex) { completion.TrySetException(ex); }
                    finally { Task<ShellLocation[]> ignored; pending.TryRemove(key, out ignored); }
                }) { IsBackground = true, Name = "File Compass shell folders" };
                thread.SetApartmentState(ApartmentState.STA); thread.Start(); return task;
            }
        }
        static ShellLocation Describe(object shell, object location, string path, string fallback)
        {
            object folder = null;
            try { folder = Call(shell, "NameSpace", BindingFlags.InvokeMethod, location); string name = folder == null ? fallback : Convert.ToString(Get(folder, "Title")); names[path] = name; return new ShellLocation { Path = path, Name = name, Icon = Icon(path), Directory = true }; }
            finally { Release(folder); }
        }
        static ShellLocation[] ReadRoots()
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true));
            try {
                var roots = new List<ShellLocation>();
                roots.Add(Describe(shell, Home, Home, L.Text("tree.home")));
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!String.IsNullOrEmpty(desktop)) roots.Add(Describe(shell, 0, desktop, L.Text("tree.desktop")));
                string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!String.IsNullOrEmpty(user)) roots.Add(Describe(shell, user, user, Environment.UserName));
                // Cloud providers registered in the desktop namespace supply their real paths and names.
                foreach (var item in ReadItems(DesktopNamespace, true).Where(x => x.Directory && x.Path != Computer && x.Path != Home && x.Path != NetworkBrowser.Root && !NetworkBrowser.IsServer(x.Path))) {
                    if (roots.Any(x => SamePath(x.Path, item.Path)) || IsWithin(item.Path, desktop) || IsWithin(item.Path, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory))) continue;
                    roots.Add(item);
                }
                roots.Add(Describe(shell, 17, Computer, L.Text("tree.computer")));
                var network = Describe(shell, 18, NetworkNamespace, L.Text("network.shortTitle")); network.Path = NetworkBrowser.Root; roots.Add(network);
                return roots.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToArray();
            } finally { Release(shell); }
        }
        internal static bool SamePath(string first, string second) { return String.Equals((first ?? "").TrimEnd('\\'), (second ?? "").TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        internal static bool IsWithin(string path, string parent) { return !String.IsNullOrEmpty(parent) && (SamePath(path, parent) || path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)); }
        static ShellLocation[] ReadItems(string path, bool foldersOnly = false)
        {
            object shell = null, folder = null, items = null;
            var result = new List<ShellLocation>();
            try {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true));
                folder = Call(shell, "NameSpace", BindingFlags.InvokeMethod, path == DesktopNamespace ? (object)0 : path == Computer ? (object)17 : path);
                if (folder == null && path == Home) folder = Call(shell, "NameSpace", BindingFlags.InvokeMethod, QuickAccess);
                if (folder == null) {
                    if (path == Home) return HomeFallback();
                    throw new IOException(L.Text("tree.locationUnavailable"));
                }
                items = Call(folder, "Items", BindingFlags.InvokeMethod);
                int count = Convert.ToInt32(Get(items, "Count"));
                for (int i = 0; i < count; i++) {
                    object item = null;
                    try {
                        item = Call(items, "Item", BindingFlags.InvokeMethod, i);
                        string itemPath = Convert.ToString(Get(item, "Path"));
                        bool directory = Convert.ToBoolean(Get(item, "IsFolder")), fileSystem = Convert.ToBoolean(Get(item, "IsFileSystem"));
                        if (String.IsNullOrEmpty(itemPath) || (foldersOnly && !directory)) continue;
                        if (SamePath(itemPath, NetworkNamespace)) itemPath = NetworkBrowser.Root;
                        if (!fileSystem && !IsLocation(itemPath) && itemPath != NetworkBrowser.Root) continue;
                        FileAttributes attributes = 0;
                        if (fileSystem && !SamePath(itemPath, System.IO.Path.GetPathRoot(itemPath))) { try { attributes = File.GetAttributes(itemPath); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
                        long size = 0; if (fileSystem && !directory) { try { size = Convert.ToInt64(Get(item, "Size")); } catch { } }
                        string name = Convert.ToString(Get(item, "Name")); names[itemPath] = name;
                        result.Add(new ShellLocation { Path = itemPath, Name = name, Directory = directory || !fileSystem,
                            Hidden = (attributes & FileAttributes.Hidden) != 0, System = (attributes & FileAttributes.System) != 0, Size = size,
                            Icon = path == Computer && fileSystem && SamePath(itemPath, System.IO.Path.GetPathRoot(itemPath)) ? DesktopIcons.WindowsDriveIcon(itemPath, new DriveInfo(itemPath).DriveType == DriveType.Network, true) : Icon(itemPath == NetworkBrowser.Root ? NetworkNamespace : itemPath) });
                    } catch (COMException) { } catch (TargetInvocationException) { }
                    finally { Release(item); }
                }
                if (path == Home && result.Count == 0) return HomeFallback();
                return result.ToArray();
            } finally { Release(items); Release(folder); Release(shell); }
        }
        static ShellLocation[] HomeFallback()
        {
            return new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos }
                .Select(x => Environment.GetFolderPath(x)).Where(x => !String.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => new ShellLocation { Path = x, Name = Path.GetFileName(x), Directory = true, Icon = Icon(x) }).ToArray();
        }
    }
}
