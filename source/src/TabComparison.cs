using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FileCompass
{
    internal sealed class TabComparisonLocation
    {
        internal FilePanel Panel;
        internal int Index;
        internal string Path;
        internal RemoteConnection Remote;
        internal bool Active;
        public string Label { get; set; }
    }

    public partial class FilePanel
    {
        internal IEnumerable<TabComparisonLocation> ComparisonLocations(bool leftSide)
        {
            if (EmptyPanel) yield break;
            string side = L.Text(leftSide ? "compare.leftPrefix" : "compare.rightPrefix").Trim().TrimEnd(':');
            for (int index = 0; index < tabs.Count; index++)
            {
                var tab = tabs[index];
                bool current = index == tabIndex;
                string path = current ? CurrentPath : tab.Saved.Locked ? tab.Saved.Home : tab.Saved.Path;
                var remote = current ? Remote : tab.Remote;
                if (String.IsNullOrEmpty(path) || tab.Error != null || (current && (IsArchive || IsVirtualBrowser)) ||
                    (!current && !String.IsNullOrEmpty(tab.Saved.ArchiveFile)) || ExtLocations.IsPath(path) ||
                    WindowsShell.IsLocation(path) || NetworkBrowser.IsLocation(path)) continue;
                if (remote == null && ((!current && !String.IsNullOrEmpty(tab.Saved.ProfileId)) || !Path.IsPathRooted(path))) continue;
                if (remote != null && remote.Profile.Protocol == NetworkProtocol.SCP) continue;

                string caption = remote == null ? path : remote.Display(path);
                yield return new TabComparisonLocation
                {
                    Panel = this, Index = index, Path = path, Remote = remote, Active = current,
                    Label = side + " " + (index + 1) + " · " + caption
                };
            }
        }
    }

    internal sealed partial class SyncRow
    {
        internal PanelDifference TabDifference { get; set; }
    }

    [Flags]
    internal enum TabComparisonCriteria { Missing = 1, Size = 2, Date = 4, Content = 8 }

    internal static class TabComparison
    {
        internal static List<SyncRow> Run(SyncLocation left, SyncLocation right, TabComparisonCriteria criteria,
            bool recursive, bool showMatches, CopyControl control)
        {
            var comparer = left.Template == null && right.Template == null ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            using (var first = left.Open(control.Token))
            using (var second = right.Open(control.Token))
            {
                var a = first.Scan(comparer, control.Check, recursive);
                var b = second.Scan(comparer, control.Check, recursive);
                var rows = new List<SyncRow>();
                foreach (string name in a.Keys.Union(b.Keys, comparer).OrderBy(key => key, comparer))
                {
                    control.Check();
                    a.TryGetValue(name, out var source);
                    b.TryGetValue(name, out var target);
                    var reasons = new List<string>(3);
                    if (source == null || target == null)
                    {
                        if (!criteria.HasFlag(TabComparisonCriteria.Missing)) continue;
                        reasons.Add(L.Text(source == null ? "panelCompare.RightOnly" : "panelCompare.LeftOnly"));
                    }
                    else if (source.Directory != target.Directory) reasons.Add(L.Text("compare.fileFolderConflict"));
                    else if (!source.Directory)
                    {
                        if (criteria.HasFlag(TabComparisonCriteria.Size) && source.Length != target.Length)
                            reasons.Add(L.Text("tabCompare.differentSize"));
                        if (criteria.HasFlag(TabComparisonCriteria.Date) && source.Modified != target.Modified)
                            reasons.Add(L.Text(source.Modified > target.Modified ? "tabCompare.firstNewer" : "tabCompare.secondNewer"));
                        if (criteria.HasFlag(TabComparisonCriteria.Content))
                        {
                            bool equal = source.Length == target.Length &&
                                first.ContentHash(name, source, control.Check) == second.ContentHash(name, target, control.Check);
                            first.Validate(name, source);
                            second.Validate(name, target);
                            if (!equal) reasons.Add(L.Text("tabCompare.differentContent"));
                        }
                    }
                    if (reasons.Count == 0 && !showMatches) continue;
                    rows.Add(new SyncRow
                    {
                        Relative = name, Source = source, Target = target,
                        TabDifference = source == null ? PanelDifference.RightOnly : target == null ? PanelDifference.LeftOnly :
                            reasons.Count == 0 ? PanelDifference.Same : PanelDifference.Different,
                        SourcePath = source == null ? null : first.PathFor(name), TargetPath = target == null ? null : second.PathFor(name),
                        Difference = reasons.Count == 0 ? L.Text("tabCompare.match") : String.Join(" · ", reasons)
                    });
                }
                return rows;
            }
        }
    }

    internal sealed class TabComparisonWindow : Window
    {
        readonly ComboBox first = new ComboBox();
        readonly ComboBox second = new ComboBox();
        readonly StackPanel options = new StackPanel { Margin = new Thickness(12, 8, 12, 8) };
        readonly CheckBox missing = Check("tabCompare.missing", true);
        readonly CheckBox size = Check("tabCompare.size", true);
        readonly CheckBox date = Check("tabCompare.date", true);
        readonly CheckBox content = Check("compare.compareFileContents", false);
        readonly CheckBox recursive = Check("projection.includeChildren", false);
        readonly CheckBox matches = Check("compare.showEqualItems", false);
        readonly ListView list = new ListView { Margin = new Thickness(12, 0, 12, 6) };
        readonly TextBlock status = new TextBlock { Margin = new Thickness(12, 2, 12, 8), TextWrapping = TextWrapping.Wrap };
        readonly Button compare, cancel, showInPanels;
        internal TabComparisonLocation FirstLocation { get { return first.SelectedItem as TabComparisonLocation; } }
        internal TabComparisonLocation SecondLocation { get { return second.SelectedItem as TabComparisonLocation; } }
        internal List<SyncRow> Rows { get; private set; }
        CopyControl control;
        bool closed;

        internal TabComparisonWindow(MainWindow owner, TabComparisonLocation[] locations, TabComparisonLocation preferredFirst, TabComparisonLocation preferredSecond)
        {
            string title = L.Text("tabCompare.title");
            CopyUi.Style(this, owner, title, 1040);
            StackPanel actions;
            var body = SettingsUx.Frame(this, title, out actions);
            DialogGeometry.Bound(this, 1040, 680, 650, 470);
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            compare = CopyUi.Button(L.Text("action.compare"), async delegate { await RunComparison(); });
            compare.IsDefault = true;
            FluentTheme.Primary(compare);
            actions.Children.Add(compare);
            showInPanels = CopyUi.Button(L.Text("tabCompare.showInPanels"), delegate { DialogResult = true; });
            showInPanels.IsEnabled = false;
            actions.Children.Add(showInPanels);
            cancel = CopyUi.Button(L.Text("action.cancelCurrentOperation"), delegate { control?.Cancel(); });
            cancel.IsEnabled = false;
            actions.Children.Add(cancel);
            actions.Children.Add(CopyUi.Button(L.Text("action.close"), Close));

            DockPanel.SetDock(options, Dock.Top);
            body.Children.Add(options);
            AddTabChoice(first, "tabCompare.first", locations);
            AddTabChoice(second, "tabCompare.second", locations);
            first.SelectedItem = preferredFirst ?? locations[0];
            second.SelectedItem = preferredSecond != null && preferredSecond != first.SelectedItem
                ? preferredSecond : locations.First(location => location != first.SelectedItem);
            var criteria = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
            foreach (var option in new[] { missing, size, date, content, recursive, matches })
            {
                criteria.Children.Add(option);
                option.Click += delegate { InvalidateComparison(); };
            }
            options.Children.Add(criteria);
            DockPanel.SetDock(status, Dock.Bottom);
            body.Children.Add(status);
            var columns = new GridView();
            columns.Columns.Add(new GridViewColumn { Header = L.Text("column.item"), DisplayMemberBinding = new Binding("Relative"), Width = 240 });
            columns.Columns.Add(new GridViewColumn { Header = L.Text("tabCompare.first"), DisplayMemberBinding = new Binding("SourceInfo"), Width = 205 });
            columns.Columns.Add(new GridViewColumn { Header = L.Text("tabCompare.second"), DisplayMemberBinding = new Binding("TargetInfo"), Width = 205 });
            columns.Columns.Add(new GridViewColumn { Header = L.Text("column.difference"), DisplayMemberBinding = new Binding("Difference"), Width = 290 });
            VirtualizingPanel.SetIsVirtualizing(list, true);
            VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
            list.View = columns;
            list.MouseDoubleClick += delegate
            {
                var row = list.SelectedItem as SyncRow;
                if (row == null || row.Source == null || row.Target == null || row.Source.Directory || row.Target.Directory ||
                    ((TabComparisonLocation)first.SelectedItem).Remote != null || ((TabComparisonLocation)second.SelectedItem).Remote != null) return;
                new FileDiffWindow(this, row.SourcePath, row.TargetPath).Show();
            };
            body.Children.Add(list);
            first.SelectionChanged += delegate { InvalidateComparison(); };
            second.SelectionChanged += delegate { InvalidateComparison(); };
            Closed += delegate { closed = true; control?.Cancel(); };
            InvalidateComparison();
        }

        static CheckBox Check(string key, bool value)
        {
            return new CheckBox { Content = L.Text(key), IsChecked = value, Margin = new Thickness(0, 3, 17, 3) };
        }

        void AddTabChoice(ComboBox combo, string key, TabComparisonLocation[] locations)
        {
            options.Children.Add(new TextBlock { Text = L.Text(key), Margin = new Thickness(0, 2, 0, 3) });
            System.Windows.Automation.AutomationProperties.SetName(combo, L.Text(key));
            combo.ItemsSource = locations;
            combo.DisplayMemberPath = "Label";
            combo.SetBinding(ToolTipProperty, new Binding("SelectedItem.Label") { Source = combo });
            combo.Margin = new Thickness(0, 0, 0, 6);
            options.Children.Add(combo);
        }

        void InvalidateComparison()
        {
            Rows = null;
            showInPanels.IsEnabled = false;
            list.ItemsSource = null;
            bool different = first.SelectedItem != null && second.SelectedItem != null && first.SelectedItem != second.SelectedItem;
            compare.IsEnabled = different && control == null;
            status.Text = L.Text(different ? "tabCompare.help" : "tabCompare.chooseDifferent");
        }

        internal async Task RunComparison()
        {
            if (!compare.IsEnabled || control != null) return;
            var a = (TabComparisonLocation)first.SelectedItem;
            var b = (TabComparisonLocation)second.SelectedItem;
            TabComparisonCriteria flags = 0;
            if (missing.IsChecked == true) flags |= TabComparisonCriteria.Missing;
            if (size.IsChecked == true) flags |= TabComparisonCriteria.Size;
            if (date.IsChecked == true) flags |= TabComparisonCriteria.Date;
            if (content.IsChecked == true) flags |= TabComparisonCriteria.Content;
            bool includeChildren = recursive.IsChecked == true, showMatches = matches.IsChecked == true;
            var operation = new CopyControl();
            control = operation;
            compare.IsEnabled = options.IsEnabled = false;
            cancel.IsEnabled = true;
            Rows = null;
            showInPanels.IsEnabled = false;
            list.ItemsSource = null;
            status.Text = L.Text("compare.runningInBackground");
            try
            {
                var rows = await Task.Run(delegate
                {
                    using (var left = new SyncLocation(a.Path, a.Remote))
                    using (var right = new SyncLocation(b.Path, b.Remote))
                        return TabComparison.Run(left, right, flags, includeChildren, true, operation);
                });
                if (closed) return;
                Rows = rows;
                var visible = showMatches ? rows : rows.Where(row => row.TabDifference != PanelDifference.Same).ToList();
                list.ItemsSource = visible;
                status.Text = L.Text("compare.itemCountPrefix") + visible.Count;
                showInPanels.IsEnabled = true;
            }
            catch (OperationCanceledException) { if (!closed) status.Text = L.Text("compare.cancelledStatus"); }
            catch (Exception ex) { if (!closed) status.Text = L.Text("compare.failedPrefix") + ex.Message; }
            finally
            {
                control = null;
                operation.Dispose();
                if (!closed)
                {
                    compare.IsEnabled = options.IsEnabled = true;
                    cancel.IsEnabled = false;
                }
            }
        }
    }

    public partial class MainWindow
    {
        static void AddToolbarMenuItem(ContextMenu menu, string key, Action action)
        {
            var item = new MenuItem { Header = L.Text(key) };
            item.Click += delegate { action(); };
            menu.Items.Add(item);
        }

        async void ShowTabComparison()
        {
            var first = left.ComparisonLocations(true).ToArray();
            var second = right.ComparisonLocations(false).ToArray();
            var choices = first.Concat(second).ToArray();
            if (choices.Length < 2)
            {
                MessageBox.Show(this, L.Text("tabCompare.chooseDifferent"), L.Text("tabCompare.title"));
                return;
            }
            var dialog = new TabComparisonWindow(this, choices, first.FirstOrDefault(item => item.Active), second.FirstOrDefault(item => item.Active));
            if (dialog.ShowDialog() == true && dialog.Rows != null)
                await ShowTabComparisonInPanels(dialog.FirstLocation, dialog.SecondLocation, dialog.Rows);
        }

        internal async Task ShowTabComparisonInPanels(TabComparisonLocation first, TabComparisonLocation second, List<SyncRow> rows)
        {
            if (panelLayout == "Single") SetPanelLayout("SideBySide");
            if (FilePreviewOpen) CloseQuickPreview();
            ClearPanelComparison();

            var secondPanel = first.Panel == second.Panel ? first.Panel == left ? right : left : second.Panel;
            await OpenComparisonLocation(first.Panel, first);
            await OpenComparisonLocation(secondPanel, second);
            if (!PanelShowsLocation(first.Panel, first) || !PanelShowsLocation(secondPanel, second))
            {
                MessageBox.Show(this, L.Text("panelCompare.requiresFolders"), L.Text("tabCompare.title"));
                return;
            }

            bool caseSensitive = first.Remote != null || second.Remote != null;
            var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            var top = new Dictionary<string, PanelDifference>(comparer);
            foreach (var row in rows)
            {
                int slash = row.Relative.IndexOf('/');
                if (slash < 0) top[row.Relative] = row.TabDifference;
            }
            foreach (var row in rows)
            {
                int slash = row.Relative.IndexOf('/');
                if (slash < 0 || row.TabDifference == PanelDifference.Same) continue;
                string ancestor = row.Relative.Substring(0, slash);
                if (top.TryGetValue(ancestor, out var parent) && parent == PanelDifference.Same)
                    top[ancestor] = PanelDifference.Different;
            }
            ApplyTabDifferences(first.Panel, top, first.Panel != left);
            ApplyTabDifferences(secondPanel, top, first.Panel != left);
        }

        static async Task OpenComparisonLocation(FilePanel panel, TabComparisonLocation location)
        {
            if (panel != location.Panel)
            {
                if (PanelShowsLocation(panel, location)) return;
                await panel.OpenLocationTab(new SavedTab { Path = location.Path,
                    ProfileId = location.Remote == null ? "" : location.Remote.Profile.Id });
            }
            else if (panel.CurrentTabIndex != location.Index)
            {
                panel.SwitchTab(location.Index);
                if (panel.TabNavigationTask != null) await panel.TabNavigationTask;
            }
            if (panel.IsProjection && !panel.IsLoading && !panel.LocalLoading)
            {
                await panel.OpenLocationTab(new SavedTab { Path = location.Path });
            }
        }

        static bool PanelShowsLocation(FilePanel panel, TabComparisonLocation location)
        {
            return panel.CanComparePanel && String.Equals(panel.CurrentPath, location.Path,
                location.Remote == null ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                (location.Remote == null ? !panel.IsRemote : panel.IsRemote && panel.Remote.Profile.Id == location.Remote.Profile.Id);
        }

        static void ApplyTabDifferences(FilePanel panel, Dictionary<string, PanelDifference> results, bool reversed)
        {
            var entries = panel.PanelComparisonRows();
            foreach (var entry in entries)
            {
                if (!results.TryGetValue(FilePanel.EntryName(entry), out var difference)) continue;
                if (reversed && difference == PanelDifference.LeftOnly) difference = PanelDifference.RightOnly;
                else if (reversed && difference == PanelDifference.RightOnly) difference = PanelDifference.LeftOnly;
                entry.SetDifference(difference);
            }
            panel.DisplayPanelComparison(entries);
        }
    }
}
