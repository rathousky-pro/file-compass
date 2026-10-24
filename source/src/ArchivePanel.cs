using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FileCompass
{
    public partial class FilePanel
    {
        public ArchiveDocument Archive { get; private set; }
        public bool IsArchive { get { return Archive != null; } }
        public string ArchivePrefix { get; private set; }
        string archiveReturnPath;
        readonly Stack<Tuple<ArchiveDocument, string>> archiveParents = new Stack<Tuple<ArchiveDocument, string>>();
        readonly List<ArchiveTemp> archiveTemps = new List<ArchiveTemp>();
        public event Action<FilePanel, Entry, bool, bool> ArchiveFileRequested;
        public async Task OpenArchive(string file, ArchiveDocument parent = null, string parentMember = null, ArchiveTemp ownedTemp = null)
        {
            if (IsLoading) return;
            try { ArchiveEngine.CheckNesting(parent); }
            catch (IOException ex) { if (ownedTemp != null) ownedTemp.Dispose(); MessageBox.Show(Window.GetWindow(this), ex.Message, L.Text("archive.shortTitle")); return; }
            CancelNavigation(); Loading(true);
            try
            {
                ArchiveDocument document = null; string password = null;
                while (document == null)
                {
                    Exception failure = null;
                    try { document = await Task.Run(delegate { return ArchiveEngine.Open(file, password); }); }
                    catch (Exception ex) { failure = ex; }
                    if (failure != null)
                    {
                        if (!(failure is ArchivePasswordException)) throw failure;
                        password = ArchivePasswordDialog.Ask(Window.GetWindow(this), file); if (password == null) return;
                    }
                }
                document.Parent = parent; document.ParentMember = parentMember;
                if (Archive != null && parent != null) archiveParents.Push(Tuple.Create(Archive, ArchivePrefix));
                else { archiveReturnPath = CurrentPath; archiveParents.Clear(); }
                if (Remote != null) { RememberRemote(); Remote = null; }
                Archive = document; ArchivePrefix = "";
                history.Clear(); forwardHistory.Clear();
                if (ownedTemp != null) { archiveTemps.Add(ownedTemp); ownedTemp = null; }
                ApplyArchiveRows();
            }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, L.Text("archive.openFailed"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            finally { Loading(false); if (ownedTemp != null) ownedTemp.Dispose(); }
        }
        internal void ApplyArchiveRows()
        {
            while (ArchivePrefix.Length > 0 && !Archive.Items.Any(x => x.Directory && x.Path.Equals(ArchivePrefix, StringComparison.OrdinalIgnoreCase)))
                ArchivePrefix = ArchivePrefix.Contains("/") ? ArchivePrefix.Substring(0, ArchivePrefix.LastIndexOf('/')) : "";
            var marked = new HashSet<string>(List.Items.Cast<Entry>().Where(x => x.IsMarked).Select(x => x.FullPath), StringComparer.OrdinalIgnoreCase);
            string prefix = ArchivePrefix.Length == 0 ? "" : ArchivePrefix + "/";
            var rows = new List<Entry> { new Entry { Name = "[..]", FullPath = ArchivePrefix.Length == 0 ? "archive-exit:" : "/" + ArchivePrefix.Substring(0, Math.Max(0, ArchivePrefix.LastIndexOf('/'))), IsDirectory = true, IsParent = true, Icon = DesktopIcons.FileIcon("", true) } };
            foreach (var item in Archive.Items.Where(x => x.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && x.Path.Length > prefix.Length && !x.Path.Substring(prefix.Length).Contains("/")))
            {
                string name = item.Path.Substring(prefix.Length), extension = item.Directory ? "" : Path.GetExtension(name);
                rows.Add(new Entry { Name = item.Directory ? "[" + name + "]" : extension.Length == 0 ? name : Path.GetFileNameWithoutExtension(name), Extension = extension.TrimStart('.'),
                    FullPath = "/" + item.Path, IsDirectory = item.Directory, Bytes = item.Bytes, ModifiedTime = item.Modified,
                    IsHidden = name.StartsWith(".", StringComparison.Ordinal),
                    Modified = item.Modified == DateTime.MinValue ? "" : item.Modified.ToString("dd.MM.yyyy HH:mm"), Attributes = item.Encrypted ? L.Text("archive.encryptedShort") : "----",
                    IsMarked = marked.Contains("/" + item.Path), Icon = DesktopIcons.FileIcon(name, item.Directory) });
            }
            synchronizingSelection = true;
            try { CurrentPath = "/" + ArchivePrefix; ShowPath(); List.ItemsSource = rows; Sort(); List.SelectedIndex = 0; }
            finally { synchronizingSelection = false; }
            SyncWindowsSelection();
            capacity.Text = Path.GetFileName(Archive.FilePath) + " · " + Archive.Type + " · " + (ArchiveEngine.Writable(Archive) ? L.Text("archive.shortTitle") : L.Text("archive.readOnlyStatus")); capacity.ToolTip = Archive.Display;
            foreach (var button in driveButtons.Values) button.Background = CompactTheme.WhiteSurface;
            UpdateStatus(); UpdateConnectionButtons(); if (PathChanged != null) PathChanged(this, EventArgs.Empty);
        }
        internal void NavigateArchive(string path, bool remember)
        {
            if (path == "archive-exit:") { ExitArchive(); return; }
            string member = path.Trim('/');
            if (member.Length > 0 && !Archive.Items.Any(x => x.Directory && x.Path.Equals(member, StringComparison.OrdinalIgnoreCase))) throw new IOException(L.Text("archive.folderNotFound"));
            if (remember && CurrentPath != path) { history.Push(CurrentPath); forwardHistory.Clear(); }
            string previous = ArchivePrefix;
            ArchivePrefix = member; ApplyArchiveRows();
            if (!String.IsNullOrEmpty(previous) && (previous.Contains("/") ? previous.Substring(0, previous.LastIndexOf('/')) : "") == member) {
                var returned = List.Items.Cast<Entry>().FirstOrDefault(x => !x.IsParent && x.FullPath == "/" + previous);
                if (returned != null) { List.SelectedItem = returned; List.ScrollIntoView(returned); }
            }
        }
        void ExitArchive()
        {
            if (archiveParents.Count > 0)
            { var parent = archiveParents.Pop(); Archive = parent.Item1; ArchivePrefix = parent.Item2; history.Clear(); forwardHistory.Clear(); ApplyArchiveRows(); }
            else { string back = archiveReturnPath; Archive = null; ArchivePrefix = null; history.Clear(); forwardHistory.Clear(); Navigate(back, false); }
        }
        internal async void RefreshArchive()
        {
            var document = Archive; Loading(true);
            try { await Task.Run(delegate { ArchiveEngine.Reload(document, CancellationToken.None); }); if (Archive == document) ApplyArchiveRows(); }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, L.Text("archive.refreshFailed")); }
            finally { Loading(false); }
        }
        internal void CloseArchiveContext() { Archive = null; ArchivePrefix = null; archiveParents.Clear(); }
        internal void DisposeArchives()
        { foreach (var temp in archiveTemps) { try { temp.Dispose(); } catch (IOException) { } catch (UnauthorizedAccessException) { } } archiveTemps.Clear(); }
    }
    public sealed class ArchivePasswordDialog : Window
    {
        public static string Ask(Window owner, string file)
        {
            var dialog = new ArchivePasswordDialog(); CopyUi.Style(dialog, owner, L.Text("archive.passwordTitle"), 440);
            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) }; dialog.Content = panel;
            panel.Children.Add(CopyUi.Label(Path.GetFileName(file)));
            var input = new System.Windows.Controls.PasswordBox { Margin = new Thickness(3), Height = 24 }; panel.Children.Add(input);
            var buttons = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 }; panel.Children.Add(buttons);
            var ok = CopyUi.Button("OK", delegate { dialog.DialogResult = true; }); ok.IsDefault = true; buttons.Children.Add(ok);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { dialog.DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            dialog.Loaded += delegate { input.Focus(); }; return dialog.ShowDialog() == true ? input.Password : null;
        }
    }
}

