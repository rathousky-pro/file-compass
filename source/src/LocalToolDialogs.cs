using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CommanderCZ
{
    internal sealed class ImageToolsDialog : Window
    {
        internal ImageToolOptions Options;
        internal ImageToolsDialog(Window owner, string[] paths, string folder)
        {
            CopyUi.Style(this,owner,L.Text("imageTools.title"),540);
            var body = new StackPanel { Margin = new Thickness(12) }; Content = body;
            body.Children.Add(CopyUi.Label(L.Format("imageTools.count",paths.Length)));
            body.Children.Add(CopyUi.Label(L.Text("imageTools.rotate")));
            var rotate = new ComboBox { ItemsSource = new[] { "0°","90°","180°","270°" }, SelectedIndex = 0 }; body.Children.Add(rotate);
            body.Children.Add(CopyUi.Label(L.Text("imageTools.resize")));
            var resize = new ComboBox { ItemsSource = new[] { L.Text("imageTools.noResize"), L.Text("imageTools.maxEdge"), L.Text("imageTools.percent") }, SelectedIndex = 1 }; body.Children.Add(resize);
            var amount = new TextBox { Text = "1920", Margin = new Thickness(0,5,0,5) }; body.Children.Add(amount);
            resize.SelectionChanged += delegate { amount.IsEnabled = resize.SelectedIndex != 0; amount.Text = resize.SelectedIndex == 2 ? "50" : "1920"; };
            body.Children.Add(CopyUi.Label(L.Text("imageTools.format")));
            var format = new ComboBox { ItemsSource = new[] { "PNG","JPEG","BMP","TIFF" }, SelectedIndex = 0 }; body.Children.Add(format);
            body.Children.Add(CopyUi.Label(L.Text("imageTools.quality")));
            var quality = new TextBox { Text = "90", IsEnabled = false }; body.Children.Add(quality); format.SelectionChanged += delegate { quality.IsEnabled = format.SelectedIndex == 1; };
            body.Children.Add(CopyUi.Label(L.Text("imageTools.output")));
            var destination = new TextBox { Text = folder }; body.Children.Add(destination);
            body.Children.Add(CopyUi.Button(L.Text("action.browse"),delegate { using (var picker = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = destination.Text }) if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) destination.Text = picker.SelectedPath; }));
            body.Children.Add(new TextBlock { Text = L.Text("imageTools.copiesHelp"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,8), Foreground = FluentTheme.Muted });
            var buttons = FluentTheme.Actions(); body.Children.Add(buttons);
            var run = CopyUi.Button(L.Text("imageTools.run"),delegate {
                try {
                    int value, jpeg; if (!Int32.TryParse(amount.Text,out value) || !Int32.TryParse(quality.Text,out jpeg)) throw new IOException(L.Text("imageTools.invalidOptions"));
                    var options = new ImageToolOptions { Rotation = rotate.SelectedIndex*90, ResizeMode = resize.SelectedIndex, MaxEdge = resize.SelectedIndex == 1 ? value : 1920, Percent = resize.SelectedIndex == 2 ? value : 50, Format = (string)format.SelectedItem, Quality = jpeg, OutputFolder = Path.GetFullPath(destination.Text) };
                    ImageTools.Plan(paths,options); Options = options; DialogResult = true;
                } catch (Exception ex) { MessageBox.Show(this,ex.Message,Title); }
            }); run.IsDefault = true; buttons.Children.Add(run);
            var cancel = CopyUi.Button(L.Text("action.cancel"),delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
        }
    }
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
        async void QuickImageTools()
        {
            if (active.IsRemote || active.IsArchive || active.IsVirtualBrowser) { MessageBox.Show(this,L.Text("diskUsage.localOnly"),ApplicationBrand.Name); return; }
            string[] paths = active.Selected().Where(x => !x.IsDirectory).Select(x => x.FullPath).ToArray();
            if (paths.Length == 0 || paths.Any(x => FileViewers.Kind(x) != "image")) { MessageBox.Show(this,L.Text("imageTools.selectImages"),ApplicationBrand.Name); return; }
            var dialog = new ImageToolsDialog(this,paths,active.CurrentPath); if (dialog.ShowDialog() != true) return;
            await ArchiveOperation(L.Text("imageTools.title"),(token,progress) => ImageTools.OnSta(() => ImageTools.Process(paths,dialog.Options,token,progress)));
            left.Refresh(); right.Refresh();
        }
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
