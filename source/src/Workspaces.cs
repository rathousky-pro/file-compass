using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    public sealed class Workspace
    {
        public string Name;
        public string LeftFilter = "", RightFilter = "";
        public SavedTab[] Left = new SavedTab[0], Right = new SavedTab[0];
        public int LeftIndex, RightIndex;
        public PanelView LeftView = new PanelView(), RightView = new PanelView();
        public double Split = .5; public double[] Widths = new double[0];
        public string PanelLayout = "SideBySide"; public bool SinglePanelRight; public double StackedSplitRatio = .5;
        public string LastDualLayout = "SideBySide";
        public override string ToString() { return Name; }
    }
    public sealed class WorkspaceData { public List<Workspace> Items = new List<Workspace>(); }
    public partial class FilePanel { internal string QuickFilter { get { return quickText.Text; } } }
    public partial class MainWindow
    {
        internal Workspace CaptureWorkspace(string name)
        {
            name = name.Trim(); if (name.Length == 0 || name.Length > 80) throw new IOException(L.Text("workspace.invalidName"));
            var state = new ProgramSettings(); CaptureView(state);
            return new Workspace { Name = name, LeftFilter = left.QuickFilter, RightFilter = right.QuickFilter, Left = state.LeftTabs, Right = state.RightTabs, LeftIndex = state.LeftTabIndex, RightIndex = state.RightTabIndex, LeftView = state.LeftView, RightView = state.RightView, Split = state.SplitRatio, Widths = state.ColumnWidths, PanelLayout = state.PanelLayout, SinglePanelRight = state.SinglePanelRight, StackedSplitRatio = state.StackedSplitRatio, LastDualLayout = state.LastDualLayout };
        }
        internal async Task ApplyWorkspace(Workspace workspace)
        {
            if (PanelsLoading || busy || fileToolsRunning > 0) throw new IOException(L.Text("app.fileOperationBusy"));
            CloseQuickPreview(); left.RestorePanelView(workspace.LeftView); right.RestorePanelView(workspace.RightView);
            foreach (var panel in new[] { left, right }) { if (panel.IsRemote || panel.IsArchive) panel.Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), false); }
            await Task.WhenAll(left.NavigationTask ?? Task.FromResult(true), right.NavigationTask ?? Task.FromResult(true));
            left.RestoreTabs(workspace.Left, workspace.LeftIndex); right.RestoreTabs(workspace.Right, workspace.RightIndex);
            if (workspace.Widths != null) for (int i = 0; i < Math.Min(5, workspace.Widths.Length); i++) { left.SetColumnWidth(i, workspace.Widths[i]); right.SetColumnWidth(i, workspace.Widths[i]); }
            RestorePanelLayout(new ProgramSettings { PanelLayout = workspace.PanelLayout, SinglePanelRight = workspace.SinglePanelRight, SplitRatio = workspace.Split, StackedSplitRatio = workspace.StackedSplitRatio, LastDualLayout = workspace.LastDualLayout });
            await Task.WhenAll(left.NavigationTask ?? Task.FromResult(true), right.NavigationTask ?? Task.FromResult(true));
            left.SetQuickFilter(workspace.LeftFilter);
            right.SetQuickFilter(workspace.RightFilter);
            SetActive(panelLayout == "Single" && workspace.SinglePanelRight ? right : left);
        }
        async void ShowWorkspaces()
        {
            WorkspaceData data;
            try { data = await Task.Run(() => WorkflowStore.Load<WorkspaceData>("workspaces.xml")); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("workspace.title")); return; }
            if (windowClosed) return;
            var window = new Window(); CopyUi.Style(window, this, L.Text("workspace.title"), 640); var body = new StackPanel { Margin = new Thickness(12) }; window.Content = body;
            body.Children.Add(new TextBlock { Text = L.Text("workspace.help"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted });
            var list = new ListBox { Height = 220, Margin = new Thickness(0, 8, 0, 8), ItemsSource = data.Items }; body.Children.Add(list);
            var actions = FluentTheme.Actions(); body.Children.Add(actions);
            Action refresh = delegate { list.ItemsSource = null; list.ItemsSource = data.Items; };
            actions.Children.Add(CopyUi.Button(L.Text("workspace.saveCurrent"), delegate
            {
                string name = Ask(L.Text("workspace.saveCurrent"), L.Text("workspace.name"), ""); if (name == null) return;
                try { var saved = CaptureWorkspace(name); var old = data.Items.FirstOrDefault(x => x.Name.Equals(saved.Name, StringComparison.CurrentCultureIgnoreCase)); if (old != null && MessageBox.Show(window, L.Text("workspace.confirmReplace"), L.Text("workspace.title"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return; if (old != null) data.Items.Remove(old); if (data.Items.Count >= 100) throw new IOException(L.Text("workspace.limit")); data.Items.Add(saved); WorkflowStore.Save("workspaces.xml", data); refresh(); }
                catch (Exception ex) { MessageBox.Show(window, ex.Message, L.Text("workspace.title")); }
            }));
            actions.Children.Add(CopyUi.Button(L.Text("action.open"), async delegate { var selected = list.SelectedItem as Workspace; if (selected == null) return; window.Close(); try { await ApplyWorkspace(selected); } catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("workspace.title")); } }));
            actions.Children.Add(CopyUi.Button(L.Text("action.delete"), delegate { var selected = list.SelectedItem as Workspace; if (selected == null || MessageBox.Show(window, L.Text("workspace.confirmDelete"), L.Text("workspace.title"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return; try { data.Items.Remove(selected); WorkflowStore.Save("workspaces.xml", data); refresh(); } catch (Exception ex) { MessageBox.Show(window, ex.Message, L.Text("workspace.title")); } }));
            actions.Children.Add(CopyUi.Button(L.Text("action.close"), window.Close)); window.ShowDialog();
        }
    }
}

