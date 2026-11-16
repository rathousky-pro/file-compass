using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace FileCompass
{
    public partial class FilePanel
    {
        static bool recycleOperationRunning;
        ContextMenu recycleMenu;

        void ShowRecycleMenu(FrameworkElement target)
        {
            if (!IsRecycleBin || LocalLoading || IsLoading) return;
            activate(this);
            if (recycleMenu == null)
            {
                recycleMenu = new ContextMenu();
                var restore = new MenuItem { Header = L.Text("recycle.restore"), Tag = "recycle.restore" };
                restore.Click += async delegate
                {
                    var ids = RecycleSelection();
                    if (ids.Length > 0) await RunRecycleCommand(owner => WindowsShell.RestoreRecycleItems(ids));
                };
                var empty = new MenuItem { Header = L.Text("recycle.empty"), Tag = "recycle.empty" };
                empty.Click += async delegate
                {
                    await RunRecycleCommand(owner => WindowsShell.EmptyRecycleBin(new WindowInteropHelper(owner).Handle));
                };
                recycleMenu.Items.Add(restore);
                recycleMenu.Items.Add(new Separator());
                recycleMenu.Items.Add(empty);
                recycleMenu.Opened += delegate
                {
                    restore.IsEnabled = !recycleOperationRunning && RecycleSelection().Length > 0;
                    empty.IsEnabled = !recycleOperationRunning && (List.ItemsSource as ICollection)?.Count > 0;
                };
            }
            recycleMenu.PlacementTarget = target;
            recycleMenu.Placement = target == List ? PlacementMode.MousePoint : PlacementMode.Top;
            recycleMenu.IsOpen = true;
        }

        string[] RecycleSelection()
        {
            var marked = List.Items.Cast<Entry>().Where(entry => entry.IsMarked).ToArray();
            var selected = marked.Length > 0 ? marked : List.SelectedItems.Cast<Entry>();
            return selected.Select(entry => entry.RecycleId).Where(id => !String.IsNullOrEmpty(id)).ToArray();
        }

        async Task RunRecycleCommand(Func<Window, Task> run)
        {
            var owner = Window.GetWindow(this) as MainWindow;
            if (owner == null || !IsRecycleBin || LocalLoading || recycleOperationRunning) return;
            recycleOperationRunning = true;
            try { await run(owner); }
            catch (Exception ex)
            {
                if (owner.IsLoaded) MessageBox.Show(owner, ex.GetBaseException().Message, L.Text("locations.recycleBin"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                recycleOperationRunning = false;
                if (owner.IsLoaded) owner.RefreshRecyclePanels();
            }
        }
    }

    public partial class MainWindow
    {
        internal void RefreshRecyclePanels()
        {
            foreach (var panel in new[] { left, right })
                if (panel.IsRecycleBin && !panel.LocalLoading) panel.Refresh();
        }
    }
}
