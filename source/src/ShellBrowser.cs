using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CommanderCZ
{
    public partial class FilePanel
    {
        internal bool IsShellBrowser { get { return WindowsShell.IsLocation(CurrentPath); } }
        internal bool IsVirtualBrowser { get { return IsShellBrowser || IsNetworkBrowser; } }
        internal string ShellLocationName { get; private set; }
        async Task NavigateShell(string path, bool remember)
        {
            CancelNavigation(); int version = navigationVersion; LocalLoading = true; List.IsEnabled = false;
            status.Text = L.Text("panel.loadingFolder");
            Exception failure = null;
            try {
                var locations = await BoundedShellRead(WindowsShell.Read(path));
                string name = WindowsShell.Name(path)
                    ?? (path == WindowsShell.DesktopNamespace ? L.Text("tree.desktop") : L.Text("tree.systemLocation"));
                if (version != navigationVersion) return;
                var rows = locations.Select(x => new Entry { Name = x.Name, FullPath = x.Path, IsDirectory = x.Directory,
                    Extension = "", Icon = x.Icon, Bytes = x.Size, IsHidden = x.Hidden, IsSystem = x.System }).ToList();
                if (IsArchive) CloseArchiveContext();
                if (IsRemote) { RememberRemote(); Remote = null; }
                if (remember && CurrentPath != null && CurrentPath != path) { history.Push(CurrentPath); forwardHistory.Clear(); }
                CurrentPath = path; ShellLocationName = name; address.Text = name; capacity.Text = name;
                List.ItemsSource = rows; Sort(); if (rows.Count > 0) List.SelectedIndex = 0;
                selectingDrive = true; driveSelector.SelectedIndex = -1; selectingDrive = false;
                foreach (var button in driveButtons.Values) { button.Background = Brushes.Transparent; button.BorderBrush = CompactTheme.Line; }
                UpdateStatus(); UpdateConnectionButtons(); if (PathChanged != null) PathChanged(this, EventArgs.Empty);
            } catch (Exception ex) { failure = ex; }
            finally {
                if (version == navigationVersion) {
                    if (failure != null) { status.Text = L.Text("tree.locationUnavailable") + " " + failure.Message; status.ToolTip = status.Text; }
                    LocalLoading = false; List.IsEnabled = true;
                }
            }
        }
    }
}
