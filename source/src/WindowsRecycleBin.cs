using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FileCompass
{
    internal static partial class WindowsShell
    {
        internal const string RecycleBin = "::{645FF040-5081-101B-9F08-00AA002F954E}";
        const int RecycleBinFolder = 10;
        const int OperationCancelled = unchecked((int)0x800704C7);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHEmptyRecycleBin(IntPtr owner, string driveRoot, uint flags);

        static void VisitRecycleItems(Action<object, string> visit)
        {
            object shell = null, folder = null, items = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true));
                folder = Call(shell, "NameSpace", BindingFlags.InvokeMethod, RecycleBinFolder);
                if (folder == null) throw new IOException(L.Text("tree.locationUnavailable"));
                items = Call(folder, "Items", BindingFlags.InvokeMethod);
                int count = Convert.ToInt32(Get(items, "Count"));
                // Work backwards so a synchronous restore can remove items from a live collection.
                for (int index = count - 1; index >= 0; index--)
                {
                    object item = null;
                    try
                    {
                        item = Call(items, "Item", BindingFlags.InvokeMethod, index);
                        if (item == null) continue;
                        string id = Convert.ToString(Get(item, "Path"));
                        if (!String.IsNullOrEmpty(id)) visit(item, id);
                    }
                    finally { Release(item); }
                }
            }
            finally { Release(items); Release(folder); Release(shell); }
        }

        static ShellLocation[] ReadRecycleBin(bool foldersOnly)
        {
            if (foldersOnly) return new ShellLocation[0];
            var result = new List<ShellLocation>();
            VisitRecycleItems((item, id) =>
            {
                bool folder = Convert.ToBoolean(Get(item, "IsFolder"));
                string name = Convert.ToString(Get(item, "Name"));
                string originalFolder = null;
                try
                {
                    originalFolder = Convert.ToString(Call(item, "ExtendedProperty", BindingFlags.InvokeMethod, "System.Recycle.DeletedFrom"));
                }
                catch (COMException) { }
                catch (TargetInvocationException) { }
                long size = 0;
                if (!folder)
                {
                    try { size = Convert.ToInt64(Get(item, "Size")); }
                    catch (COMException) { }
                    catch (TargetInvocationException) { }
                }
                result.Add(new ShellLocation
                {
                    Path = "recycle-item:" + id,
                    RecycleId = id,
                    Name = name,
                    OriginalFolder = originalFolder,
                    Directory = folder,
                    Size = size,
                    Icon = DesktopIcons.StockIcon(folder ? 3 : 0)
                });
            });
            return result.ToArray();
        }

        internal static Task RestoreRecycleItems(string[] ids)
        {
            return RunRecycleOperation(delegate
            {
                var remaining = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
                var submitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                VisitRecycleItems((item, id) =>
                {
                    if (!remaining.Contains(id) || !submitted.Add(id)) return;
                    Call(item, "InvokeVerb", BindingFlags.InvokeMethod, "undelete");
                });
                if (submitted.Count != remaining.Count) throw new IOException(L.Text("recycle.restoreIncomplete"));
                // Shell automation can return before the operation has updated the namespace.
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    var stillPresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    VisitRecycleItems((item, id) => { if (remaining.Contains(id)) stillPresent.Add(id); });
                    if (stillPresent.Count == 0) return;
                    remaining = stillPresent;
                    Thread.Sleep(150);
                }
                throw new IOException(L.Text("recycle.restoreIncomplete"));
            });
        }

        internal static Task EmptyRecycleBin(IntPtr owner)
        {
            return RunRecycleOperation(delegate
            {
                // Windows owns the confirmation and progress UI for all drives.
                int result = SHEmptyRecycleBin(owner, null, 0);
                if (result != OperationCancelled) Marshal.ThrowExceptionForHR(result);
            });
        }

        static Task RunRecycleOperation(Action work)
        {
            var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { work(); completion.TrySetResult(null); }
                catch (Exception ex) { completion.TrySetException(ex); }
            }) { IsBackground = true, Name = "File Compass Recycle Bin" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }
    }
}
