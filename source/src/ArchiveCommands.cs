using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CommanderCZ
{
    public partial class MainWindow
    {
        async Task<List<ArchiveDocument>> SelectedArchives()
        {
            if (active.IsArchive) return new List<ArchiveDocument> { active.Archive };
            if (active.IsRemote)
            { MessageBox.Show(this, L.Text("archive.downloadBeforeOpening"), L.Text("settings.archivesSection")); return null; }
            var entries = active.Selected().ToArray();
            if (entries.Length == 0) return null;
            if (entries.Any(x => x.IsDirectory || !ArchiveEngine.Recognized(x.FullPath)))
            { MessageBox.Show(this, L.Text("archive.selectArchives"), L.Text("settings.archivesSection")); return null; }
            var documents = new List<ArchiveDocument>();
            foreach (var entry in entries)
            {
                ArchiveDocument document = null;
                bool cancelled = false;
                if (!await ArchiveOperation(L.Text("archive.loadingPrefix") + entry.Name, delegate(CancellationToken token, Action<string> progress)
                {
                    string password = null;
                    while (true)
                    {
                        try { document = ArchiveEngine.Open(entry.FullPath, password, token); break; }
                        catch (ArchivePasswordException)
                        {
                            password = Dispatcher.Invoke(new Func<string>(delegate { return ArchivePasswordDialog.Ask(this, entry.FullPath); }));
                            if (password == null) { cancelled = true; return; }
                        }
                    }
                }) || cancelled) return null;
                documents.Add(document);
            }
            return documents;
        }
        async void TestArchives()
        {
            var documents = await SelectedArchives(); if (documents == null) return;
            foreach (var document in documents)
            {
                if (!ArchivePassword(document, document.Items.Select(x => x.Path))) return;
                if (!await ArchiveOperation(L.Text("archive.testTitlePrefix") + document.Display, delegate(CancellationToken token, Action<string> progress)
                { ArchiveEngine.Test(document, token, progress); })) return;
            }
            MessageBox.Show(this, L.Text("archive.testSuccessCount") + documents.Count,
                L.Text("archive.testTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        async void ExtractArchives()
        {
            if (active.IsArchive) { ArchiveTransfer(); return; }
            var targetPanel = active == left ? right : left;
            string initial = !targetPanel.IsArchive && !targetPanel.IsRemote ? targetPanel.CurrentPath : active.CurrentPath;
            var documents = await SelectedArchives(); if (documents == null) return;
            var dialog = new ArchiveExtractDialog(this, documents.Select(x => x.Display), initial);
            if (dialog.ShowDialog() != true) return;
            string destination = dialog.Destination;
            foreach (var document in documents)
            {
                if (!ArchivePassword(document, document.Items.Select(x => x.Path))) return;
                if (!await ArchiveOperation(L.Text("archive.extractingPrefix") + document.Display, delegate(CancellationToken token, Action<string> progress)
                {
                    using (var temp = new ArchiveTemp())
                    {
                        if (document.Items.Count == 0) { Directory.CreateDirectory(destination); return; }
                        ArchiveEngine.Extract(document, document.Items.Select(x => x.Path), temp.Path, token, progress);
                        using (var control = new CopyControl())
                        using (token.Register(control.Cancel))
                        {
                            var copied = new CopyEngine(new CopyOptions { Target = destination, Rule = dialog.Overwrite ? CopyRule.OverwriteAll : CopyRule.SkipAll }, control,
                                delegate { return new CopyDecision(CopyChoice.Cancel); },
                                delegate(CopyUpdate update) { progress(update.Phase + " · " + (update.TotalBytes > 0 ? 100.0 * update.TotalDone / update.TotalBytes : 0).ToString("F0") + "%"); },
                                delegate(CopyFailure failure) { return Dispatcher.Invoke(new Func<CopyErrorChoice>(delegate { var error = new CopyErrorDialog(this, failure); error.ShowDialog(); return error.Choice; })); })
                                .Run(Directory.GetFileSystemEntries(temp.Path));
                            if (copied.Cancelled) throw new OperationCanceledException();
                            if (copied.Errors.Count > 0) throw new IOException(String.Join("\n", copied.Errors));
                        }
                    }
                })) return;
                foreach (var panel in new[] { left, right })
                    if (!panel.IsArchive && !panel.IsRemote && panel.CurrentPath == destination) panel.Refresh();
            }
        }
    }
    public sealed class ArchiveExtractDialog : Window
    {
        readonly TextBox destination;
        readonly ComboBox rule;
        public string Destination { get; private set; }
        public bool Overwrite { get; private set; }
        public ArchiveExtractDialog(Window owner, IEnumerable<string> archives, string initial)
        {
            CopyUi.Style(this, owner, L.Text("archive.extractDialogTitle"), 540);
            var body = new StackPanel { Margin = new Thickness(10) }; Content = body;
            body.Children.Add(CopyUi.Label(String.Join("\n", archives.Take(3)) + (archives.Count() > 3 ? L.Text("common.moreLinesSuffix") : "")));
            body.Children.Add(CopyUi.Label(L.Text("archive.extractDestinationLabel")));
            var path = new DockPanel(); body.Children.Add(path);
            var browse = CopyUi.Button(L.Text("action.browse"), delegate
            {
                using (var picker = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = destination.Text, Description = L.Text("archive.extractDestinationTitle") })
                    if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) destination.Text = picker.SelectedPath;
            }); DockPanel.SetDock(browse, Dock.Right); path.Children.Add(browse);
            destination = new TextBox { Text = initial ?? "", Margin = new Thickness(2, 4, 2, 4), VerticalContentAlignment = VerticalAlignment.Center }; path.Children.Add(destination);
            rule = new ComboBox { Margin = new Thickness(2, 6, 2, 8), SelectedIndex = 0 };
            rule.Items.Add(L.Text("conflict.skipExistingFiles")); rule.Items.Add(L.Text("conflict.overwriteExistingFiles")); body.Children.Add(rule);
            var buttons = new UniformGrid { Columns = 2 }; body.Children.Add(buttons);
            var ok = CopyUi.Button(L.Text("action.extractArchive"), delegate
            {
                try
                {
                    if (!System.IO.Path.IsPathRooted(destination.Text)) throw new IOException(L.Text("folder.absoluteDestinationRequired"));
                    Destination = System.IO.Path.GetFullPath(destination.Text); Overwrite = rule.SelectedIndex == 1; DialogResult = true;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("folder.destinationTitle")); }
            }); ok.IsDefault = true; buttons.Children.Add(ok);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
        }
    }
}

