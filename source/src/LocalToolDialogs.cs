using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    internal sealed class FilePartsDialog : Window
    {
        internal string Destination; internal long Bytes;
        internal FilePartsDialog(Window owner, bool combine, string input, string initial)
        {
            CopyUi.Style(this,owner,L.Text(combine ? "parts.combine" : "parts.split"),570);
            var body = new StackPanel { Margin = new Thickness(12) }; Content = body; body.Children.Add(CopyUi.Label(input));
            body.Children.Add(CopyUi.Label(L.Text(combine ? "parts.combinedOutput" : "parts.outputBase")));
            var output = new TextBox { Text = initial }; body.Children.Add(output);
            body.Children.Add(CopyUi.Label(L.Text("parts.sizeLabel")));
            var size = new ComboBox { IsEditable = true, ItemsSource = new[] { "100 MB","700 MB","4 GB" }, Text = "100 MB", IsEnabled = !combine }; body.Children.Add(size);
            body.Children.Add(new TextBlock { Text = L.Text(combine ? "parts.combineHelp" : "parts.splitHelp"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,8) });
            var buttons = FluentTheme.Actions(); body.Children.Add(buttons);
            var ok = CopyUi.Button(L.Text(combine ? "parts.combine" : "parts.split"),delegate {
                try {
                    Destination = Path.GetFullPath(output.Text);
                    if (!Directory.Exists(Path.GetDirectoryName(Destination)) || (combine && File.Exists(Destination)) || Directory.Exists(Destination)) throw new IOException(L.Text("parts.outputExists"));
                    if (!combine) Bytes = FileParts.ParseSize(size.Text);
                    DialogResult = true;
                } catch (Exception ex) { MessageBox.Show(this,ex.Message,Title); }
            }); ok.IsDefault = true; buttons.Children.Add(ok);
            var cancel = CopyUi.Button(L.Text("action.cancel"),delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
        }
    }
    public partial class MainWindow
    {
        async void SplitSelectedFile()
        {
            if (active.IsRemote || active.IsArchive || active.IsVirtualBrowser) { MessageBox.Show(this,L.Text("diskUsage.localOnly"),ApplicationBrand.Name); return; }
            var selected = active.Selected(); if (selected.Count != 1 || selected[0].IsDirectory) { MessageBox.Show(this,L.Text("parts.selectFile"),ApplicationBrand.Name); return; }
            string input = selected[0].FullPath; var other = active == left ? right : left;
            string folder = other.IsRemote || other.IsArchive || other.IsVirtualBrowser ? active.CurrentPath : other.CurrentPath;
            var dialog = new FilePartsDialog(this,false,input,Path.Combine(folder,Path.GetFileName(input))); if (dialog.ShowDialog() != true) return;
            await ArchiveOperation(L.Text("parts.split"),(token,progress) => FileParts.Split(input,dialog.Destination,dialog.Bytes,token,progress));
            left.Refresh(); right.Refresh();
        }
        async void CombineSelectedFiles()
        {
            if (active.IsRemote || active.IsArchive || active.IsVirtualBrowser) { MessageBox.Show(this,L.Text("diskUsage.localOnly"),ApplicationBrand.Name); return; }
            var entry = active.List.SelectedItem as Entry; if (entry == null || entry.IsDirectory) return;
            try {
                string input = entry.FullPath, baseName = FileParts.BaseName(input); FileParts.Discover(input);
                var other = active == left ? right : left; string folder = other.IsRemote || other.IsArchive || other.IsVirtualBrowser ? active.CurrentPath : other.CurrentPath;
                var dialog = new FilePartsDialog(this,true,input,Path.Combine(folder,Path.GetFileName(baseName))); if (dialog.ShowDialog() != true) return;
                await ArchiveOperation(L.Text("parts.combine"),(token,progress) => FileParts.Combine(input,dialog.Destination,token,progress));
                left.Refresh(); right.Refresh();
            } catch (Exception ex) { MessageBox.Show(this,ex.Message,L.Text("parts.combine")); }
        }
    }
}
