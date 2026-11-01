using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileCompass
{
    public partial class FilePanel
    {
        async void BeginFolderSize(Entry entry) { await CalculateFolderSize(entry); }
        internal async Task CalculateFolderSize(Entry entry)
        {
            if (entry.SizeLoading || entry.IsLink || IsVirtualBrowser) return;
            entry.FolderSize(null, true);
            if (List.Items.Contains(entry)) UpdateStatus();
            var archive = Archive; RemoteConnection connection = null;
            try
            {
                connection = IsRemote ? Remote.TransferConnection() : null;
                long bytes = await Task.Run(delegate
                {
                    if (archive != null) { string prefix = entry.FullPath.TrimStart('/').TrimEnd('/') + "/"; return archive.Items.Where(x => !x.Directory && x.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Bytes); }
                    if (connection != null) { using (connection) { connection.OpenTransfer(); return RemoteFolderBytes(connection, entry.FullPath); } }
                    return LocalFolderBytes(entry.FullPath);
                });
                await Dispatcher.InvokeAsync(new Action(delegate { entry.FolderSize(bytes, false); if (List.Items.Contains(entry)) UpdateStatus(); })).Task;
            }
            catch (Exception ex) { Dispatcher.Invoke(new Action(delegate { entry.FolderSize(null, false); MessageBox.Show(System.Windows.Window.GetWindow(this), L.Text("folderSize.incompleteWarningPrefix") + ex.Message, L.Text("folderSize.title")); })); }
        }
        internal static long LocalFolderBytes(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return 0;
            long total = 0;
            var pending = new Stack<DirectoryInfo>(); pending.Push(new DirectoryInfo(path));
            while (pending.Count > 0)
            {
                foreach (var child in pending.Pop().EnumerateFileSystemInfos())
                {
                    var attributes = child.Attributes; if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push((DirectoryInfo)child);
                    else total = checked(total + ((FileInfo)child).Length);
                }
            }
            return total;
        }
        static long RemoteFolderBytes(RemoteConnection connection, string path)
        {
            long total = 0;
            foreach (var file in connection.Session.ListDirectory(path).Files)
            {
                if (file.IsThisDirectory || file.IsParentDirectory || Char.ToUpperInvariant(file.FileType) == 'L') continue;
                total = checked(total + (file.IsDirectory ? RemoteFolderBytes(connection, file.FullName) : file.Length));
            }
            return total;
        }
    }
}

