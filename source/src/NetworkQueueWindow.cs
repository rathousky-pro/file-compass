using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace CommanderCZ
{
    public sealed class NetworkQueueWindow : Window
    {
        readonly Func<NetworkTransferJob[]> getJobs;
        readonly ListBox jobs = new ListBox { Height = 95 };
        readonly ListView files = new ListView();
        readonly TextBlock status = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(2, 5, 2, 5) };
        readonly DualCopyProgress progress = new DualCopyProgress { Height = 12, Margin = new Thickness(2, 6, 2, 9), ToolTip = L.Text("progress.fileAndTotalTracksHelp") };
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        readonly Button cancel;
        readonly Button pauseControl;
        bool hadRunningJobs;
        bool completing;
        bool refreshing;
        readonly TransferWindowState placement;
        internal void RestoreProgress() { placement.Restore(); }
        public NetworkQueueWindow(Window owner, Func<NetworkTransferJob[]> source, NetworkTransferJob selected)
        {
            getJobs = source; CopyUi.Style(this, owner, L.Text("queue.networkCopyTitle"), 950); Height = 510; MinWidth = 650; MinHeight = 340;
            SizeToContent = SizeToContent.Manual; ResizeMode = ResizeMode.CanResize;
            placement = new TransferWindowState(this,delegate { var main = Owner as MainWindow; if (main != null) main.NetworkWindowBackground(true); },delegate { var main = Owner as MainWindow; if (main != null) main.NetworkWindowBackground(false); });
            Closing += delegate(object s,System.ComponentModel.CancelEventArgs e) { if (!completing && getJobs().Any(x => !x.Finished)) { e.Cancel = true; placement.DeferBackground(); } };
            var panel = new DockPanel { Margin = new Thickness(8) }; Content = panel;
            var top = new StackPanel(); top.Children.Add(CopyUi.Label(L.Text("queue.selectTransferHelp"))); top.Children.Add(jobs); top.Children.Add(status);
            top.Children.Add(progress); DockPanel.SetDock(top, Dock.Top); panel.Children.Add(top);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
            cancel = CopyUi.Button(L.Text("queue.cancelSelectedTransfer"), delegate { var job = jobs.SelectedItem as NetworkTransferJob; if (job == null) return; var main = Owner as MainWindow; if (main != null) main.StopNetworkJobs(new[] { job }); else job.Cancel(); }); buttons.Children.Add(cancel);
            buttons.Children.Add(CopyUi.Button(L.Text("queue.cancelAllTransfers"), delegate { var main = Owner as MainWindow; if (main != null) main.StopNetworkJobs(getJobs()); else foreach (var job in getJobs()) job.Cancel(); }));
            buttons.Children.Add(CopyUi.Button(L.Text("action.close"), Close)); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
            var controls = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,4,0,4) };
            pauseControl = CopyUi.Button(L.Text("action.pause"),delegate { var job = jobs.SelectedItem as NetworkTransferJob; if (job != null) job.Pause(!job.Paused); RefreshFiles(); }); controls.Children.Add(pauseControl);
            controls.Children.Add(CopyUi.Button(L.Text("queue.cancelItem"),delegate { var job = jobs.SelectedItem as NetworkTransferJob; var file = files.SelectedItem as NetworkQueueItem; if (job != null && file != null) job.CancelItem(file); }));
            controls.Children.Add(CopyUi.Button(L.Text("action.moveUp"),delegate { var job = jobs.SelectedItem as NetworkTransferJob; var file = files.SelectedItem as NetworkQueueItem; if (job != null && file != null) job.Reorder(file,-1); }));
            controls.Children.Add(CopyUi.Button(L.Text("action.moveDown"),delegate { var job = jobs.SelectedItem as NetworkTransferJob; var file = files.SelectedItem as NetworkQueueItem; if (job != null && file != null) job.Reorder(file,1); }));
            var limit = new TextBox { Text = "0", Width = 65, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = L.Text("transfer.speedLimitHelp") }; controls.Children.Add(new TextBlock { Text = " Limit KiB/s:",VerticalAlignment = VerticalAlignment.Center }); controls.Children.Add(limit);
            controls.Children.Add(CopyUi.Button(L.Text("action.set"),delegate { long value; var job = jobs.SelectedItem as NetworkTransferJob; if (job == null) return; if (Int64.TryParse(limit.Text,out value) && value >= 0 && value <= 1048576) job.Limit(value * 1024); else MessageBox.Show(this,L.Text("transfer.speedLimitRangeRequired"),L.Text("transfer.speedLimitTitle")); })); controls.Children.Add(CopyUi.Button(L.Text("action.minimize"),placement.Background));
            DockPanel.SetDock(controls,Dock.Top); panel.Children.Add(controls);
            buttons.Children.Insert(2, CopyUi.Button(L.Text("history.openDialog"), delegate { var main = Owner as MainWindow; if (main != null) main.ShowOperationHistory(); }));
            var columns = new GridView(); AddColumn(columns, L.Text("queue.sourceItemColumn"), "Source", 265); AddColumn(columns, L.Text("column.target"), "Destination", 265); AddColumn(columns, L.Text("column.size"), "Size", 80); AddColumn(columns, L.Text("queue.existingFilePolicyColumn"), "Policy", 75); AddColumn(columns, L.Text("column.state"), "State", 200); files.View = columns; panel.Children.Add(files);
            jobs.SelectionChanged += delegate { if (!refreshing) { RefreshFiles(); var job = jobs.SelectedItem as NetworkTransferJob; limit.Text = job == null ? "0" : (job.SpeedLimit / 1024).ToString(); } };
            Refresh(selected); timer.Tick += delegate { Refresh(jobs.SelectedItem as NetworkTransferJob); if (hadRunningJobs && jobs.Items.Count == 0) { completing = true; placement.Finish(); } }; timer.Start();
            Closed += delegate { timer.Stop(); };
        }
        static void AddColumn(GridView view, string heading, string property, double width)
        { view.Columns.Add(new GridViewColumn { Header = heading, Width = width, DisplayMemberBinding = L.Binding(property) }); }
        void Refresh(NetworkTransferJob selected)
        {
            var selectedFile = files.SelectedItem as NetworkQueueItem;
            var running = getJobs().Where(x => !x.Finished).ToArray();
            if (running.Length > 0) hadRunningJobs = true;
            refreshing = true;
            jobs.ItemsSource = running; jobs.SelectedItem = selected;
            if (jobs.SelectedItem == null && jobs.Items.Count > 0) jobs.SelectedIndex = 0;
            refreshing = false;
            RefreshFiles();
            if (selectedFile != null) files.SelectedItem = files.Items.Cast<NetworkQueueItem>().FirstOrDefault(x => x.Source == selectedFile.Source && x.Destination == selectedFile.Destination);
        }
        public void SelectJob(NetworkTransferJob job) { Refresh(job); }
        void RefreshFiles()
        {
            var job = jobs.SelectedItem as NetworkTransferJob;
            var selectedFile = files.SelectedItem as NetworkQueueItem;
            files.ItemsSource = job == null ? new NetworkQueueItem[0] : job.SnapshotItems();
            if (selectedFile != null) files.SelectedItem = files.Items.Cast<NetworkQueueItem>().FirstOrDefault(x => x.Source == selectedFile.Source && x.Destination == selectedFile.Destination);
            status.Text = job == null ? L.Text("queue.emptyTransfersHelp") : job.Update().Text; status.ToolTip = status.Text;
            if (job != null) { var update = job.Update(); progress.Update(update.File, update.Total, update.Preparing); } else progress.Update(0, 0, false);
            cancel.IsEnabled = job != null && !job.Finished;
            pauseControl.IsEnabled = job != null && !job.Finished; pauseControl.Content = job != null && job.Paused ? L.Text("action.resume") : L.Text("action.pause");
        }
    }
    public partial class MainWindow
    {
        readonly List<NetworkTransferJob> networkJobs = new List<NetworkTransferJob>();
        NetworkQueueWindow networkQueue;
        internal void NetworkWindowBackground(bool background)
        { foreach (var job in networkJobs.Where(x => !x.Finished && x.Activity != null)) job.Activity.Backgrounded = background; RefreshCopySummary(); }
        void ShowNetworkQueue(NetworkTransferJob selected)
        {
            if (networkQueue == null)
            {
                networkQueue = new NetworkQueueWindow(this, delegate { return networkJobs.ToArray(); }, selected);
                networkQueue.Closed += delegate { networkQueue = null; }; networkQueue.Show(); NetworkWindowBackground(false);
            }
            else { if (selected != null) networkQueue.SelectJob(selected); networkQueue.RestoreProgress(); NetworkWindowBackground(false); }
        }
    }
}

