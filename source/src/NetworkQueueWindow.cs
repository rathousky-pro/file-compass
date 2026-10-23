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
        readonly TextBlock status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 5, 2, 5) };
        readonly DualCopyProgress progress = new DualCopyProgress { Height = 12, Margin = new Thickness(2, 6, 2, 9), ToolTip = L.Text("progress.fileAndTotalTracksHelp") };
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        readonly Button cancel;
        readonly Button pauseControl;
        readonly StackPanel empty;
        readonly WrapPanel controls;
        readonly Button cancelAll;
        readonly TextBlock heading;
        readonly TextBlock help;
        readonly GroupBox transferCard, filesCard;
        readonly CompressionRatioTrend speedTrend = new CompressionRatioTrend { SpeedMode=true,Height=60 };
        readonly GroupBox speedCard;
        NetworkTransferJob speedJob;
        bool hadRunningJobs;
        bool completing;
        bool refreshing;
        bool? populatedLayout;
        readonly TransferWindowState placement;
        internal void RestoreProgress() { placement.Restore(); }
        public NetworkQueueWindow(Window owner, Func<NetworkTransferJob[]> source, NetworkTransferJob selected)
        {
            getJobs = source; CopyUi.Style(this, owner, L.Text("queue.networkCopyTitle"), 820); MinWidth = 600;
            SizeToContent = SizeToContent.Manual; ResizeMode = ResizeMode.CanResize;
            placement = new TransferWindowState(this,delegate { var main = Owner as MainWindow; if (main != null) main.NetworkWindowBackground(true); },delegate { var main = Owner as MainWindow; if (main != null) main.NetworkWindowBackground(false); });
            Closing += delegate(object s,System.ComponentModel.CancelEventArgs e) { if (!completing && getJobs().Any(x => !x.Finished)) { e.Cancel = true; placement.DeferBackground(); } };
            var panel = new DockPanel { Margin = new Thickness(8) }; CopyUi.SectionedSurface(this, panel); SizeToContent = SizeToContent.Height; files.MaxHeight = Math.Max(80, Math.Min(150, MaxHeight - 550));
            var top = new StackPanel();
            transferCard = new GroupBox { Header=L.Text("queue.designRunning"), Margin=new Thickness(0,0,0,10) };
            var transferBody = new StackPanel(); transferCard.Content=transferBody;
            empty = QueuePresentation.Empty(L.Text("queue.designEmpty"), L.Text("queue.designNetworkHelp")); top.Children.Add(empty); top.Children.Add(transferCard);
            heading = QueuePresentation.Heading(L.Text("queue.networkCopyTitle"));
            help = CopyUi.Label(L.Text("queue.selectTransferHelp")); help.Foreground = FluentTheme.Muted;
            transferBody.Children.Add(jobs); transferBody.Children.Add(status);
            transferBody.Children.Add(progress); DockPanel.SetDock(top, Dock.Top); panel.Children.Add(top);
            speedCard = new GroupBox { Header=L.Text("transfer.speedHistory"), Content=speedTrend, Margin=new Thickness(0,0,0,10) };DockPanel.SetDock(speedCard,Dock.Top);panel.Children.Add(speedCard);
            var buttons = QueuePresentation.Actions();
            cancel = CopyUi.Button(L.Text("queue.cancelSelectedTransfer"), delegate { var job = jobs.SelectedItem as NetworkTransferJob; if (job == null) return; var main = Owner as MainWindow; if (main != null) main.StopNetworkJobs(new[] { job }); else job.Cancel(); }); buttons.Children.Add(cancel);
            cancelAll = CopyUi.Button(L.Text("queue.cancelAllTransfers"), delegate { var main = Owner as MainWindow; if (main != null) main.StopNetworkJobs(getJobs()); else foreach (var job in getJobs()) job.Cancel(); }); buttons.Children.Add(cancelAll);
            buttons.Children.Add(CopyUi.Button(L.Text("copy.backgroundAction"), placement.Background)); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
            controls = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,6,0,6) };
            pauseControl = CopyUi.Button(L.Text("action.pause"),delegate { var job = jobs.SelectedItem as NetworkTransferJob; if (job != null) job.Pause(!job.Paused); RefreshFiles(); }); controls.Children.Add(pauseControl);
            controls.Children.Add(CopyUi.Button(L.Text("queue.cancelItem"),delegate { var job = jobs.SelectedItem as NetworkTransferJob; var file = files.SelectedItem as NetworkQueueItem; if (job != null && file != null) job.CancelItem(file); }));
            controls.Children.Add(CopyUi.Button(L.Text("action.moveUp"),delegate { var job = jobs.SelectedItem as NetworkTransferJob; var file = files.SelectedItem as NetworkQueueItem; if (job != null && file != null) job.Reorder(file,-1); }));
            controls.Children.Add(CopyUi.Button(L.Text("action.moveDown"),delegate { var job = jobs.SelectedItem as NetworkTransferJob; var file = files.SelectedItem as NetworkQueueItem; if (job != null && file != null) job.Reorder(file,1); }));
            var limit = new ComboBox { IsEditable=true, Width=150, Height=30, Text="0", VerticalContentAlignment=VerticalAlignment.Center, ToolTip=L.Text("transfer.speedLimitHelp"), Margin=new Thickness(6,3,3,3) };
            foreach (string preset in new[] { "0", "512", "1024", "5120", "10240", "25600", "51200", "102400" }) limit.Items.Add(preset);
            controls.Children.Add(new TextBlock { Text=L.Text("queue.designSpeedTitle")+" (KiB/s):", VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(12,0,0,0) }); controls.Children.Add(limit);
            Action applyLimit=delegate { long value; var job=jobs.SelectedItem as NetworkTransferJob; if(job==null)return;
                if(Int64.TryParse(limit.Text.Trim(),out value)&&value>=0&&value<=1048576)job.Limit(value*1024);else MessageBox.Show(this,L.Text("transfer.speedLimitRangeRequired"),L.Text("transfer.speedLimitTitle")); };
            controls.Children.Add(CopyUi.Button(L.Text("action.set"),applyLimit));
            limit.PreviewKeyDown+=delegate(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Enter&&!limit.IsDropDownOpen){e.Handled=true;applyLimit();}};
            var fileBody = new DockPanel();
            DockPanel.SetDock(controls,Dock.Top); fileBody.Children.Add(controls);
            filesCard = new GroupBox { Header=L.Text("queue.designFiles"), Content=fileBody }; panel.Children.Add(filesCard);
            buttons.Children.Insert(2, CopyUi.Button(L.Text("history.openDialog"), delegate { var main = Owner as MainWindow; if (main != null) main.ShowOperationHistory(); }));
            var columns = new GridView(); AddColumn(columns, L.Text("queue.sourceItemColumn"), "Source", 265); AddColumn(columns, L.Text("column.target"), "Destination", 265); AddColumn(columns, L.Text("column.size"), "Size", 80); AddColumn(columns, L.Text("queue.existingFilePolicyColumn"), "Policy", 75); AddColumn(columns, L.Text("column.state"), "State", 200); files.View = columns; fileBody.Children.Add(files);
            files.SizeChanged += delegate {
                double width=Math.Max(200,files.ActualWidth-30);
                double[] shares={.32,.32,.10,.11,.15};
                for(int i=0;i<columns.Columns.Count;i++) columns.Columns[i].Width=width*shares[i];
            };
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
            bool any = job != null;
            speedCard.Visibility=any?Visibility.Visible:Visibility.Collapsed;
            if(speedJob!=job){speedJob=job;speedTrend.Clear();}
            if(job!=null)speedTrend.Add(job.Metrics().BytesPerSecond/1048576.0,job.Update().Total);
            if (populatedLayout != any) { Width = Math.Min(MaxWidth, any ? 950 : 650); populatedLayout = any; }
            empty.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
            transferCard.Visibility = filesCard.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            heading.Visibility = help.Visibility = status.Visibility = controls.Visibility = cancel.Visibility = cancelAll.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            jobs.Height = Math.Min(64, Math.Max(32, jobs.Items.Count * 32));
            jobs.Visibility = job == null ? Visibility.Collapsed : Visibility.Visible;
            files.Visibility = job == null ? Visibility.Collapsed : Visibility.Visible;
            files.Height = Math.Min(files.MaxHeight, Math.Max(64, 32 + Math.Min(4, files.Items.Count) * 28));
            progress.Visibility = job == null ? Visibility.Collapsed : Visibility.Visible;
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

