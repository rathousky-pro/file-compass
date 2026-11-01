using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;
namespace FileCompass
{
    public sealed class OperationItem
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public string State { get; set; }
        public string Error { get; set; }
        public bool Directory { get; set; }
        public override string ToString() { return L.Display(State) + " · " + Source + L.Text("common.routeArrow") + Target + (String.IsNullOrEmpty(Error) ? "" : "\n" + Error); }
    }
    public sealed class OperationRecord
    {
        public DateTime Started = DateTime.Now, Ended;
        public string Title = "", State = L.Code("state.running"), Summary = "";
        public List<OperationItem> Items = new List<OperationItem>();
        public CopyOptions Copy;
        [XmlIgnore] internal UndoGroup Undo;
        [XmlIgnore] public Action Retry;
        [XmlIgnore] public bool RetryInterrupted;
        [XmlIgnore] public Action Cancel;
        [XmlIgnore] internal bool CancelRequested;
        public override string ToString() { return Started.ToString("dd.MM. HH:mm:ss") + " · " + L.Display(State) + " · " + Title; }
    }
    public sealed class OperationJournal
    {
        public readonly ObservableCollection<OperationRecord> Records = new ObservableCollection<OperationRecord>();
        readonly string path;
        Task writes = Task.FromResult(true);
        public string LastError { get; private set; }
        public OperationJournal(string file)
        {
            path = file;
            try
            {
                if (File.Exists(path)) using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    foreach (var item in (List<OperationRecord>)new XmlSerializer(typeof(List<OperationRecord>)).Deserialize(reader))
                    { if (item.State == L.Code("state.running") || item.State == L.Code("state.cancelling") || item.State == L.Code("state.queued")) item.State = L.Code("state.interruptedOnExit"); Records.Add(item); }
            }
            catch (Exception ex) { LastError = ex.Message; }
        }
        public void Save()
        {
            // Snapshot and serialize on the owner thread; queued disk writes cannot race with each other.
            string xml;
            using (var writer = new StringWriter()) { new XmlSerializer(typeof(List<OperationRecord>)).Serialize(writer, Records.Take(300).ToList()); xml = writer.ToString(); }
            writes = writes.ContinueWith(delegate
            {
                string temp = path + ".tmp";
                try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); File.WriteAllText(temp, xml, Encoding.Unicode); if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); LastError = null; }
                catch (Exception ex) { LastError = ex.Message; }
            }, TaskScheduler.Default);
        }
        internal Task PendingWrites { get { return writes; } }
        public static string Log(IEnumerable<OperationRecord> records)
        {
            var text = new StringBuilder();
            foreach (var record in records) { text.AppendLine(record.ToString()); text.AppendLine(record.Summary); foreach (var item in record.Items) text.AppendLine(item.ToString()); text.AppendLine(); }
            return text.ToString();
        }
    }
    public partial class MainWindow
    {
        OperationJournal operationJournal;
        Window historyWindow;
        bool flushingJournal, windowClosed;
        async void FlushOperationsBeforeClose()
        {
            if (flushingJournal) return; flushingJournal = true;
            await operationJournal.PendingWrites; var closeRequest = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { flushingJournal = false; if (!windowClosed) Close(); }));
        }

        void InitializeOperations(string testDirectory)
        {
            durableQueue = new DurableQueue(DurableQueue.TestDirectory ?? testDirectory ?? AppPaths.DataDirectory);
            operationJournal = new OperationJournal(Path.Combine(testDirectory ?? AppPaths.DataDirectory, "operations.xml"));
            foreach (var record in operationJournal.Records) if (record.Copy != null) ConfigureCopyRetry(record, record.Copy);
        }
        void EnqueueCopy(CopyJob job)
        {
            if (job.Durable == null) job.Durable = durableQueue.Begin("Local",job.Sources,job.Options.Target,job.Options,NetworkCopyRule.Reject,job.Options.Move,null,null);
            job.Record = new OperationRecord { Title = (job.Options.Move ? L.Text("operation.moveTitle") : L.Code("copy.title")) + " · " + job.Options.Target, State = L.Code("state.queued"), Summary = job.Sources.Length + L.Text("common.itemCountSuffix") };
            ConfigureCopyRetry(job.Record, job.Options); operationJournal.Records.Insert(0, job.Record);
            job.Record.Cancel = delegate
            {
                var keep = copyJobs.Where(x => x != job).ToArray(); copyJobs.Clear(); foreach (var entry in keep) copyJobs.Enqueue(entry);
                durableQueue.Remove(job.Durable);
                job.Record.State = L.Code("state.cancelled"); job.Record.Ended = DateTime.Now; job.Record.Summary = L.Text("state.cancelledBeforeStart"); operationJournal.Save();
            };
            copyJobs.Enqueue(job);
        }
        void CancelWaitingCopies()
        {
            foreach (var job in copyJobs) if (job.Durable != null) durableQueue.Remove(job.Durable);
            foreach (var job in copyJobs) if (job.Record != null) { job.Record.State = L.Code("state.cancelled"); job.Record.Ended = DateTime.Now; job.Record.Summary = L.Text("state.cancelledBeforeStart"); }
            copyJobs.Clear(); operationJournal.Save();
        }
        void ConfigureCopyRetry(OperationRecord record, CopyOptions options)
        {
            record.Copy = options.Clone();
            record.Retry = delegate
            {
                foreach (var item in record.Items.Where(x => x.State == L.Code("state.error")).ToArray())
                {
                    var settings = options.Clone(); settings.Target = item.Target; settings.TargetIsFile = true;
                    if (item.Directory) settings.Target = Path.GetDirectoryName(item.Target);
                    EnqueueCopy(new CopyJob { Sources = new[] { item.Source }, Options = settings });
                }
                if (!copyRunning && copyJobs.Count > 0) RunCopyQueue();
            };
        }
        internal void ShowOperationHistory()
        {
            if (historyWindow != null) { historyWindow.Activate(); return; }
            var window = historyWindow = new Window(); CopyUi.Style(window, this, L.Text("history.title"), 900);
            window.Height = 570; window.ResizeMode = ResizeMode.CanResize; window.SizeToContent = SizeToContent.Manual;
            var dock = new DockPanel { Margin = new Thickness(10) }; window.Content = dock;
            var buttons = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(buttons, Dock.Bottom); dock.Children.Add(buttons);
            var list = new ListBox { Height = 190, ItemsSource = operationJournal.Records, Background = CompactTheme.WhiteSurface };
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            var row = new FrameworkElementFactory(typeof(TextBlock)); row.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); row.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding()); row.SetBinding(TextBlock.ToolTipProperty, new System.Windows.Data.Binding()); list.ItemTemplate = new DataTemplate { VisualTree = row };
            var listBorder = new Border { Child = list, BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Background = CompactTheme.WhiteSurface }; DockPanel.SetDock(listBorder, Dock.Top); dock.Children.Add(listBorder);
            var details = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 8) }; dock.Children.Add(details);
            var stop = CopyUi.Button(L.Text("history.cancelSelectedOperation"), delegate { var record = list.SelectedItem as OperationRecord; if (record != null && record.Ended == default(DateTime) && record.Cancel != null) record.Cancel(); }); buttons.Children.Add(stop);
            var retry = CopyUi.Button(L.Text("history.retryOrResume"), delegate { var record = list.SelectedItem as OperationRecord; if (record != null && record.Retry != null && record.Items.Any(x => x.State == L.Code("state.error") || (record.RetryInterrupted && (x.State == L.Code("state.interrupted") || x.State == L.Code("state.cancelled"))))) record.Retry(); }); buttons.Children.Add(retry);
            buttons.Children.Add(CopyUi.Button(L.Text("history.clear"),delegate{ConfirmClearTransactionHistory(window);}));
            buttons.Children.Add(CopyUi.Button(L.Text("history.saveLog"), delegate { var save = new SaveFileDialog { Filter = L.Text("history.textLogFileFilter"), FileName = "FileCompass-operace.txt" }; if (save.ShowDialog(window) == true) try { File.WriteAllText(save.FileName, OperationJournal.Log(operationJournal.Records), new UTF8Encoding(true)); } catch (Exception ex) { MessageBox.Show(window, ex.Message, L.Text("history.saveLogFailed")); } }));
            buttons.Children.Add(CopyUi.Button(L.Text("action.close"), window.Close));
            Action refresh = delegate { var record = list.SelectedItem as OperationRecord; details.Text = record == null ? L.Text("history.selectOperationPrompt") : OperationJournal.Log(new[] { record }); stop.IsEnabled = record != null && record.Ended == default(DateTime) && record.Cancel != null && !record.CancelRequested; retry.IsEnabled = record != null && record.Ended != default(DateTime) && record.Retry != null && record.Items.Any(x => x.State == L.Code("state.error") || (record.RetryInterrupted && (x.State == L.Code("state.interrupted") || x.State == L.Code("state.cancelled")))); };
            list.SelectionChanged += delegate { refresh(); };
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; timer.Tick += delegate { list.Items.Refresh(); refresh(); }; timer.Start();
            window.Closed += delegate { timer.Stop(); historyWindow = null; }; if (list.Items.Count > 0) list.SelectedIndex = 0; refresh(); window.Show();
        }
    }
}


