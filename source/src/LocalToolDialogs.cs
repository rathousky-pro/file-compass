using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    internal sealed class ImageToolsDialog : Window
    {
        internal ImageToolOptions Options;
        internal readonly ComboBox ResizeChoice = new ComboBox();
        internal readonly TextBox WidthInput=new TextBox{Text="1920"},HeightInput=new TextBox{Text="1080"},AmountInput=new TextBox{Text="1920",Margin=new Thickness(0,5,0,5)};
        readonly ComboBox rotate=new ComboBox{ItemsSource=new[]{"0°","90°","180°","270°"},SelectedIndex=0},format=new ComboBox{ItemsSource=new[]{"PNG","JPEG","BMP","TIFF"},SelectedIndex=0};
        readonly TextBox quality=new TextBox{Text="90",IsEnabled=false},destination=new TextBox();
        internal ImageToolsDialog(Window owner,string[] paths,string folder)
        {
            CopyUi.Style(this,owner,L.Text("imageTools.title"),620);var root=new DockPanel();CopyUi.SectionedSurface(this,root);
            var buttons=FluentTheme.Actions();DockPanel.SetDock(buttons,Dock.Bottom);root.Children.Add(buttons);
            var body=new StackPanel();root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=Math.Max(220,MaxHeight-140)});
            body.Children.Add(CopyUi.Label(L.Format("imageTools.count",paths.Length)));body.Children.Add(CopyUi.Label(L.Text("imageBatch.help")));
            body.Children.Add(CopyUi.Label(L.Text("imageTools.rotate")));body.Children.Add(rotate);
            body.Children.Add(CopyUi.Label(L.Text("imageTools.resize")));
            ResizeChoice.ItemsSource=new[]{L.Text("imageTools.noResize"),L.Text("imageTools.maxEdge"),L.Text("imageTools.percent"),L.Text("imageBatch.exact"),L.Text("imageBatch.widthOnly"),L.Text("imageBatch.heightOnly")};ResizeChoice.SelectedIndex=1;body.Children.Add(ResizeChoice);
            var amountLabel=CopyUi.Label(L.Text("imageTools.maxEdge"));body.Children.Add(amountLabel);body.Children.Add(AmountInput);
            var dimensions=new Grid{Margin=new Thickness(0,4,0,6)};dimensions.ColumnDefinitions.Add(new ColumnDefinition());dimensions.ColumnDefinitions.Add(new ColumnDefinition());
            var widthPanel=new StackPanel{Margin=new Thickness(0,0,8,0)};widthPanel.Children.Add(CopyUi.Label(L.Text("imageBatch.width")));widthPanel.Children.Add(WidthInput);dimensions.Children.Add(widthPanel);
            var heightPanel=new StackPanel();heightPanel.Children.Add(CopyUi.Label(L.Text("imageBatch.height")));heightPanel.Children.Add(HeightInput);Grid.SetColumn(heightPanel,1);dimensions.Children.Add(heightPanel);body.Children.Add(dimensions);
            var sizeHelp=CopyUi.Label("");body.Children.Add(sizeHelp);
            Action update=delegate{int mode=ResizeChoice.SelectedIndex;AmountInput.Visibility=amountLabel.Visibility=mode==1||mode==2?Visibility.Visible:Visibility.Collapsed;amountLabel.Text=L.Text(mode==2?"imageTools.percent":"imageTools.maxEdge");dimensions.Visibility=mode>=3?Visibility.Visible:Visibility.Collapsed;WidthInput.IsEnabled=mode==3||mode==4;HeightInput.IsEnabled=mode==3||mode==5;WidthInput.Text=mode==5?"":String.IsNullOrWhiteSpace(WidthInput.Text)?"1920":WidthInput.Text;HeightInput.Text=mode==4?"":String.IsNullOrWhiteSpace(HeightInput.Text)?"1080":HeightInput.Text;sizeHelp.Text=L.Text(mode==3?"imageBatch.exactHelp":mode>=4?"imageBatch.ratioHelp":"imageBatch.resizeHelp");};
            ResizeChoice.SelectionChanged+=delegate{AmountInput.Text=ResizeChoice.SelectedIndex==2?"50":"1920";update();};update();
            body.Children.Add(CopyUi.Label(L.Text("imageTools.format")));body.Children.Add(format);body.Children.Add(CopyUi.Label(L.Text("imageTools.quality")));body.Children.Add(quality);format.SelectionChanged+=delegate{quality.IsEnabled=format.SelectedIndex==1;};
            body.Children.Add(CopyUi.Label(L.Text("imageTools.output")));destination.Text=folder;body.Children.Add(destination);body.Children.Add(CopyUi.Button(L.Text("action.browse"),delegate{using(var picker=new System.Windows.Forms.FolderBrowserDialog{SelectedPath=destination.Text})if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)destination.Text=picker.SelectedPath;}));body.Children.Add(CopyUi.Label(L.Text("imageTools.copiesHelp")));
            var run=CopyUi.Button(L.Text("imageTools.run"),delegate{try{var options=ReadOptions();ImageTools.Plan(paths,options);Options=options;DialogResult=true;}catch(Exception ex){MessageBox.Show(this,ex.Message,Title);}});run.IsDefault=true;buttons.Children.Add(run);var cancel=CopyUi.Button(L.Text("action.cancel"),delegate{DialogResult=false;});cancel.IsCancel=true;buttons.Children.Add(cancel);
        }
        internal ImageToolOptions ReadOptions(){int mode=ResizeChoice.SelectedIndex,value=1920,jpeg=90,width=1920,height=1080;if((mode==1||mode==2)&&!Int32.TryParse(AmountInput.Text,out value)||format.SelectedIndex==1&&!Int32.TryParse(quality.Text,out jpeg)||(mode==3||mode==4)&&!Int32.TryParse(WidthInput.Text,out width)||(mode==3||mode==5)&&!Int32.TryParse(HeightInput.Text,out height))throw new IOException(L.Text("imageTools.invalidOptions"));return new ImageToolOptions{Rotation=rotate.SelectedIndex*90,ResizeMode=mode,MaxEdge=mode==1?value:1920,Percent=mode==2?value:50,Width=width,Height=height,Format=(string)format.SelectedItem,Quality=jpeg,OutputFolder=Path.GetFullPath(destination.Text)};}
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
