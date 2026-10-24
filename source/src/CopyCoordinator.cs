using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    public partial class MainWindow
    {
        sealed class CopyJob
        {
            public string[] Sources; public CopyOptions Options; public OperationRecord Record;
            internal DurableTransfer Durable; internal Dictionary<string,string> RecoveryTargets;
            public override string ToString() { return String.Join("; ", Sources) + "\n→ " + Options.Target; }
        }
        readonly Queue<CopyJob> copyJobs = new Queue<CopyJob>();
        readonly List<string> copyTargets = new List<string>();
        bool copyRunning;
        CopyProgressDialog copyProgress;
        CopyJob runningCopyJob;
        CopyControl copyControl;

        void StartCopy(bool move = false)
        {
            if (PanelsLoading || active.LocalLoading || (active == left ? right : left).LocalLoading) return;
            if (active.IsRemote || (active == left ? right : left).IsRemote) { NetworkTransfer(move); return; }
            var entries = active.Selected(); var other = active == left ? right : left;
            if (entries.Count == 0 || other.CurrentPath == null) return;
            var setup = new CopySetupDialog(this, entries, other.CurrentPath, copyTargets, move);
            var sourcePanel = active;
            bool accepted = setup.ShowDialog() == true; sourcePanel.FocusFiles(); if (!accepted) return;
            copyTargets.Remove(setup.Options.Target); copyTargets.Insert(0, setup.Options.Target); if (copyTargets.Count > 15) copyTargets.RemoveAt(15);
            setup.Options.Move = move;
            EnqueueCopy(new CopyJob { Sources = entries.Select(x => x.FullPath).ToArray(), Options = setup.Options });
            operation.Visibility = Visibility.Visible;
            if (!copyRunning) RunCopyQueue();
            else operation.Text = L.Text("copy.runningWithWaitingCount") + copyJobs.Count + L.Text("progress.clickToShowSuffix");
        }

        async void RunCopyQueue()
        {
            copyRunning = true;
            try
            {
                while (copyJobs.Count > 0)
                {
                    var job = copyJobs.Dequeue(); runningCopyJob = job;
                    using (var control = new CopyControl())
                    {
                        copyControl = control;
                        var dialog = new CopyProgressDialog(this, control); copyProgress = dialog;
                        var activity = AddCopyActivity((job.Options.Move ? L.Text("copy.preparingMovePrefix") : L.Text("copy.preparingCopyPrefix")) + job.Options.Target, control.Cancel, ShowCopyProgress, job.Record);
                        activity.TransferTitle = (job.Options.Move ? L.Text("copy.movingStatus") : L.Text("copy.copyingStatus")) + (job.Sources.Length == 1 ? System.IO.Path.GetFileName(job.Sources[0].TrimEnd('\\')) : job.Sources.Length + L.Text("common.itemCountSuffix"));
                        activity.TransferRoute = String.Join("; ", job.Sources) + L.Text("common.routeArrow") + job.Options.Target;
                        activity.LocalMetrics = dialog.Metrics;
                        dialog.MetricsChanged = RefreshCopySummary;
                        activity.Backgrounded = false; dialog.BecameBackground = delegate { activity.Backgrounded = true; RefreshCopySummary(); }; dialog.BecameForeground = delegate { activity.Backgrounded = false; RefreshCopySummary(); };
                        dialog.Show(); RefreshCopySummary();
                        operation.Text = L.Text("copy.copyingPrefix") + job.Options.Target + L.Text("progress.clickToShowSuffix");
                        var updates = new Progress<CopyUpdate>(delegate(CopyUpdate update)
                        {
                            if (copyProgress != dialog) return;
                            dialog.Update(update);
                            activity.Update(update.Phase + " · " + System.IO.Path.GetFileName(update.Source) + L.Text("copy.currentFilePrefix") + (update.FileTotal > 0 ? (100.0 * update.FileDone / update.FileTotal).ToString("F0") : "0") + L.Text("progress.percentAndTotalSeparator") + (update.TotalBytes > 0 ? (100.0 * update.TotalDone / update.TotalBytes).ToString("F0") : "0") + "% · " + update.Completed + " / " + update.TotalFiles + L.Text("common.fileCountSuffix"),
                                update.FileTotal > 0 ? (double)update.FileDone / update.FileTotal : 0,
                                update.TotalBytes > 0 ? (double)update.TotalDone / update.TotalBytes : (update.TotalFiles > 0 ? (double)update.Completed / update.TotalFiles : 0), update.Phase == L.Code("copy.loadingFiles") || update.Phase == L.Code("copy.savingPlan"));
                            operation.Text = (control.Paused ? L.Code("state.paused") : update.Phase) + ": " + update.Completed + " / " + update.TotalFiles +
                                L.Text("queue.waitingCountPrefix") + copyJobs.Count + L.Text("progress.clickToShowSuffix");
                        });
                        ConfigureCopyRetry(activity.Record, job.Options);
                        CopyResult result;
                        try
                        {
                            result = await Task.Run(delegate
                            {
                                var engine = new CopyEngine(job.Options, control, delegate(CopyConflict conflict)
                                {
                                    control.Check();
                                    return Dispatcher.Invoke(new Func<CopyDecision>(delegate
                                    {
                                        if (!dialog.IsVisible) dialog.RestoreProgress(); dialog.Activate();
                                        var collision = new CopyConflictDialog(dialog, conflict); collision.ShowDialog(); return collision.Decision;
                                    }));
                                }, ((IProgress<CopyUpdate>)updates).Report, delegate(CopyFailure failure)
                                {
                                    control.Check();
                                    return Dispatcher.Invoke(new Func<CopyErrorChoice>(delegate
                                    {
                                        var error = new CopyErrorDialog(this, failure); error.ShowDialog(); return error.Choice;
                                    }));
                                });
                                engine.RecoveryTargets = job.RecoveryTargets;
                                engine.PlanReady = delegate(List<DurableFile> plan) {
                                    if (job.RecoveryTargets != null) { foreach (var file in job.Durable.Files.Where(x => !x.Done && !x.Directory)) { var current = plan.FirstOrDefault(x => x.Source == file.Source); if (current == null || current.Bytes != file.Bytes || current.Modified != file.Modified) throw new System.IO.IOException(L.Text("queue.sourceChangedSinceSavePrefix") + file.Source); } }
                                    else durableQueue.Plan(job.Durable,plan);
                                };
                                engine.PersistDone = delegate(string path) { durableQueue.Done(job.Durable,path); };
                                var copied = engine.Run(job.Sources);
                                if (job.RecoveryTargets != null && job.Options.Move && !copied.Cancelled && copied.Errors.Count == 0) foreach (var folder in job.Durable.Files.Where(x => x.Directory).OrderByDescending(x => x.Source.Length)) if (System.IO.Directory.Exists(folder.Source) && !System.IO.Directory.EnumerateFileSystemEntries(folder.Source).Any()) System.IO.Directory.Delete(folder.Source,false);
                                return copied;
                            });
                        }
                        catch (Exception ex) { durableQueue.Finish(job.Durable,false); activity.Finish(L.Text("copy.failedPrefix") + ex.Message, false); throw; }
                        finally { dialog.Finish(); copyProgress = null; copyControl = null; }
                        if (!left.IsRemote && (job.Options.Move || left.CurrentPath == job.Options.Target)) left.Refresh();
                        if (!right.IsRemote && (job.Options.Move || right.CurrentPath == job.Options.Target)) right.Refresh();
                        operation.Text = (result.Cancelled ? L.Code("state.cancelled") : L.Code("state.completed")) + (job.Options.Move ? L.Text("copy.movedCountPrefix") : L.Text("copy.copiedCountPrefix")) + result.Copied + L.Text("copy.skippedCountSeparator") + result.Skipped + L.Text("operation.errorCountSeparator") + result.Errors.Count;
                        activity.Record.Undo=SafeUndo.Record(L.Text(job.Options.Move?"action.move":"action.copy"),result.Undo);
                        activity.Record.Items.AddRange(result.Items);
                        durableQueue.Finish(job.Durable,(result.Cancelled && !preserveQueueOnExit) || (!result.Cancelled && result.Errors.Count == 0 && !result.Items.Any(x => x.State == L.Code("state.error"))));
                        activity.Finish(operation.Text, !result.Cancelled && result.Errors.Count == 0 && !result.Items.Any(x => x.State == L.Code("state.error")));
                        if (result.Errors.Count > 0) ShowCopyErrors(result);
                    }
                }
            }
            catch (Exception ex) { operation.Text = L.Text("copy.failedPrefix") + ex.Message; MessageBox.Show(this, ex.Message, L.Code("copy.title"), MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { copyRunning = false; copyProgress = null; copyControl = null; runningCopyJob = null; }
        }

        void ShowCopyProgress()
        {
            if (copyProgress != null) copyProgress.RestoreProgress();
        }
        void ShowCopyQueue()
        {
            var window = new Window(); CopyUi.Style(window, this, L.Text("queue.copyTitle"), 650);
            var panel = new StackPanel(); CopyUi.SectionedSurface(window, panel);
            var empty = QueuePresentation.Empty(L.Text("queue.designEmpty"), L.Text("queue.designLocalHelp")); panel.Children.Add(empty);
            var runningSection = FluentTheme.Section(panel, L.Text("queue.designRunning"));
            var running = new TextBlock { TextWrapping=TextWrapping.Wrap, LineHeight=21 }; runningSection.Children.Add(running);
            var state = CopyUi.Label(""); state.Foreground=FluentTheme.Muted;runningSection.Children.Add(state);
            var runningProgress = new ProgressBar { Height=4, Minimum=0, Maximum=1, Margin=new Thickness(0,6,0,4) }; runningSection.Children.Add(runningProgress);
            var waitingSection = FluentTheme.Section(panel, L.Text("queue.designWaiting"));
            var noWaiting = CopyUi.Label(L.Text("queue.designNoWaiting"));waitingSection.Children.Add(noWaiting);
            var list = new ListBox { BorderThickness = new Thickness(0), Margin = new Thickness(0,8,0,8) }; waitingSection.Children.Add(list);
            var rowText = new FrameworkElementFactory(typeof(TextBlock)); rowText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
            rowText.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); rowText.SetValue(TextBlock.MarginProperty,new Thickness(4,5,4,5));
            list.ItemTemplate = new DataTemplate { VisualTree = rowText };
            list.HorizontalContentAlignment = HorizontalAlignment.Stretch; ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            var actions = QueuePresentation.Actions(); panel.Children.Add(actions);
            var show = CopyUi.Button(L.Text("action.showProgress"), delegate { window.Close(); ShowCopyProgress(); }); FluentTheme.Primary(show); actions.Children.Add(show);
            var waiting = CopyUi.Button(L.Text("queue.cancelWaiting"), CancelWaitingCopies); actions.Children.Add(waiting);
            var cancel = CopyUi.Button(L.Text("queue.cancelAll"), delegate { CancelWaitingCopies(); if (copyControl != null) copyControl.Cancel(); }); actions.Children.Add(cancel);
            var footer = QueuePresentation.Actions(); panel.Children.Add(footer);
            footer.Children.Add(CopyUi.Button(L.Text("history.openDialog"), ShowOperationHistory));
            footer.Children.Add(CopyUi.Button(L.Text("action.close"), window.Close));
            Action update = delegate {
                var pending = copyJobs.ToArray(); bool any = copyRunning || pending.Length > 0;
                if (!list.Items.Cast<CopyJob>().SequenceEqual(pending)) list.ItemsSource = pending;
                empty.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
                ((FrameworkElement)runningSection.Parent).Visibility = ((FrameworkElement)waitingSection.Parent).Visibility = actions.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
                running.Text = runningCopyJob == null ? L.Text("queue.noRunningJob") : String.Join("\n", runningCopyJob.Sources) + "\n→ " + runningCopyJob.Options.Target;
                noWaiting.Visibility = pending.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                state.Text = copyProgress == null ? "" : copyProgress.QueueStatus;
                runningProgress.Value = copyProgress == null ? 0 : Math.Max(0, Math.Min(1, copyProgress.QueueFraction));
                list.Visibility = pending.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
                list.Height = Math.Min(240, Math.Max(64, pending.Length * 64));
                show.IsEnabled = copyRunning; waiting.IsEnabled = pending.Length > 0;
            };
            var refresh = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            refresh.Tick += delegate { update(); }; update();
            window.Loaded += delegate { refresh.Start(); }; window.Closed += delegate { refresh.Stop(); }; window.ShowDialog();
        }
        void ShowCopyErrors(CopyResult result)
        {
            var window = new Window(); CopyUi.Style(window, this, L.Text("copy.resultTitle"), 760);
            var panel = new StackPanel { Margin = new Thickness(10) }; window.Content = panel;
            panel.Children.Add(CopyUi.Label(L.Text("copy.copiedPrefix") + result.Copied + L.Text("copy.skippedSummaryPrefix") + result.Skipped + L.Text("operation.errorSummaryPrefix") + result.Errors.Count));
            panel.Children.Add(new TextBox { Text = String.Join("\r\n\r\n", result.Errors), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 300 });
            panel.Children.Add(CopyUi.Button(L.Text("action.close"), window.Close)); window.ShowDialog();
        }
    }
}

