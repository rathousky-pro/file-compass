using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace CommanderCZ {
    internal static class MetadataColumnTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var results=new List<string>();MainWindow main=null;
            string rulePath=FolderViewRules.FilePath;byte[] previous=File.Exists(rulePath)?File.ReadAllBytes(rulePath):null;
            Action<bool,string> check=(valid,label)=>{if(!valid)throw new Exception(label);results.Add(label);File.AppendAllText(Path.Combine(root,"progress.txt"),label+Environment.NewLine);};
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                string images=Path.Combine(root,"Photos"),mixed=Path.Combine(root,"Mixed"),docs=Path.Combine(root,"Documents");
                Directory.CreateDirectory(images);Directory.CreateDirectory(mixed);Directory.CreateDirectory(docs);
                foreach(int width in new[]{100,400,900}){
                    var bitmap=BitmapSource.Create(width,100,96,96,PixelFormats.Bgra32,null,new byte[width*100*4],width*4);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(images,width+".png")))encoder.Save(stream);
                }
                File.WriteAllText(Path.Combine(mixed,"plain.txt"),"hello");
                foreach(int pages in new[]{2,10}){
                    var builder=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();builder.DocumentInformation.Author="Compass tester";
                    for(int n=0;n<pages;n++)builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
                    File.WriteAllBytes(Path.Combine(docs,pages+".pdf"),builder.Build());
                }
                main=new MainWindow(mixed,new ProgramSettings{AutoRefresh=false}){Width=1450,Height=900,ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};
                main.Show();NavigationTests.WaitPanels(main);
                NavigationTests.Wait(main.CheckMetadataColumns(root,images,mixed,docs,check));
                var settings=new ProgramSettings{AutomaticFolderViews=true};settings.LeftView.MetadataColumns=new[]{"author","pages"};settings.Save(Path.Combine(root,"settings.xml"));var loaded=ProgramSettings.Load(Path.Combine(root,"settings.xml"));
                check(loaded.AutomaticFolderViews&&loaded.LeftView.MetadataColumns.SequenceEqual(settings.LeftView.MetadataColumns),"Setting and selected columns survive XML round trip");
                check(!new ProgramSettings().AutomaticFolderViews,"Automatic views are disabled by default");
                File.WriteAllLines(Path.Combine(root,"result.txt"),results.Concat(new[]{"PASS: "+results.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),String.Join(Environment.NewLine,results)+Environment.NewLine+"FAIL: "+ex);return 1;}
            finally{if(main!=null)main.Close();if(previous==null){if(File.Exists(rulePath))File.Delete(rulePath);}else File.WriteAllBytes(rulePath,previous);}
        }
    }
    public partial class MainWindow {
        internal async Task CheckMetadataColumns(string root,string images,string mixed,string docs,Action<bool,string> check){
            await left.CheckMetadataColumns(root,images,mixed,docs,check);
            UpdateLayout();NextStageTests.Draw(this,Path.Combine(root,"columns.png"));
            var configuration=new ConfigurationDialog(this);configuration.Left=-18000;configuration.Top=-18000;configuration.Show();
            try{configuration.UpdateLayout();check(AdvancedToolsTests.Visual<CheckBox>(configuration).Any(x=>Convert.ToString(x.Content)==L.Text("columns.autoSetting")),"Configuration exposes the on/off switch");NextStageTests.Draw(configuration,Path.Combine(root,"settings.png"));}finally{configuration.Close();}
        }
    }
    public partial class FilePanel {
        internal async Task CheckMetadataColumns(string root,string images,string mixed,string docs,Action<bool,string> check){
            SetMetadataColumns(new[]{"pages","author","pages","unknown"});check(MetadataColumnKeys.SequenceEqual(new[]{"pages","author"}),"Column selection rejects unknown and duplicate keys");
            SetColumnWidth(5,187);var state=new PanelView();SavePanelView(state);SetMetadataColumns(new string[0]);RestorePanelView(state);
            check(MetadataColumnKeys.SequenceEqual(new[]{"pages","author"})&&view.Columns[5].Width==187,"Column visibility and individual width restore with the panel view");
            Navigate(docs,false);await NavigationTask;await LoadAllColumnMetadata();await Task.Delay(300);
            var pdfs=List.Items.Cast<Entry>().Where(x=>!x.IsDirectory).ToArray();
            check(pdfs.All(x=>x.MetadataValues["author"]=="Compass tester"),"Isolated metadata workers populate author cells");
            check(pdfs.Select(x=>x.MetadataValues["pages"]).OrderBy(x=>x).SequenceEqual(new[]{"10","2"}),"PDF page values populate cells");
            sortProperty="Meta:pages";descending=false;Sort();
            check(List.Items.Cast<Entry>().Where(x=>!x.IsDirectory).Select(x=>x.MetadataValues["pages"]).SequenceEqual(new[]{"2","10"}),"Page sorting is numeric, not lexical");
            descending=true;Sort();check(List.Items.Cast<Entry>().First(x=>!x.IsDirectory).MetadataValues["pages"]=="10","Descending metadata sort works");
            var unknown=new Entry{Name="missing",FullPath="missing"};var known=new Entry{Name="known",FullPath="known"};var data=new FileDetails();data.Add("pages",2);known.SetColumnMetadata(data);
            check(MetadataColumn.Compare(known,unknown,"pages",-1,true)<0,"Missing metadata stays last in descending order");
            SetMetadataColumns(new string[0]);SetDisplay("Details");sortProperty="Name";descending=false;
            Navigate(images,false);await NavigationTask;check(DisplayMode=="Details"&&MetadataColumnKeys.Length==0,"Disabled automation leaves an image folder unchanged");
            SetAutomaticFolderViews(true);check(DisplayMode=="Thumbnails"&&MetadataColumnKeys.Contains("dimensions"),"Enabled automation detects photographs and chooses thumbnails");
            SetDisplay("Details");Refresh();await NavigationTask;check(DisplayMode=="Details","Refreshing the same folder preserves a manual display choice");
            Navigate(docs,false);await NavigationTask;check(DisplayMode=="Details"&&MetadataColumnKeys.Contains("pages"),"Entering documents selects document columns");
            Navigate(mixed,false);await NavigationTask;check(DisplayMode=="Details"&&MetadataColumnKeys.Length==0,"Ordinary folders restore the baseline view");
            Navigate(images,false);await NavigationTask;SetAutomaticFolderViews(false);check(DisplayMode=="Details"&&MetadataColumnKeys.Length==0,"Disabling automation immediately restores the baseline");
            var rules=new FolderViewRules();rules.Rules.Add(new FolderViewRule{Path=root,IncludeChildren=true,View=new PanelView{Display="Short",MetadataColumns=new[]{"title"}}});rules.Rules.Add(new FolderViewRule{Path=docs,View=new PanelView{Display="Details",MetadataColumns=new[]{"author"}}});
            check(rules.Match(docs).Path==docs&&rules.Match(images).Path==root&&rules.Match(root+"-other")==null,"Folder rules prefer the nearest path and respect directory boundaries");
            rules.Save();SetAutomaticFolderViews(true);check(DisplayMode=="Short"&&MetadataColumnKeys.SequenceEqual(new[]{"title"}),"An inherited saved folder view overrides content detection");
            Navigate(docs,false);await NavigationTask;check(MetadataColumnKeys.SequenceEqual(new[]{"author"}),"An exact folder rule overrides inherited views");
            SetAutomaticFolderViews(false);
            check(MetadataColumn.SortValue("dimensions","100 × 900 px").Equals(90000d),"Resolution sorts by pixel count");
            check(MetadataColumn.SortValue("duration","00:10:00") is TimeSpan,"Media durations have typed sort values");
            check(DetectFolderKind(new[]{new Entry{Extension="png"},new Entry{Extension="png"},new Entry{Extension="png"},new Entry{Extension="txt"},new Entry{Extension="zip"}})=="images","Content detection uses the sixty percent threshold");
            check(DetectFolderKind(new[]{new Entry{Extension="png"},new Entry{Extension="txt"}})=="","Mixed folders keep the ordinary view");
            Navigate(docs,false);await NavigationTask;SetMetadataColumns(new[]{"author","pages"});SetDisplay("Details");await LoadAllColumnMetadata();Sort();
            var savedTabs=SaveTabs(); check(savedTabs.Any(x=>x.View!=null&&x.View.MetadataColumns.SequenceEqual(new[]{"author","pages"})),"Saved tabs retain their metadata columns"); var header=(GridViewColumnHeader)view.Columns[0].Header;header.ContextMenu.IsOpen=true;await Task.Delay(50);
            check(header.ContextMenu.Items.OfType<MenuItem>().Count(x=>x.IsCheckable)==MetadataColumn.Keys.Length,"Header menu exposes every supported metadata column");header.ContextMenu.IsOpen=false;
        }
    }
}