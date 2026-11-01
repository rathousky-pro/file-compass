using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;

namespace FileCompass
{
    public sealed class CopyActivity : Border
    {
        readonly TextBlock label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        readonly DualCopyProgress progress = new DualCopyProgress { Height = 12, Margin = new Thickness(0, 8, 0, 0), ToolTip = L.Text("progress.twoTracksHelp") };
        readonly Button cancel = new Button { Content = L.Text("action.cancelOperation"), MinWidth = 70, Margin = new Thickness(8, 0, 0, 0) };
        internal OperationRecord Record;
        internal Action Cancellation;
        internal Action PauseAction;
        internal Func<bool> IsPaused;
        internal Func<bool> ConfirmCancellation;
        internal bool Backgrounded = true;
        internal Func<NetworkTransferMetrics> LocalMetrics;
        internal string TransferTitle, TransferRoute;internal bool CompactDock;
        internal Action RestoreDetails;
        internal void RequestCancel() { RequestCancel(true); }
        internal void RequestCancel(bool confirm) { if (finished || (Record != null && Record.CancelRequested)) return; if (confirm && ConfirmCancellation != null && !ConfirmCancellation()) return; if (finished) return; if (Record != null) { Record.CancelRequested = true; Record.State = L.Code("state.cancelling"); } Cancellation(); cancel.IsEnabled = false; label.Text = L.Text("state.cancellingOperation"); if (Changed != null) Changed(); }
        bool finished;
        internal bool Finished { get { return finished; } }
        internal double FileFraction { get { return progress.FileFraction; } }
        internal bool Preparing { get { return progress.Preparing; } }
        internal Action Changed;
        internal double TotalFraction { get { return progress.TotalFraction; } }
        public CopyActivity(string title, Action cancellation, Action details = null)
        {
            RestoreDetails = details;
            Cancellation = cancellation;
            Margin = new Thickness(3, 2, 3, 2); Padding = new Thickness(5); BorderThickness = new Thickness(1); BorderBrush = CompactTheme.Line; Background = CompactTheme.Surface;
            var dock = new DockPanel(); Child = dock; DockPanel.SetDock(cancel, Dock.Right); dock.Children.Add(cancel);
            var body = new StackPanel(); body.Children.Add(label); body.Children.Add(progress); dock.Children.Add(body);
            if (details != null) { body.Cursor = System.Windows.Input.Cursors.Hand; body.MouseLeftButtonUp += delegate { details(); }; }
            label.Text = title; progress.Update(0, 0, true);
            cancel.Click += delegate { if (finished) { var panel = Parent as Panel; if (panel != null) panel.Children.Remove(this); } else RequestCancel(); if (Changed != null) Changed(); };
        }
        public void Update(string text, double fileFraction, double totalFraction, bool indeterminate = false)
        {
            if (finished) return;
            label.Text = text; ToolTip = text; if (Record != null) Record.Summary = text;
            progress.Update(fileFraction, totalFraction, indeterminate);
            if (Changed != null) Changed();
        }
        public void Finish(string text, bool success)
        {
            Update(text, success ? 1 : progress.FileFraction, success ? 1 : progress.TotalFraction); finished = true;
            if (Record != null) { Record.Ended = DateTime.Now; Record.State = success ? L.Code("state.completed") : Record.CancelRequested || text.IndexOf(L.Text("state.cancelledMatchFragment"), StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf(L.Text("state.interruptedMatchFragment"), StringComparison.OrdinalIgnoreCase) >= 0 ? L.Code("state.cancelled") : L.Text("state.completedWithErrors"); }
            cancel.Content = L.Text("action.close"); cancel.IsEnabled = true;
            if (Changed != null) Changed();
        }
    }
    public sealed class DualCopyProgress : FrameworkElement
    {
        public double FileFraction { get; private set; }
        public double TotalFraction { get; private set; }
        bool preparing;
        internal bool Preparing { get { return preparing; } }
        static readonly Brush FileColor = new SolidColorBrush(Color.FromRgb(133, 197, 244));
        static readonly Brush TotalColor = new SolidColorBrush(Color.FromRgb(20, 87, 162));
        public void Update(double file, double total, bool indeterminate)
        {
            FileFraction = Math.Max(0, Math.Min(1, file)); TotalFraction = Math.Max(0, Math.Min(1, total)); preparing = indeterminate; InvalidateVisual();
        }
        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing);
            double width = Math.Max(0, ActualWidth), height = Math.Max(0, ActualHeight);
            if (width == 0 || height == 0) return;
            double line = Math.Min(3, height / 3);
            var track = CompactTheme.ThemeBrush("#E7ECF3");
            drawing.DrawRoundedRectangle(track, null, new Rect(0, 0, width, line), 1.5, 1.5);
            drawing.DrawRoundedRectangle(track, null, new Rect(0, height - line, width, line), 1.5, 1.5);
            drawing.DrawRoundedRectangle(FileColor, null, new Rect(0, 0, width * (preparing ? .25 : FileFraction), line), 1.5, 1.5);
            drawing.DrawRoundedRectangle(TotalColor, null, new Rect(0, height - line, width * (preparing ? .25 : TotalFraction), line), 1.5, 1.5);
        }
    }
    public partial class MainWindow
    {
        readonly StackPanel copyActivities = new StackPanel();
        readonly Border copySummary = new Border { Margin = new Thickness(8, 0, 8, 6), Padding = new Thickness(8), CornerRadius = new CornerRadius(6), Background = CompactTheme.WhiteSurface, BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(1), Visibility = Visibility.Collapsed, Cursor = System.Windows.Input.Cursors.Hand };
        readonly DualCopyProgress summaryProgress = new DualCopyProgress();
        readonly ProgressBar summaryTrack = new ProgressBar { Height = 6, Minimum = 0, Maximum = 1, BorderThickness = new Thickness(0), Foreground = FluentTheme.Accent, Background = CompactTheme.ThemeBrush("#E7ECF3"), Margin = new Thickness(0, 6, 0, 0) };
        readonly TextBlock summaryLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = FluentTheme.Text, TextTrimming = TextTrimming.CharacterEllipsis };
        readonly TextBlock summaryPercent = new TextBlock { Foreground = FluentTheme.Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
        readonly TextBlock summaryRoute = new TextBlock { FontSize = 11, Margin = new Thickness(0, 2, 4, 0), Foreground = FluentTheme.Muted, TextTrimming = TextTrimming.CharacterEllipsis };
        readonly Button summaryPause = CompactTheme.Button("Ⅱ", L.Text("transfer.pauseTooltip"), null);
        readonly Button summaryCancel = new Wpf.Ui.Controls.Button { Content = "×", MinHeight = 0, Height = 28, Padding = new Thickness(0), MinWidth = 28, Width = 28, Margin = new Thickness(3, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Arrow, Visibility = Visibility.Collapsed };
        readonly TextBlock summaryTransferInfo = new TextBlock { FontSize = 11, Margin = new Thickness(0, 2, 0, 0), Foreground = FluentTheme.Muted, TextTrimming = TextTrimming.CharacterEllipsis, Visibility = Visibility.Collapsed };
        readonly Button summaryDetails=CompactTheme.Button("⌃ "+L.Text("transfer.compactDetails"),L.Text("transfer.openDetailsTooltip"),null);
        Window activityDetails;
        void InitializeCopySummary()
        {
            var dock=new DockPanel();copySummary.Child=dock;copySummary.Padding=new Thickness(12,10,12,10);copySummary.BorderBrush=CompactTheme.ThemeBrush("#79A9DF");copySummary.Background=CompactTheme.PanelSurface;
            DockPanel.SetDock(summaryCancel,Dock.Right);dock.Children.Add(summaryCancel);
            summaryDetails.Height=30;summaryDetails.Background=Brushes.Transparent;summaryDetails.BorderThickness=new Thickness(0);summaryDetails.Foreground=FluentTheme.Accent;summaryDetails.Padding=new Thickness(9,0,9,0);summaryDetails.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(summaryDetails,Dock.Right);dock.Children.Add(summaryDetails);summaryDetails.Click+=delegate{ShowSummaryDetails();};
            summaryPause.Width=Double.NaN;summaryPause.MinWidth=110;summaryPause.Height=30;summaryPause.Background=Brushes.Transparent;summaryPause.BorderThickness=new Thickness(0);summaryPause.Foreground=FluentTheme.Accent;summaryPause.Padding=new Thickness(9,0,9,0);summaryPause.Margin=new Thickness(12,0,0,0);DockPanel.SetDock(summaryPause,Dock.Right);dock.Children.Add(summaryPause);
            var icon=new TextBlock{Text="⇄",FontFamily=new FontFamily("Segoe UI Symbol"),FontSize=24,Foreground=FluentTheme.Accent,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(3,0,14,0)};DockPanel.SetDock(icon,Dock.Left);dock.Children.Add(icon);
            var body=new StackPanel();var title=new DockPanel();DockPanel.SetDock(summaryPercent,Dock.Right);title.Children.Add(summaryPercent);summaryLabel.MaxWidth=280;DockPanel.SetDock(summaryLabel,Dock.Left);title.Children.Add(summaryLabel);summaryRoute.Margin=new Thickness(6,0,4,0);summaryRoute.FontSize=12;summaryRoute.VerticalAlignment=VerticalAlignment.Center;title.Children.Add(summaryRoute);body.Children.Add(title);
            summaryTrack.Margin=new Thickness(0,6,0,0);body.Children.Add(summaryTrack);summaryTransferInfo.Margin=new Thickness(0,6,0,0);body.Children.Add(summaryTransferInfo);dock.Children.Add(body);
            copySummary.ToolTip = L.Text("transfer.openDetailsTooltip");
            body.MouseLeftButtonUp += delegate { ShowSummaryDetails(); };
            icon.MouseLeftButtonUp += delegate { ShowSummaryDetails(); };
            summaryPause.Click += delegate
            {
                var local = copyControl != null && copyActivities.Children.OfType<CopyActivity>().Any(x => !x.Finished && x.Backgrounded && x.LocalMetrics != null);
                var remote = networkJobs.Where(x => !x.Finished && x.Activity != null && x.Activity.Backgrounded).ToArray();
                bool pause = (local && !copyControl.Paused) || remote.Any(x => !x.Paused);
                if (local && copyControl.Paused != pause) copyProgress.TogglePause();
                foreach (var job in remote) job.Pause(pause);
                RefreshCopySummary();
            };
            InitializeTaskDock();
            summaryCancel.Click += delegate
            {
                if (!ConfirmNetworkCancellation(networkJobs.Where(x => !x.Finished && x.Activity != null && x.Activity.Backgrounded).ToArray())) return;
                var background = copyActivities.Children.OfType<CopyActivity>().Where(x => !x.Finished && x.Backgrounded).ToArray();
                if (copyControl != null && background.Any(x => x.Cancellation.Target == copyControl)) CancelWaitingCopies();
                foreach (var activity in background) activity.RequestCancel(false);
                RefreshCopySummary();
            };
        }
        void ShowSummaryDetails()
        {
            var running = copyActivities.Children.OfType<CopyActivity>().Where(x => !x.Finished && x.Backgrounded).ToArray();
            if (running.Length == 1 && running[0].RestoreDetails != null) { running[0].RestoreDetails(); return; }
            var transfer = networkJobs.FirstOrDefault(x => !x.Finished && x.Activity != null && x.Activity.Backgrounded);
            if (transfer != null) ShowNetworkQueue(transfer);
            else if (copyProgress != null && !copyProgress.IsVisible) ShowCopyProgress();
            else ShowActivityDetails();
        }
        void RefreshCopySummary()
        {
            foreach (var completed in copyActivities.Children.OfType<CopyActivity>().Where(x => x.Finished).ToArray()) copyActivities.Children.Remove(completed);
            var all = copyActivities.Children.OfType<CopyActivity>().Where(x => x.Backgrounded).ToArray();
            var running = all.Where(x => !x.Finished).ToArray();
            summaryCancel.Visibility = running.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            summaryCancel.IsEnabled = running.Any(x => x.Record == null || !x.Record.CancelRequested);
            summaryCancel.ToolTip = L.Text("transfer.cancelSummaryTooltip");
            // The task dock alone owns visibility, including background tools without copy jobs.
            var primary = running.FirstOrDefault();
            summaryLabel.Text = running.Length == 1 ? primary.TransferTitle ?? L.Text("transfer.filesTitle") : L.Text("transfer.activeOperationsPrefix") + running.Length;
            var transfers = networkJobs.Where(x => !x.Finished && x.Activity != null && !x.Activity.Finished && x.Activity.Backgrounded).Select(x => x.Metrics())
                .Concat(running.Where(x => x.LocalMetrics != null).Select(x => x.LocalMetrics())).ToArray();
            copySummary.ToolTip = primary == null ? L.Text("transfer.detailsTitle") : primary.TransferRoute ?? primary.ToolTip;
            summaryTransferInfo.Visibility = transfers.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            long rate = transfers.Sum(x => x.BytesPerSecond);
            double? remaining = transfers.Length > 0 && transfers.All(x => x.RemainingSeconds.HasValue) ? (double?)transfers.Max(x => x.RemainingSeconds.Value) : null;
            bool localPaused = copyControl != null && running.Any(x => x.LocalMetrics != null) && copyControl.Paused;
            var remoteJobs = networkJobs.Where(x => !x.Finished && x.Activity != null && x.Activity.Backgrounded).ToArray();
            bool paused = (localPaused || remoteJobs.Any(x => x.Paused)) && !(copyControl != null && running.Any(x => x.LocalMetrics != null) && !copyControl.Paused) && !remoteJobs.Any(x => !x.Paused);
            summaryPause.IsEnabled = (copyControl != null && running.Any(x => x.LocalMetrics != null)) || remoteJobs.Length > 0;
            summaryPause.Content = paused ? "▶ "+L.Text("action.resume") : "Ⅱ "+L.Text("action.pause"); summaryPause.ToolTip = paused ? L.Text("transfer.resumeTooltip") : L.Text("transfer.pauseTooltip");
            summaryRoute.Text = running.Length == 1 ? primary.TransferRoute ?? "" : ""; summaryRoute.ToolTip = summaryRoute.Text;
            summaryTransferInfo.Text = (running.Length==1&&primary.LocalMetrics!=null&&copyProgress!=null?copyProgress.FileCountSummary:"")+(paused ? L.Text("transfer.pausedPrefix") : "") + (rate / 1048576.0).ToString("F1") + L.Text("transfer.rateAndRemainingSuffix") + TransferTime(remaining);
            summaryTransferInfo.ToolTip = L.Text("transfer.summaryMetricsHelp");
            var visible = running.Length == 0 ? all : running;
            if (visible.Length > 0) {
                summaryProgress.Update(visible.Average(x => x.FileFraction), visible.Average(x => x.TotalFraction), visible.All(x => x.Preparing));
                summaryTrack.Value = summaryProgress.TotalFraction; summaryTrack.IsIndeterminate = summaryProgress.Preparing && !paused;
                summaryPercent.Text = summaryProgress.Preparing ? L.Text("state.loading") : (100 * summaryProgress.TotalFraction).ToString("F0") + " %";
            }
            if (running.Length == 0 && activityDetails != null) activityDetails.Close();
            RefreshTaskDock();
        }
        internal static string TransferTime(double? seconds)
        {
            if (!seconds.HasValue || Double.IsNaN(seconds.Value) || Double.IsInfinity(seconds.Value) || seconds.Value < 0) return L.Text("common.unknownValue");
            long total = (long)Math.Min(Int64.MaxValue / 2, Math.Ceiling(seconds.Value));
            return (total / 3600).ToString("00") + ":" + ((total / 60) % 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
        void ShowActivityDetails()
        {
            if (activityDetails != null) { activityDetails.WindowState = WindowState.Normal; activityDetails.Activate(); return; }
            activityDetails = new Window(); CopyUi.Style(activityDetails, this, L.Text("queue.progressAndQueue"), 850);
            activityDetails.SizeToContent = SizeToContent.Height; activityDetails.ResizeMode = ResizeMode.CanResize;
            var body = new DockPanel { Margin = new Thickness(8) };
            var history = CopyUi.Button(L.Text("history.openMenu"), ShowOperationHistory); DockPanel.SetDock(history, Dock.Bottom); body.Children.Add(history);
            var network = CopyUi.Button(L.Text("queue.networkDetails"), delegate { ShowNetworkQueue(null); }); DockPanel.SetDock(network, Dock.Bottom); body.Children.Add(network);
            var scroll = new ScrollViewer { Content = copyActivities, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; body.Children.Add(scroll); CopyUi.Surface(activityDetails, body); scroll.MaxHeight = Math.Max(90, Math.Min(420, activityDetails.MaxHeight - 160));
            activityDetails.Closed += delegate { scroll.Content = null; activityDetails = null; }; activityDetails.Show();
        }
        int networkCopies;
        CopyActivity AddCopyActivity(string title, Action cancel, Action details = null, OperationRecord existing = null)
        {
            var record = existing ?? new OperationRecord { Title = title }; if (existing == null) operationJournal.Records.Insert(0, record); record.State = L.Code("state.running");
            var activity = new CopyActivity(title, cancel, details) { Record = record }; bool saved = false;
            record.Cancel = activity.RequestCancel;
            activity.Changed = delegate { RefreshCopySummary(); if (activity.Finished && !saved) { saved = true; operationJournal.Save(); } }; copyActivities.Children.Add(activity); RefreshCopySummary(); return activity;
        }
    }
}
