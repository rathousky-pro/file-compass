using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace CommanderCZ
{
    public enum DiffLineKind { Equal, Modified, Added, Removed }

    public sealed class DiffRow
    {
        public string LeftNumber { get; set; }
        public string LeftText { get; set; }
        public string Marker { get; set; }
        public string RightNumber { get; set; }
        public string RightText { get; set; }
        public DiffLineKind Kind { get; set; }
        public bool IsDifferent { get { return Kind != DiffLineKind.Equal; } }
    }

    public sealed class FileDiffResult
    {
        public bool Identical;
        public bool BinaryMode;
        public bool TruncatedRows;
        public long LeftBytes, RightBytes;
        public int EqualCount, ModifiedCount, AddedCount, RemovedCount, DifferentByteBlocks;
        public long TotalDifferentBytes;
        public string Summary;
        public List<DiffRow> Rows = new List<DiffRow>();
    }

    internal static class FileDiffEngine
    {
        const int MaxDisplayRows = 4000;
        const long MaxTextFileBytes = 32L * 1024 * 1024;
        const long AutoBinaryThresholdBytes = 16L * 1024 * 1024;

        static void ValidateRegularFile(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new IOException(L.Text("diff.fileMissingOrDirectory"));
            string full = Path.GetFullPath(path);
            if (!File.Exists(full) || Directory.Exists(full)) throw new IOException(L.Text("diff.fileMissingOrDirectory") + "\n" + full);
            for (string current = full; !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException(L.Text("compare.pathContainsLinkPrefix") + current);
        }

        internal static bool LooksBinary(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int probe = (int)Math.Min(stream.Length, 8192);
                if (probe == 0) return false;
                var buffer = new byte[probe];
                int read = stream.Read(buffer, 0, probe);
                if (read >= 2 && ((buffer[0] == 0xFF && buffer[1] == 0xFE) || (buffer[0] == 0xFE && buffer[1] == 0xFF))) return false;
                for (int i = 0; i < read; i++) if (buffer[i] == 0) return true;
                return false;
            }
        }

        internal static FileDiffResult Compare(string leftPath, string rightPath, int modeIndex, Encoding encoding, bool ignoreCase, bool ignoreTrailingSpace, CancellationToken token)
        {
            ValidateRegularFile(leftPath);
            ValidateRegularFile(rightPath);
            leftPath = Path.GetFullPath(leftPath);
            rightPath = Path.GetFullPath(rightPath);
            var leftInfo = new FileInfo(leftPath);
            var rightInfo = new FileInfo(rightPath);
            if (modeIndex == 1 && (leftInfo.Length > MaxTextFileBytes || rightInfo.Length > MaxTextFileBytes))
                throw new IOException(L.Text("diff.textSizeLimitExceeded"));
            bool binary = modeIndex == 2 || (modeIndex == 0 && (leftInfo.Length > AutoBinaryThresholdBytes || rightInfo.Length > AutoBinaryThresholdBytes || LooksBinary(leftPath) || LooksBinary(rightPath)));
            return binary
                ? CompareBinary(leftPath, rightPath, leftInfo.Length, rightInfo.Length, token)
                : CompareText(leftPath, rightPath, leftInfo.Length, rightInfo.Length, encoding ?? Encoding.UTF8, ignoreCase, ignoreTrailingSpace, token);
        }

        static FileDiffResult CompareBinary(string leftPath, string rightPath, long leftLength, long rightLength, CancellationToken token)
        {
            var result = new FileDiffResult { BinaryMode = true, LeftBytes = leftLength, RightBytes = rightLength };
            var equalPreview = new List<DiffRow>();
            using (var leftStream = new FileStream(leftPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var rightStream = new FileStream(rightPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] leftBuffer = new byte[65536], rightBuffer = new byte[65536];
                long offset = 0;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    int leftRead = ReadFullChunk(leftStream, leftBuffer);
                    int rightRead = ReadFullChunk(rightStream, rightBuffer);
                    int maxRead = Math.Max(leftRead, rightRead);
                    if (maxRead == 0) break;
                    for (int rowOffset = 0; rowOffset < maxRead; rowOffset += 16)
                    {
                        int leftCount = Math.Max(0, Math.Min(16, leftRead - rowOffset));
                        int rightCount = Math.Max(0, Math.Min(16, rightRead - rowOffset));
                        int rowDiffBytes = Math.Abs(leftCount - rightCount);
                        int shared = Math.Min(leftCount, rightCount);
                        for (int i = 0; i < shared; i++)
                            if (leftBuffer[rowOffset + i] != rightBuffer[rowOffset + i]) rowDiffBytes++;
                        long address = offset + rowOffset;
                        if (rowDiffBytes > 0)
                        {
                            result.TotalDifferentBytes += rowDiffBytes;
                            result.DifferentByteBlocks++;
                            if (leftCount > 0 && rightCount > 0) result.ModifiedCount++;
                            else if (leftCount == 0) result.AddedCount++;
                            else result.RemovedCount++;
                            if (result.Rows.Count < MaxDisplayRows)
                            {
                                result.Rows.Add(new DiffRow
                                {
                                    LeftNumber = leftCount > 0 ? address.ToString("X8") : "",
                                    LeftText = leftCount > 0 ? FormatHexLine(leftBuffer, rowOffset, leftCount) : "",
                                    Marker = leftCount == 0 ? "+" : rightCount == 0 ? "−" : "≠",
                                    RightNumber = rightCount > 0 ? address.ToString("X8") : "",
                                    RightText = rightCount > 0 ? FormatHexLine(rightBuffer, rowOffset, rightCount) : "",
                                    Kind = leftCount == 0 ? DiffLineKind.Added : rightCount == 0 ? DiffLineKind.Removed : DiffLineKind.Modified
                                });
                            }
                            else result.TruncatedRows = true;
                        }
                        else
                        {
                            result.EqualCount++;
                            if (equalPreview.Count < 256)
                            {
                                string text = FormatHexLine(leftBuffer, rowOffset, leftCount);
                                equalPreview.Add(new DiffRow
                                {
                                    LeftNumber = address.ToString("X8"),
                                    LeftText = text,
                                    Marker = "",
                                    RightNumber = address.ToString("X8"),
                                    RightText = text,
                                    Kind = DiffLineKind.Equal
                                });
                            }
                        }
                    }
                    offset += maxRead;
                }
            }
            result.Identical = result.TotalDifferentBytes == 0 && leftLength == rightLength;
            if (result.Identical) result.Rows = equalPreview;
            result.Summary = (result.Identical ? L.Text("diff.filesIdentical") : L.Format("diff.summaryBinaryFormat", result.TotalDifferentBytes, result.DifferentByteBlocks)) + (result.TruncatedRows ? L.Text("diff.rowLimitSuffix") : "");
            return result;
        }

        static int ReadFullChunk(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int n = stream.Read(buffer, total, buffer.Length - total);
                if (n == 0) break;
                total += n;
            }
            return total;
        }

        internal static string FormatHexLine(byte[] buffer, int offset, int count)
        {
            var sb = new StringBuilder(72);
            for (int i = 0; i < 16; i++)
            {
                if (i == 8) sb.Append("- ");
                if (i < count) sb.Append(buffer[offset + i].ToString("X2")).Append(' ');
                else sb.Append("   ");
            }
            sb.Append("| ");
            for (int i = 0; i < count; i++)
            {
                byte b = buffer[offset + i];
                sb.Append(b >= 32 && b < 127 ? (char)b : '.');
            }
            return sb.ToString();
        }

        static string[] ReadAllLinesShared(string path, Encoding encoding, CancellationToken token)
        {
            var lines = new List<string>();
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(file, encoding, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    token.ThrowIfCancellationRequested();
                    lines.Add(line);
                }
            }
            return lines.ToArray();
        }

        static string NormalizeLine(string line, bool ignoreTrailingSpace, bool ignoreCase)
        {
            if (ignoreTrailingSpace) line = line.TrimEnd(' ', '\t');
            return ignoreCase ? line.ToUpperInvariant() : line;
        }

        static FileDiffResult CompareText(string leftPath, string rightPath, long leftLength, long rightLength, Encoding encoding, bool ignoreCase, bool ignoreTrailingSpace, CancellationToken token)
        {
            string[] leftLines = ReadAllLinesShared(leftPath, encoding, token);
            string[] rightLines = ReadAllLinesShared(rightPath, encoding, token);
            string[] leftKeys = leftLines.Select(x => NormalizeLine(x, ignoreTrailingSpace, ignoreCase)).ToArray();
            string[] rightKeys = rightLines.Select(x => NormalizeLine(x, ignoreTrailingSpace, ignoreCase)).ToArray();

            var edits = ComputeLineEdits(leftKeys, rightKeys, token);
            var result = new FileDiffResult { BinaryMode = false, LeftBytes = leftLength, RightBytes = rightLength };

            int idx = 0;
            while (idx < edits.Count)
            {
                token.ThrowIfCancellationRequested();
                var item = edits[idx];
                if (item.Kind == 0)
                {
                    result.EqualCount++;
                    AddRow(result, new DiffRow
                    {
                        LeftNumber = (item.LeftIndex + 1).ToString(),
                        LeftText = leftLines[item.LeftIndex],
                        Marker = "",
                        RightNumber = (item.RightIndex + 1).ToString(),
                        RightText = rightLines[item.RightIndex],
                        Kind = DiffLineKind.Equal
                    });
                    idx++;
                    continue;
                }
                var removed = new List<int>();
                var added = new List<int>();
                while (idx < edits.Count && edits[idx].Kind != 0)
                {
                    if (edits[idx].Kind < 0) removed.Add(edits[idx].LeftIndex);
                    else added.Add(edits[idx].RightIndex);
                    idx++;
                }
                int paired = Math.Min(removed.Count, added.Count);
                for (int i = 0; i < paired; i++)
                {
                    result.ModifiedCount++;
                    AddRow(result, new DiffRow
                    {
                        LeftNumber = (removed[i] + 1).ToString(),
                        LeftText = leftLines[removed[i]],
                        Marker = "≠",
                        RightNumber = (added[i] + 1).ToString(),
                        RightText = rightLines[added[i]],
                        Kind = DiffLineKind.Modified
                    });
                }
                for (int i = paired; i < removed.Count; i++)
                {
                    result.RemovedCount++;
                    AddRow(result, new DiffRow
                    {
                        LeftNumber = (removed[i] + 1).ToString(),
                        LeftText = leftLines[removed[i]],
                        Marker = "−",
                        RightNumber = "",
                        RightText = "",
                        Kind = DiffLineKind.Removed
                    });
                }
                for (int i = paired; i < added.Count; i++)
                {
                    result.AddedCount++;
                    AddRow(result, new DiffRow
                    {
                        LeftNumber = "",
                        LeftText = "",
                        Marker = "+",
                        RightNumber = (added[i] + 1).ToString(),
                        RightText = rightLines[added[i]],
                        Kind = DiffLineKind.Added
                    });
                }
            }

            result.Identical = result.ModifiedCount == 0 && result.AddedCount == 0 && result.RemovedCount == 0;
            result.Summary = (result.Identical
                ? L.Text("diff.filesIdentical")
                : L.Format("diff.summaryTextFormat", result.ModifiedCount, result.AddedCount, result.RemovedCount, result.EqualCount)) + (result.TruncatedRows ? L.Text("diff.rowLimitSuffix") : "");
            return result;
        }

        static void AddRow(FileDiffResult result, DiffRow row)
        {
            if (result.Rows.Count < MaxDisplayRows) result.Rows.Add(row);
            else result.TruncatedRows = true;
        }

        struct LineEdit { public int Kind, LeftIndex, RightIndex; }

        static List<LineEdit> ComputeLineEdits(string[] a, string[] b, CancellationToken token)
        {
            var list = new List<LineEdit>(a.Length + b.Length);
            int start = 0;
            while (start < a.Length && start < b.Length && String.Equals(a[start], b[start], StringComparison.Ordinal))
            {
                list.Add(new LineEdit { Kind = 0, LeftIndex = start, RightIndex = start });
                start++;
            }
            int endA = a.Length - 1, endB = b.Length - 1;
            var suffix = new List<LineEdit>();
            while (endA >= start && endB >= start && String.Equals(a[endA], b[endB], StringComparison.Ordinal))
            {
                suffix.Add(new LineEdit { Kind = 0, LeftIndex = endA, RightIndex = endB });
                endA--; endB--;
            }
            int n = endA - start + 1, m = endB - start + 1;
            if (n > 0 && m > 0)
            {
                if ((long)n * m <= 2500000L)
                {
                    var dp = new int[n + 1, m + 1];
                    for (int i = n - 1; i >= 0; i--)
                    {
                        token.ThrowIfCancellationRequested();
                        for (int j = m - 1; j >= 0; j--)
                            dp[i, j] = String.Equals(a[start + i], b[start + j], StringComparison.Ordinal) ? 1 + dp[i + 1, j + 1] : Math.Max(dp[i + 1, j], dp[i, j + 1]);
                    }
                    int x = 0, y = 0;
                    while (x < n && y < m)
                    {
                        if (String.Equals(a[start + x], b[start + y], StringComparison.Ordinal))
                        { list.Add(new LineEdit { Kind = 0, LeftIndex = start + x, RightIndex = start + y }); x++; y++; }
                        else if (dp[x + 1, y] >= dp[x, y + 1])
                        { list.Add(new LineEdit { Kind = -1, LeftIndex = start + x }); x++; }
                        else
                        { list.Add(new LineEdit { Kind = 1, RightIndex = start + y }); y++; }
                    }
                    while (x < n) { list.Add(new LineEdit { Kind = -1, LeftIndex = start + x }); x++; }
                    while (y < m) { list.Add(new LineEdit { Kind = 1, RightIndex = start + y }); y++; }
                }
                else
                {
                    int x = 0, y = 0;
                    while (x < n && y < m)
                    {
                        token.ThrowIfCancellationRequested();
                        if (String.Equals(a[start + x], b[start + y], StringComparison.Ordinal))
                        { list.Add(new LineEdit { Kind = 0, LeftIndex = start + x, RightIndex = start + y }); x++; y++; continue; }
                        int matchX = -1, matchY = -1;
                        int window = Math.Min(64, Math.Max(n - x, m - y));
                        for (int d = 1; d <= window; d++)
                        {
                            if (x + d < n && String.Equals(a[start + x + d], b[start + y], StringComparison.Ordinal)) { matchX = x + d; matchY = y; break; }
                            if (y + d < m && String.Equals(a[start + x], b[start + y + d], StringComparison.Ordinal)) { matchX = x; matchY = y + d; break; }
                        }
                        if (matchX >= 0)
                        {
                            while (x < matchX) { list.Add(new LineEdit { Kind = -1, LeftIndex = start + x }); x++; }
                            while (y < matchY) { list.Add(new LineEdit { Kind = 1, RightIndex = start + y }); y++; }
                        }
                        else
                        {
                            list.Add(new LineEdit { Kind = -1, LeftIndex = start + x }); x++;
                            list.Add(new LineEdit { Kind = 1, RightIndex = start + y }); y++;
                        }
                    }
                    while (x < n) { list.Add(new LineEdit { Kind = -1, LeftIndex = start + x }); x++; }
                    while (y < m) { list.Add(new LineEdit { Kind = 1, RightIndex = start + y }); y++; }
                }
            }
            else
            {
                for (int i = 0; i < n; i++) list.Add(new LineEdit { Kind = -1, LeftIndex = start + i });
                for (int j = 0; j < m; j++) list.Add(new LineEdit { Kind = 1, RightIndex = start + j });
            }
            for (int i = suffix.Count - 1; i >= 0; i--) list.Add(suffix[i]);
            return list;
        }
    }

    public sealed class FileDiffWindow : Window
    {
        readonly ImageComparisonView imageComparison=new ImageComparisonView{Visibility=Visibility.Collapsed};
        readonly TextBox leftInput = new TextBox(), rightInput = new TextBox();
        readonly ComboBox mode = new ComboBox { Width = 175, SelectedIndex = 0, Margin = new Thickness(0, 0, 8, 0) };
        readonly ComboBox encoding = new ComboBox { Width = 145, SelectedIndex = 0, Margin = new Thickness(0, 0, 8, 0) };
        readonly CheckBox onlyDiffs = new CheckBox { Content = L.Text("diff.onlyDifferences"), Margin = new Thickness(4, 4, 10, 4) };
        readonly CheckBox ignoreSpace = new CheckBox { Content = L.Text("diff.ignoreTrailingSpace"), Margin = new Thickness(0, 4, 10, 4) };
        readonly CheckBox ignoreCase = new CheckBox { Content = L.Text("diff.ignoreCase"), Margin = new Thickness(0, 4, 10, 4) };
        readonly ListView list = new ListView { FontFamily = new FontFamily("Consolas"), FontSize = 12 };
        readonly TextBox leftDetail = new TextBox { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12, Height = 28, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 2) };
        readonly TextBox rightDetail = new TextBox { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12, Height = 28, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 4) };
        readonly TextBlock status = CopyUi.Label(L.Text("diff.selectTwoFilesHelp"));
        readonly Button compareButton, prevButton, nextButton;
        CancellationTokenSource cancellation;
        internal Task ComparisonTask { get; private set; }
        internal FileDiffResult LastResult { get; private set; }

        public FileDiffWindow(Window owner, string leftFile, string rightFile)
        {
            CopyUi.Style(this, owner, L.Text("diff.title"), 1120); Height = 740; MinWidth = 860; MinHeight = 560; ResizeMode = ResizeMode.CanResize; SizeToContent = SizeToContent.Manual;
            leftInput.Text = leftFile ?? ""; rightInput.Text = rightFile ?? "";
            foreach (string item in new[] { L.Text("diff.modeAuto"), L.Text("diff.modeText"), L.Text("diff.modeBinary"),L.Text("imageCompare.mode") }) mode.Items.Add(item);
            foreach (string item in new[] { "UTF-8 / podle BOM", "Windows-1250", "UTF-16 LE", "UTF-16 BE" }) encoding.Items.Add(item);

            var body = new DockPanel { Margin = new Thickness(12) }; Content = body;
            var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); body.Children.Add(top);
            var filesSection = FluentTheme.Section(top, L.Text("diff.title"));
            AddFileRow(filesSection, L.Text("diff.leftFileLabel"), leftInput);
            AddFileRow(filesSection, L.Text("diff.rightFileLabel"), rightInput);

            var toolbar = new WrapPanel { Margin = new Thickness(0, 8, 0, 4), VerticalAlignment = VerticalAlignment.Center };
            filesSection.Children.Add(toolbar);
            toolbar.Children.Add(mode);
            toolbar.Children.Add(encoding);
            toolbar.Children.Add(onlyDiffs);
            toolbar.Children.Add(ignoreSpace);
            toolbar.Children.Add(ignoreCase);

            var bottomActions = FluentTheme.Actions(); DockPanel.SetDock(bottomActions, Dock.Bottom); body.Children.Add(bottomActions);
            compareButton = CopyUi.Button(L.Text("action.compare"), async delegate { await RunCompare(); }); compareButton.IsDefault = true; bottomActions.Children.Add(compareButton);
            bottomActions.Children.Add(CopyUi.Button(L.Text("diff.swapFiles"), async delegate { string t = leftInput.Text; leftInput.Text = rightInput.Text; rightInput.Text = t; if (File.Exists(leftInput.Text) && File.Exists(rightInput.Text)) await RunCompare(); }));
            prevButton = CopyUi.Button(L.Text("diff.previousDifference"), delegate { JumpDifference(-1); }); bottomActions.Children.Add(prevButton);
            nextButton = CopyUi.Button(L.Text("diff.nextDifference"), delegate { JumpDifference(1); }); bottomActions.Children.Add(nextButton);
            var close = CopyUi.Button(L.Text("action.close"), delegate { if (cancellation != null) cancellation.Cancel(); else Close(); }); close.IsCancel = true; bottomActions.Children.Add(close);

            var detailPanel = new StackPanel { Margin = new Thickness(0, 6, 0, 4) }; DockPanel.SetDock(detailPanel, Dock.Bottom); body.Children.Add(detailPanel);
            detailPanel.Children.Add(leftDetail);
            detailPanel.Children.Add(rightDetail);
            DockPanel.SetDock(status, Dock.Bottom); body.Children.Add(status);

            var view = new GridView();
            view.Columns.Add(new GridViewColumn { Header = L.Text("diff.columnLeftLine"), Width = 64, DisplayMemberBinding = new Binding("LeftNumber") });
            view.Columns.Add(new GridViewColumn { Header = L.Text("diff.columnLeftContent"), Width = 440, DisplayMemberBinding = new Binding("LeftText") });
            view.Columns.Add(new GridViewColumn { Header = L.Text("diff.columnMarker"), Width = 54, DisplayMemberBinding = new Binding("Marker") });
            view.Columns.Add(new GridViewColumn { Header = L.Text("diff.columnRightLine"), Width = 64, DisplayMemberBinding = new Binding("RightNumber") });
            view.Columns.Add(new GridViewColumn { Header = L.Text("diff.columnRightContent"), Width = 440, DisplayMemberBinding = new Binding("RightText") });
            list.View = view;
            list.ItemContainerStyle = CreateRowStyle();
            list.SizeChanged += delegate
            {
                if (list.ActualWidth > 320)
                {
                    double side = Math.Max(180, (list.ActualWidth - 210) / 2);
                    view.Columns[1].Width = side;
                    view.Columns[4].Width = side;
                }
            };
            list.SelectionChanged += delegate
            {
                var row = list.SelectedItem as DiffRow;
                leftDetail.Text = row == null ? "" : row.LeftText ?? "";
                rightDetail.Text = row == null ? "" : row.RightText ?? "";
            };
            var displayHost=new Grid();displayHost.Children.Add(list);displayHost.Children.Add(imageComparison);body.Children.Add(new Border{Child=displayHost,Background=Brushes.White,BorderBrush=CompactTheme.ChromeLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),Padding=new Thickness(8)});

            onlyDiffs.Checked += delegate { ApplyFilter(); }; onlyDiffs.Unchecked += delegate { ApplyFilter(); };
            mode.SelectionChanged += delegate { if (LastResult != null || imageComparison.Visibility==Visibility.Visible || cancellation!=null) ComparisonTask = RunCompare(); };
            encoding.SelectionChanged += delegate { if (LastResult != null || imageComparison.Visibility==Visibility.Visible || cancellation!=null) ComparisonTask = RunCompare(); };
            ignoreSpace.Click += delegate { if (LastResult != null || imageComparison.Visibility==Visibility.Visible || cancellation!=null) ComparisonTask = RunCompare(); };
            ignoreCase.Click += delegate { if (LastResult != null || imageComparison.Visibility==Visibility.Visible || cancellation!=null) ComparisonTask = RunCompare(); };

            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.F7 || (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.Shift)) { JumpDifference(-1); e.Handled = true; }
                else if (e.Key == Key.F8 || (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.None)) { JumpDifference(1); e.Handled = true; }
            };
            Closing += delegate { if (cancellation != null) cancellation.Cancel(); };
            if (!String.IsNullOrWhiteSpace(leftFile) && !String.IsNullOrWhiteSpace(rightFile) && File.Exists(leftFile) && File.Exists(rightFile))
                ComparisonTask = RunCompare();
        }

        void AddFileRow(Panel parent, string label, TextBox input)
        {
            parent.Children.Add(CopyUi.Label(label));
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 4) };
            var browse = CopyUi.Button(L.Text("common.ellipsis"), delegate
            {
                var dialog = new OpenFileDialog { CheckFileExists = true, FileName = input.Text };
                if (dialog.ShowDialog(this) == true) input.Text = dialog.FileName;
            });
            browse.Width = 36; browse.MinWidth = 36; DockPanel.SetDock(browse, Dock.Right);
            row.Children.Add(browse); row.Children.Add(input);
            parent.Children.Add(row);
        }

        static Style CreateRowStyle()
        {
            var style = new Style(typeof(ListViewItem));
            style.Setters.Add(new Setter(Control.ForegroundProperty, FluentTheme.Text));
            var modified = new DataTrigger { Binding = new Binding("Kind"), Value = DiffLineKind.Modified };
            modified.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ColorBrush("#FFF6D6")));
            var added = new DataTrigger { Binding = new Binding("Kind"), Value = DiffLineKind.Added };
            added.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ColorBrush("#E6F9E6")));
            var removed = new DataTrigger { Binding = new Binding("Kind"), Value = DiffLineKind.Removed };
            removed.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ColorBrush("#FFE8E8")));
            style.Triggers.Add(modified); style.Triggers.Add(added); style.Triggers.Add(removed);
            return style;
        }

        void ApplyFilter()
        {
            if (LastResult == null) return;
            var rows = onlyDiffs.IsChecked == true ? LastResult.Rows.Where(x => x.IsDifferent).ToList() : LastResult.Rows;
            list.ItemsSource = rows;
            var firstDiff = rows.FirstOrDefault(x => x.IsDifferent) ?? rows.FirstOrDefault();
            if (firstDiff != null) { list.SelectedItem = firstDiff; list.ScrollIntoView(firstDiff); }
        }

        internal void JumpDifference(int direction)
        {
            var items = list.Items.Cast<DiffRow>().ToList();
            if (items.Count == 0 || !items.Any(x => x.IsDifferent)) { status.Text = L.Text("diff.noMoreDifferences"); return; }
            int current = Math.Max(0, list.SelectedIndex);
            for (int step = 1; step <= items.Count; step++)
            {
                int index = (current + step * direction + items.Count * 2) % items.Count;
                if (items[index].IsDifferent)
                {
                    list.SelectedIndex = index;
                    list.ScrollIntoView(items[index]);
                    return;
                }
            }
        }

        internal async Task RunCompare()
        {
            if (cancellation != null) cancellation.Cancel();
            var cts = new CancellationTokenSource(); cancellation = cts; var token = cts.Token;
            string leftPath = leftInput.Text.Trim().Trim('"'), rightPath = rightInput.Text.Trim().Trim('"');
            int selectedMode = mode.SelectedIndex;
            Encoding selectedEncoding = encoding.SelectedIndex == 1 ? Encoding.GetEncoding(1250) : encoding.SelectedIndex == 2 ? Encoding.Unicode : encoding.SelectedIndex == 3 ? Encoding.BigEndianUnicode : Encoding.UTF8;
            bool ignCase = ignoreCase.IsChecked == true, ignSpace = ignoreSpace.IsChecked == true;
            compareButton.IsEnabled = false; status.Text = L.Text("diff.comparingStatus");
            try
            {
                bool images=selectedMode==3||(selectedMode==0&&FileViewers.Kind(leftPath)=="image"&&FileViewers.Kind(rightPath)=="image");
                encoding.Visibility=onlyDiffs.Visibility=ignoreSpace.Visibility=ignoreCase.Visibility=images?Visibility.Collapsed:Visibility.Visible;
                if(images) {
                    await imageComparison.Load(leftPath,rightPath,token);
                    if(token.IsCancellationRequested)return;
                    LastResult=null;list.Visibility=Visibility.Collapsed;imageComparison.Visibility=Visibility.Visible;
                    leftDetail.Visibility=rightDetail.Visibility=Visibility.Collapsed;
                    prevButton.IsEnabled=nextButton.IsEnabled=false;
                    status.Text=L.Text("imageCompare.ready");return;
                }
                imageComparison.Visibility=Visibility.Collapsed;list.Visibility=Visibility.Visible;
                leftDetail.Visibility=rightDetail.Visibility=Visibility.Visible;prevButton.IsEnabled=nextButton.IsEnabled=true;
                var result = await Task.Run(() => FileDiffEngine.Compare(leftPath, rightPath, selectedMode, selectedEncoding, ignCase, ignSpace, token), token);
                if (token.IsCancellationRequested) return;
                LastResult = result;
                status.Text = result.Summary;
                ApplyFilter();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, L.Text("diff.title"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            finally { if (cancellation == cts) { cancellation = null; compareButton.IsEnabled = true; } cts.Dispose(); }
        }
    }

    public partial class MainWindow
    {
        internal void CompareFilesByContent()
        {
            string leftFile = "", rightFile = "";
            if (!active.IsRemote && !active.IsArchive)
            {
                var selected = active.Selected().Where(x => !x.IsDirectory).ToList();
                if (selected.Count >= 2)
                {
                    leftFile = selected[0].FullPath;
                    rightFile = selected[1].FullPath;
                }
                else if (selected.Count == 1)
                {
                    leftFile = selected[0].FullPath;
                    var other = active == left ? right : left;
                    if (!other.IsRemote && !other.IsArchive)
                    {
                        var otherSelected = other.Selected().Where(x => !x.IsDirectory).ToList();
                        string sameName = other.CurrentPath == null ? null : Path.Combine(other.CurrentPath, Path.GetFileName(leftFile));
                        if (sameName != null && File.Exists(sameName) && !String.Equals(sameName, leftFile, StringComparison.OrdinalIgnoreCase))
                            rightFile = sameName;
                        else if (otherSelected.Count >= 1)
                            rightFile = otherSelected[0].FullPath;
                    }
                }
            }
            new FileDiffWindow(this, leftFile, rightFile).Show();
        }
    }
}

