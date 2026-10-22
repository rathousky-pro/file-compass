using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    public enum PreviewMode { Auto = 0, Text = 1, Binary = 2, Hex = 3 }

    internal sealed class PreviewData
    {
        internal string Text, BinaryText, HexText, Status;
        internal BitmapSource Image;
        internal Markdig.Syntax.MarkdownDocument Markdown;
        internal long TotalLength, Offset;
        internal int ReadBytes;
        internal bool IsBinary;
        internal List<ArchiveItem> ArchiveItems;

        internal static PreviewData Load(string path, Encoding encoding, CancellationToken token, long offset = 0, PreviewMode mode = PreviewMode.Auto)
        {
            token.ThrowIfCancellationRequested();
            if (mode == PreviewMode.Auto && offset == 0 && !DocumentPreview.Supports(path) && ArchiveEngine.Recognized(path)) {
                var archive = ArchiveEngine.Open(path, null, token); token.ThrowIfCancellationRequested();
                return new PreviewData { ArchiveItems = archive.Items.Take(10000).ToList(), TotalLength = new FileInfo(path).Length, Status = L.Format("preview.archiveContents", archive.Items.Count) + (archive.Items.Count > 10000 ? " · " + L.Text("preview.archiveLimit") : "") };
            }
            string kind = FileViewers.Kind(path);
            if (mode == PreviewMode.Auto && offset <= 0 && kind == "image")
            {
                var image = new BitmapImage();
                using (var stream = File.OpenRead(path))
                {
                    image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 1400; image.StreamSource = stream; image.EndInit();
                }
                image.Freeze(); token.ThrowIfCancellationRequested();
                long imgLen = new FileInfo(path).Length;
                return new PreviewData { Image = image, TotalLength = imgLen, ReadBytes = (int)Math.Min(imgLen, Int32.MaxValue), Status = L.Text("preview.imageFitsPanel") };
            }
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long total = stream.Length;
                long safeOffset = total == 0 ? 0 : Math.Max(0, Math.Min(offset, total - 1));
                if (mode == PreviewMode.Hex) safeOffset = (safeOffset / 16) * 16;
                stream.Position = safeOffset;
                int pageCap = (mode == PreviewMode.Hex || mode == PreviewMode.Binary) ? 65536 : 131072;
                int length = (int)Math.Min(total - safeOffset, pageCap);
                var bytes = new byte[length];
                int read = 0;
                while (read < length)
                {
                    token.ThrowIfCancellationRequested();
                    int n = stream.Read(bytes, read, length - read);
                    if (n == 0) break;
                    read += n;
                }
                bool hasNulls = HasBinaryNulls(bytes, read);
                bool nonTextKind = kind != "text" && Path.HasExtension(path);
                bool isBin = hasNulls || nonTextKind;
                string hexText = FormatHexDump(bytes, read, safeOffset);
                string binText = FormatBinaryDump(bytes, read, 80);
                string decodedText;
                using (var reader = new StreamReader(new MemoryStream(bytes, 0, read), encoding, safeOffset == 0))
                {
                    decodedText = reader.ReadToEnd();
                    token.ThrowIfCancellationRequested();
                }
                string status;
                if (safeOffset == 0 && mode == PreviewMode.Auto && !isBin)
                {
                    status = total > read ? L.Text("preview.textPrefixLimitHelp") : L.Text("preview.readOnlyText");
                }
                else
                {
                    string modeLabel = mode == PreviewMode.Hex || (mode == PreviewMode.Auto && isBin) ? "Hex" : mode == PreviewMode.Binary ? "Bin" : "Text";
                    double pct = total == 0 ? 100.0 : (safeOffset + read) * 100.0 / total;
                    status = L.Format("preview.pageStatusFormat", modeLabel, safeOffset, safeOffset + read, total, pct);
                }
                return new PreviewData
                {
                    Text = decodedText,
                    BinaryText = binText,
                    HexText = hexText,
                    Markdown = safeOffset == 0 && !isBin && RichFilePreview.IsMarkdown(path) ? RichFilePreview.Parse(decodedText) : null,
                    TotalLength = total,
                    Offset = safeOffset,
                    ReadBytes = read,
                    IsBinary = isBin,
                    Status = status
                };
            }
        }

        static bool HasBinaryNulls(byte[] bytes, int count)
        {
            int check = Math.Min(count, 8192);
            if (check >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF))) return false;
            for (int i = 0; i < check; i++) if (bytes[i] == 0) return true;
            return false;
        }

        internal static string FormatHexDump(byte[] bytes, int length, long baseOffset)
        {
            if (length == 0) return "";
            var sb = new StringBuilder((length / 16 + 1) * 78);
            for (int row = 0; row < length; row += 16)
            {
                int rowCount = Math.Min(16, length - row);
                sb.Append((baseOffset + row).ToString("X10")).Append("  ");
                for (int i = 0; i < 16; i++)
                {
                    if (i == 8) sb.Append("- ");
                    if (i < rowCount) sb.Append(bytes[row + i].ToString("X2")).Append(' ');
                    else sb.Append("   ");
                }
                sb.Append(" | ");
                for (int i = 0; i < rowCount; i++)
                {
                    byte b = bytes[row + i];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                if (row + 16 < length) sb.AppendLine();
            }
            return sb.ToString();
        }

        internal static string FormatBinaryDump(byte[] bytes, int length, int width)
        {
            if (length == 0) return "";
            var sb = new StringBuilder(length + length / width + 16);
            for (int i = 0; i < length; i++)
            {
                if (i > 0 && i % width == 0) sb.AppendLine();
                byte b = bytes[i];
                sb.Append(b >= 32 && b < 127 ? (char)b : b >= 160 ? (char)b : '.');
            }
            return sb.ToString();
        }

        internal static bool TryParseHexPattern(string input, out byte[] pattern)
        {
            pattern = null;
            if (String.IsNullOrWhiteSpace(input)) return false;
            string cleaned = input.Trim();
            if (cleaned.StartsWith("hex:", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(4).Trim();
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(2).Trim();
            cleaned = cleaned.Replace(" ", "").Replace("-", "").Replace(":", "");
            if (cleaned.Length == 0 || cleaned.Length % 2 != 0) return false;
            var result = new byte[cleaned.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                byte b;
                if (!Byte.TryParse(cleaned.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)) return false;
                result[i] = b;
            }
            pattern = result;
            return true;
        }

        internal static long FindInFile(string path, byte[] pattern, long startOffset, CancellationToken token)
        {
            if (pattern == null || pattern.Length == 0 || pattern.Length > 65536) return -1;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long total = stream.Length;
                if (total < pattern.Length) return -1;
                long found = ScanRange(stream, pattern, Math.Max(0, startOffset), total, token);
                if (found >= 0) return found;
                if (startOffset > 0) return ScanRange(stream, pattern, 0, Math.Min(total, startOffset + pattern.Length - 1), token);
                return -1;
            }
        }

        static long ScanRange(FileStream stream, byte[] pattern, long from, long to, CancellationToken token)
        {
            if (from >= to || to - from < pattern.Length) return -1;
            stream.Position = from;
            byte[] buffer = new byte[262144];
            int keep = pattern.Length - 1;
            int carried = 0;
            long bufferStart = from;
            while (stream.Position < to)
            {
                token.ThrowIfCancellationRequested();
                int toRead = (int)Math.Min(buffer.Length - carried, to - stream.Position);
                int n = stream.Read(buffer, carried, toRead);
                if (n == 0) break;
                int valid = carried + n;
                int limit = valid - pattern.Length;
                for (int i = 0; i <= limit; i++)
                {
                    if (buffer[i] != pattern[0]) continue;
                    bool match = true;
                    for (int j = 1; j < pattern.Length; j++)
                        if (buffer[i + j] != pattern[j]) { match = false; break; }
                    if (match) return bufferStart + i;
                }
                if (valid <= keep) break;
                Array.Copy(buffer, valid - keep, buffer, 0, keep);
                bufferStart += valid - keep;
                carried = keep;
            }
            return -1;
        }
    }

    internal sealed partial class QuickPreviewPane : Border
    {
        readonly TextBlock title = CopyUi.Label(L.Text("preview.quickTitle")), status = CopyUi.Label("");
        readonly TextBox text = new TextBox { IsReadOnly = true, AcceptsReturn = true, AcceptsTab = true, FontFamily = new FontFamily("Consolas"), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        readonly Image picture = new Image { Stretch = Stretch.Uniform };
        readonly ComboBox encoding = new ComboBox { Width = 150, ItemsSource = new[] { "UTF-8 / podle BOM", "Windows-1250", "UTF-16 LE" }, SelectedIndex = 0 };
        readonly RichFilePreview rich = new RichFilePreview();
        readonly CheckBox formatted = new CheckBox { Content = L.Text("preview.formatted"), IsChecked = true, Margin = new Thickness(0, 0, 8, 5) };
        readonly CheckBox wrap = new CheckBox { Content = L.Text("preview.wrapText"), IsChecked = true, Margin = new Thickness(0, 0, 8, 5) };
        readonly RadioButton modeAuto = new RadioButton { Content = L.Text("preview.modeAuto"), IsChecked = true, Margin = new Thickness(0, 0, 8, 4), VerticalAlignment = VerticalAlignment.Center };
        readonly RadioButton modeText = new RadioButton { Content = L.Text("preview.modeText"), Margin = new Thickness(0, 0, 8, 4), VerticalAlignment = VerticalAlignment.Center };
        readonly RadioButton modeBinary = new RadioButton { Content = L.Text("preview.modeBinary"), Margin = new Thickness(0, 0, 8, 4), VerticalAlignment = VerticalAlignment.Center };
        readonly RadioButton modeHex = new RadioButton { Content = L.Text("preview.modeHex"), Margin = new Thickness(0, 0, 8, 4), VerticalAlignment = VerticalAlignment.Center };
        readonly WrapPanel navigationBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 5), Visibility = Visibility.Collapsed };
        readonly TextBox offsetInput = new TextBox { Width = 95, ToolTip = L.Text("preview.offsetHelp"), Margin = new Thickness(2, 0, 2, 0) };
        readonly TextBox find = new TextBox { ToolTip = L.Text("preview.findInLoadedText"), MinWidth = 40 };
        bool richAvailable, updatingMode;
        PreviewMode currentMode = PreviewMode.Auto;
        PreviewData lastData;
        long currentOffset;
        readonly Grid content = new Grid();
        readonly ListView archiveContents = new ListView { Visibility = Visibility.Collapsed };
        CancellationTokenSource cancellation;
        string current;
        int version;
        internal Task LoadingTask { get; private set; }
        RemoteConnection remoteTemplate;

        internal QuickPreviewPane(Action close, bool compact = false)
        {
            Background = CompactTheme.Surface; BorderBrush = CompactTheme.Line; BorderThickness = new Thickness(1);
            var body = new DockPanel { Margin = new Thickness(compact ? 4 : 8) }; Child = body;
            var heading = new DockPanel(); DockPanel.SetDock(heading, Dock.Top); body.Children.Add(heading);
            var exit = CopyUi.Button(L.Text("preview.closeShortcut"), close); DockPanel.SetDock(exit, Dock.Right); heading.Children.Add(exit);
            title.TextTrimming = TextTrimming.CharacterEllipsis; heading.Children.Add(title);
            if (compact) heading.Visibility = Visibility.Collapsed;
            DockPanel.SetDock(status, Dock.Bottom); body.Children.Add(status); InitializeDetails(body, compact);

            var tools = new DockPanel { Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(tools, Dock.Top); body.Children.Add(tools);
            DockPanel.SetDock(encoding, Dock.Right); tools.Children.Add(encoding);
            var next = CopyUi.Button(L.Text("action.find"), async delegate { await SearchNext(find.Text); });
            DockPanel.SetDock(next, Dock.Right); tools.Children.Add(next); tools.Children.Add(find);

            var modeRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) }; DockPanel.SetDock(modeRow, Dock.Top); body.Children.Add(modeRow);
            string groupName = "PreviewMode_" + Guid.NewGuid().ToString("N");
            foreach (var rb in new[] { modeAuto, modeText, modeBinary, modeHex }) { rb.GroupName = groupName; modeRow.Children.Add(rb); }
            modeRow.Children.Add(wrap);
            modeRow.Children.Add(formatted);
            if (compact) {
                tools.Visibility = Visibility.Collapsed;
                modeRow.Children.Clear();
                var modes = new ComboBox { Width = 115, MinWidth = 0, ItemsSource = new[] { L.Text("preview.modeAuto"), L.Text("preview.modeText"), L.Text("preview.modeBinary"), L.Text("preview.modeHex") }, SelectedIndex = 0, Margin = new Thickness(0, 0, 8, 0) };
                modes.SelectionChanged += delegate { if (modes.SelectedIndex >= 0) SetViewMode((PreviewMode)modes.SelectedIndex); };
                modeAuto.Checked += delegate { modes.SelectedIndex = 0; }; modeText.Checked += delegate { modes.SelectedIndex = 1; }; modeBinary.Checked += delegate { modes.SelectedIndex = 2; }; modeHex.Checked += delegate { modes.SelectedIndex = 3; };
                formatted.Content = L.Text("layout.formatted"); modeRow.Children.Add(modes); modeRow.Children.Add(formatted); modeRow.Children.Add(wrap);
                modeRow.Children.Add(CompactTheme.Button(L.Text("action.find"), L.Text("preview.findInLoadedText"), delegate { tools.Visibility = tools.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; if (tools.Visibility == Visibility.Visible) find.Focus(); }));
            }

            navigationBar.Children.Add(CompactTheme.Button(L.Text("preview.firstPage"), L.Text("preview.firstPage"), delegate { LoadingTask = NavigateOffset(0); }));
            navigationBar.Children.Add(CompactTheme.Button(L.Text("preview.previousPage"), L.Text("preview.previousPage"), delegate { LoadingTask = NavigateOffset(Math.Max(0, currentOffset - PageStep)); }));
            navigationBar.Children.Add(offsetInput);
            navigationBar.Children.Add(CompactTheme.Button(L.Text("preview.goToOffset"), L.Text("preview.offsetHelp"), delegate { JumpToEnteredOffset(); }));
            navigationBar.Children.Add(CompactTheme.Button(L.Text("preview.nextPage"), L.Text("preview.nextPage"), delegate { if (lastData != null && currentOffset + lastData.ReadBytes < lastData.TotalLength) LoadingTask = NavigateOffset(currentOffset + PageStep); }));
            navigationBar.Children.Add(CompactTheme.Button(L.Text("preview.lastPage"), L.Text("preview.lastPage"), delegate { if (lastData != null) LoadingTask = NavigateOffset(Math.Max(0, lastData.TotalLength - PageStep)); }));
            DockPanel.SetDock(navigationBar, Dock.Top); body.Children.Add(navigationBar);

            offsetInput.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) { JumpToEnteredOffset(); e.Handled = true; } };
            find.KeyDown += async delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; await SearchNext(find.Text); } };

            text.TextWrapping = TextWrapping.Wrap;
            wrap.Checked += delegate { if (!IsHexOrBinaryActive) text.TextWrapping = TextWrapping.Wrap; };
            wrap.Unchecked += delegate { text.TextWrapping = TextWrapping.NoWrap; };
            formatted.Checked += delegate { UpdateFormat(); };
            formatted.Unchecked += delegate { UpdateFormat(); };

            modeAuto.Checked += delegate { OnModeSelected(PreviewMode.Auto); };
            modeText.Checked += delegate { OnModeSelected(PreviewMode.Text); };
            modeBinary.Checked += delegate { OnModeSelected(PreviewMode.Binary); };
            modeHex.Checked += delegate { OnModeSelected(PreviewMode.Hex); };

            PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.OriginalSource == find || e.OriginalSource == offsetInput || Keyboard.Modifiers != ModifierKeys.None) return;
                if (e.Key == Key.D1 || e.Key == Key.NumPad1) { SetViewMode(PreviewMode.Text); e.Handled = true; }
                else if (e.Key == Key.D2 || e.Key == Key.NumPad2) { SetViewMode(PreviewMode.Binary); e.Handled = true; }
                else if (e.Key == Key.D3 || e.Key == Key.NumPad3) { SetViewMode(PreviewMode.Hex); e.Handled = true; }
                else if (e.Key == Key.D4 || e.Key == Key.NumPad4) { SetViewMode(PreviewMode.Auto); e.Handled = true; }
            };

            var archiveColumns = new GridView(); archiveColumns.Columns.Add(new GridViewColumn { Header = L.Text("column.name"), DisplayMemberBinding = new System.Windows.Data.Binding("Path"), Width = 380 }); archiveColumns.Columns.Add(new GridViewColumn { Header = L.Text("column.size"), DisplayMemberBinding = new System.Windows.Data.Binding("Bytes"), Width = 100 }); archiveColumns.Columns.Add(new GridViewColumn { Header = L.Text("column.date"), DisplayMemberBinding = new System.Windows.Data.Binding("Modified") { StringFormat = "dd.MM.yyyy HH:mm" }, Width = 150 }); archiveContents.View = archiveColumns;
            InitializeDocumentPreview(body); content.Children.Add(archiveContents); content.Children.Add(text); content.Children.Add(picture); content.Children.Add(rich); rich.Visibility = Visibility.Collapsed; body.Children.Add(content);
            encoding.SelectionChanged += delegate { if (current != null) LoadingTask = Load(current, currentOffset); };
        }

        int PageStep { get { return (currentMode == PreviewMode.Hex || currentMode == PreviewMode.Binary) ? 65536 : 131072; } }
        bool IsHexOrBinaryActive { get { return currentMode == PreviewMode.Hex || currentMode == PreviewMode.Binary || (currentMode == PreviewMode.Auto && !WantsDocument && lastData != null && lastData.IsBinary && lastData.Image == null); } }

        void OnModeSelected(PreviewMode selected)
        {
            if (updatingMode || currentMode == selected) return;
            currentMode = selected;
            if (current != null && remoteTemplate == null) LoadingTask = Load(current, currentOffset);
            else ApplyDataToControls();
        }

        internal void SetViewMode(PreviewMode mode)
        {
            updatingMode = true;
            currentMode = mode;
            modeAuto.IsChecked = mode == PreviewMode.Auto;
            modeText.IsChecked = mode == PreviewMode.Text;
            modeBinary.IsChecked = mode == PreviewMode.Binary;
            modeHex.IsChecked = mode == PreviewMode.Hex;
            updatingMode = false;
            if (current != null && remoteTemplate == null) LoadingTask = Load(current, currentOffset);
            else ApplyDataToControls();
        }

        internal Task NavigateOffset(long newOffset)
        {
            if (current == null || remoteTemplate != null) return Task.FromResult(false);
            currentOffset = Math.Max(0, newOffset);
            return LoadingTask = Load(current, currentOffset);
        }

        void JumpToEnteredOffset()
        {
            if (lastData == null || current == null || remoteTemplate != null) return;
            string raw = offsetInput.Text.Trim();
            if (String.IsNullOrEmpty(raw)) return;
            long target = 0;
            if (raw.EndsWith("%", StringComparison.Ordinal))
            {
                double pct;
                if (Double.TryParse(raw.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out pct) || Double.TryParse(raw.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out pct))
                    target = (long)(lastData.TotalLength * Math.Max(0.0, Math.Min(100.0, pct)) / 100.0);
            }
            else if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                Int64.TryParse(raw.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out target);
            }
            else
            {
                if (!Int64.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out target))
                    Int64.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out target);
            }
            LoadingTask = NavigateOffset(target);
        }

        internal async Task<bool> SearchNext(string query)
        {
            if (String.IsNullOrEmpty(query)) return false;
            formatted.IsChecked = false;
            string activeText = text.Text ?? "";
            int start = Math.Min(activeText.Length, text.SelectionStart + text.SelectionLength);
            int index = activeText.IndexOf(query, start, StringComparison.CurrentCultureIgnoreCase);
            if (index >= 0)
            {
                text.Focus(); text.Select(index, query.Length); text.ScrollToLine(text.GetLineIndexFromCharacterIndex(index));
                return true;
            }
            if (current != null && remoteTemplate == null && lastData != null && (lastData.TotalLength > lastData.ReadBytes || IsHexOrBinaryActive))
            {
                Encoding enc = SelectedEncoding;
                byte[] pattern = null;
                bool hexQuery = (currentMode == PreviewMode.Hex || query.StartsWith("hex:", StringComparison.OrdinalIgnoreCase) || query.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) && PreviewData.TryParseHexPattern(query, out pattern);
                if (!hexQuery) pattern = enc.GetBytes(query);
                long searchStart = currentOffset + Math.Max(1, text.SelectionStart > 0 ? lastData.ReadBytes : 1);
                status.Text = L.Text("preview.searchingFileStatus");
                string path = current;
                var token = cancellation.Token;
                long foundOffset = await Task.Run(() => PreviewData.FindInFile(path, pattern, searchStart, token), token);
                if (foundOffset >= 0)
                {
                    long pageOffset = currentMode == PreviewMode.Hex ? (foundOffset / 16) * 16 : foundOffset;
                    await NavigateOffset(pageOffset);
                    int localIdx = (text.Text ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
                    if (localIdx >= 0) { text.Focus(); text.Select(localIdx, query.Length); text.ScrollToLine(text.GetLineIndexFromCharacterIndex(localIdx)); }
                    return true;
                }
            }
            index = activeText.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
            if (index >= 0)
            {
                text.Focus(); text.Select(index, query.Length); text.ScrollToLine(text.GetLineIndexFromCharacterIndex(index));
                return true;
            }
            status.Text = L.Text("preview.textNotFound");
            return false;
        }

        Encoding SelectedEncoding
        {
            get { return encoding.SelectedIndex == 1 ? Encoding.GetEncoding(1250) : encoding.SelectedIndex == 2 ? Encoding.Unicode : Encoding.UTF8; }
        }

        void UpdateFormat()
        {
            UpdateHtmlControls();
            bool archive = lastData != null && lastData.ArchiveItems != null;
            archiveContents.Visibility = archive ? Visibility.Visible : Visibility.Collapsed;
            if (archive) { archiveContents.ItemsSource = lastData.ArchiveItems.Select(x => new { Path = x.Path, Bytes = x.Directory ? "<DIR>" : x.Bytes.ToString(), Modified = x.Modified }).ToArray(); text.Visibility = picture.Visibility = rich.Visibility = Visibility.Collapsed; navigationBar.Visibility = documentLoadingLabel.Visibility = Visibility.Collapsed; status.Text = lastData.Status; return; }
            documentLoadingLabel.Visibility=WaitingForDocument?Visibility.Visible:Visibility.Collapsed;
            if(WaitingForDocument) {
                if(documentPreview!=null)documentPreview.Visibility=Visibility.Hidden;
                text.Visibility=picture.Visibility=rich.Visibility=Visibility.Collapsed;
                navigationBar.Visibility=Visibility.Collapsed;status.Text=L.Text("preview.loadingStatus");return;
            }
            if(documentPreview!=null)documentPreview.Visibility=ShowingDocument?Visibility.Visible:Visibility.Hidden;
            bool showRich = !ShowingDocument && currentMode == PreviewMode.Auto && richAvailable && (formatted.IsChecked == true || HtmlCodeSelected) && !IsHexOrBinaryActive;
            rich.Visibility = showRich ? Visibility.Visible : Visibility.Collapsed;
            bool showImage = currentMode == PreviewMode.Auto && picture.Source != null;
            picture.Visibility = showImage ? Visibility.Visible : Visibility.Collapsed;
            text.Visibility = !ShowingDocument && !showImage && !showRich ? Visibility.Visible : Visibility.Collapsed;
            if(!ShowingDocument)ApplyDocumentExcerpt();
            text.TextWrapping = IsHexOrBinaryActive ? TextWrapping.NoWrap : (wrap.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap);
            if (ShowingDocument) status.Text = L.Text("preview.documentReady");
            else if (lastData != null && status.Text == L.Text("preview.loadingStatus")) status.Text = lastData.Status;
        }

        void ApplyDataToControls()
        {
            if (lastData == null) return;
            picture.Source = currentMode == PreviewMode.Auto ? lastData.Image : null;
            bool useHex = currentMode == PreviewMode.Hex || (currentMode == PreviewMode.Auto && !WantsDocument && lastData.IsBinary && lastData.Image == null);
            bool useBin = currentMode == PreviewMode.Binary;
            text.Text = useHex ? (lastData.HexText ?? "") : useBin ? (lastData.BinaryText ?? "") : (lastData.Text ?? "");
            if(WantsDocument&&!DocumentPreview.IsHtml(current))text.Text=L.Text("details.noTextPreview");
            richAvailable = currentMode == PreviewMode.Auto && !useHex && lastData.Text != null && currentOffset == 0 && current != null && RichFilePreview.Supports(current);
            if (richAvailable) rich.Show(current, lastData.Text, lastData.Markdown);
            formatted.IsEnabled = richAvailable || documentReady;
            bool showNav = remoteTemplate == null && lastData.Image == null && (lastData.TotalLength > lastData.ReadBytes || useHex || useBin || currentOffset > 0);
            navigationBar.Visibility = showNav ? Visibility.Visible : Visibility.Collapsed;
            offsetInput.Text = useHex ? "0x" + lastData.Offset.ToString("X") : lastData.Offset.ToString();
            UpdateFormat();
            status.Text = ShowingDocument ? L.Text("preview.documentReady") : WaitingForDocument?L.Text("preview.loadingStatus"):lastData.Status; ApplyDocumentExcerpt();
        }

        internal void ShowMessage(string message)
        {
            Stop(); if (remoteTemplate != null) remoteTemplate.Dispose(); remoteTemplate = null;
            current = null; lastData = null; currentOffset = 0; text.Text = ""; picture.Source = null; rich.Clear(); richAvailable = false; navigationBar.Visibility = Visibility.Collapsed;
            UpdateFormat(); status.Text = message;
        }

        internal void ShowFile(string path)
        {
            if (remoteTemplate != null) remoteTemplate.Dispose(); remoteTemplate = null;
            currentOffset = 0;
            LoadingTask = Load(path, 0);
        }

        internal void ShowRemoteFile(string path, RemoteConnection remote)
        {
            if(DocumentPreview.Supports(path)||FileViewers.Kind(path)=="system"||FileViewers.Kind(path)=="image") {ShowDownloadedPreview(path,remote);return;}
            if (remoteTemplate != null) remoteTemplate.Dispose(); remoteTemplate = remote.TransferConnection();
            currentOffset = 0;
            LoadingTask = Load(path, 0);
        }

        async Task Load(string path, long offset)
        {
            Stop(); current = path; currentOffset = offset; lastData=null;
            documentLoading=WantsDocument;documentDetailsLoading=WantsDocument;
            var token = cancellation.Token; int request = version;
            if (remoteTemplate == null) MetadataLoadingTask = LoadDetails(path, token, request);
            DocumentLoadingTask = LoadDocumentPreview(path, token, request);
            title.Text = Path.GetFileName(path); title.ToolTip = path;
            text.Text = ""; picture.Source = null; rich.Clear(); richAvailable = false;
            UpdateFormat(); status.Text = L.Text("preview.loadingStatus");
            var selectedEncoding = SelectedEncoding;
            var modeSnapshot = currentMode;
            var remote = remoteTemplate == null ? null : remoteTemplate.TransferConnection();
            try
            {
                var data = await Task.Run(delegate
                {
                    using (remote)
                    {
                        return remote == null
                            ? PreviewData.Load(path, selectedEncoding, token, offset, modeSnapshot)
                            : RemotePreview.Load(remote, path, selectedEncoding, token);
                    }
                });
                if (request != version) return;
                lastData = data;
                currentOffset = data.Offset;
                ApplyDataToControls();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (request == version) status.Text = L.Text("preview.loadFailedPrefix") + ex.Message; }
        }

        internal void Stop() { ClearDocumentPreview(); ClearDetails(); version++; if (cancellation != null) { cancellation.Cancel(); cancellation.Dispose(); } cancellation = new CancellationTokenSource(); ClearPreparedPreview(); }
        internal void DisposePreview() { Stop(); cancellation.Dispose(); cancellation = null; if (remoteTemplate != null) remoteTemplate.Dispose(); remoteTemplate = null; }
    }

    public partial class MainWindow
    {
        internal static Window ShowRichPreviewWindow(Window owner, string path, PreviewMode initialMode = PreviewMode.Auto)
        {
            var window = new Window(); CopyUi.Style(window, owner, Path.GetFileName(path), 1000); window.Height = 720; window.SizeToContent = SizeToContent.Manual; window.ResizeMode = ResizeMode.CanResize;
            var preview = new QuickPreviewPane(window.Close);
            if (initialMode != PreviewMode.Auto) preview.SetViewMode(initialMode);
            window.Content = preview; window.Closed += delegate { preview.DisposePreview(); }; preview.ShowFile(path); window.Show();
            return window;
        }
        void OpenRichPreview()
        {
            ToggleQuickPreview();
        }
        QuickPreviewPane quickPreview; FilePanel previewSource, previewTarget;
        bool FilePreviewOpen { get { return panelLayout == "Single" ? active != null && active.DetailExpanded : quickPreview != null; } }
        void InitializeQuickPreview()
        {
            var first = left; var second = right;
            first.DetailExpandedChanged += UpdateViewToolbarControls;
            second.DetailExpandedChanged += UpdateViewToolbarControls;
            first.List.SelectionChanged += delegate { if (previewSource == first) UpdateQuickPreview(); };
            second.List.SelectionChanged += delegate { if (previewSource == second) UpdateQuickPreview(); };
            first.PathChanged += delegate { if (previewTarget == first) CloseQuickPreview(); else if (previewSource == first) UpdateQuickPreview(); };
            second.PathChanged += delegate { if (previewTarget == second) CloseQuickPreview(); else if (previewSource == second) UpdateQuickPreview(); };
            Closed += delegate { CloseQuickPreview(); };
        }
        internal void ToggleQuickPreview()
        {
            if (panelLayout == "Single") { active.SetDetailExpanded(!active.DetailExpanded); return; }
            if (quickPreview != null) { CloseQuickPreview(); return; }
            OpenQuickPreview();
        }
        void OpenQuickPreview()
        {
            previewSource = active; previewTarget = active == left ? right : left;
            quickPreview = new QuickPreviewPane(CloseQuickPreview);
            Grid.SetRow(quickPreview, Grid.GetRow(previewTarget)); Grid.SetColumn(quickPreview, Grid.GetColumn(previewTarget));
            panelSplit.Children.Add(quickPreview);
            previewTarget.List.Visibility = previewTarget.Visibility = Visibility.Collapsed;
            UpdateQuickPreview(); UpdateViewToolbarControls();
        }
        void UpdateQuickPreview()
        {
            if (quickPreview == null) return;
            var entry = previewSource.List.SelectedItem as Entry;
            if (entry == null || entry.IsDirectory) quickPreview.ShowMessage(L.Text("preview.selectOtherPanelFile"));
            else if (previewSource.IsRemote && (previewSource.Remote.Profile.Protocol == NetworkProtocol.FTP || previewSource.Remote.Profile.Protocol == NetworkProtocol.SFTP)) quickPreview.ShowRemoteFile(entry.FullPath, previewSource.Remote);
            else if (previewSource.IsArchive) quickPreview.ShowArchiveFile(previewSource.Archive,entry.FullPath);
            else if (previewSource.IsRemote) quickPreview.ShowMessage(L.Text("preview.scpArchiveUseF3"));
            else quickPreview.ShowFile(entry.FullPath);
        }
        internal void CloseQuickPreview()
        {
            if (quickPreview == null) return;
            panelSplit.Children.Remove(quickPreview);
            previewTarget.List.Visibility = previewTarget.Visibility = Visibility.Visible;
            quickPreview.DisposePreview(); quickPreview = null; previewSource = previewTarget = null;
            UpdateViewToolbarControls();
            if (active != null) active.List.Focus();
        }
    }
}


