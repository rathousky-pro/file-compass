using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FileCompass
{
    public partial class Entry
    {
        public string FlatLocation { get; set; }
    }

    public partial class FilePanel
    {
        string flatPreviousDisplay;

        internal bool IsFlatView { get { return projection != null && projection.Kind == "Flat"; } }
        internal bool CanOpenFlatView { get { return CanUseFolderView && !IsExt && !HasUnavailableTab && !EmptyPanel; } }

        internal bool ToggleFlatView()
        {
            if (IsFlatView)
            {
                Navigate(projection.Root, false);
                return true;
            }
            if (!CanOpenFlatView || LocalLoading || IsLoading) return false;

            ShowProjection(new ProjectionSpec { Kind = "Flat", Root = CurrentPath }, false);
            return true;
        }

        void ShowFlatLocationColumn()
        {
            if (flatLocationColumn != null) return;
            AddColumn(L.Text("projection.location"), "FlatLocation", 145);
            flatLocationColumn = view.Columns.Last();
            view.Columns.Move(view.Columns.Count - 1, 1);
            FitColumns("FlatLocation");
        }

        void ExitFlatLocationColumn()
        {
            if (flatLocationColumn == null) return;
            view.Columns.Remove(flatLocationColumn);
            flatLocationColumn = null;
            if (sortProperty == "FlatLocation") sortProperty = "Name";
            var previousDisplay = flatPreviousDisplay;
            flatPreviousDisplay = null;
            if (previousDisplay != null && DisplayMode == "Details") SetDisplay(previousDisplay);
            FitColumns();
        }

        async Task LoadFlatView(ProjectionSpec spec)
        {
            CancelNavigation();
            var cancellation = new CancellationTokenSource();
            navigationCancellation = cancellation;
            var token = cancellation.Token;
            int request = navigationVersion;
            LocalLoading = true;
            Exception failure = null;
            var previous = List.ItemsSource == null ? new Entry[0] : List.ItemsSource.Cast<Entry>().ToArray();
            var selectedPath = (List.SelectedItem as Entry)?.FullPath;
            string root = spec.Root;
            try
            {
                root = Path.GetFullPath(spec.Root);
                if (IsArchive) CloseArchiveContext();
                if (IsRemote) { RememberRemote(); Remote = null; }
                projection = spec;
                CurrentPath = root;
                ShowFlatLocationColumn();
                List.ItemsSource = new List<Entry>();
                Sort();
                ShowPath();
                ConfigureWatcher();
                capacity.Text = L.Text("panel.loadingFolder");
                capacity.ToolTip = root;
                UpdateConnectionButtons();
                if (PathChanged != null) PathChanged(this, EventArgs.Empty);
                if (DisplayMode != "Details")
                {
                    flatPreviousDisplay = DisplayMode;
                    SetDisplay("Details");
                }

                var marked = new HashSet<string>(previous.Where(row => row.IsMarked).Select(row => row.FullPath), StringComparer.OrdinalIgnoreCase);
                await Task.Run(async delegate
                {
                    var rows = new List<Entry>();
                    var paths = new List<string>();
                    int batchSize = 64;

                    // Publish a small first batch, then grow the batches to limit full-list refreshes.
                    foreach (string path in ProjectionFiles.Enumerate(root, token))
                    {
                        paths.Add(path);
                        if (paths.Count < batchSize) continue;
                        AppendFlatRows(paths, root, rows, marked, token);
                        await PublishFlatRows(rows, selectedPath, request, token).ConfigureAwait(false);
                        paths.Clear();
                        batchSize = Math.Min(batchSize * 2, 8192);
                    }
                    AppendFlatRows(paths, root, rows, marked, token);
                    await PublishFlatRows(rows, selectedPath, request, token).ConfigureAwait(false);
                }, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { failure = ex; }
            finally
            {
                try
                {
                    if (!Dispatcher.HasShutdownStarted)
                        await Dispatcher.InvokeAsync(delegate
                        {
                            if (request != navigationVersion) return;
                            LocalLoading = false;
                            navigationCancellation = null;
                            capacity.Text = failure == null
                                ? L.Text("projection.flat") + " · " + List.Items.Cast<Entry>().Count(row => !row.IsParent) + L.Text("common.itemCountSuffix")
                                : L.Text("projection.incomplete");
                            capacity.ToolTip = failure == null ? root : failure.Message;
                            UpdateStatus();
                            if (failure != null) { status.Text = failure.Message; status.ToolTip = failure.Message; }
                        }).Task.ConfigureAwait(false);
                }
                finally { cancellation.Dispose(); }
            }
        }

        static void AppendFlatRows(List<string> paths, string root, List<Entry> rows, HashSet<string> marked, CancellationToken token)
        {
            var batch = ProjectionFiles.Rows(paths, root, token, true);
            if (rows.Count == 0) rows.Add(batch[0]);
            foreach (var row in batch.Skip(1))
            {
                row.IsMarked = marked.Contains(row.FullPath);
                rows.Add(row);
            }
        }

        Task PublishFlatRows(List<Entry> rows, string initialPath, int request, CancellationToken token)
        {
            return Dispatcher.InvokeAsync(delegate
            {
                if (request != navigationVersion || token.IsCancellationRequested) return;
                var cursor = List.SelectedItem as Entry;
                if (cursor == null || cursor.IsParent)
                    cursor = rows.FirstOrDefault(row => !row.IsParent && String.Equals(row.FullPath, initialPath, StringComparison.OrdinalIgnoreCase)) ?? cursor;

                double scroll = CaptureScroll();
                var snapshot = rows.ToList();
                // Keep the same row instances so live marks survive publication.
                List.ItemsSource = snapshot;
                Sort();
                List.SelectedItem = cursor != null && List.Items.Contains(cursor) ? cursor : List.Items.Cast<Entry>().FirstOrDefault();
                RestoreScroll(scroll, request);
                UpdateStatus();
                capacity.Text = L.Text("projection.flat") + " · " + (snapshot.Count - 1) + L.Text("common.itemCountSuffix") + " · " + L.Text("panel.loadingFolder");
            }, DispatcherPriority.Background, token).Task;
        }
    }
}
