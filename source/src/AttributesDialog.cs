using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace FileCompass
{
    internal sealed class AttributeChanges
    {
        internal bool? ReadOnly, Hidden, System, Archive;
        internal DateTime? Created, Modified, Accessed;
        internal bool Recursive;
        internal static List<OperationItem> Apply(string[] paths, AttributeChanges changes, Func<bool> cancel)
        {
            var results = new List<OperationItem>();
            var pending = new Stack<string>(paths.Reverse());
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pending.Count > 0) {
                if (cancel()) break;
                string path = pending.Pop(); if (!visited.Add(Path.GetFullPath(path))) continue;
                var result = new OperationItem { Source = path, Target = path, State = L.Code("state.completed") };
                FileAttributes original = 0; bool read = false;
                try {
                    original = File.GetAttributes(path); read = true;
                    if ((original & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("attributes.editLinkInWindows"));
                    result.Directory = (original & FileAttributes.Directory) != 0;
                    if (changes.Recursive && result.Directory)
                        foreach (string child in Directory.GetFileSystemEntries(path).Reverse()) pending.Push(child);
                    var next = original;
                    Change(ref next, FileAttributes.ReadOnly, changes.ReadOnly); Change(ref next, FileAttributes.Hidden, changes.Hidden); Change(ref next, FileAttributes.System, changes.System); Change(ref next, FileAttributes.Archive, changes.Archive);
                    bool times = changes.Created.HasValue || changes.Modified.HasValue || changes.Accessed.HasValue;
                    if (times && (original & FileAttributes.ReadOnly) != 0) File.SetAttributes(path, original & ~FileAttributes.ReadOnly);
                    try {
                        if (changes.Created.HasValue) { if (result.Directory) Directory.SetCreationTime(path, changes.Created.Value); else File.SetCreationTime(path, changes.Created.Value); }
                        if (changes.Modified.HasValue) { if (result.Directory) Directory.SetLastWriteTime(path, changes.Modified.Value); else File.SetLastWriteTime(path, changes.Modified.Value); }
                        if (changes.Accessed.HasValue) { if (result.Directory) Directory.SetLastAccessTime(path, changes.Accessed.Value); else File.SetLastAccessTime(path, changes.Accessed.Value); }
                    } finally { if (times && (original & FileAttributes.ReadOnly) != 0) File.SetAttributes(path, original); }
                    File.SetAttributes(path, next);
                } catch (Exception ex) { result.State = L.Code("state.error"); result.Error = ex.Message; if (read) result.Directory = (original & FileAttributes.Directory) != 0; }
                results.Add(result);
            }
            return results;
        }
        static void Change(ref FileAttributes attributes, FileAttributes flag, bool? value) { if (!value.HasValue) return; if (value.Value) attributes |= flag; else attributes &= ~flag; }
    }
    internal sealed class AttributesDialog : Window
    {
        readonly CheckBox readOnly = Check(L.Text("attributes.readOnlyLabel")), hidden = Check(L.Text("attributes.hiddenLabel")), system = Check(L.Text("attributes.systemLabel")), archive = Check(L.Text("attributes.archiveLabel"));
        readonly CheckBox recursive = Check(L.Text("attributes.includeSubfolders")), changeTime = Check(L.Text("attributes.changeTimestampLabel"));
        readonly TextBox date = Input(), time = Input(), created = Input(), accessed = Input();
        readonly TextBlock status = CopyUi.Label("");
        readonly Button ok;
        int loadVersion;
        static CheckBox Check(string title) { return new CheckBox { Content = title, IsThreeState = true, IsChecked = null, Margin = new Thickness(0, 2, 0, 2), MinHeight = 26 }; }
        static TextBox Input() { return new TextBox { Margin = new Thickness(3), MinHeight = 28 }; }
        internal AttributesDialog(Window owner, string[] paths, Action<string[], AttributeChanges> apply, string sourceCursor = null, string targetCursor = null)
        {
            CopyUi.Style(this, owner, paths.Length == 1 ? L.Text("attributes.changeTitlePrefix") + Path.GetFileName(paths[0]) : L.Text("attributes.changeTitlePrefix") + paths.Length + L.Text("common.itemCountSuffix"), 510);
            var panel = new StackPanel { Margin = new Thickness(14) }; Content = panel;
            recursive.IsThreeState = false; recursive.IsChecked = false; panel.Children.Add(recursive);
            var attributes = new StackPanel(); foreach (var check in new[] { archive, readOnly, hidden, system }) attributes.Children.Add(check);
            attributes.Children.Add(new TextBlock { Text = L.Text("attributes.threeStateHelp"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0), Foreground = FluentTheme.Muted });
            panel.Children.Add(new GroupBox { Header = L.Text("attributes.title"), Content = attributes, Margin = new Thickness(0, 6, 0, 8) });
            changeTime.IsThreeState = false; changeTime.IsChecked = false;
            var timestamps = new StackPanel(); var heading = new DockPanel();
            var now = CopyUi.Button(L.Text("action.current"), delegate { SetDate(DateTime.Now); changeTime.IsChecked = true; }); DockPanel.SetDock(now, Dock.Right); heading.Children.Add(now); heading.Children.Add(changeTime); timestamps.Children.Add(heading);
            var row = new Grid(); foreach (double width in new[] { 52.0, 1.0, 38.0, 1.0 }) row.ColumnDefinitions.Add(new ColumnDefinition { Width = width == 1 ? new GridLength(1, GridUnitType.Star) : new GridLength(width) });
            Add(row, CopyUi.Label(L.Text("attributes.dateLabel")), 0); Add(row, date, 1); Add(row, CopyUi.Label(L.Text("attributes.timeLabel")), 2); Add(row, time, 3); timestamps.Children.Add(row);
            date.ToolTip = L.Text("attributes.modifiedDateHint"); time.ToolTip = L.Text("attributes.modifiedTimeHint");
            changeTime.Checked += delegate { date.IsEnabled = time.IsEnabled = true; }; changeTime.Unchecked += delegate { date.IsEnabled = time.IsEnabled = false; };
            date.IsEnabled = time.IsEnabled = false; SetDate(DateTime.Now);
            panel.Children.Add(new GroupBox { Header = L.Text("attributes.modifiedTimestampLabel"), Content = timestamps, Margin = new Thickness(0, 0, 0, 8) });
            var advanced = new StackPanel(); advanced.Children.Add(CopyUi.Label(L.Text("attributes.keepTimestampHelp")));
            advanced.Children.Add(CopyUi.Label(L.Text("attributes.createdLabel"))); advanced.Children.Add(created); advanced.Children.Add(CopyUi.Label(L.Text("attributes.accessedLabel"))); advanced.Children.Add(accessed);
            panel.Children.Add(new Expander { Header = L.Text("attributes.additionalTimesGroup"), Content = advanced, Margin = new Thickness(0, 0, 0, 8) });
            var load = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            var source = CopyUi.Button(L.Text("attributes.activePanelChoice"), delegate { LoadValues(new[] { sourceCursor }, true); }); source.IsEnabled = sourceCursor != null; load.Children.Add(source);
            var target = CopyUi.Button(L.Text("attributes.targetPanelChoice"), delegate { LoadValues(new[] { targetCursor }, true); }); target.IsEnabled = targetCursor != null; load.Children.Add(target);
            panel.Children.Add(new GroupBox { Header = L.Text("attributes.loadFromCursorFile"), Content = load });
            status.Foreground = FluentTheme.Muted; panel.Children.Add(status);
            var buttons = FluentTheme.Actions(); panel.Children.Add(buttons);
            ok = CopyUi.Button("OK", delegate {
                try { var changes = new AttributeChanges { ReadOnly = readOnly.IsChecked, Hidden = hidden.IsChecked, System = system.IsChecked, Archive = archive.IsChecked, Recursive = recursive.IsChecked == true, Created = Parse(created.Text), Modified = changeTime.IsChecked == true ? Parse(date.Text + " " + time.Text) : null, Accessed = Parse(accessed.Text) }; if (changeTime.IsChecked == true && !changes.Modified.HasValue) throw new FormatException(L.Text("attributes.modifiedTimestampRequired")); apply(paths, changes); DialogResult = true; }
                catch (FormatException ex) { MessageBox.Show(this, ex.Message, L.Text("attributes.invalidTimeTitle")); }
            }); ok.IsDefault = true; buttons.Children.Add(ok); var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            buttons.Children.Add(CopyUi.Button(L.Text("action.help"), delegate { HelpService.Open(this,"attributes"); }));
            Loaded += delegate { LoadValues(paths, false); };
        }
        static void Add(Grid row, UIElement child, int column) { Grid.SetColumn(child, column); row.Children.Add(child); }
        void SetDate(DateTime value) { date.Text = value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture); time.Text = value.ToString("HH:mm:ss", CultureInfo.InvariantCulture); }
        async void LoadValues(string[] paths, bool loadTimes)
        {
            int version = ++loadVersion; SetBusy(true); status.Text = L.Text("attributes.loadingStatus");
            try {
                var snapshot = await Task.Run(delegate { return paths.Select(path => new { Flags = File.GetAttributes(path), Modified = File.GetLastWriteTime(path), Created = File.GetCreationTime(path), Accessed = File.GetLastAccessTime(path) }).ToArray(); });
                if (version != loadVersion) return;
                var checks = new[] { archive, readOnly, hidden, system }; var flags = new[] { FileAttributes.Archive, FileAttributes.ReadOnly, FileAttributes.Hidden, FileAttributes.System };
                for (int i = 0; i < checks.Length; i++) { int index = i; bool first = (snapshot[0].Flags & flags[index]) != 0; checks[i].IsChecked = snapshot.All(x => ((x.Flags & flags[index]) != 0) == first) ? (bool?)first : null; }
                SetDate(snapshot[0].Modified);
                if (loadTimes) { changeTime.IsChecked = true; created.Text = snapshot[0].Created.ToString("dd.MM.yyyy HH:mm:ss"); accessed.Text = snapshot[0].Accessed.ToString("dd.MM.yyyy HH:mm:ss"); }
                status.Text = loadTimes ? L.Text("attributes.valuesLoadedStatus") : L.Text("selection.selectedPrefix") + paths.Length + L.Text("common.itemCountSuffix");
            } catch (Exception ex) { if (version == loadVersion) status.Text = L.Text("attributes.loadFailedPrefix") + ex.Message; }
            finally { if (version == loadVersion) SetBusy(false); }
        }
        void SetBusy(bool busy)
        {
            foreach (Control control in new Control[] { archive, readOnly, hidden, system, recursive, changeTime, created, accessed, ok }) control.IsEnabled = !busy;
            date.IsEnabled = time.IsEnabled = !busy && changeTime.IsChecked == true;
        }
        internal static DateTime? Parse(string text) { if (String.IsNullOrWhiteSpace(text)) return null; DateTime value; if (!DateTime.TryParseExact(text.Trim(), new[] { "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)) throw new FormatException(L.Text("attributes.dateTimeExampleRequired")); return value; }
    }
}

