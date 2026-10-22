using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CommanderCZ
{
    internal sealed class DirectorySyncWindow : Window
    {
        readonly string left, right;
        readonly SyncLocation leftLocation,rightLocation;
        readonly ComboBox direction = new ComboBox { SelectedIndex = 0, ItemsSource = new[] { L.Text("compare.leftToRight"), L.Text("compare.rightToLeft"), L.Text("sync.bidirectional") } };
        readonly CheckBox contents = new CheckBox { Content = L.Text("compare.compareFileContents"), Margin = new Thickness(0,4,0,4) };
        readonly CheckBox mirror = new CheckBox { Content = L.Text("sync.mirrorDeleteTargetOnly"), Margin = new Thickness(0,4,0,4), ToolTip = L.Text("sync.deleteSafetyHelp") };
        readonly CheckBox same = new CheckBox { Content = L.Text("compare.showEqualItems"), Margin = new Thickness(0,4,0,4) };
        readonly ListView list = new ListView(); readonly TextBlock status = CopyUi.Label(L.Text("compare.initialScanHelp"));
        readonly Button compare, apply; readonly StackPanel options;
        readonly MainWindow main; SyncPlan plan; CopyControl control; bool running;
        internal DirectorySyncWindow(MainWindow owner, string leftPath, string rightPath, bool sync)
            : this(owner,new SyncLocation(leftPath),new SyncLocation(rightPath),sync) { }
        internal DirectorySyncWindow(MainWindow owner,SyncLocation leftPath,SyncLocation rightPath,bool sync)
        {
            main = owner; leftLocation = leftPath; rightLocation = rightPath; left = leftPath.Caption; right = rightPath.Caption;
            CopyUi.Style(this, owner, sync ? L.Text("sync.title") : L.Text("compare.title"), 1100); Height = 720; MinWidth = 850; MinHeight = 560; ResizeMode = ResizeMode.CanResize; SizeToContent = SizeToContent.Manual;
            var body = new DockPanel { Margin = new Thickness(12) }; Content = body;
            var buttons = FluentTheme.Actions(); DockPanel.SetDock(buttons, Dock.Bottom); body.Children.Add(buttons);
            compare = CopyUi.Button(L.Text("action.compare"), async delegate { await Compare(); }); compare.IsDefault = true; buttons.Children.Add(compare);
            apply = CopyUi.Button(L.Text("action.synchronize"), async delegate { await Apply(); }); apply.IsEnabled = false; buttons.Children.Add(apply);
            buttons.Children.Add(CopyUi.Button(L.Text("action.cancelCurrentOperation"), delegate { if (control != null) control.Cancel(); }));
            buttons.Children.Add(CopyUi.Button(L.Text("action.close"), Close));
            DockPanel.SetDock(status, Dock.Bottom); body.Children.Add(status);
            var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); body.Children.Add(top);
            options = FluentTheme.Section(top, L.Text("compare.foldersAndRulesGroup"));
            options.Children.Add(CopyUi.Label(L.Text("compare.leftPrefix") + left + L.Text("compare.rightPrefix") + right)); options.Children.Add(direction);
            options.Children.Add(contents); options.Children.Add(mirror); options.Children.Add(same);
            options.Children.Add(new TextBlock { Text = L.Text("sync.directionHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted });
            var columns = new GridView();
            foreach (var column in new[] { new[] { L.Text("column.item"), "Relative", "240" }, new[] { L.Text("column.source"), "SourceInfo", "190" }, new[] { L.Text("column.target"), "TargetInfo", "190" }, new[] { L.Text("column.difference"), "Difference", "150" }, new[] { L.Text("column.plan"), "Action", "110" } })
                columns.Columns.Add(new GridViewColumn { Header = column[0], Width = Int32.Parse(column[2]), DisplayMemberBinding = L.Binding(column[1]) });
            var actionCell = new FrameworkElementFactory(typeof(ComboBox));
            actionCell.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("AvailableActions"));
            actionCell.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, new Binding("ChosenAction") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            actionCell.SetValue(FrameworkElement.MinWidthProperty, 94.0);
            columns.Columns.Insert(1, new GridViewColumn { Header = "↔", Width = 112, CellTemplate = new DataTemplate { VisualTree = actionCell } });
            list.View = columns; body.Children.Add(list);
            list.MouseDoubleClick += delegate { var row = list.SelectedItem as SyncRow; if (row != null && leftLocation.Template == null && rightLocation.Template == null && !String.IsNullOrEmpty(row.SourcePath) && !String.IsNullOrEmpty(row.TargetPath) && File.Exists(row.SourcePath) && File.Exists(row.TargetPath)) new FileDiffWindow(this, row.SourcePath, row.TargetPath).Show(); };
            direction.SelectionChanged += delegate { mirror.IsEnabled = direction.SelectedIndex != 2; if (!mirror.IsEnabled) { mirror.IsChecked = false; contents.IsChecked = true; } Invalidate(); }; contents.Click += delegate { Invalidate(); }; mirror.Click += delegate { Invalidate(); }; same.Click += delegate { Display(); };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (running) { if (control != null) control.Cancel(); e.Cancel = true; status.Text = L.Text("progress.cancellingWaitHelp"); } };
            Closed += delegate { leftLocation.Dispose(); rightLocation.Dispose(); };
        }
        void Invalidate() { plan = null; list.ItemsSource = null; apply.IsEnabled = false; status.Text = L.Text("compare.settingsChanged"); }
        void Display(bool refresh = true)
        {
            if (plan == null) return;
            if (refresh) list.ItemsSource = plan.Rows.Where(x => same.IsChecked == true || x.Action != L.Text("action.keep") || x.Difference == L.Text("compare.onlyInTarget")).ToArray();
            status.Text = L.Text("compare.itemCountPrefix") + plan.Rows.Count + L.Text("compare.copyCountPrefix") + plan.Rows.Count(x => x.Action == L.Text("action.copy") || x.Action == L.Code("action.overwrite")) + L.Text("compare.newFolderCountPrefix") + plan.Rows.Count(x => x.Action == L.Text("compare.createFolderPlan")) + L.Text("compare.deleteCountPrefix") + plan.Rows.Count(x => x.Action == L.Text("action.remove")) + L.Text("compare.conflictCountPrefix") + plan.Rows.Count(x => x.Action == L.Text("state.conflict"));
            apply.IsEnabled = !running && !plan.Rows.Any(x => x.Action == L.Text("state.conflict")) && plan.Rows.Any(x => x.Action != L.Text("action.keep"));
        }
        void Busy(bool value) { running = value; list.IsEnabled = options.IsEnabled = compare.IsEnabled = !value; apply.IsEnabled = false; }
        internal async Task Compare()
        {
            bool reverse = direction.SelectedIndex == 1, hash = contents.IsChecked == true, remove = mirror.IsChecked == true && direction.SelectedIndex != 2, both = direction.SelectedIndex == 2; plan = null; list.ItemsSource = null; Busy(true);
            using (control = new CopyControl()) {
                status.Text = L.Text("compare.runningInBackground");
                try { plan = await Task.Run(delegate { return leftLocation.Template == null && rightLocation.Template == null ? DirectorySync.Compare(reverse ? right : left, reverse ? left : right, hash, remove, control.Check, both) : RemoteDirectorySync.Compare(reverse ? rightLocation : leftLocation,reverse ? leftLocation : rightLocation,hash,remove,control,both); }); }
                catch (OperationCanceledException) { status.Text = L.Text("compare.cancelledStatus"); }
                catch (Exception ex) { status.Text = L.Text("compare.failedPrefix") + ex.Message; }
                finally { Busy(false); if (plan != null) { foreach (var row in plan.Rows) row.PropertyChanged += delegate { Display(false); }; Display(); } }
            } control = null;
        }
        async Task Apply()
        {
            var selected = plan; if (selected == null) return;
            int deletes = selected.Rows.Count(x => x.Action == L.Text("action.remove"));
            if (MessageBox.Show(this, L.Text("sync.confirmPlanSourcePrefix") + selected.SourceRoot + "\nDo: " + selected.TargetRoot + "\n\n" + status.Text + (deletes > 0 ? L.Text("sync.confirmMirrorWarning") : L.Text("sync.confirmOverwriteWarning")), L.Text("sync.confirmTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
            Busy(true);
            using (control = new CopyControl()) {
                try { await main.RunDirectorySync(selected, control, delegate(string text) { status.Text = text; }); }
                finally { Busy(false); plan = null; apply.IsEnabled = false; }
            } control = null;
        }
    }
    public partial class MainWindow
    {
        void CompareFolders(bool synchronize)
        {
            if (left.IsVirtualBrowser || right.IsVirtualBrowser || left.IsArchive || right.IsArchive || (left.IsRemote && left.Remote.Profile.Protocol == NetworkProtocol.SCP) || (right.IsRemote && right.Remote.Profile.Protocol == NetworkProtocol.SCP)) { MessageBox.Show(this, L.Text("compare.supportedLocations"), L.Text("compare.title")); return; }
            if (PanelsLoading || left.CurrentPath == null || right.CurrentPath == null) return;
            new DirectorySyncWindow(this,new SyncLocation(left.CurrentPath,left.Remote),new SyncLocation(right.CurrentPath,right.Remote),synchronize).Show();
        }
        internal async Task RunDirectorySync(SyncPlan plan, CopyControl control, Action<string> update)
        {
            var results = new List<OperationItem>(); var activity = AddCopyActivity(L.Text("sync.operationTitlePrefix") + plan.TargetRoot, control.Cancel);
            fileToolsRunning++;
            var progress = new Progress<Tuple<string,double>>(delegate(Tuple<string,double> value) { update(value.Item1); activity.Update(value.Item1, 0, value.Item2); });
            string message; bool success = false;
            try { await Task.Run(delegate { DirectorySync.Apply(plan, control, delegate(string text, double fraction) { ((IProgress<Tuple<string,double>>)progress).Report(Tuple.Create(text,fraction)); }, results); }); success = results.All(x => x.State == L.Code("state.completed")); message = L.Text("sync.completedErrorCountPrefix") + results.Count(x => x.State == L.Code("state.error")); }
            catch (OperationCanceledException) { message = L.Text("sync.cancelledChangesPreserved"); }
            catch (Exception ex) { message = L.Text("sync.failedPrefix") + ex.Message; results.Add(new OperationItem { Source = plan.SourceRoot, Target = plan.TargetRoot, State = L.Code("state.error"), Error = ex.Message }); }
            finally { fileToolsRunning--; }
            activity.Record.Items.AddRange(results); activity.Finish(message, success); update(message); left.Refresh(); right.Refresh();
        }
    }
}

