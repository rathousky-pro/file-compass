using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FileCompass
{
    public partial class Entry
    {
        double diskPercent;
        bool diskAnalyzed;
        string diskNote = "";
        public double DiskPercent { get { return diskPercent; } }
        public string DiskPercentText { get { return diskAnalyzed ? diskPercent.ToString("F1") + " %" : ""; } }
        public Visibility DiskUsageVisibility { get { return diskAnalyzed ? Visibility.Visible : Visibility.Collapsed; } }
        public string DiskUsageNote { get { return diskNote; } }
        internal void SetDiskUsage(long bytes, double percent, string note, bool visible = true)
        {
            diskAnalyzed = visible; diskPercent = Math.Max(0, Math.Min(100, percent)); diskNote = note ?? "";
            if (visible && IsDirectory) FolderSize(bytes, false);
            if (PropertyChanged != null) foreach (var property in new[] { "DiskPercent", "DiskPercentText", "DiskUsageVisibility", "DiskUsageNote" }) PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(property));
        }
    }
    internal sealed class DiskUsageItem
    {
        internal string Path; internal long Bytes; internal List<string> Warnings = new List<string>();
    }
    internal static class DiskUsageScanner
    {
        internal static DiskUsageItem Measure(string path, CancellationToken token)
        {
            var result = new DiskUsageItem { Path = path }; var pending = new Stack<string>(); pending.Push(path);
            while (pending.Count > 0) {
                token.ThrowIfCancellationRequested(); string current = pending.Pop();
                try {
                    var attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) { result.Warnings.Add(current); continue; }
                    if ((attributes & FileAttributes.Directory) == 0) { result.Bytes = checked(result.Bytes + new FileInfo(current).Length); continue; }
                    try { foreach (string child in Directory.EnumerateFileSystemEntries(current)) { token.ThrowIfCancellationRequested(); pending.Push(child); } }
                    catch (IOException ex) { result.Warnings.Add(current + ": " + ex.Message); }
                    catch (UnauthorizedAccessException ex) { result.Warnings.Add(current + ": " + ex.Message); }
                } catch (IOException ex) { result.Warnings.Add(current + ": " + ex.Message); }
                catch (UnauthorizedAccessException ex) { result.Warnings.Add(current + ": " + ex.Message); }
            }
            return result;
        }
    }
    public partial class FilePanel
    {
        CancellationTokenSource diskUsageCancellation;
        bool diskUsageShown;
        internal Task DiskUsageTask { get; private set; }
        void InitializeDiskUsage()
        {
            PathChanged += delegate { CancelDiskUsage(); diskUsageShown = false; };
            Unloaded += delegate { CancelDiskUsage(); };
        }
        void CancelDiskUsage() { var running = diskUsageCancellation; diskUsageCancellation = null; if (running != null) running.Cancel(); }
        internal void ToggleDiskUsage()
        {
            if (diskUsageCancellation != null || diskUsageShown) {
                CancelDiskUsage(); diskUsageShown = false;
                foreach (Entry item in List.Items) item.SetDiskUsage(0,0,null,false); UpdateStatus(); return;
            }
            DiskUsageTask = AnalyzeDiskUsage();
        }
        internal async Task AnalyzeDiskUsage()
        {
            if (IsRemote || IsArchive || IsVirtualBrowser || IsProjection || CurrentPath == null) {
                status.Text = L.Text("diskUsage.localOnly"); return;
            }
            var entries = List.Items.Cast<Entry>().Where(x => !x.IsParent).ToArray();
            var cancellation = diskUsageCancellation = new CancellationTokenSource();
            string path = CurrentPath; diskUsageShown = false; status.Text = L.Text("diskUsage.scanning");
            try {
                var measured = await Task.Run(delegate {
                    var data = new List<DiskUsageItem>();
                    foreach (var entry in entries) { cancellation.Token.ThrowIfCancellationRequested(); data.Add(DiskUsageScanner.Measure(entry.FullPath,cancellation.Token)); }
                    return data;
                });
                if (diskUsageCancellation != cancellation || CurrentPath != path) return;
                long total = measured.Sum(x => x.Bytes); int warnings = measured.Sum(x => x.Warnings.Count);
                for (int i = 0; i < entries.Length; i++) entries[i].SetDiskUsage(measured[i].Bytes, total == 0 ? 0 : 100.0 * measured[i].Bytes / total, String.Join(Environment.NewLine, measured[i].Warnings.Take(12)));
                diskUsageShown = true; SetDisplay("Details"); Sort();
                status.Text = L.Format("diskUsage.summary", CopyUi.Bytes(total), entries.Length, warnings);
                status.ToolTip = L.Text("diskUsage.logicalBytesHelp") + Environment.NewLine + String.Join(Environment.NewLine, measured.SelectMany(x => x.Warnings).Take(20));
            } catch (OperationCanceledException) { if (CurrentPath == path) UpdateStatus(); }
            catch (Exception ex) { if (CurrentPath == path) { status.Text = ex.Message; status.ToolTip = ex.Message; } }
            finally { if (diskUsageCancellation == cancellation) diskUsageCancellation = null; cancellation.Dispose(); }
        }
    }
}
