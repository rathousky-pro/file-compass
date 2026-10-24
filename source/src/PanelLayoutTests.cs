using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FileCompass
{
    internal static class PanelLayoutTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow main = null;
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                main = new MainWindow(root, new ProgramSettings { AutoRefresh = false }) { Width = 1240, Height = 860, ShowActivated = false, ShowInTaskbar = false, Left = -18000, Top = -18000, WindowStartupLocation = WindowStartupLocation.Manual };
                main.Show(); NavigationTests.WaitPanels(main);
                NavigationTests.Wait(main.CheckPanelLayouts(root, delegate(bool valid, string message) { if (!valid) throw new Exception(message); checks.Add(message); }));
                File.WriteAllLines(Path.Combine(root, "panel-layout-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "panel-layout-result.txt"), "FAIL after " + checks.Count + " checks\n" + String.Join("\n", checks) + "\n" + ex); return 1; }
            finally { if (main != null) main.Close(); }
        }
    }

    public partial class MainWindow
    {
        internal async Task CheckPanelLayouts(string root, Action<bool, string> check)
        {
            string source = Directory.CreateDirectory(Path.Combine(root, "Fotografie a kód")).FullName;
            string nested = Directory.CreateDirectory(Path.Combine(source, "Podsložka")).FullName;
            string destination = Directory.CreateDirectory(Path.Combine(root, "Cíl kopírování")).FullName;
            string code = Path.Combine(source, "Ukázka.cs"), picture = Path.Combine(source, "Obrázek.png");
            File.WriteAllText(code, "// Příliš žluťoučký kůň\nclass Ukazka { public string Nazev = \"File Compass\"; }\n");
            var pixels = new byte[320 * 200 * 3]; for (int i = 0; i < pixels.Length; i += 3) { pixels[i] = 35; pixels[i + 1] = 130; pixels[i + 2] = 205; }
            var bitmap = BitmapSource.Create(320, 200, 96, 96, PixelFormats.Rgb24, null, pixels, 320 * 3);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var output = File.Create(picture)) encoder.Save(output);
            left.Navigate(source, false); right.Navigate(destination, false); await Task.WhenAll(left.NavigationTask, right.NavigationTask);
            SetActive(left); left.List.SelectedItem = left.List.Items.Cast<Entry>().Single(x => x.FullPath == code); ((Entry)left.List.SelectedItem).IsMarked = true;
            left.NewTab(); left.Navigate(nested, false); await left.NavigationTask; int tabs = left.TabCount;
            left.SwitchTab(0); await left.NavigationTask;
            var selected = left.List.Items.Cast<Entry>().Single(x => x.FullPath == code); left.List.SelectedItem = selected; selected.IsMarked = true;
            SetPanelLayout("Stacked"); UpdateLayout();
            var leftPosition = left.TranslatePoint(new Point(), panelSplit); var rightPosition = right.TranslatePoint(new Point(), panelSplit);
            check(rightPosition.Y > leftPosition.Y + left.ActualHeight && Math.Abs(rightPosition.X - leftPosition.X) < 1, "stacked panels occupy full-width rows with a horizontal divider");
            check(left.TabCount == tabs && left.CurrentPath == source && right.CurrentPath == destination && selected.IsMarked, "layout changes preserve folders, tabs and file marks");
            NextStageTests.Draw(this, Path.Combine(root, "panely-pod-sebou.png"));
            SetPanelLayout("Single"); if (!FilePreviewOpen) ToggleQuickPreview(); UpdateLayout(); if (left.TreeLoadingTask != null) await left.TreeLoadingTask; if (left.DetailLoadingTask != null) await left.DetailLoadingTask;
            check(left.IsVisible && !right.IsVisible && right.CurrentPath == destination, "single panel hides and retains the destination panel");
            var savedPanel = new PanelView(); left.SavePanelView(savedPanel);
            check(savedPanel.Tree && savedPanel.DetailExpanded && left.DetailExpanded, "single-panel explorer defaults to tree, folder contents and expanded detail");
            check(left.List.Visibility == Visibility.Visible && left.List.ActualHeight > 80, "bottom preview leaves the folder list visible and usable");
            check(FluentUiTests.Children<ICSharpCode.AvalonEdit.TextEditor>(left).Any(x => x.IsVisible && x.Text.Contains("class Ukazka") && x.SyntaxHighlighting != null), "selected source code appears in a bottom preview with syntax highlighting");
            check(FluentUiTests.Children<TreeView>(left).Single().SelectedItem is TreeViewItem && String.Equals((string)((TreeViewItem)FluentUiTests.Children<TreeView>(left).Single().SelectedItem).Tag, source, StringComparison.OrdinalIgnoreCase), "tree reveals and selects the current nested folder");
            NextStageTests.Draw(this, Path.Combine(root, "jeden-panel-kod.png"));
            left.List.SelectedItem = left.List.Items.Cast<Entry>().Single(x => x.FullPath == picture); left.List.SelectedItem = selected;
            if (left.DetailLoadingTask != null) await left.DetailLoadingTask;
            check(FluentUiTests.Children<ICSharpCode.AvalonEdit.TextEditor>(left).Any(x => x.IsVisible && x.Text.Contains("class Ukazka")), "rapid cursor changes keep the final file preview instead of a stale image");
            SetActive(right); check(active == left, "a hidden panel cannot become the source of keyboard file operations");
            double before = left.List.ActualHeight;
            left.SetDetailExpanded(false); UpdateLayout(); check(!left.DetailExpanded && left.List.ActualHeight > before + 30, "collapsing details returns their space to the folder list");
            using (File.Open(code, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { check(true, "collapsing the preview leaves the source file available for exclusive access"); }
            NextStageTests.Draw(this, Path.Combine(root, "jeden-panel-sbaleny-detail.png"));
            left.List.SelectedItem = left.List.Items.Cast<Entry>().Single(x => x.FullPath == picture);
            left.SetDetailExpanded(true); if (left.DetailLoadingTask != null) await left.DetailLoadingTask; UpdateLayout();
            check(FluentUiTests.Children<Image>(left).Any(x => x.IsVisible && x.Source is BitmapSource && ((BitmapSource)x.Source).PixelWidth >= 320), "expanded detail shows the selected image");
            NextStageTests.Draw(this, Path.Combine(root, "jeden-panel-obrazek.png"));
            var grip = FluentUiTests.Children<Thumb>(left).Single(x => x.Cursor == System.Windows.Input.Cursors.SizeNS);
            var detailBefore = new PanelView(); left.SavePanelView(detailBefore);
            grip.RaiseEvent(new DragDeltaEventArgs(0, -45) { RoutedEvent = Thumb.DragDeltaEvent }); UpdateLayout();
            var detailAfter = new PanelView(); left.SavePanelView(detailAfter); check(detailAfter.DetailRatio > detailBefore.DetailRatio, "dragging the bottom divider resizes the detail area");
            left.Navigate(nested, false); await left.NavigationTask; if (left.TreeLoadingTask != null) await left.TreeLoadingTask;
            check(String.Equals((string)((TreeViewItem)FluentUiTests.Children<TreeView>(left).Single().SelectedItem).Tag, nested, StringComparison.OrdinalIgnoreCase), "tree selection follows folder navigation without changing the current tab");
            left.Navigate(source, false); await left.NavigationTask;
            left.List.SelectedItem = left.List.Items.Cast<Entry>().Single(x => x.FullPath == code);
            left.SetDetailExpanded(false); ToggleQuickPreview(); check(left.DetailExpanded && quickPreview == null, "Ctrl+Q toggles the bottom preview in single-panel mode"); ToggleQuickPreview();
            var state = new ProgramSettings(); CaptureView(state); state.LeftFolder = source; state.RightFolder = destination;
            string settingsPath = Path.Combine(root, "layout-settings.xml"); state.Save(settingsPath); var reloaded = ProgramSettings.Load(settingsPath);
            check(reloaded.PanelLayout == "Single" && !reloaded.LeftView.DetailExpanded && reloaded.LeftView.DetailConfigured && reloaded.LeftView.Tree, "settings round trip preserves layout and a deliberately collapsed detail");
            check(reloaded.LastDualLayout == "Stacked", "single-panel settings preserve the preceding dual-panel layout");
            SetPanelLayout("SideBySide"); UpdateLayout(); check(left.IsVisible && right.IsVisible && right.CurrentPath == destination, "returning to two panels restores the original destination");
            check(!FluentUiTests.Children<TreeView>(left).Single().IsVisible && !FluentUiTests.Children<TreeView>(right).Single().IsVisible && left.List.TranslatePoint(new Point(), left).X < 10, "returning from one panel to side-by-side hides both trees and releases their width");
            left.SwitchTab(1); await left.TabNavigationTask; left.SwitchTab(0); await left.TabNavigationTask; UpdateLayout();
            check(!FluentUiTests.Children<TreeView>(left).Single().IsVisible, "restoring a tab with a saved tree cannot show it in a dual-panel layout");
            left.RestorePanelView(reloaded.LeftView); UpdateLayout();
            check(!FluentUiTests.Children<TreeView>(left).Single().IsVisible && left.List.TranslatePoint(new Point(), left).X < 10, "legacy saved views with Tree=true stay full-width in dual-panel mode");
            NextStageTests.Draw(this, Path.Combine(root, "panely-vedle-sebe-bez-stromu.png"));
            SetPanelLayout("Stacked"); SwapPanels(); UpdateLayout();
            check(!FluentUiTests.Children<TreeView>(left).Single().IsVisible && !FluentUiTests.Children<TreeView>(right).Single().IsVisible, "stacked layout also keeps both trees hidden after swapping panels");
            check(Grid.GetRow(left) == 0 && Grid.GetRow(right) == 2 && left.CurrentPath == destination && right.CurrentPath == source, "swapping panels respects the stacked layout");
            SwapPanels(); SetPanelLayout("SideBySide"); SetActive(left);
            SetActive(right); SetPanelLayout("Single"); UpdateLayout(); check(right.IsVisible && !left.IsVisible, "single-panel mode retains the previously active right panel");
            var rightState = new ProgramSettings(); CaptureView(rightState); check(rightState.SinglePanelRight, "saved layout records which panel is visible");
            SwapPanels(); UpdateLayout(); check(active.IsVisible && left.IsVisible && !right.IsVisible, "swapping a single panel keeps the active folder visible"); SwapPanels();
            RestorePanelLayout(reloaded); left.RestorePanelView(reloaded.LeftView); right.RestorePanelView(reloaded.RightView); UpdateLayout();
            check(active == left && left.IsVisible && !right.IsVisible && !left.DetailExpanded, "restoring a saved single-panel view preserves its collapsed detail");
            check(FluentUiTests.Children<TreeView>(left).Single().IsVisible, "single-panel restore still displays its saved tree");
            for (int mask = 0; mask < 2; mask++) {
                var stored = new ProgramSettings { SaveViewOnExit = mask != 0, PanelLayout = "SideBySide" };
                ExitPreferences.Merge(stored, reloaded);
                check(stored.PanelLayout == (mask != 0 ? "Single" : "SideBySide"), "layout respects save-view-on-exit setting " + mask);
            }
            var workspace = CaptureWorkspace("Explorer"); SetPanelLayout("Stacked"); await ApplyWorkspace(workspace); UpdateLayout();
            check(panelLayout == "Single" && active == left && !right.IsVisible && !left.DetailExpanded, "workspaces restore single-panel layout and collapsed details");
            SetPanelLayout("Stacked"); UpdateLayout(); panelSplit.RowDefinitions[0].Height = new GridLength(.65, GridUnitType.Star); panelSplit.RowDefinitions[2].Height = new GridLength(.35, GridUnitType.Star); UpdateLayout();
            SetPanelLayout("SideBySide"); UpdateLayout(); SetPanelLayout("Stacked"); UpdateLayout();
            var ratioState = new ProgramSettings(); CaptureView(ratioState); check(Math.Abs(ratioState.StackedSplitRatio - .65) < .04, "stacked split ratio survives switching to another layout and back");
            SetPanelLayout("SideBySide"); SetActive(left); left.SetDetailExpanded(false); right.SetDetailExpanded(false);
            left.List.SelectedItem = left.List.Items.Cast<Entry>().FirstOrDefault(x => !x.IsDirectory);
            ToggleQuickPreview(); if (quickPreview.LoadingTask != null) await quickPreview.LoadingTask;
            check(right.List.Visibility == Visibility.Collapsed && quickPreview != null, "two-panel Ctrl+Q retains the existing opposite-panel preview");
            SetPanelLayout("Single"); check(quickPreview == null && right.List.Visibility == Visibility.Visible && left.DetailExpanded, "switching to one panel moves the open preview below the source list");
            Width = 900; Height = 650; UpdateLayout(); left.SetDetailExpanded(true); if (left.DetailLoadingTask != null) await left.DetailLoadingTask;
            check(left.List.ActualWidth > 250 && left.List.ActualHeight > 40, "single-panel layout remains usable in a compact window");
            NextStageTests.Draw(this, Path.Combine(root, "jeden-panel-900.png"));
            var previewSize = new PanelView(); left.SavePanelView(previewSize);
            Width = 1440; Height = 900; SetPanelLayout("SideBySide"); SetActive(left);
            left.List.SelectedItem = left.List.Items.Cast<Entry>().Single(x => x.FullPath == code);
            if (!FilePreviewOpen) ToggleQuickPreview();
            if (quickPreview.LoadingTask != null) await quickPreview.LoadingTask;
            UpdateLayout();
            check(quickPreview.Parent == panelSplit && !right.IsVisible && left.List.IsVisible, "dual preview replaces the whole opposite panel while preserving the source list");
            check(!FluentUiTests.Children<QuickPreviewPane>(left).Any(x => x.IsVisible), "dual layouts hide the old bottom preview");
            check(FluentUiTests.Children<QuickPreviewPane>(this).Count(x => x.IsVisible) == 1, "only one preview is visible in side-by-side mode");
            NextStageTests.Draw(this, Path.Combine(root, "nahled-vedle-sebe.png"));
            Action<Button> click = button => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Func<Button, string> name = button => System.Windows.Automation.AutomationProperties.GetName(button);
            var tools = (Panel)toolbarLayoutButton.Parent;
            var buttons = tools.Children.OfType<Button>().ToArray();
            int hiddenIndex = Array.IndexOf(buttons, toolbarAllFilesButton);
            check(name(buttons[hiddenIndex + 1]) == L.Text("selection.invertMarks") + " (Num *)" && name(buttons[hiddenIndex + 2]) == "Náhled" && name(buttons[hiddenIndex + 3]) == "Editovat" && buttons[hiddenIndex + 4] == toolbarThumbnailsButton, "toolbar order is hidden files, invert, preview, edit, thumbnails");
            check(toolbarLayoutButton.ContextMenu == null && !name(toolbarLayoutButton).Contains("▾"), "layout button has no dropdown");
            var samePreview = quickPreview;
            click(toolbarThumbnailsButton); check(left.DisplayMode == "Thumbnails" && quickPreview == samePreview, "thumbnails change the file list without toggling file preview");
            click(toolbarThumbnailsButton); check(left.DisplayMode == "Details", "thumbnails toggle back to the details list");
            ApplyDisplaySettings(new ProgramSettings { ShowHiddenFiles = false, ShowSystemFiles = false });
            check(name(toolbarAllFilesButton) == "Skryté soubory" && System.Windows.Automation.AutomationProperties.GetItemStatus(toolbarAllFilesButton) == L.Text("toolbar.disabled"), "hidden-files toolbar label shows the disabled state");
            ApplyDisplaySettings(new ProgramSettings { ShowHiddenFiles = true, ShowSystemFiles = true });
            check(name(toolbarAllFilesButton) == "Skryté soubory" && System.Windows.Automation.AutomationProperties.GetItemStatus(toolbarAllFilesButton) == L.Text("toolbar.enabled"), "hidden-files toolbar label shows the enabled state");
            click(toolbarLayoutButton); UpdateLayout(); if (quickPreview.LoadingTask != null) await quickPreview.LoadingTask;
            check(panelLayout == "Stacked" && name(toolbarLayoutButton) == L.Text("toolbar.panelsStacked"), "layout click switches to stacked and updates its name");
            check(Grid.GetRow(quickPreview) == 2 && Grid.GetColumn(quickPreview) == 0 && !right.IsVisible, "stacked preview occupies the opposite full-width row");
            check(FluentUiTests.Children<QuickPreviewPane>(this).Count(x => x.IsVisible) == 1, "only one preview is visible in stacked mode");
            NextStageTests.Draw(this, Path.Combine(root, "nahled-pod-sebou.png"));
            click(toolbarPreviewButton); check(!FilePreviewOpen && right.Visibility == Visibility.Visible, "toolbar closes the preview opened through Ctrl+Q");
            ToggleQuickPreview(); check(FilePreviewOpen, "Ctrl+Q reopens the same preview controlled by the toolbar");
            click(toolbarLayoutButton); UpdateLayout(); if (left.DetailLoadingTask != null) await left.DetailLoadingTask;
            check(panelLayout == "Single" && quickPreview == null && left.DetailExpanded && name(toolbarLayoutButton) == L.Text("toolbar.panelsSingle"), "layout click moves preview below the single list and updates its name");
            check(FluentUiTests.Children<QuickPreviewPane>(this).Count(x => x.IsVisible) == 1, "only one preview is visible in single-panel mode");
            var restoredPreviewSize = new PanelView(); left.SavePanelView(restoredPreviewSize);
            check(Math.Abs(restoredPreviewSize.DetailRatio - previewSize.DetailRatio) < .04, "moving preview between layouts preserves its single-panel height");
            check(FluentUiTests.Children<ICSharpCode.AvalonEdit.TextEditor>(left).Any(x => x.IsVisible && x.ActualHeight > 30 && x.Text.Contains("class Ukazka")), "single-panel preview retains a visible content area after layout cycling");
            NextStageTests.Draw(this, Path.Combine(root, "nahled-jeden-panel.png"));
            click(toolbarPreviewButton); check(!left.DetailExpanded && !FilePreviewOpen, "toolbar closes the bottom preview");
            ToggleQuickPreview(); check(left.DetailExpanded && quickPreview == null, "Ctrl+Q opens the bottom preview without a second viewer");
            OpenRichPreview(); check(!FilePreviewOpen, "legacy formatted-preview shortcut also controls the unified preview");
            click(toolbarLayoutButton); check(panelLayout == "SideBySide" && !FilePreviewOpen, "layout cycle wraps back to side-by-side while keeping preview closed");
            SetActive(right); right.List.SelectedItem = right.List.Items.Cast<Entry>().FirstOrDefault();
            ToggleQuickPreview(); UpdateLayout();
            check(previewSource == right && previewTarget == left && Grid.GetColumn(quickPreview) == 0 && !left.IsVisible, "right-panel selection previews in the left panel");
            SetPanelLayout("Stacked"); UpdateLayout();
            check(previewSource == right && previewTarget == left && Grid.GetRow(quickPreview) == 0, "bottom-panel selection previews in the top row");
            ToggleQuickPreview(); check(left.Visibility == Visibility.Visible && right.Visibility == Visibility.Visible, "closing a reverse preview restores both file panels");
        }
    }
}


