using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
namespace FileCompass
{
    internal static class ExitPreferences
    {
        internal static void Merge(ProgramSettings stored, ProgramSettings current)
        {
            if (stored.SaveFoldersOnExit) { stored.LeftFolder = current.LeftFolder; stored.RightFolder = current.RightFolder; }
            if (stored.SaveWindowOnExit) {
                stored.WindowLeft = current.WindowLeft; stored.WindowTop = current.WindowTop; stored.WindowWidth = current.WindowWidth; stored.WindowHeight = current.WindowHeight; stored.Maximized = current.Maximized;
            }
            if (stored.SaveViewOnExit) {
                stored.LeftView = current.LeftView; stored.RightView = current.RightView; stored.ColumnWidths = current.ColumnWidths; stored.SplitRatio = current.SplitRatio;
                stored.PanelLayout = current.PanelLayout; stored.SinglePanelRight = current.SinglePanelRight; stored.StackedSplitRatio = current.StackedSplitRatio;
                stored.LastDualLayout = current.LastDualLayout;
            }
            stored.LeftTabs = MergeTabs(stored.LeftTabs, current.LeftTabs, stored.LeftTabIndex, stored.LeftView, stored.SaveTabsOnExit, stored.SaveViewOnExit);
            stored.RightTabs = MergeTabs(stored.RightTabs, current.RightTabs, stored.RightTabIndex, stored.RightView, stored.SaveTabsOnExit, stored.SaveViewOnExit);
            if (stored.SaveTabsOnExit) { stored.LeftTabIndex = current.LeftTabIndex; stored.RightTabIndex = current.RightTabIndex; }
        }
        static SavedTab Copy(SavedTab tab)
        { return new SavedTab { Projection = tab.Projection, Filter = tab.Filter, Path = tab.Path, Home = tab.Home, Label = tab.Label, Caption = tab.Caption, ProfileId = tab.ProfileId, ArchiveFile = tab.ArchiveFile, ArchivePrefix = tab.ArchivePrefix, Locked = tab.Locked, View = tab.View }; }
        static SavedTab[] MergeTabs(SavedTab[] previous, SavedTab[] current, int selected, PanelView fallback, bool saveTabs, bool saveView)
        {
            previous = previous ?? new SavedTab[0]; current = current ?? new SavedTab[0];
            if (!saveTabs && !saveView) return previous;
            var source = saveTabs ? current : previous;
            return source.Where(x => x != null).Select((tab, index) => {
                var result = Copy(tab);
                var match = (saveTabs ? previous : current).FirstOrDefault(x => x != null && x.Path == tab.Path && x.ProfileId == tab.ProfileId && x.ArchiveFile == tab.ArchiveFile && x.ArchivePrefix == tab.ArchivePrefix);
                if (saveTabs && !saveView) result.View = match == null ? fallback : match.View;
                if (!saveTabs && saveView) result.View = match != null ? match.View : index == selected ? fallback : result.View;
                return result;
            }).ToArray();
        }
    }
    public partial class MainWindow
    {
        readonly Queue<string> launchFolders = new Queue<string>();
        bool processingLaunch;
        internal void AcceptLaunch(string folder)
        {
            if (windowClosed) return;
            if (IsLoaded) { if (WindowState == WindowState.Minimized) ShowWindow(new WindowInteropHelper(this).Handle, 9); Activate(); }
            if (!String.IsNullOrEmpty(folder) && launchFolders.Count < 32) launchFolders.Enqueue(InstanceCoordinator.ValidateFolder(folder));
            ProcessLaunches();
        }
        async void ProcessLaunches()
        {
            if (processingLaunch || !IsLoaded || launchFolders.Count == 0) return;
            processingLaunch = true;
            try {
                if (StartupRestoration != null) await StartupRestoration;
                while (!windowClosed && launchFolders.Count > 0) {
                    var panel = active;
                    while (!windowClosed && (panel.LocalLoading || panel.IsLoading)) await Task.Delay(50);
                    if (windowClosed) return;
                    panel.NewTab(); panel.Navigate(launchFolders.Dequeue(), true);
                    if (panel.NavigationTask != null) await panel.NavigationTask;
                }
            } catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("instance.openFolderFailed")); }
            finally { processingLaunch = false; }
        }
        internal void SaveExitPreferences(string path = null)
        {
            var stored = ProgramSettings.Load(path);
            if (!stored.SaveFoldersOnExit && !stored.SaveTabsOnExit && !stored.SaveViewOnExit && !stored.SaveWindowOnExit) return;
            var current = new ProgramSettings(); CaptureView(current);
            if (stored.SaveFoldersOnExit) { var folders = ViewFolders(); current.LeftFolder = folders[0]; current.RightFolder = folders[1]; }
            ExitPreferences.Merge(stored, current); stored.Save(path);
        }
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
    }
}

