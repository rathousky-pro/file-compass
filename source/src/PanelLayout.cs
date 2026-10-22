using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CommanderCZ
{
    public partial class MainWindow
    {
        string panelLayout = "SideBySide", lastDualLayout = "SideBySide";
        double sideSplitRatio = .5, stackedSplitRatio = .5;
        bool layoutInitialized;

        void AddPanelLayoutMenu(MenuItem display)
        {
            var layouts = new MenuItem { Header = L.Text("layout.panels") }; display.Items.Add(layouts);
            foreach (string mode in new[] { "SideBySide", "Stacked", "Single" }) {
                string choice = mode;
                var item = new MenuItem { Header = L.Text(mode == "Single" ? "layout.single" : mode == "Stacked" ? "layout.stacked" : "layout.sideBySide"), InputGestureText = mode == "Single" ? "Ctrl+Shift+H" : "", IsCheckable = true };
                item.Click += delegate { SetPanelLayout(choice); };
                layouts.SubmenuOpened += delegate { item.IsChecked = panelLayout == choice; };
                layouts.Items.Add(item);
            }

        }

        void InitializePanelLayout(ProgramSettings settings)
        {
            RestorePanelLayout(settings);
            Closed += delegate { left.DisposeFileDetail(); right.DisposeFileDetail(); };
        }

        void RestorePanelLayout(ProgramSettings settings)
        {
            CloseQuickPreview();
            sideSplitRatio = ValidDimension(settings.SplitRatio, .1, .9, .5);
            stackedSplitRatio = ValidDimension(settings.StackedSplitRatio, .1, .9, .5);
            lastDualLayout = settings.LastDualLayout == "Stacked" ? "Stacked" : "SideBySide";
            left.Visibility = right.Visibility = Visibility.Visible;
            SetActive(settings.SinglePanelRight ? right : left);
            // Restoring a view must retain the saved tree and collapsed-detail choices.
            layoutInitialized = false;
            SetPanelLayout(settings.PanelLayout, false, false);
        }

        void RememberPanelRatio()
        {
            if (!layoutInitialized || panelLayout == "Single") return;
            bool stacked = panelLayout == "Stacked";
            double first = stacked ? panelSplit.RowDefinitions[0].ActualHeight : panelSplit.ColumnDefinitions[0].ActualWidth;
            double second = stacked ? panelSplit.RowDefinitions[2].ActualHeight : panelSplit.ColumnDefinitions[2].ActualWidth;
            if (first + second <= 0) return;
            double ratio = ValidDimension(first / (first + second), .1, .9, .5);
            if (stacked) stackedSplitRatio = ratio; else sideSplitRatio = ratio;
        }

        internal void CapturePanelLayout(ProgramSettings settings)
        {
            RememberPanelRatio(); settings.PanelLayout = panelLayout; settings.SinglePanelRight = active == right;
            settings.LastDualLayout = lastDualLayout;
            settings.SplitRatio = sideSplitRatio; settings.StackedSplitRatio = stackedSplitRatio;
        }

        internal void SetPanelLayout(string mode, bool focus = true, bool explorerDefaults = true)
        {
            if (mode != "Stacked" && mode != "Single") mode = "SideBySide";
            bool preservePreview = layoutInitialized && FilePreviewOpen;
            bool preserveState = layoutInitialized;
            RememberPanelRatio(); CloseQuickPreview();
            bool enteringSingle = mode == "Single" && (!layoutInitialized || panelLayout != "Single");
            panelLayout = mode; if (mode != "Single") lastDualLayout = mode;
            // A tab or saved view may request a tree, but dual-panel layouts never display it.
            left.SetLayoutTreeVisibility(mode == "Single"); right.SetLayoutTreeVisibility(mode == "Single");
            left.SetDetailLayoutVisibility(mode == "Single"); right.SetDetailLayoutVisibility(mode == "Single");
            panelSplit.RowDefinitions.Clear(); panelSplit.ColumnDefinitions.Clear();
            left.Visibility = right.Visibility = Visibility.Visible;
            Grid.SetRow(left, 0); Grid.SetColumn(left, 0); Grid.SetRow(right, 0); Grid.SetColumn(right, 0);
            Grid.SetRow(panelSplitter, 0); Grid.SetColumn(panelSplitter, 0);
            bool stacked = mode == "Stacked";
            panelSplitter.Visibility = mode == "Single" ? Visibility.Collapsed : Visibility.Visible;
            panelSplitter.Width = stacked ? Double.NaN : 6; panelSplitter.Height = stacked ? 6 : Double.NaN;
            panelSplitter.HorizontalAlignment = HorizontalAlignment.Stretch; panelSplitter.VerticalAlignment = VerticalAlignment.Stretch;
            panelSplitter.ResizeDirection = stacked ? GridResizeDirection.Rows : GridResizeDirection.Columns;
            panelSplitter.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
            panelSplitter.Cursor = stacked ? Cursors.SizeNS : Cursors.SizeWE;
            if (mode == "Single") {
                panelSplit.ColumnDefinitions.Add(new ColumnDefinition()); panelSplit.RowDefinitions.Add(new RowDefinition());
                (active == right ? left : right).Visibility = Visibility.Collapsed;
                if (enteringSingle && explorerDefaults) { active.SetTree(true); active.EnsureExplorerDetail(); }
                if (preserveState) active.SetDetailExpanded(preservePreview);
            } else if (stacked) {
                panelSplit.ColumnDefinitions.Add(new ColumnDefinition());
                panelSplit.RowDefinitions.Add(new RowDefinition { Height = new GridLength(stackedSplitRatio, GridUnitType.Star) });
                panelSplit.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
                panelSplit.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - stackedSplitRatio, GridUnitType.Star) });
                Grid.SetRow(panelSplitter, 1); Grid.SetRow(right, 2);
            } else {
                panelSplit.RowDefinitions.Add(new RowDefinition());
                panelSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(sideSplitRatio, GridUnitType.Star) });
                panelSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                panelSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - sideSplitRatio, GridUnitType.Star) });
                Grid.SetColumn(panelSplitter, 1); Grid.SetColumn(right, 2);
            }
            layoutInitialized = true; SetActive(active ?? left);
            if (preservePreview && mode != "Single") OpenQuickPreview();
            UpdateViewToolbarControls();
            if (focus) active.List.Focus();
        }
    }
}

