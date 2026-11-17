using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    internal enum PanelDifference { None, LeftOnly, RightOnly, Different, Same }

    internal static class ComparisonColors
    {
        internal static readonly System.Windows.Media.Brush LeftOnly = CompactTheme.ColorBrush("#2581C4");
        internal static readonly System.Windows.Media.Brush RightOnly = CompactTheme.ColorBrush("#9364C8");
        internal static readonly System.Windows.Media.Brush Different = CompactTheme.ColorBrush("#D58724");
        internal static readonly System.Windows.Media.Brush Same = CompactTheme.ColorBrush("#299661");
    }

    public partial class Entry
    {
        internal PanelDifference Difference { get; private set; }
        public System.Windows.Media.Brush ComparisonBrush
        {
            get
            {
                switch (Difference)
                {
                    case PanelDifference.LeftOnly: return ComparisonColors.LeftOnly;
                    case PanelDifference.RightOnly: return ComparisonColors.RightOnly;
                    case PanelDifference.Different: return ComparisonColors.Different;
                    case PanelDifference.Same: return ComparisonColors.Same;
                    default: return null;
                }
            }
        }
        public string ComparisonLabel { get { return Difference == PanelDifference.None ? "" : L.Text("panelCompare." + Difference); } }

        internal void SetDifference(PanelDifference difference)
        {
            if (Difference == difference) return;
            Difference = difference;
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("ComparisonLabel"));
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("ComparisonBrush"));
            }
        }
    }

    public partial class FilePanel
    {
        ComboBox comparisonFilter;
        StackPanel comparisonActions;
        Entry[] comparedRows;
        GridViewColumn flatLocationColumn;

        internal bool CanComparePanel
        {
            get { return !EmptyPanel && !HasUnavailableTab && !LocalLoading && !IsLoading && !IsVirtualBrowser &&
                !IsArchive && !IsExt && !IsProjection && CurrentPath != null && List.ItemsSource != null; }
        }

        void InitializePanelViewActions(StackPanel host)
        {
            comparisonActions = new StackPanel { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed,
                Margin = new Thickness(9, 2, 9, 2) };
            host.Children.Add(comparisonActions);
            comparisonFilter = new ComboBox { MinWidth = 120, Margin = new Thickness(4, 0, 4, 0) };
            comparisonFilter.Items.Add(new ComboBoxItem { Content = L.Text("panelCompare.showAll"), Tag = PanelDifference.None });
            foreach (var difference in new[] { PanelDifference.LeftOnly, PanelDifference.RightOnly, PanelDifference.Different, PanelDifference.Same })
            {
                var label = new StackPanel { Orientation = Orientation.Horizontal };
                var marker = new Entry();
                marker.SetDifference(difference);
                label.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 6, 0), Background = marker.ComparisonBrush });
                label.Children.Add(new TextBlock { Text = marker.ComparisonLabel });
                comparisonFilter.Items.Add(new ComboBoxItem { Content = label, Tag = difference });
            }
            comparisonFilter.SelectedIndex = 0;
            comparisonFilter.SelectionChanged += delegate { if (comparedRows != null) ApplyQuickFilter(); };
            comparisonActions.Children.Add(comparisonFilter);
            comparisonActions.Children.Add(PanelViewButton("panelCompare.mark", MarkComparisonRows));
            comparisonActions.Children.Add(PanelViewButton("panelCompare.content", delegate
            {
                var owner = Window.GetWindow(this) as MainWindow;
                if (owner != null) owner.CompareMatchingPanelFiles(this);
            }));
            comparisonActions.Children.Add(PanelViewButton("panelCompare.close", delegate
            {
                var owner = Window.GetWindow(this) as MainWindow;
                if (owner != null) owner.ClearPanelComparison();
            }));
        }

        Button PanelViewButton(string label, Action action)
        {
            var button = CompactTheme.Button(L.Text(label), L.Text(label), delegate { action(); });
            button.MinWidth = 0;
            button.Height = 24;
            button.Padding = new Thickness(7, 0, 7, 0);
            button.Margin = new Thickness(0, 0, 4, 0);
            return button;
        }

        internal Entry[] PanelComparisonRows()
        {
            return List.ItemsSource.Cast<Entry>().Where(row => !row.IsParent &&
                (showHiddenFiles || !row.IsHidden) && (showSystemFiles || !row.IsSystem)).ToArray();
        }

        internal void DisplayPanelComparison(Entry[] rows)
        {
            if (DisplayMode != "Details") SetDisplay("Details");
            comparedRows = rows;
            comparisonFilter.SelectedIndex = 0;
            comparisonActions.Visibility = Visibility.Visible;
            SetComparisonColumn(true);
        }

        internal void ClearPanelComparison()
        {
            if (comparedRows == null) return;
            foreach (var row in comparedRows) row.SetDifference(PanelDifference.None);
            comparedRows = null;
            comparisonActions.Visibility = Visibility.Collapsed;
            SetComparisonColumn(false);
            if (List.ItemsSource != null) ApplyQuickFilter();
        }

        bool MatchesComparison(Entry entry)
        {
            var item = comparisonFilter == null ? null : comparisonFilter.SelectedItem as ComboBoxItem;
            var filter = item == null ? PanelDifference.None : (PanelDifference)item.Tag;
            return comparedRows == null || filter == PanelDifference.None || entry.Difference == filter;
        }

        void MarkComparisonRows()
        {
            if (comparedRows == null) return;
            var filter = (PanelDifference)((ComboBoxItem)comparisonFilter.SelectedItem).Tag;
            var results = new HashSet<Entry>(List.Items.Cast<Entry>().Where(row => filter == PanelDifference.None
                ? row.Difference != PanelDifference.Same && row.Difference != PanelDifference.None
                : row.Difference == filter));
            if (results.Count == 0)
            {
                MessageBox.Show(Window.GetWindow(this), L.Text("panelCompare.noMatches"), L.Text("panelCompare.command"));
                return;
            }
            RememberSelection();
            foreach (var row in comparedRows) row.IsMarked = results.Contains(row);
            UpdateStatus();
            FocusFiles();
        }

        internal Entry FindComparedFile(string name)
        {
            if (comparedRows == null || IsRemote) return null;
            var matches = comparedRows.Where(row => !row.IsDirectory &&
                String.Equals(EntryName(row), name, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        GridViewColumn comparisonColumn;
        void SetComparisonColumn(bool enabled)
        {
            if (enabled)
            {
                AddColumn(L.Text("panelCompare.column"), "ComparisonLabel", 112);
                comparisonColumn = view.Columns.Last();
            }
            else if (comparisonColumn != null)
            {
                view.Columns.Remove(comparisonColumn);
                comparisonColumn = null;
                if (sortProperty == "ComparisonLabel") { sortProperty = "Name"; Sort(); }
            }
            FitColumns();
        }
    }

    public partial class MainWindow
    {
        void InitializePanelComparison()
        {
            left.PathChanged += delegate { ClearPanelComparison(); };
            right.PathChanged += delegate { ClearPanelComparison(); };
        }

        internal void ClearPanelComparison()
        {
            left.ClearPanelComparison();
            right.ClearPanelComparison();
        }

        internal void CompareOpenPanels()
        {
            if (panelLayout == "Single" || !left.CanComparePanel || !right.CanComparePanel)
            {
                MessageBox.Show(this, L.Text("panelCompare.requiresFolders"), L.Text("panelCompare.command"));
                return;
            }

            ClearPanelComparison();
            var leftRows = left.PanelComparisonRows();
            var rightRows = right.PanelComparisonRows();
            ComparePanelRows(leftRows, rightRows, left.IsRemote || right.IsRemote);
            left.DisplayPanelComparison(leftRows);
            right.DisplayPanelComparison(rightRows);
        }

        internal void CompareMatchingPanelFiles(FilePanel panel)
        {
            var selected = panel.List.SelectedItem as Entry;
            var other = panel == left ? right : left;
            var match = selected == null || selected.IsParent || selected.IsDirectory || panel.IsRemote
                ? null : other.FindComparedFile(FilePanel.EntryName(selected));
            if (match == null)
            {
                MessageBox.Show(this, L.Text("panelCompare.noPair"), L.Text("panelCompare.command"));
                return;
            }
            var leftFile = panel == left ? selected : match;
            var rightFile = panel == right ? selected : match;
            new FileDiffWindow(this, leftFile.FullPath, rightFile.FullPath).Show();
        }

        internal static void ComparePanelRows(Entry[] leftRows, Entry[] rightRows, bool remote)
        {
            var comparer = remote ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            ComparePanelSide(leftRows, rightRows, comparer, PanelDifference.LeftOnly);
            ComparePanelSide(rightRows, leftRows, comparer, PanelDifference.RightOnly);
        }

        static void ComparePanelSide(Entry[] source, Entry[] target, StringComparer comparer, PanelDifference unique)
        {
            var byName = target.ToLookup(FilePanel.EntryName, comparer);
            foreach (var group in source.GroupBy(FilePanel.EntryName, comparer))
            {
                var candidates = byName[group.Key];
                bool hasMatch = candidates.Any();
                var counterpart = hasMatch && !candidates.Skip(1).Any() && !group.Skip(1).Any() ? candidates.First() : null;
                foreach (var row in group)
                    row.SetDifference(!hasMatch ? unique :
                        counterpart != null && MetadataMatches(row, counterpart) ? PanelDifference.Same : PanelDifference.Different);
            }
        }

        static bool MetadataMatches(Entry leftRow, Entry rightRow)
        {
            if (leftRow.IsDirectory != rightRow.IsDirectory) return false;
            if (leftRow.IsDirectory) return true;
            return leftRow.Bytes == rightRow.Bytes && leftRow.ModifiedTime != DateTime.MinValue &&
                rightRow.ModifiedTime != DateTime.MinValue &&
                Math.Abs((leftRow.ModifiedTime.ToUniversalTime() - rightRow.ModifiedTime.ToUniversalTime()).TotalSeconds) <= 2;
        }
    }
}
