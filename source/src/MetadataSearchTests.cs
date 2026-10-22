using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace CommanderCZ {
    internal static class MetadataSearchTests {
        static FileSearchOptions Options(string root,string field,string op,string value){return new FileSearchOptions{Root=root,MetadataConditions=new[]{new MetadataSearchCondition{Field=field,Operator=op,Value=value}}};}
        static SearchHit[] Find(FileSearchOptions options,List<string> warnings=null,CancellationToken token=default(CancellationToken)){
            var hits=new List<SearchHit>();FileSearchEngine.Local(options,new string[0],token,hits.Add,(warnings??new List<string>()).Add);return hits.ToArray();
        }
        static void Image(string path,int width){
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width,10,96,96,PixelFormats.Bgra32,null,new byte[width*10*4],width*4)));using(var s=File.Create(path))encoder.Save(s);
        }
        static void Pdf(string path,int pages,string author){
            var builder=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();builder.DocumentInformation.Author=author;
            for(int i=0;i<pages;i++)builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);File.WriteAllBytes(path,builder.Build());
        }
        static void Wav(string path,int seconds){
            using(var w=new BinaryWriter(File.Create(path))){w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+seconds);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(1);w.Write(1);w.Write((short)1);w.Write((short)8);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(seconds);w.Write(new byte[seconds]);}
        }
        static async Task CheckWindowSearch(FileSearchWindow window,Action<bool,string> check) {
            var start=AdvancedToolsTests.Visual<Button>(window).First(x=>Convert.ToString(x.Content)==L.Text("action.search"));
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(!start.IsEnabled,"Search dialog starts metadata work asynchronously");
            var clock=System.Diagnostics.Stopwatch.StartNew();
            while(!start.IsEnabled&&clock.ElapsedMilliseconds<25000)await Task.Delay(50);
            check(start.IsEnabled,"Metadata search dialog completes without blocking the UI");
            await Task.Delay(100);
            var list=AdvancedToolsTests.Visual<ListView>(window).Single();var hits=list.Items.Cast<SearchHit>().ToArray();
            check(hits.Length==1&&hits[0].Name=="long.pdf"&&hits[0].Metadata.Contains("Alice"),"Dialog search applies the combined metadata filters and displays the matching values");
        }
        internal static int Run(string root){
            Directory.CreateDirectory(root);var checks=new List<string>();string previous=SearchAdvanced.TestDirectory;FileSearchWindow window=null;
            Action<bool,string> check=(valid,label)=>{if(!valid)throw new Exception(label);checks.Add(label);File.AppendAllText(Path.Combine(root,"progress.txt"),label+Environment.NewLine);};
            try{
                string files=Path.Combine(root,"files");Directory.CreateDirectory(files);
                Image(Path.Combine(files,"4000.png"),4000);Image(Path.Combine(files,"4001.png"),4001);
                Pdf(Path.Combine(files,"short.pdf"),2,"Jan Novák");Pdf(Path.Combine(files,"long.pdf"),21,"Alice Smith");
                Wav(Path.Combine(files,"hour.wav"),3600);Wav(Path.Combine(files,"long.wav"),3601);
                File.WriteAllText(Path.Combine(files,"plain.txt"),"Jan Novák");
                var o=Options(files,"width","gt","4000");int inspected=0;o.MetadataInspecting=p=>inspected++;
                var hits=Find(o);check(hits.Length==1&&hits[0].Name=="4001.png","Width greater than 4000 px searches actual image metadata");
                check(inspected==4,"Width search skips unrelated documents and text before metadata reads");
                check(hits[0].Metadata.Contains("4001"),"Search results include the matching metadata value");
                o=Options(files,"width","ge","4000");check(Find(o).Length==2,"Inclusive width boundary is supported");
                o=Options(files,"pages","gt","20");hits=Find(o);check(hits.Length==1&&hits[0].Name=="long.pdf","PDF page counts are read from the actual document");
                o=Options(files,"author","contains","novák");hits=Find(o);check(hits.Length==1&&hits[0].Name=="short.pdf","Author search is case insensitive and preserves Czech accents");
                o.CaseSensitive=true;check(Find(o).Length==0,"Case-sensitive metadata text matching follows the search switch");
                o=Options(files,"author","starts","Alice");check(Find(o).Single().Name=="long.pdf","Text prefix matching is supported");
                o=Options(files,"author","eq","Alice Smith");check(Find(o).Single().Name=="long.pdf","Exact author matching is supported");
                o=Options(files,"duration","gt","01:00:00");hits=Find(o);check(hits.Length==1&&hits[0].Name=="long.wav","Media duration uses time values with a strict one-hour boundary");
                var data=new FileDetails();data.Add("duration","01:00:00.5000000");string summary;
                check(MetadataSearch.Matches(data,o,out summary),"Fractional media seconds are preserved when comparing durations");
                check(MetadataSearch.CouldMatch("video.mp4",o),"Video extensions are eligible for duration searches");
                o=Options(files,"pages","gt","20");o.MetadataConditions=o.MetadataConditions.Concat(new[]{new MetadataSearchCondition{Field="author",Operator="contains",Value="Alice"}}).ToArray();
                check(Find(o).Single().Name=="long.pdf","Multiple metadata conditions are combined with AND");
                o.MetadataConditions[1].Value="Jan";check(Find(o).Length==0,"A single failing metadata condition excludes the file");
                o=Options(files,"width","gt","4000");o.Mask="*.pdf";check(Find(o).Length==0,"Filename masks and metadata filters combine");
                o=Options(files,"width","gt","4000");o.Minimum=Int64.MaxValue;check(Find(o).Length==0,"File-size limits still apply before metadata reads");
                o=Options(files,"pages","gt","20");o.SelectedOnly=true;var selected=new List<SearchHit>();FileSearchEngine.Local(o,new[]{Path.Combine(files,"long.pdf")},CancellationToken.None,selected.Add,p=>{});
                check(selected.Count==1,"Selected-only searches support metadata");
                var missing=new FileDetails();check(!MetadataSearch.Matches(missing,o,out summary),"Missing properties never satisfy an active condition");
                var cached=new FileDetails();cached.Add("cachedPages",25);check(MetadataSearch.Matches(cached,o,out summary),"Stored Office page counts can satisfy page filters");
                var date=Options(files,"taken","eq","21.10.2026");var exif=new FileDetails();exif.Add("taken",new DateTime(2026,10,21,13,45,0));check(MetadataSearch.Matches(exif,date,out summary),"Capture-date equality compares the calendar date");
                foreach(string value in new[]{"-1","NaN","Infinity","not a number"}){
                    bool rejected=false;try{MetadataSearch.Validate(Options(files,"width","gt",value));}catch(IOException){rejected=true;}check(rejected,"Invalid numeric condition rejected: "+value);
                }
                bool bad=false;try{MetadataSearch.Validate(Options(files,"duration","gt","1"));}catch(IOException){bad=true;}check(bad,"Ambiguous duration without a time separator is rejected");
                using(var cts=new CancellationTokenSource()){cts.Cancel();bool cancelled=false;try{Find(Options(files,"pages","gt","0"),null,cts.Token);}catch(OperationCanceledException){cancelled=true;}check(cancelled,"Cancelled search does not continue traversing or reading metadata");}
                using(var cts=new CancellationTokenSource()){var cancellationOptions=Options(files,"pages","gt","0");cancellationOptions.MetadataInspecting=p=>cts.Cancel();bool cancelled=false;try{Find(cancellationOptions,null,cts.Token);}catch(OperationCanceledException){cancelled=true;}check(cancelled,"Cancellation during metadata inspection stops the search");}
                o=Options(files,"pages","gt","20");SearchAdvanced.TestDirectory=root;SearchAdvanced.SaveProfiles(new List<SearchProfile>{new SearchProfile{Name="Metadata",Options=o}});var profile=SearchAdvanced.LoadProfiles().Single();
                check(profile.Options.MetadataConditions.Single().Value=="20"&&profile.Options.MetadataConditions[0].Field=="pages","Saved search profiles round-trip metadata conditions");
                check(!MetadataSearch.Enabled(new FileSearchOptions()),"Existing searches and old profiles have no active metadata filter");
                var ordinary=Find(new FileSearchOptions{Root=files,Mask="*.txt"});check(ordinary.Length==1,"Ordinary filename search continues to work");
                bool remote=false;try{FileSearchEngine.Remote(o,new string[0],null,CancellationToken.None,p=>{});}catch(IOException){remote=true;}check(remote,"Unsupported remote searches reject active metadata filters explicitly");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                window=new FileSearchWindow(null,files,true,false,(options,token,found,warn)=>FileSearchEngine.Local(options,new string[0],token,found,warn),p=>{}){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();
                var tabs=AdvancedToolsTests.Visual<TabControl>(window).Single();var tab=tabs.Items.Cast<TabItem>().Single(x=>Convert.ToString(x.Header)==L.Text("metaSearch.tab"));tabs.SelectedItem=tab;window.UpdateLayout();
                window.MetadataEditor.Restore(o.MetadataConditions);check(window.ReadSearchOptions().MetadataConditions.Single().Field=="pages","Metadata editor feeds search options");
                var preset=AdvancedToolsTests.Visual<ComboBox>(window.MetadataEditor).First();preset.SelectedIndex=1;var presetOptions=window.ReadSearchOptions();
                check(presetOptions.Mask.Contains("*.jpg")&&presetOptions.MetadataConditions.Single().Field=="width"&&presetOptions.MetadataConditions[0].Value=="4000","Photo preset configures both image masks and width condition");
                preset.SelectedIndex=4;presetOptions=window.ReadSearchOptions();check(presetOptions.Mask.Contains("*.mp4")&&presetOptions.MetadataConditions.Single().Value=="01:00:00","Video preset selects video masks and a one-hour threshold");
                preset.SelectedIndex=2; window.MetadataEditor.Restore(new[]{new MetadataSearchCondition{Field="pages",Operator="gt",Value="20"},new MetadataSearchCondition{Field="author",Operator="contains",Value="Alice"}}); NavigationTests.Wait(CheckWindowSearch(window,check));
                window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"metadata-search.png"));
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),String.Join(Environment.NewLine,checks)+Environment.NewLine+"FAIL: "+ex);return 1;}
            finally{SearchAdvanced.TestDirectory=previous;if(window!=null)window.Close();}
        }
    }
}