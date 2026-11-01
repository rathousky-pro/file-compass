#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace FileCompass {
    internal static class ArchiveInteractionTests {
        static void Escape(UIElement target){target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});}
        static void Open(Window window){window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-18000;window.Top=-18000;window.ShowActivated=false;window.ShowInTaskbar=false;window.Show();window.UpdateLayout();}
        internal static int Run(string root){
            Directory.CreateDirectory(root);var checks=new List<string>();var windows=new List<Window>();string oldSettings=ArchiveSettings.TestPath;
            Action<bool,string> check=(yes,message)=>{if(!yes)throw new Exception(message);checks.Add(message);File.WriteAllLines(Path.Combine(root,"progress.txt"),checks);};
            try{
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));FluentTheme.Ensure();L.ChangeLanguage("cs");
                ArchiveSettings.TestPath=Path.Combine(root,"archive-settings.xml");var settings=new ArchiveSettings{MaximumMemoryMiB=512};settings.Save();
                var random=new Random(828);var words=Enumerable.Range(0,1200).Select(i=>new string(Enumerable.Range(0,random.Next(4,140)).Select(n=>(char)random.Next(32,126)).ToArray())).ToArray();var corpus=new StringBuilder();
                for(int i=0;i<35000;i++){corpus.Append("<row id=\"").Append(i%731).Append("\" value=\"").Append(words[random.Next(words.Length)]).Append("\" category=\"").Append(i%19).Append("\"/>\n");}
                string source=Path.Combine(root,"corpus.txt");File.WriteAllText(source,corpus.ToString());var sizes=new Dictionary<int,long>();
                foreach(int level in new[]{0,1,6,9}){
                    string target=Path.Combine(root,"quality-"+level+".zip");ArchiveEngine.Create(target,new[]{source},settings,CancellationToken.None,null,new ArchivePackOptions{Level=level});
                    ArchiveEngine.Test(ArchiveEngine.Open(target),CancellationToken.None);sizes[level]=new FileInfo(target).Length;
                }
                check(sizes[0]>sizes[1]&&sizes[1]>=sizes[6]&&sizes[9]<sizes[6],"ZIP store/fast/high/ultra use real codec quality; representative corpus is smaller at 9 than 6 ("+String.Join(", ",sizes.Select(x=>x.Key+"="+x.Value))+")");
                settings.WinRarPath=ArchiveSettings.FindWinRar()??"";settings.Save();
                foreach(string extension in String.IsNullOrEmpty(settings.WinRarPath)?new[]{"7z","gz","bz2","xz"}:new[]{"7z","gz","bz2","xz","rar"})foreach(int level in new[]{6,9}){
                    string target=Path.Combine(root,"quality-"+level+"."+extension);ArchiveEngine.Create(target,new[]{source},settings,CancellationToken.None,null,new ArchivePackOptions{Level=level});var document=ArchiveEngine.Open(target);ArchiveEngine.Test(document,CancellationToken.None);
                    string extracted=Directory.CreateDirectory(Path.Combine(root,"roundtrip-"+extension+"-"+level)).FullName;ArchiveEngine.Extract(document,document.Items.Select(x=>x.Path),extracted,CancellationToken.None);check(FileParts.Hash(Directory.GetFiles(extracted,"*",SearchOption.AllDirectories).Single(),CancellationToken.None)==FileParts.Hash(source,CancellationToken.None),extension+" quality "+level+" produces a verified archive and restores the exact source bytes");
                }
                check(ArchiveCompressionQuality.Rar(6)=="-m3"&&ArchiveCompressionQuality.Rar(9)=="-m5"&&ArchiveCompressionQuality.Arj(6)=="-m2"&&ArchiveCompressionQuality.Arj(9)=="-m1"&&ArchiveCompressionQuality.Arj(0)=="-m0","RAR and ARJ receive their actual supported quality switches, including ARJ's reversed scale");
                string folder=Directory.CreateDirectory(Path.Combine(root,"locked-fixture")).FullName,good=Path.Combine(folder,"good.txt"),locked=Path.Combine(folder,"locked.txt");File.WriteAllText(good,"included payload");File.WriteAllText(locked,"locked payload");Directory.CreateDirectory(Path.Combine(folder,"empty"));
                int prompted=0;var policy=new ArchiveInputPolicy{Decide=failure=>{check(failure.Path==locked,"lock decision identifies the exact source file");prompted++;return ArchiveInputChoice.Skip;}};
                using(var held=new FileStream(locked,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
                    string target=Path.Combine(root,"skip-folder.zip");ArchiveEngine.Create(target,new[]{folder},settings,CancellationToken.None,null,new ArchivePackOptions{Inputs=policy});
                    var document=ArchiveEngine.Open(target);check(prompted==1&&policy.Skipped.SetEquals(new[]{locked})&&document.Items.Any(x=>x.Path.EndsWith("good.txt"))&&!document.Items.Any(x=>x.Path.EndsWith("locked.txt"))&&document.Items.Any(x=>x.Directory&&x.Path.EndsWith("empty")),"skipping a locked folder member preserves readable files and empty directories and records the omission");
                    var directPolicy=new ArchiveInputPolicy{Decide=failure=>ArchiveInputChoice.Skip};string direct=Path.Combine(root,"skip-direct.zip");ArchiveEngine.Create(direct,new[]{good,locked},settings,CancellationToken.None,null,new ArchivePackOptions{Inputs=directPolicy});check(ArchiveEngine.Open(direct).Items.Count(x=>!x.Directory)==1,"direct-file compression excludes only the skipped member");
                    string stopped=Path.Combine(root,"stopped.zip");File.WriteAllText(stopped,"original destination");bool cancelled=false;
                    try{ArchiveEngine.Create(stopped,new[]{folder},settings,CancellationToken.None,null,new ArchivePackOptions{Inputs=new ArchiveInputPolicy{Decide=failure=>ArchiveInputChoice.Stop}});}catch(OperationCanceledException){cancelled=true;}
                    check(cancelled&&File.ReadAllText(stopped)=="original destination"&&!Directory.GetFiles(root,".FileCompass-*").Any(),"stop preserves the previous destination and removes partial archive output");
                    bool allSkipped=false;string noOutput=Path.Combine(root,"all-skipped.zip");try{ArchiveEngine.Create(noOutput,new[]{locked},settings,CancellationToken.None,null,new ArchivePackOptions{Inputs=new ArchiveInputPolicy{Decide=failure=>ArchiveInputChoice.Skip}});}catch(ArchiveAllInputsSkippedException){allSkipped=true;}
                    check(allSkipped&&!File.Exists(noOutput),"all skipped direct inputs do not replace an archive with an empty or invalid file");
                    string move=Path.Combine(root,"move-skip.zip");ArchiveEngine.Create(move,new[]{folder},settings,CancellationToken.None,null,new ArchivePackOptions{Move=true,Inputs=new ArchiveInputPolicy{Decide=failure=>ArchiveInputChoice.Skip}});
                    check(File.Exists(locked)&&!File.Exists(good)&&Directory.Exists(folder)&&ArchiveEngine.Open(move).Items.Any(x=>x.Path.EndsWith("good.txt")),"move-to-archive deletes only verified included files and retains skipped files and their containing folder");
                }
                File.WriteAllText(good,"retry payload");FileStream retryLock=new FileStream(good,FileMode.Open,FileAccess.ReadWrite,FileShare.None);int retried=0;
                try{string retry=Path.Combine(root,"retry.zip");ArchiveEngine.Create(retry,new[]{good},settings,CancellationToken.None,null,new ArchivePackOptions{Inputs=new ArchiveInputPolicy{Decide=failure=>{retryLock.Dispose();retried++;return ArchiveInputChoice.Retry;}}});check(retried==1&&ArchiveEngine.Open(retry).Items.Any(x=>x.Path=="good.txt"),"retry packs the source after its lock is released");}finally{retryLock.Dispose();}

                string rangedFolder=Directory.CreateDirectory(Path.Combine(root,"range-fixture")).FullName,ranged=Path.Combine(rangedFolder,"range.txt"),kept=Path.Combine(rangedFolder,"kept.txt");File.WriteAllText(ranged,"byte-range locked payload");File.WriteAllText(kept,"readable payload");
                using(var holder=new FileStream(ranged,FileMode.Open,FileAccess.Read,FileShare.Read)){
                    holder.Lock(0,1);bool rangeLocked=true;
                    try{
                        string target=Path.Combine(root,"skip-range.zip");var rangePolicy=new ArchiveInputPolicy{Decide=failure=>ArchiveInputChoice.Skip};ArchiveEngine.Create(target,new[]{rangedFolder},settings,CancellationToken.None,null,new ArchivePackOptions{Inputs=rangePolicy});
                        var document=ArchiveEngine.Open(target);check(rangePolicy.Skipped.Contains(ranged)&&document.Items.Any(x=>x.Path.EndsWith("kept.txt"))&&!document.Items.Any(x=>x.Path.EndsWith("range.txt")),"a byte-range read lock is interactive and skipping removes the partially staged file");
                        string moved=Path.Combine(root,"move-range.zip");ArchiveEngine.Create(moved,new[]{rangedFolder},settings,CancellationToken.None,null,new ArchivePackOptions{Move=true,Inputs=new ArchiveInputPolicy{Decide=failure=>ArchiveInputChoice.Skip}});check(File.Exists(ranged)&&!File.Exists(kept),"move-to-archive also preserves files skipped during snapshot hashing because of a byte-range lock");
                        string retry=Path.Combine(root,"retry-range.zip");ArchiveEngine.Create(retry,new[]{rangedFolder},settings,CancellationToken.None,null,new ArchivePackOptions{Inputs=new ArchiveInputPolicy{Decide=failure=>{holder.Unlock(0,1);rangeLocked=false;return ArchiveInputChoice.Retry;}}});check(ArchiveEngine.Open(retry).Items.Any(x=>x.Path.EndsWith("range.txt")),"retrying a read lock replaces its partial stage and includes the complete file");
                    }finally{if(rangeLocked)holder.Unlock(0,1);}
                }
                var plainInput=new TextBox{Text="focused input"};var plain=new Window{Content=plainInput,Width=450,Height=240};windows.Add(plain);Open(plain);Escape(plainInput);check(!plain.IsVisible,"Escape closes an unstyled tool window while a text input has focus");
                bool cancelClicked=false;var cancelledWindow=new Window{Width=450,Height=240};var cancel=new Button{Content="Cancel",IsCancel=true};cancel.Click+=delegate{cancelClicked=true;cancelledWindow.Close();};cancelledWindow.Content=cancel;windows.Add(cancelledWindow);Open(cancelledWindow);Escape(cancelledWindow);check(cancelClicked&&!cancelledWindow.IsVisible,"Escape preserves the existing cancel-button decision handler");
                var guarded=new Window{Content=new TextBlock{Text="guarded operation"},Width=450,Height=240};bool running=true;int closeRequests=0;guarded.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs args){closeRequests++;args.Cancel=running;};windows.Add(guarded);Open(guarded);Escape(guarded);check(guarded.IsVisible&&closeRequests==1,"Escape respects a running operation's Closing cancellation guard");running=false;Escape(guarded);check(!guarded.IsVisible,"Escape closes the same tool after its operation can safely finish");
                var activeTool=new Window{Content=new TextBlock{Text="active tool"},Width=450,Height=240};bool active=true;activeTool.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs args){args.Cancel=active;};WindowEscape.TrackActivity(activeTool,()=>active);windows.Add(activeTool);Open(activeTool);Escape(activeTool);check(activeTool.IsVisible,"busy tool waits for its work to stop after Escape");active=false;NavigationTests.Wait(Task.Delay(300));check(!activeTool.IsVisible,"busy tool closes automatically once its cancelled operation becomes idle");
                var combo=new ComboBox{ItemsSource=new[]{"a","b"},SelectedIndex=0};var comboWindow=new Window{Content=combo,Width=450,Height=240};windows.Add(comboWindow);Open(comboWindow);combo.IsDropDownOpen=true;Escape(combo);check(comboWindow.IsVisible,"Escape in an open selector does not close its host window");combo.IsDropDownOpen=false;Escape(comboWindow);check(!comboWindow.IsVisible,"Escape closes the host after the selector is dismissed");
                var smart=new DiskSmartWindow(null,false);windows.Add(smart);Open(smart);Escape(smart);check(!smart.IsVisible,"Escape closes the SMART tool");
                var analyzer=new DiskAnalyzerWindow(null,folder,null,true);windows.Add(analyzer);Open(analyzer);NavigationTests.Wait(analyzer.ScanTask);Escape(analyzer);check(!analyzer.IsVisible,"Escape closes the space analyzer and releases its scan index");
                var converter=new MediaConverterWindow(null,new string[0],root,true,delegate{},delegate{});windows.Add(converter);Open(converter);NavigationTests.Wait(converter.AnalysisTask);Escape(converter);check(!converter.IsVisible,"Escape closes an idle image/audio converter");
                var packDialog=new ArchivePackDialog(null,2,Path.Combine(root,"dialog.zip"));bool arjEnabled=false,tarDisabled=false,lhaDisabled=false;
                packDialog.Loaded+=delegate{Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate{
                    var formats=FluentUiTests.Children<ComboBox>(packDialog).Single(x=>x.Items.Cast<object>().Any(item=>Convert.ToString(item)=="ARJ*"));
                    var quality=FluentUiTests.Children<ComboBox>(packDialog).Single(x=>x.Items.Count==10&&x.Items[0] is int);
                    formats.SelectedItem="ARJ*";arjEnabled=quality.IsEnabled;formats.SelectedItem="TAR";tarDisabled=!quality.IsEnabled;
                    formats.SelectedItem="LHA*";lhaDisabled=!quality.IsEnabled;Escape(packDialog);
                }));};packDialog.ShowDialog();check(arjEnabled&&tarDisabled&&lhaDisabled,"ARJ quality is selectable while TAR and fixed-method LHA explain their disabled control");
                var prompt=new ArchiveInputDialog(null,new ArchiveInputFailure{Path=locked,Error=new IOException("The file is used by another process.")});
                prompt.Loaded+=delegate{Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate{NextStageTests.Draw(prompt,Path.Combine(root,"locked-file-dialog.png"));Escape(prompt);}));};prompt.ShowDialog();check(prompt.Choice==ArchiveInputChoice.Stop,"Escape in the locked-file prompt selects stop, never skip");
                var main=new MainWindow(folder,new ProgramSettings{AutoRefresh=false}){Width=1100,Height=750};windows.Add(main);Open(main);NavigationTests.WaitPanels(main);Escape(main);check(main.IsVisible,"Escape does not exit the main file manager");main.Close();
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}
            finally{foreach(var window in windows.Where(x=>x.IsVisible).ToArray())window.Close();ArchiveSettings.TestPath=oldSettings;}
        }
    }
}
#endif
