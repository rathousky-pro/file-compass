using System.Linq;
using System.Windows.Controls;
namespace FileCompass
{
    public partial class FilePanel
    {
        bool windowsSelection, synchronizingSelection;
        internal void ApplySelectionMode(bool enabled)
        {
            if (windowsSelection == enabled) return;
            var marked = List.Items.Cast<Entry>().Where(x => x.IsMarked && !x.IsParent).ToArray();
            var cursor = List.SelectedItem as Entry;
            synchronizingSelection = true;
            try {
                windowsSelection = enabled; List.SelectionMode = enabled ? SelectionMode.Extended : SelectionMode.Single;
                if (enabled) {
                    if (marked.Length == 0 && cursor != null && !cursor.IsParent) cursor.IsMarked = true;
                    else { List.SelectedItems.Clear(); foreach (var entry in marked) List.SelectedItems.Add(entry); }
                }
            } finally { synchronizingSelection = false; }
            SyncWindowsSelection();
        }
        void SyncWindowsMarks()
        {
            if (!windowsSelection || synchronizingSelection || LocalLoading || IsLoading) return;
            var selected = new System.Collections.Generic.HashSet<Entry>(List.SelectedItems.Cast<Entry>());
            foreach (Entry entry in List.Items) entry.IsMarked = !entry.IsParent && selected.Contains(entry);
        }
        void SyncWindowsSelection()
        {
            if (!windowsSelection || synchronizingSelection) return;
            var marked = List.Items.Cast<Entry>().Where(x => x.IsMarked && !x.IsParent).ToArray();
            if (new System.Collections.Generic.HashSet<Entry>(marked).SetEquals(List.SelectedItems.Cast<Entry>().Where(x => !x.IsParent))) return;
            synchronizingSelection = true;
            try { List.SelectedItems.Clear(); foreach (var entry in marked) List.SelectedItems.Add(entry); }
            finally { synchronizingSelection = false; }
        }
    }
}

