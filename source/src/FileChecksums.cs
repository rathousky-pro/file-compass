using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;

namespace FileCompass
{
    public enum ChecksumAlgorithm { Sha256, Md5, Crc32 }

    public sealed class ChecksumVerifyRow
    {
        public string RelativePath { get; set; }
        public string FullPath { get; set; }
        public string Algorithm { get; set; }
        public string ExpectedHash { get; set; }
        public string ActualHash { get; set; }
        public string Status { get; set; }
        public bool IsOk { get; set; }
    }

    internal static class FileChecksums
    {
        static readonly uint[] CrcTable = BuildCrcTable();

        static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                table[i] = c;
            }
            return table;
        }

        internal static bool IsChecksumFile(string path)
        {
            if (String.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".sha256" || ext == ".md5" || ext == ".sfv";
        }

        internal static string ExtensionFor(ChecksumAlgorithm algorithm)
        {
            return algorithm == ChecksumAlgorithm.Md5 ? ".md5" : algorithm == ChecksumAlgorithm.Crc32 ? ".sfv" : ".sha256";
        }

        internal static string NameFor(ChecksumAlgorithm algorithm)
        {
            return algorithm == ChecksumAlgorithm.Md5 ? "MD5" : algorithm == ChecksumAlgorithm.Crc32 ? "CRC32" : "SHA-256";
        }

        internal static ChecksumAlgorithm AlgorithmFromPath(string path)
        {
            string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext == ".md5" ? ChecksumAlgorithm.Md5 : ext == ".sfv" ? ChecksumAlgorithm.Crc32 : ChecksumAlgorithm.Sha256;
        }

        internal static string ComputeHash(string path, ChecksumAlgorithm algorithm, CancellationToken token)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buffer = new byte[65536];
                if (algorithm == ChecksumAlgorithm.Crc32)
                {
                    uint crc = 0xFFFFFFFFu;
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        for (int i = 0; i < read; i++)
                            crc = CrcTable[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
                    }
                    return (~crc).ToString("X8");
                }
                using (HashAlgorithm hasher = algorithm == ChecksumAlgorithm.Md5 ? (HashAlgorithm)MD5.Create() : SHA256.Create())
                {
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        hasher.TransformBlock(buffer, 0, read, null, 0);
                    }
                    hasher.TransformFinalBlock(buffer, 0, 0);
                    var sb = new StringBuilder(hasher.Hash.Length * 2);
                    foreach (byte b in hasher.Hash) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
        }

        internal static List<string> CollectFiles(IEnumerable<string> inputs, CancellationToken token)
        {
            var files = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in inputs)
            {
                token.ThrowIfCancellationRequested();
                string full = Path.GetFullPath(raw);
                var attr = File.GetAttributes(full);
                if ((attr & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("compare.pathContainsLinkPrefix") + full);
                if ((attr & FileAttributes.Directory) == 0)
                {
                    if (visited.Add(full)) files.Add(full);
                    continue;
                }
                var stack = new Stack<string>();
                stack.Push(full);
                while (stack.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    string dir = stack.Pop();
                    var entries = Directory.EnumerateFileSystemEntries(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
                    for (int i = entries.Length - 1; i >= 0; i--)
                    {
                        string child = entries[i];
                        var childAttr = File.GetAttributes(child);
                        if ((childAttr & FileAttributes.ReparsePoint) != 0) continue;
                        if ((childAttr & FileAttributes.Directory) != 0) stack.Push(child);
                        else if (visited.Add(child)) files.Add(child);
                    }
                }
            }
            return files;
        }

        static string RelativeName(string baseFolder, string filePath)
        {
            if (!String.IsNullOrEmpty(baseFolder))
            {
                string prefix = Path.GetFullPath(baseFolder).TrimEnd('\\') + "\\";
                string full = Path.GetFullPath(filePath);
                if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return full.Substring(prefix.Length).Replace('\\', '/');
            }
            return Path.GetFileName(filePath);
        }

        static void WriteAtomicText(string targetPath, IEnumerable<string> lines)
        {
            string full = Path.GetFullPath(targetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            string temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllLines(temp, lines, new UTF8Encoding(false));
                if (File.Exists(full)) File.Replace(temp, full, null);
                else File.Move(temp, full);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        internal static string[] CreateManifests(string[] inputs, string baseFolder, ChecksumAlgorithm algorithm, string outputFile, bool separateFiles, CancellationToken token, Action<string, double> progress = null)
        {
            var files = CollectFiles(inputs, token);
            if (files.Count == 0) throw new IOException(L.Text("checksum.noFilesFound"));
            string ext = ExtensionFor(algorithm);
            var created = new List<string>();
            if (separateFiles)
            {
                for (int i = 0; i < files.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    string file = files[i];
                    if (progress != null) progress(Path.GetFileName(file), (double)i / files.Count);
                    string hash = ComputeHash(file, algorithm, token);
                    string target = file + ext;
                    string line = algorithm == ChecksumAlgorithm.Crc32
                        ? Path.GetFileName(file) + " " + hash
                        : hash + " *" + Path.GetFileName(file);
                    WriteAtomicText(target, new[] { line });
                    created.Add(target);
                }
            }
            else
            {
                if (String.IsNullOrWhiteSpace(outputFile)) throw new IOException(L.Text("checksum.outputFileLabel"));
                string fullOutput = Path.GetFullPath(outputFile);
                string manifestDir = Path.GetDirectoryName(fullOutput) ?? baseFolder;
                var lines = new List<string>(files.Count + 1);
                if (algorithm == ChecksumAlgorithm.Crc32) lines.Add("; Generated by File Compass");
                for (int i = 0; i < files.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    string file = files[i];
                    if (String.Equals(file, fullOutput, StringComparison.OrdinalIgnoreCase)) continue;
                    if (progress != null) progress(Path.GetFileName(file), (double)i / files.Count);
                    string hash = ComputeHash(file, algorithm, token);
                    string rel = RelativeName(manifestDir, file);
                    lines.Add(algorithm == ChecksumAlgorithm.Crc32 ? rel + " " + hash : hash + " *" + rel);
                }
                WriteAtomicText(fullOutput, lines);
                created.Add(fullOutput);
            }
            return created.ToArray();
        }

        internal static List<ChecksumVerifyRow> VerifyManifests(string[] manifestPaths, CancellationToken token, Action<ChecksumVerifyRow> onRow = null, Action<string, double> progress = null)
        {
            var allRows = new List<ChecksumVerifyRow>();
            foreach (string manifest in manifestPaths)
            {
                token.ThrowIfCancellationRequested();
                string fullManifest = Path.GetFullPath(manifest);
                if (!File.Exists(fullManifest)) throw new IOException(L.Text("diff.fileMissingOrDirectory") + "\n" + fullManifest);
                string baseDir = Path.GetDirectoryName(fullManifest) ?? "";
                string basePrefix = Path.GetFullPath(baseDir).TrimEnd('\\') + "\\";
                ChecksumAlgorithm defaultAlg = AlgorithmFromPath(fullManifest);
                string[] lines = File.ReadAllLines(fullManifest, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    string raw = lines[i].Trim();
                    if (raw.Length == 0 || raw.StartsWith(";", StringComparison.Ordinal) || raw.StartsWith("#", StringComparison.Ordinal)) continue;
                    string relativePath; string expectedHash; ChecksumAlgorithm alg;
                    if (!TryParseLine(raw, defaultAlg, out relativePath, out expectedHash, out alg))
                    {
                        var bad = new ChecksumVerifyRow { RelativePath = raw, FullPath = fullManifest, Algorithm = NameFor(defaultAlg), ExpectedHash = "—", ActualHash = "—", Status = L.Text("checksum.statusInvalidLine"), IsOk = false };
                        allRows.Add(bad); if (onRow != null) onRow(bad);
                        continue;
                    }
                    if (progress != null) progress(relativePath, (double)i / Math.Max(1, lines.Length));
                    string candidate;
                    try { candidate = Path.GetFullPath(Path.Combine(baseDir, relativePath.Replace('/', '\\'))); }
                    catch (Exception)
                    {
                        var invalid = new ChecksumVerifyRow { RelativePath = relativePath, FullPath = "", Algorithm = NameFor(alg), ExpectedHash = expectedHash, ActualHash = "—", Status = L.Text("checksum.statusInvalidLine"), IsOk = false };
                        allRows.Add(invalid); if (onRow != null) onRow(invalid);
                        continue;
                    }
                    if (!candidate.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var unsafeRow = new ChecksumVerifyRow { RelativePath = relativePath, FullPath = candidate, Algorithm = NameFor(alg), ExpectedHash = expectedHash, ActualHash = "—", Status = L.Text("checksum.unsafeRelativePath") + relativePath, IsOk = false };
                        allRows.Add(unsafeRow); if (onRow != null) onRow(unsafeRow);
                        continue;
                    }
                    if (!File.Exists(candidate) || (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
                    {
                        var missing = new ChecksumVerifyRow { RelativePath = relativePath, FullPath = candidate, Algorithm = NameFor(alg), ExpectedHash = expectedHash, ActualHash = "—", Status = L.Text("checksum.statusMissing"), IsOk = false };
                        allRows.Add(missing); if (onRow != null) onRow(missing);
                        continue;
                    }
                    string actual = ComputeHash(candidate, alg, token);
                    bool ok = String.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
                    var row = new ChecksumVerifyRow
                    {
                        RelativePath = relativePath,
                        FullPath = candidate,
                        Algorithm = NameFor(alg),
                        ExpectedHash = expectedHash,
                        ActualHash = actual,
                        Status = ok ? L.Text("checksum.statusOk") : L.Text("checksum.statusMismatch"),
                        IsOk = ok
                    };
                    allRows.Add(row); if (onRow != null) onRow(row);
                }
            }
            return allRows;
        }

        static bool TryParseLine(string line, ChecksumAlgorithm defaultAlg, out string relativePath, out string expectedHash, out ChecksumAlgorithm algorithm)
        {
            relativePath = null; expectedHash = null; algorithm = defaultAlg;
            if (defaultAlg == ChecksumAlgorithm.Crc32)
            {
                int lastSpace = line.LastIndexOf(' ');
                if (lastSpace > 0 && line.Length - lastSpace - 1 == 8)
                {
                    string candidateCrc = line.Substring(lastSpace + 1).Trim();
                    if (IsHex(candidateCrc))
                    {
                        relativePath = line.Substring(0, lastSpace).Trim().Trim('"');
                        expectedHash = candidateCrc.ToUpperInvariant();
                        algorithm = ChecksumAlgorithm.Crc32;
                        return relativePath.Length > 0;
                    }
                }
            }
            int space = line.IndexOfAny(new[] { ' ', '\t' });
            if (space > 0)
            {
                string first = line.Substring(0, space).Trim();
                string rest = line.Substring(space + 1).Trim();
                if (rest.StartsWith("*", StringComparison.Ordinal)) rest = rest.Substring(1).Trim();
                if (IsHex(first) && (first.Length == 64 || first.Length == 32 || first.Length == 8) && rest.Length > 0)
                {
                    expectedHash = first.Length == 8 ? first.ToUpperInvariant() : first.ToLowerInvariant();
                    relativePath = rest.Trim('"');
                    algorithm = first.Length == 64 ? ChecksumAlgorithm.Sha256 : first.Length == 32 ? ChecksumAlgorithm.Md5 : ChecksumAlgorithm.Crc32;
                    return true;
                }
            }
            int trailingSpace = line.LastIndexOf(' ');
            if (trailingSpace > 0)
            {
                string tail = line.Substring(trailingSpace + 1).Trim();
                if (IsHex(tail) && tail.Length == 8)
                {
                    relativePath = line.Substring(0, trailingSpace).Trim().Trim('"');
                    expectedHash = tail.ToUpperInvariant();
                    algorithm = ChecksumAlgorithm.Crc32;
                    return relativePath.Length > 0;
                }
            }
            return false;
        }

        static bool IsHex(string value)
        {
            if (String.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            }
            return true;
        }
    }

    public sealed class ChecksumCreateWindow : Window
    {
        readonly ComboBox algorithm = new ComboBox { SelectedIndex = 0, Margin = new Thickness(0, 4, 0, 8) };
        readonly TextBox outputInput = new TextBox { Margin = new Thickness(0, 4, 0, 8) };
        readonly CheckBox separateFiles = new CheckBox { Content = L.Text("checksum.separateFilesOption"), Margin = new Thickness(0, 4, 0, 6) };
        readonly CheckBox verifyAfter = new CheckBox { Content = L.Text("checksum.verifyAfterCreate"), IsChecked = true, Margin = new Thickness(0, 4, 0, 8) };
        readonly ProgressBar progress = new ProgressBar { Height = 6, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 6) };
        readonly TextBlock status = CopyUi.Label(L.Text("state.ready"));
        readonly Button startButton;
        CancellationTokenSource cancellation;
        internal Task OperationTask { get; private set; }
        internal string[] CreatedFiles { get; private set; }

        public ChecksumCreateWindow(Window owner, string[] inputPaths, string baseFolder, string defaultOutput, Action refreshPanels)
        {
            CopyUi.Style(this, owner, L.Text("checksum.createTitle"), 580);
            var body = new StackPanel { Margin = new Thickness(12) }; Content = body;
            var section = FluentTheme.Section(body, L.Text("checksum.createTitle"));
            section.Children.Add(CopyUi.Label(L.Text("checksum.algorithmLabel")));
            foreach (string item in new[] { "SHA-256 (.sha256)", "MD5 (.md5)", "CRC32 (.sfv)" }) algorithm.Items.Add(item);
            section.Children.Add(algorithm);

            section.Children.Add(CopyUi.Label(L.Text("checksum.outputFileLabel")));
            outputInput.Text = defaultOutput ?? Path.Combine(baseFolder, "checksums.sha256");
            var outputRow = new DockPanel();
            var browse = CopyUi.Button(L.Text("common.ellipsis"), delegate
            {
                var dlg = new SaveFileDialog { FileName = outputInput.Text, Filter = "SHA-256 (*.sha256)|*.sha256|MD5 (*.md5)|*.md5|SFV (*.sfv)|*.sfv" };
                if (dlg.ShowDialog(this) == true) outputInput.Text = dlg.FileName;
            });
            browse.Width = 36; browse.MinWidth = 36; DockPanel.SetDock(browse, Dock.Right);
            outputRow.Children.Add(browse); outputRow.Children.Add(outputInput);
            section.Children.Add(outputRow);
            section.Children.Add(separateFiles);
            section.Children.Add(verifyAfter);
            body.Children.Add(progress);
            body.Children.Add(status);

            algorithm.SelectionChanged += delegate
            {
                ChecksumAlgorithm alg = algorithm.SelectedIndex == 1 ? ChecksumAlgorithm.Md5 : algorithm.SelectedIndex == 2 ? ChecksumAlgorithm.Crc32 : ChecksumAlgorithm.Sha256;
                string current = outputInput.Text.Trim();
                if (!String.IsNullOrEmpty(current)) outputInput.Text = Path.ChangeExtension(current, FileChecksums.ExtensionFor(alg));
            };
            separateFiles.Checked += delegate { outputInput.IsEnabled = false; browse.IsEnabled = false; };
            separateFiles.Unchecked += delegate { outputInput.IsEnabled = true; browse.IsEnabled = true; };

            var actions = FluentTheme.Actions(); body.Children.Add(actions);
            startButton = CopyUi.Button(L.Text("action.start"), async delegate
            {
                OperationTask = ExecuteCreate(owner, inputPaths, baseFolder, refreshPanels);
                await OperationTask;
            });
            startButton.IsDefault = true; actions.Children.Add(startButton);
            var close = CopyUi.Button(L.Text("action.cancel"), delegate { if (cancellation != null) cancellation.Cancel(); else Close(); });
            close.IsCancel = true; actions.Children.Add(close);
            ToolDock.Attach(this,actions,delegate{return new DockStatus{Title=Title,Icon="#",Description=status.Text,Busy=cancellation!=null,Fraction=progress.IsIndeterminate?(double?)null:progress.Value/Math.Max(1,progress.Maximum)};});
            Closing += delegate { if (cancellation != null) cancellation.Cancel(); };
        }

        internal async Task ExecuteCreate(Window owner, string[] inputPaths, string baseFolder, Action refreshPanels)
        {
            if (cancellation != null) return;
            var cts = new CancellationTokenSource(); cancellation = cts; var token = cts.Token;
            ChecksumAlgorithm alg = algorithm.SelectedIndex == 1 ? ChecksumAlgorithm.Md5 : algorithm.SelectedIndex == 2 ? ChecksumAlgorithm.Crc32 : ChecksumAlgorithm.Sha256;
            string outFile = outputInput.Text.Trim().Trim('"');
            bool separate = separateFiles.IsChecked == true;
            bool openVerify = verifyAfter.IsChecked == true;
            startButton.IsEnabled = false; progress.Visibility = Visibility.Visible; progress.Value = 0;
            var reporter = new Progress<Tuple<string, double>>(t => { status.Text = L.Text("checksum.creatingStatus") + t.Item1; progress.Value = t.Item2 * 100.0; });
            try
            {
                string[] created = await Task.Run(() => FileChecksums.CreateManifests(inputPaths, baseFolder, alg, outFile, separate, token, (name, frac) => ((IProgress<Tuple<string, double>>)reporter).Report(Tuple.Create(name, frac))), token);
                CreatedFiles = created;
                progress.Value = 100;
                status.Text = L.Format("checksum.createdSummaryFormat", created.Length, FileChecksums.NameFor(alg));
                if (refreshPanels != null) refreshPanels();
                if (openVerify && created.Length > 0 && IsVisible)
                {
                    var verifyWindow = new ChecksumVerifyWindow(owner ?? this, created, null);
                    Close();
                    verifyWindow.Show();
                }
            }
            catch (OperationCanceledException) { status.Text = L.Text("state.cancelled"); }
            catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, L.Text("checksum.createTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            finally { cancellation = null; cts.Dispose(); startButton.IsEnabled = true; progress.Visibility = Visibility.Collapsed; }
        }
    }

    public sealed class ChecksumVerifyWindow : Window
    {
        readonly ObservableCollection<ChecksumVerifyRow> visibleRows = new ObservableCollection<ChecksumVerifyRow>();
        readonly List<ChecksumVerifyRow> allRows = new List<ChecksumVerifyRow>();
        readonly CheckBox onlyErrors = new CheckBox { Content = L.Text("checksum.onlyErrors"), Margin = new Thickness(0, 4, 0, 6) };
        readonly ListView list = new ListView();
        readonly ProgressBar progress = new ProgressBar { Height = 5, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 4) };
        readonly TextBlock status = CopyUi.Label(L.Text("state.ready"));
        CancellationTokenSource cancellation;
        internal Task VerificationTask { get; private set; }
        internal List<ChecksumVerifyRow> AllRows { get { return allRows; } }

        public ChecksumVerifyWindow(Window owner, string[] manifestPaths, Action<string[]> markFailedInPanel)
        {
            CopyUi.Style(this, owner, L.Text("checksum.verifyTitle") + (manifestPaths.Length == 1 ? " — " + Path.GetFileName(manifestPaths[0]) : ""), 940);
            Height = 600; MinWidth = 760; MinHeight = 440; ResizeMode = ResizeMode.CanResize; SizeToContent = SizeToContent.Manual;
            var body = new DockPanel { Margin = new Thickness(12) }; Content = body;

            var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); body.Children.Add(top);
            top.Children.Add(onlyErrors);
            top.Children.Add(progress);

            var actions = FluentTheme.Actions(); DockPanel.SetDock(actions, Dock.Bottom); body.Children.Add(actions);
            actions.Children.Add(CopyUi.Button(L.Text("checksum.copyReport"), delegate
            {
                try { Clipboard.SetText(String.Join(Environment.NewLine, allRows.Select(r => r.Status + "\t" + r.Algorithm + "\t" + r.ExpectedHash + "\t" + r.ActualHash + "\t" + r.RelativePath))); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("clipboard.unavailableTitle")); }
            }));
            if (markFailedInPanel != null)
            {
                actions.Children.Add(CopyUi.Button(L.Text("checksum.markFailedInPanel"), delegate
                {
                    var failed = allRows.Where(x => !x.IsOk && !String.IsNullOrEmpty(x.FullPath)).Select(x => x.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    markFailedInPanel(failed);
                }));
            }
            var close = CopyUi.Button(L.Text("action.close"), delegate { if (cancellation != null) cancellation.Cancel(); else Close(); });
            close.IsCancel = true; close.IsDefault = true; actions.Children.Add(close);
            DockPanel.SetDock(status, Dock.Bottom); body.Children.Add(status);

            var view = new GridView();
            view.Columns.Add(new GridViewColumn { Header = L.Text("checksum.columnFile"), Width = 280, DisplayMemberBinding = new Binding("RelativePath") });
            view.Columns.Add(new GridViewColumn { Header = L.Text("checksum.columnAlgorithm"), Width = 75, DisplayMemberBinding = new Binding("Algorithm") });
            view.Columns.Add(new GridViewColumn { Header = L.Text("checksum.columnExpected"), Width = 195, DisplayMemberBinding = new Binding("ExpectedHash") });
            view.Columns.Add(new GridViewColumn { Header = L.Text("checksum.columnActual"), Width = 195, DisplayMemberBinding = new Binding("ActualHash") });
            view.Columns.Add(new GridViewColumn { Header = L.Text("checksum.columnStatus"), Width = 155, DisplayMemberBinding = new Binding("Status") });
            list.View = view;
            list.ItemsSource = visibleRows;
            var style = new Style(typeof(ListViewItem));
            style.Setters.Add(new Setter(Control.ForegroundProperty, FluentTheme.Text));
            var okTrigger = new DataTrigger { Binding = new Binding("IsOk"), Value = true };
            okTrigger.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ThemeBrush("#EEFBF0")));
            var failTrigger = new DataTrigger { Binding = new Binding("IsOk"), Value = false };
            failTrigger.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ThemeBrush("#FFE8E8")));
            style.Triggers.Add(okTrigger); style.Triggers.Add(failTrigger);
            list.ItemContainerStyle = style;
            body.Children.Add(list);

            onlyErrors.Checked += delegate { RefreshFilter(); };
            onlyErrors.Unchecked += delegate { RefreshFilter(); };
            Closing += delegate { if (cancellation != null) cancellation.Cancel(); };
            VerificationTask = RunVerification(manifestPaths);
            ToolDock.Attach(this,actions,delegate{return new DockStatus{Title=Title,Icon="#",Description=status.Text,Busy=cancellation!=null,Fraction=progress.IsIndeterminate?(double?)null:progress.Value/Math.Max(1,progress.Maximum)};});
        }

        void RefreshFilter()
        {
            visibleRows.Clear();
            foreach (var row in allRows)
                if (onlyErrors.IsChecked != true || !row.IsOk) visibleRows.Add(row);
        }

        void UpdateSummary()
        {
            int ok = allRows.Count(x => x.IsOk);
            int mismatch = allRows.Count(x => x.Status == L.Text("checksum.statusMismatch"));
            int missing = allRows.Count(x => x.Status == L.Text("checksum.statusMissing"));
            int invalid = allRows.Count - ok - mismatch - missing;
            status.Text = L.Format("checksum.verifySummaryFormat", allRows.Count, ok, mismatch, missing, invalid);
        }

        async Task RunVerification(string[] manifestPaths)
        {
            var cts = new CancellationTokenSource(); cancellation = cts; var token = cts.Token;
            progress.Visibility = Visibility.Visible; progress.Value = 0;
            var rowProgress = new Progress<ChecksumVerifyRow>(row =>
            {
                allRows.Add(row);
                if (onlyErrors.IsChecked != true || !row.IsOk) visibleRows.Add(row);
                UpdateSummary();
            });
            var textProgress = new Progress<Tuple<string, double>>(t =>
            {
                status.Text = L.Text("checksum.verifyingStatus") + t.Item1;
                progress.Value = t.Item2 * 100.0;
            });
            try
            {
                await Task.Run(() => FileChecksums.VerifyManifests(manifestPaths, token, r => ((IProgress<ChecksumVerifyRow>)rowProgress).Report(r), (name, frac) => ((IProgress<Tuple<string, double>>)textProgress).Report(Tuple.Create(name, frac))), token);
                UpdateSummary();
            }
            catch (OperationCanceledException) { status.Text = L.Text("state.cancelled"); }
            catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, L.Text("checksum.verifyTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            finally { cancellation = null; cts.Dispose(); progress.Visibility = Visibility.Collapsed; }
        }
    }

    public partial class MainWindow
    {
        internal void CreateChecksumsDialog()
        {
            if (active.IsRemote || active.IsArchive || active.CurrentPath == null)
            { MessageBox.Show(this, L.Text("projection.localRequired"), L.Text("checksum.createTitle")); return; }
            var selected = active.Selected();
            if (selected.Count == 0) { MessageBox.Show(this, L.Text("checksum.noFilesFound"), L.Text("checksum.createTitle")); return; }
            string baseFolder = active.CurrentPath;
            string baseName = selected.Count == 1 ? Path.GetFileNameWithoutExtension(selected[0].FullPath) : Path.GetFileName(baseFolder.TrimEnd('\\'));
            if (String.IsNullOrEmpty(baseName)) baseName = "checksums";
            string defaultOutput = Path.Combine(baseFolder, baseName + ".sha256");
            new ChecksumCreateWindow(this, selected.Select(x => x.FullPath).ToArray(), baseFolder, defaultOutput, delegate { left.Refresh(); right.Refresh(); }).Show();
        }

        internal void VerifyChecksumsDialog(string explicitFile = null)
        {
            if (explicitFile != null)
            {
                OpenChecksumVerification(new[] { explicitFile });
                return;
            }
            if (active.IsRemote || active.IsArchive)
            { MessageBox.Show(this, L.Text("projection.localRequired"), L.Text("checksum.verifyTitle")); return; }
            var manifests = active.Selected().Where(x => !x.IsDirectory && FileChecksums.IsChecksumFile(x.FullPath)).Select(x => x.FullPath).ToArray();
            if (manifests.Length == 0)
            {
                var picker = new OpenFileDialog { Title = L.Text("checksum.verifyTitle"), Filter = "Kontrolní součty (*.sha256;*.md5;*.sfv)|*.sha256;*.md5;*.sfv|Všechny soubory (*.*)|*.*", CheckFileExists = true, Multiselect = true, InitialDirectory = active.CurrentPath };
                if (picker.ShowDialog(this) != true) return;
                manifests = picker.FileNames;
            }
            OpenChecksumVerification(manifests);
        }

        void OpenChecksumVerification(string[] manifests)
        {
            var targetPanel = active;
            new ChecksumVerifyWindow(this, manifests, delegate(string[] failedPaths)
            {
                if (targetPanel == null || targetPanel.List.ItemsSource == null) return;
                var failedSet = new HashSet<string>(failedPaths, StringComparer.OrdinalIgnoreCase);
                foreach (Entry item in targetPanel.List.Items)
                    if (!item.IsParent) item.IsMarked = failedSet.Contains(item.FullPath);
            }).Show();
        }
    }
}
